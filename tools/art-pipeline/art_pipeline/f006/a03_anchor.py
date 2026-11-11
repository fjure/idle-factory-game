"""F006 T517: measurement of the A-03 badge-anchor candidates (research R17, AD-3; FR-014, FR-025).

`uv run --frozen --offline python -m art_pipeline.f006.a03_anchor --out <dir>` (from tools/art-pipeline) writes the candidate sheets
per screen class and `measurements.json`. Everything is a pure function of (lattice export, direction A sprites, F004 badge, marker
and frame sprites, palette): integer art-pixel geometry, no clock, no randomness, byte-identical on a rerun.

Frame: lattice art pixels, y up, density 24. A sprite pixel and a lattice pixel are the same pixel (nearest neighbour, n = 1); the
badge shift of a candidate is a whole-pixel shift of the F004 badge sprite (80 x 64, pivot (40, 15)) away from the baseline anchor
(59, 33), which is +19 px right and +16 px up from the pivot (Art Bible 3.2: box 7.5..30.5 x 16..39 above the pivot).

Measured per candidate and per layout (3 x 4 portrait, 4 x 3 landscape, 12 occupied slots, every slot badged, both markers on every
machine, one selection frame): the 54 px row band, the selection frame, the overlap proof (plates, hit shapes, machine bodies, markers,
lanes, attribution ratio), the C8 backdrop ring on the direction A world, and the point size of the 23 px badge at `n_min`.
"""
from __future__ import annotations

import argparse
import json
from dataclasses import dataclass
from pathlib import Path

from PIL import Image, ImageDraw, ImageFont

from .. import checks_v1 as c1
from .. import mockup as mk
from ..mockup_geometry import GEOMETRY_PATH, Lattice, build_lattice, consts_of, load_geometry, rect_layout
from ..palette import PALETTE_V1_PATH, Palette, load_palette

REPO = Path(__file__).resolve().parents[4]
DIRECTION_A = REPO / "specs/006-world-first-factory-experience/reviews/direction/direction-a.json"
BADGE_FILES = {
    "blocked": REPO / "art-source/f004/cues/f003-badge-missing-input/f003-badge-missing-input_00.png",
    "faulted": REPO / "art-source/f004/cues/f003-badge-storage-full/f003-badge-storage-full_00.png",
}
MARKER_FILE = REPO / "art-source/f004/markers/f003-marker-boosted/f003-marker-boosted_00.png"
MARKER2_SHIFT = 12            # M2 sits 12 px right of M1 (canvas columns 36..46 against 24..34)
BASE_ANCHOR = (19, 16)        # baseline badge anchor: bottom-centre, +19 right and +16 up from the pivot = canvas (59, 33)
BADGE_PX = 23
PT_RULE = 24.0                # research R06: a cue icon of at least 24 pt at n_min
BAND_BELOW, BAND_ABOVE = 15, 39   # the 54 px row band: 15 px under the pivot (plate bottom) to 39 px above it (badge band top)
PAD = 70                      # canvas padding around the lattice extent, art px
SCHEMA = "f006-a03-anchor/1"
Px = tuple[int, int]


@dataclass(frozen=True)
class Candidate:
    cid: str
    name: str
    dx: int          # badge box bottom-centre, px right of the pivot
    dy: int          # badge box bottom, px above the pivot
    tail: int = 0    # pointer-tail rows below the box (triangle 7, 5, 3, 1 px wide for 4 rows), 0 = none
    note: str = ""

    @property
    def shift(self) -> tuple[int, int]:
        return (self.dx - BASE_ANCHOR[0], self.dy - BASE_ANCHOR[1])


CANDIDATES: tuple[Candidate, ...] = (
    Candidate("K0", "Baseline (59,33)", 19, 16, 0, "F004 S3 = b anchor, the reference"),
    Candidate("K1", "Centred x = 40", 0, 16, 0, "box centred over the pivot, same height"),
    Candidate("K2", "Lower offset", 22, 12, 0, "baseline lowered by 4 px and moved 3 px right (box 12..35 above the pivot, x 10.5..33.5), the lowest point that still clears the plate diamond"),
    Candidate("K3", "Left offset", 13, 16, 0, "baseline moved 6 px left (box x 1.5..24.5), clears the own branch"),
    Candidate("K4", "Pointer tail", 19, 16, 4, "baseline box plus a 4 px tail pointing at the own plate"),
    Candidate("K5", "Lower offset + tail (hybrid)", 22, 12, 4, "K2 plus the K4 tail; not in the task list, measured because the sweep points at it"),
)


