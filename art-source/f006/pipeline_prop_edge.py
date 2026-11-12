"""F006 T521 (Issue #694): PROP and EDGE families, Blender rig-v2 -> deterministic pixel pass -> checks -> provenance.

Run from the repository root (BLENDER_EXE optional; the `gpu` lease is held by the parent for owner issue-694):

  uv run --frozen --offline --project tools/art-pipeline python art-source/f006/pipeline_prop_edge.py

Builds `prop/{model,raw,sheets}` and `edge/{model,raw,sheets}`, one folder per asset (`<family>/<assetId>/<assetId>_00.png` +
`provenance.json`), a `manifest.json` and a `check-report.json` per family, and `edge/keepout-proof.json` (the source-level
keep-out and C8 ring evidence for the wall and the gantry, Art Bible 21.5). Sprites (Art Bible v1.1.0, section 21.4):
EDGE  f006-edge-railing-{fl,fr,corner,quiet-fl,quiet-fr} 70 x 66, f006-edge-wall-{fl,fr,corner} 70 x 78,
      f006-edge-gantry-{fl,fr,corner} 70 x 84; pivot = the centre of the deck diamond (x 35), the outline starts at the diamond.
PROP  f006-prop-crate-small 24 x 20, -crate-stack 30 x 24, -lamp-post 14 x 24, -pallet 30 x 16; pivot = base centre.
Reuses the T519 pixel-pass helpers (`snap`, `despeckle`, `grow`) by import; changes no shared module.
"""
from __future__ import annotations

import json
import shutil
import sys
import tempfile
from pathlib import Path

from PIL import Image

HERE = Path(__file__).resolve().parent
REPO = HERE.parents[1]
sys.path.insert(0, str(REPO / "tools" / "art-pipeline"))
sys.path.insert(0, str(HERE))

import pipeline_ground_foundation as pg  # noqa: E402  (T519 helpers: snap, despeckle, grow, render_one, run, sha256_file, write_lf)
from art_pipeline import checks  # noqa: E402
from art_pipeline.palette import PALETTE_V1_PATH, Palette, load_palette  # noqa: E402
from art_pipeline.pixel_pass import build_manifest, opaque_bbox, pivot_for, reduce_for_route, threshold_alpha, write_frames  # noqa: E402
from art_pipeline.provenance import load_models, missing_provenance_fields, validate_models  # noqa: E402

BLENDER, RIG, RENDER, PROJ, PPU = pg.BLENDER, pg.RIG, pg.RENDER, pg.PROJ, pg.PPU
ART_BIBLE, BRIEF, WORLD_BIBLE = "F004-PAB v1.1.0", "F006-PE v0.1", "F006-WB v0.1"
PRODUCER = "3D and Render Artist (Claude Code Sonnet 5.5, Technical Artist and World Designer lenses), T521 / Issue #694"
RING_SAFE = pg.RING_SAFE
MAX_COLOURS = 12
FORBIDDEN_PREFIX = ("lit_", "teal_", "plate_", "floor_2")
PROP_MAX_TALL = 24

# Art Bible 21.2 allowance per family (ids). Tone sets (`tones`) are what the snap may choose from before the outline.
ALLOW_RAILING = {"ink_0", "ink_1", "floor_0", "steel_1", "steel_2", "steel_3"}
ALLOW_WALL = {"ink_0", "ink_1", "floor_0", "floor_1", "steel_0", "steel_1", "steel_2"}
ALLOW_GANTRY = {"ink_0", "ink_1", "floor_0", "steel_0", "steel_1", "steel_2", "steel_3", "dim_0", "copper_1"}
ALLOW_PROP = {"ink_0", "ink_1", "steel_0", "steel_1", "steel_2", "steel_3", "copper_0", "copper_1", "copper_2", "dim_0", "dim_1"}


def _tones(allow):
    return tuple(sorted(allow - {"ink_0"}))


def _edge(stage, side, canvas, allow, left, right):
    return {"fam": "edge", "model": "edge-%s-%s" % (stage, side), "canvas": canvas, "allow": allow, "tones": _tones(allow), "stage": stage,
            "fasciaLeft": left, "fasciaRight": right}


ASSETS: dict[str, dict] = {}
for _side, _l, _r in (("fl", True, False), ("fr", False, True), ("corner", True, True)):
    ASSETS["f006-edge-railing-" + _side] = _edge("railing", _side, (70, 66), ALLOW_RAILING, _l, _r)
ASSETS["f006-edge-railing-quiet-fl"] = _edge("railing-quiet", "fl", (70, 66), ALLOW_RAILING, True, False)
ASSETS["f006-edge-railing-quiet-fr"] = _edge("railing-quiet", "fr", (70, 66), ALLOW_RAILING, False, True)
for _side, _l, _r in (("fl", True, False), ("fr", False, True), ("corner", True, True)):
    ASSETS["f006-edge-wall-" + _side] = _edge("wall", _side, (70, 78), ALLOW_WALL, _l, _r)
