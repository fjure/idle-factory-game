"""F004 T436: the scripted tier frame, pips and numeral of the three item icons (brief row 116, Art Bible 11).

Triple tier cue: frame shape (Tier I circle, II square, III triangle), pip count (1, 2, 3) and numeral (I, II, III); the
frame colour (bronze `res_ore_1`, `ui_text`, gold `res_gold`) is supplemental. Pure function of the tier and the
pictogram: byte-identical on every run. Layout in "art" coordinates (30 x 30, before the 1 px ink outline, which
`pixel_pass.add_outline` adds, giving the 32 x 32 canvas):

  rows 0..21   frame shape, 1 px ring in the frame colour, interior `ui_panel`
  row 22       ink separator (width of the label bar)
  rows 23..29  label bar, `ui_text` fill; pips and numeral in `ink_0` on rows 24..28 (5 rows)
"""
from __future__ import annotations

import sys
from pathlib import Path

from PIL import Image

REPO = Path(__file__).resolve().parents[3]
sys.path.insert(0, str(REPO / "tools" / "art-pipeline"))

from art_pipeline.pixel_pass import TRANSPARENT, add_outline  # noqa: E402

ART = (30, 30)
SHAPE_ROWS = 22
GLYPH_I = ["###", ".#.", ".#.", ".#.", "###"]   # bitmap numeral I, authored here (3 x 5, same size as cues.DIGITS)
TIERS = {
    1: {"shape": "circle", "frame": "res_ore_1", "pips": 1, "numeral": "I", "anchorY": 5},
    2: {"shape": "square", "frame": "ui_text", "pips": 2, "numeral": "II", "anchorY": 5},
    3: {"shape": "triangle", "frame": "res_gold", "pips": 3, "numeral": "III", "anchorY": 9},
}
PICTURE_BOX = (13, 11)   # art px of the pictogram before its own outline (14 x 12 with it)


def shape_mask(shape: str) -> set[tuple[int, int]]:
    cells = set()
    if shape == "circle":                       # diameter 21, centre (14.5, 10.5)
        for y in range(SHAPE_ROWS):
            for x in range(ART[0]):
                if (x - 14.5) ** 2 + (y - 10.5) ** 2 <= 10.5 ** 2 + 0.25:
                    cells.add((x, y))
    elif shape == "square":                     # 24 x 22
        cells = {(x, y) for y in range(SHAPE_ROWS) for x in range(3, 27)}
    elif shape == "triangle":                   # base 30, apex row 0, 22 rows: half width grows 15/21 per row
        for y in range(SHAPE_ROWS):
            half = 0.5 + y * 14.5 / (SHAPE_ROWS - 1)
            for x in range(ART[0]):
                if abs(x - 14.5) <= half:
                    cells.add((x, y))
    else:
        raise ValueError(shape)
    return cells


def ring(cells: set[tuple[int, int]]) -> set[tuple[int, int]]:
    return {(x, y) for x, y in cells
            if any((x + dx, y + dy) not in cells for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1)))}


def bar_cells(width: int) -> tuple[int, int]:
    x0 = (ART[0] - width) // 2
    return x0, x0 + width


def label_layout(tier: int) -> dict:
    t = TIERS[tier]
    pips_w = t["pips"] * 2 + (t["pips"] - 1)
    glyphs_w = len(t["numeral"]) * 3 + (len(t["numeral"]) - 1)
    inner = pips_w + 2 + glyphs_w
    return {"pipsWidth": pips_w, "glyphsWidth": glyphs_w, "innerWidth": inner, "barWidth": inner + 4}


def compose(tier: int, picture: Image.Image, palette) -> tuple[Image.Image, dict]:
    """Returns the 30 x 30 art (before the outline) with `picture` (already outlined) pasted on the panel."""
    t = TIERS[tier]
    img = Image.new("RGBA", ART, TRANSPARENT)
    px = img.load()
    cells = shape_mask(t["shape"])
    edge = ring(cells)
    for x, y in cells:
        px[x, y] = palette.rgb("ui_panel") + (255,)
    for x, y in edge:
        px[x, y] = palette.rgb(t["frame"]) + (255,)
    lay = label_layout(tier)
    x0, x1 = bar_cells(lay["barWidth"])
    for x in range(x0, x1):
        px[x, 22] = palette.rgb("ink_0") + (255,)
        for y in range(23, 30):
            px[x, y] = palette.rgb("ui_text") + (255,)
    ink = palette.rgb("ink_0") + (255,)
    cx = x0 + 2
    for p in range(t["pips"]):               # 2 x 2 pips
        for dx in range(2):
            for dy in range(2):
                px[cx + p * 3 + dx, 25 + dy] = ink
    gx = cx + lay["pipsWidth"] + 2
    glyph = GLYPH_I
    for gi in range(len(t["numeral"])):
        for gy, row in enumerate(glyph):
            for gxx, ch in enumerate(row):
                if ch == "#":
                    px[gx + gi * 4 + gxx, 24 + gy] = ink
    # pictogram: centred on x, its top row at anchorY - (picture height - PICTURE_BOX[1]) / 2
    pw, ph = picture.size
    ox = (ART[0] - pw) // 2
    oy = t["anchorY"] - 1
    img.paste(picture, (ox, oy), picture)
    return img, {"layout": lay, "pictureOrigin": [ox, oy], "frameCells": len(cells)}


def outlined(img: Image.Image, palette) -> Image.Image:
    return add_outline(img, palette.rgb("ink_0"))
