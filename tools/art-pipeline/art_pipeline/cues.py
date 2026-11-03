"""Scripted drawing of state-cue icons, markers and simple UI glyphs (route `scripted`).

Pure functions of (name, palette): pixel-identical on every run. Shapes are exact and
distinct so the grayscale and CVD tests apply unchanged. Outlines are added by `pixel_pass`.
"""
from __future__ import annotations

from PIL import Image

from .palette import Palette

CUE_SIZE = 12  # state cue icons: 12x12 art px, meets the >= 12x12 rule before the outline
MARKER_SIZE = 8

# state -> (primary color id, secondary color id)
CUE_COLORS = {
    "pause": ("cue_dim", "cue_neutral"),
    "power": ("cue_dim", "cue_neutral"),
    "missing_input": ("cue_warn", "cue_neutral"),
    "storage_full": ("cue_alert", "cue_neutral"),
    "waiting": ("cue_info", "cue_neutral"),
}
MARKER_COLORS = {"boosted": ("cue_ok", "cue_neutral"), "overdrive": ("cue_warn", "cue_neutral")}

MARKER_BITMAPS = {
    "boosted": [
        "...##...",
        "..####..",
        ".######.",
        "########",
        "...##...",
        "...##...",
        "...##...",
        "...##...",
    ],
    "overdrive": [
        "....###.",
        "...###..",
        "..###...",
        ".######.",
        "...###..",
        "..###...",
        ".###....",
        "###.....",
    ],
}

DIGITS = {  # 3x5 bitmap font, authored here (no external license)
    "0": ["###", "#.#", "#.#", "#.#", "###"],
    "1": [".#.", "##.", ".#.", ".#.", "###"],
    "2": ["###", "..#", "###", "#..", "###"],
    "3": ["###", "..#", "###", "..#", "###"],
    "4": ["#.#", "#.#", "###", "..#", "..#"],
    "5": ["###", "#..", "###", "..#", "###"],
    "6": ["###", "#..", "###", "#.#", "###"],
    "7": ["###", "..#", ".#.", ".#.", ".#."],
    "8": ["###", "#.#", "###", "#.#", "###"],
    "9": ["###", "#.#", "###", "..#", "###"],
}


def _new(size: int) -> list[list[int]]:
    return [[0] * size for _ in range(size)]  # 0 empty, 1 primary, 2 secondary


def _pause() -> list[list[int]]:
    g = _new(CUE_SIZE)
    for y in range(CUE_SIZE):
        for x in list(range(0, 5)) + list(range(7, 12)):
            g[y][x] = 1
    return g


def _power() -> list[list[int]]:
    g = _new(CUE_SIZE)
    c = (CUE_SIZE - 1) / 2
    for y in range(CUE_SIZE):
        for x in range(CUE_SIZE):
            d = ((x - c) ** 2 + (y - c) ** 2) ** 0.5
            if 3.8 <= d <= 6.0 and not (abs(x - c) < 1.6 and y < c):
                g[y][x] = 1
            if abs(x - c) < 1.1 and y <= 6:
                g[y][x] = 2
    return g


def _missing_input() -> list[list[int]]:
    g = _new(CUE_SIZE)
    last = CUE_SIZE - 1
    for i in range(CUE_SIZE):
        for x, y in ((i, 0), (i, last), (0, i), (last, i)):
            if (x + y) % 2 == 0:
                g[y][x] = 1
    for y in range(2, 6):
        g[y][5] = g[y][6] = 2
    for k, y in enumerate(range(6, 9)):
        for x in range(4 + k, 8 - k):
            g[y][x] = 2
    return g