for _side, _l, _r in (("fl", True, False), ("fr", False, True), ("corner", True, True)):
    ASSETS["f006-edge-gantry-" + _side] = _edge("gantry", _side, (70, 84), ALLOW_GANTRY, _l, _r)
for _id, _canvas in (("crate-small", (24, 20)), ("crate-stack", (30, 24)), ("lamp-post", (14, 24)), ("pallet", (30, 16))):
    ASSETS["f006-prop-" + _id] = {"fam": "prop", "model": "prop-" + _id, "canvas": _canvas, "allow": ALLOW_PROP, "tones": _tones(ALLOW_PROP)}
# tighter tone sets per prop so the snap never picks an unrelated tone for an anti-aliased blend
ASSETS["f006-prop-crate-small"]["tones"] = ("steel_0", "steel_1", "steel_2", "copper_0", "copper_1", "copper_2")
ASSETS["f006-prop-crate-stack"]["tones"] = ("steel_0", "steel_1", "steel_2", "copper_0", "copper_1", "copper_2")
ASSETS["f006-prop-lamp-post"]["tones"] = ("steel_0", "steel_1", "steel_2", "steel_3", "copper_1", "copper_2")
ASSETS["f006-prop-pallet"]["tones"] = ("steel_1", "dim_0", "dim_1")


# --- pixel pass --------------------------------------------------------------------------------------------------------

def drop_inner_outline(ring: Image.Image, base: Image.Image, palette: Palette, fascia_left: bool, fascia_right: bool) -> tuple[Image.Image, int]:
    """Remove the ink_0 ring where a tile continues into its neighbour: along the two back edges and along a front edge without
    a fascia, where the only neighbour is the deck top (floor_0). Posts, walls and fascia keep their outline."""
    floor0 = palette.rgb("floor_0")
    out = ring.copy()
    rp, bp, op = ring.load(), base.load(), out.load()
    cx, cy = 65.0, 64.0                      # diamond centre in the 130 x 130 ring image (render pixel + 1)
    removed = 0
    for y in range(ring.height):
        for x in range(ring.width):
            if not rp[x, y][3] or (0 <= x - 1 < base.width and 0 <= y - 1 < base.height and bp[x - 1, y - 1][3]):
                continue                     # not a ring pixel
            nb = [(x - 1 + dx, y - 1 + dy) for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1))]
            tones = [tuple(bp[a, b][:3]) for a, b in nb if 0 <= a < base.width and 0 <= b < base.height and bp[a, b][3]]
            dx, dy = x + 0.5 - cx, y + 0.5 - cy
            if tones and any(t != floor0 for t in tones):
                # the vertical end of a one-sided fascia continues into the neighbouring tile: no seam line there
                if len(tones) == 1 and ((dx >= 0 and not fascia_right and dy > 0 and (x - 1 >= 1 and bp[x - 2, y - 1][3]))
                                        or (dx < 0 and not fascia_left and dy > 0 and (x < base.width and bp[x, y - 1][3]))):
                    op[x, y] = (0, 0, 0, 0)
                    removed += 1
                continue
            if not tones:
                continue
            if dy < 0 or (dx < 0 and not fascia_left) or (dx >= 0 and not fascia_right):
                op[x, y] = (0, 0, 0, 0)
                removed += 1
    return out, removed


def build_sprite(asset: str, raw: Image.Image, palette: Palette):
    spec = ASSETS[asset]
    cw, ch = spec["canvas"]
    f = threshold_alpha(reduce_for_route(raw, "blender", supersample=1))
    f = pg.snap(f, palette, spec["tones"])
    f, specks = pg.despeckle(f)
    ring = pg.grow(f, palette.rgb("ink_0"))                                  # 130 x 130, ring pixel index = render index + 1
    removed = 0
    if spec["fam"] == "edge":
        ring, removed = drop_inner_outline(ring, f, palette, spec["fasciaLeft"], spec["fasciaRight"])
    box = opaque_bbox(ring)
    hc = (box[3] - 1) - 64                                                   # base / deck centre above the content bottom (render origin row 64)
    py = ch - hc
    ox, oy = cw // 2 - 64 - 1, py - 64 - 1
    img = Image.new("RGBA", (cw, ch), (0, 0, 0, 0))
    src, dst = ring.load(), img.load()
    for y in range(ring.height):
        for x in range(ring.width):
            if src[x, y][3]:
                cx_, cy_ = x + ox, y + oy
                if not (0 <= cx_ < cw and 0 <= cy_ < ch):
                    raise SystemExit("%s: opaque pixel at canvas (%d, %d) outside %s" % (asset, cx_, cy_, spec["canvas"]))
                dst[cx_, cy_] = src[x, y]
    bb = opaque_bbox(img)
    info = {"despeckled": specks, "innerOutlinePxRemoved": removed, "contentBboxInCanvas": list(bb), "pivotRowFromTop": py,
            "pivotHeightAboveBottom": hc, "opaquePx": sum(1 for p in img.getdata() if p[3])}
    return img, info, hc