# ------------------------------------------------------------------------------------------------------------------ inputs

@dataclass
class Inputs:
    geo: dict
    palette: Palette
    direction: mk.Direction
    badge: dict[str, Image.Image]
    marker: Image.Image


def load_inputs() -> Inputs:
    geo = load_geometry(GEOMETRY_PATH)
    palette = load_palette(PALETTE_V1_PATH)
    return Inputs(geo, palette, mk.load_direction(DIRECTION_A), {k: Image.open(v).convert("RGBA") for k, v in BADGE_FILES.items()},
                  Image.open(MARKER_FILE).convert("RGBA"))


def layout_for(inp: Inputs, columns: int, mixed: bool = False) -> Lattice:
    """The densest frame (12 occupied slots, columns x 12/columns) or, with `mixed`, the direction sheet layout (one empty, one locked)."""
    states = None
    if mixed:
        states = mk.LOCATION_STATES if columns == 3 else {(2, 2): "locked", (2, 1): "empty"}
    layout = rect_layout(f"{columns}x{12 // columns}", columns, 12 // columns, states=states)
    return build_lattice(layout, consts_of(inp.geo), inp.direction.params, "portrait" if columns == 3 else "landscape")


# ---------------------------------------------------------------------------------------------------------------- masks

class Frame:
    """Image frame of a lattice: lattice (x, y up) -> image pixel (x0 + x, y0 - y) at n = 1."""

    def __init__(self, lat: Lattice):
        ew, eh = lat.extent
        self.w, self.h = ew + 2 * PAD, eh + 2 * PAD
        self.tf = mk.Transform(1, PAD, PAD + eh)

    def sprite_mask(self, img: Image.Image, pivot: tuple[int, int], sprite_pivot: tuple[int, int] = mk.LAYER_PIVOT,
                    shift: tuple[int, int] = (0, 0)) -> set[Px]:
        left = self.tf.x0 + pivot[0] - sprite_pivot[0] + shift[0]
        top = self.tf.y0 - pivot[1] - (img.height - sprite_pivot[1]) - shift[1]
        px = img.getchannel("A").load()
        return {(left + x, top + y) for y in range(img.height) for x in range(img.width) if px[x, y]}

    def up(self, p: Px) -> tuple[float, float]:
        """Pixel centre in lattice coordinates (y up)."""
        return (p[0] - self.tf.x0 + 0.5, self.tf.y0 - p[1] - 0.5)


def candidate_sprite(inp: Inputs, cand: Candidate, kind: str) -> Image.Image:
    """The F004 badge sprite (80 x 64, pivot (40, 15), box canvas x 48..70, y 10..32) with the candidate's pointer tail, palette ids only.

    The tail is a triangle below the box, centred on canvas column 59: rows of 7, 5, 3 and 1 px (a shorter tail stops early), `ink_0` on its
    two edges and tip, the badge's own fill inside. The box's `ink_0` bottom row turns to fill under the tail so the tail joins the body. The
    box itself is never altered (the 23 px set is final, Art Bible 4.3)."""
    base = inp.badge[kind]
    if not cand.tail:
        return base
    img = base.copy()
    px = img.load()
    a = base.getchannel("A").load()
    bottom = max(y for y in range(base.height) for x in range(base.width) if a[x, y])
    cx = 59
    fill = px[cx, bottom - 1]
    ink = inp.palette.rgb("ink_0") + (255,)
    for x in range(cx - 3, cx + 4):
        px[x, bottom] = fill
    for i in range(cand.tail):
        half = 3 - i
        for x in range(cx - half, cx + half + 1):
            px[x, bottom + 1 + i] = ink if (x in (cx - half, cx + half) or i == cand.tail - 1) else fill
    return img


def badge_mask(fr: Frame, inp: Inputs, cand: Candidate, kind: str, pivot: tuple[int, int]) -> set[Px]:
    return fr.sprite_mask(candidate_sprite(inp, cand, kind), pivot, shift=cand.shift)


def marker_masks(fr: Frame, inp: Inputs, pivot: tuple[int, int]) -> tuple[set[Px], set[Px]]:
    return (fr.sprite_mask(inp.marker, pivot), fr.sprite_mask(inp.marker, pivot, shift=(MARKER2_SHIFT, 0)))


def in_diamond(p: tuple[float, float], centre: tuple[float, float], hw: float, hh: float) -> bool:
    return abs(p[0] - centre[0]) / hw + abs(p[1] - centre[1]) / hh < 1.0


# ---------------------------------------------------------------------------------------------------------- measurement

def _kind_of(index: int) -> str:
    return "blocked" if index % 2 == 0 else "faulted"


def measure_layout(inp: Inputs, cand: Candidate, columns: int, backdrop: Image.Image | None = None) -> dict:
    """Worst case over the 12 slots of one layout; every count is in art pixels of the opaque badge (box plus tail) mask."""
    lat = layout_for(inp, columns)
    c = lat.consts
    fr = Frame(lat)
    locs = sorted(lat.locations(), key=lambda s: (s.row, s.column))
    body = inp.direction.sprites["machine"].frames[0]
    frame_sprite = inp.direction.sprites["selection"].image
    badges = {(s.row, s.column): badge_mask(fr, inp, cand, _kind_of(i), s.pivot) for i, s in enumerate(locs)}
    markers = {(s.row, s.column): marker_masks(fr, inp, s.pivot) for s in locs}
    bodies = {(s.row, s.column): fr.sprite_mask(body, s.pivot) for s in locs}
    frames = {(s.row, s.column): fr.sprite_mask(frame_sprite, s.pivot) for s in locs}
    lane_px: dict[Px, str] = {}
    for lane in lat.trunks + lat.branches:
        x0, y0, x1, y1 = lane.rect
        for y in range(y0, y1):
            for x in range(x0, x1):
                lane_px[(fr.tf.x0 + x, fr.tf.y0 - y - 1)] = lane.kind

    keys = ("rowBandSpillPx", "ownPlatePx", "foreignPlatePx", "foreignHitPx", "ownFrameHiddenPx", "foreignFrameHiddenPx", "ownBodyPx",
            "foreignBodyPx", "ownMarkerPx", "foreignOverlayPx", "trunkPx", "branchPx", "extentOverflowPx", "badgePx")
    worst = {k: 0 for k in keys}
    ratio_min = 10**9
    own_frac_min = 1.0
    ext_top = fr.tf.y0 - lat.extent[1]
    for s in locs:
        key = (s.row, s.column)
        b = badges[key]
        worst["badgePx"] = max(worst["badgePx"], len(b))
        spill = sum(1 for p in b if not (s.pivot[1] - BAND_BELOW <= fr.up(p)[1] < s.pivot[1] + BAND_ABOVE))
        own_plate = foreign_plate = foreign_hit = own_only = 0
        for p in b:
            u = fr.up(p)
            hits = []
            for t in locs:
                tc = (t.pivot[0], t.pivot[1] + c.centre_above_pivot)
                if in_diamond(u, tc, c.plate_w / 2, c.plate_h / 2):
                    if t is s:
                        own_plate += 1
                    else:
                        foreign_plate += 1
                x0, y0, x1, y1 = t.tile           # the hit shape clipped to the 70 x 54 cell, as SlotGridLayout.HitTest
                if x0 <= u[0] < x1 and y0 <= u[1] < y1 and in_diamond(u, tc, c.hit_w / 2, c.hit_h / 2):
                    hits.append(t)
            if hits and all(t is not s for t in hits):
                foreign_hit += 1
            if len(hits) == 1 and hits[0] is s:
                own_only += 1
        others: set[Px] = set()
        for o in locs:
            if (o.row, o.column) != key:
                others |= badges[(o.row, o.column)] | markers[(o.row, o.column)][0] | markers[(o.row, o.column)][1]
        vals = {
            "rowBandSpillPx": spill, "ownPlatePx": own_plate, "foreignPlatePx": foreign_plate, "foreignHitPx": foreign_hit,
            "ownFrameHiddenPx": len(b & frames[key]),
            "foreignFrameHiddenPx": max((len(b & frames[o]) for o in frames if o != key), default=0),
            "ownBodyPx": len(b & bodies[key]),
            "foreignBodyPx": max((len(b & bodies[o]) for o in bodies if o != key), default=0),
            "ownMarkerPx": len(b & (markers[key][0] | markers[key][1])),
            "foreignOverlayPx": len(b & others),
            "trunkPx": sum(1 for p in b if lane_px.get(p) == "trunk"), "branchPx": sum(1 for p in b if lane_px.get(p) == "branch"),
            "extentOverflowPx": sum(1 for p in b if p[1] < ext_top),
        }
        for k, v in vals.items():
            worst[k] = max(worst[k], v)
        own_frac_min = min(own_frac_min, own_only / len(b))
        # Attribution ratio: nearest foreign plate centre over own plate centre, from the badge centroid, in the plate-diamond metric
        # |dx| / 35 + |dy| / 17.5. Below 1.0 the badge is nearer to a foreign plate (it reads as the machine behind's); near 1.0 it is ambiguous.
        cx = sum(fr.up(p)[0] for p in b) / len(b)
        cy = sum(fr.up(p)[1] for p in b) / len(b)

        def dist(t):
            return abs(cx - t.pivot[0]) / (c.plate_w / 2) + abs(cy - (t.pivot[1] + c.centre_above_pivot)) / (c.plate_h / 2)
        ratio_min = min(ratio_min, min(dist(t) for t in locs if t is not s) / dist(s))
    out = dict(worst)
    out["gaps"] = gaps(inp, fr, lat, cand, badges, frames, bodies)
    out["attributionRatioMin"] = round(ratio_min, 3)
    out["tapOwnOnlyFractionMin"] = round(own_frac_min, 3)
    out["layout"] = f"{columns}x{12 // columns}"
    if backdrop is not None:
        out["c8"] = ring_check(inp, backdrop, badges, markers, lane_px)
        out["halo"] = halo_busyness(inp, backdrop, badges)
    return out


def _gap(mask: set[Px], target: set[Px], rmax: int = 16) -> int | None:
    """Chebyshev gap in px between two masks: 0 = they overlap, 1 = they touch edge to edge or corner to corner, n = n - 1 empty px between."""
    if mask & target:
        return 0
    grown = set(mask)
    for r in range(1, rmax + 1):
        ring = set()
        for (x, y) in grown:
            for dx in (-1, 0, 1):
                for dy in (-1, 0, 1):
                    q = (x + dx, y + dy)
                    if q not in grown:
                        ring.add(q)
        if ring & target:
            return r
        grown |= ring
    return None


def gaps(inp: Inputs, fr: Frame, lat: Lattice, cand: Candidate, badges, frames, bodies) -> dict:
    """Clearances of the badge of the interior slot (1, 1) to: its own plate, own selection frame, own machine body, and the plate and the
    machine body of the slot behind it (0, 1). A badge that is nearer to the foreign machine than to its own reads as the foreign machine's."""
    plate = inp.direction.sprites["plate_occupied"].image
    key, behind = (1, 1), (0, 1)
    s, t = lat.location(*key), lat.location(*behind)
    b = badges[key]
    own_plate = fr.sprite_mask(plate, s.pivot)
    foreign_plate = fr.sprite_mask(plate, t.pivot)
    return {"ownPlate": _gap(b, own_plate), "ownFrame": _gap(b, frames[key]), "ownBody": _gap(b, bodies[key]),
            "behindPlate": _gap(b, foreign_plate), "behindBody": _gap(b, bodies[behind])}


# ------------------------------------------------------------------------------------------------------ backdrop and ring

def world_backdrop(inp: Inputs, columns: int) -> Image.Image:
    """Direction A world layers without the overlay layer (no badge, marker or frame), at art-pixel resolution."""
    lat = layout_for(inp, columns)
    fr = Frame(lat)
    sprites = {k: v for k, v in inp.direction.sprites.items() if not k.startswith("cue_") and k != "selection"}
    img, _ = mk.render_scene({"widthPx": fr.w, "heightPx": fr.h}, lat, inp.direction, inp.palette, frame=0, reduced=False, n=1, sprites=sprites,
                             pip_phase=False, machine_anim=False, selected=None, tf=fr.tf)
    return img


def _ring(mask: set[Px]) -> set[Px]:
    out = set()
    for (x, y) in mask:
        for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1)):
            q = (x + dx, y + dy)
            if q not in mask:
                out.add(q)
    return out


