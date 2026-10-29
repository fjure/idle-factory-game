"""F004 style spike S2 (T418) driver: models -> Blender renders -> pixel pass -> checks -> provenance.

Run from the repository root (BLENDER_EXE optional):

  uv run --frozen --offline --project tools/art-pipeline python art-source/f004/spike/s2/pipeline_s2.py

S2 renders the S1 set on the winning projection (B, isometric 2:1, owner decision T417) at D = 16 and D = 24.
Outputs under art-source/f004/spike/s2/: models/D<k>/ (one model .blend per asset, frame/state 00 only),
raw/D<k>/ (Blender renders, 128 px), pixel/D<k>/ (pixel-pass frames), sheets/D<k>/ (shuffled grayscale/CVD
review sheets), provenance/D<k>/ (one record per asset state), candidates-D<k>.json, check-report.json (checks,
silhouette IoU and the palette v0 group budgets per density, plus a re-render reproducibility sample).
Spike candidates only; nothing here is a shipped manifest.
"""
from __future__ import annotations

import hashlib
import json
import os
import shutil
import subprocess
import sys
import tempfile
import time
from pathlib import Path

HERE = Path(__file__).resolve().parent
REPO = HERE.parents[3]
sys.path.insert(0, str(REPO / "tools" / "art-pipeline"))

from PIL import Image  # noqa: E402

from art_pipeline import checks  # noqa: E402
from art_pipeline.palette import load_palette  # noqa: E402
from art_pipeline.pixel_pass import PassConfig, build_manifest, pixel_hash, run_pixel_pass, write_frames  # noqa: E402
from art_pipeline.provenance import load_models, missing_provenance_fields, validate_models  # noqa: E402

BLENDER = os.environ.get("BLENDER_EXE") or r"C:\Program Files\Blender Foundation\Blender 5.2\blender.exe"
RIG = REPO / "tools" / "art-pipeline" / "blender" / "rig-v1.blend"
RENDER = REPO / "tools" / "art-pipeline" / "blender" / "render.py"
PROJ = "isometric-2-1"
DENSITIES = (16, 24)
FPS = 8
FRAMES = 6
SEED = 418

# footprint px = projected ground footprint of the 2x2 cell slot (rig-v1 isometric camera; the subject is scaled
# to D px per cell): D = 24 is the S1 table, D = 16 is 2/3 of it (68 x 30 -> 45 x 20, 20 x 9 -> 13 x 6).
FOOT_SLOT = {24: (68, 30), 16: (45, 20)}
FOOT_RES = {24: (20, 9), 16: (13, 6)}
CANVAS_SLOT = {24: (80, 64), 16: (54, 44)}
CANVAS_RES = {24: (32, 24), 16: (22, 16)}

GROUPS_MACHINE = {"machine", "ink"}
GROUPS_PLATE = {"floor", "ui", "machine", "ink"}
GROUPS_RES = {"resources", "ink"}
QUANT_MACHINE = {"machine"}
QUANT_PLATE = {"floor", "ui", "machine"}
QUANT_RES = {"resources"}

# family -> (list of (state, frame asset ids)), canvas, footprint table, groups
FAMILIES = {
    "miner": {"kind": "machine", "states": {
        "run": ["s2-miner-run-%02d" % i for i in range(FRAMES)],
        "paused": ["s2-miner-paused-00"], "warning": ["s2-miner-warning-00"], "inactive": ["s2-miner-inactive-00"]}},
    "assembler": {"kind": "machine", "states": {
        "run": ["s2-assembler-run-%02d" % i for i in range(FRAMES)],
        "paused": ["s2-assembler-paused-00"], "warning": ["s2-assembler-warning-00"],
        "inactive": ["s2-assembler-inactive-00"]}},
    "slots": {"kind": "plate", "states": {s: ["s2-slot-%s-00" % s] for s in ("empty", "locked", "occupied", "selected")}},
    "res-ore": {"kind": "res", "states": {"": ["s2-res-ore-00"]}},
    "res-ingot": {"kind": "res", "states": {"": ["s2-res-ingot-00"]}},
}