def _storage_full() -> list[list[int]]:
    """Diamond (full bin) with a secondary band across the middle."""
    g = _new(CUE_SIZE)
    for y in range(CUE_SIZE):
        half = min(y, CUE_SIZE - 1 - y) + 1
        for x in range(CUE_SIZE // 2 - half, CUE_SIZE // 2 + half):
            g[y][x] = 1
    for y in (5, 6):
        for x in range(CUE_SIZE):
            g[y][x] = 2
    return g


def _waiting() -> list[list[int]]:
    g = _new(CUE_SIZE)
    for y in range(CUE_SIZE):
        half = min(y, CUE_SIZE - 1 - y)
        for x in range(half, CUE_SIZE - half):
            g[y][x] = 1
    for y in (0, CUE_SIZE - 1):
        for x in range(CUE_SIZE):
            g[y][x] = 2
    return g


_BUILDERS = {"pause": _pause, "power": _power, "missing_input": _missing_input,
             "storage_full": _storage_full, "waiting": _waiting}


def _render(grid: list[list[int]], primary, secondary, scale: int = 1) -> Image.Image:
    h, w = len(grid), len(grid[0])
    img = Image.new("RGBA", (w * scale, h * scale), (0, 0, 0, 0))
    px = img.load()
    for y in range(h):
        for x in range(w):
            v = grid[y][x]
            if v:
                color = (primary if v == 1 else secondary) + (255,)
                for dy in range(scale):
                    for dx in range(scale):
                        px[x * scale + dx, y * scale + dy] = color
    return img


def cue_names() -> list[str]:
    return sorted(CUE_COLORS)


def render_cue(name: str, palette: Palette, scale: int = 1) -> Image.Image:
    """State cue icon, 12x12 art px (times the cue's own integer factor `scale`)."""
    p, s = CUE_COLORS[name]
    return _render(_BUILDERS[name](), palette.rgb(p), palette.rgb(s), scale)


def marker_names() -> list[str]:
    return sorted(MARKER_BITMAPS)


def render_marker(name: str, palette: Palette, scale: int = 1) -> Image.Image:
    rows = MARKER_BITMAPS[name]
    grid = [[1 if ch == "#" else 0 for ch in row] for row in rows]
    p, _s = MARKER_COLORS[name]
    return _render(grid, palette.rgb(p), palette.rgb(p), scale)


def render_selection_frame(width: int, height: int, palette: Palette, thickness: int = 1,
                           color_id: str = "ui_accent") -> Image.Image:
    img = Image.new("RGBA", (width, height), (0, 0, 0, 0))
    px = img.load()
    rgba = palette.rgb(color_id) + (255,)
    for y in range(height):
        for x in range(width):
            if x < thickness or y < thickness or x >= width - thickness or y >= height - thickness:
                px[x, y] = rgba
    return img


def render_digits(text: str, palette: Palette, color_id: str = "ui_text", scale: int = 1) -> Image.Image:
    """Locked-plate digits from the authored 3x5 font, one transparent column between glyphs."""
    glyph_w = 3 * scale
    width = len(text) * glyph_w + (len(text) - 1) * scale
    img = Image.new("RGBA", (width, 5 * scale), (0, 0, 0, 0))
    px = img.load()
    rgba = palette.rgb(color_id) + (255,)
    for i, ch in enumerate(text):
        for y, row in enumerate(DIGITS[ch]):
            for x, c in enumerate(row):
                if c == "#":
                    for dy in range(scale):
                        for dx in range(scale):
                            px[i * (glyph_w + scale) + x * scale + dx, y * scale + dy] = rgba
    return img


# =====================================================================================================
# Production drawing for F004-PAB v1.0 (T437). Additive: everything above is the spike-era API.
# Overlay sprites are full 80 x 64 machine-canvas layers (pivot (40, 15), footprint box 68 x 30, Art Bible 3.2).
# All shapes are pure predicates or bitmaps sampled at pixel centres: no randomness, no anti-aliasing.
# =====================================================================================================

CANVAS = (80, 64)
FOOTPRINT = (68, 30)
PIVOT_PX = (40, 15)            # origin bottom-left, whole art pixel (rule P1)
INK_ID = "ink_0"
SCRIPT_VERSION = "t437-1"

BADGE_INNER = 21               # 23 x 23 with the 1 px pixel_pass outline (Art Bible 8.4)
BADGE_BOX = (48, 10, 71, 33)   # x0, y0, x1, y1 (exclusive): bottom-centre anchor (59, 33)
MARKER_INNER = 9               # 11 x 11 with the outline (Art Bible 3.2, DEV-3)
MARKER_BOXES = {"boosted": (24, 16, 35, 27), "overdrive": (36, 16, 47, 27)}  # M1 / M2
LABEL_SCALE = 3                # owner decision R4: authored 3 x 5 font at scale 3 = 9 x 15 digit
LABEL_CENTRE = (40, 49)        # the footprint centre = the pivot (40, 15 from the bottom)
LOCK_LEVELS = ("2", "3", "4", "5")
FRAME_RAW_WIDTH = 68           # raw diamond width; 70 with the outline = the 70 x 35 plate diamond (3.2a)
FRAME_APEX = 4                 # clipped apex width (3.2a)
FRAME_TOP_ROW = 30             # raw top row; outline row 29 = plate apex row

# F003 key -> (state, fill colour id, glyph name, glyph scale). Fill semantics: Art Bible 8.1 / 8.2.
BADGE_SPECS = {
    "F003-BADGE-WAITING": ("waiting", "cue_info", "hourglass_sand", 1.0),
    "F003-BADGE-MISSING-INPUT": ("missing-input", "cue_alert", "arrow", 1.0),
    "F003-BADGE-STORAGE-FULL": ("storage-full", "cue_warn", "stack", 1.0),
    "F003-BADGE-PAUSED": ("paused", "cue_neutral", "pause", 0.62),
    "F003-BADGE-INACTIVE": ("inactive", "cue_dim", "ring_dash", 0.7),
}
RUNNING_BADGE_KEY = "F003-BADGE-RUNNING"
MARKER_SPECS = {"F003-MARKER-BOOSTED": ("boosted", "cvd_green"), "F003-MARKER-OVERDRIVE": ("overdrive", "cvd_pink")}
FRAME_KEY = "F003-SLOT-SELECTED-FRAME"
LABEL_KEYS = {"F004-LABEL-LOCK-L%s" % n: n for n in LOCK_LEVELS}


def asset_id(key: str) -> str:
    """Manifest assetId: the lowercase kebab form of the key (brief section 1)."""
    return key.lower()


# --- unit-square predicates (u right, v down) ---------------------------------------------------

def _rect(u0, u1, v0, v1):
    return lambda u, v: u0 <= u <= u1 and v0 <= v <= v1


def _ring(cx, cy, r_out, r_in):
    return lambda u, v: r_in * r_in < (u - cx) ** 2 + (v - cy) ** 2 <= r_out * r_out


def _tri(p, q, r):
    def f(u, v):
        d1 = (u - q[0]) * (p[1] - q[1]) - (p[0] - q[0]) * (v - q[1])
        d2 = (u - r[0]) * (q[1] - r[1]) - (q[0] - r[0]) * (v - r[1])
        d3 = (u - p[0]) * (r[1] - p[1]) - (r[0] - p[0]) * (v - p[1])
        return not ((d1 < 0 or d2 < 0 or d3 < 0) and (d1 > 0 or d2 > 0 or d3 > 0))
    return f


def _rrect(u0, u1, v0, v1, r):
    def f(u, v):
        if not (u0 <= u <= u1 and v0 <= v <= v1):
            return False
        cx, cy = min(max(u, u0 + r), u1 - r), min(max(v, v0 + r), v1 - r)
        return (u - cx) ** 2 + (v - cy) ** 2 <= r * r
    return f


def _scaled(f, k):
    return lambda u, v: f(0.5 + (u - 0.5) / k, 0.5 + (v - 0.5) / k)


def _union(*fs):
    return lambda u, v: any(f(u, v) for f in fs)


def _minus(a, b):
    return lambda u, v: a(u, v) and not b(u, v)


_GLYPHS = {
    # bold hourglass: solid upper sand, hollow lower bulb with pile and stream (T460)
    "hourglass_sand": _union(
        _rect(0.12, 0.88, 0.08, 0.22), _rect(0.12, 0.88, 0.78, 0.92), _tri((0.2, 0.26), (0.8, 0.26), (0.5, 0.5)),
        _minus(_tri((0.5, 0.5), (0.16, 0.78), (0.84, 0.78)), _tri((0.5, 0.58), (0.27, 0.78), (0.73, 0.78))),
        _rect(0.44, 0.56, 0.46, 0.7), _tri((0.5, 0.64), (0.34, 0.78), (0.66, 0.78))),
    "arrow": _union(_rect(0.43, 0.57, 0.14, 0.48), _tri((0.24, 0.44), (0.76, 0.44), (0.5, 0.74)),
                    _rect(0.2, 0.8, 0.8, 0.9)),
    "stack": _union(_rect(0.18, 0.82, 0.2, 0.36), _rect(0.18, 0.82, 0.42, 0.58), _rect(0.18, 0.82, 0.64, 0.8)),
    "pause": _union(_rect(0.0, 0.32, 0.0, 1.0), _rect(0.68, 1.0, 0.0, 1.0)),
    "ring_dash": _union(_ring(0.5, 0.5, 0.5, 0.34), _rect(0.36, 0.64, 0.43, 0.57)),
}
_BADGE_PLATE = _rrect(0.0, 1.0, 0.0, 1.0, 0.22)

# 9 x 9 marker plates (corners trimmed) carry a glyph in ink_0.
MARKER_GLYPHS = {
    "boosted": [   # double up-chevron
        ".........",
        "...###...",
        "..##.##..",
        ".##...##.",
        ".........",
        "...###...",
        "..##.##..",
        ".##...##.",
        ".........",
    ],
    "overdrive": [  # lightning bolt
        ".........",
        "....####.",
        "...###...",
        "..###....",
        ".#######.",
        "....###..",
        "...###...",
        "..##.....",
        ".........",
    ],
}


def _sample(n: int, f) -> list[list[bool]]:
    return [[bool(f((x + 0.5) / n, (y + 0.5) / n)) for x in range(n)] for y in range(n)]


def _paint(n: int, plate: list[list[bool]], glyph: list[list[bool]], fill: tuple, ink: tuple) -> Image.Image:
    img = Image.new("RGBA", (n, n), (0, 0, 0, 0))
    px = img.load()
    for y in range(n):
        for x in range(n):
            if plate[y][x]:
                px[x, y] = (ink if glyph[y][x] else fill) + (255,)
    return img


def raw_badge(key: str, palette: Palette) -> Image.Image:
    """21 x 21 plate in the state's reserved cue colour with the glyph in `ink_0` (before the outline)."""
    _state, fill_id, glyph, k = BADGE_SPECS[key]
    return _paint(BADGE_INNER, _sample(BADGE_INNER, _BADGE_PLATE), _sample(BADGE_INNER, _scaled(_GLYPHS[glyph], k)),
                  palette.rgb(fill_id), palette.rgb(INK_ID))


def _bitmap(rows: list[str]) -> list[list[bool]]:
    return [[ch == "#" for ch in row] for row in rows]


def _trimmed_plate(n: int) -> list[list[bool]]:
    return [[not ((x in (0, n - 1)) and (y in (0, n - 1))) for x in range(n)] for y in range(n)]


def raw_marker(key: str, palette: Palette) -> Image.Image:
    name, fill_id = MARKER_SPECS[key]
    n = MARKER_INNER
    return _paint(n, _trimmed_plate(n), _bitmap(MARKER_GLYPHS[name]), palette.rgb(fill_id), palette.rgb(INK_ID))


def raw_glyph_mask(key: str) -> Image.Image:
    """Silhouette of the ink glyph alone (opaque where the glyph is), used for the peer-shape IoU check."""
    if key in BADGE_SPECS:
        n, g = BADGE_INNER, _sample(BADGE_INNER, _scaled(_GLYPHS[BADGE_SPECS[key][2]], BADGE_SPECS[key][3]))
    else:
        n, g = MARKER_INNER, _bitmap(MARKER_GLYPHS[MARKER_SPECS[key][0]])
    img = Image.new("RGBA", (n, n), (0, 0, 0, 0))
    px = img.load()
    for y in range(n):
        for x in range(n):
            if g[y][x]:
                px[x, y] = (255, 255, 255, 255)
    return img


def _erode(cells: set[tuple[int, int]]) -> set[tuple[int, int]]:
    return {(x, y) for (x, y) in cells if all((x + dx, y + dy) in cells for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1)))}


