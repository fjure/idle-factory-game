"""T438 scripted drawing of the 50 UI glyph icons (Art Bible 12, brief section 5): 24 x 24 art px, one colour.

A tiny, dependency-free raster toolkit. Every shape is a predicate on pixel centres (x + 0.5, y + 0.5), so the
result is a pure function of the code: no randomness, no clock, no font. Coordinates are art pixels, y down.

Stroke rule: an even stroke width is placed on integer coordinates and an odd one on half-integer coordinates,
which gives exactly that many pixel rows or columns for an axis-aligned stroke.

Primitive counting (brief section 5: "at most 6 primitives"): every call of a shape method counts as one
primitive. A repeated element placed by symmetry (the teeth of a gear, the six drag dots, the node rings of a
graph, the four corner arrows) is one `array` or `radial` call, i.e. one primitive of that kind.
"""
from __future__ import annotations

import math
from typing import Callable

N = 24
Pt = tuple[float, float]


def _seg_dist(px: float, py: float, a: Pt, b: Pt) -> float:
    ax, ay = a
    bx, by = b
    dx, dy = bx - ax, by - ay
    L2 = dx * dx + dy * dy
    t = 0.0 if L2 == 0 else max(0.0, min(1.0, ((px - ax) * dx + (py - ay) * dy) / L2))
    return math.hypot(px - (ax + t * dx), py - (ay + t * dy))


def _in_poly(px: float, py: float, pts: list[Pt]) -> bool:
    inside = False
    n = len(pts)
    for i in range(n):
        (x0, y0), (x1, y1) = pts[i], pts[(i + 1) % n]
        if (y0 > py) != (y1 > py) and px < (x1 - x0) * (py - y0) / (y1 - y0) + x0:
            inside = not inside
    return inside


def _angle_in(px: float, py: float, cx: float, cy: float, a0: float, a1: float) -> bool:
    """Angle of the pixel about (cx, cy) in degrees, 0 = east, 90 = south (clockwise on screen), within [a0, a1]."""
    ang = math.degrees(math.atan2(py - cy, px - cx)) % 360.0
    span = (a1 - a0) % 360.0
    return (ang - a0) % 360.0 <= span


