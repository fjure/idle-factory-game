"""F006 T522: scripted LINK tiles and FX sprites (Art Bible v1.1.0 sections 21.2, 21.4, 21.7, 21.8; research R07, R12).

`uv run --frozen --offline python -m art_pipeline.f006.link_fx --out art-source/f006` (from tools/art-pipeline) writes
`<out>/link/` (4 tiles) and `<out>/fx/` (7 sprites of 6 frames plus the static completion highlight): PNGs, one provenance record per
asset, a manifest, a family-scoped `check-report.json` and review sheets. Everything is a pure function of (palette, this file, the
F004 machine frames): integer pixel drawing from palette ids, alpha 0/255, no clock, no randomness, byte-identical on a rerun.

Route: scripted (research R07/R12). No `lit_*`, no `teal_*`, no GPU. The check set per sprite is the 21.2 id allowance (exact ids), the
12-colour cap, canvas, alpha and frame count, `check_flash` at its paces and, for machine-anchored FX, the 21.5 overlay keep-out.
Family level: the flash evidence (body + accent + FX composites, four machine families, 6, 8 and 10 fps, pace cap per family), the
LINK C8 ring proof (set R only, zero `steel_*`) and the cue-over-lane contrast (P1-F3).

Coordinates: sprite pixels, y down from the top. A LINK tile is the 70 x 54 lattice cell of a row: its bottom is 15 px under the machine
pivot and its top 39 px above it, so a point `a` px above the machine pivot is tile row `39 - a` (the machine pivot is tile row 39). The trunk
lies at a = 27..31 (rows 8..12, the lattice rect of `mockup_geometry`), the branch at a = 20..26 (rows 13..19) and x = 34..36.
"""
from __future__ import annotations

import argparse
import hashlib
import json
import math
from dataclasses import dataclass, field
from pathlib import Path

from PIL import Image

from .. import checks as ck
from .. import checks_v1 as v1
from .. import pixel_pass as pp
from ..color import contrast_ratio
from ..palette import PALETTE_V1_PATH, Palette, load_palette

REPO = Path(__file__).resolve().parents[4]
ART_BIBLE = "F004-PAB v1.1.0"
BRIEF = "F006-LINK-FX-AB v1.0 (specs/006-world-first-factory-experience, task T522; Art Bible 21.2 and 21.4)"
SCRIPT_VERSION = "t522-1"
PPU = 24
FPS_PACES = (6, 8, 10)
FLASH_TARGET = 2        # F006 composite requirement (Art Bible 6.4, 21.8 item 2); the check limit stays 3
MACHINES = ("miner", "smelter", "press", "assembler")
MACHINE_DIR = REPO / "art-source" / "f004" / "machines"
MACHINE_CANVAS = (80, 64)
KEEPOUT_ROWS, KEEPOUT_COLS = (0, 33), (15, 71)
ASSEMBLER_CAP = 8       # research R12: the Assembler stays at 8 fps or lower

# Set R (Art Bible 21.2): the C1 set A_ov minus every steel_*, teal_1, lit_* and the unused resources.
SET_R = frozenset({"ink_0", "ink_1", "copper_1", "copper_2", "dim_0", "floor_0", "floor_1", "plate_1", "plate_2", "res_ore_1",
                   "res_coal", "res_gold", "res_plank", "ui_bg", "ui_panel", "ui_line", "ui_text"})
MAX_COLOURS = 12

TILE = (70, 54)
TILE_FOOTPRINT = (70, 54)
MACHINE_PIVOT_ROW = 39          # tile row of the machine pivot (15 px above the tile bottom)
TRUNK_ROWS = (8, 12)            # a = 31..27
BRANCH_ROWS = (13, 19)          # a = 26..20
SPINE_COLS = (34, 36)

_CLEAR = (0, 0, 0, 0)


class Canvas:
    """Exact-pixel canvas over palette ids."""

    def __init__(self, palette: Palette, size: tuple[int, int]):
        self.p = palette
        self.im = Image.new("RGBA", size, _CLEAR)
        self.px = self.im.load()
        self.w, self.h = size

    def put(self, x: int, y: int, cid: str) -> None:
        if 0 <= x < self.w and 0 <= y < self.h:
            self.px[x, y] = self.p.rgb(cid) + (255,)

    def opaque(self, x: int, y: int) -> bool:
        return 0 <= x < self.w and 0 <= y < self.h and self.px[x, y][3] != 0

    def outline(self, cid: str = "ink_0") -> None:
        """1 px 4-connected outline outside the opaque pixels."""
        src = [(x, y) for y in range(self.h) for x in range(self.w) if self.px[x, y][3]]
        for x, y in src:
            for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1)):
                if 0 <= x + dx < self.w and 0 <= y + dy < self.h and not self.px[x + dx, y + dy][3]:
                    self.put(x + dx, y + dy, cid)


@dataclass
class Sprite:
    sprite_id: str
    family: str                       # link | fx
    frames: list[Image.Image]
    allowed: frozenset[str]           # exact palette ids allowed by the 21.2 row
    role: str
    fps: int = 0
    loop: bool = False
    still_frame: int = 0
    machine_anchored: bool = False
    anchor: tuple[int, int] | None = None   # top-left of frame 0 on the 80 x 64 machine canvas
    applies_to: tuple[str, ...] = ()
    reduced_motion: str = ""
    pace_fps: tuple[int, ...] = ()
    footprint: tuple[int, int] | None = None
    pivot: tuple[int, int] | None = None     # px from the left and from the BOTTOM (Unity convention)
    notes: str = ""
    results: list = field(default_factory=list)

    @property
    def asset_id(self) -> str:
        return self.sprite_id


# ------------------------------------------------------------------------------------------------------------------------ LINK

def _dash_h(x: int) -> bool:
    return x % 10 in (3, 4, 5, 6)


def _dash_v(y: int) -> bool:
    return y % 4 in (1, 2)


