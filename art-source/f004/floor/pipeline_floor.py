"""F004 T435, floor family: model -> Blender render -> tile cut and pixel pass -> tiling checks (`F004-FLOOR-TILE`).

This module is the floor stage of the T435 run. The single driver is `art-source/f004/slots/pipeline_slots.py`
(it builds the floor first, because the plates are composed over it, then writes both family reports, including
the cross-family checks that need both). Run from the repository root:

  uv run --frozen --offline --project tools/art-pipeline python art-source/f004/slots/pipeline_slots.py

Tile geometry (Art Bible 3.2a, 3.5, 6.1; brief section 6). The tile is a 2:1 diamond of 60 x 30 art px: the
lattice period proposed for T456 (slot spacing 60 px). Row y (0..29) holds `4 * min(y, 29 - y) + 2` pixels centred
on the tile centre (30, 15), so the edges run at exactly 2 px per row, the tile touches the 60 x 30 canvas on
all four sides, and tiles at the lattice vectors (30, 15) and (30, -15) cover the plane exactly once: at every row
the two tiles that meet have widths that sum to the 60 px period. Width and height are even and the pivot is the
tile centre (30, 15), a whole art pixel (rule P1). The edges carry no outline and no tone step (brief 6: no seam):
the outermost ring of the tile is `floor_0`.
"""
from __future__ import annotations

import hashlib
import json
import shutil
import subprocess
import sys
from pathlib import Path

from PIL import Image

HERE = Path(__file__).resolve().parent
REPO = HERE.parents[2]
sys.path.insert(0, str(REPO / "tools" / "art-pipeline"))

from art_pipeline import checks  # noqa: E402
from art_pipeline.palette import Palette  # noqa: E402
from art_pipeline.pixel_pass import (  # noqa: E402
    place_on_canvas, quantize, reduce_for_route, threshold_alpha)

TILE = (60, 30)
CENTRE_CROP = (34, 49, 94, 79)          # the 60 x 30 window of the 128 x 128 render centred on the world origin
ASSET_ID = "f004-floor-tile"
KEY = "F004-FLOOR-TILE"
FIELD, DETAIL = "floor_0", "floor_1"
LATTICE = ((30, 15), (30, -15))


def diamond_mask(w: int = 60, h: int = 30) -> Image.Image:
    """Exact 2:1 diamond: row y holds `4 * min(y, h - 1 - y) + 2` pixels centred on the tile centre."""
    m = Image.new("L", (w, h), 0)
    px = m.load()
    for y in range(h):
        n = 4 * min(y, h - 1 - y) + 2
        x0 = (w - n) // 2
        for x in range(x0, x0 + n):
            px[x, y] = 255
    return m


def sha256_file(p: Path) -> str:
    return hashlib.sha256(p.read_bytes()).hexdigest()


# --- tile cut and pixel pass --------------------------------------------------------------------------

def cut_tile(raw: Image.Image, palette: Palette) -> Image.Image:
    """Crop the render to the 60 x 30 window, reduce (blender route, factor 1), quantise to the floor tones and keep
    exactly the diamond. The render is a ground patch much larger than the tile, so the crop is opaque everywhere."""
    crop = raw.convert("RGBA").crop(CENTRE_CROP)
    if min(p[3] for p in crop.getdata()) != 255:
        raise SystemExit("floor render does not cover the tile window")
    f = reduce_for_route(crop, "blender", supersample=1)
    f = threshold_alpha(f)
    f = quantize(f, palette, {"floor"})
    mask = diamond_mask(*TILE)
    out = Image.new("RGBA", TILE, (0, 0, 0, 0))
    out.paste(f, (0, 0), mask)
    return out


# --- tiling checks ------------------------------------------------------------------------------------

def _opaque(img: Image.Image) -> set[tuple[int, int]]:
    px = img.convert("RGBA").load()
    return {(x, y) for y in range(img.height) for x in range(img.width) if px[x, y][3]}