def frame_diamond() -> set[tuple[int, int]]:
    """Raw 2:1 diamond, 68 wide, 33 rows (from FRAME_TOP_ROW), clipped 4 px apex, centred on the canvas."""
    cells = set()
    rows = 33
    for r in range(rows):
        w = min(FRAME_RAW_WIDTH, FRAME_APEX + 4 * min(r, rows - 1 - r))
        x0 = CANVAS[0] // 2 - w // 2
        for x in range(x0, x0 + w):
            cells.add((x, FRAME_TOP_ROW + r))
    return cells


def raw_frame(palette: Palette) -> Image.Image:
    """Selection frame before the outline: outermost raw layer `ui_accent`, a 1 px `ink_0` layer inside it (inner outline);
    pixel_pass adds the outer ink outline. On the 80 x 64 canvas."""
    s = frame_diamond()
    acc_cells = s - _erode(s)
    ink_cells = {(x + dx, y + dy) for (x, y) in acc_cells for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1))} & s
    ink_cells -= acc_cells
    acc, ink = palette.rgb("ui_accent") + (255,), palette.rgb(INK_ID) + (255,)
    frame = Image.new("RGBA", CANVAS, (0, 0, 0, 0))
    fp = frame.load()
    for (x, y) in acc_cells:
        fp[x, y] = acc
    for (x, y) in ink_cells:
        fp[x, y] = ink
    return frame


def raw_label(level: str, palette: Palette) -> Image.Image:
    """Locked-plate digit: authored 3 x 5 font at scale 3 (9 x 15 px, cap 15), `ui_text`."""
    return render_digits(level, palette, "ui_text", LABEL_SCALE)
