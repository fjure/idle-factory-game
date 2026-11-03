"""F004 T436: the deterministic pre-pass and pixel pass shared by the resource and item families (route `comfy`).

Order (all steps are pure functions of the committed raw PNG; rerunning them is byte-identical):
  1. `key_background`   - touch-up (scripted): every magenta/pink pixel (r-g > 40 and b-g > 25: the generation background,
                          its shadow tint and enclosed holes) to alpha 0. No model, no randomness.
  2. `fit_crop`         - touch-up (scripted): pick the smallest integer grid factor k so that the subject fits the
                          declared art box, and crop a window of whole k x k blocks centred on the subject.
  3. pixel pass (tools/art-pipeline `pixel_pass`): `reduce_for_route(comfy, grid=k)` with a palette mapper restricted to
                          the declared tones (mode vote per block, CIELAB nearest, no dithering), alpha 0/255, quantise,
                          1 px `ink_0` outline, one canvas and pivot.
  4. `rim_lift`         - touch-up (scripted): rule C6 of the Art Bible (section 9.4): a resource or item pictogram on a UI
                          surface needs a rim tone of `res_ore_1`, `res_gold`, `steel_2` or `steel_3`. The inward neighbour
                          of an outline pixel that carries an interior tone (`res_coal`, `res_ore_0`, ...) takes the lift tone.
"""
from __future__ import annotations

import sys
from pathlib import Path

from PIL import Image

HERE = Path(__file__).resolve().parent
REPO = HERE.parents[2]
sys.path.insert(0, str(REPO / "tools" / "art-pipeline"))

from art_pipeline.pixel_pass import (  # noqa: E402
    TRANSPARENT, add_outline, nearest_palette_index, opaque_bbox, place_on_canvas, reduce_for_route, threshold_alpha)

N4 = ((1, 0), (-1, 0), (0, 1), (0, -1))   # right, left, down, up (Art Bible 9.3 rim order)
RIM_OK = ("res_ore_1", "res_gold", "steel_2", "steel_3")


def is_background(p) -> bool:
    return p[0] - p[1] > 40 and p[2] - p[1] > 25


def key_background(raw: Image.Image) -> Image.Image:
    """Every background-like pixel (generation background, its cast tint, and the enclosed holes of a gear) to alpha 0."""
    img = raw.convert("RGBA")
    w, h = img.size
    px = img.load()
    out = Image.new("RGBA", img.size, TRANSPARENT)
    op = out.load()
    for y in range(h):
        for x in range(w):
            if not is_background(px[x, y]):
                op[x, y] = px[x, y][:3] + (255,)
    return out


def fit_crop(keyed: Image.Image, max_art: tuple[int, int]) -> tuple[Image.Image, int, dict]:
    bb = opaque_bbox(keyed)
    if bb is None:
        raise ValueError("nothing left after keying")
    bw, bh = bb[2] - bb[0], bb[3] - bb[1]
    k = 1
    while -(-bw // k) > max_art[0] or -(-bh // k) > max_art[1]:
        k += 1
    aw, ah = -(-bw // k), -(-bh // k)
    cx, cy = (bb[0] + bb[2]) // 2, (bb[1] + bb[3]) // 2
    x0, y0 = cx - aw * k // 2, cy - ah * k // 2
    crop = keyed.crop((x0, y0, x0 + aw * k, y0 + ah * k))
    return crop, k, {"subjectBBox": list(bb), "gridFactor": k, "artBlocks": [aw, ah], "cropOrigin": [x0, y0]}


def subset_mapper(palette, ids: tuple[str, ...]):
    rgbs = [palette.rgb(i) for i in ids]
    cache: dict = {}

    def mapper(rgb):
        if rgb not in cache:
            cache[rgb] = rgbs[nearest_palette_index(rgb, rgbs)]
        return cache[rgb]
    return mapper


def _opaque(px, w, h, x, y) -> bool:
    return 0 <= x < w and 0 <= y < h and px[x, y][3] != 0


def rim_lift(img: Image.Image, palette, lift_id: str, ok_ids: tuple[str, ...] = RIM_OK) -> tuple[Image.Image, int]:
    """Section 9.3 rim = first inward 4-neighbour (right, left, down, up) that is opaque and not an outline pixel."""
    out = img.copy()
    src, dst = img.load(), out.load()
    w, h = img.size

    def outline(x, y):
        return src[x, y][3] != 0 and any(not _opaque(src, w, h, x + dx, y + dy) for dx, dy in N4)
    ok = {palette.rgb(i) for i in ok_ids}
    lift = palette.rgb(lift_id) + (255,)
    changed = set()
    for y in range(h):
        for x in range(w):
            if not outline(x, y):
                continue
            for dx, dy in N4:
                nx, ny = x + dx, y + dy
                if _opaque(src, w, h, nx, ny) and not outline(nx, ny):
                    if src[nx, ny][:3] not in ok:
                        changed.add((nx, ny))
                    break
    for x, y in sorted(changed):
        dst[x, y] = lift
    return out, len(changed)


def picture_pass(raw: Image.Image, palette, tones: tuple[str, ...], lift_id: str, max_art: tuple[int, int],
                 ok_ids: tuple[str, ...] = RIM_OK):
    """Raw PNG -> outlined, rim-lifted pixel picture (no canvas yet)."""
    keyed = key_background(raw)
    crop, k, info = fit_crop(keyed, max_art)
    mapper = subset_mapper(palette, tones)
    red = reduce_for_route(crop, "comfy", grid=k, mapper=mapper)
    red = threshold_alpha(red)
    red = _snap(red, mapper)
    outlined = add_outline(red, palette.rgb("ink_0"))
    lifted, n = rim_lift(outlined, palette, lift_id, ok_ids)
    info.update(rimLiftPixels=n, artSizeAfterOutline=list(lifted.size))
    return lifted, info, red


def resource_pass(raw: Image.Image, palette, tones: tuple[str, ...], lift_id: str, max_art: tuple[int, int],
                  canvas: tuple[int, int], footprint: tuple[int, int]):
    lifted, info, red = picture_pass(raw, palette, tones, lift_id, max_art)
    (placed,), placement = place_on_canvas([lifted], canvas, footprint)
    return placed, placement, info, red


def _snap(img: Image.Image, mapper) -> Image.Image:
    out = Image.new("RGBA", img.size, TRANSPARENT)
    s, d = img.load(), out.load()
    for y in range(img.height):
        for x in range(img.width):
            if s[x, y][3]:
                d[x, y] = mapper(s[x, y][:3]) + (255,)
    return out
