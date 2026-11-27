"""`render-hd` route of art pipeline v2 (contracts/art-pipeline-v2.md): render, post, checks, manifest row.

Steps: (1) headless Blender renders the passes with `rig-v3.blend` and `blender/render.py` (CPU, fixed seed, denoiser off),
(2) post trims every pass to the UNION bounding box, derives one pivot, dilates the colour of fully transparent edge pixels
(so bilinear filtering and mips do not bleed black) and writes the manifest row, (3) the v2 checks run; a failure blocks the
manifest. Final pixels come only from the headless batch.

CLI: python -m art_pipeline render-hd --params asset.json --out dir [--blender exe] [--repro]
"""
from __future__ import annotations

import json
import os
import subprocess
import sys
from pathlib import Path

from PIL import Image

from . import checks_v2 as c2
from .checks import CheckResult, build_report, report_json, write_report
from .provenance import file_sha256, v2_provenance_problems

PIPELINE_DIR = Path(__file__).resolve().parent.parent
BLENDER_DIR = PIPELINE_DIR / "blender"
RIG_V3 = BLENDER_DIR / "rig-v3.blend"
RENDER_SCRIPT = BLENDER_DIR / "render.py"
DEFAULT_BLENDER = r"C:\Program Files\Blender Foundation\Blender 5.2\blender.exe"
BLENDER_PIN = "5.2.1"
PASS_NAMES = ("beauty", "shadow", "emissiveMask", "normal")
#: data-model MachineArtEntry names the colour pass `body`.
MANIFEST_PASS_NAME = {"beauty": "body", "shadow": "shadow", "emissiveMask": "emissiveMask", "normal": "normal"}
DEFAULT_PADDING = 4
DEFAULT_MEMORY_BUDGET = 8 * 1024 * 1024


# --- post ---------------------------------------------------------------------------------------------------------

def union_bbox(passes: dict[str, Image.Image]) -> tuple[int, int, int, int] | None:
    union = None
    for img in passes.values():
        box = img.convert("RGBA").getchannel("A").getbbox()
        if box:
            union = box if union is None else (min(union[0], box[0]), min(union[1], box[1]), max(union[2], box[2]), max(union[3], box[3]))
    return union


def trim_to_union(passes: dict[str, Image.Image], padding: int = DEFAULT_PADDING) -> tuple[dict[str, Image.Image], tuple[float, float]]:
    """Crop every pass to the same box (union bbox plus padding) and return the pivot in art px, top-left origin.

    The camera looks at the world origin, so the origin projects to the centre of the render canvas: that is the one pivot.
    """
    box = union_bbox(passes)
    if box is None:
        raise ValueError("no pass has any content")
    first = next(iter(passes.values()))
    crop = (box[0] - padding, box[1] - padding, box[2] + padding, box[3] + padding)
    pivot = (first.width / 2.0 - crop[0], first.height / 2.0 - crop[1])
    return {n: img.convert("RGBA").crop(crop) for n, img in passes.items()}, pivot