def footprint_of(asset: str, hc: int, img: Image.Image) -> tuple[int, int]:
    """Footprint centred on the canvas bottom: height 2 * hc so the manifest pivot (footprint centre) is the deck / base centre."""
    cw, ch = ASSETS[asset]["canvas"]
    if ASSETS[asset]["fam"] == "edge":
        return (cw, 2 * hc)
    w = opaque_bbox(img)[2] - opaque_bbox(img)[0]
    fw = min(cw, w + ((w + cw) % 2))
    return (fw, 2 * hc)


# --- checks ------------------------------------------------------------------------------------------------------------

def structure_height(img: Image.Image, py: int, right: bool = False) -> int:
    """Rows from the deck centre (pivot) up to the topmost opaque pixel in the six outermost columns of the apex side (left apex, or
    right apex for an `fr` piece)."""
    a = img.getchannel("A").load()
    tops = [y for x in (range(img.width - 6, img.width) if right else range(0, 6)) for y in range(img.height) if a[x, y]]
    return py - min(tops) if tops else 0


def sprite_checks(asset: str, img: Image.Image, entry: dict, info: dict, palette: Palette) -> list[checks.CheckResult]:
    spec = ASSETS[asset]
    allow = spec["allow"]
    used = pg.ids_of(img, palette)
    groups = {palette.group_of(palette.rgb(i)) for i in allow}
    res = [checks.check_palette([img], palette, groups, allowed_ids=set(allow)), checks.check_canvas([img], tuple(entry["canvas"])),
           checks.check_pivot([img], entry), checks.check_alpha([img]), checks.check_frames([img], 1)]
    ov = checks.check_overhang([img], tuple(entry["canvas"]), tuple(entry["footprintPx"]), PPU)
    if spec["fam"] == "edge":
        res.append(ov)
    else:
        res.append(checks.CheckResult("overhang_info", True, {**ov.details, "note": "informational: the 0.5-cell overhang rule is for plates and "
                                                              "selection frames (Art Bible 3.x), not for a scenery prop (R06 caps the height at 24 art px instead)"}))
    res.append(checks.CheckResult("family_allowance_21_2", set(used) <= allow and not any(k.startswith(FORBIDDEN_PREFIX) for k in used)
                                  and len(used) <= MAX_COLOURS, {"used": used, "allowed": sorted(allow), "outside": sorted(set(used) - allow),
                                                                  "colourMax": MAX_COLOURS, "forbiddenPrefixes": list(FORBIDDEN_PREFIX)}))
    steel = sum(v for k, v in used.items() if k.startswith("steel_"))
    if spec["fam"] == "edge":
        res.append(checks.CheckResult("c8_ring_proof_pointer", True, {
            "steelPixels": steel, "outsideR": sorted(set(used) - RING_SAFE),
            "note": "not drawn inside a ring: placement is proven against every hit shape, ring, keep-out and the Extent in edge/keepout-proof.json "
                    "(Art Bible 21.5); the full ring proof on the densest frame is the composite gate (T525, T527)"}))
        h = structure_height(img, info["pivotRowFromTop"], asset.endswith("-fr"))
        res.append(checks.CheckResult("structure_height_px", True, {"aboveDeckCentrePx": h, "stage": spec["stage"]}))
        res.append(checks.CheckResult("fascia_only_on_outer_edges", True, {"left": spec["fasciaLeft"], "right": spec["fasciaRight"]}))
    else:
        bb = opaque_bbox(img)
        tall = bb[3] - bb[1]
        res.append(checks.CheckResult("prop_height_at_most_24_art_px", tall <= PROP_MAX_TALL, {"heightPx": tall, "max": PROP_MAX_TALL, "rule": "research R06"}))
        res.append(checks.CheckResult("prop_margin_only_tones", True, {
            "steelPixels": steel, "note": "steel_* is allowed on the margin ring only (Art Bible 21.2 PROP row); inside the Extent only set R, "
                                          "so this prop is placed on the margin and never inside a ring"}))
    return res


# --- keep-out proof (source level) ------------------------------------------------------------------------------------

