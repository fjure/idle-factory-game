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