def kind_table(d):
    return {
        "machine": (CANVAS_SLOT[d], FOOT_SLOT[d], QUANT_MACHINE, GROUPS_MACHINE),
        "plate": (CANVAS_SLOT[d], FOOT_SLOT[d], QUANT_PLATE, GROUPS_PLATE),
        "res": (CANVAS_RES[d], FOOT_RES[d], QUANT_RES, GROUPS_RES),
    }


def write_lf(path: Path, text: str) -> None:
    """Text output is always UTF-8 with LF, so hashes equal the bytes git stores (.gitattributes: eol=lf)."""
    with open(path, "w", encoding="utf-8", newline="\n") as fh:
        fh.write(text)


def sha256_file(p: Path) -> str:
    return hashlib.sha256(p.read_bytes()).hexdigest()


def run(cmd, **kw):
    r = subprocess.run(cmd, capture_output=True, text=True, **kw)
    if r.returncode != 0:
        raise SystemExit("command failed: %s\n%s\n%s" % (" ".join(map(str, cmd)), r.stdout[-2000:], r.stderr[-2000:]))
    return r.stdout


def palette_budget_report(palette, density_frames):
    """Palette v0 group-budget figures for one density: distinct colors used per group across the whole set
    (union of every frame) against the group budget, per-asset maxima, the reserved cue group, and the cap."""
    from art_pipeline.palette import RESERVED_CUE_GROUP
    union: dict[str, set] = {}
    per_asset_max: dict[str, int] = {}
    unknown = set()
    for _aid, frames in density_frames.items():
        local: dict[str, set] = {}
        for f in frames:
            for px in set(f.convert("RGBA").getdata()):
                if px[3] == 0:
                    continue
                rgb = tuple(px[:3])
                g = palette.group_of(rgb)
                if g is None:
                    unknown.add(rgb)
                    continue
                union.setdefault(g, set()).add(rgb)
                local.setdefault(g, set()).add(rgb)
        for g, cols in local.items():
            per_asset_max[g] = max(per_asset_max.get(g, 0), len(cols))
    groups = {g: {"budget": palette.budgets[g], "usedInSet": len(union.get(g, ())),
                  "maxPerAsset": per_asset_max.get(g, 0)} for g in sorted(palette.budgets)}
    used_total = sum(len(v) for v in union.values())
    passed = (not unknown and all(v["usedInSet"] <= v["budget"] for v in groups.values())
              and not union.get(RESERVED_CUE_GROUP) and used_total <= palette.cap)
    return {"passed": passed, "groups": groups, "usedTotal": used_total, "cap": palette.cap,
            "unknownColors": len(unknown), "reservedCueGroupUsed": len(union.get(RESERVED_CUE_GROUP, ()))}


def render_one(blend: Path, out_dir: Path, name: str) -> dict:
    run([BLENDER, "-b", str(RIG), "--factory-startup", "--python-exit-code", "1", "-P", str(RENDER), "--",
         str(blend), "--projection", PROJ, "--out", str(out_dir), "--name", name])
    stub_path = out_dir / (name + ".provenance.json")
    stub = json.loads(stub_path.read_text(encoding="utf-8"))
    stub_path.unlink()
    return stub


OPEN_ITEMS = [
    "Half-art-pixel pivots: the footprint is centered on the canvas by integer division, so an odd "
    "canvas-minus-footprint difference leaves the pivot on a half art pixel (owner decision T417 condition 4; "
    "resolved in the Art Bible rules, T424). Not redefined here.",
    "D = 16 uses the unchanged S1 geometry scaled by 16/24: features thinner than about 0.06 cell (spokes, rims, "
    "brackets) fall to sub-pixel width and are lost or merged by the pixel pass; no redraw or touch-up was done "
    "(spike evidence, not a production asset).",
]


