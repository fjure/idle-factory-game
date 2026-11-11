"""F006 T506: scripted concept sprites for the two P0 visual directions (research R03).

Scripted route only (no ComfyUI, no Blender). `build_all` is a pure function of (palette, this file): no clock, no
randomness, byte-identical on a rerun. Every pixel is an exact Art Bible v1.0.4 palette colour (alpha 0/255).

Directions
  a  Open platform deck: a flat deck on a dark base, railing edge, open depot.
  b  Walled hall: the same deck inside a low wall, bunker depot.

Geometry is the F004 lattice: pitch 70 x 54 art px at PPU 24, plate diamond 70 x 35 (2:1). Deck, hatch and edge
tiles are 70 x 54 period tiles whose diamond centre is the tile centre (35, 27) unless the manifest says otherwise.
"""
from __future__ import annotations

import hashlib
import json
from dataclasses import dataclass
from pathlib import Path

from PIL import Image

from . import checks as ck
from . import pixel_pass as pp
from .palette import PALETTE_V1_PATH, Palette, load_palette

ART_BIBLE = "F004-PAB v1.0.4"
WORLD_BIBLE = "F006-WB v0.1"
BRIEF_VERSION = "F006-DB v0.1"
PPU = 24
PITCH = (70, 54)
FPS = 12
ALLOWED_GROUPS = {"ink", "machine", "floor", "resources", "state_cues"}

_Pix = tuple[int, int, int, int]
_CLEAR: _Pix = (0, 0, 0, 0)


class Canvas:
    """Tiny exact-pixel canvas over palette ids."""

    def __init__(self, palette: Palette, size: tuple[int, int]):
        self.p = palette
        self.im = Image.new("RGBA", size, _CLEAR)
        self.px = self.im.load()
        self.w, self.h = size

    def put(self, x: int, y: int, cid: str) -> None:
        if 0 <= x < self.w and 0 <= y < self.h:
            self.px[x, y] = self.p.rgb(cid) + (255,)

    def rect(self, x0: int, y0: int, x1: int, y1: int, cid: str) -> None:
        for y in range(y0, y1):
            for x in range(x0, x1):
                self.put(x, y, cid)

    def diamond(self, cx: float, cy: float, w: int, h: int, cid: str) -> None:
        for y in range(self.h):
            for x in range(self.w):
                if abs(x + 0.5 - cx) / (w / 2) + abs(y + 0.5 - cy) / (h / 2) <= 1.0:
                    self.put(x, y, cid)

    def ring(self, cx: float, cy: float, w: int, h: int, thick: float, cid: str) -> None:
        for y in range(self.h):
            for x in range(self.w):
                d = abs(x + 0.5 - cx) / (w / 2) + abs(y + 0.5 - cy) / (h / 2)
                if 1.0 - thick / (w / 2) <= d <= 1.0:
                    self.put(x, y, cid)

    def box(self, cx: int, by: int, hw: int, height: int, top: str, left: str, right: str) -> None:
        """Iso box: base diamond centre (cx, by), half width hw (diamond hw*2 x hw), vertical extent `height`."""
        cy = by - height
        for x in range(cx - hw, cx + hw):
            th = (hw - abs(x + 0.5 - cx)) / 2.0
            for y in range(int(cy - th), int(by + th) + 1):
                if y + 0.5 < cy - th:
                    continue
                if y + 0.5 <= cy + th:
                    self.put(x, y, top)
                elif y + 0.5 <= by + th:
                    self.put(x, y, left if x + 0.5 < cx else right)

    def outline(self) -> None:
        """1 px ink_0 outline, 4-connected, drawn inside the canvas (canvas has a transparent margin)."""
        ink = self.p.rgb("ink_0") + (255,)
        src = self.im.copy().load()
        for y in range(self.h):
            for x in range(self.w):
                if src[x, y][3]:
                    continue
                for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1)):
                    nx, ny = x + dx, y + dy
                    if 0 <= nx < self.w and 0 <= ny < self.h and src[nx, ny][3]:
                        self.px[x, y] = ink
                        break