def tiling_checks(tile: Image.Image, palette: Palette) -> list[checks.CheckResult]:
    w, h = tile.size
    res: list[checks.CheckResult] = []
    px = tile.load()
    ids = {(x, y): palette.id_of(tuple(px[x, y][:3])) for (x, y) in _opaque(tile)}

    # 1. exact diamond (2:1 edges, even width and height, rows 2, 6, ..., 58, 58, ..., 2)
    dm = diamond_mask(w, h)
    want = {(x, y) for y in range(h) for x in range(w) if dm.getpixel((x, y))}
    got = set(ids)
    rows = [sum(1 for (x, yy) in got if yy == y) for y in range(h)]
    res.append(checks.CheckResult("tile_geometry", got == want and w % 2 == 0 and h % 2 == 0 and w == 2 * h, {
        "size": [w, h], "ratio": "2:1", "rowWidths": rows, "matchesExactDiamond": got == want}))

    # 2. lattice sheet: every pixel of a window around the centre is covered exactly once, no tone step on the seams
    reps = range(-4, 5)
    ox, oy = 150, 75                      # sheet coordinates of the tile (0, 0) centre
    cov: dict[tuple[int, int], list] = {}
    for a in reps:
        for b in reps:
            cx = ox + a * LATTICE[0][0] + b * LATTICE[1][0]
            cy = oy + a * LATTICE[0][1] + b * LATTICE[1][1]
            for (x, y), pid in ids.items():
                cov.setdefault((cx - w // 2 + x, cy - h // 2 + y), []).append(((a, b), pid))
    win = [(x, y) for y in range(oy - 30, oy + 30) for x in range(ox - 60, ox + 60)]
    winset = set(win)
    gaps = sum(1 for p in win if p not in cov)
    overlaps = sum(1 for p in win if p in cov and len(cov[p]) > 1)
    steps = pairs = 0
    for (x, y) in win:
        if (x, y) not in cov:
            continue
        (t0, id0), = cov[(x, y)][:1]
        for dx, dy in ((1, 0), (0, 1)):
            q = (x + dx, y + dy)
            if q in cov and q in winset:
                (t1, id1), = cov[q][:1]
                if t1 != t0:
                    pairs += 1
                    steps += id0 != id1
    res.append(checks.CheckResult("tiling_lattice", not gaps and not overlaps and not steps and pairs > 0, {
        "lattice": [list(v) for v in LATTICE], "period": [60, 30], "windowPx": len(win), "gaps": gaps,
        "overlaps": overlaps, "seamPairs": pairs, "toneSteps": steps}))

    # 3. rectangular unit (60 x 30 period, wrap-around): two tiles per unit cover it exactly once; edge columns compared
    unit: dict[tuple[int, int], int] = {}
    for (cx, cy) in ((30, 15), (60, 30)):
        for (x, y) in ids:
            p = ((cx - w // 2 + x) % 60, (cy - h // 2 + y) % 30)
            unit[p] = unit.get(p, 0) + 1
    unit_ok = len(unit) == 60 * 30 and all(v == 1 for v in unit.values())
    left = [ids.get((x, y)) for y in range(h) for x in (0,)]
    right = [ids.get((x, y)) for y in range(h) for x in (w - 1,)]
    res.append(checks.CheckResult("tiling_wrap_unit", unit_ok, {
        "unitPx": [60, 30], "tilesPerUnit": 2, "coveredOnce": unit_ok, "cells": len(unit),
        "edgeColumnTones": sorted({t for t in left + right if t})}))

    # 4. edge ring: the outermost opaque ring is the field tone (no highlight, no detail, no outline)
    ring = [p for p in got if any((p[0] + dx, p[1] + dy) not in got for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1)))]
    ring_tones = sorted({ids[p] for p in ring})
    res.append(checks.CheckResult("edge_ring_field_tone", ring_tones == [FIELD], {"ringPixels": len(ring), "tones": ring_tones}))

    # 5. detail: clusters of the detail tone (8-connected), count, distance to the edge, distinct shapes
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
    outside = [(x, y) for y in range(-3, h + 3) for x in range(-3, w + 3) if (x, y) not in got]
    near = [min(max(abs(x - qx), abs(y - qy)) for (x, y) in comp for (qx, qy) in outside) for comp in clusters]
    shapes = [tuple(sorted((x - min(c[0] for c in comp), y - min(c[1] for c in comp)) for (x, y) in comp)) for comp in clusters]
    res.append(checks.CheckResult("detail_clusters", len(clusters) <= 2 and all(d >= 3 for d in near)
                                  and len(set(shapes)) == len(shapes), {
        "clusters": len(clusters), "max": 2, "pixelsPerCluster": [len(c) for c in clusters],
        "chebyshevDistanceToEdge": near, "minRequired": 3, "distinctShapes": len(set(shapes)) == len(shapes),
        "detailPixels": len(det), "outsidePixelsSampled": len(outside)}))

    # 6. tones: floor_0 and floor_1 only; floor_2, plate_* and lit tones never
    used = sorted({t for t in ids.values()})
    res.append(checks.CheckResult("floor_tones", set(used) <= {FIELD, DETAIL}, {
        "used": used, "floor_2": "unused (forbidden behind badges, rule C1)", "plate_tones": 0, "lit_tones": 0}))
    return res


def make_lattice_sheet(tile: Image.Image, palette: Palette, out: Path, scale: int = 3) -> Path:
    """3 x 3 diamonds on the lattice, native pixels scaled by an integer, on `ui_bg` (the camera clear colour)."""
    w, h = tile.size
    W, H = 3 * w, 3 * h
    sheet = Image.new("RGBA", (W, H), palette.rgb("ui_bg") + (255,))
    for a in (-1, 0, 1):
        for b in (-1, 0, 1):
            cx = W // 2 + a * LATTICE[0][0] + b * LATTICE[1][0]
            cy = H // 2 + a * LATTICE[0][1] + b * LATTICE[1][1]
            sheet.paste(tile, (cx - w // 2, cy - h // 2), tile)
    sheet = sheet.resize((W * scale, H * scale), Image.Resampling.NEAREST)
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


if __name__ == "__main__":
    raise SystemExit("run art-source/f004/slots/pipeline_slots.py (it builds the floor first and writes both reports)")