def _trunk(c: Canvas, x0: int, x1: int, active_near: int | None = None) -> None:
    """Horizontal trunk pieces for x in [x0, x1): ink_0 edges, dim_0 body, ink_1 shade row, copper dashes in the middle row."""
    top, bot = TRUNK_ROWS
    for x in range(x0, x1):
        c.put(x, top, "ink_0")
        c.put(x, top + 1, "dim_0")
        dash = _dash_h(x)
        hot = dash and active_near is not None and abs(x - active_near) <= 13
        c.put(x, top + 2, ("copper_2" if hot else "copper_1") if dash else "dim_0")
        c.put(x, top + 3, "ink_1")
        c.put(x, bot, "ink_0")


def _branch(c: Canvas, active: bool) -> None:
    """Vertical branch x 34..36, rows 12..19: ink_0 walls, a 1 px body that opens the trunk's lower edge at row 12."""
    x0, x1 = SPINE_COLS
    for y in range(TRUNK_ROWS[1], BRANCH_ROWS[1] + 1):
        c.put(x0, y, "ink_0")
        c.put(x1, y, "ink_0")
        mid = (x0 + x1) // 2
        c.put(mid, y, ("copper_2" if active else "copper_1") if _dash_v(y) else "dim_0")


def link_trunk(p: Palette) -> Sprite:
    c = Canvas(p, TILE)
    _trunk(c, 0, TILE[0])
    return _link("f006-link-trunk", c, "static row trunk; period 10 px horizontally so tiles join; only set R tones")


def link_junction(p: Palette) -> Sprite:
    """Trunk end at the depot: the trunk meets the depot spine (x 34..36, full tile height, period 9 vertically)."""
    c = Canvas(p, TILE)
    _trunk(c, 0, SPINE_COLS[0])
    x0, x1 = SPINE_COLS
    mid = (x0 + x1) // 2
    for y in range(TILE[1]):
        c.put(x0, y, "ink_0")
        c.put(x1, y, "ink_0")
        c.put(mid, y, "copper_1" if y % 9 in (3, 4, 5) else "dim_0")
    # the trunk's ink_0 edge rows end at the spine wall; the lane body opens into the spine
    for y in (TRUNK_ROWS[0] + 1, TRUNK_ROWS[0] + 2, TRUNK_ROWS[0] + 3):
        c.put(x0, y, "dim_0")
    return _link("f006-link-junction", c, "trunk end at the depot; the spine column is the depot meeting line; only set R tones")