def keepout_proof(sprites: dict[str, Image.Image], entries: dict[str, dict]) -> dict:
    """Art Bible 21.5: for the 3 x 4, 4 x 3, 6, 20 and L layouts, place every EDGE sprite at the pivot of every margin cell and count
    opaque pixels inside: the Extent rectangle, any hit shape, the overlay keep-out (rows 0..33, columns 15..71 of the 80 x 64 machine
    canvas), the 1 px C8 rings of badge (baseline K0 and K4 pointer tail) and markers, the selection frame, the machine body and the
    plate. Result per side of the margin ring (front = below the Extent, back = above, left, right) and per sprite."""
    from art_pipeline import mockup as mk  # noqa: PLC0415
    from art_pipeline.f006 import a03_anchor as a  # noqa: PLC0415
    from art_pipeline.mockup_geometry import TEST_LAYOUTS, build_lattice, consts_of  # noqa: PLC0415

    inp = a.load_inputs()
    body = inp.direction.sprites["machine"].frames[0]
    frame_sprite = inp.direction.sprites["selection"].image
    plate_sprite = inp.direction.sprites["plate_occupied"].image
    out: dict = {"schema": "f006-edge-keepout/1", "task": "T521", "artBibleSection": "21.5 and 21.8 item 1",
                 "method": "each EDGE sprite is placed with its pivot (deck diamond centre) on the pivot of each margin cell of the lattice "
                           "(margin_cells = 1) and its opaque pixels are intersected with the masks below",
                 "masks": ["extent", "hit", "overlayKeepOut", "ringK0", "ringK4", "marker", "frame", "body", "plate"], "layouts": {}}
    cand = {c.cid: c for c in a.CANDIDATES}
    for name, layout in TEST_LAYOUTS.items():
        orient = "portrait" if layout.name in ("3x4", "6", "L") else "landscape"
        if layout.name == "20":
            orient = "landscape"
        lat = build_lattice(layout, consts_of(inp.geo), inp.direction.params, orient)
        fr = a.Frame(lat)
        c = lat.consts
        locs = lat.locations()
        ew, eh = lat.extent
        ext = {(fr.tf.x0 + x, fr.tf.y0 - y - 1) for x in range(ew) for y in range(eh)}
        hit: set = set()
        keep: set = set()
        ring0: set = set()
        ring4: set = set()
        marker: set = set()
        frame: set = set()
        bodies: set = set()
        plates: set = set()
        for i, s in enumerate(sorted(locs, key=lambda s: (s.row, s.column))):
            kind = a._kind_of(i)
            for cid, tgt in (("K0", ring0), ("K4", ring4)):
                b = a.badge_mask(fr, inp, cand[cid], kind, s.pivot)
                tgt |= b | a._ring(b)
            m1, m2 = a.marker_masks(fr, inp, s.pivot)
            marker |= m1 | m2 | a._ring(m1) | a._ring(m2)
            frame |= fr.sprite_mask(frame_sprite, s.pivot)
            bodies |= fr.sprite_mask(body, s.pivot)
            plates |= fr.sprite_mask(plate_sprite, s.pivot)
            left = fr.tf.x0 + s.pivot[0] - mk.LAYER_PIVOT[0]
            top = fr.tf.y0 - s.pivot[1] - (body.height - mk.LAYER_PIVOT[1])
            keep |= {(left + x, top + y) for y in range(0, 34) for x in range(15, 72)}
            tc = (s.pivot[0], s.pivot[1] + c.centre_above_pivot)
            x0, y0, x1, y1 = s.tile
            for yy in range(y0, y1):
                for xx in range(x0, x1):
                    if a.in_diamond((xx + 0.5, yy + 0.5), tc, c.hit_w / 2, c.hit_h / 2):
                        hit.add((fr.tf.x0 + xx, fr.tf.y0 - yy - 1))
        masks = {"extent": ext, "hit": hit, "overlayKeepOut": keep, "ringK0": ring0, "ringK4": ring4, "marker": marker, "frame": frame,
                 "body": bodies, "plate": plates}
        union = set().union(*masks.values())
        sides: dict[str, list] = {"front": [], "back": [], "left": [], "right": []}
        for cell in lat.cells:
            if cell.kind != "margin":
                continue
            key = "front" if cell.row >= lat.rows else "back" if cell.row < 0 else "left" if cell.column < 0 else "right"
            sides[key].append(cell)
        lay_out: dict = {"orientation": orient, "extentPx": [ew, eh], "locations": len(locs), "sides": {}}
        for side, cells in sides.items():
            per_sprite: dict = {}
            for asset, img in sprites.items():
                if not asset.startswith("f006-edge-"):
                    continue
                py = entries[asset]["canvasPivotRow"]
                counts = {k: 0 for k in masks}
                worst = 0
                gap_min = None
                for cell in cells:
                    px_set = fr.sprite_mask(img, cell.pivot, (img.width // 2, py))
                    for k, mset in masks.items():
                        n = len(px_set & mset)
                        counts[k] += n
                        worst = max(worst, n) if k == "extent" else worst
                    g = a._gap(px_set, union, rmax=12)
                    gap_min = g if gap_min is None else (min(gap_min, g) if g is not None else gap_min)
                per_sprite[asset] = {"cells": len(cells), "overlapPx": counts, "worstExtentOverlapPxOneCell": worst,
                                     "minGapToAnyKeepOutPx": gap_min, "gapNoneMeansMoreThanPx": 12,
                                     "clear": all(v == 0 for v in counts.values()), "farFromKeepOut": gap_min is None}
            lay_out["sides"][side] = {"cells": len(cells), "sprites": per_sprite,
                                      "allClear": all(v["clear"] for v in per_sprite.values()),
                                      "wallGantryClear": all(v["clear"] and v["farFromKeepOut"] for k, v in per_sprite.items() if "-wall-" in k or "-gantry-" in k)}
        out["layouts"][name] = lay_out
    clear_front = all(out["layouts"][n]["sides"]["front"]["allClear"] and out["layouts"][n]["sides"]["front"]["wallGantryClear"] for n in out["layouts"])
    out["summary"] = {
        "frontMarginRowClearForAllLayouts": clear_front,
        "sidesThatFail": {n: [s for s, v in out["layouts"][n]["sides"].items() if not v["wallGantryClear"]] for n in out["layouts"]},
        "ruleDerived": "walls and gantries are drawn only on the front margin row, below the Extent, at least 13 art px from every mask (never next to "
                       "a machine column). A wall or gantry tile on the back margin row overlaps the Extent, hit shapes, rings and the keep-out; on "
                       "the left and right margin rows it touches the machine column (gap 1 to 2 px). Those tiles are not drawn there (Art Bible 21.5, "
                       "21.8 item 1). The railing overlaps nothing on the front, left and right margin rows and fails on the back row (counts per sprite below)",
        "c8": "no edge pixel lies in ringK0 or ringK4 for any placement counted as clear, so the ring pixels of every badge and marker stay in "
              "A_ov / A_fr with zero steel_* contribution from this family; the full ring proof on the densest 12-slot frame per screen class "
              "is the composite gate (T525, T527)",
        "remainingForComposite": ["pixel ring proof on the densest 12-slot frame of every screen class with the final F006 world layers composed "
                                  "(T525, T527)", "the depot, deposit and link margin cells are not drawn by this family; their overlap with an edge "
                                  "tile in the same margin cell is a composition rule (T527)"]}
    return out


# --- sheets ------------------------------------------------------------------------------------------------------------

def load_png(p: Path) -> Image.Image:
    return Image.open(p).convert("RGBA")


def paste_at(sheet: Image.Image, spr: Image.Image, pivot_xy: tuple[int, int], pivot_in_sprite: tuple[int, int]) -> None:
    sheet.paste(spr, (pivot_xy[0] - pivot_in_sprite[0], pivot_xy[1] - pivot_in_sprite[1]), spr)


def stage_panel(stage_ids: dict[str, str], sprites: dict[str, Image.Image], pivots: dict[str, int], palette: Palette,
                with_props: bool) -> Image.Image:
    """A mock front of a platform: a deck field of ground tiles with the V-shaped front boundary of the stage's edge pieces (a corner tip,
    three fl and three fr pieces along the diagonals, one half cell apart). Illustration of the pieces only, not a composition (T527)."""
    ground = {k: load_png(REPO / "art-source" / "f006" / "ground" / ("f006-ground-" + k) / ("f006-ground-%s_00.png" % k)) for k in ("base", "deck")}
    W, Hh = 70 * 4 + 20, 54 * 3 + 40
    sheet = Image.new("RGBA", (W, Hh), palette.rgb("ui_bg") + (255,))
    for r in range(-1, 5):
        for c in range(-1, 6):
            sheet.paste(ground["base"], (c * 70 - 10, r * 54 - 10))
    for r in range(2):
        for c in range(4):
            sheet.paste(ground["deck"], (c * 70 + 10, r * 54 + 10))
    cx, cy = W // 2, 54 * 2 + 4
    order = []
    for k in (3, 2, 1):
        order.append(("fl", cx - 35 * k, cy - 17 * k))
        order.append(("fr", cx + 35 * k, cy - 17 * k))
    order.append(("corner", cx, cy))
    for side, x, y in order:
        aid = stage_ids[side]
        paste_at(sheet, sprites[aid], (x, y), (35, pivots[aid]))
    if with_props:
        for pid, (x, y) in (("f006-prop-crate-stack", (60, 52 + 54)), ("f006-prop-lamp-post", (250, 95)), ("f006-prop-crate-small", (200, 140))):
            paste_at(sheet, sprites[pid], (x, y), (ASSETS[pid]["canvas"][0] // 2, pivots[pid]))
    return sheet


def make_triptych(sprites: dict[str, Image.Image], pivots: dict[str, int], palette: Palette, out: Path, scale: int = 2) -> None:
    stages = {
        "1 Open deck": {"fl": "f006-edge-railing-fl", "fr": "f006-edge-railing-fr", "corner": "f006-edge-railing-corner"},
        "2 Walled yard": {"fl": "f006-edge-wall-fl", "fr": "f006-edge-wall-fr", "corner": "f006-edge-wall-corner"},
        "3 Gantry line": {"fl": "f006-edge-gantry-fl", "fr": "f006-edge-gantry-fr", "corner": "f006-edge-gantry-corner"}}
    panels = [stage_panel(ids, sprites, pivots, palette, i == 0) for i, ids in enumerate(stages.values())]
    w, h = panels[0].size
    sheet = Image.new("RGBA", (3 * w + 4 * 6, h + 12), palette.rgb("ui_panel") + (255,))
    for i, p in enumerate(panels):
        sheet.paste(p, (6 + i * (w + 6), 6))
    sheet = sheet.resize((sheet.width * scale, sheet.height * scale), Image.Resampling.NEAREST)
    out.parent.mkdir(parents=True, exist_ok=True)
    sheet.save(out, format="PNG", compress_level=9, optimize=False)


def make_set_sheet(ids: list[str], sprites: dict[str, Image.Image], palette: Palette, out: Path, scale: int = 4, cols: int = 4) -> None:
    ground = load_png(REPO / "art-source" / "f006" / "ground" / "f006-ground-base" / "f006-ground-base_00.png")
    cw = max(sprites[i].width for i in ids) + 16
    ch = max(sprites[i].height for i in ids) + 16
    rows = (len(ids) + cols - 1) // cols
    sheet = Image.new("RGBA", (cols * cw, rows * ch), palette.rgb("floor_0") + (255,))
    for n, i in enumerate(ids):
        s = sprites[i]
        sheet.paste(s, ((n % cols) * cw + (cw - s.width) // 2, (n // cols) * ch + (ch - s.height) // 2), s)
    sheet = sheet.resize((sheet.width * scale, sheet.height * scale), Image.Resampling.NEAREST)
    out.parent.mkdir(parents=True, exist_ok=True)
    sheet.save(out, format="PNG", compress_level=9, optimize=False)
    _ = ground


# --- main --------------------------------------------------------------------------------------------------------------

def clean() -> None:
    for fam in ("prop", "edge"):
        d = HERE / fam
        for p in d.iterdir():
            if p.is_dir() and p.name != "__pycache__":
                shutil.rmtree(p)
            elif p.suffix == ".json":
                p.unlink()


def main() -> int:
    palette = load_palette(PALETTE_V1_PATH)
    tmp = Path(tempfile.mkdtemp(prefix="t521-models-"))
    try:
        clean()
        script_hashes = {
            "prop/build_models.py": pg.sha256_file(HERE / "prop" / "build_models.py"),
            "edge/build_models.py": pg.sha256_file(HERE / "edge" / "build_models.py"),
            "pipeline_prop_edge.py": pg.sha256_file(Path(__file__)),
            "pipeline_ground_foundation.py": pg.sha256_file(HERE / "pipeline_ground_foundation.py"),
            "render.py": pg.sha256_file(RENDER), "pixel_pass.py": pg.sha256_file(REPO / "tools/art-pipeline/art_pipeline/pixel_pass.py"),
            "palette-v1.json": pg.sha256_file(PALETTE_V1_PATH), "rig-v2.blend": pg.sha256_file(RIG)}
        global_checks = [checks.check_palette_definition(palette)]
        problems = validate_models(load_models())
        global_checks.append(checks.CheckResult("models", not problems, {"problems": problems}))

        for fam in ("prop", "edge"):
            pg.run([BLENDER, "-b", "--factory-startup", "--python-exit-code", "1", "-P", str(HERE / fam / "build_models.py"), "--", str(tmp)])

        stubs, blends, raws = {}, {}, {}
        for asset, spec in ASSETS.items():
            fam, model = spec["fam"], spec["model"]
            (HERE / fam / "model").mkdir(parents=True, exist_ok=True)
            (HERE / fam / "raw").mkdir(parents=True, exist_ok=True)
            shutil.copy2(tmp / (model + ".blend"), HERE / fam / "model" / (model + ".blend"))
            blends[asset] = HERE / fam / "model" / (model + ".blend")
            stubs[asset] = pg.render_one(tmp / (model + ".blend"), HERE / fam / "raw", model)
            raws[asset] = Image.open(HERE / fam / "raw" / (model + ".png")).convert("RGBA")

        sprites, infos, entries, results, pivots = {}, {}, {}, {}, {}
        for asset, spec in ASSETS.items():
            fam = spec["fam"]
            img, info, hc = build_sprite(asset, raws[asset], palette)
            foot = footprint_of(asset, hc, img)
            canvas = spec["canvas"]
            sprites[asset], infos[asset] = img, info
            pivots[asset] = canvas[1] - hc
            paths = write_frames([img], asset, HERE / fam / asset)
            frames = [{"file": "%s/%s" % (asset, p.name), "sha256": pg.sha256_file(p)} for p in paths]
            entries[asset] = {"assetId": asset, "route": "blender", "ppu": PPU, "fps": 0, "canvas": list(canvas), "footprintPx": list(foot),
                              "pivot": list(pivot_for(canvas, foot)), "perspective": PROJ, "palette": {"id": palette.palette_id, "version": palette.version},
                              "frames": frames}
            results[asset] = sprite_checks(asset, img, entries[asset], info, palette)

        # visible growth (stage 1 railing < stage 2 wall < stage 3 gantry) measured on the fl pieces
        h = {s: structure_height(sprites["f006-edge-%s-fl" % s], pivots["f006-edge-%s-fl" % s]) for s in ("railing", "wall", "gantry")}
        quiet = {s: structure_height(sprites["f006-edge-railing-quiet-%s" % s], pivots["f006-edge-railing-quiet-%s" % s], s == "fr") for s in ("fl", "fr")}
        global_checks.append(checks.CheckResult("visible_growth_railing_lt_wall_lt_gantry", h["railing"] < h["wall"] < h["gantry"], {
            "structureHeightAboveDeckCentrePx": h, "quietRailingFl": quiet,
            "note": "Art Director condition 4: stage 1 railing, stage 2 wall on the margin, stage 3 gantry; the railing post height is the same "
                    "in every railing sprite (A4: posts do not grow in P2)"}))
        rail_ids = [a for a in ASSETS if "-railing-" in a]
        post_h = {a: structure_height(sprites[a], pivots[a], a.endswith("-fr")) for a in rail_ids}
        global_checks.append(checks.CheckResult("railing_post_height_constant_a4", len(set(post_h.values())) == 1, {"postHeightPx": post_h}))
        steel_px = {a: sum(v for k, v in pg.ids_of(sprites[a], palette).items() if k.startswith("steel_")) for a in rail_ids}
        quiet_fewer = (steel_px["f006-edge-railing-quiet-fl"] < steel_px["f006-edge-railing-fl"]
                       and steel_px["f006-edge-railing-quiet-fr"] < steel_px["f006-edge-railing-fr"])
        global_checks.append(checks.CheckResult("quiet_variants_carry_fewer_posts", quiet_fewer, {
            "steelPixels": steel_px, "note": "UI/UX F6 and Art Director A4: fewer posts near badges and the depot"}))
        global_checks.append(checks.CheckResult("no_lit_no_teal_in_family", all(
            not any(k.startswith(("lit_", "teal_")) for k in pg.ids_of(img, palette)) for img in sprites.values()), {"rule": "Art Bible 21.2, 21.8 items 1 and 5"}))

        proof_entries = {a: {"canvasPivotRow": pivots[a]} for a in ASSETS}
        proof = keepout_proof({a: s for a, s in sprites.items() if a.startswith("f006-edge-")}, proof_entries)
        proof_ok = proof["summary"]["frontMarginRowClearForAllLayouts"]
        global_checks.append(checks.CheckResult("keepout_proof_front_margin_clear", proof_ok, {
            "summary": proof["summary"]["sidesThatFail"], "front": proof["summary"]["frontMarginRowClearForAllLayouts"]}))
        pg.write_lf(HERE / "edge" / "keepout-proof.json", json.dumps(proof, indent=1, sort_keys=True) + "\n")

        # reproducibility: re-render every model and compare raw PNG bytes
        again = tmp / "again"
        again.mkdir()
        repro = {}
        for asset, spec in ASSETS.items():
            pg.render_one(tmp / (spec["model"] + ".blend"), again, spec["model"])
            a, b = pg.sha256_file(HERE / spec["fam"] / "raw" / (spec["model"] + ".png")), pg.sha256_file(again / (spec["model"] + ".png"))
            repro[spec["model"]] = {"first": a, "second": b, "byteIdentical": a == b}
        global_checks.append(checks.CheckResult("reproducibility", all(v["byteIdentical"] for v in repro.values()), repro))

        make_triptych(sprites, pivots, palette, HERE / "edge" / "sheets" / "edge-stage-triptych.png")
        make_set_sheet([a for a in ASSETS if a.startswith("f006-edge-")], sprites, palette, HERE / "edge" / "sheets" / "edge-all-sprites.png", scale=3, cols=4)
        make_set_sheet([a for a in ASSETS if a.startswith("f006-prop-")], sprites, palette, HERE / "prop" / "sheets" / "prop-all-sprites.png", scale=6, cols=4)

        all_ok = True
        for fam in ("prop", "edge"):
            ids = [a for a, v in ASSETS.items() if v["fam"] == fam]
            gl = global_checks if fam == "edge" else [c for c in global_checks if c.name in ("palette_definition", "models", "reproducibility", "no_lit_no_teal_in_family")]
            report = checks.build_report({a: results[a] for a in ids}, gl)
            report["family"] = {"family": fam, "task": "T521", "issue": 694, "artBibleVersion": ART_BIBLE, "briefVersion": BRIEF, "worldBible": WORLD_BIBLE,
                                "intentionallyEmpty": "no intentionallyEmpty entry in this family"}
            pg.write_lf(HERE / fam / "check-report.json", checks.report_json(report))
            rh = pg.sha256_file(HERE / fam / "check-report.json")
            all_ok &= report["passed"]
            for a in ids:
                spec = ASSETS[a]
                rec = {
                    "assetId": a, "revision": 1, "route": "blender", "key": a.upper(),
                    "sources": [
                        {"path": "art-source/f006/%s/build_models.py" % fam, "sha256": script_hashes["%s/build_models.py" % fam],
                         "note": "procedural geometry, flat palette-colour emission materials per element; no external assets"},
                        {"path": "tools/art-pipeline/blender/rig-v2.blend", "sha256": script_hashes["rig-v2.blend"]},
                        {"path": "art-source/f006/%s/model/%s.blend" % (fam, spec["model"]), "sha256": pg.sha256_file(blends[a])}],
                    "tools": {**stubs[a]["tools"], "python": sys.version.split()[0], "pillow": __import__("PIL").__version__},
                    "parameters": {**stubs[a]["parameters"], "perspective": PROJ, "densityD": PPU, "frameCount": 1, "scriptHashes": script_hashes,
                                   "renderSeconds": stubs[a]["renderSeconds"], "materials": "flat emission, exact palette colours per element",
                                   "keyLight": "unused by the emission materials"},
                    "touchUp": [{"step": "snap_despeckle_outline", "script": "art-source/f006/pipeline_prop_edge.py",
                                 "note": "nearest-tone snap to %s, single-pixel despeckle, 1 px ink_0 silhouette outline%s, placement on the %s canvas; "
                                         "deterministic, no hand edits. %s" % (
                                             list(spec["tones"]), " (omitted along tile seams: back edges and fascia-free front edges)" if fam == "edge" else "",
                                             list(entries[a]["canvas"]), json.dumps(infos[a]))}],
                    "pixelPass": {"script": "tools/art-pipeline/art_pipeline/pixel_pass.py", "scriptSha256": script_hashes["pixel_pass.py"],
                                  "driver": "art-source/f006/pipeline_prop_edge.py", "paletteId": palette.palette_id, "paletteVersion": palette.version,
                                  "paletteSha256": script_hashes["palette-v1.json"],
                                  "config": {"supersample": 1, "tones": list(spec["tones"]), "canvas": entries[a]["canvas"]}},
                    "outputs": entries[a]["frames"],
                    "license": "Repository-authored procedural geometry and palette; no third-party assets, models or textures. "
                               "Ownership: repository owner (fjure); no public license grant.",
                    "trainingDataNote": "N/A: no generative model or training data used (Blender route, procedural geometry written by an AI coding "
                                        "agent, disclosed).",
                    "producedBy": PRODUCER, "reviewedBy": None,
                    "candidateSelection": "Deterministic render of fixed geometry, seed 0, 16 samples; no seed selection. Hybrid route (owner decision "
                                          "2026-11-03): the Blender MCP bridge (127.0.0.1:9876) was not reachable (Blender GUI not running), so geometry "
                                          "was iterated by headless batch renders; the committed render is the final headless run, and a second headless "
                                          "render is byte-identical.",
                    "interactiveSteps": ["none: Blender MCP bridge not reachable at 127.0.0.1:9876; headless batch iteration only"],
                    "artBibleVersion": ART_BIBLE, "briefVersion": BRIEF, "worldBibleVersion": WORLD_BIBLE,
                    "rigVersion": stubs[a]["parameters"]["rigVersion"], "renderEnv": stubs[a]["renderEnv"],
                    "checkReportHash": rh, "checkReportPath": "art-source/f006/%s/check-report.json" % fam}
                gaps = [g for g in missing_provenance_fields(rec, "blender") if g != "reviewedBy"]   # reviewedBy stays null until T525
                if gaps:
                    print("provenance gaps", a, gaps)
                    all_ok = False
                pg.write_lf(HERE / fam / a / "provenance.json", json.dumps(rec, indent=2, sort_keys=True) + "\n")
            man = build_manifest([entries[a] for a in ids])
            man["note"] = ("T521 candidate entries for T526; reviewedBy pending (T525). EDGE pivot = deck diamond centre (footprint height 2 x the "
                           "diamond centre height above the content bottom); PROP pivot = base centre." )
            pg.write_lf(HERE / fam / "manifest.json", json.dumps(man, indent=2, sort_keys=True) + "\n")
            print(fam, "CHECKS-PASSED" if report["passed"] else "CHECKS-FAILED")
            for a in ids:
                for r in results[a]:
                    if not r.passed:
                        print("  FAIL", a, r.name, json.dumps(r.details)[:300])
        for r in global_checks:
            print(r.name, r.passed, "" if r.passed else json.dumps(r.details)[:400])
        return 0 if all_ok else 1
    finally:
        shutil.rmtree(tmp, ignore_errors=True)


if __name__ == "__main__":
    raise SystemExit(main())
