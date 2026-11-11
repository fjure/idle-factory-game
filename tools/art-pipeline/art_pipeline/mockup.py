"""F006 mockup compositor (T505, research R03, R07, R22; FR-040, FR-041).

`python -m art_pipeline mockup --direction DIRECTION.json --out OUT` reads the shared lattice export
(`tools/art-pipeline/f006/lattice-geometry.json`, written by `LatticeGeometryExportTests`), derives the deck cells, the trunks and
branches and the depot and deposit cells (`mockup_geometry`), and composites the direction's sprites at the exact device-pixel
scale `n` of the screen class (an integer, nearest-neighbour, whole device pixels). UI is drawn from a layout description in
points (`ui_layout`) with the F003/F004 UI tokens and the project default font; it is an approximation (research R03 risk): the UX
fit is proven on the emulator and the Windows player, not here.

Inputs: a direction JSON (see `load_direction`) naming sprites by role. The palette gate (`checks.check_palette`) runs on every
sprite input and the flash gate (`checks.check_flash`) on every animation strip. A failing gate is reported and makes the command
exit 1. The output is deterministic: no timestamps, fixed ordering, fixed fonts, fixed encoder settings.

Output per direction in `OUT/<name>/`: PNG sheets per screen class, `-hub` (hub open) and `-text200` (text scale 200 percent)
variants, `triptych.png` (three stages), 6-frame GIF and APNG strips (`machine-loop`, `branch-pip`, `completion-pop`), the
reduced-motion still, and `report.json`.
"""
from __future__ import annotations

import hashlib
import json
from dataclasses import dataclass
from functools import lru_cache
from pathlib import Path

from PIL import Image, ImageDraw, ImageFont

from . import checks
from .color import hex_to_rgb
from .mockup_geometry import (BRANCH_WIDTH, GEOMETRY_PATH, CompositionParams, Lattice, build_lattice, consts_of, decor_cells,
                              decor_violations, lane_violations, load_geometry, rect_layout)
from .palette import PALETTE_V1_PATH, Palette, load_palette

FRAMES = 6
FPS = 6
FRAME_MS = 167

# Sheets of research R03: phone portrait 1080x2400, phone landscape 2400x1080, 640x360-class at 200 %, tablet 1536x2048.
SHEETS = (
    ("phone-portrait", "devices", "phone-portrait 1080x2400 420dpi"),
    ("phone-landscape", "devices", "phone-landscape 2400x1080 420dpi"),
    ("phone-landscape-640x360-200", "pointScreens", "phone-landscape 640x360 @2"),
    ("tablet-portrait", "devices", "tablet-portrait 1536x2048 264dpi"),
)

# F003 UI tokens (FactoryRun.uss) and the type scale of the Art Bible section 11.
TOKENS = {
    "bg": "#111827", "surface": "#1F2937", "surface-raised": "#374151", "text-primary": "#E5E7EB", "text-strong": "#F8FAFC",
    "text-secondary": "#CBD5E1", "text-muted": "#94A3B8", "accent": "#38BDF8", "on-accent": "#0B1020", "positive": "#4ADE80",
    "warning": "#FBBF24", "danger": "#F87171", "border": "#64748B",
}
FONT_PT = {"caption": 12, "body": 14, "value": 16}