def main() -> int:
    palette = load_palette()
    tmp = Path(tempfile.mkdtemp(prefix="s2-models-"))
    try:
        for out_name in ("models", "raw", "pixel", "sheets", "provenance"):
            if (HERE / out_name).exists():
                shutil.rmtree(HERE / out_name)
        script_hashes = {"build_models.py": sha256_file(HERE / "build_models.py"),
                         "pipeline_s2.py": sha256_file(Path(__file__)),
                         "render.py": sha256_file(RENDER),
                         "pixel_pass.py": sha256_file(REPO / "tools/art-pipeline/art_pipeline/pixel_pass.py"),
                         "palette-v0.json": sha256_file(REPO / "tools/art-pipeline/palette/palette-v0.json"),
                         "rig-v1.blend": sha256_file(RIG)}
        flat: dict[str, list] = {}
        iou_report: dict[str, dict] = {}
        budget_report: dict[str, dict] = {}
        repro: dict[str, dict] = {}
        match_s1: dict[str, dict] = {}
        timings: dict[str, dict] = {}
        global_checks = [checks.check_palette_definition(palette)]
        problems = validate_models(load_models())
        global_checks.append(checks.CheckResult("models", not problems, {"problems": problems}))
        incomplete: list = []
        candidates: dict[int, list] = {}

        for d in DENSITIES:
            tag = "D%d" % d
            work = tmp / tag
            t0 = time.perf_counter()
            run([BLENDER, "-b", "--factory-startup", "--python-exit-code", "1", "-P", str(HERE / "build_models.py"),
                 "--", str(work), str(d)])
            build_seconds = round(time.perf_counter() - t0, 2)
            models_out = HERE / "models" / tag
            models_out.mkdir(parents=True)
            for blend in sorted(work.glob("*.blend")):
                if blend.stem.endswith("-00") or blend.stem.endswith("-run-00"):
                    shutil.copy2(blend, models_out / blend.name)
            raw_dir = HERE / "raw" / tag
            raw_dir.mkdir(parents=True)
            stubs = {}
            render_total = 0.0
            for blend in sorted(work.glob("*.blend")):
                stubs[blend.stem] = render_one(blend, raw_dir, blend.stem)
                render_total += stubs[blend.stem]["renderSeconds"]
            timings[tag] = {"buildModelsSeconds": build_seconds, "totalRenderSeconds": round(render_total, 2)}

            # reproducibility sample: re-render one asset into a scratch dir, compare the PNG bytes
            probe = "s2-miner-run-00"
            again = tmp / ("again-" + tag)
            again.mkdir()
            render_one(work / (probe + ".blend"), again, probe)
            repro[tag] = {"asset": probe, "firstSha256": sha256_file(raw_dir / (probe + ".png")),
                          "secondSha256": sha256_file(again / (probe + ".png"))}
            repro[tag]["byteIdentical"] = repro[tag]["firstSha256"] == repro[tag]["secondSha256"]

            sprites: dict[str, Image.Image] = {}
            all_frames: dict[str, list] = {}
            masks: dict[str, dict[str, Image.Image]] = {}
            entries: list[dict] = []
            table = kind_table(d)
            for fam, spec in FAMILIES.items():
                canvas, footprint, quant, allowed = table[spec["kind"]]
                order = [(state, i, aid) for state, ids in spec["states"].items() for i, aid in enumerate(ids)]
                raws = [Image.open(raw_dir / (aid + ".png")).convert("RGBA") for _s, _i, aid in order]
                cfg = PassConfig(route="blender", canvas=canvas, footprint=footprint, supersample=1,
                                 quant_groups=quant, outline=True, ink_id="ink_0")
                frames, placement = run_pixel_pass(raws, palette, cfg)
                pos = 0
                for state, ids in spec["states"].items():
                    sf = frames[pos:pos + len(ids)]
                    pos += len(ids)
                    prefix = {"slots": "s2-slot"}.get(fam, "s2-" + fam)
                    asset_id = "%s-%s" % (prefix, state) if state else prefix
                    paths = write_frames(sf, asset_id, HERE / "pixel" / tag)
                    entry = {"assetId": asset_id, "route": "blender", "ppu": d, "fps": FPS if len(sf) > 1 else 1,
                             "canvas": list(placement.canvas), "footprintPx": list(placement.footprint),
                             "pivot": list(placement.pivot), "perspective": PROJ,
                             "palette": {"id": palette.palette_id, "version": palette.version},
                             "frames": [{"file": p.relative_to(HERE).as_posix(), "sha256": sha256_file(p)} for p in paths]}
                    res = checks.run_asset_checks(sf, entry, palette, expected_frames=len(ids), ppu=d,
                                                  allowed_groups=allowed, running_loop=(state == "run"))
                    flat["%s/%s" % (tag, asset_id)] = res
                    sprites[asset_id] = sf[0]
                    all_frames[asset_id] = sf
                    if fam in ("miner", "assembler"):
                        masks.setdefault(fam, {})[state] = checks.silhouette_mask(sf[0])
                    # D24 must equal the S1 isometric set pixel for pixel (same models, same rig, same pass)
                    if d == 24:
                        s1_dir = HERE.parent / "s1" / "pixel" / PROJ
                        same = []
                        for i, f in enumerate(sf):
                            p1 = s1_dir / ("s1" + asset_id[2:] + "_%02d.png" % i)
                            same.append(p1.exists() and pixel_hash(Image.open(p1)) == pixel_hash(f))
                        match_s1[asset_id] = {"frames": len(same), "identical": sum(same)}

                    frame_stubs = [stubs[a] for a in ids]
                    first = frame_stubs[0]
                    rec = {
                        "assetId": asset_id, "revision": 1, "route": "blender",
                        "sources": [{"path": "art-source/f004/spike/s2/build_models.py",
                                     "sha256": script_hashes["build_models.py"],
                                     "note": "S1 procedural geometry (no external assets) scaled by D/24 about the "
                                             "footprint center; per-frame models are generated, the frame/state 00 "
                                             ".blend is committed under models/%s/" % tag},
                                    {"path": "tools/art-pipeline/blender/rig-v1.blend",
                                     "sha256": script_hashes["rig-v1.blend"]}]
                                   + [{"path": "models/%s/%s.blend" % (tag, a),
                                       "sha256": sha256_file(models_out / (a + ".blend"))}
                                      for a in ids if (models_out / (a + ".blend")).exists()],
                        "tools": {**first["tools"], "python": sys.version.split()[0],
                                  "pillow": __import__("PIL").__version__},
                        "parameters": {**first["parameters"], "perspective": PROJ, "densityD": d, "frameCount": len(ids),
                                       "fps": entry["fps"], "scriptHashes": script_hashes,
                                       "subjectScale": "%d/24" % d,
                                       "renderSecondsPerFrame": [s["renderSeconds"] for s in frame_stubs]},
                        "touchUp": [],
                        "pixelPass": {"script": "tools/art-pipeline/art_pipeline/pixel_pass.py",
                                      "scriptSha256": script_hashes["pixel_pass.py"],
                                      "driver": "art-source/f004/spike/s2/pipeline_s2.py",
                                      "paletteId": palette.palette_id, "paletteVersion": palette.version,
                                      "paletteSha256": script_hashes["palette-v0.json"],
                                      "config": {"supersample": 1, "quantGroups": sorted(quant), "outline": "ink_0",
                                                 "canvas": list(canvas), "footprintPx": list(footprint),
                                                 "sharedPlacementWithFamily": fam}},
                        "outputs": entry["frames"],
                        "license": "Repository-authored procedural geometry and palette; no third-party assets, "
                                   "models or textures. Ownership: repository owner (fjure); no public license grant.",
                        "trainingDataNote": "N/A: no generative model or training data used (Blender route, "
                                            "procedural geometry written by an AI coding agent, disclosed).",
                        "producedBy": "3D and Render Artist (Claude Code Sonnet 5.x, Art Director lens), T418 / Issue #530",
                        "reviewedBy": None,
                        "candidateSelection": "Single deterministic render of fixed geometry, seed 0; no candidate or "
                                              "seed selection. Winning projection from owner decision T417 "
                                              "(B, isometric 2:1).",
                        "artBibleVersion": "none yet (spike input): F003-PAB v0.1 section 4 plan silhouettes; "
                                           "palette-v0 0.1.0; Art Bible v1 is T424",
                        "briefVersion": "T418 assignment (specs/004-visual-identity/tasks.md); FR-007; research R03, R16 S2",
                        "rigVersion": first["parameters"]["rigVersion"],
                        "renderEnv": first["renderEnv"],
                        "checkReportHash": None,
                        "openItems": OPEN_ITEMS,
                    }
                    rec["_asset"] = {"family": fam, "state": state, "perspective": PROJ, "densityD": d}
                    pdir = HERE / "provenance" / tag
                    pdir.mkdir(parents=True, exist_ok=True)
                    write_lf(pdir / (asset_id + ".provenance.json"), json.dumps(rec, indent=2, sort_keys=True) + "\n")
                    incomplete.append((tag, asset_id, missing_provenance_fields(rec, "blender")))
                    entries.append(entry)
            candidates[d] = entries

            checks.make_review_sheets(sprites, SEED, HERE / "sheets" / tag, scale=3)
            key = HERE / "sheets" / tag / "sheet_key.json"
            write_lf(key, key.read_text(encoding="utf-8"))

            worst_full = worst_small = 0.0
            for sm, ma in masks["miner"].items():
                for sa, mb in masks["assembler"].items():
                    r = checks.check_iou({"miner-" + sm: ma, "assembler-" + sa: mb})
                    worst_full = max(worst_full, r.details["worstFull"])
                    worst_small = max(worst_small, r.details["worstSmall"])
            head = checks.check_iou({"miner-run": masks["miner"]["run"], "assembler-run": masks["assembler"]["run"]})
            iou_report[tag] = {"worstFull": worst_full, "worstSmall": worst_small, "limit": checks.IOU_MAX,
                               "headline": head.details,
                               "passed": worst_full <= checks.IOU_MAX and worst_small <= checks.IOU_MAX}
            global_checks.append(checks.CheckResult("silhouette_iou[%s]" % tag, iou_report[tag]["passed"], iou_report[tag]))
            budget_report[tag] = palette_budget_report(palette, all_frames)
            global_checks.append(checks.CheckResult("palette_group_budget[%s]" % tag, budget_report[tag]["passed"],
                                                    budget_report[tag]))
            global_checks.append(checks.CheckResult("reproducibility[%s]" % tag, repro[tag]["byteIdentical"], repro[tag]))
        global_checks.append(checks.check_size_budgets(HERE.parent.parent))

        report = checks.build_report(flat, global_checks)
        report["spike"] = {"stage": "S2", "task": "T418", "densities": list(DENSITIES), "perspective": PROJ,
                           "ownerDecision": "T417: B, isometric 2:1",
                           "notRun": ["contrast (needs the declared surfaces of the Art Bible, T424)",
                                      "cue icon size (no cues in S2; S3 covers scripted cues)",
                                      "reproduction golden (the pipeline tests own the golden hashes)"],
                           "d24MatchesS1Isometric": match_s1, "timings": timings}
        text = checks.report_json(report)
        write_lf(HERE / "check-report.json", text)
        report_hash = hashlib.sha256((HERE / "check-report.json").read_bytes()).hexdigest()

        for d in DENSITIES:
            tag = "D%d" % d
            for f in (HERE / "provenance" / tag).glob("*.provenance.json"):
                rec = json.loads(f.read_text(encoding="utf-8"))
                rec["checkReportHash"] = report_hash
                rec["checkReportPath"] = "art-source/f004/spike/s2/check-report.json"
                write_lf(f, json.dumps(rec, indent=2, sort_keys=True) + "\n")
            man = build_manifest(candidates[d])
            man["spike"] = {"note": "S2 candidate manifest, not a shipped manifest (reviewedBy pending by contract)",
                            "perspective": PROJ, "densityD": d}
            write_lf(HERE / ("candidates-%s.json" % tag), json.dumps(man, indent=2, sort_keys=True) + "\n")

        still = {}
        for tag, aid, miss in incomplete:
            miss = [m for m in miss if m != "checkReportHash"]
            if miss:
                still.setdefault(tuple(miss), []).append("%s/%s" % (tag, aid))
        print("CHECKS-PASSED" if report["passed"] else "CHECKS-FAILED")
        print("provenance gaps (after checkReportHash):", {",".join(k): len(v) for k, v in still.items()})
        for r in global_checks:
            print(r.name, r.passed, {k: v for k, v in r.details.items()
                                     if k in ("worstFull", "worstSmall", "totalMB", "usedTotal", "byteIdentical")})
        print("d24 vs s1 identical frames:", sum(v["identical"] for v in match_s1.values()), "of",
              sum(v["frames"] for v in match_s1.values()))
        failing = [(a, r.name) for a, rs in flat.items() for r in rs if not r.passed]
        print("failing asset checks:", failing)
        return 0 if report["passed"] else 1
    finally:
        shutil.rmtree(tmp, ignore_errors=True)


if __name__ == "__main__":
    raise SystemExit(main())