def _tone(inp: Inputs, backdrop: Image.Image, p: Px):
    rgba = backdrop.getpixel(p)
    return inp.palette.id_of(tuple(rgba[:3])) if rgba[3] else "ui_bg"


def ring_check(inp: Inputs, backdrop: Image.Image, badges, markers, lane_px: dict[Px, str]) -> dict:
    """C8: the 1 px ring just outside every badge, ring pixels covered by another overlay excluded. Pass = 100 % in A_ov (rule C1).
    Also counts `steel_*` ring pixels, which Art Director condition 2 forbids although A_ov lists steel_1..3."""
    overlay_all: set[Px] = set()
    for k in badges:
        overlay_all |= badges[k] | markers[k][0] | markers[k][1]
    total = ok = steel = steel_lane = outside = 0
    bad: dict[str, int] = {}
    for b in badges.values():
        for p in _ring(b):
            if p in overlay_all:
                continue
            total += 1
            if not (0 <= p[0] < backdrop.width and 0 <= p[1] < backdrop.height):
                outside += 1
                continue
            tid = _tone(inp, backdrop, p)
            if tid in c1.A_OV:
                ok += 1
            else:
                bad[str(tid)] = bad.get(str(tid), 0) + 1
            if tid and tid.startswith("steel_"):
                steel += 1
                steel_lane += p in lane_px
    return {"ringPx": total, "ringInAov": ok, "ringFractionInAov": round(ok / total, 4) if total else 1.0, "ringSteelPx": steel, "ringSteelOnLanePx": steel_lane,
            "ringOutsideCanvasPx": outside, "forbiddenTones": dict(sorted(bad.items()))}