@dataclass
class Sprite:
    sprite_id: str
    kind: str
    frames: list[Image.Image]
    pivot: tuple[int, int]          # px from the top-left of the canvas
    footprint: tuple[int, int]      # art px of the ground footprint the sprite stands on
    note: str
    groups: set[str]
    fps: int = 0
    still_frame: int = 0


# --- shared drawing -------------------------------------------------------------------------------------------

def _deck_surface(c: Canvas, cx: float, cy: float, style: str) -> None:
    """Deck diamond 70 x 35 on a dark base. Direction a: clean slab with a lit rim. b: slab with panel seams."""
    c.diamond(cx, cy, 70, 35, "floor_1")
    for y in range(c.h):
        for x in range(c.w):
            d = abs(x + 0.5 - cx) / 35.0 + abs(y + 0.5 - cy) / 17.5
            if 0.94 <= d <= 1.0 and y + 0.5 < cy:
                c.put(x, y, "floor_2")            # 1 px highlight on the two back edges only (Art Bible: floor_2 highlights)
    if style == "b":
        for y in range(c.h):
            for x in range(c.w):
                dx, dy = abs(x + 0.5 - cx), abs(y + 0.5 - cy)
                if dx / 35.0 + dy / 17.5 <= 0.94 and (abs(dx - 2 * dy) < 1.0 and (int(dx) % 2 == 0)):
                    c.put(x, y, "floor_0")        # dashed seam from the centre to the side apexes


def _base(palette: Palette, style: str) -> Canvas:
    c = Canvas(palette, PITCH)
    c.rect(0, 0, c.w, c.h, "floor_0")             # the dark base (region `starter-platform`)
    return c


def deck_tile(palette: Palette, style: str) -> Sprite:
    c = _base(palette, style)
    _deck_surface(c, 35, 27, style)
    # two ground rivets in the visible field beside the diamond (same period as the F004 floor tile)
    for (x, y) in ((6, 6), (63, 47)):
        c.put(x, y, "floor_1")
    return Sprite("deck-tile", "tile", [c.im], (35, 27), PITCH, "Period tile, opaque; diamond centre = tile centre",
                  {"floor"})


def hatch_tile(palette: Palette, style: str) -> Sprite:
    c = _base(palette, style)
    _deck_surface(c, 35, 27, style)
    c.diamond(35, 27, 34, 17, "steel_0")          # hatch frame
    c.diamond(35, 27, 28, 14, "dim_0")            # hatch leaf
    for y in range(c.h):                          # grate slats along the 2:1 direction
        for x in range(c.w):
            dx, dy = x + 0.5 - 35, y + 0.5 - 27
            if abs(dx) / 14 + abs(dy) / 7 <= 1.0 and int(dx + 2 * dy + 100) % 5 == 0:
                c.put(x, y, "ink_1")
    return Sprite("hatch-tile", "tile", [c.im], (35, 27), PITCH,
                  "Period tile, opaque; a hatch over a location that is not built yet", {"floor", "machine", "ink"})


