"""F004 style spike S1 (T415) driver: models -> Blender renders -> pixel pass -> checks -> provenance.

Run from the repository root (BLENDER_EXE optional):

  uv run --frozen --offline --project tools/art-pipeline python art-source/f004/spike/s1/pipeline_s1.py

Outputs under art-source/f004/spike/s1/: models/ (one model .blend per asset, frame/state 00 only),
raw/<projection>/ (Blender renders, 128 px, D = 24), pixel/<projection>/ (pixel-pass frames), sheets/<projection>/
(shuffled grayscale/CVD review sheets), provenance/<projection>/ (one record per asset state), candidates-*.json,
check-report.json. Spike candidates only; nothing here is a shipped manifest.
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
PROJECTIONS = ("oblique-3-4", "isometric-2-1")
D = 24
PPU = D
FPS = 8
FRAMES = 6
SEED = 415

# footprint px = projected ground footprint of the 2x2 cell slot at D = 24 (rig-v1 cameras): width, depth
FOOT_SLOT = {"oblique-3-4": (48, 24), "isometric-2-1": (68, 30)}
FOOT_RES = {"oblique-3-4": (14, 7), "isometric-2-1": (20, 9)}
CANVAS_SLOT = (80, 64)
CANVAS_RES = (32, 24)

GROUPS_MACHINE = {"machine", "ink"}
GROUPS_PLATE = {"floor", "ui", "machine", "ink"}
GROUPS_RES = {"resources", "ink"}
QUANT_MACHINE = {"machine"}
QUANT_PLATE = {"floor", "ui", "machine"}
QUANT_RES = {"resources"}

# family -> (list of (state, frame asset ids)), canvas, footprint table, groups
FAMILIES = {
    "miner": {"kind": "machine", "states": {
        "run": ["s1-miner-run-%02d" % i for i in range(FRAMES)],
        "paused": ["s1-miner-paused-00"], "warning": ["s1-miner-warning-00"], "inactive": ["s1-miner-inactive-00"]}},
    "assembler": {"kind": "machine", "states": {
        "run": ["s1-assembler-run-%02d" % i for i in range(FRAMES)],
        "paused": ["s1-assembler-paused-00"], "warning": ["s1-assembler-warning-00"],
        "inactive": ["s1-assembler-inactive-00"]}},
    "slots": {"kind": "plate", "states": {s: ["s1-slot-%s-00" % s] for s in ("empty", "locked", "occupied", "selected")}},
    "res-ore": {"kind": "res", "states": {"": ["s1-res-ore-00"]}},
    "res-ingot": {"kind": "res", "states": {"": ["s1-res-ingot-00"]}},
}
KIND = {
    "machine": (CANVAS_SLOT, FOOT_SLOT, QUANT_MACHINE, GROUPS_MACHINE),
    "plate": (CANVAS_SLOT, FOOT_SLOT, QUANT_PLATE, GROUPS_PLATE),
    "res": (CANVAS_RES, FOOT_RES, QUANT_RES, GROUPS_RES),
}


def sha256_file(p: Path) -> str:
    return hashlib.sha256(p.read_bytes()).hexdigest()


def run(cmd, **kw):
    r = subprocess.run(cmd, capture_output=True, text=True, **kw)
    if r.returncode != 0:
        raise SystemExit("command failed: %s\n%s\n%s" % (" ".join(map(str, cmd)), r.stdout[-2000:], r.stderr[-2000:]))
    return r.stdout


def main() -> int:
    palette = load_palette()
    work = Path(tempfile.mkdtemp(prefix="s1-models-"))
    try:
        t0 = time.perf_counter()
        run([BLENDER, "-b", "--factory-startup", "--python-exit-code", "1", "-P", str(HERE / "build_models.py"),
             "--", str(work)])
        build_seconds = round(time.perf_counter() - t0, 2)

        # committed model sources: the state/frame-00 .blend of each asset (the script regenerates the rest)
        models_out = HERE / "models"
        if models_out.exists():
            shutil.rmtree(models_out)
        models_out.mkdir()
        for blend in sorted(work.glob("*.blend")):
            if blend.stem.endswith("-00") or blend.stem.endswith("-run-00"):
                shutil.copy2(blend, models_out / blend.name)

        for out_name in ("raw", "pixel", "sheets", "provenance"):
            if (HERE / out_name).exists():
                shutil.rmtree(HERE / out_name)

        script_hashes = {"build_models.py": sha256_file(HERE / "build_models.py"),
                         "pipeline_s1.py": sha256_file(Path(__file__)),
                         "render.py": sha256_file(RENDER),
                         "pixel_pass.py": sha256_file(REPO / "tools/art-pipeline/art_pipeline/pixel_pass.py"),
                         "palette-v0.json": sha256_file(REPO / "tools/art-pipeline/palette/palette-v0.json"),
                         "rig-v1.blend": sha256_file(RIG)}
        results: dict[str, dict[str, list]] = {}     # projection -> asset-state -> check results
        sprites: dict[str, dict[str, Image.Image]] = {p: {} for p in PROJECTIONS}   # projection -> id -> frame0
        masks_by_state: dict[str, dict[str, dict[str, Image.Image]]] = {p: {} for p in PROJECTIONS}
        candidates: dict[str, list] = {p: [] for p in PROJECTIONS}
        incomplete: dict[str, list] = {}
        render_total = 0.0

        for proj in PROJECTIONS:
            raw_dir = HERE / "raw" / proj
            raw_dir.mkdir(parents=True, exist_ok=True)
            stubs: dict[str, dict] = {}
            for blend in sorted(work.glob("*.blend")):
                run([BLENDER, "-b", str(RIG), "--factory-startup", "--python-exit-code", "1", "-P", str(RENDER), "--",
                     str(blend), "--projection", proj, "--out", str(raw_dir), "--name", blend.stem])
                stub_path = raw_dir / (blend.stem + ".provenance.json")
                stubs[blend.stem] = json.loads(stub_path.read_text(encoding="utf-8"))
                stub_path.unlink()
                render_total += stubs[blend.stem]["renderSeconds"]
            results[proj] = {}
            for fam, spec in FAMILIES.items():
                canvas, foot, quant, allowed = KIND[spec["kind"]]
                footprint = foot[proj]
                # one animation group per family: a shared canvas offset and pivot for every state (no jitter)
                order = [(state, i, aid) for state, ids in spec["states"].items() for i, aid in enumerate(ids)]
                raws = [Image.open(raw_dir / (aid + ".png")).convert("RGBA") for _s, _i, aid in order]
                cfg = PassConfig(route="blender", canvas=canvas, footprint=footprint, supersample=1,
                                 quant_groups=quant, outline=True, ink_id="ink_0")
                frames, placement = run_pixel_pass(raws, palette, cfg)
                pos = 0
                for state, ids in spec["states"].items():
                    sf = frames[pos:pos + len(ids)]
                    pos += len(ids)
                    prefix = {"slots": "s1-slot"}.get(fam, "s1-" + fam)
                    asset_id = "%s-%s" % (prefix, state) if state else prefix
                    pix_dir = HERE / "pixel" / proj
                    paths = write_frames(sf, asset_id, pix_dir)
                    entry = {"assetId": asset_id, "route": "blender", "ppu": PPU, "fps": FPS if len(sf) > 1 else 1,
                             "canvas": list(placement.canvas), "footprintPx": list(placement.footprint),
                             "pivot": list(placement.pivot), "perspective": proj,
                             "palette": {"id": palette.palette_id, "version": palette.version},
                             "frames": [{"file": p.relative_to(HERE).as_posix(), "sha256": sha256_file(p)} for p in paths]}
                    res = checks.run_asset_checks(sf, entry, palette, expected_frames=len(ids), ppu=PPU,
                                                  allowed_groups=allowed, running_loop=(state == "run"))
                    results[proj][asset_id] = res
                    sprites[proj][asset_id] = sf[0]
                    if fam in ("miner", "assembler"):
                        masks_by_state[proj].setdefault(fam, {})[state] = checks.silhouette_mask(sf[0])

                    frame_stubs = [stubs[a] for a in ids]
                    first = frame_stubs[0]
                    rec = {
                        "assetId": asset_id, "revision": 1, "route": "blender",
                        "sources": [{"path": "art-source/f004/spike/s1/build_models.py",
                                     "sha256": script_hashes["build_models.py"],
                                     "note": "procedural geometry, no external assets; per-frame models are generated, "
                                             "the frame/state 00 .blend is committed under models/"},
                                    {"path": "tools/art-pipeline/blender/rig-v1.blend",
                                     "sha256": script_hashes["rig-v1.blend"]}]
                                   + [{"path": "models/" + a + ".blend", "sha256": sha256_file(HERE / "models" / (a + ".blend"))}
                                      for a in ids if (HERE / "models" / (a + ".blend")).exists()],
                        "tools": {**first["tools"], "python": sys.version.split()[0],
                                  "pillow": __import__("PIL").__version__},
                        "parameters": {**first["parameters"], "perspective": proj, "frameCount": len(ids),
                                       "fps": entry["fps"], "scriptHashes": script_hashes,
                                       "renderSecondsPerFrame": [s["renderSeconds"] for s in frame_stubs]},
                        "touchUp": [],
                        "pixelPass": {"script": "tools/art-pipeline/art_pipeline/pixel_pass.py",
                                      "scriptSha256": script_hashes["pixel_pass.py"],
                                      "driver": "art-source/f004/spike/s1/pipeline_s1.py",
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
                        "producedBy": "3D and Render Artist (Claude Code Sonnet 5.x, Art Director lens), T415 / Issue #527",
                        "reviewedBy": None,
                        "candidateSelection": "Single deterministic render of fixed geometry, seed 0; frame poses are "
                                              "computed from the loop parameter; no candidate or seed selection.",
                        "artBibleVersion": "none yet (spike input): F003-PAB v0.1 section 4 plan silhouettes; "
                                           "palette-v0 0.1.0; Art Bible v1 is T424",
                        "briefVersion": "T415 assignment (specs/004-visual-identity/tasks.md); FR-007; research R16 S1",
                        "rigVersion": first["parameters"]["rigVersion"],
                        "renderEnv": first["renderEnv"],
                        "checkReportHash": None,
                    }
                    rec["_asset"] = {"family": fam, "state": state, "perspective": proj}
                    pdir = HERE / "provenance" / proj
                    pdir.mkdir(parents=True, exist_ok=True)
                    (pdir / (asset_id + ".provenance.json")).write_text(
                        json.dumps(rec, indent=2, sort_keys=True) + "\n", encoding="utf-8")
                    incomplete.setdefault(proj, []).append((asset_id, missing_provenance_fields(rec, "blender")))
                    candidates[proj].append(entry)

            # review sheets for the independent agent reviewer (shuffled; the key stays separate)
            checks.make_review_sheets(sprites[proj], SEED, HERE / "sheets" / proj, scale=3)

        # cross-machine silhouette IoU (spike gate): every Miner state against every Assembler state, both sizes
        global_checks = [checks.check_palette_definition(palette)]
        problems = validate_models(load_models())
        global_checks.append(checks.CheckResult("models", not problems, {"problems": problems}))
        iou_report: dict[str, dict] = {}
        for proj in PROJECTIONS:
            worst_full = worst_small = 0.0
            pairs = []
            for sm, ma in masks_by_state[proj]["miner"].items():
                for sa, mb in masks_by_state[proj]["assembler"].items():
                    r = checks.check_iou({"miner-" + sm: ma, "assembler-" + sa: mb})
                    worst_full = max(worst_full, r.details["worstFull"])
                    worst_small = max(worst_small, r.details["worstSmall"])
                    pairs.append({"pair": ["miner-" + sm, "assembler-" + sa], "full": r.details["worstFull"],
                                  "small": r.details["worstSmall"]})
            head = checks.check_iou({"miner-run": masks_by_state[proj]["miner"]["run"],
                                     "assembler-run": masks_by_state[proj]["assembler"]["run"]})
            iou_report[proj] = {"worstFull": worst_full, "worstSmall": worst_small, "limit": checks.IOU_MAX,
                                "headline": head.details, "passed": worst_full <= checks.IOU_MAX and worst_small <= checks.IOU_MAX}
            global_checks.append(checks.CheckResult("silhouette_iou[%s]" % proj, iou_report[proj]["passed"], iou_report[proj]))
        global_checks.append(checks.check_size_budgets(HERE.parent.parent))

        # flatten per-projection results into one report; assets keyed "<projection>/<assetId>"
        flat = {"%s/%s" % (p, a): r for p, d in results.items() for a, r in d.items()}
        report = checks.build_report(flat, global_checks)
        report["spike"] = {"stage": "S1", "task": "T415", "densityD": D, "perspectives": list(PROJECTIONS),
                           "notRun": ["contrast (needs the declared surfaces of the Art Bible, T424)",
                                      "cue icon size (no cues in S1; S3 covers scripted cues)",
                                      "reproduction golden (T416/pipeline tests own the golden hashes)"],
                           "buildModelsSeconds": build_seconds, "totalRenderSeconds": round(render_total, 2)}
        text = checks.report_json(report)
        (HERE / "check-report.json").write_text(text, encoding="utf-8")
        report_hash = hashlib.sha256((HERE / "check-report.json").read_bytes()).hexdigest()

        # stamp the check-report hash into each provenance record and write the candidate manifests
        for proj in PROJECTIONS:
            for f in (HERE / "provenance" / proj).glob("*.provenance.json"):
                rec = json.loads(f.read_text(encoding="utf-8"))
                rec["checkReportHash"] = report_hash
                rec["checkReportPath"] = "art-source/f004/spike/s1/check-report.json"
                f.write_text(json.dumps(rec, indent=2, sort_keys=True) + "\n", encoding="utf-8")
            man = build_manifest(candidates[proj])
            man["spike"] = {"note": "S1 candidate manifest, not a shipped manifest (reviewedBy pending by contract)",
                            "perspective": proj}
            (HERE / ("candidates-%s.json" % proj)).write_text(json.dumps(man, indent=2, sort_keys=True) + "\n",
                                                               encoding="utf-8")

        # provenance completeness summary
        still = {}
        for proj, lst in incomplete.items():
            for aid, miss in lst:
                miss = [m for m in miss if m != "checkReportHash"]
                if miss:
                    still.setdefault(tuple(miss), []).append("%s/%s" % (proj, aid))
        print("CHECKS-PASSED" if report["passed"] else "CHECKS-FAILED")
        print("provenance gaps (after checkReportHash):", {",".join(k): len(v) for k, v in still.items()})
        for r in global_checks:
            print(r.name, r.passed, {k: v for k, v in r.details.items() if k in ("worstFull", "worstSmall", "totalMB")})
        failing = [(a, r.name) for a, rs in flat.items() for r in rs if not r.passed]
        print("failing asset checks:", failing)
        return 0 if report["passed"] else 1
    finally:
        shutil.rmtree(work, ignore_errors=True)


if __name__ == "__main__":
    raise SystemExit(main())