def halo_busyness(inp: Inputs, backdrop: Image.Image, badges, radius: int = 3) -> dict:
    """Backdrop noise near the badges (Art Director F6, the railing): tone changes between right and down neighbours inside the halo of
    `radius` px around every badge mask, per halo pixel, and the number of distinct tones. Badge pixels are not counted."""
    cover: set[Px] = set().union(*badges.values())
    halo: set[Px] = set()
    for m in badges.values():
        grown = set(m)
        for _ in range(radius):
            grown |= _ring(grown)
        halo |= grown - cover
    halo = {p for p in halo if 0 <= p[0] < backdrop.width and 0 <= p[1] < backdrop.height}
    tones = {p: _tone(inp, backdrop, p) for p in halo}
    changes = sum(1 for p in halo for d in ((1, 0), (0, 1)) if (p[0] + d[0], p[1] + d[1]) in halo and tones[p] != tones[(p[0] + d[0], p[1] + d[1])])
    return {"haloPx": len(halo), "distinctTones": len(set(tones.values())), "toneChangesPerPx": round(changes / len(halo), 4) if halo else 0.0}


# ---------------------------------------------------------------------------------------------------------- legibility

def legibility(inp: Inputs) -> list[dict]:
    """The 23 px badge at `n_min` per device class of the export: points = 23 x n_min / px per pt (research R06). Identical for every candidate
    (the badge is never rescaled); a tail adds rows below the box and no width. Also the height of the hit diamond (the tap target) in points."""
    rows = []
    for key in ("devices", "pointScreens"):
        for s in inp.geo[key]:
            pt = BADGE_PX * s["nMin"] / s["pixelsPerPoint"]
            rows.append({"set": key, "name": s["name"], "class": s["class"], "nMin": s["nMin"], "pxPerPt": s["pixelsPerPoint"],
                         "badgePt": round(pt, 2), "badgeDevPx": BADGE_PX * s["nMin"], "meets24pt": pt >= PT_RULE,
                         "hitDiamondHeightPt": round(inp.geo["hitPx"][1] * s["nMin"] / s["pixelsPerPoint"], 2)})
    return rows


