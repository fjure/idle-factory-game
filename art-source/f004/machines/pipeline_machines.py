"""F004 T434 driver: models -> Blender renders -> pixel pass -> accent layers -> checks -> provenance -> report.

Run from the repository root (BLENDER_EXE optional):

  uv run --frozen --offline --project tools/art-pipeline python art-source/f004/machines/pipeline_machines.py

Outputs under art-source/f004/machines/: `<machine>/model|raw|pixel/` and `<machine>/provenance.json` for the body
(`f004-machine-<machine>-run`, 6 frames at 8 fps), `<machine>-accent/pixel/` and provenance for the accent layer
(`f004-accent-<machine>-run`), `sheets/` (shuffled grayscale and CVD review sheets), `manifest-candidates.json`
and the family `check-report.json`. Nothing here is a shipped manifest (reviewedBy stays empty for T439).
"""
from __future__ import annotations

import hashlib
import json
import os
import shutil
import subprocess
import sys
import tempfile
from pathlib import Path

HERE = Path(__file__).resolve().parent
REPO = HERE.parents[2]
sys.path.insert(0, str(REPO / "tools" / "art-pipeline"))

from PIL import Image  # noqa: E402

from art_pipeline import checks, checks_v1 as c1  # noqa: E402
from art_pipeline.palette import PALETTE_V1_PATH, load_palette  # noqa: E402
from art_pipeline.pixel_pass import PassConfig, build_manifest, run_pixel_pass, write_frames  # noqa: E402
from art_pipeline.provenance import load_models, missing_provenance_fields, validate_models  # noqa: E402

BLENDER = os.environ.get("BLENDER_EXE") or r"C:\Program Files\Blender Foundation\Blender 5.2\blender.exe"
RIG = REPO / "tools" / "art-pipeline" / "blender" / "rig-v2.blend"
RENDER = REPO / "tools" / "art-pipeline" / "blender" / "render.py"
PROJ = "isometric-2-1"
MACHINES = ("miner", "smelter", "press", "assembler")
KEYS = {"miner": "F003-MACHINE-MINER", "smelter": "F003-MACHINE-SMELTER", "press": "F003-MACHINE-PRESS",
        "assembler": "F003-MACHINE-ASSEMBLER"}
CANVAS, FOOTPRINT, FPS, FRAMES, PPU = (80, 64), (68, 30), 8, 6, 24
SEED = 434
SPIKE = REPO / "art-source" / "f004" / "spike"
# Backdrop and overlay stand-ins until T435 (plates) and T437 (cues) exist: the approved spike sprites, same canvas.
PLATE = SPIKE / "s2" / "pixel" / "D24" / "s2-slot-occupied_00.png"
FRAME_SPRITE = SPIKE / "s2" / "pixel" / "D24" / "s2-slot-selected_00.png"
BADGES = sorted((SPIKE / "s3-s4" / "pixel").glob("s3b-cue-*-23_00.png"))
ART_BIBLE = "F004-PAB v1.0"
BRIEF = "F004-AB v1.0"


def write_lf(path: Path, text: str) -> None:
    with open(path, "w", encoding="utf-8", newline="\n") as fh:
        fh.write(text)


def sha256_file(p: Path) -> str:
    return hashlib.sha256(p.read_bytes()).hexdigest()


def run(cmd):
    r = subprocess.run(cmd, capture_output=True, text=True)
    if r.returncode != 0:
        raise SystemExit("command failed: %s\n%s\n%s" % (" ".join(map(str, cmd)), r.stdout[-2000:], r.stderr[-2000:]))


def render_one(blend: Path, out_dir: Path, name: str) -> dict:
    run([BLENDER, "-b", str(RIG), "--factory-startup", "--python-exit-code", "1", "-P", str(RENDER), "--",
         str(blend), "--projection", PROJ, "--out", str(out_dir), "--name", name])
    stub_path = out_dir / (name + ".provenance.json")
    stub = json.loads(stub_path.read_text(encoding="utf-8"))
    stub_path.unlink()
    return stub


