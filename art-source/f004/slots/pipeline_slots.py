"""F004 T435 driver: models -> Blender renders -> pixel pass -> checks -> provenance -> reports (slots and floor).

Run from the repository root (BLENDER_EXE optional); the lease `gpu` must be held for the renders:

  uv run --frozen --offline --project tools/art-pipeline python art-source/f004/slots/pipeline_slots.py

Builds the floor tile (`art-source/f004/floor/pipeline_floor.py`), then the plates, then the cross-family checks of the
real plates and floor against the machines (T434) and overlays (T437), and writes for each family:
`plates/<asset>/pixel/` + `provenance.json` and `source/<plate>/model|raw/` for slots (one render shared by the four
locked keys), `floor/model|raw|pixel/` + `provenance.json` for the floor, `manifest.json`
and the family `check-report.json`. Nothing here is a shipped manifest (`reviewedBy` stays empty for T439). The T437
files under `slots/` (`f003-slot-selected-frame/`, `f004-label-lock-l2..l5/`, `manifest-t437.json`,
`check-report-t437.json`) are read-only inputs and are never written.
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
ART = HERE.parent
PLATES = HERE / "plates"           # not directly under slots/: T437's test globs slots/*/provenance.json (scripted route only)
sys.path.insert(0, str(REPO / "tools" / "art-pipeline"))
sys.path.insert(0, str(HERE))
sys.path.insert(0, str(ART / "floor"))

from PIL import Image  # noqa: E402

import cross_family as cf  # noqa: E402
import pipeline_floor as fl  # noqa: E402
from art_pipeline import checks, checks_v1 as c1  # noqa: E402
from art_pipeline.color import contrast_ratio, luma  # noqa: E402
from art_pipeline.palette import PALETTE_V1_PATH, load_palette  # noqa: E402
from art_pipeline.pixel_pass import (  # noqa: E402
    PassConfig, add_outline, build_manifest, nearest_palette_index, opaque_bbox, place_on_canvas, reduce_for_route, run_pixel_pass,
    threshold_alpha, write_frames)
from art_pipeline.provenance import load_models, missing_provenance_fields, validate_models  # noqa: E402

BLENDER = os.environ.get("BLENDER_EXE") or r"C:\Program Files\Blender Foundation\Blender 5.2\blender.exe"
RIG = REPO / "tools" / "art-pipeline" / "blender" / "rig-v1.blend"
RENDER = REPO / "tools" / "art-pipeline" / "blender" / "render.py"
PROJ = "isometric-2-1"
CANVAS, FOOTPRINT, PPU = (80, 64), (68, 30), 24
PLATE_BOX = (5, 29, 75, 64)              # plate diamond incl. outline: 70 x 35, centre 2.5 art px above the pivot
ART_BIBLE, BRIEF = "F004-PAB v1.0", "F004-AB v1.0"
GROUPS = {"floor", "ui", "machine", "ink"}                         # family allowance of plates and floor (Art Bible 5)
GROUP_LIMITS = {"floor": 5, "ui": 4, "machine": 10, "ink": 1}      # per-asset maxima (Art Bible 5)
DIGIT_ZONE = (34, 40, 47, 59)                                      # label bbox 35..46 x 41..58 plus a 1 px margin
SEED = 435
PRODUCER = "3D and Render Artist (Claude Code Sonnet 5.5, Art Director lens self-check), T435 / Issue #549"

# plate render -> keys (assetIds). The four locked keys use identical pixels (brief section 2).
KEYS = {
    "f003-slot-empty": ("F003-SLOT-EMPTY", "empty"),
    "f003-slot-locked-l2": ("F003-SLOT-LOCKED-L2", "locked"),
    "f003-slot-locked-l3": ("F003-SLOT-LOCKED-L3", "locked"),
    "f003-slot-locked-l4": ("F003-SLOT-LOCKED-L4", "locked"),
    "f003-slot-locked-l5": ("F003-SLOT-LOCKED-L5", "locked"),
    "f003-slot-occupied-base": ("F003-SLOT-OCCUPIED-BASE", "occupied"),
}


def write_lf(path: Path, text: str) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
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


def named(r: checks.CheckResult, name: str) -> checks.CheckResult:
    r.name = name
    return r


# --- plate pixel pass: restricted tone set, touch-up (scripted, deterministic) -----------------------------

# Declared tone set per plate (before the outline). Anti-aliased blends of the render snap to the nearest of these
# (CIELAB, lowest-index tie-break, no dithering), so no stray palette tone (plate_0, steel_0, ui_panel, ...) can appear.
TONES = {
    "occupied": ("plate_1", "plate_2", "ink_0"),
    "empty": ("steel_1", "steel_3", "plate_2", "ink_0"),
    "locked": ("plate_1", "plate_2", "dim_0", "ui_line", "steel_1", "steel_3", "ink_0"),
}


def snap_to_tones(img: Image.Image, palette, ids: tuple[str, ...]) -> Image.Image:
    rgbs = [palette.rgb(i) for i in ids]
    cache: dict = {}
    out = Image.new("RGBA", img.size, (0, 0, 0, 0))
    src, dst = img.load(), out.load()
    for y in range(img.height):
        for x in range(img.width):
            r, g, b, a = src[x, y]
            if a:
                if (r, g, b) not in cache:
                    cache[(r, g, b)] = rgbs[nearest_palette_index((r, g, b), rgbs)]
                dst[x, y] = cache[(r, g, b)] + (255,)
    return out


def despeckle(img: Image.Image) -> tuple[Image.Image, int]:
    """Remove single-pixel noise: an opaque pixel whose four 4-neighbours are all opaque and of one other colour
    takes that colour (the anti-aliased blend where two decals or a wall and its skirt meet). Reads the input only
    (no cascade), row-major, deterministic. Returns the image and the number of pixels changed."""
    out = img.copy()
    src, dst = img.load(), out.load()
    w, h = img.size
    n = 0
    for y in range(1, h - 1):
        for x in range(1, w - 1):
            if not src[x, y][3]:
                continue
            nb = [src[x + dx, y + dy] for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1))]
            for c in sorted(set(nb)):
                if c[3] and c != src[x, y] and nb.count(c) == 4:
                    dst[x, y] = c
                    n += 1
                    break
    return out, n


def plate_pass(raw: Image.Image, palette, kind: str):
    """reduce (blender route, factor 1) -> alpha threshold -> snap to the declared tones -> 1 px ink_0 outline ->
    placement on the 80 x 64 canvas (shared with machines: footprint 68 x 30 centred, resting on the bottom)."""
    f = reduce_for_route(raw, "blender", supersample=1)
    f = threshold_alpha(f)
    f = snap_to_tones(f, palette, TONES[kind])
    f, specks = despeckle(f)
    f = add_outline(f, palette.rgb("ink_0"))
    placed, pl = place_on_canvas([f], CANVAS, FOOTPRINT)
    return placed[0], pl, specks

def clear_digit_zone(img: Image.Image, palette) -> tuple[Image.Image, int]:
    """Keep the zone under the T437 lock-level digit free of hatch: `ui_line` pixels inside DIGIT_ZONE become the plate
    field tone `dim_0` (the digit is centred on the footprint and sits on `dim_0`, contrast 11.55). Returns the image and
    the number of pixels changed."""
    out = img.copy()
    px = out.load()
    x0, y0, x1, y1 = DIGIT_ZONE
    n = 0
    for y in range(y0, y1 + 1):
        for x in range(x0, x1 + 1):
            if px[x, y][3] and palette.id_of(tuple(px[x, y][:3])) == "ui_line":
                px[x, y] = palette.rgb("dim_0") + (255,)
                n += 1
    return out, n


# --- plate checks --------------------------------------------------------------------------------------

def rim_plate2_share(img: Image.Image, base: Image.Image, palette):
    """Share of outline pixels (section 9.3 definition, 4-neighbour) whose rim is plate_2, over the outline pixels that
    belong to the plate diamond (those the occupied plate also has); the padlock overhang of the locked plate carries
    its own steel rim, which reaches 3:1 over the floor (cross-checked by outline_contrast_bare_floor)."""
    def outline(im):
        px = im.load()
        w, h = im.size
        n4 = ((1, 0), (-1, 0), (0, 1), (0, -1))
        op = lambda x, y: 0 <= x < w and 0 <= y < h and px[x, y][3] != 0
        return {(x, y) for y in range(h) for x in range(w) if op(x, y) and any(not op(x + dx, y + dy) for dx, dy in n4)}, px, op, n4
    out_b, _pb, _ob, _n = outline(base)
    out_i, px, op, n4 = outline(img)
    p2 = palette.rgb("plate_2")
    sel = sorted(out_i & out_b)
    good = 0
    for (x, y) in sel:
        for dx, dy in n4:
            if op(x + dx, y + dy) and (x + dx, y + dy) not in out_i:
                good += tuple(px[x + dx, y + dy][:3]) == p2
                break
    return good / len(sel), len(sel), "outline pixels shared with the occupied plate silhouette"


def plate_checks(kind: str, img: Image.Image, entry: dict, palette, base: Image.Image) -> list[checks.CheckResult]:
    frames = [img]
    res = [
        checks.check_palette(frames, palette, GROUPS, group_limits=GROUP_LIMITS),
        checks.check_canvas(frames, CANVAS), checks.check_pivot(frames, entry), checks.check_alpha(frames),
        checks.check_outline(frames, palette), checks.check_frames(frames, 1),
        checks.check_overhang(frames, CANVAS, FOOTPRINT, PPU),
    ]
    box = opaque_bbox(img)
    res.append(checks.CheckResult("plate_diamond", box == PLATE_BOX, {
        "bbox": list(box) if box else None, "expected": list(PLATE_BOX), "size": [PLATE_BOX[2] - PLATE_BOX[0], PLATE_BOX[3] - PLATE_BOX[1]],
        "centreAbovePivotPx": 2.5, "note": "70 x 35 diamond incl. the 1 px outline; hit shape of T456 (Art Bible 3.2a)"}))
    # contrast of the plate against the floor (Art Bible 9.3, C2): outline OR rim, composite-mask share over bare floor
    r = c1.check_outline_contrast(img, None, palette)
    res.append(named(r, "outline_contrast_bare_floor"))
    share, n, scope = rim_plate2_share(img, base, palette)
    res.append(checks.CheckResult("plate_rim_is_plate_2", share >= c1.SHARE_THRESHOLD, {
        "outlinePixels": n, "rimPlate2Share": round(share, 4), "threshold": c1.SHARE_THRESHOLD, "scope": scope,
        "rule": "C2: the outer rim of every plate is plate_2 (plate_2 vs floor_0 4.70, vs floor_1 3.87)"}))
    res.append(cf.tone_audit("tones_in_A_OV", img, palette, c1.A_OV))
    res.append(cf.tone_audit("tones_no_lit", img, palette, set(c.id for c in palette.colors) - {"lit_0", "lit_1"}))
    if kind == "occupied":
        res.append(cf.tone_audit("tones_in_C5_set", img, palette, c1.BODY_BACKDROP_TONES))
    res.append(checks.check_contrast([
        {"fg": "plate_2", "bg": "floor_0", "kind": "nontext"}, {"fg": "plate_2", "bg": "floor_1", "kind": "nontext"},
        {"fg": "ink_0", "bg": "plate_1", "kind": "nontext"}, {"fg": "ink_0", "bg": "plate_2", "kind": "nontext"}], palette, 1.0))
    tones = {}
    for p in img.getdata():
        if p[3]:
            t = palette.id_of(tuple(p[:3]))
            tones[t] = tones.get(t, 0) + 1
    if kind == "empty":
        res.append(checks.CheckResult("corner_marks", tones.get("steel_3", 0) >= 20 and tones.get("plate_2", 0) >= 20, {
            "steel_3Px": tones.get("steel_3", 0), "plate_2Px": tones.get("plate_2", 0)}))
    if kind == "locked":
        px = img.load()
        zx0, zy0, zx1, zy1 = DIGIT_ZONE
        zone = {palette.id_of(tuple(px[x, y][:3])) for y in range(zy0, zy1 + 1) for x in range(zx0, zx1 + 1) if px[x, y][3]}
        res.append(checks.CheckResult("padlock_and_hatch", tones.get("steel_3", 0) >= 20 and tones.get("ui_line", 0) >= 40, {
            "padlock_steel_3Px": tones.get("steel_3", 0), "hatch_ui_line_Px": tones.get("ui_line", 0)}))
        res.append(checks.CheckResult("digit_zone_clear", not (zone & {"ui_line", "steel_2", "steel_3"}), {
            "zone": list(DIGIT_ZONE), "tones": sorted(zone), "forbiddenInZone": ["ui_line", "steel_2", "steel_3"],
            "note": "no hatch or padlock under the T437 digit; the field is dim_0 (ui_text on dim_0 11.55), only the "
                    "bottom rows of the zone reach the plate_1 bezel and plate_2 ring"}))
    return res


def distinct(plates: dict[str, Image.Image], palette) -> checks.CheckResult:
    """The three conditions differ clearly: fraction of differing pixels inside the diamond, mean luma of the interior."""
    names = sorted(plates)
    out, ok = {}, True
    for i, a in enumerate(names):
        for b in names[i + 1:]:
            pa, pb = plates[a].load(), plates[b].load()
            tot = diff = 0
            for y in range(PLATE_BOX[1], PLATE_BOX[3]):
                for x in range(PLATE_BOX[0], PLATE_BOX[2]):
                    if pa[x, y][3] or pb[x, y][3]:
                        tot += 1
                        diff += pa[x, y] != pb[x, y]
            out["%s/%s" % (a, b)] = round(diff / tot, 4)
            ok &= diff / tot >= 0.30
    lum = {}
    for n, img in plates.items():
        vs = [luma(tuple(p[:3])) for p in img.getdata() if p[3] and palette.id_of(tuple(p[:3])) not in ("ink_0", "plate_2")]
        lum[n] = round(sum(vs) / len(vs), 1)
    return checks.CheckResult("condition_distinct", ok, {"differingPixelShare": out, "minRequired": 0.30,
                                                        "meanInteriorLuma": lum})


def main() -> int:
    palette = load_palette(PALETTE_V1_PATH)
    tmp = Path(tempfile.mkdtemp(prefix="t435-models-"))
    try:
        # ---------------------------------------------------------------- clean outputs of this task only
        fl.clean()
        for d in ("plates", "source", "sheets"):
            if (HERE / d).exists():
                shutil.rmtree(HERE / d)
        for f in ("provenance.json", "manifest.json", "check-report.json"):
            if (HERE / f).exists():
                (HERE / f).unlink()
        script_hashes = {
            "slots/build_models.py": sha256_file(HERE / "build_models.py"),
            "slots/pipeline_slots.py": sha256_file(Path(__file__)),
            "slots/cross_family.py": sha256_file(HERE / "cross_family.py"),
            "floor/build_models.py": sha256_file(ART / "floor" / "build_models.py"),
            "floor/pipeline_floor.py": sha256_file(ART / "floor" / "pipeline_floor.py"),
            "render.py": sha256_file(RENDER), "pixel_pass.py": sha256_file(REPO / "tools/art-pipeline/art_pipeline/pixel_pass.py"),
            "checks_v1.py": sha256_file(REPO / "tools/art-pipeline/art_pipeline/checks_v1.py"),
            "palette-v1.json": sha256_file(PALETTE_V1_PATH), "rig-v1.blend": sha256_file(RIG)}
        global_models = [checks.check_palette_definition(palette)]
        problems = validate_models(load_models())
        global_models.append(checks.CheckResult("models", not problems, {"problems": problems}))

        # ---------------------------------------------------------------- Blender: build and render both families
        run([BLENDER, "-b", "--factory-startup", "--python-exit-code", "1", "-P", str(HERE / "build_models.py"), "--", str(tmp)])
        run([BLENDER, "-b", "--factory-startup", "--python-exit-code", "1", "-P", str(ART / "floor" / "build_models.py"), "--", str(tmp)])
        stubs, raws = {}, {}
        for src in ("empty", "locked", "occupied", "floor-tile"):
            fam = ART / "floor" if src == "floor-tile" else HERE / "source" / src
            (fam / "model").mkdir(parents=True, exist_ok=True)
            (fam / "raw").mkdir(exist_ok=True)
            shutil.copy2(tmp / (src + ".blend"), fam / "model" / (src + ".blend"))
            stubs[src] = render_one(tmp / (src + ".blend"), fam / "raw", src)
            raws[src] = Image.open(fam / "raw" / (src + ".png")).convert("RGBA")

        # ---------------------------------------------------------------- floor: cut, tiling checks, files
        tile = fl.cut_tile(raws["floor-tile"], palette)
        (placed,), fplace = place_on_canvas_one(tile)
        tile = placed
        fpaths = write_frames([tile], fl.ASSET_ID, ART / "floor" / "pixel")
        fentry = {"assetId": fl.ASSET_ID, "route": "blender", "ppu": PPU, "fps": 0, "canvas": list(fplace.canvas),
                  "footprintPx": list(fplace.footprint), "pivot": list(fplace.pivot), "perspective": PROJ,
                  "palette": {"id": palette.palette_id, "version": palette.version},
                  "frames": [{"file": p.relative_to(ART / "floor").as_posix(), "sha256": sha256_file(p)} for p in fpaths]}
        fchecks = [checks.check_palette([tile], palette, {"floor"}, group_limits={"floor": 5}),
                   checks.check_canvas([tile], fl.TILE), checks.check_pivot([tile], fentry), checks.check_alpha([tile]),
                   checks.check_frames([tile], 1), checks.check_overhang([tile], fl.TILE, fl.TILE, PPU),
                   checks.CheckResult("outline", True, {
                       "waived": True, "reason": "a floor tile has no ink outline: an outline on a lattice tile is a seam "
                                                 "(brief section 6: no seam, no tone step at the shared edges; edge ring is floor_0)"})]
        fchecks += fl.tiling_checks(tile, palette)
        fchecks.append(checks.check_contrast([
            {"fg": "plate_2", "bg": "floor_0", "kind": "nontext"}, {"fg": "plate_2", "bg": "floor_1", "kind": "nontext"},
            {"fg": "ui_accent", "bg": "floor_0", "kind": "nontext"}, {"fg": "ui_accent", "bg": "floor_1", "kind": "nontext"},
            {"fg": "cue_alert", "bg": "floor_1", "kind": "nontext"}], palette, 1.0))
        fchecks.append(cf.tone_audit("tones_in_A_OV_and_A_fr", tile, palette, c1.A_OV & c1.a_fr(palette)))
        fl.make_lattice_sheet(tile, palette, ART / "floor" / "sheets" / "floor-lattice-3x3.png")

        # ---------------------------------------------------------------- plates: pixel pass, touch-up, files
        pix: dict[str, Image.Image] = {}
        placements = {}
        touch = {}
        specks = {}
        for src in ("empty", "locked", "occupied"):
            img, pl, specks[src] = plate_pass(raws[src], palette, src)
            touch[src] = None
            if src == "locked":
                img, n = clear_digit_zone(img, palette)
                touch[src] = n
            pix[src], placements[src] = img, pl
        entries, plate_by_id, asset_checks = [], {}, {}
        for aid, (key, src) in KEYS.items():
            img, pl = pix[src], placements[src]
            ppath = write_frames([img], aid, PLATES / aid / "pixel")
            e = {"assetId": aid, "route": "blender", "ppu": PPU, "fps": 0, "canvas": list(pl.canvas),
                 "footprintPx": list(pl.footprint), "pivot": list(pl.pivot), "perspective": PROJ,
                 "palette": {"id": palette.palette_id, "version": palette.version},
                 "frames": [{"file": p.relative_to(HERE).as_posix(), "sha256": sha256_file(p)} for p in ppath]}
            entries.append(e)
            plate_by_id[aid] = (e, img, src)
            asset_checks[aid] = plate_checks(src, img, e, palette, pix["occupied"])
        asset_checks[fl.ASSET_ID] = fchecks

        # ---------------------------------------------------------------- global and cross-family checks
        sources = cf.load_sources(ART)
        plates_by_cond = {"empty": pix["empty"], "locked": pix["locked"], "occupied": pix["occupied"]}
        cross = cf.machine_outline(sources, pix["occupied"], tile, palette)
        cross += cf.overlay_checks(sources, plates_by_cond, tile, palette)
        # the badge, marker, frame and label keep-out reference stays T437's: here only the world they sit on is checked
        global_slots = list(global_models)
        masks = {n: tuple(sorted((x, y) for y in range(CANVAS[1]) for x in range(CANVAS[0]) if im.getpixel((x, y))[3])) for n, im in pix.items()}
        base = set(masks["occupied"])
        extra = {n: len(set(m) - base) for n, m in masks.items()}
        missing = {n: len(base - set(m)) for n, m in masks.items()}
        boxes = {n: list(opaque_bbox(im)) for n, im in pix.items()}
        global_slots.append(checks.CheckResult("plate_silhouettes_consistent", len({tuple(b) for b in boxes.values()}) == 1
                                               and not any(missing.values()) and max(extra.values()) <= 40, {
            "bbox": boxes, "pixelsOutsideTheOccupiedSilhouette": extra, "pixelsMissing": missing, "limitExtraPx": 40,
            "note": "all conditions share the 70 x 35 diamond (hit shape, footprint); the locked padlock may stand a few "
                    "pixels over the upper-left edge (plate overhang is allowed up to 12 px, Art Bible 3.4)"}))
        global_slots.append(distinct(plates_by_cond, palette))
        locked_bytes = {aid: sha256_file(PLATES / aid / "pixel" / (aid + "_00.png")) for aid in KEYS if KEYS[aid][1] == "locked"}
        global_slots.append(checks.CheckResult("locked_keys_identical_pixels", len(set(locked_bytes.values())) == 1, {
            "sha256": sorted(set(locked_bytes.values())), "keys": sorted(locked_bytes)}))
        global_slots.append(checks.CheckResult("entry_set", sorted(KEYS) == sorted(e["assetId"] for e in entries) and len(entries) == 6, {
            "entries": [e["assetId"] for e in entries], "keys": [v[0] for v in KEYS.values()]}))
        global_slots.append(checks.CheckResult("selected_condition", sources["frame"].size == CANVAS, {
            "note": "the selected condition is F003-SLOT-SELECTED-FRAME of T437 (scripted overlay) over the occupied plate; "
                    "its backdrop, outline and tone checks against these plates are in the cross_frame_* checks"}))
        # reproducibility: re-render every source and compare the raw PNG bytes
        repro = {}
        for src in ("empty", "locked", "occupied", "floor-tile"):
            again = tmp / ("again-" + src)
            again.mkdir()
            render_one(tmp / (src + ".blend"), again, src)
            base = (ART / "floor" if src == "floor-tile" else HERE / "source" / src) / "raw" / (src + ".png")
            a, b = sha256_file(base), sha256_file(again / (src + ".png"))
            repro[src] = {"first": a, "second": b, "byteIdentical": a == b}
        repro_check = checks.CheckResult("reproducibility", all(v["byteIdentical"] for v in repro.values()), repro)
        global_slots.append(repro_check)
        global_floor = list(global_models) + [repro_check]
        fam_mb = sum(f.stat().st_size for d in (HERE, ART / "floor") for f in d.rglob("*") if f.is_file()) / (1024 * 1024)
        global_slots.append(checks.CheckResult("family_size_budget", fam_mb <= checks.FAMILY_BUDGET_MB, {
            "slotsAndFloorFilesMB": round(fam_mb, 1), "limitMB": checks.FAMILY_BUDGET_MB,
            "note": "includes the T437 files in slots/ and the model .blend sources"}))

        # ---------------------------------------------------------------- sheets (review, shuffled grayscale and CVD)
        sprites = {}
        bg0 = cf.lattice_floor(tile, (0, 0))
        def on_floor(*layers):
            out = bg0.copy()
            for l in layers:
                out.paste(l, (0, 0), l)
            return out
        miner0, assembler0 = sources["machines"]["miner"][0], sources["machines"]["assembler"][0]
        sprites["slot-empty"] = on_floor(pix["empty"])
        sprites["slot-locked-l3"] = on_floor(pix["locked"], sources["labels"]["f004-label-lock-l3"])
        sprites["slot-occupied-miner"] = on_floor(pix["occupied"], miner0)
        sprites["slot-selected-assembler"] = on_floor(pix["occupied"], assembler0, sources["frame"])
        sheet_info = checks.make_review_sheets(sprites, SEED, HERE / "sheets", scale=3, background=palette.rgb("ui_bg"))
        for p in (HERE / "sheets").glob("*.json"):
            write_lf(p, p.read_text(encoding="utf-8"))

        # ---------------------------------------------------------------- reports
        slots_report = checks.build_report({k: v for k, v in asset_checks.items() if k != fl.ASSET_ID},
                                           global_slots + cross)
        floor_report = checks.build_report({fl.ASSET_ID: fchecks}, global_floor + cross)
        meta = {"task": "T435", "issue": 549, "artBibleVersion": ART_BIBLE, "briefVersion": BRIEF,
                "crossFamilySources": "art-source/f004/machines (T434), cues, markers, slots/f003-slot-selected-frame and "
                                      "slots/f004-label-lock-l2..l5 (T437), read-only; world backdrop = floor lattice under the plate, "
                                      "phases %s" % (list(cf.PHASES),),
                "intentionallyEmpty": "no intentionallyEmpty entry in these families"}
        slots_report["family"] = {"family": "slots", **meta,
                                  "touchUp": {"digitZoneClear": {"zone": list(DIGIT_ZONE), "pixelsChanged": touch["locked"]},
                                              "despeckle": specks},
                                  "selected": "F003-SLOT-SELECTED-FRAME is T437's scripted overlay; it passes against these plates (cross_frame_*)"}
        floor_report["family"] = {"family": "floor", **meta, "latticePeriodPx": [60, 30],
                                  "latticeNote": "60 x 30 (2:1 diamond) proposed by T435 from the Art Bible 3.2a candidate; T456 confirms"}
        write_lf(HERE / "check-report.json", checks.report_json(slots_report))
        write_lf(ART / "floor" / "check-report.json", checks.report_json(floor_report))
        slots_hash, floor_hash = sha256_file(HERE / "check-report.json"), sha256_file(ART / "floor" / "check-report.json")

        # ---------------------------------------------------------------- provenance and manifests
        gaps = {}
        def provenance(aid, key, src, family_dir, fam_path, entry, stub, report_hash, report_path, touch_note):
            model_rel = (fam_path + "/" if fam_path else "") + "model/%s.blend" % src
            first = stub
            rec = {
                "assetId": aid, "revision": 1, "route": "blender", "key": key,
                "sources": [
                    {"path": "art-source/f004/%s/build_models.py" % ("floor" if src == "floor-tile" else "slots"),
                     "sha256": script_hashes["floor/build_models.py" if src == "floor-tile" else "slots/build_models.py"],
                     "note": "procedural geometry, flat palette-colour materials chosen per element; no external assets"},
                    {"path": "tools/art-pipeline/blender/rig-v1.blend", "sha256": script_hashes["rig-v1.blend"]},
                    {"path": "art-source/f004/" + model_rel, "sha256": sha256_file(ART / model_rel)}],
                "tools": {**first["tools"], "python": sys.version.split()[0], "pillow": __import__("PIL").__version__},
                "parameters": {**first["parameters"], "perspective": PROJ, "densityD": PPU, "frameCount": 1,
                               "scriptHashes": script_hashes, "renderSeconds": first["renderSeconds"],
                               "materials": "flat emission, exact palette colours per element (tops, walls, decals)",
                               "keyLight": "rig-v1 key light unused by the emission materials"},
                "touchUp": touch_note,
                "pixelPass": {"script": "tools/art-pipeline/art_pipeline/pixel_pass.py", "scriptSha256": script_hashes["pixel_pass.py"],
                              "driver": "art-source/f004/slots/pipeline_slots.py", "paletteId": palette.palette_id,
                              "paletteVersion": palette.version, "paletteSha256": script_hashes["palette-v1.json"],
                              "config": ({"supersample": 1, "quantGroups": ["floor"], "outline": None,
                                          "cut": "60 x 30 window at the render centre, then the exact 2:1 diamond mask",
                                          "canvas": list(fl.TILE), "footprintPx": list(fl.TILE)} if src == "floor-tile" else
                                         {"supersample": 1, "tonesBeforeOutline": list(TONES[src]), "outline": "ink_0",
                                          "canvas": list(CANVAS), "footprintPx": list(FOOTPRINT)})},
                "outputs": entry["frames"],
                "license": "Repository-authored procedural geometry and palette; no third-party assets, models or textures. "
                           "Ownership: repository owner (fjure); no public license grant.",
                "trainingDataNote": "N/A: no generative model or training data used (Blender route, procedural geometry "
                                    "written by an AI coding agent, disclosed).",
                "producedBy": PRODUCER, "reviewedBy": None,
                "candidateSelection": "Deterministic render of fixed geometry, seed 0, 16 samples; geometry, tones and decal "
                                      "positions iterated by measurement of rim share, tone sets and tiling during production "
                                      "(the final render is the one committed); no seed selection.",
                "artBibleVersion": ART_BIBLE, "briefVersion": BRIEF,
                "rigVersion": first["parameters"]["rigVersion"], "renderEnv": first["renderEnv"],
                "checkReportHash": report_hash, "checkReportPath": report_path,
            }
            write_lf(family_dir / "provenance.json", json.dumps(rec, indent=2, sort_keys=True) + "\n")
            gaps[aid] = missing_provenance_fields(rec, "blender")

        for aid, (e, img, src) in plate_by_id.items():
            note = [{"step": "despeckle", "script": "art-source/f004/slots/pipeline_slots.py",
                     "note": "single-pixel anti-aliasing blends whose four neighbours share one colour take that colour (%d pixels); deterministic" % specks[src]}]
            note += [{"step": "digit_zone_clear", "script": "art-source/f004/slots/pipeline_slots.py",
                     "note": "ui_line hatch pixels inside the T437 label zone %s become dim_0 (%d pixels); deterministic" % (list(DIGIT_ZONE), touch["locked"])}] if src == "locked" else []
            provenance(aid, KEYS[aid][0], src, PLATES / aid, "slots/source/" + src, e, stubs[src], slots_hash,
                       "art-source/f004/slots/check-report.json", note)
        # model/raw of the plates live under slots/source/<plate>/; paths in provenance are relative to art-source/f004/
        provenance(fl.ASSET_ID, fl.KEY, "floor-tile", ART / "floor", "floor", fentry, stubs["floor-tile"], floor_hash,
                   "art-source/f004/floor/check-report.json",
                   [{"step": "tile_cut", "script": "art-source/f004/floor/pipeline_floor.py",
                     "note": "60 x 30 window of the render, exact 2:1 diamond mask (rows of 4 * min(y, 29 - y) + 2 px); deterministic"}])
        sman = build_manifest(entries)
        sman["note"] = "T435 candidate entries for T440; reviewedBy pending (T439). The four locked keys share identical pixels."
        write_lf(HERE / "manifest.json", json.dumps(sman, indent=2, sort_keys=True) + "\n")
        fman = build_manifest([fentry])
        fman["note"] = "T435 candidate entry for T440; reviewedBy pending (T439). Floor tile 60 x 30, pivot (30, 15)."
        write_lf(ART / "floor" / "manifest.json", json.dumps(fman, indent=2, sort_keys=True) + "\n")

        ok = slots_report["passed"] and floor_report["passed"]
        print("CHECKS-PASSED" if ok else "CHECKS-FAILED")
        print("provenance gaps:", {k: v for k, v in gaps.items() if v})
        for a, rs in sorted(asset_checks.items()):
            bad = [r.name for r in rs if not r.passed]
            print(a, "FAIL " + ",".join(bad) if bad else "ok")
        for r in global_slots + cross:
            print("global" if r in global_slots else "cross", r.name, r.passed)
        return 0 if ok else 1
    finally:
        shutil.rmtree(tmp, ignore_errors=True)


def place_on_canvas_one(tile: Image.Image):
    from art_pipeline.pixel_pass import place_on_canvas
    placed, pl = place_on_canvas([tile], fl.TILE, fl.TILE)
    return placed, pl


if __name__ == "__main__":
    raise SystemExit(main())
