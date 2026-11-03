"""F004 T436: checks and record helpers shared by the resource and item drivers (route `comfy`)."""
from __future__ import annotations

import hashlib
import json
import sys
from pathlib import Path

HERE = Path(__file__).resolve().parent
REPO = HERE.parents[2]
ART = HERE.parent
sys.path.insert(0, str(REPO / "tools" / "art-pipeline"))
sys.path.insert(0, str(ART / "slots"))

from PIL import Image  # noqa: E402

import cross_family as cf  # noqa: E402
from art_pipeline import checks, checks_v1 as c1  # noqa: E402
from art_pipeline.color import contrast_ratio, hex_to_rgb  # noqa: E402

ART_BIBLE, BRIEF = "F004-PAB v1.0", "F004-AB v1.0"
PPU = 24
TOKENS = {"--bg": "#111827", "--surface": "#1F2937", "--surface-raised": "#374151"}   # Art Bible 9.1 and 11


def sha256_file(p: Path) -> str:
    return hashlib.sha256(Path(p).read_bytes()).hexdigest()


def write_lf(path: Path, text: str) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    with open(path, "w", encoding="utf-8", newline="\n") as fh:
        fh.write(text)


def na(name: str, reason: str) -> checks.CheckResult:
    return checks.CheckResult("not_applicable[%s]" % name, True, {"reason": reason})


def _pairs_once(img: Image.Image, palette):
    """Section 9.3 pairs, one per (outline pixel, outward neighbour): the bare-floor backdrop yields two records per pair
    (`floor_0`, `floor_1`), so only the `floor_0` record is kept."""
    return [(ink, rim) for ink, rim, tone in c1.outline_pairs(img, None, palette) if tone == "floor_0"]


def ui_surface_contrast(img: Image.Image, palette, ok_note: str = "") -> checks.CheckResult:
    """Rule C6 (Art Bible 9.4) on the three UI token surfaces: composite-mask share of section 9.3 (ink OR rim reaches 3:1,
    4-neighbour pairs, none merged), at least 90 % per surface."""
    pairs = _pairs_once(img, palette)
    rows, ok = {}, bool(pairs)
    for name, hexv in TOKENS.items():
        s = hex_to_rgb(hexv)
        hit = sum(1 for ink, rim in pairs if contrast_ratio(ink, s) >= 3 or (rim is not None and contrast_ratio(rim, s) >= 3))
        share = hit / len(pairs) if pairs else 0.0
        rows[name] = {"pairs": len(pairs), "share": round(share, 4), "passed": share >= c1.SHARE_THRESHOLD}
        ok &= share >= c1.SHARE_THRESHOLD
    return checks.CheckResult("c6_ui_surface_outline_contrast", ok, {"threshold": c1.SHARE_THRESHOLD, "surfaces": rows, "note": ok_note})


def bare_floor_contrast(img: Image.Image, palette) -> checks.CheckResult:
    r = c1.check_outline_contrast(img, None, palette)
    r.name = "outline_contrast_bare_floor"
    return r


def informational(r: checks.CheckResult, note: str) -> checks.CheckResult:
    would = r.passed
    r.passed = True
    r.details.update(gated=False, wouldPass=would, note=note)
    return r


def lattice_contrast(img: Image.Image, pivot_px_from_top: tuple[int, int], palette, tile: Image.Image,
                     plates: dict[str, Image.Image]) -> list[checks.CheckResult]:
    """The sprite placed with its pivot on the footprint centre of the real T435 floor lattice (3 phases), bare and over each
    real plate. Reported, not gated: the brief places resources and items in UI lists, not on the world floor."""
    off = (cf.CENTRE[0] - pivot_px_from_top[0], cf.CENTRE[1] - pivot_px_from_top[1])
    body = Image.new("RGBA", cf.CANVAS, (0, 0, 0, 0))
    body.paste(img, off, img)
    rows = []
    for plate_name, plate in [("bare floor", None)] + sorted(plates.items()):
        for ph in cf.PHASES:
            rows.append(cf._outline_row("%s phase %s" % (plate_name, ph), body, cf.world(plate, tile, ph), palette))
    res = cf._agg("physical_floor_and_plates_outline_contrast", rows)
    return [informational(res, "real T435 floor lattice (period 60 x 30, 3 phases) bare and over the real plates; the brief places "
                               "resources and item icons in UI lists only, so this is reported, not gated")]


def load_t435(palette) -> tuple[Image.Image, dict[str, Image.Image]]:
    tile = Image.open(ART / "floor" / "pixel" / "f004-floor-tile_00.png").convert("RGBA")
    plates = {}
    for key, name in (("empty", "f003-slot-empty"), ("locked", "f003-slot-locked-l3"), ("occupied", "f003-slot-occupied-base")):
        plates[key] = Image.open(ART / "slots" / "plates" / name / "pixel" / (name + "_00.png")).convert("RGBA")
    return tile, plates


def interactive_steps() -> list[str]:
    return [
        "Exploration (prompt wording, seed candidates) ran interactively against the local ComfyUI server under the gpu lease "
        "(helper script over the server's REST API, same graph as the committed workflow; candidate PNGs are not committed).",
        "ComfyUI MCP (comfy-mcp 0.10.0): server_info and system_stats preflight, launch_comfyui, and one run_workflow of the "
        "committed f003-res-gear workflow.json as the seed re-generation check.",
        "The committed raw PNGs were produced by `python art-source/f004/resources/comfy_gen.py final` from the committed "
        "workflow template and the seeds recorded in candidate-selection.json; they are regenerable.",
        "Candidate choice was a visual selection by the producer (contact sheets), recorded with reasons per candidate.",
    ]