# --------------------------------------------------------------------------------------------------------------- sweep

def sweep(inp: Inputs, columns: int = 3, dxs=range(-12, 31, 2), dys=range(0, 21, 2)) -> list[dict]:
    """A coarse (dx, dy) sweep of the badge box (no tail) for the report."""
    out = []
    for dx in dxs:
        for dy in dys:
            m = measure_layout(inp, Candidate("S", "sweep", dx, dy), columns)
            out.append({"dx": dx, "dy": dy, **{k: m[k] for k in ("rowBandSpillPx", "ownPlatePx", "foreignPlatePx", "foreignHitPx", "ownBodyPx",
                                                                 "ownMarkerPx", "branchPx", "attributionRatioMin", "tapOwnOnlyFractionMin")}})
    return out


# -------------------------------------------------------------------------------------------------------------- sheets

SHEET_CLASSES = (   # (file stem, geometry set, name)
    ("phone-portrait-1080x2400", "devices", "phone-portrait 1080x2400 420dpi"),
    ("phone-landscape-2400x1080", "devices", "phone-landscape 2400x1080 420dpi"),
    ("phone-landscape-640x360-at2", "pointScreens", "phone-landscape 640x360 @2"),
    ("tablet-portrait-1536x2048", "devices", "tablet-portrait 1536x2048 264dpi"),
)