def edge_tile(palette: Palette, style: str) -> Sprite:
    """Deck edge, transparent outside the deck: the diamond with its two front faces, plus railing (a) or back wall (b)."""
    c = Canvas(palette, (70, 66))
    cx, cy, th = 35, 44, 12
    c.box(cx, cy + 0, 35, th, "floor_1", "floor_0", "ink_1")
    # the box() base diamond is 70 wide; redraw top highlight on back edges
    for y in range(c.h):
        for x in range(c.w):
            d = abs(x + 0.5 - cx) / 35.0 + abs(y + 0.5 - (cy - th)) / 17.5
            if 0.94 <= d <= 1.0 and y + 0.5 < cy - th:
                c.put(x, y, "floor_2")
    if style == "a":
        # railing on the two front top edges: posts and a top rail (steel), open between posts
        for k in range(0, 36, 7):
            for side in (-1, 1):
                x = cx + side * k - (1 if side < 0 else 0)
                y = int(cy - th + (35 - k) / 2.0 + 0.5)
                c.rect(x, y - 8, x + 1, y, "steel_2")
                c.put(x, y - 9, "steel_3")
        for k in range(0, 36):
            for side in (-1, 1):
                x = cx + side * k - (1 if side < 0 else 0)
                c.put(x, int(cy - th + (35 - k) / 2.0 + 0.5) - 8, "steel_1")
    else:
        # low wall on the two back edges (a walled yard): wall top steel_2, face steel_1 / steel_0
        wall = 12
        for k in range(0, 35):
            for side in (-1, 1):
                x = cx + side * k - (1 if side < 0 else 0)
                yb = int(cy - th - (35 - k) / 2.0 + 0.5)      # back edge of the deck diamond at column x
                c.rect(x, yb - wall, x + 1, yb, "steel_1" if side < 0 else "steel_0")
                c.put(x, yb - wall, "steel_2")
    c.outline()
    return Sprite("edge-tile", "tile", [c.im], (35, cy - th), (70, 35),
                  "Deck edge piece; pivot = diamond centre; railing on the front edges (a) or back wall (b)",
                  {"floor", "machine", "ink"})


def depot(palette: Palette, style: str) -> Sprite:
    c = Canvas(palette, (80, 72))
    cx, by = 40, 52
    c.box(cx, by, 28, 4, "plate_2", "plate_0", "plate_1")                 # foundation slab 56 x 28
    if style == "a":
        c.box(cx, by - 4, 12, 12, "steel_2", "steel_0", "steel_1")        # open hopper block
        c.box(cx, by - 16, 9, 6, "copper_2", "copper_0", "copper_1")      # intake collar
        for dx in (-22, 22):                                              # two corner posts
            c.rect(cx + dx - 1, by - 22, cx + dx + 1, by - 6, "steel_1")
        c.rect(cx - 23, by - 23, cx + 23, by - 21, "steel_2")             # overhead beam
        c.rect(cx - 1, by - 28, cx + 1, by - 22, "lit_0")                 # work lamp
    else:
        c.box(cx, by - 4, 22, 22, "steel_1", "steel_0", "dim_1")          # bunker block
        c.box(cx, by - 26, 18, 2, "steel_2", "steel_1", "steel_0")        # roof lip
        c.diamond(cx + 8, by - 8, 12, 6, "teal_0")                        # hatch door on the right face
        c.rect(cx - 1, by - 32, cx + 1, by - 26, "lit_0")
    c.outline()
    return Sprite("depot", "structure", [c.im], (40, by - 2), (56, 28),
                  "Central depot; pivot = slab centre on the ground; footprint 56 x 28 diamond", ALLOWED_GROUPS)


def vein(palette: Palette, style: str) -> Sprite:
    c = Canvas(palette, (48, 34))
    c.diamond(24, 24, 40, 14, "floor_1")                                  # scarred ground patch
    for (dx, by, hw, h) in ((-9, 22, 7, 5), (6, 24, 8, 8), (-1, 19, 6, 11)):
        c.box(24 + dx, by, hw, h, "res_ore_1", "res_ore_0", "res_coal")
    for (x, y) in ((14, 26), (34, 25), (24, 12), (20, 15)):
        c.put(x, y, "res_ore_1")
    c.outline()
    return Sprite("vein", "deposit", [c.im], (24, 24), (40, 14),
                  "One ore vein, scenery only (R23); pivot = patch centre", {"ink", "floor", "resources"})


def prop_crate(palette: Palette, style: str) -> Sprite:
    c = Canvas(palette, (30, 36))
    c.box(15, 30, 10, 8, "steel_2", "steel_0", "steel_1")
    c.box(15, 22, 7, 7, "copper_2", "copper_0", "copper_1")
    c.outline()
    return Sprite("prop-crate", "prop", [c.im], (15, 29), (20, 10), "Crate stack; pivot = base centre",
                  {"ink", "machine"})