# --- provenance ---------------------------------------------------------------------------------------------

PRODUCER = "Asset Artist (ComfyUI route, Art Director lens self-check; Claude Code Sonnet 5.5), T436 / Issue #550"


def provenance_record(*, asset_id: str, key: str, family: str, raw_path: Path, workflow_path: Path, seed: int, prompt: str,
                      negative: str, env: dict, selection: dict, touch_up: list[dict], pixel_pass: dict, outputs: list[dict],
                      script_hashes: dict, report_hash: str, report_path: str, extra_params: dict) -> dict:
    lora = env["lora"]
    graph = json.loads(workflow_path.read_text(encoding="utf-8"))
    k = graph["9"]["inputs"]
    return {
        "assetId": asset_id, "revision": 1, "route": "comfy", "key": key,
        "sources": [
            {"path": "art-source/f004/%s/%s/raw/%s" % (family, asset_id, raw_path.name), "sha256": sha256_file(raw_path),
             "note": "winning raw output of the committed workflow run (1024 x 1024)"},
            {"path": "art-source/f004/%s/%s/raw/workflow.json" % (family, asset_id), "sha256": sha256_file(workflow_path),
             "note": "executed API graph (the committed template with prompt, negative prompt, seed and size filled)"},
            {"path": "tools/art-pipeline/comfy/z-image-turbo-pixel-lora.json", "sha256": script_hashes["workflowTemplate"]},
            {"path": "tools/art-pipeline/models.json", "sha256": script_hashes["models.json"]}],
        "tools": {"comfyUiCore": env["comfyUiCore"], "pytorch": env["pytorch"], "gpu": env["gpu"], "comfyMcp": "0.10.0",
                  "model": {"id": "z_image_turbo_bf16", "file": env["models"]["unet"]["fileName"], "sha256": env["models"]["unet"]["sha256"],
                            "license": "Apache-2.0"},
                  "textEncoder": {"file": env["models"]["textEncoder"]["fileName"], "sha256": env["models"]["textEncoder"]["sha256"]},
                  "vae": {"file": env["models"]["vae"]["fileName"], "sha256": env["models"]["vae"]["sha256"]},
                  "lora": {"id": lora["id"], "revision": lora["version"], "sha256": lora["sha256"], "license": lora["license"],
                           "strength": [graph["4"]["inputs"]["strength_model"], graph["4"]["inputs"]["strength_clip"]],
                           "sha256VerifiedBeforeRun": True},
                  "python": sys.version.split()[0], "pillow": Image.__version__ if hasattr(Image, "__version__") else "pinned 11.3.0"},
        "parameters": {"prompt": prompt, "negativePrompt": negative, "seed": seed, "sampler": k["sampler_name"],
                       "scheduler": k["scheduler"], "steps": k["steps"], "cfg": k["cfg"], "denoise": k["denoise"],
                       "controlAfterGenerate": k["control_after_generate"], "shift": graph["5"]["inputs"]["shift"],
                       "size": [graph["8"]["inputs"]["width"], graph["8"]["inputs"]["height"]],
                       "workflowHash": sha256_file(workflow_path), "perspective": "isometric-2-1 (prompted three-quarter view)",
                       "densityD": PPU, **extra_params},
        "touchUp": touch_up, "pixelPass": pixel_pass, "outputs": outputs,
        "license": "Generated with Z-Image-Turbo (Apache-2.0) and the LoRA %s (%s, commercial use permitted). Outputs are "
                   "repository-owned (owner fjure); no public license grant is claimed." % (lora["id"], lora["license"]),
        "trainingDataNote": lora["trainingDataNote"],
        "producedBy": PRODUCER, "reviewedBy": {},
        "candidateSelection": selection,
        "comfyNodeVersions": {"core": env["comfyUiCore"], "nodes": env["nodesUsed"], "partnerNodes": env["partnerNodes"]},
        "interactiveSteps": interactive_steps(),
        "environmentRecord": "art-source/f004/resources/environment.json",
        "artBibleVersion": ART_BIBLE, "briefVersion": BRIEF,
        "checkReportHash": report_hash, "checkReportPath": report_path,
    }


def script_hashes(extra: tuple[str, ...] = ()) -> dict:
    t = REPO / "tools" / "art-pipeline"
    h = {"workflowTemplate": sha256_file(t / "comfy" / "z-image-turbo-pixel-lora.json"),
         "models.json": sha256_file(t / "models.json"),
         "pixel_pass.py": sha256_file(t / "art_pipeline" / "pixel_pass.py"),
         "checks_v1.py": sha256_file(t / "art_pipeline" / "checks_v1.py"),
         "palette-v1.json": sha256_file(t / "palette" / "palette-v1.json")}
    for rel in ("resources/comfy_gen.py", "resources/comfy_pass.py", "resources/family_common.py") + tuple(extra):
        p = ART / rel
        if p.exists():
            # LF-normalised so the hash does not depend on checkout line endings
            h[rel] = hashlib.sha256(p.read_bytes().replace(b"\r\n", b"\n")).hexdigest()
    return h