def render_panel(inp: Inputs, cand: Candidate, columns: int, n: int, selected: tuple[int, int], mixed: bool) -> Image.Image:
    """One lattice (extent plus the margin cell) at the integer scale n with the candidate's badges, markers and the selection frame.

    `mixed` = the direction sheet mix (6 running, 2 idle, 1 blocked, 1 faulted, one empty and one locked location); otherwise the densest
    frame (12 occupied, every machine badged, both markers on every machine). The overlays draw above the world layers."""
    lat = layout_for(inp, columns, mixed)
    pad = 12
    ew, eh = lat.extent
    m = lat.params.margin_cells
    x_lo, x_hi = -m * lat.consts.pitch_x, ew + m * lat.consts.pitch_x
    y_lo, y_hi = -m * lat.consts.pitch_y, eh + m * lat.consts.pitch_y
    sprites = {k: v for k, v in inp.direction.sprites.items() if not k.startswith("cue_") and k != "selection"}
    tf = mk.Transform(n, -x_lo * n + pad, y_hi * n + pad)
    row = {"widthPx": (x_hi - x_lo) * n + 2 * pad, "heightPx": (y_hi - y_lo) * n + 2 * pad}
    img, _ = mk.render_scene(row, lat, inp.direction, inp.palette, frame=0, reduced=False, n=n, sprites=sprites, pip_phase=False,
                             machine_anim=False, selected=None, tf=tf)
    locs = sorted(lat.locations(), key=lambda s: (s.row, s.column))
    act = mk.activities(lat) if mixed else {(s.row, s.column): _kind_of(i) for i, s in enumerate(locs)}
    for s in locs:
        a = act.get((s.row, s.column))
        if a is None:
            continue
        for shift in (0, MARKER2_SHIFT):
            mk.blit(img, inp.marker, tf.pt(s.pivot[0] + shift, s.pivot[1]), mk.LAYER_PIVOT, tf)
        if a in ("blocked", "faulted"):
            mk.blit(img, candidate_sprite(inp, cand, a), tf.pt(s.pivot[0] + cand.shift[0], s.pivot[1] + cand.shift[1]), mk.LAYER_PIVOT, tf)
    mk.draw_selection(img, lat, tf, lat.location(*selected), inp.palette, inp.direction.sprites)
    return img


def class_row(inp: Inputs, geo_set: str, name: str) -> dict:
    return next(s for s in inp.geo[geo_set] if s["name"] == name)


def selected_cell(columns: int) -> tuple[int, int]:
    """The selected machine of the direction sheets: front-left on phone portrait (3 columns), back-right otherwise."""
    return (3, 0) if columns == 3 else (0, columns - 1)