def dilate_colour(img: Image.Image, radius: int) -> Image.Image:
    """Give fully transparent pixels within `radius` px of content the mean colour of their filled neighbours (alpha stays 0)."""
    img = img.convert("RGBA")
    w, h = img.size
    px = img.load()
    filled = [[px[x, y][3] > 0 for x in range(w)] for y in range(h)]
    for _ in range(radius):
        updates = []
        for y in range(h):
            for x in range(w):
                if filled[y][x]:
                    continue
                acc = [0, 0, 0]
                n = 0
                for dy in (-1, 0, 1):
                    for dx in (-1, 0, 1):
                        xx, yy = x + dx, y + dy
                        if 0 <= xx < w and 0 <= yy < h and filled[yy][xx]:
                            p = px[xx, yy]
                            acc[0] += p[0]; acc[1] += p[1]; acc[2] += p[2]
                            n += 1
                if n:
                    updates.append((x, y, (acc[0] // n, acc[1] // n, acc[2] // n, 0)))
        for x, y, v in updates:
            px[x, y] = v
            filled[y][x] = True
    return img


def premultiply(img: Image.Image) -> Image.Image:
    img = img.convert("RGBA")
    out = Image.new("RGBA", img.size)
    pi, po = img.load(), out.load()
    for y in range(img.height):
        for x in range(img.width):
            r, g, b, a = pi[x, y]
            po[x, y] = (round(r * a / 255), round(g * a / 255), round(b * a / 255), a)
    return out


def frame_strip(frames: list[Image.Image]) -> Image.Image:
    strip = Image.new("RGBA", (frames[0].width * len(frames), frames[0].height))
    for i, f in enumerate(frames):
        strip.paste(f, (i * f.width, 0))
    return strip


def post_process(raw: dict[str, Image.Image], padding: int = DEFAULT_PADDING, dilate: int = 2) -> tuple[dict[str, Image.Image], tuple[float, float]]:
    trimmed, pivot = trim_to_union(raw, padding)
    for name in ("beauty", "normal"):
        if name in trimmed and dilate:
            trimmed[name] = dilate_colour(trimmed[name], dilate)
    return trimmed, pivot


# --- manifest row and provenance ------------------------------------------------------------------------------------

def manifest_row(params: dict, passes: dict[str, Image.Image], pivot_px: tuple[float, float], provenance_ref: str) -> dict:
    w, h = next(iter(passes.values())).size
    # Params give anchors relative to the pivot in art px (x right, y up); the row stores them in canvas px, top-left origin.
    def to_canvas(p):
        return [round(pivot_px[0] + p[0], 4), round(pivot_px[1] - p[1], 4)]
    row = {
        "machineDefinitionId": params["machineDefinitionId"],
        "look": params["look"],
        "stage": params.get("stage", 0),
        "canvas": [w, h],
        "pivot": [round(pivot_px[0] / w, 6), round(1.0 - pivot_px[1] / h, 6)],  # normalised, origin bottom-left (Unity)
        "pivotPx": [round(pivot_px[0], 4), round(pivot_px[1], 4)],
        "footprint": params.get("footprint"),
        "indicatorAnchor": to_canvas(params["indicatorAnchor"]) if "indicatorAnchor" in params else None,
        "effectAnchors": [to_canvas(p) for p in params.get("effectAnchors", [])],
        "frames": params.get("frames", 1),
        "fps": params.get("fps", 1),
        "passes": [MANIFEST_PASS_NAME[n] for n in PASS_NAMES if n in passes],
        "textureFormat": params.get("textureFormat", {}),
        "provenanceRef": provenance_ref,
    }
    return row


def build_provenance(params: dict, rig_path: Path, render_info: dict, passes: list[str]) -> dict:
    prov = {
        "look": params["look"],
        "pipeline": params.get("pipeline", "v2-prototype"),
        "route": "render-hd",
        "renderPasses": passes,
        "rigVersion": {"version": "rig-v3", "sha256": file_sha256(rig_path)},
        "textureFormat": params.get("textureFormat", {}),
        "generatedTextures": params.get("generatedTextures", []),
        "artDirectionBrief": params.get("artDirectionBrief", {"path": "none", "version": "none (prototype brief)"}),
        "artBibleVersion": params.get("artBibleVersion", "none (prototype brief)"),
        "tools": render_info.get("tools", {}),
        "parameters": render_info.get("parameters", {}),
    }
    return prov


# --- running Blender ------------------------------------------------------------------------------------------------

def blender_exe(explicit: str | None = None) -> str:
    return explicit or os.environ.get("BLENDER_EXE") or DEFAULT_BLENDER


def run_blender(params_path: Path, out_dir: Path, exe: str, threads: int | None = None) -> dict:
    out_dir.mkdir(parents=True, exist_ok=True)
    cmd = [exe, "-b", str(RIG_V3), "--factory-startup", "--python-exit-code", "1", "-P", str(RENDER_SCRIPT), "--",
           "--params", str(params_path), "--out", str(out_dir)]
    if threads:
        cmd += ["--threads", str(threads)]
    proc = subprocess.run(cmd, capture_output=True, text=True, timeout=1800)
    if proc.returncode != 0 or "RENDER-OK" not in proc.stdout:
        raise RuntimeError("Blender render failed (exit %s):\n%s\n%s" % (proc.returncode, proc.stdout[-2000:], proc.stderr[-2000:]))
    name = json.loads(Path(params_path).read_text(encoding="utf-8"))["assetId"]
    return json.loads((out_dir / (name + ".render-info.json")).read_text(encoding="utf-8"))


def load_raw_passes(out_dir: Path, asset_id: str, names: list[str]) -> dict[str, Image.Image]:
    return {n: Image.open(out_dir / ("%s.%s.png" % (asset_id, n))).convert("RGBA") for n in names}


# --- the route --------------------------------------------------------------------------------------------------------

def run_checks(params: dict, passes: dict[str, Image.Image], pivot_px: tuple[float, float], row: dict) -> list[CheckResult]:
    results = list(c2.check_v2_geometry([passes["beauty"]] * row["frames"], row))
    results.append(c2.check_pass_alignment(passes, {n: pivot_px for n in passes}, padding=params.get("padding", DEFAULT_PADDING)))
    results.append(c2.check_key_colour_absent(list(passes.values()), tuple(params.get("keyColour", c2.CAPTURE_KEY_RGB))))
    if "emissiveMask" in passes:
        results.append(c2.check_flash_composite([passes["beauty"]] * max(2, row["frames"]), [passes["emissiveMask"]] * max(2, row["frames"]), max(1, row["fps"])))
    if "indicatorAnchor" in params and params.get("hitPolygon"):
        anchor = tuple(row["indicatorAnchor"])
        hit = [(pivot_px[0] + p[0], pivot_px[1] - p[1]) for p in params["hitPolygon"]]
        results.append(c2.check_indicator_anchor(passes["beauty"], anchor, hit))
        ind = params.get("indicator", {})
        if ind:
            results.append(c2.check_indicator_contrast(passes["beauty"], anchor, ind.get("radius", 4), tuple(ind["fill"]), tuple(ind["outline"]) if ind.get("outline") else None))
    fmt = params.get("textureFormat", {})
    rows = []
    for n, img in passes.items():
        f = fmt.get("android", "ASTC_6x6")
        if n == "shadow":
            f = "A8"
        rows.append({"name": n, "width": img.width, "height": img.height, "format": f, "mips": params.get("mips", False),
                     "frames": row["frames"], "material": {"beauty": "body", "shadow": "shadow-multiply", "emissiveMask": "emissive-additive"}.get(n, "body")})
    results.append(c2.check_texture_memory(rows, int(params.get("memoryBudgetBytes", DEFAULT_MEMORY_BUDGET))))
    gen = params.get("generatedInputs", [])
    for rec in gen:
        results.append(c2.check_prompt_lint(rec))
    return results


def run(params_path: Path, out_dir: Path, exe: str | None = None, repro: bool = False) -> int:
    params = json.loads(Path(params_path).read_text(encoding="utf-8"))
    asset_id = params["assetId"]
    exe = blender_exe(exe)
    raw_dir = out_dir / "raw"
    info = run_blender(Path(params_path), raw_dir, exe)
    passes_wanted = params.get("passes", ["beauty", "shadow", "emissiveMask"])
    raw = load_raw_passes(raw_dir, asset_id, passes_wanted)
    passes, pivot = post_process(raw, params.get("padding", DEFAULT_PADDING), params.get("dilate", 2))
    out_dir.mkdir(parents=True, exist_ok=True)
    for n, img in passes.items():
        img.save(out_dir / ("%s.%s.png" % (asset_id, n)))
    prov_name = asset_id + ".provenance.json"
    row = manifest_row(params, passes, pivot, prov_name)
    prov = build_provenance(params, RIG_V3, info, list(passes_wanted))
    results = run_checks(params, passes, pivot, row)
    problems = v2_provenance_problems(prov)
    results.append(CheckResult("provenance_v2", not problems, {"problems": problems}))
    if repro:
        raw2_dir = out_dir / "raw-repro"
        run_blender(Path(params_path), raw2_dir, exe, threads=1)
        for n in passes_wanted:
            a = raw[n]
            b = Image.open(raw2_dir / ("%s.%s.png" % (asset_id, n))).convert("RGBA")
            r = c2.check_reproducibility(a, b)
            results.append(CheckResult("reproducibility_" + n, r.passed, r.details))
    report = build_report({asset_id: [r for r in results]}, [])
    write_report(str(out_dir / (asset_id + ".check-report.json")), report)
    if report["passed"]:
        (out_dir / (asset_id + ".manifest.json")).write_text(json.dumps(row, indent=2, sort_keys=True) + "\n", encoding="utf-8")
        (out_dir / prov_name).write_text(json.dumps(prov, indent=2, sort_keys=True) + "\n", encoding="utf-8")
    sys.stdout.write("render-hd %s: %s\n" % (asset_id, "all checks pass" if report["passed"] else "FAILING (manifest blocked)"))
    return 0 if report["passed"] else 1


def main(args) -> int:
    return run(Path(args.params), Path(args.out), args.blender, args.repro)