# Mixed machine activity on the first ten locations; slot 10 is empty and unlocked, slot 11 is locked (at least 8 machines).
ACTIVITY = ("running", "running", "idle", "running", "blocked", "running", "running", "faulted", "running", "idle")
LOCATION_STATES = {(10 // 3, 10 % 3): "empty", (11 // 3, 11 % 3): "locked"}

# Role names a direction may provide. Required: tile_margin, plate_occupied, machine.
ROLES = ("tile_margin", "tile_filler", "tile_floor", "plate_occupied", "plate_empty", "plate_locked", "machine", "machine_idle", "cue_blocked",
         "cue_faulted", "depot", "vein", "vein_chip", "pip", "pop", "edge", "prop_1", "prop_2", "selection")
# Optional roles (T507): `edge` replaces the margin tile of every free margin cell, `prop_1` and `prop_2` stand on two margin cells,
# `selection` is the frame drawn on the selected machine (a palette outline when absent).
REQUIRED = ("tile_margin", "plate_occupied", "machine")
LAYER_PIVOT = (40, 15)   # the 80 x 64 machine canvas layers (StateCueGeometry)
TILE_PIVOT = (35, 27)    # the 70 x 54 floor tile: its pivot is the footprint centre


@dataclass(frozen=True)
class Sprite:
    frames: tuple[Image.Image, ...]
    pivot: tuple[int, int]   # x from the left, y from the bottom, in sprite pixels

    @property
    def image(self) -> Image.Image:
        return self.frames[0]


@dataclass
class Direction:
    name: str
    sprites: dict[str, Sprite]
    lane_colors: dict[str, str]
    stages: list[dict[str, Sprite]]
    ui: dict
    params: CompositionParams
    source: str = ""
    stage_labels: tuple[str, ...] = ()


def _load_sprite(base: Path, role: str, spec) -> Sprite:
    pivot = None
    if isinstance(spec, dict):
        pivot = tuple(spec["pivot"]) if "pivot" in spec else None
        spec = spec["file"]
    files = [spec] if isinstance(spec, str) else list(spec)
    frames = tuple(Image.open(base / f).convert("RGBA") for f in files)
    default = TILE_PIVOT if role.startswith("tile") or role == "edge" else LAYER_PIVOT
    return Sprite(frames, pivot or default)


def load_direction(path: Path | str) -> Direction:
    """Direction JSON: {"name", "sprites": {role: "file.png" | {"file", "pivot": [x, y_from_bottom]} | ["f0.png", ...]},
    "laneColors": {"trunk", "branch", "active", "pip": palette ids}, "stages": [{role: ...} x3, optional overrides],
    "ui": {"hubLabel", "siteName"}, "composition": {"marginCells", "depotRow"}, "stageLabels": [str x3]}. File paths are relative to the JSON."""
    path = Path(path)
    doc = json.loads(path.read_text(encoding="utf-8"))
    base = path.parent
    sprites = {role: _load_sprite(base, role, spec) for role, spec in doc["sprites"].items()}
    missing = [r for r in REQUIRED if r not in sprites]
    unknown = [r for r in sprites if r not in ROLES]
    if missing or unknown:
        raise ValueError(f"direction {doc.get('name')!r}: missing roles {missing}, unknown roles {unknown}")
    stages = [{role: _load_sprite(base, role, spec) for role, spec in stage.items()} for stage in doc.get("stages", [])]
    comp = doc.get("composition", {})
    params = CompositionParams(margin_cells=int(comp.get("marginCells", 1)), depot_row=comp.get("depotRow", "middle"))
    lanes = {"trunk": "steel_1", "branch": "steel_2", "active": "teal_1", "pip": "lit_0"}
    lanes.update(doc.get("laneColors", {}))
    return Direction(doc["name"], sprites, lanes, stages, doc.get("ui", {}), params, path.name, tuple(doc.get("stageLabels", ())))


# ------------------------------------------------------------------------------------------------ transform and drawing

@dataclass(frozen=True)
class Transform:
    """Lattice art px (y up) to device px (y down): integer scale `n`, device origin of lattice (0, 0)."""
    n: int
    x0: int
    y0: int

    def pt(self, x: float, y: float) -> tuple[int, int]:
        return (round(self.x0 + x * self.n), round(self.y0 - y * self.n))

    def rect(self, r) -> tuple[int, int, int, int]:
        return (self.x0 + r[0] * self.n, self.y0 - r[3] * self.n, self.x0 + r[2] * self.n, self.y0 - r[1] * self.n)


def _visible(tf: "Transform", rect, w: int, h: int) -> bool:
    x0, y0, x1, y1 = tf.rect(rect)
    return x0 >= 0 and y0 >= 0 and x1 <= w and y1 <= h


def _fraction(tf: "Transform", rect, w: int, h: int) -> float:
    x0, y0, x1, y1 = tf.rect(rect)
    area = max(0, min(x1, w) - max(x0, 0)) * max(0, min(y1, h) - max(y0, 0))
    return round(area / ((x1 - x0) * (y1 - y0)), 3)


def make_transform(row: dict, lat: Lattice, n: int | None = None) -> Transform:
    """Centre the location extent in the unobstructed rectangle at the integer scale n (default the row's n_fit). One margin cell
    on each side does not fit at n_fit on a phone (the fit rule keeps only a half-cell margin), so the view is panned toward the
    depot cell, then the deposit cell, by the minimal whole-device-pixel shift that keeps the whole location extent in the sheet
    (a camera pan, never a scale change). What stays clipped is reported by `framing`."""
    n = int(n if n is not None else row["nFit"])
    rx, ry, rw, rh = (float(v) for v in row["unobstructedPx"])
    ew, eh = lat.extent
    left = int(rx + (rw - n * ew) // 2)
    top = int((row["heightPx"] - (ry + rh)) + (rh - n * eh) // 2)
    tf = Transform(n, left, top + n * eh)
    w, h = int(row["widthPx"]), int(row["heightPx"])
    for cell in (lat.depot, lat.deposit):
        if _visible(tf, cell.plate, w, h):
            continue
        x0, y0, x1, y1 = tf.rect(cell.plate)
        dx = -x0 if x0 < 0 else (w - x1 if x1 > w else 0)
        dy = -y0 if y0 < 0 else (h - y1 if y1 > h else 0)
        ex0, ey0, ex1, ey1 = tf.rect((0, 0, ew, eh))
        dx = max(-ex0, min(dx, w - ex1)) if dx else 0
        dy = max(-ey0, min(dy, h - ey1)) if dy else 0
        moved = Transform(n, tf.x0 + dx, tf.y0 + dy)
        if cell is lat.depot or _fraction(moved, lat.depot.plate, w, h) >= _fraction(tf, lat.depot.plate, w, h):
            tf = moved
    return tf


def framing(row: dict, lat: Lattice, tf: Transform, covers: dict | None = None, selected: tuple[int, int] | None = None) -> dict:
    """What the sheet shows of the margin band, for the report: at n_fit the whole margin ring may not fit (a finding for the P1 camera rule)."""
    w, h = int(row["widthPx"]), int(row["heightPx"])
    ew, eh = lat.extent
    if covers is None:
        return {"n": tf.n, "nMin": row["nMin"], "nFit": row["nFit"], "originPx": [tf.x0, tf.y0],
                "extentVisible": _visible(tf, (0, 0, ew, eh), w, h),
                "depotVisible": _visible(tf, lat.depot.plate, w, h), "depotFraction": _fraction(tf, lat.depot.plate, w, h),
                "depositVisible": _visible(tf, lat.deposit.plate, w, h), "depositFraction": _fraction(tf, lat.deposit.plate, w, h)}
    # With the docked UI: what is visible is the sprite box not under the strips, the nav, the side panel, the zoom buttons or the card.
    world = world_rect(row, covers)
    ex = tf.rect((0, 0, ew, eh))
    dep = occupied_boxes(lat, tf)["depot"]
    vein = tf.rect((lat.deposit.pivot[0] - 24, lat.deposit.pivot[1] - 10, lat.deposit.pivot[0] + 24, lat.deposit.pivot[1] + 24))
    out = {"n": tf.n, "nMin": row["nMin"], "nFit": row["nFit"], "originPx": [tf.x0, tf.y0], "worldRectPx": list(world),
           "extentVisible": ex[0] >= world[0] and ex[1] >= world[1] and ex[2] <= world[2] and ex[3] <= world[3],
           "extentClippedPx": {"left": max(0, world[0] - ex[0]), "right": max(0, ex[2] - world[2]), "top": max(0, world[1] - ex[1]), "bottom": max(0, ex[3] - world[3])},
           "depotFraction": visible_fraction(dep, covers, (w, h)), "depositFraction": visible_fraction(vein, covers, (w, h))}
    out["depotVisible"] = out["depotFraction"] == 1.0
    out["depositVisible"] = out["depositFraction"] == 1.0
    if selected is not None:
        sel = occupied_boxes(lat, tf)[f"machine ({selected[0]},{selected[1]})"]
        out["selectedFraction"] = visible_fraction(sel, covers, (w, h))
    return out


def ui_covers(row: dict, shell: dict, ppp: float, card_px: tuple[int, int, int, int] | None = None) -> dict[str, tuple[int, int, int, int]]:
    """The docked UI over the world, in device px: the top strips, the bottom nav on phone portrait, the side panel on tablets and
    desktop, the zoom buttons, and the card. What is not under these is what the player sees of the world."""
    w, h = int(row["widthPx"]), int(row["heightPx"])
    out = {"top": (0, 0, w, round(shell["top"] * ppp))}
    if shell["side"]:
        out["panel"] = (round(shell["right"] * ppp), 0, w, h)
    elif shell["bottom"] < h / ppp:
        out["nav"] = (0, round(shell["bottom"] * ppp), w, h)
    y0 = shell["top"] + 8
    out["zoom"] = (round(4 * ppp), round(y0 * ppp), round(48 * ppp), round((y0 + 2 * 52 + 44) * ppp))
    if card_px is not None:
        out["card"] = (card_px[0], card_px[1], card_px[0] + card_px[2], card_px[1] + card_px[3])
    return out


def world_rect(row: dict, covers: dict) -> tuple[int, int, int, int]:
    """The sheet minus the strips, the nav and the side panel (not the card or the small zoom buttons)."""
    w, h = int(row["widthPx"]), int(row["heightPx"])
    x1 = covers["panel"][0] if "panel" in covers else w
    y1 = covers["nav"][1] if "nav" in covers else h
    return (0, covers["top"][3], x1, y1)


def visible_fraction(box, covers: dict, bounds: tuple[int, int]) -> float:
    """Share of `box` (device px) that is on the sheet and under none of the docked UI."""
    x0, y0, x1, y1 = box
    bw, bh = x1 - x0, y1 - y0
    mask = Image.new("L", (bw, bh), 255)
    d = ImageDraw.Draw(mask)
    big = 10 ** 6
    rects = list(covers.values()) + [(-big, -big, 0, big), (-big, -big, big, 0), (bounds[0], -big, big, big), (-big, bounds[1], big, big)]
    for r in rects:
        d.rectangle((r[0] - x0, r[1] - y0, r[2] - x0 - 1, r[3] - y0 - 1), fill=0)
    return round(mask.histogram()[255] / (bw * bh), 3)


def pan_depot_into_world(lat: Lattice, tf: Transform, world, selected: tuple[int, int], gap: int = 8) -> Transform:
    """Whole-pixel horizontal pan (never a scale change) that brings the depot fully into the world area, as far as the selected
    machine keeps fully inside it. The location extent may then slip past the left edge; `framing` reports that."""
    boxes = occupied_boxes(lat, tf)
    dep, sel = boxes["depot"], boxes[f"machine ({selected[0]},{selected[1]})"]
    if dep[2] > world[2] - gap:
        dx = max(world[2] - gap - dep[2], world[0] - sel[0])
    elif dep[0] < world[0]:
        dx = min(world[0] - dep[0], world[2] - sel[2])
    else:
        return tf
    return Transform(tf.n, tf.x0 + dx, tf.y0) if dx else tf


def blit(canvas: Image.Image, sprite: Image.Image, pivot_xy: tuple[int, int], sprite_pivot: tuple[int, int], tf: Transform, frame_img: Image.Image | None = None) -> None:
    img = frame_img or sprite
    n = tf.n
    big = img.resize((img.width * n, img.height * n), Image.NEAREST)
    left = pivot_xy[0] - sprite_pivot[0] * n
    top = pivot_xy[1] - (img.height - sprite_pivot[1]) * n
    if left >= canvas.width or top >= canvas.height or left + big.width <= 0 or top + big.height <= 0:
        return
    canvas.alpha_composite(big, (max(left, 0), max(top, 0)), (max(-left, 0), max(-top, 0)))


def _frame(sprite: Sprite, index: int) -> Image.Image:
    return sprite.frames[index % len(sprite.frames)]


def _place(canvas, sprites, role, lattice_pt, tf: Transform, index: int = 0, offset: tuple[float, float] = (0, 0)) -> None:
    sp = sprites.get(role)
    if sp is None:
        return
    blit(canvas, sp.image, tf.pt(lattice_pt[0] + offset[0], lattice_pt[1] + offset[1]), sp.pivot, tf, _frame(sp, index))


def _fill(canvas: Image.Image, tf: Transform, rect, rgb) -> None:
    ImageDraw.Draw(canvas).rectangle((lambda r: (r[0], r[1], r[2] - 1, r[3] - 1))(tf.rect(rect)), fill=tuple(rgb) + (255,))


def activities(lat: Lattice) -> dict[tuple[int, int], str]:
    """Activity per occupied location in reading order (row-major), from `ACTIVITY`; the rest run."""
    out: dict[tuple[int, int], str] = {}
    k = 0
    for c in sorted(lat.locations(), key=lambda c: (c.row, c.column)):
        if c.state == "occupied":
            out[(c.row, c.column)] = ACTIVITY[k] if k < len(ACTIVITY) else "running"
            k += 1
    return out


def render_scene(row: dict, lat: Lattice, direction: Direction, palette: Palette, frame: int = 0, reduced: bool = False, n: int | None = None,
                 sprites: dict[str, Sprite] | None = None, pip_phase: bool = True, machine_anim: bool = True, pop: int | None = None,
                 selected: tuple[int, int] | None = None, tf: Transform | None = None) -> tuple[Image.Image, Transform]:
    sprites = sprites or direction.sprites
    tf = tf or make_transform(row, lat, n)
    canvas = Image.new("RGBA", (int(row["widthPx"]), int(row["heightPx"])), palette.rgb("ui_bg") + (255,))
    colors = {k: palette.rgb(v) for k, v in direction.lane_colors.items()}
    act = activities(lat)

    # Deck: floor tiles for every cell including the margin band, back to front. A free margin cell takes the edge sprite when the
    # direction has one; the props stand on two margin cells (never on a location, the depot or the deposit).
    decor = decor_cells(lat)
    edge_cells = {(c.row, c.column) for c in decor["edge"]} if "edge" in sprites else set()
    props = {(c.row, c.column): role for role in ("prop_1", "prop_2") if role in sprites and (c := decor[role]) is not None}
    for c in sorted(lat.cells, key=lambda c: (c.row, c.column)):
        role = "tile_margin" if c.kind == "margin" else ("tile_filler" if c.kind == "filler" else "tile_floor")
        role = role if role in sprites else "tile_margin"
        if (c.row, c.column) in edge_cells:
            role = "edge"
        _place(canvas, sprites, role, c.pivot, tf, 0, (0, 0))
        if (c.row, c.column) in props:
            _place(canvas, sprites, props[(c.row, c.column)], c.pivot, tf)
    # Links: trunks, the spine and branches; the branch shows the static active tile under reduced motion.
    for lane in lat.trunks + ([lat.spine] if lat.spine else []):
        _fill(canvas, tf, lane.rect, colors["trunk"])
    for b in lat.branches:
        running = act.get((b.row, b.column)) == "running"
        _fill(canvas, tf, b.rect, colors["active"] if reduced and running else colors["branch"])
    # Locations back to front: plate, machine, cue badge.
    for c in sorted(lat.locations(), key=lambda c: (c.row, c.column)):
        plate_role = "plate_" + c.state
        _place(canvas, sprites, plate_role if plate_role in sprites else "plate_occupied", c.pivot, tf)
        a = act.get((c.row, c.column))
        if a is None:
            continue
        index = (c.row * lat.columns + c.column)
        if a == "running":
            _place(canvas, sprites, "machine", c.pivot, tf, (frame + index) if (machine_anim and not reduced) else index)
        else:
            _place(canvas, sprites, "machine_idle" if "machine_idle" in sprites else "machine", c.pivot, tf, 0)
            cue = {"blocked": "cue_blocked", "faulted": "cue_faulted"}.get(a)
            if cue:
                bx, by, bw, bh = lat.consts.badge_box
                _place(canvas, sprites, cue, c.pivot, tf, 0, (bx + bw / 2, by))
        if selected == (c.row, c.column):
            draw_selection(canvas, lat, tf, c, palette, sprites)
    # Pips ride only the branches of Running machines (R07); none under reduced motion.
    if not reduced and pip_phase:
        for b in lat.branches:
            if act.get((b.row, b.column)) != "running":
                continue
            t = (frame % FRAMES) / (FRAMES - 1)
            y = b.rect[1] + round(t * (b.rect[3] - b.rect[1] - 1))
            px = (b.rect[0], y, b.rect[0] + BRANCH_WIDTH, y + BRANCH_WIDTH)
            if "pip" in sprites:
                _place(canvas, sprites, "pip", ((b.rect[0] + b.rect[2]) / 2, y), tf)
            else:
                _fill(canvas, tf, px, colors["pip"])
    # Depot and ore vein in the margin band.
    _place(canvas, sprites, "depot", lat.depot.pivot, tf)
    chip = "vein_chip" if "vein_chip" in sprites else "vein"
    _place(canvas, sprites, chip if (not reduced and frame % 2 == 0) else "vein", lat.deposit.pivot, tf, frame)
    if pop is not None:
        draw_pop(canvas, lat, tf, act, pop, palette, sprites)
    return canvas, tf


def draw_selection(canvas, lat, tf, cell, palette, sprites) -> None:
    """The selection frame on the selected machine: the F004 frame sprite when the direction names one, else a palette outline of the plate diamond."""
    if "selection" in sprites:
        _place(canvas, sprites, "selection", cell.pivot, tf)
        return
    (cx, cy), hw, hh = ((cell.pivot[0], cell.pivot[1] + lat.consts.centre_above_pivot), lat.consts.plate_w / 2, lat.consts.plate_h / 2)
    pts = [tf.pt(cx, cy + hh), tf.pt(cx + hw, cy), tf.pt(cx, cy - hh), tf.pt(cx - hw, cy)]
    ImageDraw.Draw(canvas).polygon(pts, outline=palette.rgb("lit_1") + (255,), width=max(1, tf.n))


def draw_pop(canvas, lat, tf, act, step, palette, sprites) -> None:
    """The completion pop on the first running machine: a custom `pop` sprite, else an expanding square ring (6 frames)."""
    first = next((lat.location(r, c) for (r, c), a in sorted(act.items()) if a == "running"), None)
    if first is None:
        return
    if "pop" in sprites:
        _place(canvas, sprites, "pop", first.pivot, tf, step)
        return
    r = 4 + step * 3
    cx, cy = first.pivot[0], first.pivot[1] + 14
    box = tf.rect((cx - r, cy - r, cx + r, cy + r))
    ImageDraw.Draw(canvas).rectangle((box[0], box[1], box[2] - 1, box[3] - 1), outline=palette.rgb("lit_1") + (255,), width=max(1, tf.n))


# ------------------------------------------------------------------------------------------------------------ UI in points

SIDE_CLASSES = ("TabletPortrait", "TabletLandscape", "Desktop")
LINE = 1.25   # line height factor


def _measure_draw() -> ImageDraw.ImageDraw:
    return ImageDraw.Draw(Image.new("RGBA", (1, 1)))


def text_height_pt(text: str, size: str, width_pt: float, ppp: float, scale: float) -> float:
    """Height in points of `text` wrapped at `width_pt` with the shell font at the given text scale (the same wrap as `draw_ui`)."""
    font = _font(FONT_PT[size] * ppp * scale)
    lines = _wrap(_measure_draw(), text, font, round(width_pt * ppp) - 2)
    return len(lines) * font.size * LINE / ppp


def shell_metrics(cls: str, w: float, h: float, ppp: float, scale: float, hint: str = "Next: unlock location 11") -> dict:
    """The closed shell in points: header height, detail bar, the safe rectangle left for the world, the side panel."""
    side = cls in SIDE_CLASSES
    right = w - (360 if side else 0)
    # Header: site name and credits side by side; stacked in two runs once the text scale no longer lets them share a row.
    stacked = scale >= 1.5
    head_h = 48 + (round(FONT_PT["body"] * scale * LINE) if stacked else 0)
    hint_h = max(32.0, text_height_pt(hint, "caption", right - 24, ppp, scale) + 12)
    top = head_h + hint_h
    bottom = h - (56 if cls == "CompactPortrait" else 0)
    return {"cls": cls, "side": side, "right": right, "stacked": stacked, "headH": head_h, "hintH": hint_h, "top": top, "bottom": bottom, "zoomRight": 52}


def card_metrics(w_pt: float, title: str, body: str, ppp: float, scale: float) -> dict:
    """The docked card sized to its wrapped text, so nothing is clipped: title, body, a 44 pt button, padding."""
    inner = w_pt - 24
    title_h = text_height_pt(title, "value", inner, ppp, scale)
    body_h = text_height_pt(body, "body", inner, ppp, scale)
    return {"w": w_pt, "h": round(10 + title_h + 6 + body_h + 10 + 44 + 8, 2), "titleH": title_h, "bodyH": body_h, "title": title, "body": body}


def card_text(activity: str) -> tuple[str, str]:
    title = "Smelter: " + activity.capitalize()
    body = ("Makes 2 plates per cycle. Output goes to the depot." if activity == "running" else "Waiting for ore. Output goes to the depot.")
    return title, body


def ui_layout(cls: str, w: float, h: float, hub_open: bool, hub_label: str, site: str, card: dict | None = None, text_scale: float = 1.0,
              ppp: float = 1.0) -> list[dict]:
    """The shell as a list of elements in panel points (top-left origin): kind rect|text|button, token colours. A fixed model of
    the closed shell (top strip 48 + detail bar 32, 52 pt zoom column, 56 pt tab bar on phone portrait, 360 pt side panel on
    tablets and desktop) with the docked card and the hub panel. `card` is {x, y, w, h, title, body, titleH, bodyH} in points (placed
    by `plan_card`); without it the card sits at the legacy fixed spot. The header carries the site name and the credits as two
    separate text runs (stacked at 150 percent text and up); every text box has the height of its wrapped text."""
    m = shell_metrics(cls, w, h, ppp, text_scale)
    side, portrait_phone, right = m["side"], cls == "CompactPortrait", m["right"]
    items: list[dict] = []
    head, top = m["headH"], m["top"]
    items.append(dict(kind="rect", x=0, y=0, w=right, h=head, fill="surface"))
    if m["stacked"]:
        items.append(dict(kind="text", x=12, y=14, w=right - 24, h=24, text=site, size="value", color="text-strong"))
        items.append(dict(kind="text", x=12, y=14 + round(FONT_PT["value"] * text_scale * LINE), w=right - 24, h=20, text="Credits 12,480", size="body", color="text-secondary"))
    else:
        items.append(dict(kind="text", x=12, y=14, w=(right - 24) * 0.55, h=24, text=site, size="value", color="text-strong"))
        items.append(dict(kind="text", x=12 + (right - 24) * 0.55, y=14, w=(right - 24) * 0.45, h=24, text="Credits 12,480", size="value", color="text-strong", align="right"))
    items.append(dict(kind="rect", x=0, y=head, w=right, h=m["hintH"], fill="surface-raised"))
    items.append(dict(kind="text", x=12, y=head + 6, w=right - 24, h=m["hintH"] - 12, text="Next: unlock location 11", size="caption", color="text-secondary"))
    for i, label in enumerate(("+", "-", "Fit")):
        items.append(dict(kind="button", x=4, y=top + 8 + i * 52, w=44, h=44, text=label, fill="surface", color="text-primary", size="value" if i < 2 else "caption"))
    if side:
        items.append(dict(kind="rect", x=right, y=0, w=360, h=h, fill="surface"))
    elif portrait_phone:
        items.append(dict(kind="rect", x=0, y=h - 56, w=w, h=56, fill="surface"))
        for i, label in enumerate(("Factory", "Build", "Goals", hub_label)):
            items.append(dict(kind="text", x=i * w / 4 + 8, y=h - 38, w=w / 4 - 16, h=20, text=label, size="caption", color="accent" if i == 3 and hub_open else "text-primary"))
    if card is None:
        title, body = card_text("running")
        card = dict(card_metrics(336 if side else (w - 16 if portrait_phone else 308), title, body, ppp, text_scale))
        card["x"], card["y"] = (right + 12, 96) if side else ((8, h - 56 - 8 - card["h"]) if portrait_phone else (w - 316, 88))
    cx, cy, cw, ch = card["x"], card["y"], card["w"], card["h"]
    items.append(dict(kind="rect", x=cx, y=cy, w=cw, h=ch, fill="surface-raised", outline="accent", role="card"))
    items.append(dict(kind="text", x=cx + 12, y=cy + 10, w=cw - 24, h=card["titleH"], text=card["title"], size="value", color="text-strong", role="card"))
    items.append(dict(kind="text", x=cx + 12, y=cy + 10 + card["titleH"] + 6, w=cw - 24, h=card["bodyH"], text=card["body"], size="body", color="text-secondary", role="card"))
    items.append(dict(kind="button", x=cx + 12, y=cy + ch - 52, w=min(160, cw - 24), h=44, text="Upgrade", fill="accent", color="on-accent", size="body", role="card"))
    if hub_open:
        px, py, pw = 24, top + 8, (min(w - 48, 360) if not side else 336)
        items.append(dict(kind="rect", x=px, y=py, w=pw, h=232, fill="surface", outline="accent"))
        items.append(dict(kind="text", x=px + 12, y=py + 10, w=pw - 24, h=24, text=hub_label, size="value", color="text-strong"))
        for i, entry in enumerate(("Build", "Contracts", "Goals", "Settings")):
            items.append(dict(kind="button", x=px + 12, y=py + 44 + i * 44, w=pw - 24, h=40, text=entry, fill="surface-raised", color="text-primary", size="body"))
    return items


def _overlap(a, b) -> bool:
    return a[0] < b[2] and b[0] < a[2] and a[1] < b[3] and b[1] < a[3]


def occupied_boxes(lat: Lattice, tf: Transform) -> dict[str, tuple[int, int, int, int]]:
    """Device-pixel boxes of every machine (80 canvas width, pivot 15 above the bottom, content up to the badge band top 39 above the pivot) and the depot, keyed by name."""
    out = {}
    for c in lat.locations():
        if c.state == "occupied":
            out[f"machine ({c.row},{c.column})"] = tf.rect((c.pivot[0] - 40, c.pivot[1] - 15, c.pivot[0] + 40, c.pivot[1] + 39))
    d = lat.depot.pivot
    out["depot"] = tf.rect((d[0] - 40, d[1] - 22, d[0] + 40, d[1] + 50))
    return out


def plan_card(row: dict, lat: Lattice, tf: Transform, ppp: float, selected: tuple[int, int], card: dict, shell: dict) -> tuple[dict, Transform, dict]:
    """Place the docked card beside the selected machine inside the safe area and pan the view (never a scale change) so the card
    covers no machine and not the depot. Candidates, in order of the class: right, left, below, above of the machine block, each
    with the whole-pixel pan that clears it (clamped so the location extent stays on the sheet). The first candidate that covers
    nothing wins; if none does, the one covering the fewest wins and the report lists what it covers. Side-panel classes dock the
    card in the panel, which never covers the world. Returns (card in points, transform, report)."""
    w, h = int(row["widthPx"]), int(row["heightPx"])
    if shell["side"]:
        sel_top = occupied_boxes(lat, tf)[f"machine ({selected[0]},{selected[1]})"][1] / ppp
        placed = dict(card, x=shell["right"] + 12, y=max(shell["top"] + 8, min(sel_top, h / ppp - card["h"] - 8)))
        return placed, tf, {"position": "side-panel", "covers": [], "panPx": [0, 0], "cardPx": [round(placed["x"] * ppp), round(placed["y"] * ppp), round(card["w"] * ppp), round(card["h"] * ppp)]}
    cw, ch = round(card["w"] * ppp), round(card["h"] * ppp)
    gap = max(4, round(4 * ppp))
    safe = (round(shell["zoomRight"] * ppp), round(shell["top"] * ppp), w - round(8 * ppp), round(shell["bottom"] * ppp) - round(8 * ppp))
    boxes = occupied_boxes(lat, tf)
    sel = boxes[f"machine ({selected[0]},{selected[1]})"]
    mb = (min(b[0] for b in boxes.values()), min(b[1] for b in boxes.values()), max(b[2] for b in boxes.values()), max(b[3] for b in boxes.values()))
    ew, eh = lat.extent
    ext = tf.rect((0, 0, ew, eh))
    clamp = lambda v, lo, hi: max(lo, min(v, hi))
    order = ("below", "above", "right", "left") if shell["cls"] == "CompactPortrait" else ("right", "left", "below", "above")
    best = None
    for pos in order:
        dx = dy = 0
        if pos == "right":
            rect = (safe[2] - cw, clamp(sel[1], safe[1], safe[3] - ch))
            dx = max(safe[0] - ext[0], min(0, rect[0] - gap - mb[2]))
        elif pos == "left":
            rect = (safe[0], clamp(sel[1], safe[1], safe[3] - ch))
            dx = min(w - ext[2], max(0, rect[0] + cw + gap - mb[0]))
        elif pos == "below":
            rect = (clamp(sel[0], safe[0], safe[2] - cw), safe[3] - ch)
            dy = max(safe[1] - ext[1], min(0, rect[1] - gap - mb[3]))
        else:
            rect = (clamp(sel[0], safe[0], safe[2] - cw), safe[1])
            dy = min(h - ext[3], max(0, rect[1] + ch + gap - mb[1]))
        card_px = (rect[0], rect[1], rect[0] + cw, rect[1] + ch)
        moved = {k: (b[0] + dx, b[1] + dy, b[2] + dx, b[3] + dy) for k, b in boxes.items()}
        covers = sorted(k for k, b in moved.items() if _overlap(card_px, b))
        dep = moved["depot"]
        hidden = 1 if (dep[0] < 0 or dep[2] > w or dep[1] < 0 or dep[3] > h) else 0   # a pan that pushes the depot off the sheet counts against a candidate
        score = (f"machine ({selected[0]},{selected[1]})" in covers, len(covers) + hidden)
        if best is None or score < best[0]:
            best = (score, pos, card_px, covers, dx, dy)
        if not covers:
            break
    _, pos, card_px, covers, dx, dy = best
    placed = dict(card, x=card_px[0] / ppp, y=card_px[1] / ppp)
    return placed, Transform(tf.n, tf.x0 + dx, tf.y0 + dy), {"position": pos, "covers": covers, "panPx": [dx, dy], "cardPx": [card_px[0], card_px[1], cw, ch]}


@lru_cache(maxsize=None)
def _font(px: int):
    return ImageFont.load_default(size=max(6, int(px)))


def _wrap(draw, text: str, font, width: float) -> list[str]:
    lines, cur = [], ""
    for word in text.split(" "):
        trial = (cur + " " + word).strip()
        if draw.textlength(trial, font=font) <= width or not cur:
            cur = trial
        else:
            lines.append(cur)
            cur = word
    return lines + [cur]


def draw_ui(canvas: Image.Image, items: list[dict], ppp: float, text_scale: float = 1.0) -> None:
    d = ImageDraw.Draw(canvas)
    col = {k: hex_to_rgb(v) + (255,) for k, v in TOKENS.items()}
    for it in items:
        box = (round(it["x"] * ppp), round(it["y"] * ppp), round((it["x"] + it["w"]) * ppp) - 1, round((it["y"] + it["h"]) * ppp) - 1)
        if it["kind"] in ("rect", "button"):
            d.rectangle(box, fill=col[it["fill"]], outline=col.get(it.get("outline", ""), None))
        if it["kind"] in ("text", "button") and it.get("text"):
            font = _font(FONT_PT[it["size"]] * ppp * text_scale)
            lines = _wrap(d, it["text"], font, round(it["w"] * ppp) - 2 - (0 if it["kind"] == "text" else 8))
            line_h = font.size * LINE
            if it["kind"] == "button":
                y = box[1] + max(0, ((box[3] - box[1]) - line_h * len(lines)) / 2)
                x = box[0] + 8
            else:
                y, x = box[1], box[0]
            for line in lines:
                if y + font.size > box[3] + 1 and it["kind"] != "text":
                    break
                lx = x if it.get("align") != "right" else box[2] - d.textlength(line, font=font)
                d.text((lx, y), line, font=font, fill=col[it["color"]])
                y += line_h


# --------------------------------------------------------------------------------------------------------- products

def _save_png(img: Image.Image, path: Path) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    img.convert("RGB").save(path, format="PNG", optimize=False, compress_level=6)


def _save_strip(frames: list[Image.Image], stem: Path) -> list[Path]:
    rgb = [f.convert("RGB") for f in frames]
    gif = stem.with_suffix(".gif")
    apng = stem.with_suffix(".apng")
    rgb[0].save(gif, format="GIF", save_all=True, append_images=rgb[1:], duration=FRAME_MS, loop=0, optimize=False, disposal=1)
    rgb[0].save(apng, format="PNG", save_all=True, append_images=rgb[1:], duration=FRAME_MS, loop=0)
    return [gif, apng]


def _sha(path: Path) -> str:
    return hashlib.sha256(path.read_bytes()).hexdigest()


def _row_of(geo: dict, section: str, name: str) -> dict:
    for r in geo[section]:
        if r["name"] == name:
            return r
    raise KeyError(f"{name!r} not in {section}")


def sheet_lattice(row: dict, geo: dict, direction: Direction) -> Lattice:
    columns = int(row["columns"])
    layout = rect_layout(f"{columns}-columns", columns, 12 // columns, states=LOCATION_STATES if columns == 3 else {(2, 2): "locked", (2, 1): "empty"})
    return build_lattice(layout, consts_of(geo), direction.params, "portrait" if columns == 3 else "landscape")


def input_gates(direction: Direction, palette: Palette) -> list[dict]:
    out = []
    for stage_idx, sprites in enumerate([direction.sprites] + direction.stages):
        for role in sorted(sprites):
            r = checks.check_palette(list(sprites[role].frames), palette)
            out.append({"stage": stage_idx, "role": role, **r.to_dict()})
            if len(sprites[role].frames) > 1:
                out.append({"stage": stage_idx, "role": role, **checks.check_flash(list(sprites[role].frames), FPS).to_dict()})
    return out


TRI_FOOTER = 64


def selected_machine(sheet: str, lat: Lattice) -> tuple[int, int]:
    """The selected machine of a sheet: the front-left machine on phone portrait (the card docks below it), else the back-right one
    (the card docks to the right of the lattice or in the side panel, level with it)."""
    return (lat.rows - 1, 0) if sheet == "phone-portrait" else (0, lat.columns - 1)


def run_direction(direction: Direction, geo: dict, out_dir: Path, palette: Palette, strip_scale: int | None = None) -> dict:
    """Render every product of one direction; returns the report (written to `report.json`)."""
    root = out_dir / direction.name
    files: list[Path] = []
    gates = input_gates(direction, palette)
    lane_problems: dict[str, list[str]] = {}
    sheet_framing: dict[str, dict] = {}
    card_plans: dict[str, dict] = {}
    decor_problems: dict[str, list[str]] = {}

    for sheet, section, name in SHEETS:
        row = _row_of(geo, section, name)
        lat = sheet_lattice(row, geo, direction)
        lane_problems[sheet] = lane_violations(lat)
        decor_problems[sheet] = decor_violations(lat, decor_cells(lat))
        selected = selected_machine(sheet, lat)
        activity = activities(lat).get(selected, "running")
        ppp = float(row["pixelsPerPoint"])
        hub_label = direction.ui.get("hubLabel", "Hub")
        site = direction.ui.get("siteName", "Site")
        for suffix, hub, scale in (("", False, 1.0), ("-hub", True, 1.0), ("-text200", False, 2.0)):
            wpt, hpt = float(row["widthPt"]), float(row["heightPt"])
            shell = shell_metrics(row["class"], wpt, hpt, ppp, scale)
            card_w = 336 if shell["side"] else min(wpt - 60, 308 if row["class"] != "CompactPortrait" else wpt)
            card = card_metrics(card_w, *card_text(activity), ppp, scale)
            tf0 = make_transform(row, lat)
            tf0 = pan_depot_into_world(lat, tf0, world_rect(row, ui_covers(row, shell, ppp)), selected)
            placed, tf, info = plan_card(row, lat, tf0, ppp, selected, card, shell)
            img, _ = render_scene(row, lat, direction, palette, frame=1, selected=selected, tf=tf)
            draw_ui(img, ui_layout(row["class"], wpt, hpt, hub, hub_label, site, card=placed, text_scale=scale, ppp=ppp), ppp, scale)
            sheet_framing[f"{sheet}{suffix}"] = framing(row, lat, tf, ui_covers(row, shell, ppp, tuple(info["cardPx"])), selected)
            card_plans[f"{sheet}{suffix}"] = {**info, "selected": list(selected), "cardPt": [round(placed["x"], 1), round(placed["y"], 1), card["w"], card["h"]]}
            p = root / f"{sheet}{suffix}.png"
            _save_png(img, p)
            files.append(p)

    # Stage triptych: three stages side by side at scale 2 on the phone-portrait lattice.
    row = _row_of(geo, "devices", SHEETS[0][2])
    lat = sheet_lattice(row, geo, direction)
    tri_n = 2
    panels = []
    for i in range(3):
        stage = direction.stages[i] if i < len(direction.stages) else {}
        sprites = {**direction.sprites, **stage}
        small = dict(row, widthPx=lat.extent[0] * tri_n + 2 * 70 * tri_n, heightPx=lat.extent[1] * tri_n + 2 * 54 * tri_n + 40, unobstructedPx=[0, 0, lat.extent[0] * tri_n + 2 * 70 * tri_n, lat.extent[1] * tri_n + 2 * 54 * tri_n + 40], nFit=tri_n)
        scene, _ = render_scene(small, lat, direction, palette, frame=0, n=tri_n, sprites=sprites)
        label = direction.stage_labels[i] if i < len(direction.stage_labels) else f"Stage {i + 1}"
        img = Image.new("RGBA", (scene.width, scene.height + TRI_FOOTER), palette.rgb("ui_bg") + (255,))
        img.paste(scene, (0, 0))
        dr = ImageDraw.Draw(img)
        font = _font(20)
        yy = scene.height + 6
        for line in _wrap(dr, label, font, scene.width - 24):
            dr.text((12, yy), line, font=font, fill=hex_to_rgb(TOKENS["text-strong"]) + (255,))
            yy += 26
        panels.append(img)
    tri = Image.new("RGBA", (sum(p.width for p in panels), max(p.height for p in panels)), palette.rgb("ui_bg") + (255,))
    x = 0
    for p in panels:
        tri.paste(p, (x, 0))
        x += p.width
    p = root / "triptych.png"
    _save_png(tri, p)
    files.append(p)

    # Strips: 6 frames each, cropped around the first location (plate, machine, branch, trunk), at the phone-portrait scale.
    n = strip_scale or int(row["nFit"])
    base_img, tf = render_scene(row, lat, direction, palette, n=n)
    first = lat.location(0, 0)
    crop = tf.rect((first.pivot[0] - 60, first.pivot[1] - 22, first.pivot[0] + 60, first.pivot[1] + 62))
    crop = (max(crop[0], 0), max(crop[1], 0), min(crop[2], base_img.width), min(crop[3], base_img.height))
    strips = {
        "machine-loop": lambda i: render_scene(row, lat, direction, palette, frame=i, n=n, pip_phase=False)[0],
        "branch-pip": lambda i: render_scene(row, lat, direction, palette, frame=i, n=n, machine_anim=False)[0],
        "completion-pop": lambda i: render_scene(row, lat, direction, palette, frame=0, n=n, pip_phase=False, machine_anim=False, pop=i)[0],
    }
    for sname, make in strips.items():
        frames = [make(i).crop(crop) for i in range(FRAMES)]
        gates.append({"stage": 0, "role": "strip:" + sname, **checks.check_flash(frames, FPS).to_dict()})
        files += _save_strip(frames, root / f"strip-{sname}")
    still, _ = render_scene(row, lat, direction, palette, reduced=True, n=n)
    p = root / "still-reduced-motion.png"
    _save_png(still.crop(crop), p)
    files.append(p)

    failed_gates = [g for g in gates if not g["passed"]]
    failed_lanes = {k: v for k, v in {**lane_problems, **{"decor:" + k: v for k, v in decor_problems.items()}}.items() if v}
    report = {
        "direction": direction.name,
        "source": direction.source,
        "geometrySchema": geo["schema"],
        "gates": gates,
        "laneViolations": lane_problems,
        "framing": sheet_framing,
        "card": card_plans,
        "decorViolations": decor_problems,
        "passed": not failed_gates and not failed_lanes,
        "files": {str(f.relative_to(root)).replace("\\", "/"): _sha(f) for f in sorted(files)},
    }
    (root / "report.json").write_text(json.dumps(report, indent=2, sort_keys=True) + "\n", encoding="utf-8", newline="\n")
    return report


def main(args) -> int:
    geo = load_geometry(args.geometry or GEOMETRY_PATH)
    palette = load_palette(args.palette or PALETTE_V1_PATH)
    out = Path(args.out)
    ok = True
    for d in args.direction:
        direction = load_direction(d)
        report = run_direction(direction, geo, out, palette, args.strip_scale)
        bad = [g for g in report["gates"] if not g["passed"]]
        print("mockup %s: %d files, %s" % (direction.name, len(report["files"]), "all gates pass" if report["passed"] else "FAILING %d gates, %d lane sheets" % (len(bad), sum(1 for v in report["laneViolations"].values() if v))))
        ok = ok and report["passed"]
    return 0 if ok else 1