def prop_second(palette: Palette, style: str) -> Sprite:
    c = Canvas(palette, (24, 44))
    if style == "a":                                                      # work lamp post
        c.box(12, 38, 6, 2, "steel_1", "steel_0", "steel_0")
        c.rect(11, 14, 13, 37, "steel_2")
        c.rect(8, 10, 16, 14, "steel_1")
        c.rect(9, 11, 15, 13, "lit_0")
        prop = "prop-lamp"
    else:                                                                 # drum
        c.box(12, 36, 8, 2, "steel_1", "steel_0", "steel_0")
        c.rect(5, 22, 19, 36, "teal_0")
        c.diamond(12, 22, 14, 7, "teal_1")
        c.rect(5, 27, 19, 29, "steel_1")
        prop = "prop-drum"
    c.outline()
    s = Sprite(prop, "prop", [c.im], (12, 37), (12, 6), "Second prop; pivot = base centre", {"ink", "machine"})
    return s


def trunk_branch(palette: Palette, style: str) -> Sprite:
    """Static trunk lane meeting a branch: lanes along both 2:1 diagonals meeting at the tile centre."""
    c = Canvas(palette, PITCH)
    for x in range(c.w):
        for (sign, near_lane) in ((1, True), (-1, False)):
            ly = 27 + sign * (x + 0.5 - 35) / 2.0
            if not near_lane and x < 35:
                continue                                                   # the branch leaves to the right only
            for dy in range(-3, 4):
                y = int(ly + dy)
                c.put(x, y, "dim_1" if abs(dy) <= 1 else "dim_0")
            if x % 8 < 3:
                c.put(x, int(ly), "steel_2")
    c.outline()
    return Sprite("trunk-branch", "lane", [c.im], (35, 27), PITCH,
                  "Trunk with one branch (R07); static lane; pivot = junction", {"ink", "machine"})


def branch_pip(palette: Palette, style: str) -> Sprite:
    frames = []
    for r in (4, 4, 5, 5, 5, 4):                  # fixed core, the halo grows by one ring: small luminance change
        c = Canvas(palette, (14, 10))
        c.diamond(7, 5, 2 * r + 2, r + 1, "teal_0")
        c.diamond(7, 5, 8, 4, "teal_1")
        c.put(6, 5, "lit_1")
        c.put(7, 5, "lit_1")
        c.outline()
        frames.append(c.im)
    return Sprite("branch-pip", "fx", frames, (7, 5), (14, 10), "Branch pip; 6 frames, loop, size pulse", {"ink", "machine"},
                  fps=FPS, still_frame=3)


def completion_pop(palette: Palette, style: str) -> Sprite:
    frames = []
    spec = ((8, 4, 2.5, "lit_1"), (20, 10, 3.0, "lit_0"), (34, 17, 3.0, "lit_0"), (46, 23, 2.5, "cue_ok"),
            (56, 28, 1.5, "cue_ok"), (62, 31, 1.0, "cue_dim"))
    for (w, h, t, cid) in spec:
        c = Canvas(palette, (68, 36))
        c.ring(34, 18, w, h, t, cid)
        frames.append(c.im)
    return Sprite("completion-pop", "fx", frames, (34, 18), (70, 35), "Completion pop; 6 frames, once; reduced-motion = frame 3",
                  {"machine", "state_cues"}, fps=FPS, still_frame=3)


BUILDERS = (deck_tile, hatch_tile, edge_tile, depot, vein, prop_crate, prop_second, trunk_branch, branch_pip,
            completion_pop)
DIRECTIONS = {"a": "Open platform deck", "b": "Walled hall"}


def build_direction(palette: Palette, style: str) -> list[Sprite]:
    return [b(palette, style) for b in BUILDERS]


# --- checks and writing ---------------------------------------------------------------------------------------