def compose_sheet(inp: Inputs, geo_set: str, name: str, mixed: bool, candidates=CANDIDATES) -> Image.Image:
    row = class_row(inp, geo_set, name)
    columns, n = row["columns"], row["nMin"]
    sel = selected_cell(columns)
    panels = [(c, render_panel(inp, c, columns, n, sel, mixed)) for c in candidates]
    pw, ph = panels[0][1].size
    per_row = 3 if pw * 3 <= 3200 else 2
    cap = 22
    rows_n = (len(panels) + per_row - 1) // per_row
    ink = inp.palette.rgb("ui_text") + (255,)
    sheet = Image.new("RGBA", (per_row * (pw + 10) + 10, rows_n * (ph + cap + 10) + 10 + cap), inp.palette.rgb("ui_bg") + (255,))
    d = ImageDraw.Draw(sheet)
    f = ImageFont.load_default()
    d.text((10, 6), f"A-03 candidates | {row['name']} | n_min = {n} | {'sheet mix' if mixed else 'densest frame'} | direction A | badge = {BADGE_PX} px", fill=ink, font=f)
    for i, (c, p) in enumerate(panels):
        x = 10 + (i % per_row) * (pw + 10)
        y = 10 + cap + (i // per_row) * (ph + cap + 10)
        d.text((x, y), f"{c.cid} {c.name}  anchor +{c.dx},+{c.dy}" + (f"  tail {c.tail}px" if c.tail else ""), fill=ink, font=f)
        sheet.alpha_composite(p, (x, y + 14))
    return sheet


def compose_detail(inp: Inputs, candidates=CANDIDATES, scale: int = 8) -> Image.Image:
    """Detail at n = 8: the two front-left slots (3 x 4 lattice, selected machine (3, 0)) with their badge bands, all candidates side by side."""
    lat = layout_for(inp, 3)
    m = lat.params.margin_cells
    pitch_x, pitch_y = lat.consts.pitch_x, lat.consts.pitch_y
    y_top = lat.location(2, 0).pivot[1] + lat.consts.content_above
    y_bot = lat.location(3, 0).pivot[1] - lat.consts.pivot_above
    ox = m * pitch_x * scale + 12
    oy = ((lat.extent[1] + m * pitch_y) - y_top) * scale + 12
    crops = []
    for c in candidates:
        full = render_panel(inp, c, 3, scale, (3, 0), False)
        crops.append((c, full.crop((ox, oy, ox + 2 * pitch_x * scale, oy + (y_top - y_bot) * scale))))
    w, h = crops[0][1].size
    sheet = Image.new("RGBA", (len(crops) * (w + 10) + 10, h + 34), inp.palette.rgb("ui_bg") + (255,))
    d = ImageDraw.Draw(sheet)
    for i, (c, im) in enumerate(crops):
        x = 10 + i * (w + 10)
        d.text((x, 6), f"{c.cid} {c.name}", fill=inp.palette.rgb("ui_text") + (255,), font=ImageFont.load_default())
        sheet.alpha_composite(im, (x, 24))
    return sheet


# ------------------------------------------------------------------------------------------------------------------ run

def measure_all(inp: Inputs, candidates=CANDIDATES) -> dict:
    backdrops = {cols: world_backdrop(inp, cols) for cols in (3, 4)}
    cand_rows = []
    for c in candidates:
        cand_rows.append({"id": c.cid, "name": c.name, "anchor": [c.dx, c.dy], "tailPx": c.tail, "note": c.note,
                          "layouts": [measure_layout(inp, c, cols, backdrops[cols]) for cols in (3, 4)]})
    return {"schema": SCHEMA, "artBible": "F004-PAB v1.0.4", "direction": "direction-a", "badgePx": BADGE_PX, "baseAnchor": list(BASE_ANCHOR),
            "rowBandPx": [-BAND_BELOW, BAND_ABOVE], "candidates": cand_rows, "legibility": legibility(inp)}


def write_all(out: Path | str, with_sheets: bool = True, with_sweep: bool = True) -> dict:
    out = Path(out)
    out.mkdir(parents=True, exist_ok=True)
    inp = load_inputs()
    data = measure_all(inp)
    if with_sweep:
        data["sweep3x4"] = sweep(inp, 3)
    (out / "measurements.json").write_text(json.dumps(data, indent=1) + "\n", encoding="utf-8")
    if with_sheets:
        for stem, geo_set, name in SHEET_CLASSES:
            for mixed in (False, True):
                compose_sheet(inp, geo_set, name, mixed).convert("RGB").save(out / f"{stem}-{'mix' if mixed else 'densest'}.png")
        compose_detail(inp).convert("RGB").save(out / "detail-n8.png")
    return data


def main(argv: list[str] | None = None) -> int:
    ap = argparse.ArgumentParser(prog="a03_anchor")
    ap.add_argument("--out", required=True)
    ap.add_argument("--no-sheets", action="store_true")
    ap.add_argument("--no-sweep", action="store_true")
    args = ap.parse_args(argv)
    data = write_all(args.out, not args.no_sheets, not args.no_sweep)
    print("a03-anchor: %d candidates measured" % len(data["candidates"]))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