class Canvas:
    def __init__(self) -> None:
        self.px: set[tuple[int, int]] = set()
        self.prims = 0
        self.kinds: list[str] = []

    def _paint(self, kind: str, pred: Callable[[float, float], bool], erase: bool = False) -> "Canvas":
        self.prims += 1
        self.kinds.append(("cut-" if erase else "") + kind)
        for y in range(N):
            for x in range(N):
                if pred(x + 0.5, y + 0.5):
                    (self.px.discard if erase else self.px.add)((x, y))
        return self

    # --- filled shapes ----------------------------------------------------------------
    def rect(self, x0: float, y0: float, x1: float, y1: float, erase: bool = False) -> "Canvas":
        return self._paint("rect", lambda x, y: x0 <= x <= x1 and y0 <= y <= y1, erase)

    def disc(self, cx: float, cy: float, r: float, erase: bool = False) -> "Canvas":
        return self._paint("disc", lambda x, y: math.hypot(x - cx, y - cy) <= r, erase)

    def poly(self, pts: list[Pt], erase: bool = False) -> "Canvas":
        return self._paint("poly", lambda x, y: _in_poly(x, y, pts), erase)

    def pill(self, x0: float, y0: float, x1: float, y1: float, erase: bool = False) -> "Canvas":
        r = (y1 - y0) / 2.0
        a, b = (x0 + r, y0 + r), (x1 - r, y0 + r)
        return self._paint("pill", lambda x, y: _seg_dist(x, y, a, b) <= r, erase)

    # --- stroked shapes ---------------------------------------------------------------
    def line(self, x0: float, y0: float, x1: float, y1: float, w: float = 2) -> "Canvas":
        a, b = (x0, y0), (x1, y1)
        return self._paint("line", lambda x, y: _seg_dist(x, y, a, b) <= w / 2.0 + 1e-9)

    def polyline(self, pts: list[Pt], w: float = 2, closed: bool = False) -> "Canvas":
        segs = list(zip(pts, pts[1:] + ([pts[0]] if closed else [])))
        return self._paint("polyline", lambda x, y: any(_seg_dist(x, y, a, b) <= w / 2.0 + 1e-9 for a, b in segs))

    def box(self, x0: float, y0: float, x1: float, y1: float, t: float = 2) -> "Canvas":
        return self._paint("box", lambda x, y: x0 <= x <= x1 and y0 <= y <= y1
                           and not (x0 + t < x < x1 - t and y0 + t < y < y1 - t))

    def ring(self, cx: float, cy: float, r: float, t: float = 2) -> "Canvas":
        return self._paint("ring", lambda x, y: r - t < math.hypot(x - cx, y - cy) <= r)

    def arc(self, cx: float, cy: float, r: float, t: float, a0: float, a1: float) -> "Canvas":
        return self._paint("arc", lambda x, y: r - t < math.hypot(x - cx, y - cy) <= r
                           and _angle_in(x, y, cx, cy, a0, a1))

    def stadium(self, x0: float, y0: float, x1: float, y1: float, t: float = 2) -> "Canvas":
        r = (y1 - y0) / 2.0
        a, b = (x0 + r, y0 + r), (x1 - r, y0 + r)
        return self._paint("stadium", lambda x, y: r - t < _seg_dist(x, y, a, b) <= r)

    # --- arrays (one primitive each) ----------------------------------------------------
    def rects(self, boxes: list[tuple[float, float, float, float]]) -> "Canvas":
        return self._paint("rect-array", lambda x, y: any(a <= x <= c and b <= y <= d for a, b, c, d in boxes))

    def rings(self, centres: list[Pt], r: float, t: float = 2) -> "Canvas":
        return self._paint("ring-array", lambda x, y: any(r - t < math.hypot(x - cx, y - cy) <= r for cx, cy in centres))

    def lines(self, segs: list[tuple[float, float, float, float]], w: float = 2) -> "Canvas":
        s = [((a, b), (c, d)) for a, b, c, d in segs]
        return self._paint("line-array", lambda x, y: any(_seg_dist(x, y, p, q) <= w / 2.0 + 1e-9 for p, q in s))

    def polylines(self, chains: list[list[Pt]], w: float = 2) -> "Canvas":
        segs = [(a, b) for pts in chains for a, b in zip(pts, pts[1:])]
        return self._paint("polyline-array", lambda x, y: any(_seg_dist(x, y, a, b) <= w / 2.0 + 1e-9 for a, b in segs))

    def radial(self, cx: float, cy: float, r0: float, r1: float, angles: list[float], w: float = 2) -> "Canvas":
        s = [((cx + r0 * math.cos(math.radians(a)), cy + r0 * math.sin(math.radians(a))),
              (cx + r1 * math.cos(math.radians(a)), cy + r1 * math.sin(math.radians(a)))) for a in angles]
        return self._paint("radial", lambda x, y: any(_seg_dist(x, y, p, q) <= w / 2.0 + 1e-9 for p, q in s))

    def arrow_head(self, tip: Pt, direction_deg: float, size: float = 4.5, spread: float = 45, w: float = 2) -> "Canvas":
        """Open V head (a stroked chevron, like every other arrow of the set) with its tip at `tip`, pointing along
        `direction_deg`."""
        back = math.radians(direction_deg) + math.pi
        wings = [(tip[0] + size * math.cos(back + s * math.radians(spread)),
                  tip[1] + size * math.sin(back + s * math.radians(spread))) for s in (-1, 1)]
        return self.polyline([wings[0], tip, wings[1]], w)

    def rows(self) -> list[str]:
        return ["".join("#" if (x, y) in self.px else "." for x in range(N)) for y in range(N)]