def accent_layer(body: Image.Image, palette) -> Image.Image:
    """Lighting B accent (Art Bible 2.1 L1): the lamp glow in `lit_0`/`lit_1` (copied from the body lamp) plus a
    `lit_0` halo on its solid 4-neighbours and a `steel_3` rim highlight on the upper-left boundary rim. Opaque
    palette pixels only, never on ink, outline or boundary pixels (rule L-4), no outline of its own."""
    ids = {c.rgb: c.id for c in palette.colors}
    w, h = body.size
    lay = Image.new("RGBA", body.size, (0, 0, 0, 0))
    lp, bp = lay.load(), body.load()

    def solid(x, y):
        if not (0 <= x < w and 0 <= y < h) or bp[x, y][3] != 255 or palette.group_of(bp[x, y][:3]) == "ink":
            return False
        return all(0 <= x + dx < w and 0 <= y + dy < h and bp[x + dx, y + dy][3] == 255
                   for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1)))

    lamp = {(x, y) for y in range(h) for x in range(w)
            if bp[x, y][3] and ids.get(bp[x, y][:3]) in ("lit_0", "lit_1") and solid(x, y)}
    for x, y in lamp:
        lp[x, y] = bp[x, y]
    for x, y in lamp:
        for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1)):
            nx, ny = x + dx, y + dy
            if solid(nx, ny) and (nx, ny) not in lamp and lp[nx, ny][3] == 0:
                lp[nx, ny] = palette.rgb("lit_0") + (255,)
    for y in range(h):
        for x in range(w):
            if solid(x, y) and lp[x, y][3] == 0:
                up, left = (x, y - 1), (x - 1, y)
                if all(solid(*q) for q in (up, left)):
                    continue
                if not solid(*up) or not solid(*left):
                    lp[x, y] = palette.rgb("steel_3") + (255,)
    return lay


def composite(*layers: Image.Image) -> Image.Image:
    out = Image.new("RGBA", layers[0].size, (0, 0, 0, 0))
    for l in layers:
        out.alpha_composite(l.convert("RGBA"))
    return out


def tag(results, name_suffix):
    return results


def per_frame(name, frames, fn):
    """Run a per-frame check on every frame; one aggregated CheckResult with the per-frame details."""
    rs = [fn(i, f) for i, f in enumerate(frames)]
    return checks.CheckResult(name, all(r.passed for r in rs), {"frames": [{"frame": i, **r.details} for i, r in enumerate(rs)]})


