"""F004 T435 / T470, floor family: model -> Blender render -> tile cut -> tiling checks -> rim re-measurement (`F004-FLOOR-TILE`).

Run from the repository root with the `gpu` lease held (BLENDER_EXE optional):

  uv run --frozen --offline --project tools/art-pipeline python art-source/f004/floor/pipeline_floor.py

T470 (Issue #650, revision 3) is the floor-only driver: it builds `floor-tile.blend` (`build_models.py`), renders it through
the committed batch (`tools/art-pipeline/blender/render.py`, rig-v2), cuts the tile, runs the tiling and composite checks,
re-measures the Assembler rim against the new floor and writes `raw/`, `model/`, `pixel/`, `sheets/`, `manifest.json`,
`provenance.json` and the family `check-report.json`. The plates (`art-source/f004/slots/`) are read-only inputs here.

Tile geometry (Art Bible 3.2, 3.5 as patched by v1.0.2 and v1.0.3): the floor pattern repeats with a period of
**70 x 54 art px**, the pitch of the T456 plate lattice (3 x 4 portrait, 4 x 3 landscape), so every slot sees the same floor
phase. A 2:1 diamond floor of 70 x 35 has an odd height, so the tile is the whole period rectangle: 70 x 54 px, fully
opaque, translated by (70, 0) and (0, 54). Width and height are even and the pivot is the tile centre (35, 27), a whole art
pixel (rule P1). The outermost ring of the tile is `floor_0` (no outline, no tone step: brief section 6), so the
wrap-around edges are seamless by construction; the two detail clusters sit in the part of the period that stays visible
beside a 70 x 35 plate diamond centred on the tile.

`pipeline_slots.py` (T435, moved to the 70 x 54 lattice by T473) reads the committed tile and never writes this family;
re-render the floor with this module only.
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

from PIL import Image

HERE = Path(__file__).resolve().parent
REPO = HERE.parents[2]
ART = HERE.parent
sys.path.insert(0, str(REPO / "tools" / "art-pipeline"))

from art_pipeline import checks, checks_v1 as c1  # noqa: E402
from art_pipeline.palette import PALETTE_V1_PATH, Palette, load_palette  # noqa: E402
from art_pipeline.pixel_pass import (  # noqa: E402
    build_manifest, place_on_canvas, quantize, reduce_for_route, threshold_alpha, write_frames)
from art_pipeline.provenance import load_models, missing_provenance_fields, validate_models  # noqa: E402

TILE = (70, 54)
CENTRE_CROP = (29, 37, 99, 91)          # the 70 x 54 window of the 128 x 128 render centred on the world origin
ASSET_ID = "f004-floor-tile"
KEY = "F004-FLOOR-TILE"
FIELD, DETAIL = "floor_0", "floor_1"
PERIOD = (70, 54)                       # Art Bible 3.5: the lattice pitch
LATTICE = ((70, 0), (0, 54))
PLATE_DIAMOND = (70, 35)                # hit shape / footprint diamond that covers the tile centre
PPU = 24
PROJ = "isometric-2-1"
ART_BIBLE, BRIEF = "F004-PAB v1.0.3", "F004-AB v1.0"
BLENDER = os.environ.get("BLENDER_EXE") or r"C:\Program Files\Blender Foundation\Blender 5.2\blender.exe"
RIG = REPO / "tools" / "art-pipeline" / "blender" / "rig-v2.blend"
RENDER = REPO / "tools" / "art-pipeline" / "blender" / "render.py"
PHASES = ((0, 0), (35, 0), (0, 27), (35, 27))   # tile-centre offsets from the footprint centre (canonical first)
PRODUCER = ("3D and Render Artist (Claude Code Sonnet 5.5, Art Director lens self-check), T435 / Issue #549, "
            "revision 2 T466 / Issue #635, revision 3 T470 / Issue #650")


def sha256_file(p: Path) -> str:
    return hashlib.sha256(p.read_bytes()).hexdigest()


def write_lf(path: Path, text: str) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    with open(path, "w", encoding="utf-8", newline="\n") as fh:
        fh.write(text)


# --- tile cut and pixel pass --------------------------------------------------------------------------

def cut_tile(raw: Image.Image, palette: Palette) -> Image.Image:
    """Crop the render to the 70 x 54 window, reduce (blender route, factor 1) and quantise to the floor tones. The render
    is a ground patch much larger than the tile, so the crop is opaque everywhere and the whole window is kept."""
    crop = raw.convert("RGBA").crop(CENTRE_CROP)
    if min(p[3] for p in crop.getdata()) != 255:
        raise SystemExit("floor render does not cover the tile window")
    f = reduce_for_route(crop, "blender", supersample=1)
    f = threshold_alpha(f)
    return quantize(f, palette, {"floor"})


# --- tiling checks ------------------------------------------------------------------------------------

def _opaque(img: Image.Image) -> set[tuple[int, int]]:
    px = img.convert("RGBA").load()
    return {(x, y) for y in range(img.height) for x in range(img.width) if px[x, y][3]}


def tiling_checks(tile: Image.Image, palette: Palette) -> list[checks.CheckResult]:
    w, h = tile.size
    res: list[checks.CheckResult] = []
    px = tile.load()
    ids = {(x, y): palette.id_of(tuple(px[x, y][:3])) for (x, y) in _opaque(tile)}

    # 1. geometry: the period rectangle, fully opaque, even width and height
    res.append(checks.CheckResult("tile_geometry", (w, h) == PERIOD and len(ids) == w * h and w % 2 == 0 and h % 2 == 0, {
        "size": [w, h], "period": list(PERIOD), "opaquePixels": len(ids), "fullyOpaque": len(ids) == w * h}))

    # 2. lattice sheet: a 5 x 5 translation sheet, every pixel covered exactly once, no tone step across any tile boundary
    reps = range(-2, 3)
    cov: dict[tuple[int, int], tuple[tuple[int, int], str]] = {}
    overlaps = 0
    for a in reps:
        for b in reps:
            ox, oy = a * LATTICE[0][0], b * LATTICE[1][1]
            for (x, y), pid in ids.items():
                if (ox + x, oy + y) in cov:
                    overlaps += 1
                cov[(ox + x, oy + y)] = ((a, b), pid)
    # window: the interior 3 x 3 tiles (their neighbours are all present)
    win = [(x, y) for y in range(-h, 2 * h) for x in range(-w, 2 * w)]
    gaps = sum(1 for p in win if p not in cov)
    steps = pairs = 0
    for (x, y) in win:
        t0, id0 = cov[(x, y)]
        for dx, dy in ((1, 0), (0, 1)):
            q = (x + dx, y + dy)
            if q in cov:
                t1, id1 = cov[q]
                if t1 != t0:
                    pairs += 1
                    steps += id0 != id1
    res.append(checks.CheckResult("tiling_lattice", not gaps and not overlaps and not steps and pairs > 0, {
        "lattice": [list(v) for v in LATTICE], "period": list(PERIOD), "windowPx": len(win), "gaps": gaps,
        "overlaps": overlaps, "seamPairs": pairs, "toneSteps": steps}))

    # 3. wrap-around unit: one tile covers the 70 x 54 unit exactly once; the edge rows and columns carry the field tone
    unit: dict[tuple[int, int], int] = {}
    for (x, y) in ids:
        p = (x % PERIOD[0], y % PERIOD[1])
        unit[p] = unit.get(p, 0) + 1
    unit_ok = len(unit) == PERIOD[0] * PERIOD[1] and all(v == 1 for v in unit.values())
    cols = [ids.get((x, y)) for y in range(h) for x in (0, w - 1)]
    rows = [ids.get((x, y)) for x in range(w) for y in (0, h - 1)]
    edge_tones = sorted({t for t in cols + rows if t})
    res.append(checks.CheckResult("tiling_wrap_unit", unit_ok and edge_tones == [FIELD], {
        "unitPx": list(PERIOD), "tilesPerUnit": 1, "coveredOnce": unit_ok, "cells": len(unit),
        "leftRightTopBottomEdgeTones": edge_tones}))

    # 4. edge ring: the outermost ring is the field tone (no highlight, no detail, no outline)
    ring = [p for p in ids if p[0] in (0, w - 1) or p[1] in (0, h - 1)]
    ring_tones = sorted({ids[p] for p in ring})
    res.append(checks.CheckResult("edge_ring_field_tone", ring_tones == [FIELD], {"ringPixels": len(ring), "tones": ring_tones}))

    # 5. detail: 8-connected clusters of the detail tone; count, distance to the edge, distinct shapes, and
    #    visibility outside the 70 x 35 plate diamond centred on the tile (the part the plate never covers)
    det = {p for p, t in ids.items() if t == DETAIL}
    seen, clusters = set(), []
    for p in sorted(det):
        if p in seen:
            continue
        stack, comp = [p], []
        seen.add(p)
        while stack:
            q = stack.pop()
            comp.append(q)
            for dx in (-1, 0, 1):
                for dy in (-1, 0, 1):
                    n = (q[0] + dx, q[1] + dy)
                    if n in det and n not in seen:
                        seen.add(n)
                        stack.append(n)
        clusters.append(sorted(comp))
    near = [min(min(x, w - 1 - x, y, h - 1 - y) for (x, y) in comp) for comp in clusters]
    shapes = [tuple(sorted((x - min(c[0] for c in comp), y - min(c[1] for c in comp)) for (x, y) in comp)) for comp in clusters]
    pw, ph = PLATE_DIAMOND
    inside_plate = sum(1 for comp in clusters for (x, y) in comp
                       if abs(x + 0.5 - w / 2) / (pw / 2) + abs(y + 0.5 - h / 2) / (ph / 2) <= 1.0)
    res.append(checks.CheckResult("detail_clusters", len(clusters) <= 2 and all(d >= 3 for d in near)
                                  and len(set(shapes)) == len(shapes) and inside_plate == 0, {
        "clusters": len(clusters), "max": 2, "pixelsPerCluster": [len(c) for c in clusters],
        "pixelDistanceToEdge": near, "minRequired": 3, "distinctShapes": len(set(shapes)) == len(shapes),
        "detailPixels": len(det), "detailPixelsInsidePlateDiamond": inside_plate, "plateDiamond": list(PLATE_DIAMOND)}))

    # 6. tones: floor_0 and floor_1 only; floor_2, plate_* and lit tones never
    used = sorted({t for t in ids.values()})
    res.append(checks.CheckResult("floor_tones", set(used) <= {FIELD, DETAIL}, {
        "used": used, "floor_2": "unused (forbidden behind badges, rule C1)", "plate_tones": 0, "lit_tones": 0}))
    return res


def make_lattice_sheet(tile: Image.Image, palette: Palette, out: Path, scale: int = 3) -> Path:
    """3 x 3 translated tiles on the lattice (70, 0) / (0, 54), native pixels scaled by an integer, on `ui_bg`."""
    w, h = tile.size
    sheet = Image.new("RGBA", (3 * w, 3 * h), palette.rgb("ui_bg") + (255,))
    for a in range(3):
        for b in range(3):
            sheet.paste(tile, (a * w, b * h), tile)
    sheet = sheet.resize((3 * w * scale, 3 * h * scale), Image.Resampling.NEAREST)
    out.parent.mkdir(parents=True, exist_ok=True)
    sheet.save(out, format="PNG", compress_level=9, optimize=False)
    return out


def clean() -> None:
    for sub in ("model", "raw", "pixel", "sheets"):
        if (HERE / sub).exists():
            shutil.rmtree(HERE / sub)
    for f in ("provenance.json", "manifest.json", "check-report.json"):
        if (HERE / f).exists():
            (HERE / f).unlink()


def cross_checks(tile: Image.Image, palette: Palette) -> tuple[list[checks.CheckResult], dict]:
    """T434 machine bodies and T437 overlays over the real plates and THIS floor (read-only inputs). Returns the checks and
    the Assembler rim summary: gated bare-floor share, physical-floor share at the canonical phase and its worst."""
    sys.path.insert(0, str(ART / "slots"))
    import cross_family as cf  # noqa: E402

    plates = {n: Image.open(ART / "slots" / "plates" / aid / "pixel" / (aid + "_00.png")).convert("RGBA")
              for n, aid in (("empty", "f003-slot-empty"), ("locked", "f003-slot-locked-l2"),
                             ("occupied", "f003-slot-occupied-base"))}
    sources = cf.load_sources(ART)
    cross = cf.machine_outline(sources, plates["occupied"], tile, palette)
    cross += cf.overlay_checks(sources, plates, tile, palette)

    asm = sources["machines"]["assembler"]
    per_phase = {}
    for ph in PHASES:
        shares = []
        for f in asm:
            r = cf._outline_row("assembler", f, cf.world(plates["occupied"], tile, ph), palette)
            shares.append(r["minShare"])
        per_phase["%d,%d" % ph] = round(min(shares), 4)
    gated = cross[0].details["perMachine"]["assembler"]["minShare"]
    summary = {"gatedBareFloorMinShare": gated, "physicalFloorCanonicalPhase": per_phase["0,0"],
               "physicalFloorPerPhase": per_phase, "physicalFloorWorst": min(per_phase.values()),
               "threshold": c1.SHARE_THRESHOLD, "previous": {"gated": 0.9167, "note": "review F-09, 60 x 30 floor"}}
    return cross, summary


# --- driver ---------------------------------------------------------------------------------------------

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


def main() -> int:
    palette = load_palette(PALETTE_V1_PATH)
    tmp = Path(tempfile.mkdtemp(prefix="t470-models-"))
    try:
        clean()
        script_hashes = {
            "floor/build_models.py": sha256_file(HERE / "build_models.py"),
            "floor/pipeline_floor.py": sha256_file(Path(__file__)),
            "slots/cross_family.py": sha256_file(ART / "slots" / "cross_family.py"),
            "render.py": sha256_file(RENDER), "pixel_pass.py": sha256_file(REPO / "tools/art-pipeline/art_pipeline/pixel_pass.py"),
            "checks_v1.py": sha256_file(REPO / "tools/art-pipeline/art_pipeline/checks_v1.py"),
            "palette-v1.json": sha256_file(PALETTE_V1_PATH), "rig-v2.blend": sha256_file(RIG)}
        global_checks = [checks.check_palette_definition(palette)]
        problems = validate_models(load_models())
        global_checks.append(checks.CheckResult("models", not problems, {"problems": problems}))

        # Blender: build and render (headless batch, the committed final)
        run([BLENDER, "-b", "--factory-startup", "--python-exit-code", "1", "-P", str(HERE / "build_models.py"), "--", str(tmp)])
        (HERE / "model").mkdir(parents=True)
        (HERE / "raw").mkdir()
        shutil.copy2(tmp / "floor-tile.blend", HERE / "model" / "floor-tile.blend")
        stub = render_one(tmp / "floor-tile.blend", HERE / "raw", "floor-tile")
        raw = Image.open(HERE / "raw" / "floor-tile.png").convert("RGBA")

        # tile cut, files, checks
        tile = cut_tile(raw, palette)
        placed, pl = place_on_canvas([tile], TILE, TILE)
        tile = placed[0]
        fpaths = write_frames([tile], ASSET_ID, HERE / "pixel")
        frames = [{"file": p.relative_to(HERE).as_posix(), "sha256": sha256_file(p)} for p in fpaths]
        entry = {"assetId": ASSET_ID, "route": "blender", "ppu": PPU, "fps": 0, "canvas": list(pl.canvas),
                 "footprintPx": list(pl.footprint), "pivot": list(pl.pivot), "perspective": PROJ,
                 "palette": {"id": palette.palette_id, "version": palette.version}, "frames": frames}
        fchecks = [checks.check_palette([tile], palette, {"floor"}, group_limits={"floor": 5}),
                   checks.check_canvas([tile], TILE), checks.check_pivot([tile], entry), checks.check_alpha([tile]),
                   checks.check_frames([tile], 1), checks.check_overhang([tile], TILE, TILE, PPU),
                   checks.CheckResult("outline", True, {
                       "waived": True, "reason": "a floor tile has no ink outline (Art Bible 6.1, OD-5): an outline on a lattice "
                                                 "tile is a seam (brief section 6: no seam, no tone step at the shared edges; "
                                                 "edge ring is floor_0)"})]
        fchecks += tiling_checks(tile, palette)
        fchecks.append(checks.check_contrast([
            {"fg": "plate_2", "bg": "floor_0", "kind": "nontext"}, {"fg": "plate_2", "bg": "floor_1", "kind": "nontext"},
            {"fg": "ui_accent", "bg": "floor_0", "kind": "nontext"}, {"fg": "ui_accent", "bg": "floor_1", "kind": "nontext"},
            {"fg": "cue_alert", "bg": "floor_1", "kind": "nontext"}], palette, 1.0))
        allowed = c1.A_OV & c1.a_fr(palette)
        used = {palette.id_of(tuple(p[:3])) for p in tile.getdata() if p[3]}
        fchecks.append(checks.CheckResult("tones_in_A_OV_and_A_fr", not (used - set(allowed)), {
            "tones": sorted(used), "outsideAllowed": sorted(used - set(allowed))}))
        fchecks.append(checks.CheckResult("composite_v1_applicability", True, {
            "overlay_keepout": "n/a: the floor is a world layer under the machines, not a machine body (Art Bible 3.4)",
            "accent_subset": "n/a: the floor has no lit layer (floor never on a lit layer, Art Bible R5)",
            "outline_contrast": "n/a for the sprite itself (no outline, OD-5); the plates and machines over this floor are scored in the cross_* checks",
            "overlay_backdrop": "scored: every badge, marker, frame and label over the real plates and this floor (cross_*_overlay_backdrop)"}))
        make_lattice_sheet(tile, palette, HERE / "sheets" / "floor-lattice-3x3.png")

        # cross-family checks and the Assembler rim re-measurement on this floor
        cross, rim = cross_checks(tile, palette)
        rim_ok = rim["gatedBareFloorMinShare"] >= c1.SHARE_THRESHOLD and rim["physicalFloorCanonicalPhase"] >= c1.SHARE_THRESHOLD
        cross.append(checks.CheckResult("assembler_rim_share_on_floor", rim_ok, rim))

        # reproducibility: re-render and compare the raw PNG bytes
        again = tmp / "again"
        again.mkdir()
        render_one(tmp / "floor-tile.blend", again, "floor-tile")
        a, b = sha256_file(HERE / "raw" / "floor-tile.png"), sha256_file(again / "floor-tile.png")
        global_checks.append(checks.CheckResult("reproducibility", a == b, {"floor-tile": {"first": a, "second": b, "byteIdentical": a == b}}))

        report = checks.build_report({ASSET_ID: fchecks}, global_checks + cross)
        report["family"] = {
            "family": "floor", "task": "T470", "issue": 650, "artBibleVersion": ART_BIBLE, "briefVersion": BRIEF,
            "latticePeriodPx": list(PERIOD),
            "latticeNote": "70 x 54 = the T456 lattice pitch (Art Bible 3.5, v1.0.2); replaces the 60 x 30 proposal of T435",
            "crossFamilySources": "art-source/f004/machines (T434), cues, markers, slots/plates, slots/f003-slot-selected-frame and "
                                  "slots/f004-label-lock-l2..l5 (T437), read-only; world backdrop = the 70 x 54 floor translated "
                                  "under the plate, tile centre phases %s from the footprint centre" % (list(PHASES),),
            "assemblerRim": rim, "intentionallyEmpty": "no intentionallyEmpty entry in this family"}
        write_lf(HERE / "check-report.json", checks.report_json(report))
        report_hash = sha256_file(HERE / "check-report.json")

        # provenance and manifest
        rec = {
            "assetId": ASSET_ID, "revision": 3, "route": "blender", "key": KEY,
            "sources": [
                {"path": "art-source/f004/floor/build_models.py", "sha256": script_hashes["floor/build_models.py"],
                 "note": "procedural geometry, flat palette-colour materials chosen per element; no external assets"},
                {"path": "tools/art-pipeline/blender/rig-v2.blend", "sha256": script_hashes["rig-v2.blend"]},
                {"path": "art-source/f004/floor/model/floor-tile.blend", "sha256": sha256_file(HERE / "model" / "floor-tile.blend")}],
            "tools": {**stub["tools"], "python": sys.version.split()[0], "pillow": __import__("PIL").__version__},
            "parameters": {**stub["parameters"], "perspective": PROJ, "densityD": PPU, "frameCount": 1,
                           "scriptHashes": script_hashes, "renderSeconds": stub["renderSeconds"],
                           "materials": "flat emission, exact palette colours per element (tops, walls, decals)",
                           "keyLight": "rig-v2 key light unused by the emission materials"},
            "touchUp": [{"step": "tile_cut", "script": "art-source/f004/floor/pipeline_floor.py",
                         "note": "70 x 54 window of the render centre (29, 37, 99, 91), fully opaque, no mask; deterministic"}],
            "pixelPass": {"script": "tools/art-pipeline/art_pipeline/pixel_pass.py", "scriptSha256": script_hashes["pixel_pass.py"],
                          "driver": "art-source/f004/floor/pipeline_floor.py", "paletteId": palette.palette_id,
                          "paletteVersion": palette.version, "paletteSha256": script_hashes["palette-v1.json"],
                          "config": {"supersample": 1, "quantGroups": ["floor"], "outline": None,
                                     "cut": "70 x 54 window at the render centre (no mask)",
                                     "canvas": list(TILE), "footprintPx": list(TILE)}},
            "outputs": frames,
            "license": "Repository-authored procedural geometry and palette; no third-party assets, models or textures. "
                       "Ownership: repository owner (fjure); no public license grant.",
            "trainingDataNote": "N/A: no generative model or training data used (Blender route, procedural geometry "
                                "written by an AI coding agent, disclosed).",
            "producedBy": PRODUCER, "reviewedBy": None,
            "candidateSelection": "Deterministic render of fixed geometry, seed 0, 16 samples; no seed selection. T470 (Issue #650) "
                                  "revision 3: the tile is the 70 x 54 lattice period of Art Bible 3.5 (v1.0.2), rendered with rig-v2; "
                                  "the two detail clusters were moved into the visible part of the period beside the 70 x 35 plate "
                                  "diamond. Hybrid route (owner decision 2026-11-03): the geometry was checked interactively in the "
                                  "Blender MCP (screen projection of the clusters through the rig-v2 camera, 2:1 ground edges "
                                  "confirmed); the final render committed here was produced once by the headless batch "
                                  "(blender.exe -b rig-v2.blend --factory-startup --python-exit-code 1 -P render.py), and a "
                                  "second headless render is byte-identical (reproducibility check).",
            "interactiveSteps": ["Blender MCP: geometry and cluster placement verified by projecting the cluster corners through the "
                                 "rig-v2 camera rotation (60, 0, 45 degrees), orthographic 24 px per unit; no render and no save "
                                 "were made from the GUI session"],
            "artBibleVersion": ART_BIBLE, "briefVersion": BRIEF,
            "rigVersion": stub["parameters"]["rigVersion"], "renderEnv": stub["renderEnv"],
            "checkReportHash": report_hash, "checkReportPath": "art-source/f004/floor/check-report.json",
        }
        write_lf(HERE / "provenance.json", json.dumps(rec, indent=2, sort_keys=True) + "\n")
        gaps = missing_provenance_fields(rec, "blender")
        man = build_manifest([entry])
        man["note"] = "T470 candidate entry for T471; reviewedBy pending (T439/T471). Floor tile 70 x 54, pivot (35, 27)."
        write_lf(HERE / "manifest.json", json.dumps(man, indent=2, sort_keys=True) + "\n")

        print("CHECKS-PASSED" if report["passed"] else "CHECKS-FAILED")
        print("provenance gaps:", gaps)
        print("assembler rim:", json.dumps(rim))
        for r in fchecks + global_checks + cross:
            print(r.name, r.passed)
        return 0 if report["passed"] else 1
    finally:
        shutil.rmtree(tmp, ignore_errors=True)


if __name__ == "__main__":
    raise SystemExit(main())
