"""F004 spike S3/S4 (T421): scripted cue glyph rasterizer (pure Python + Pillow, no randomness).

Every shape is a predicate on the unit square (u, v), v pointing down, sampled at pixel centers of an n x n grid with no
anti-aliasing, so the output is a pure function of (shape, n). Colors are palette-v0 ids resolved by the caller.
"""
from __future__ import annotations

from PIL import Image

# --- predicates ---------------------------------------------------------------


def rect(u0, u1, v0, v1):
    return lambda u, v: u0 <= u <= u1 and v0 <= v <= v1


def circle(cx, cy, r):
    return lambda u, v: (u - cx) ** 2 + (v - cy) ** 2 <= r * r


def ring(cx, cy, r_out, r_in):
    return lambda u, v: r_in * r_in < (u - cx) ** 2 + (v - cy) ** 2 <= r_out * r_out


def tri(p, q, r):
    def f(u, v):
        d1 = (u - q[0]) * (p[1] - q[1]) - (p[0] - q[0]) * (v - q[1])
        d2 = (u - r[0]) * (q[1] - r[1]) - (q[0] - r[0]) * (v - r[1])
        d3 = (u - p[0]) * (r[1] - p[1]) - (r[0] - p[0]) * (v - p[1])
        neg = d1 < 0 or d2 < 0 or d3 < 0
        pos = d1 > 0 or d2 > 0 or d3 > 0
        return not (neg and pos)
    return f


def rrect(u0, u1, v0, v1, r):
    def f(u, v):
        if not (u0 <= u <= u1 and v0 <= v <= v1):
            return False
        cx = min(max(u, u0 + r), u1 - r)
        cy = min(max(v, v0 + r), v1 - r)
        return (u - cx) ** 2 + (v - cy) ** 2 <= r * r
    return f


def scaled(f, k):
    """f shrunk by k about the center of the unit square."""
    return lambda u, v: f(0.5 + (u - 0.5) / k, 0.5 + (v - 0.5) / k)


def union(*fs):
    return lambda u, v: any(f(u, v) for f in fs)


def minus(a, b):
    return lambda u, v: a(u, v) and not b(u, v)


# --- plates (outer badge shapes) and glyphs --------------------------------------

PLATE_CIRCLE = circle(0.5, 0.5, 0.5)
PLATE_TRIANGLE = tri((0.5, 0.0), (1.0, 1.0), (0.0, 1.0))
PLATE_SQUARE = rrect(0.0, 1.0, 0.0, 1.0, 0.16)
PLATE_BADGE = rrect(0.0, 1.0, 0.0, 1.0, 0.22)      # variant (b): one plate for every state

GLYPH_HOURGLASS = union(rect(0.22, 0.78, 0.14, 0.24), rect(0.22, 0.78, 0.76, 0.86),
                        tri((0.28, 0.24), (0.72, 0.24), (0.5, 0.5)), tri((0.5, 0.5), (0.28, 0.76), (0.72, 0.76)),
                        rect(0.45, 0.55, 0.44, 0.56))
# T460: waiting glyph of variant b at 23 px: bold hourglass with a solid (ink) upper sand mass, a hollow lower bulb with a
# sand pile and a falling stream; the value mass sits in the upper half, unlike the stack, arrow, pause bars and ring.
GLYPH_HOURGLASS_SAND = union(rect(0.12, 0.88, 0.08, 0.22), rect(0.12, 0.88, 0.78, 0.92),
                             tri((0.2, 0.26), (0.8, 0.26), (0.5, 0.5)),
                             minus(tri((0.5, 0.5), (0.16, 0.78), (0.84, 0.78)), tri((0.5, 0.58), (0.27, 0.78), (0.73, 0.78))),
                             rect(0.44, 0.56, 0.46, 0.7), tri((0.5, 0.64), (0.34, 0.78), (0.66, 0.78)))
GLYPH_BANG = union(rect(0.43, 0.57, 0.38, 0.66), rect(0.43, 0.57, 0.74, 0.86))   # sits in the triangle's lower body
GLYPH_STACK = union(rect(0.18, 0.82, 0.2, 0.36), rect(0.18, 0.82, 0.42, 0.58), rect(0.18, 0.82, 0.64, 0.8))
GLYPH_ARROW = union(rect(0.43, 0.57, 0.14, 0.48), tri((0.24, 0.44), (0.76, 0.44), (0.5, 0.74)),
                    rect(0.2, 0.8, 0.8, 0.9))
GLYPH_PAUSE = union(rect(0.0, 0.32, 0.0, 1.0), rect(0.68, 1.0, 0.0, 1.0))
GLYPH_RING_DASH = union(ring(0.5, 0.5, 0.5, 0.34), rect(0.36, 0.64, 0.43, 0.57))


def mask(n: int, f) -> list[list[bool]]:
    return [[bool(f((x + 0.5) / n, (y + 0.5) / n)) for x in range(n)] for y in range(n)]


def dilate(m: list[list[bool]]) -> list[list[bool]]:
    h, w = len(m), len(m[0])
    out = [[False] * w for _ in range(h)]
    for y in range(h):
        for x in range(w):
            if m[y][x]:
                for dx, dy in ((0, 0), (1, 0), (-1, 0), (0, 1), (0, -1)):
                    xx, yy = x + dx, y + dy
                    if 0 <= xx < w and 0 <= yy < h:
                        out[yy][xx] = True
    return out


def badge(n: int, plate, plate_rgb, glyph, glyph_rgb, ink_rgb) -> Image.Image:
    """n x n badge including its 1 px ink outline. plate=None draws the glyph alone (filled glyph_rgb, ink outline)."""
    m = n - 2
    img = Image.new("RGBA", (n, n), (0, 0, 0, 0))
    px = img.load()
    body = mask(m, plate) if plate is not None else mask(m, glyph)
    pad = [[False] * n for _ in range(n)]
    for y in range(m):
        for x in range(m):
            pad[y + 1][x + 1] = body[y][x]
    for y, row in enumerate(dilate(pad)):
        for x, v in enumerate(row):
            if v:
                px[x, y] = ink_rgb + (255,)
    if plate is not None:
        gm = mask(m, glyph)
        for y in range(m):
            for x in range(m):
                if body[y][x]:
                    px[x + 1, y + 1] = (ink_rgb if False else plate_rgb) + (255,)
        for y in range(m):
            for x in range(m):
                if body[y][x] and gm[y][x]:
                    px[x + 1, y + 1] = glyph_rgb + (255,)
    else:
        for y in range(m):
            for x in range(m):
                if body[y][x]:
                    px[x + 1, y + 1] = glyph_rgb + (255,)
    return img