def check_sprite(s: Sprite, palette: Palette) -> list[ck.CheckResult]:
    canvas = s.frames[0].size
    out = [ck.check_palette(s.frames, palette, allowed_groups=s.groups), ck.check_canvas(s.frames, canvas),
           ck.check_alpha(s.frames), ck.check_frames(s.frames, len(s.frames))]
    if len(s.frames) > 1:
        out.append(ck.check_flash(s.frames, s.fps))
    return out


def _sha(path: Path) -> str:
    return hashlib.sha256(path.read_bytes().replace(b"\r\n", b"\n")).hexdigest()


def write_direction(out_dir: Path, key: str, palette: Palette) -> dict:
    sprites = build_direction(palette, key)
    pix = out_dir / "pixel"
    pix.mkdir(parents=True, exist_ok=True)
    entries, prov_assets, report = [], [], {}
    me = Path(__file__)
    for s in sprites:
        files = []
        for i, f in enumerate(s.frames):
            name = f"f006-{key}-{s.sprite_id}_{i:02d}.png"
            f.save(pix / name, optimize=False)
            files.append({"file": f"pixel/{name}", "sha256": hashlib.sha256((pix / name).read_bytes()).hexdigest()})
        results = check_sprite(s, palette)
        report[s.sprite_id] = [{"name": r.name, "passed": r.passed, "details": r.details} for r in results]
        entries.append({
            "id": f"f006-{key}-{s.sprite_id}", "kind": s.kind, "size": list(s.frames[0].size), "ppu": PPU,
            "pivotPx": list(s.pivot), "footprintPx": list(s.footprint), "fps": s.fps, "frameCount": len(s.frames),
            "stillFrame": s.still_frame, "frames": files, "note": s.note})
        prov_assets.append({"id": f"f006-{key}-{s.sprite_id}", "outputs": files})
    manifest = {"schemaVersion": 1, "direction": key, "directionName": DIRECTIONS[key], "worldBible": WORLD_BIBLE,
                "artBible": ART_BIBLE, "palette": {"id": palette.palette_id, "version": palette.version},
                "perspective": "isometric-2-1", "latticePitchPx": list(PITCH), "pivotConvention":
                "pivotPx = (x, y) in px from the canvas top-left; ground anchor of the sprite", "sprites": entries}
    prov = {"route": "scripted", "status": "concept, not production art; reviewedBy pending (T509 review)",
            "briefVersion": BRIEF_VERSION, "brief": f"art-source/f006/briefs/direction-{key}.md",
            "artBibleVersion": ART_BIBLE, "worldBibleVersion": WORLD_BIBLE,
            "tools": {"generator": "tools/art-pipeline/art_pipeline/concept_f006.py", "generatorSha256": _sha(me),
                      "paletteFile": "tools/art-pipeline/palette/palette-v1.json", "paletteSha256": _sha(PALETTE_V1_PATH),
                      "pillow": "pinned by tools/art-pipeline/uv.lock"},
            "transformSteps": ["exact-pixel drawing from palette ids (no antialiasing, alpha 0/255)",
                               "1 px ink_0 outline on free-standing sprites", "no quantisation needed (exact ids)",
                               "palette, canvas, alpha, frame-count and flash checks"],
            "sources": [], "license": "Repository-authored procedural drawing and palette; no third-party assets, "
            "models, textures or AI-generated pixels. Ownership: repository owner (fjure); no public license grant.",
            "assets": prov_assets, "reproduce": "python -m art_pipeline concept-f006 --out art-source/f006/concept"}
    for name, doc in (("manifest.json", manifest), ("provenance.json", prov),
                      ("check-report.json", {"passed": all(r["passed"] for v in report.values() for r in v),
                                             "assets": report})):
        (out_dir / name).write_text(json.dumps(doc, indent=2, sort_keys=True) + "\n", encoding="utf-8", newline="\n")
    return {"passed": all(r["passed"] for v in report.values() for r in v), "report": report}


def write_all(out_root: str | Path) -> dict:
    palette = load_palette(PALETTE_V1_PATH)
    root = Path(out_root)
    return {k: write_direction(root / f"direction-{k}", k, palette) for k in DIRECTIONS}
