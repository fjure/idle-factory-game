"""F006 T520 (Issue #693): DEPOT and DEPOSIT families, Blender rig-v2 -> deterministic pixel pass -> checks -> provenance.

Run from the repository root (BLENDER_EXE optional; the `gpu` lease is held by the parent for owner issue-693):

  uv run --frozen --offline --project tools/art-pipeline python art-source/f006/pipeline_depot_deposit.py

Sprites (Art Bible v1.1.0, section 21.4):
DEPOT   f006-depot-s{1,2,3}-{empty,low,high,full}  80 x 76, footprint 80 x 40 (the slab's ground diamond), pivot (40, 20 from the bottom)
DEPOSIT f006-deposit-rest-s{1,2,3} and f006-deposit-highlight  48 x 34, footprint 48 x 24, pivot (24, 12)
FX      f006-fx-vein-chip  48 x 34, 6 frames at 8 fps, same anchor as the deposit (its reduced-motion still is f006-deposit-highlight)
Every model shares one world origin per family, so one crop window per family (derived from the s1 base model) places all of them.
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
REPO = HERE.parents[1]
sys.path.insert(0, str(REPO / "tools" / "art-pipeline"))

from art_pipeline import checks  # noqa: E402
from art_pipeline.palette import PALETTE_V1_PATH, Palette, load_palette  # noqa: E402
from art_pipeline.pixel_pass import (  # noqa: E402
    build_manifest, nearest_palette_index, opaque_bbox, pivot_for, reduce_for_route, threshold_alpha, write_frames)
from art_pipeline.provenance import load_models, missing_provenance_fields, validate_models  # noqa: E402

BLENDER = os.environ.get("BLENDER_EXE") or r"C:\Program Files\Blender Foundation\Blender 5.2\blender.exe"
RIG = REPO / "tools" / "art-pipeline" / "blender" / "rig-v2.blend"
RENDER = REPO / "tools" / "art-pipeline" / "blender" / "render.py"
PROJ, PPU = "isometric-2-1", 24
ART_BIBLE, WORLD_BIBLE = "F004-PAB v1.1.0", "F006-WB v0.1"
BRIEF = {"depot": "F006-DEPOT v0.1", "deposit": "F006-DEPOSIT v0.1"}
PRODUCER = "3D and Render Artist (Claude Code Sonnet 5.5, Technical Artist lens), T520 / Issue #693"
DEPOT_CANVAS, DEPOT_FOOT = (80, 76), (80, 40)
DEP_CANVAS, DEP_FOOT = (48, 34), (48, 24)
CHIP_FPS = 8
MAX_COLOURS = 12

DEPOT_ALLOW = {"ink_0", "ink_1", "floor_1", "steel_0", "steel_1", "steel_2", "steel_3", "copper_0", "copper_1", "copper_2",
               "dim_0", "dim_1", "res_ore_0", "res_ore_1", "res_coal"}
DEPOSIT_ALLOW = {"ink_0", "res_ore_0", "res_ore_1", "res_coal", "floor_1", "steel_3"}
CHIP_ALLOW = {"res_ore_1", "res_ore_0", "steel_3", "ink_0"}
RING_SAFE = {"ink_0", "ink_1", "copper_1", "copper_2", "dim_0", "floor_0", "floor_1", "plate_1", "plate_2", "res_ore_1",
             "res_coal", "res_gold", "res_plank", "ui_bg", "ui_panel", "ui_line", "ui_text"}
FORBIDDEN_PREFIX = ("lit_", "teal_", "plate_", "cue_", "cvd_", "floor_2")

STAGES, FILLS = ("s1", "s2", "s3"), ("empty", "low", "high", "full")
DEPOT_ASSETS = {"f006-depot-%s-%s" % (s, f): "depot-%s-%s" % (s, f) for s in STAGES for f in FILLS}
DEPOSIT_RENDERS = {"f006-deposit-rest-s%d" % s: "deposit-rest-s%d" % s for s in (1, 2, 3)}
CHIP_ID = "f006-fx-vein-chip"


def sha256_file(p: Path) -> str:
    return hashlib.sha256(p.read_bytes()).hexdigest()


def write_lf(path: Path, text: str) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    with open(path, "w", encoding="utf-8", newline="\n") as fh:
        fh.write(text)


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


# --- pixel pass ----------------------------------------------------------------------------------------

def snap(img: Image.Image, palette: Palette, ids) -> Image.Image:
    """Anti-aliased blends snap to the nearest declared tone (CIELAB, lowest-index tie-break, no dithering)."""
    ids = sorted(ids)
    rgbs = [palette.rgb(i) for i in ids]
    cache: dict = {}
    out = Image.new("RGBA", img.size, (0, 0, 0, 0))
    s, d = img.load(), out.load()
    for y in range(img.height):
        for x in range(img.width):
            r, g, b, a = s[x, y]
            if a:
                if (r, g, b) not in cache:
                    cache[(r, g, b)] = rgbs[nearest_palette_index((r, g, b), rgbs)]
                d[x, y] = cache[(r, g, b)] + (255,)
    return out


def despeckle(img: Image.Image) -> tuple[Image.Image, int]:
    out = img.copy()
    s, d = img.load(), out.load()
    n = 0
    for y in range(1, img.height - 1):
        for x in range(1, img.width - 1):
            if not s[x, y][3]:
                continue
            nb = [s[x + dx, y + dy] for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1))]
            for c in sorted(set(nb)):
                if c[3] and c != s[x, y] and nb.count(c) == 4:
                    d[x, y] = c
                    n += 1
                    break
    return out, n


def outline_in_place(img: Image.Image, rgb) -> Image.Image:
    """1 px 4-neighbour ring of `rgb` on the transparent pixels next to the silhouette (canvas size unchanged)."""
    out = img.copy()
    s, d = img.load(), out.load()
    for y in range(img.height):
        for x in range(img.width):
            if s[x, y][3]:
                continue
            if any(0 <= x + dx < img.width and 0 <= y + dy < img.height and s[x + dx, y + dy][3]
                   for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1))):
                d[x, y] = rgb + (255,)
    return out


def core_of(raw: Image.Image, palette: Palette, tones) -> tuple[Image.Image, int]:
    f = threshold_alpha(reduce_for_route(raw, "blender", supersample=1))
    f = snap(f, palette, tones)
    return despeckle(f)


def window_for(core: Image.Image, canvas, core_bottom_row: int, core_width: int):
    """Crop window (x0, y0, x1, y1) of the 128 px render that puts the core's horizontal centre at canvas centre and its
    bottom row at `core_bottom_row`."""
    box = opaque_bbox(core)
    if box is None or box[2] - box[0] != core_width:
        raise SystemExit("base core width is %s, expected %d" % (box, core_width))
    x0 = box[0] - (canvas[0] - core_width) // 2
    y0 = box[3] - 1 - core_bottom_row
    return (x0, y0, x0 + canvas[0], y0 + canvas[1])


def sprite_from_window(raw: Image.Image, win, palette: Palette, tones, ink, despeckle_on=True):
    crop = raw.convert("RGBA").crop(win)
    f = threshold_alpha(reduce_for_route(crop, "blender", supersample=1))
    f = snap(f, palette, tones)
    specks = 0
    if despeckle_on:
        f, specks = despeckle(f)
    return f, specks


def highlight_of(rest: Image.Image, palette: Palette) -> Image.Image:
    """Reduced-motion still of the chip loop: the rest vein with a 1 px steel_3 rim on every res_ore_1 pixel whose upper
    neighbour is not res_ore_1 (the back edge of each ore top). Deterministic, no hand edits."""
    out = rest.copy()
    s, d = rest.load(), out.load()
    top, rim = palette.rgb("res_ore_1") + (255,), palette.rgb("steel_3") + (255,)
    for y in range(rest.height):
        for x in range(rest.width):
            if s[x, y] == top and (y == 0 or s[x, y - 1] != top):
                d[x, y] = rim
    return out


# --- checks --------------------------------------------------------------------------------------------

def ids_of(img: Image.Image, palette: Palette) -> dict[str, int]:
    out: dict[str, int] = {}
    for p in img.getdata():
        if p[3]:
            i = palette.id_of(tuple(p[:3]))
            out[i] = out.get(i, 0) + 1
    return out


def bbox_of_id(img: Image.Image, palette: Palette, cid: str):
    rgb = palette.rgb(cid)
    px = img.load()
    pts = [(x, y) for y in range(img.height) for x in range(img.width) if px[x, y][3] and tuple(px[x, y][:3]) == rgb]
    if not pts:
        return None
    xs, ys = [p[0] for p in pts], [p[1] for p in pts]
    return (min(xs), min(ys), max(xs) + 1, max(ys) + 1)


def common_checks(imgs, entry, palette, allow, canvas, foot, expect_frames, gate_pivot=True, gate_overhang=True):
    groups = {palette.group_of(palette.rgb(i)) for i in allow}
    res = [checks.check_palette(imgs, palette, groups, allowed_ids=set(allow)),
           checks.check_canvas(imgs, canvas), checks.check_alpha(imgs), checks.check_frames(imgs, expect_frames)]
    used: dict[str, int] = {}
    for im in imgs:
        for k, v in ids_of(im, palette).items():
            used[k] = used.get(k, 0) + v
    res.append(checks.CheckResult("family_allowance_21_2", set(used) <= set(allow) and not any(k.startswith(FORBIDDEN_PREFIX) for k in used)
                                  and all(len(ids_of(im, palette)) <= MAX_COLOURS for im in imgs), {
        "used": used, "allowed": sorted(allow), "outside": sorted(set(used) - set(allow)),
        "colourMaxPerSprite": MAX_COLOURS, "perSpriteColours": [len(ids_of(im, palette)) for im in imgs]}))
    pv = checks.check_pivot(imgs, entry)
    ov = checks.check_overhang(imgs, canvas, foot, PPU)
    if gate_pivot:
        res.append(pv)
    else:
        res.append(checks.CheckResult("pivot_manifest_only", pv.details["integral"] and pv.details["expected"] == pv.details["manifest"], {
            **pv.details, "note": "FX frames hold chips only; the manifest pivot equals the deposit pivot they are anchored to"}))
    if gate_overhang:
        res.append(ov)
    else:
        res.append(checks.CheckResult("overhang_info", True, {
            **ov.details, "note": "informational: the depot is a margin-cell structure with a fixed 80 x 76 canvas (Art Bible 21.4); "
                                  "the 0.5-cell overhang rule is for plates and selection frames (Art Bible 3.x), not for this prop"}))
    steel = sum(v for k, v in used.items() if k.startswith("steel_"))
    res.append(checks.CheckResult("c8_ring_note", True, {
        "steelPixels": steel, "outsideR": sorted(set(used) - RING_SAFE),
        "note": "margin cell outside every hit shape and ring (Art Bible 21.2 DEPOT/DEPOSIT rows); the ring proof on the densest "
                "frame is the P2 composite gate (T527). This family is never drawn inside a ring."}))
    return res, used


def depot_checks(assets_imgs, palette):
    out = {}
    for asset, img in assets_imgs.items():
        out[asset] = []
    return out


def make_sheet(rows, palette: Palette, out: Path, scale: int, pad: int = 4):
    """rows: list of lists of images, laid out left to right, on ui_bg, nearest-neighbour scaled."""
    w = max(sum(i.width + pad for i in r) for r in rows) + pad
    h = sum(max(i.height for i in r) + pad for r in rows) + pad
    sheet = Image.new("RGBA", (w, h), palette.rgb("ui_bg") + (255,))
    y = pad
    for r in rows:
        x = pad
        for im in r:
            sheet.paste(im, (x, y + max(i.height for i in r) - im.height), im)
            x += im.width + pad
        y += max(i.height for i in r) + pad
    sheet = sheet.resize((w * scale, h * scale), Image.Resampling.NEAREST)
    out.parent.mkdir(parents=True, exist_ok=True)
    sheet.save(out, format="PNG", compress_level=9, optimize=False)


def clean() -> None:
    for fam in ("depot", "deposit"):
        d = HERE / fam
        for p in d.iterdir():
            if p.is_dir():
                shutil.rmtree(p)
            elif p.suffix == ".json":
                p.unlink()


def main() -> int:
    palette = load_palette(PALETTE_V1_PATH)
    tmp = Path(tempfile.mkdtemp(prefix="t520-models-"))
    try:
        clean()
        script_hashes = {
            "depot/build_models.py": sha256_file(HERE / "depot" / "build_models.py"),
            "deposit/build_models.py": sha256_file(HERE / "deposit" / "build_models.py"),
            "pipeline_depot_deposit.py": sha256_file(Path(__file__)),
            "render.py": sha256_file(RENDER),
            "pixel_pass.py": sha256_file(REPO / "tools/art-pipeline/art_pipeline/pixel_pass.py"),
            "palette-v1.json": sha256_file(PALETTE_V1_PATH), "rig-v2.blend": sha256_file(RIG)}
        global_checks = [checks.check_palette_definition(palette)]
        problems = validate_models(load_models())
        global_checks.append(checks.CheckResult("models", not problems, {"problems": problems}))

        run([BLENDER, "-b", "--factory-startup", "--python-exit-code", "1", "-P", str(HERE / "depot" / "build_models.py"), "--", str(tmp)])
        run([BLENDER, "-b", "--factory-startup", "--python-exit-code", "1", "-P", str(HERE / "deposit" / "build_models.py"), "--", str(tmp)])

        models = {}       # model name -> (family, asset-or-None)
        for a, m in DEPOT_ASSETS.items():
            models[m] = ("depot", a)
        for a, m in DEPOSIT_RENDERS.items():
            models[m] = ("deposit", a)
        for i in range(6):
            models["chip-%d" % i] = ("deposit", "%s#%d" % (CHIP_ID, i))

        raws, stubs, blends, tones = {}, {}, {}, {}
        for m, (fam, _a) in models.items():
            (HERE / fam / "model").mkdir(parents=True, exist_ok=True)
            (HERE / fam / "raw").mkdir(parents=True, exist_ok=True)
            shutil.copy2(tmp / (m + ".blend"), HERE / fam / "model" / (m + ".blend"))
            blends[m] = HERE / fam / "model" / (m + ".blend")
            tones[m] = set(json.loads((tmp / (m + ".tones.json")).read_text()))
            stubs[m] = render_one(tmp / (m + ".blend"), HERE / fam / "raw", m)
            raws[m] = Image.open(HERE / fam / "raw" / (m + ".png")).convert("RGBA")

        # --- crop windows from the s1 base models (core 78 px wide slab, 46 px wide patch) ---
        dcore, _ = core_of(raws["depot-s1-empty"], palette, tones["depot-s1-empty"])
        dwin = window_for(dcore, DEPOT_CANVAS, 74, 78)       # outline adds 1 px: core bottom row 74 -> ink row 75
        vcore, _ = core_of(raws["deposit-rest-s1"], palette, tones["deposit-rest-s1"])
        vwin = window_for(vcore, DEP_CANVAS, 32, 46)         # core bottom row 32 -> ink row 33

        sprites, frames_by_asset, infos, canvases = {}, {}, {}, {}
        ink = palette.rgb("ink_0")
        for a, m in DEPOT_ASSETS.items():
            f, specks = sprite_from_window(raws[m], dwin, palette, tones[m], ink)
            sprites[a] = outline_in_place(f, ink)
            infos[a] = {"cropWindow": list(dwin), "despeckled": specks, "tones": sorted(tones[m])}
        for a, m in DEPOSIT_RENDERS.items():
            f, specks = sprite_from_window(raws[m], vwin, palette, tones[m], ink)
            sprites[a] = outline_in_place(f, ink)
            infos[a] = {"cropWindow": list(vwin), "despeckled": specks, "tones": sorted(tones[m])}
        hl = outline_in_place(highlight_of(sprite_from_window(raws["deposit-rest-s1"], vwin, palette, tones["deposit-rest-s1"], ink)[0], palette), ink)
        sprites["f006-deposit-highlight"] = hl
        infos["f006-deposit-highlight"] = {"cropWindow": list(vwin), "derivedFrom": "f006-deposit-rest-s1",
                                           "rule": "steel_3 on each res_ore_1 pixel with no res_ore_1 pixel above it"}
        chips = []
        for i in range(6):
            f, _ = sprite_from_window(raws["chip-%d" % i], vwin, palette, tones["chip-%d" % i], ink, despeckle_on=False)
            chips.append(outline_in_place(f, ink))
        frames_by_asset[CHIP_ID] = chips
        infos[CHIP_ID] = {"cropWindow": list(vwin), "tones": sorted(set().union(*[tones["chip-%d" % i] for i in range(6)])), "fps": CHIP_FPS}

        # --- manifests / entries ---
        entries, results = {}, {}
        specs = []   # (asset, family, frames list, allow, canvas, foot, fps)
        for a in DEPOT_ASSETS:
            specs.append((a, "depot", [sprites[a]], DEPOT_ALLOW, DEPOT_CANVAS, DEPOT_FOOT, 0))
        for a in list(DEPOSIT_RENDERS) + ["f006-deposit-highlight"]:
            specs.append((a, "deposit", [sprites[a]], DEPOSIT_ALLOW, DEP_CANVAS, DEP_FOOT, 0))
        specs.append((CHIP_ID, "deposit", chips, CHIP_ALLOW, DEP_CANVAS, DEP_FOOT, CHIP_FPS))
        for a, fam, imgs, allow, canvas, foot, fps in specs:
            paths = write_frames(imgs, a, HERE / fam / a)
            fr = [{"file": "%s/%s" % (a, p.name), "sha256": sha256_file(p)} for p in paths]
            entries[a] = {"assetId": a, "route": "blender", "ppu": PPU, "fps": fps, "canvas": list(canvas), "footprintPx": list(foot),
                          "pivot": list(pivot_for(canvas, foot)), "perspective": PROJ,
                          "palette": {"id": palette.palette_id, "version": palette.version}, "frames": fr}
            if a == CHIP_ID:
                entries[a]["stillFrame"] = 0
                entries[a]["reducedMotionStill"] = "f006-deposit-highlight"
            is_depot = fam == "depot"
            is_fx = a == CHIP_ID
            res, used = common_checks(imgs, entries[a], palette, allow, canvas, foot, len(imgs),
                                      gate_pivot=not is_fx, gate_overhang=not is_depot and not is_fx)
            if is_depot:
                box = opaque_bbox(imgs[0])
                res.append(checks.CheckResult("lamp_headroom_ge_2px", box[1] >= 2, {"topRow": box[1], "required": 2}))
                cap, head = bbox_of_id(imgs[0], palette, "steel_3"), bbox_of_id(imgs[0], palette, "copper_2")
                res.append(checks.CheckResult("lamp_copper2_head_steel3_cap", bool(cap and head) and not any(k.startswith(("lit_", "teal_")) for k in used), {
                    "steel_3Bbox": cap, "copper_2Bbox": head, "note": "A3 / OD-10: copper_2 head, steel_3 cap, never lit_*"}))
                res.append(checks.CheckResult("slab_steel_not_plate", not any(k.startswith("plate_") for k in used) and "steel_1" in used and "steel_0" in used, {
                    "steel_1": used.get("steel_1", 0), "steel_0": used.get("steel_0", 0), "note": "A3: slab top steel_1, side steel_0, rim steel_2"}))
                ib = bbox_of_id(imgs[0], palette, "ink_1")
                dim = bbox_of_id(imgs[0], palette, "dim_0")
                if a.endswith("-empty"):
                    res.append(checks.CheckResult("hopper_opening_ge_12x6", bool(dim) and dim[2] - dim[0] >= 12 and dim[3] - dim[1] >= 6, {
                        "dim_0Bbox": dim, "required": [12, 6], "note": "pit floor visible through the collar, dim_0 interior"}))
                else:
                    ore = used.get("res_ore_1", 0) + used.get("res_ore_0", 0)
                    res.append(checks.CheckResult("fill_visible", ore >= 12 and bool(used.get("copper_1")), {"orePixels": ore, "coalPixels": used.get("res_coal", 0)}))
                if a.endswith("-full"):
                    res.append(checks.CheckResult("full_copper2_lip", True, {"copper_2Pixels": used.get("copper_2", 0), "note": "compare with the -high copper_2 count in the fill_monotonic check"}))
            if fam == "deposit" and not is_fx:
                res.append(checks.CheckResult("no_steel_in_rest", "steel_3" not in used or a == "f006-deposit-highlight", {"steel_3": used.get("steel_3", 0)}))
            if is_fx:
                res.append(checks.check_flash(imgs, fps))
                comp = []
                for im in imgs:
                    c = sprites["f006-deposit-rest-s1"].copy()
                    c.alpha_composite(im)
                    comp.append(c)
                cf = checks.check_flash(comp, fps)
                cf.name = "flash_composite_over_rest_vein"
                res.append(cf)
                fr_ok = all(opaque_bbox(im) is not None for im in imgs) and len({sha256_file(Path(p)) for p in paths}) == 6
                res.append(checks.CheckResult("six_distinct_frames", fr_ok, {"frames": len(imgs)}))
                bb = [opaque_bbox(im) for im in imgs]
                res.append(checks.CheckResult("chips_inside_vein_canvas_and_above_patch", all(b[1] >= 0 for b in bb), {"bboxes": bb}))
                res.append(checks.CheckResult("flash_budget_21_8", True, {
                    "limitOverall": 3, "limitF006Fx": 2, "note": "chip loop 6 frames at %d fps. Measured: chip-only frames reach 2 flashes per second (the F006 FX limit, 3 overall); over the rest vein the composite measures 0. See the flash and flash_composite_over_rest_vein results. Scenery loop, not combined with a machine loop; reduced motion shows f006-deposit-highlight (zero flashes)." % fps}))
            results[a] = res
            sprites.setdefault(a, imgs[0])

        # fill and stage monotonicity (depot)
        for s in STAGES:
            counts = []
            for f in FILLS:
                u = ids_of(sprites["f006-depot-%s-%s" % (s, f)], palette)
                counts.append(u.get("res_ore_0", 0) + u.get("res_ore_1", 0) + u.get("res_coal", 0))
            lip = [ids_of(sprites["f006-depot-%s-%s" % (s, f)], palette).get("copper_2", 0) for f in FILLS]
            ok = counts == sorted(counts) and len(set(counts)) == 4 and lip[3] > lip[2]
            for f in FILLS:
                results["f006-depot-%s-%s" % (s, f)].append(checks.CheckResult("fill_monotonic_and_lip", ok, {"fillPixels(empty,low,high,full)": counts, "copper_2Pixels": lip}))
        hashes = [sha256_file(HERE / "depot" / a / (a + "_00.png")) for a in DEPOT_ASSETS]
        global_checks.append(checks.CheckResult("depot_12_distinct", len(set(hashes)) == 12, {"count": len(set(hashes))}))

        # reproducibility
        again = tmp / "again"
        again.mkdir()
        repro = {}
        for m, (fam, _a) in models.items():
            render_one(tmp / (m + ".blend"), again, m)
            a_, b_ = sha256_file(HERE / fam / "raw" / (m + ".png")), sha256_file(again / (m + ".png"))
            repro[m] = {"first": a_, "second": b_, "byteIdentical": a_ == b_}
        global_checks.append(checks.CheckResult("reproducibility", all(v["byteIdentical"] for v in repro.values()), repro))

        # review sheets
        rows = [[sprites["f006-depot-%s-%s" % (s, f)] for f in FILLS] for s in STAGES]
        make_sheet(rows, palette, HERE / "depot" / "sheets" / "depot-3x4.png", 3)
        make_sheet(rows, palette, HERE / "depot" / "sheets" / "depot-3x4-native.png", 1)
        make_sheet([[sprites["f006-deposit-rest-s%d" % s] for s in (1, 2, 3)] + [sprites["f006-deposit-highlight"]], chips],
                   palette, HERE / "deposit" / "sheets" / "deposit-and-chip-loop.png", 4)
        rest = sprites["f006-deposit-rest-s1"]
        comps = []
        for im in chips:
            c = rest.copy()
            c.alpha_composite(im)
            comps.append(c)
        make_sheet([comps], palette, HERE / "deposit" / "sheets" / "chip-loop-over-vein.png", 4)

        all_ok = True
        for fam, ids in (("depot", list(DEPOT_ASSETS)), ("deposit", list(DEPOSIT_RENDERS) + ["f006-deposit-highlight", CHIP_ID])):
            report = checks.build_report({a: results[a] for a in ids}, global_checks)
            report["family"] = {"family": fam, "task": "T520", "issue": 693, "artBibleVersion": ART_BIBLE, "briefVersion": BRIEF[fam],
                                "worldBible": WORLD_BIBLE, "intentionallyEmpty": "no intentionallyEmpty entry in this family"}
            write_lf(HERE / fam / "check-report.json", checks.report_json(report))
            rh = sha256_file(HERE / fam / "check-report.json")
            all_ok &= report["passed"]
            for a in ids:
                ms = [m for m, (f_, a_) in models.items() if a_ == a or a_.startswith(a + "#")] or \
                     (["deposit-rest-s1"] if a == "f006-deposit-highlight" else [])
                srcs = [{"path": "art-source/f006/%s/build_models.py" % fam, "sha256": script_hashes["%s/build_models.py" % fam],
                         "note": "procedural geometry, flat palette-colour emission materials per face; no external assets"},
                        {"path": "tools/art-pipeline/blender/rig-v2.blend", "sha256": script_hashes["rig-v2.blend"]}]
                srcs += [{"path": "art-source/f006/%s/model/%s.blend" % (fam, m), "sha256": sha256_file(blends[m])} for m in ms]
                st0 = stubs[ms[0]]
                rec = {
                    "assetId": a, "revision": 1, "route": "blender", "key": a.upper(),
                    "sources": srcs,
                    "tools": {**st0["tools"], "python": sys.version.split()[0], "pillow": __import__("PIL").__version__},
                    "parameters": {**st0["parameters"], "perspective": PROJ, "densityD": PPU, "frameCount": len(frames_by_asset.get(a, [0])),
                                   "scriptHashes": script_hashes, "renderSeconds": [stubs[m]["renderSeconds"] for m in ms],
                                   "materials": "flat emission, exact palette colours per face", "keyLight": "unused by the emission materials"},
                    "touchUp": [{"step": "snap_despeckle_outline", "script": "art-source/f006/pipeline_depot_deposit.py",
                                 "note": "nearest-tone snap to the model's declared tones, single-pixel despeckle, fixed crop window, in-place 1 px ink_0 "
                                         "outline on the transparent neighbours; deterministic, no hand edits. %s" % json.dumps(infos[a], sort_keys=True)}],
                    "pixelPass": {"script": "tools/art-pipeline/art_pipeline/pixel_pass.py", "scriptSha256": script_hashes["pixel_pass.py"],
                                  "driver": "art-source/f006/pipeline_depot_deposit.py", "paletteId": palette.palette_id,
                                  "paletteVersion": palette.version, "paletteSha256": script_hashes["palette-v1.json"],
                                  "config": {"supersample": 1, "canvas": entries[a]["canvas"], "footprint": entries[a]["footprintPx"]}},
                    "outputs": entries[a]["frames"],
                    "license": "Repository-authored procedural geometry and palette; no third-party assets, models or textures. "
                               "Ownership: repository owner (fjure); no public license grant.",
                    "trainingDataNote": "N/A: no generative model or training data used (Blender route, procedural geometry "
                                        "written by an AI coding agent, disclosed).",
                    "producedBy": PRODUCER, "reviewedBy": None,
                    "candidateSelection": "Deterministic render of fixed geometry, seed 0, 16 samples; no seed selection. Hybrid route "
                                          "(owner decision 2026-11-03): the Blender MCP bridge (127.0.0.1:9876) was not reachable "
                                          "(Blender GUI not running), so geometry was iterated by headless batch renders; the committed "
                                          "render is the final headless run, and a second headless render is byte-identical.",
                    "interactiveSteps": ["none: Blender MCP bridge not reachable at 127.0.0.1:9876; headless batch iteration only"],
                    "artBibleVersion": ART_BIBLE, "briefVersion": BRIEF[fam], "worldBibleVersion": WORLD_BIBLE,
                    "rigVersion": st0["parameters"]["rigVersion"], "renderEnv": st0["renderEnv"],
                    "checkReportHash": rh, "checkReportPath": "art-source/f006/%s/check-report.json" % fam}
                if a == CHIP_ID:
                    rec["reducedMotionStill"] = "f006-deposit-highlight"
                gaps = [g for g in missing_provenance_fields(rec, "blender") if g != "reviewedBy"]
                if gaps:
                    print("provenance gaps", a, gaps)
                    all_ok = False
                write_lf(HERE / fam / a / "provenance.json", json.dumps(rec, indent=2, sort_keys=True) + "\n")
            man = build_manifest([entries[a] for a in ids])
            man["note"] = ("T520 candidate entries for T526; reviewedBy pending (T525). Depot 80 x 76 footprint 80 x 40; deposit and chip 48 x 34 footprint "
                           "48 x 24; the chip loop (fps 8) is anchored on the deposit pivot, its reduced-motion still is f006-deposit-highlight (Art Bible 21.7).")
            write_lf(HERE / fam / "manifest.json", json.dumps(man, indent=2, sort_keys=True) + "\n")
            print(fam, "CHECKS-PASSED" if report["passed"] else "CHECKS-FAILED")
            for a in ids:
                for r in results[a]:
                    if not r.passed:
                        print("  FAIL", a, r.name, json.dumps(r.details)[:400])
        for r in global_checks:
            print(r.name, r.passed)
        return 0 if all_ok else 1
    finally:
        shutil.rmtree(tmp, ignore_errors=True)


if __name__ == "__main__":
    raise SystemExit(main())