def main() -> int:
    palette = load_palette(PALETTE_V1_PATH)
    plate = Image.open(PLATE).convert("RGBA")
    frame_sprite = Image.open(FRAME_SPRITE).convert("RGBA")
    badges = [Image.open(p).convert("RGBA") for p in BADGES]
    tmp = Path(tempfile.mkdtemp(prefix="t434-models-"))
    try:
        for m in MACHINES:
            for sub in (m, m + "-accent"):
                if (HERE / sub).exists():
                    shutil.rmtree(HERE / sub)
        if (HERE / "sheets").exists():
            shutil.rmtree(HERE / "sheets")
        script_hashes = {"build_models.py": sha256_file(HERE / "build_models.py"),
                         "pipeline_machines.py": sha256_file(Path(__file__)),
                         "render.py": sha256_file(RENDER),
                         "pixel_pass.py": sha256_file(REPO / "tools/art-pipeline/art_pipeline/pixel_pass.py"),
                         "checks_v1.py": sha256_file(REPO / "tools/art-pipeline/art_pipeline/checks_v1.py"),
                         "palette-v1.json": sha256_file(PALETTE_V1_PATH), "rig-v2.blend": sha256_file(RIG)}
        run([BLENDER, "-b", "--factory-startup", "--python-exit-code", "1", "-P", str(HERE / "build_models.py"), "--",
             str(tmp)])
        asset_checks: dict[str, list] = {}
        global_checks = [checks.check_palette_definition(palette)]
        problems = validate_models(load_models())
        global_checks.append(checks.CheckResult("models", not problems, {"problems": problems}))
        bodies: dict[str, list] = {}
        accents: dict[str, list] = {}
        entries, provs = [], {}
        sprites = {}
        for m in MACHINES:
            mdir = HERE / m
            (mdir / "model").mkdir(parents=True)
            (mdir / "raw").mkdir()
            stubs = []
            for f in range(FRAMES):
                name = "%s-run-%02d" % (m, f)
                shutil.copy2(tmp / (name + ".blend"), mdir / "model" / (name + ".blend"))
                stubs.append(render_one(tmp / (name + ".blend"), mdir / "raw", name))
            raws = [Image.open(mdir / "raw" / ("%s-run-%02d.png" % (m, f))).convert("RGBA") for f in range(FRAMES)]
            cfg = PassConfig(route="blender", canvas=CANVAS, footprint=FOOTPRINT, supersample=1,
                             quant_groups={"machine"}, outline=True, ink_id="ink_0")
            body, placement = run_pixel_pass(raws, palette, cfg)
            acc = [accent_layer(b, palette) for b in body]
            bodies[m], accents[m] = body, acc
            bid, aid = "f004-machine-%s-run" % m, "f004-accent-%s-run" % m
            paths = write_frames(body, bid, mdir / "pixel")
            (HERE / (m + "-accent") / "pixel").mkdir(parents=True)
            apaths = write_frames(acc, aid, HERE / (m + "-accent") / "pixel")
            base = {"route": "blender", "ppu": PPU, "fps": FPS, "canvas": list(placement.canvas),
                    "footprintPx": list(placement.footprint), "pivot": list(placement.pivot), "perspective": PROJ,
                    "palette": {"id": palette.palette_id, "version": palette.version}}
            e_body = {"assetId": bid, **base,
                      "frames": [{"file": p.relative_to(HERE).as_posix(), "sha256": sha256_file(p)} for p in paths]}
            e_acc = {"assetId": aid, **base, "route": "scripted",
                     "frames": [{"file": p.relative_to(HERE).as_posix(), "sha256": sha256_file(p)} for p in apaths]}
            entries += [e_body, e_acc]

            # ---- body checks (run_asset_checks + v1 composite checks on every frame)
            res = checks.run_asset_checks(body, e_body, palette, expected_frames=FRAMES, ppu=PPU,
                                          allowed_groups={"machine", "ink"}, running_loop=True)
            res.append(c1.check_overlay_keepout(body))
            res.append(per_frame("outline_contrast", body,
                                 lambda i, f: c1.check_outline_contrast(f, plate, palette, tones=c1.BODY_BACKDROP_TONES)))
            res.append(per_frame("outline_contrast_bare_floor", body,
                                 lambda i, f: c1.check_outline_contrast(f, None, palette)))
            comp = [composite(plate, b) for b in body]
            comp_acc = [composite(plate, b, a) for b, a in zip(body, acc)]

            def backdrop(i, f, comps=comp_acc):
                bd = comps[i]
                rs = [c1.check_overlay_backdrop(bg, bd, c1.A_OV, palette) for bg in badges]
                rs.append(c1.check_overlay_backdrop(frame_sprite, bd, c1.a_fr(palette), palette))
                return checks.CheckResult("x", all(r.passed for r in rs), {"overlays": len(rs), "failed": [
                    r.details for r in rs if not r.passed]})
            res.append(per_frame("overlay_backdrop", body, backdrop))
            res.append(checks.CheckResult("palette_machine_colors_per_asset", len(
                {tuple(p[:3]) for f in body for p in f.getdata() if p[3] and palette.group_of(tuple(p[:3])) == "machine"}) <= 14,
                {"machineColors": len({tuple(p[:3]) for f in body for p in f.getdata()
                                       if p[3] and palette.group_of(tuple(p[:3])) == "machine"})}))
            asset_checks[bid] = res

            # ---- accent checks
            ares = [checks.check_palette(acc, palette, {"machine"}), checks.check_canvas(acc, CANVAS),
                    checks.check_alpha(acc), checks.check_frames(acc, FRAMES),
                    c1.check_accent_subset(acc, body, palette),
                    checks.CheckResult("pivot", tuple(round(v, 6) for v in e_acc["pivot"]) == tuple(placement.pivot)
                                       and checks.pivot_is_integral(e_acc),
                                       {"manifest": e_acc["pivot"], "pivotPx": [round(v, 4) for v in checks.pivot_px(e_acc)],
                                        "integral": checks.pivot_is_integral(e_acc),
                                        "note": "same canvas and pivot as the body; the accent need not touch the footprint bottom"})]
            ares.append(checks.check_flash(comp_acc_nobg := [composite(b, a) for b, a in zip(body, acc)], FPS))
            ares[-1].name = "flash_composite"
            fl = checks.flash_rate(comp_acc_nobg, FPS)
            ares.append(checks.CheckResult("flash_headroom", fl["maxFlashesPerSecond"] <= 2,
                                           {"maxFlashesPerSecond": fl["maxFlashesPerSecond"], "required": 2,
                                            "transitions": fl["transitions"], "threshold": checks.FLASH_DELTA}))
            ares.append(checks.CheckResult("accent_pixels", all(any(p[3] for p in a.getdata()) for a in acc),
                                           {"perFrame": [sum(1 for p in a.getdata() if p[3]) for a in acc]}))
            asset_checks[aid] = ares
            sprites[bid] = body[0]
            sprites[aid + "-comp"] = composite(body[0], acc[0])

            for kind, ident, ids_, route, dirp in (("body", bid, "%s-run" % m, "blender", mdir),
                                                    ("accent", aid, "%s-run" % m, "scripted", HERE / (m + "-accent"))):
                provs[ident] = (kind, route, dirp, m, stubs)

        # ---- global family checks
        masks = {m: checks.silhouette_mask(bodies[m][0]) for m in MACHINES}
        iou = checks.check_iou(masks)
        pairs = []
        names = sorted(masks)
        for i, a in enumerate(names):
            for b in names[i + 1:]:
                r = checks.check_iou({a: masks[a], b: masks[b]})
                pairs.append({"pair": [a, b], "full": r.details["worstFull"], "small": r.details["worstSmall"]})
        iou.details["pairs"] = pairs
        global_checks.append(iou)
        used: dict[str, set] = {}
        for m in MACHINES:
            for f in bodies[m] + accents[m]:
                for px in set(f.getdata()):
                    if px[3]:
                        g = palette.group_of(tuple(px[:3]))
                        used.setdefault(g, set()).add(tuple(px[:3]))
        budget_ok = all(len(v) <= palette.budgets[g] for g, v in used.items()) and "state_cues" not in used
        global_checks.append(checks.CheckResult("palette_group_budget", budget_ok, {
            g: {"used": len(v), "budget": palette.budgets[g]} for g, v in sorted(used.items())}))
        # reproducibility: re-render one frame of every machine and compare the PNG bytes
        repro = {}
        for m in MACHINES:
            again = tmp / ("again-" + m)
            again.mkdir()
            name = "%s-run-00" % m
            render_one(tmp / (name + ".blend"), again, name)
            a, b = sha256_file(HERE / m / "raw" / (name + ".png")), sha256_file(again / (name + ".png"))
            repro[m] = {"first": a, "second": b, "byteIdentical": a == b}
        global_checks.append(checks.CheckResult("reproducibility", all(v["byteIdentical"] for v in repro.values()), repro))
        keys_ok = len(entries) == 8 and len({e["assetId"] for e in entries}) == 8
        global_checks.append(checks.CheckResult("entry_set", keys_ok, {"entries": [e["assetId"] for e in entries]}))
        sizes = checks.check_size_budgets(HERE.parent)
        global_checks.append(sizes)
        checks.make_review_sheets(sprites, SEED, HERE / "sheets", scale=3)
        write_lf(HERE / "sheets" / "sheet_key.json", (HERE / "sheets" / "sheet_key.json").read_text(encoding="utf-8"))

        report = checks.build_report(asset_checks, global_checks)
        report["family"] = {"task": "T434", "issue": 548, "family": "machines", "plateBackdrop": PLATE.relative_to(REPO).as_posix(),
                            "plateNote": "spike plate stand-in until T435; outline_contrast is the Art Bible 9.3/C5 composite "
                                         "(tones plate_1, plate_2, ink_0, floor_0, floor_1); outline_contrast_bare_floor "
                                         "treats every outward pair as bare floor (stricter).",
                            "overlayStandIns": [p.relative_to(REPO).as_posix() for p in BADGES] + [FRAME_SPRITE.relative_to(REPO).as_posix()],
                            "intentionallyEmpty": "no intentionallyEmpty entry in this family",
                            "artBibleVersion": ART_BIBLE, "briefVersion": BRIEF}
        write_lf(HERE / "check-report.json", checks.report_json(report))
        report_hash = sha256_file(HERE / "check-report.json")

        # ---- provenance and manifest
        gaps = {}
        for ident, (kind, route, dirp, m, stubs) in provs.items():
            first = stubs[0]
            body_ids = ["%s-run-%02d" % (m, f) for f in range(FRAMES)]
            entry = next(e for e in entries if e["assetId"] == ident)
            sources = [{"path": "art-source/f004/machines/build_models.py", "sha256": script_hashes["build_models.py"],
                        "note": "procedural geometry, flat palette-colour materials chosen per face orientation; no external assets"},
                       {"path": "tools/art-pipeline/blender/rig-v2.blend", "sha256": script_hashes["rig-v2.blend"]}]
            if kind == "body":
                sources += [{"path": "art-source/f004/machines/%s/model/%s.blend" % (m, b),
                             "sha256": sha256_file(HERE / m / "model" / (b + ".blend"))} for b in body_ids]
            else:
                sources.append({"path": "art-source/f004/machines/%s/pixel/ (body frames)" % m,
                                "sha256": hashlib.sha256("".join(f["sha256"] for f in next(
                                    e for e in entries if e["assetId"] == "f004-machine-%s-run" % m)["frames"]).encode()).hexdigest()})
            rec = {
                "assetId": ident, "revision": 2, "route": route, "key": ("F004-ACCENT-" + m.upper()) if kind == "accent" else KEYS[m],
                "sources": sources,
                "tools": {**first["tools"], "python": sys.version.split()[0], "pillow": __import__("PIL").__version__},
                "parameters": {**first["parameters"], "perspective": PROJ, "densityD": PPU, "frameCount": FRAMES, "fps": FPS,
                               "scriptHashes": script_hashes, "renderSecondsPerFrame": [s["renderSeconds"] for s in stubs],
                               "materials": "flat emission, exact palette colours per face orientation (top, left, right) plus trim",
                               "keyLight": "rig-v2 key light unused by the emission materials; ramp order encodes the top-left light"},
                "touchUp": [] if kind == "body" else [{"step": "accent_layer", "script": "art-source/f004/machines/pipeline_machines.py",
                                                         "note": "scripted from the body frames (lamp glow, halo, steel_3 upper-left rim); deterministic"}],
                "pixelPass": {"script": "tools/art-pipeline/art_pipeline/pixel_pass.py", "scriptSha256": script_hashes["pixel_pass.py"],
                              "driver": "art-source/f004/machines/pipeline_machines.py", "paletteId": palette.palette_id,
                              "paletteVersion": palette.version, "paletteSha256": script_hashes["palette-v1.json"],
                              "config": {"supersample": 1, "quantGroups": ["machine"], "outline": "ink_0", "canvas": list(CANVAS),
                                         "footprintPx": list(FOOTPRINT), "sharedPlacementAllFrames": True}},
                "outputs": entry["frames"],
                "license": "Repository-authored procedural geometry and palette; no third-party assets, models or textures. "
                           "Ownership: repository owner (fjure); no public license grant.",
                "trainingDataNote": "N/A: no generative model or training data used (Blender route, procedural geometry "
                                    "written by an AI coding agent, disclosed).",
                "producedBy": "3D and Render Artist (Claude Code Sonnet 5.5, Art Director lens self-check), T434 / Issue #548, revision 2 T466 / Issue #635",
                "reviewedBy": None,
                "candidateSelection": "Deterministic render of fixed geometry, seed 0, 16 samples; geometry and trim iterated by "
                                      "measurement of rim share, flash, keep-out and IoU during production (final render is the one "
                                      "committed); no seed selection. T466 (Issue #635) revision 2: re-rendered with rig-v2 (true 2:1 ground edges); geometry changes (if any) are described in the module notes of build_models.py; iteration and the final render were both run through the headless batch (no interactive Blender MCP step), the final render is the committed one.",
                "artBibleVersion": ART_BIBLE, "briefVersion": BRIEF,
                "rigVersion": first["parameters"]["rigVersion"], "renderEnv": first["renderEnv"],
                "checkReportHash": report_hash, "checkReportPath": "art-source/f004/machines/check-report.json",
            }
            write_lf(dirp / "provenance.json", json.dumps(rec, indent=2, sort_keys=True) + "\n")
            miss = missing_provenance_fields(rec, route)
            gaps[ident] = miss
        man = build_manifest(entries)
        man["note"] = "T434 candidate entries for T440; reviewedBy pending (T439); accent entries use route scripted."
        write_lf(HERE / "manifest-candidates.json", json.dumps(man, indent=2, sort_keys=True) + "\n")

        print("CHECKS-PASSED" if report["passed"] else "CHECKS-FAILED")
        print("provenance gaps:", {k: v for k, v in gaps.items() if v})
        for a, rs in sorted(asset_checks.items()):
            bad = [r.name for r in rs if not r.passed]
            print(a, "FAIL " + ",".join(bad) if bad else "ok")
        for r in global_checks:
            print("global", r.name, r.passed)
        return 0 if report["passed"] else 1
    finally:
        shutil.rmtree(tmp, ignore_errors=True)


if __name__ == "__main__":
    raise SystemExit(main())