def link_branch(p: Palette, active: bool = False) -> Sprite:
    """Row trunk plus the branch of an occupied location (the T replaces the trunk tile at that column)."""
    c = Canvas(p, TILE)
    _trunk(c, 0, TILE[0], active_near=35 if active else None)
    x0, x1 = SPINE_COLS
    c.put((x0 + x1) // 2, TRUNK_ROWS[1], "dim_0")          # open the trunk's lower edge
    _branch(c, active)
    sid = "f006-link-branch-active" if active else "f006-link-branch"
    note = ("static running tile, the reduced-motion state of the pip (copper_2 dashes)" if active
            else "branch to an occupied location; trunk piece included")
    return _link(sid, c, note)


def link_branch_active(p: Palette) -> Sprite:
    return link_branch(p, True)


def _link(sid: str, c: Canvas, note: str) -> Sprite:
    return Sprite(sid, "link", [c.im], frozenset({"ink_0", "ink_1", "dim_0", "copper_1", "copper_2", "floor_0", "floor_1"}),
                  "lane", footprint=TILE_FOOTPRINT, pivot=(35, 27), notes=note)


# --------------------------------------------------------------------------------------------------------------------------- FX

def _ring(c: Canvas, cx: float, cy: float, hw: float, thick: int, cid: str, gap: int = 0) -> None:
    """A 2:1 diamond ring of half width `hw` as a stair-step line (one pixel per column per edge, `thick` pixels tall). With `gap` n, every
    n-th column is left out, which keeps the ring readable but halves what it changes over a body (restrained, FR-029)."""
    for x in range(c.w):
        dx = abs(x + 0.5 - cx)
        if dx > hw or (gap and x % gap == gap - 1):
            continue
        off = (hw - dx) / 2.0
        for t in range(thick):
            c.put(x, int(round(cy - off - 0.5)) + t, cid)
            c.put(x, int(round(cy + off - 0.5)) - t, cid)
    for y in range(c.h):        # close the left and right apexes
        for t in range(thick):
            for sx in (-1, 1):
                if abs(y + 0.5 - cy) <= 0.5:
                    c.put(int(round(cx + sx * hw - 0.5)) - sx * t, y, cid)


def fx_completion_pop(p: Palette) -> Sprite:
    """One-shot 2:1 ring, 68 x 36, centred on the plate centre (2.5 px above the machine pivot). Rows 0..5 are clipped so no pixel lies
    in the overlay keep-out (the ring reaches the plate's rear apex, which sits under the badge band)."""
    frames = []
    spec = ((5, 1, "steel_2"), (11, 1, "steel_2"), (18, 1, "steel_2"), (24, 1, "steel_2"), (29, 1, "steel_2"), (33, 1, "steel_2"))
    for hw, thick, cid in spec:
        c = Canvas(p, (68, 36))
        _ring(c, 34, 18, hw, thick, cid, gap=3)
        for y in range(6):
            for x in range(c.w):
                c.px[x, y] = _CLEAR
        frames.append(c.im)
    return Sprite("f006-fx-completion-pop", "fx", frames, frozenset({"steel_3", "steel_2", "ink_0"}), "completion pop", fps=12,
                  still_frame=3, machine_anchored=True, anchor=(6, 28), applies_to=MACHINES, footprint=(68, 36), pivot=(34, 18),
                  reduced_motion="static 1 s highlight (f006-fx-completion-highlight) on the card or the depot (research R12); frame 3 is the evidence-sheet still",
                  notes="one-shot, 6 frames, plays once; rows 0..5 clipped for the overlay keep-out")


def fx_completion_highlight(p: Palette) -> Sprite:
    """Static reduced-motion highlight: the pop's ring held still, no motion. Not machine-anchored (card or depot), so no clip."""
    c = Canvas(p, (68, 36))
    _ring(c, 34, 18, 33, 2, "steel_3")
    c.outline()
    return Sprite("f006-fx-completion-highlight", "fx", [c.im], frozenset({"steel_3", "ink_0"}), "reduced-motion highlight", fps=0,
                  applies_to=("depot", "card"), footprint=(68, 36), pivot=(34, 18),
                  reduced_motion="this sprite is the static state itself; held for 1 s then removed (research R12)",
                  notes="one frame, no animation; ADDITIONAL to the 21.4 table (+1 frame, 68 x 36)")


def fx_branch_pip(p: Palette) -> Sprite:
    """Loop on the branch, 14 x 10: a copper_2 core with an ink_0 ring; the tips toggle so the luminance change stays small."""
    frames = []
    for k in range(6):
        c = Canvas(p, (14, 10))
        core = [(6, 3), (7, 3), (5, 4), (6, 4), (7, 4), (8, 4), (5, 5), (6, 5), (7, 5), (8, 5), (6, 6), (7, 6)]
        tips = [(4, 4), (9, 4), (4, 5), (9, 5)]
        for x, y in core:
            c.put(x, y, "copper_2")
        grow = (0, 1, 2, 2, 1, 0)[k]
        for x, y in tips[: 2 * grow]:
            c.put(x, y, "copper_2")
        c.outline()
        frames.append(c.im)
    return Sprite("f006-fx-branch-pip", "fx", frames, frozenset({"copper_2", "ink_0"}), "branch pip", fps=12, loop=True, still_frame=0,
                  applies_to=("branch",), footprint=(14, 10), pivot=(7, 5),
                  reduced_motion="no pip is drawn; the branch shows f006-link-branch-active (research R07, UI/UX C5)",
                  notes="loop on a branch only, never on a trunk; pace of travel is set by ActivityPace, the sprite clock stays 12 fps")


def _spark_frame(p: Palette, k: int) -> Image.Image:
    c = Canvas(p, (20, 16))
    cx, cy = 10, 8
    if k == 0:
        for dx in (0, 1):
            for dy in (0, 1):
                c.put(cx + dx, cy + dy, "steel_3")
        c.outline()
    elif k == 1:
        for dx, dy in ((0, 0), (1, 0), (0, 1), (1, 1), (-1, 0), (2, 1), (0, -1), (1, 2)):
            c.put(cx + dx, cy + dy, "steel_3")
        c.outline()
    else:
        reach = (0, 0, 3, 5, 7, 8)[k]
        cols = ("", "", "copper_2", "copper_2", "copper_1", "copper_1")[k]
        for sx, sy in ((1, 0), (-1, 0), (0, 1), (0, -1), (1, 1), (-1, -1), (1, -1), (-1, 1)):
            x = cx + (1 if sx > 0 else 0) + sx * reach
            y = cy + (1 if sy > 0 else 0) + sy * (reach // 2 if sx and not sy else reach * 2 // 3)
            c.put(x, y, cols)
            if k == 2:
                c.put(x + (1 if sx > 0 else -1 if sx < 0 else 0), y, "steel_3")
        if k == 2:
            c.put(cx, cy, "steel_3")
            c.put(cx + 1, cy + 1, "steel_3")
    return c.im


def fx_spark(p: Palette) -> Sprite:
    frames = [_spark_frame(p, k) for k in range(6)]
    return Sprite("f006-fx-spark", "fx", frames, frozenset({"copper_2", "copper_1", "steel_3", "ink_0"}), "spark", fps=8, loop=True,
                  machine_anchored=True, anchor=(58, 36), applies_to=("smelter", "press"), pace_fps=FPS_PACES, footprint=(20, 16),
                  pivot=(10, 8), reduced_motion="nothing is drawn (Art Bible 21.7)",
                  notes="loop on the machine pace clock (6, 8 or 10 fps); Smelter and Press only")


def fx_steam(p: Palette) -> Sprite:
    """Loop, 24 x 32: two puffs rise and drift left in the sprite's columns 0..14 (the 21.5 keep-out leaves columns 0..14 free)."""
    frames = []
    for k in range(6):
        c = Canvas(p, (24, 32))
        for j in (0, 1):
            age = (k + 3 * j) % 6
            cy = 28 - age * 4
            cx = 12 - (age + 1) // 2 + (1 if j else 0)
            r = (1, 1, 2, 2, 2, 1)[age]
            body = "steel_3" if age < 3 else "steel_2"
            for y in range(cy - r, cy + r + 1):
                for x in range(cx - r, cx + r + 1):
                    if (x - cx) ** 2 + (y - cy) ** 2 <= r * r + (1 if r > 1 else 0) and 0 <= x <= 13:
                        c.put(x, y, body)
        c.outline()
        for y in range(c.h):
            for x in range(15, c.w):
                c.px[x, y] = _CLEAR      # the outline may have spilled one column past 14
        frames.append(c.im)
    return Sprite("f006-fx-steam", "fx", frames, frozenset({"steel_3", "steel_2", "ink_0"}), "steam", fps=8, loop=True,
                  machine_anchored=True, anchor=(0, 24), applies_to=("smelter",), pace_fps=FPS_PACES, footprint=(24, 32), pivot=(12, 0),
                  reduced_motion="nothing is drawn (Art Bible 21.7)",
                  notes="loop on the machine pace clock; Smelter only; content in columns 0..14 because the chimney tip (row 35) leaves no room above it")


def fx_build_dust(p: Palette) -> Sprite:
    """One-shot at the foundation, 48 x 28: six puffs spread along a 2:1 ellipse and fade. No outline: a restrained, low-contrast cloud."""
    frames = []
    for k in range(6):
        c = Canvas(p, (48, 28))
        rx = 6 + k * 3.6
        ry = rx / 2
        body = ("steel_2", "steel_2", "steel_1", "steel_1", "floor_1", "floor_1")[k]
        r = (1, 2, 2, 2, 1, 1)[k]
        for i in range(6):
            ang = i * math.pi / 3 + 0.35
            cx = 24 + rx * math.cos(ang)
            cy = 14 + ry * math.sin(ang)
            for y in range(int(cy) - r, int(cy) + r + 1):
                for x in range(int(cx) - r, int(cx) + r + 1):
                    if (x + 0.5 - cx) ** 2 + (y + 0.5 - cy) ** 2 <= (r + 0.3) ** 2:
                        c.put(x, y, body)
        frames.append(c.im)
    return Sprite("f006-fx-build-dust", "fx", frames, frozenset({"floor_1", "steel_1", "steel_2", "ink_0"}), "build dust", fps=12,
                  still_frame=2, machine_anchored=False, anchor=(16, 32), applies_to=("foundation",), footprint=(48, 28), pivot=(24, 14),
                  reduced_motion="nothing is drawn (Art Bible 21.7); the foundation appears at once",
                  notes="one-shot at an empty location's foundation, before a machine or badge exists; keep-out is recorded, not gated")


def fx_upgrade_glint(p: Palette) -> Sprite:
    """One-shot on the body, 24 x 24: a four-point star that grows and shrinks, at the machine's upper right shoulder. No outline."""
    frames = []
    for k in range(6):
        c = Canvas(p, (24, 24))
        arm = (1, 2, 3, 4, 3, 1)[k]
        cx, cy = 11, 11
        for d in range(-arm, arm + 1):
            c.put(cx + d, cy, "steel_3")
            c.put(cx, cy + d, "steel_3")
        if 2 <= k <= 4:
            c.put(cx, cy, "copper_2")
        frames.append(c.im)
    return Sprite("f006-fx-upgrade-glint", "fx", frames, frozenset({"steel_3", "copper_2", "ink_0"}), "upgrade glint", fps=12, still_frame=3,
                  machine_anchored=True, anchor=(47, 31), applies_to=MACHINES, footprint=(24, 24), pivot=(12, 12),
                  reduced_motion="nothing is drawn (Art Bible 21.7)", notes="one-shot on the machine's upper right shoulder, below row 34 of the machine canvas")


LINK_BUILDERS = (link_trunk, link_junction, link_branch, link_branch_active)
FX_BUILDERS = (fx_completion_pop, fx_branch_pip, fx_spark, fx_steam, fx_build_dust, fx_upgrade_glint, fx_completion_highlight)
EXPECTED_FRAMES = {"f006-fx-completion-pop": 6, "f006-fx-branch-pip": 6, "f006-fx-spark": 6, "f006-fx-steam": 6,
                   "f006-fx-build-dust": 6, "f006-fx-upgrade-glint": 6, "f006-fx-completion-highlight": 1}


def build_sprites(p: Palette) -> list[Sprite]:
    return [b(p) for b in LINK_BUILDERS + FX_BUILDERS]


# ------------------------------------------------------------------------------------------------------------------------ checks

def _ids(frames: list[Image.Image], p: Palette) -> set[str]:
    out = set()
    for f in frames:
        px = f.load()
        for y in range(f.height):
            for x in range(f.width):
                if px[x, y][3]:
                    out.add(p.id_of(px[x, y][:3]) or "unknown")
    return out


def check_ids(s: Sprite, p: Palette) -> ck.CheckResult:
    used = _ids(s.frames, p)
    forbidden = sorted(i for i in used if i not in s.allowed)
    bad_prefix = sorted(i for i in used if i.startswith(("lit_", "teal_", "cue_", "cvd_")))
    return ck.CheckResult("allowed_ids", not forbidden and not bad_prefix and len(used) <= MAX_COLOURS,
                          {"used": sorted(used), "allowed": sorted(s.allowed), "forbidden": forbidden, "reservedUsed": bad_prefix,
                           "colourCount": len(used), "colourMax": MAX_COLOURS})


def _on_floor(f: Image.Image, p: Palette) -> Image.Image:
    bg = Image.new("RGBA", f.size, p.rgb("floor_0") + (255,))
    bg.alpha_composite(f)
    return bg


def check_flash_own(s: Sprite, p: Palette) -> list[ck.CheckResult]:
    """`check_flash` on the sprite over bare floor (`floor_0`) at 6, 8 and 10 fps and at its own pace. The raw overlap-only measure of the
    bare sprite is recorded too (`overlapOnly`) but does not gate: it divides by the few pixels two sparse frames share."""
    if len(s.frames) < 2:
        return []
    paces = sorted({*FPS_PACES, s.fps})
    floor = [_on_floor(f, p) for f in s.frames]
    rows = {str(fps): ck.flash_rate(floor, fps)["maxFlashesPerSecond"] for fps in paces}
    raw = {str(fps): ck.flash_rate(s.frames, fps)["maxFlashesPerSecond"] for fps in paces}
    ok = all(v <= FLASH_TARGET for v in rows.values())
    return [ck.CheckResult("flash", ok, {"limit": ck.FLASH_MAX_PER_SECOND, "target": FLASH_TARGET, "backdrop": "floor_0",
                                         "maxFlashesPerSecondByFps": rows, "overlapOnly": raw,
                                         "transitions": ck.flash_rate(floor, 8)["transitions"]})]


def place_on_machine(s: Sprite) -> list[Image.Image]:
    out = []
    for f in s.frames:
        c = Image.new("RGBA", MACHINE_CANVAS, _CLEAR)
        c.alpha_composite(f, s.anchor)
        out.append(c)
    return out


def check_keepout(s: Sprite) -> ck.CheckResult:
    if not s.machine_anchored:
        return ck.CheckResult("overlay_keepout", True, {"applies": False, "note": "not anchored to a machine (21.5)"})
    r = v1.check_overlay_keepout(place_on_machine(s), KEEPOUT_ROWS, KEEPOUT_COLS)
    return ck.CheckResult("overlay_keepout", r.passed, {**r.details, "anchor": list(s.anchor)})


def check_sprite(s: Sprite, p: Palette) -> list[ck.CheckResult]:
    canvas = s.frames[0].size
    out = [check_ids(s, p), ck.check_canvas(s.frames, canvas), ck.check_alpha(s.frames),
           ck.check_frames(s.frames, EXPECTED_FRAMES.get(s.sprite_id, 1))]
    if s.family == "link":
        out.append(ck.CheckResult("set_r_only", _ids(s.frames, p) <= SET_R, {"ids": sorted(_ids(s.frames, p)), "setR": sorted(SET_R)}))
    else:
        out += check_flash_own(s, p)
        out.append(check_keepout(s))
    return out


# ------------------------------------------------------------------------------------------------------------- flash evidence

def _machine_frames(m: str) -> list[Image.Image]:
    body = [Image.open(MACHINE_DIR / m / "pixel" / f"f004-machine-{m}-run_{i:02d}.png").convert("RGBA") for i in range(6)]
    acc = [Image.open(MACHINE_DIR / f"{m}-accent" / "pixel" / f"f004-accent-{m}-run_{i:02d}.png").convert("RGBA") for i in range(6)]
    out = []
    for b, a in zip(body, acc):
        c = Image.new("RGBA", MACHINE_CANVAS, _CLEAR)
        c.alpha_composite(b)
        c.alpha_composite(a)
        out.append(c)
    return out


def composite(machine: list[Image.Image], fxs: list[Sprite]) -> list[Image.Image]:
    """Body + accent + the FX frames on the shared clock (frame i of every layer at once)."""
    out = []
    placed = [place_on_machine(f) if f.anchor else None for f in fxs]
    for i, m in enumerate(machine):
        c = m.copy()
        for f, pl in zip(fxs, placed):
            if pl:
                c.alpha_composite(pl[i % len(pl)])
        out.append(c)
    return out


def _flash(frames: list[Image.Image]) -> dict:
    return {str(fps): ck.flash_rate(frames, fps)["maxFlashesPerSecond"] for fps in FPS_PACES}


def foundation_standin(p: Palette) -> list[Image.Image]:
    """An empty foundation (no machine yet): a floor_1 diamond 70 x 35 on the 80 x 64 canvas, the same six frames every time."""
    c = Canvas(p, MACHINE_CANVAS)
    for y in range(c.h):
        for x in range(c.w):
            if abs(x + 0.5 - 40) / 35.0 + abs(y + 0.5 - 46.5) / 17.5 <= 1.0:
                c.put(x, y, "floor_1")
    return [c.im.copy() for _ in range(6)]


def flash_evidence(sprites: list[Sprite], palette: Palette) -> dict:
    """The T522 evidence table: body + accent + FX composites for the four machine families at 6, 8 and 10 fps.

    Method: frame i of the body, the accent and every FX layer on one clock (frames 0..5), `check_flash`'s window of `fps` transitions with the
    loop wrap counted. The one-shots (pop, glint, dust) are treated as loops too, the conservative reading. Only the effects that apply to a
    family are composited on it (spark: Smelter and Press; steam: Smelter; pop and glint: every machine). `stacks` put every effect that can
    run on the family at once (the loop effects plus each one-shot). Build dust has no machine (it plays at an empty foundation), so it is
    composited on a foundation stand-in and gates every family's cap.
    Pass: every composite at most 2 flashes per second (the F006 requirement, Art Bible 6.4 and 21.8) and so at most 3 (the check limit). The
    pace cap of a family is the highest of 6, 8 and 10 fps at which every composite of the family passes; the Assembler is also capped at 8 fps
    (research R12)."""
    fx = {s.sprite_id.replace("f006-fx-", ""): s for s in sprites
          if s.family == "fx" and s.sprite_id not in ("f006-fx-completion-highlight", "f006-fx-branch-pip")}
    loops = [fx["spark"], fx["steam"]]
    dust_flash = _flash(composite(foundation_standin(palette), [fx["build-dust"]]))
    dust_row = {"machine": "(foundation, no machine)", "fx": "build-dust", "flashes": dust_flash,
                "ok": all(v <= FLASH_TARGET for v in dust_flash.values())}
    rows, stacks, caps = [], [], {}
    for m in MACHINES:
        base = _machine_frames(m)
        base_flash = _flash(base)
        rows.append({"machine": m, "fx": "(none)", "flashes": base_flash, "ok": all(v <= FLASH_TARGET for v in base_flash.values())})
        fam_rows = [rows[-1], dust_row]
        for name in ("completion-pop", "spark", "steam", "upgrade-glint"):
            f = fx[name]
            if m not in f.applies_to:
                continue
            fl = _flash(composite(base, [f]))
            rows.append({"machine": m, "fx": name, "flashes": fl, "ok": all(v <= FLASH_TARGET for v in fl.values())})
            fam_rows.append(rows[-1])
        run = [f for f in loops if m in f.applies_to]
        for oneshot in ("completion-pop", "upgrade-glint"):
            combo = run + [fx[oneshot]]
            if len(combo) < 2:
                continue            # a stack of one effect is the single row above
            fl = _flash(composite(base, combo))
            stacks.append({"machine": m, "fx": "+".join(f.sprite_id.replace("f006-fx-", "") for f in combo), "flashes": fl,
                           "ok": all(v <= FLASH_TARGET for v in fl.values())})
            fam_rows.append(stacks[-1])
        cap = None
        for fps in FPS_PACES:
            if all(r["flashes"][str(fps)] <= FLASH_TARGET for r in fam_rows):
                cap = fps
        caps[m] = {"paceCapFps": min(cap, ASSEMBLER_CAP) if (m == "assembler" and cap is not None) else cap, "baseline": base_flash,
                   "compositesChecked": len(fam_rows), "capBeforeAssemblerRule": cap}
    rows.append(dust_row)
    passed = all(c["paceCapFps"] is not None for c in caps.values())
    max_flash = max(v for r in rows + stacks for v in r["flashes"].values())
    assembler_ok = caps["assembler"]["paceCapFps"] is not None and caps["assembler"]["paceCapFps"] <= ASSEMBLER_CAP
    return {"method": "check_flash on body + accent + FX frame i, frames 0..5 on one clock, window = fps transitions, loop wrap counted",
            "pacesFps": list(FPS_PACES), "targetPerSecond": FLASH_TARGET, "limitPerSecond": ck.FLASH_MAX_PER_SECOND,
            "rows": rows, "stacks": stacks, "paceCaps": caps, "worstFlashesPerSecond": max_flash,
            "everyCompositeWithinLimit": max_flash <= ck.FLASH_MAX_PER_SECOND, "assemblerCapAtMost8": assembler_ok,
            "passed": passed and assembler_ok and max_flash <= ck.FLASH_MAX_PER_SECOND}


# ------------------------------------------------------------------------------------------------------------------- LINK proofs

def _link_layer(inp, lat, fr, sprites: dict[str, Sprite]) -> Image.Image:
    """The LINK layer of one lattice at n = 1: trunk tiles along each row up to the depot column (junction there), branch tiles at the
    occupied locations; the tile of a cell is centred on the cell pivot."""
    layer = Image.new("RGBA", (fr.w, fr.h), _CLEAR)
    cells = {(c.row, c.column): c for c in lat.cells}
    occupied = {(c.row, c.column) for c in lat.locations() if c.state == "occupied"}
    dcol = lat.depot.column
    side = lat.params.depot_side_of(lat.orientation)
    for row in range(lat.rows):
        cols_in_row = [c for r, c in lat.layout.cells if r == row]
        lo, hi = (min(cols_in_row), dcol) if side == "right" else (dcol, max(cols_in_row))
        for col in range(lo, hi + 1):
            cell = cells.get((row, col)) or next(c for c in lat.cells if (c.row, c.column) == (row, col))
            name = "f006-link-junction" if col == dcol else ("f006-link-branch" if (row, col) in occupied else "f006-link-trunk")
            img = sprites[name].frames[0]
            left = fr.tf.x0 + cell.pivot[0] - 35
            top = fr.tf.y0 - cell.pivot[1] - (TILE[1] - 15)
            layer.alpha_composite(img, (left, top))
    return layer


def ring_proof(sprites: list[Sprite], palette: Palette) -> ck.CheckResult:
    """C8 for the LINK layer: on the densest frame of every layout (all slots occupied, every badge and both markers drawn), the 1 px ring
    outside every badge is sampled on the LINK layer alone; every ring pixel that lies on a lane must be in set R, and zero may be `steel_*`.
    Run for the baseline badge (K0) and the OD-12 badge with the 4 px tail (K4). Other layers are other families' proof (ground, edge, depot)."""
    from . import a03_anchor as a3
    from ..mockup_geometry import TEST_LAYOUTS, build_lattice, consts_of
    inp = a3.load_inputs()
    by_id = {s.sprite_id: s for s in sprites}
    cands = {c.cid: c for c in a3.CANDIDATES if c.cid in ("K0", "K4")}
    rows, ok = [], True
    for name, layout in TEST_LAYOUTS.items():
        lat = build_lattice(layout, consts_of(inp.geo), inp.direction.params, None)
        fr = a3.Frame(lat)
        layer = _link_layer(inp, lat, fr, by_id)
        lp = layer.load()
        locs = sorted(lat.locations(), key=lambda s: (s.row, s.column))
        for cid, cand in cands.items():
            badges = {(s.row, s.column): a3.badge_mask(fr, inp, cand, a3._kind_of(i), s.pivot) for i, s in enumerate(locs)}
            markers = {(s.row, s.column): a3.marker_masks(fr, inp, s.pivot) for s in locs}
            overlay: set = set()
            for k in badges:
                overlay |= badges[k] | markers[k][0] | markers[k][1]
            tones: dict[str, int] = {}
            ring_px = on_lane = 0
            for b in badges.values():
                for q in a3._ring(b):
                    if q in overlay or not (0 <= q[0] < layer.width and 0 <= q[1] < layer.height):
                        continue
                    ring_px += 1
                    px = lp[q[0], q[1]]
                    if px[3]:
                        on_lane += 1
                        tid = palette.id_of(px[:3]) or "unknown"
                        tones[tid] = tones.get(tid, 0) + 1
            under_badge = sum(1 for b in badges.values() for q in b if 0 <= q[0] < layer.width and 0 <= q[1] < layer.height and lp[q[0], q[1]][3])
            bad = {t: n for t, n in tones.items() if t not in SET_R}
            steel = sum(n for t, n in tones.items() if t.startswith("steel_"))
            passed = not bad and steel == 0
            ok &= passed
            rows.append({"layout": name, "badge": cid, "slots": len(locs), "ringPx": ring_px, "ringPxOnLane": on_lane,
                         "ringTonesOnLane": dict(sorted(tones.items())), "ringPxNotInSetR": sum(bad.values()), "ringSteelPx": steel,
                         "laneUnderBadgePx": under_badge, "passed": passed})
    return ck.CheckResult("c8_ring_link_layer", ok, {"rule": "C8 and Art Bible 21.8 items 1 and 3: set R only, zero steel_* in any ring",
                                                     "layouts": list(TEST_LAYOUTS), "rows": rows})


def cue_over_lane_contrast(palette: Palette) -> ck.CheckResult:
    """P1-F3: badges and markers lie over their own trunk and branch (lanes draw below the overlay layer). For every F004 badge and marker
    sprite, the composite-mask outline share of section 9.3 (ink OR rim reaching 3:1) against each tone a lane uses, measured on a uniform
    backdrop of that tone. A tone under 90 % is reported (`failingTones`); it is recorded evidence, not a LINK gate, because the cue sprite
    and its rule C5 belong to F004 (the badge keeps its own ink_0 outline and fill)."""
    from ..cues_production import uniform_backdrop
    tones = ("dim_0", "ink_1", "ink_0", "copper_1", "copper_2")
    root = REPO / "art-source" / "f004"
    files = sorted((root / "cues").glob("f003-badge-*/f003-badge-*_00.png")) + sorted((root / "markers").glob("f003-marker-*/f003-marker-*_00.png"))
    table, failing = {}, {}
    for f in files:
        img = Image.open(f).convert("RGBA")
        row = {}
        for t in tones:
            r = v1.outline_contrast_shares(img, uniform_backdrop(t, palette), palette).get(t)
            row[t] = round(r["share"], 4) if r else None
            if r and r["share"] < v1.SHARE_THRESHOLD:
                failing.setdefault(t, []).append(f.parent.name)
        table[f.parent.name] = row
    lane_pairs = {f"{a}|{b}": round(contrast_ratio(palette.rgb(a), palette.rgb(b)), 2)
                  for a, b in (("dim_0", "floor_0"), ("dim_0", "floor_1"), ("ink_0", "dim_0"), ("copper_1", "dim_0"), ("copper_2", "dim_0"),
                               ("copper_1", "floor_0"), ("copper_2", "floor_0"))}
    return ck.CheckResult("cue_over_lane_contrast", not failing, {
        "threshold": v1.SHARE_THRESHOLD, "tones": list(tones), "sharesByCue": table, "failingTones": {k: sorted(set(v)) for k, v in failing.items()},
        "laneContrastRatios": lane_pairs, "note": "P1-F3 evidence; a failing tone is a finding for the Art Director, see the T522 report"})


# --------------------------------------------------------------------------------------------------------------------- provenance

_SRC_FILES = ("f006/link_fx.py", "checks.py", "checks_v1.py", "pixel_pass.py")


def source_hashes() -> dict[str, str]:
    base = Path(__file__).resolve().parents[1]
    return {n: hashlib.sha256((base / n).read_bytes().replace(b"\r\n", b"\n")).hexdigest() for n in _SRC_FILES}


def script_sha256() -> str:
    h = hashlib.sha256()
    for name, digest in sorted(source_hashes().items()):
        h.update(name.encode() + b":" + digest.encode() + b"\n")
    return h.hexdigest()


def _provenance(s: Sprite, p: Palette, outputs: list[dict], report_hash: str) -> dict:
    return {
        "assetId": s.asset_id, "revision": 1, "route": "scripted",
        "sources": [{"path": "tools/art-pipeline/art_pipeline/%s" % n, "sha256": h} for n, h in sorted(source_hashes().items())],
        "tools": [{"name": "art_pipeline.f006.link_fx", "version": SCRIPT_VERSION},
                  {"name": "Pillow", "version": "pinned in tools/art-pipeline/pyproject.toml"},
                  {"name": "Python", "version": ">=3.12,<3.14"}],
        "parameters": {"scriptVersion": SCRIPT_VERSION, "family": s.family, "role": s.role, "canvas": list(s.frames[0].size),
                       "frames": len(s.frames), "fps": s.fps, "loop": s.loop, "allowedIds": sorted(s.allowed), "seed": None,
                       "randomness": "none", "brief": BRIEF},
        "touchUp": [],
        "pixelPass": {"steps": ["exact-pixel drawing from palette ids (no antialiasing, alpha 0/255)", "1 px ink_0 outline where the sprite has one",
                                "no quantisation needed (exact ids)", "allowed-id, canvas, alpha, frame-count, flash and keep-out checks"],
                      "scriptSha256": script_sha256(), "paletteId": p.palette_id, "paletteVersion": p.version},
        "outputs": outputs,
        "license": "authored: project-owned scripted drawing, no third-party material",
        "trainingDataNote": "not applicable: no generative model, LoRA or training data is used on the scripted route",
        "producedBy": "Asset Artist (scripted route, Art Director lens), MAQA T522 (#695)",
        "reviewedBy": {}, "candidateSelection": "none: a single deterministic render, no candidates, no seed",
        "artBibleVersion": ART_BIBLE, "briefVersion": BRIEF, "checkReportHash": report_hash,
    }


def _entry(s: Sprite, p: Palette, prov: dict) -> dict:
    cw, ch = s.frames[0].size
    fp = s.footprint or (cw, ch)
    px, py = s.pivot or (cw // 2, ch // 2)
    placement = pp.Placement((cw, ch), fp, pp.footprint_rect((cw, ch), fp), (round(px / cw, 6), round(py / ch, 6)), (0, 0))
    gate = dict(prov, reviewedBy="pending: independent Art Director review")
    e = pp.build_manifest_entry(s.asset_id, "scripted", s.frames, placement, p, PPU, s.fps, gate, frame_dir=s.asset_id)
    e.update(family="F006-LINK" if s.family == "link" else "F006-FX", kind=s.role, atlas="world" if s.family == "link" else "fx",
             loop=s.loop, stillFrame=s.still_frame, pivotPx=[px, py], pivotConvention="px from the left and from the bottom of the canvas",
             appliesTo=list(s.applies_to), note=s.notes)
    if s.family == "link":
        e["lattice"] = {"machinePivotRowFromTop": MACHINE_PIVOT_ROW, "trunkRows": list(TRUNK_ROWS), "branchRows": list(BRANCH_ROWS),
                        "spineColumns": list(SPINE_COLS), "rule": "tile centred on the cell pivot; a = 39 - row above the machine pivot"}
    else:
        e["reducedMotion"] = s.reduced_motion
        if s.pace_fps:
            e["paceFps"] = list(s.pace_fps)
        if s.anchor:
            e["machineAnchorPx"] = {"topLeftOn80x64": list(s.anchor), "keepOutRows": list(KEEPOUT_ROWS), "keepOutCols": list(KEEPOUT_COLS),
                                    "proposal": "Technical Art confirms the anchor per machine in Unity (P3)"}
    return e


# --------------------------------------------------------------------------------------------------------------------- build / write

def build_all(palette: Palette | None = None) -> dict:
    p = palette or load_palette(PALETTE_V1_PATH)
    sprites = build_sprites(p)
    twins = {s.sprite_id: s for s in build_sprites(p)}
    for s in sprites:
        s.results = check_sprite(s, p)
        s.results.append(ck.CheckResult("reproduction", all(ck.reproduction_diff(a, b) == 0.0 for a, b in zip(s.frames, twins[s.sprite_id].frames)),
                                        {"note": "an independent second render is pixel-identical"}))
    link = [s for s in sprites if s.family == "link"]
    fxs = [s for s in sprites if s.family == "fx"]
    evidence = flash_evidence(sprites, p)
    glob_link = [ck.check_palette_definition(p), ring_proof(sprites, p), cue_over_lane_contrast(p),
                 ck.CheckResult("inventory", [s.sprite_id for s in link] == ["f006-link-trunk", "f006-link-junction", "f006-link-branch",
                                                                             "f006-link-branch-active"], {"tiles": len(link)})]
    glob_fx = [ck.check_palette_definition(p), ck.CheckResult("flash_composite_matrix", evidence["passed"], evidence),
               ck.CheckResult("inventory", sum(len(s.frames) for s in fxs if s.sprite_id != "f006-fx-completion-highlight") == 36 and len(fxs) == 7,
                              {"frames": sum(len(s.frames) for s in fxs), "table": "21.4 FX atlas 42 frames = these 36 (six sprites of 6) + the vein chip (6, deposit task); plus 1 static highlight, an addition"})]
    return {"palette": p, "link": link, "fx": fxs, "evidence": evidence,
            "report_link": ck.build_report({s.sprite_id: s.results for s in link}, glob_link),
            "report_fx": ck.build_report({s.sprite_id: s.results for s in fxs}, glob_fx)}


def _write(path: Path, data: bytes) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_bytes(data)


def _contact_sheet(sprites: list[Sprite], scale: int, bg=(54, 60, 74)) -> Image.Image:
    cell_w = max(f.width for s in sprites for f in s.frames) * scale + 6
    cell_h = max(f.height for s in sprites for f in s.frames) * scale + 6
    cols = max(len(s.frames) for s in sprites)
    sheet = Image.new("RGB", (cols * cell_w, len(sprites) * cell_h), bg)
    for r, s in enumerate(sprites):
        for k, f in enumerate(s.frames):
            big = f.resize((f.width * scale, f.height * scale), Image.Resampling.NEAREST)
            sheet.paste(big, (k * cell_w + 3, r * cell_h + 3), big)
    return sheet


def _composite_sheet(sprites: list[Sprite], scale: int = 3) -> Image.Image:
    """Body + accent + all FX of a family (stack) for frames 0..5, one row per machine, on the plain floor colour."""
    by = {s.sprite_id.replace("f006-fx-", ""): s for s in sprites}
    rows = []
    for m in MACHINES:
        base = _machine_frames(m)
        fxs = [by[n] for n in ("spark", "steam", "completion-pop", "upgrade-glint") if (m in by[n].applies_to)]
        comp = composite(base, fxs)
        bg = Image.new("RGBA", MACHINE_CANVAS, (42, 47, 58, 255))
        rows.append([Image.alpha_composite(bg, f) for f in comp])
    w, h = MACHINE_CANVAS
    sheet = Image.new("RGB", (6 * w * scale, len(rows) * h * scale), (0, 0, 0))
    for r, row in enumerate(rows):
        for k, f in enumerate(row):
            sheet.paste(f.convert("RGB").resize((w * scale, h * scale), Image.Resampling.NEAREST), (k * w * scale, r * h * scale))
    return sheet


def write_all(out_root: Path | str, palette: Palette | None = None) -> dict:
    out_root = Path(out_root)
    b = build_all(palette)
    p = b["palette"]
    for fam, sprites, report in (("link", b["link"], b["report_link"]), ("fx", b["fx"], b["report_fx"])):
        fam_dir = out_root / fam
        text = ck.report_json(report)
        _write(fam_dir / "check-report.json", text.encode("utf-8"))
        rhash = pp.sha256_hex(text.encode("utf-8"))
        entries = []
        for s in sprites:
            outputs = []
            for k, f in enumerate(s.frames):
                data = pp.png_bytes(f)
                rel = "%s/%s_%02d.png" % (s.asset_id, s.asset_id, k)
                _write(fam_dir / rel, data)
                outputs.append({"path": "art-source/f006/%s/%s" % (fam, rel), "sha256": pp.sha256_hex(data), "pixelHash": pp.pixel_hash(f)})
            prov = _provenance(s, p, outputs, rhash)
            _write(fam_dir / s.asset_id / "provenance.json", (json.dumps(prov, indent=2, sort_keys=True) + "\n").encode("utf-8"))
            entries.append(_entry(s, p, prov))
        _write(fam_dir / "manifest.json", pp.manifest_json(pp.build_manifest(entries)).encode("utf-8"))
        scale = 4 if fam == "fx" else 3
        (fam_dir / "sheets").mkdir(parents=True, exist_ok=True)
        _contact_sheet(sprites, scale).save(fam_dir / "sheets" / ("contact_x%d.png" % scale), format="PNG", compress_level=9, optimize=False)
    _composite_sheet(b["fx"]).save(out_root / "fx" / "sheets" / "composites_x3.png", format="PNG", compress_level=9, optimize=False)
    return b


def main(argv: list[str] | None = None) -> int:
    ap = argparse.ArgumentParser(prog="art_pipeline.f006.link_fx")
    ap.add_argument("--out", required=True, help="art-source/f006 directory to write (link/ and fx/ below it)")
    args = ap.parse_args(argv)
    b = write_all(args.out)
    ok = b["report_link"]["passed"] and b["report_fx"]["passed"]
    print("f006 link/fx: %s" % ("all checks pass" if ok else "FAILING"))
    return 0 if ok else 1


if __name__ == "__main__":
    raise SystemExit(main())
