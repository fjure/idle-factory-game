"""F006 mockup geometry (T505, research R03, R07, R22): the lattice derived from the shared Unity export.

Everything here is pure integer art-pixel geometry in the Unity lattice frame: art pixels at density 24, y up, the
origin at the lower-left corner of the location extent (`SlotGridLayout.Extent`). A layout is a set of explicit cells
(row, column), row 0 being the back (top) row, exactly as `SlotGridLayout` indexes slots. From it the module derives

- the deck cells including the margin band (kind `location`, `filler` for a hole inside the bounding box, `margin`),
- one trunk per row in the 19 px row gap behind the row's plates, running to the depot side (R07),
- one branch per occupied location from its plate's rear apex to its row trunk (R07),
- the depot cell and the deposit cell from `CompositionParams`, which mirrors `WorldCompositionSettings` (R22),
- the keep-outs and `lane_violations`, which proves the lane rules.

A mockup-only assumption (not in R07 text): the trunks meet in a short vertical `spine` in the depot column, so every
trunk end touches the depot. It is a lane like a trunk (static, never attributed).
"""
from __future__ import annotations

import json
from dataclasses import dataclass, field
from pathlib import Path

Rect = tuple[int, int, int, int]  # x0, y0, x1, y1 (half-open, art px, y up)

GEOMETRY_PATH = Path(__file__).resolve().parent.parent / "f006" / "lattice-geometry.json"
SCHEMA = "f006-lattice-geometry/1"

TRUNK_WIDTH = 5
BRANCH_WIDTH = 3


@dataclass(frozen=True)
class Consts:
    """The lattice constants of the export (SlotGridLayout and StateCueGeometry)."""
    density: int
    pitch_x: int
    pitch_y: int
    plate_w: int
    plate_h: int
    hit_w: int
    hit_h: int
    pivot_above: int          # pivot above the plate bottom
    content_above: int        # badge band top above the pivot
    row_gap: int
    centre_above_pivot: float  # plate centre above the pivot
    badge_box: tuple[float, float, float, float]
    marker_boxes: tuple[tuple[float, float, float, float], ...]

    @property
    def plate_top_above(self) -> int:
        """The plate's rear apex above the pivot (20)."""
        return self.plate_h - self.pivot_above


def load_geometry(path: Path | str = GEOMETRY_PATH) -> dict:
    geo = json.loads(Path(path).read_text(encoding="utf-8"))
    if geo.get("schema") != SCHEMA:
        raise ValueError(f"unexpected geometry schema {geo.get('schema')!r}, expected {SCHEMA!r}")
    return geo


def consts_of(geo: dict) -> Consts:
    return Consts(
        density=int(geo["density"]), pitch_x=int(geo["pitchPx"][0]), pitch_y=int(geo["pitchPx"][1]),
        plate_w=int(geo["platePx"][0]), plate_h=int(geo["platePx"][1]), hit_w=int(geo["hitPx"][0]), hit_h=int(geo["hitPx"][1]),
        pivot_above=int(geo["pivotAbovePlateBottomPx"]), content_above=int(geo["contentAbovePivotPx"]),
        row_gap=int(geo["rowGapPx"]), centre_above_pivot=float(geo["plateCentreAbovePivotPx"]),
        badge_box=tuple(geo["badgeBoxPx"]), marker_boxes=tuple(tuple(m) for m in geo["markerBoxesPx"]))


@dataclass(frozen=True)
class CompositionParams:
    """Mirrors `WorldCompositionSettings` (R22): margin width in cells, the depot cell rule and the deposit cell rule
    per orientation. The values are mockup assumptions until the P1 asset exists."""
    margin_cells: int = 1
    depot_side: tuple[tuple[str, str], ...] = (("portrait", "right"), ("landscape", "right"))
    depot_row: str = "middle"  # "middle" | "front" | "back"
    deposit_corner: tuple[tuple[str, str], ...] = (("portrait", "top-left"), ("landscape", "top-left"))

    def depot_side_of(self, orientation: str) -> str:
        return dict(self.depot_side)[orientation]

    def deposit_corner_of(self, orientation: str) -> str:
        return dict(self.deposit_corner)[orientation]


@dataclass(frozen=True)
class Layout:
    """A world layout as explicit cells (row, column), row 0 = back row. `states` maps a cell to occupied, empty or locked;
    a cell without an entry is occupied."""
    name: str
    cells: tuple[tuple[int, int], ...]
    states: tuple[tuple[tuple[int, int], str], ...] = ()

    def state_of(self, cell: tuple[int, int]) -> str:
        return dict(self.states).get(cell, "occupied")


def rect_layout(name: str, columns: int, rows: int, count: int | None = None, states: dict | None = None) -> Layout:
    """`count` cells in reading order of a columns x rows rectangle (the last row may be short), like `SlotGridLayout`."""
    count = columns * rows if count is None else count
    cells = tuple((i // columns, i % columns) for i in range(count))
    return Layout(name, cells, tuple(sorted((states or {}).items())))


def l_layout(name: str = "L") -> Layout:
    """An L-shaped layout: the left column and the front row of a 3 x 3 box; four bounding-box cells are holes (`filler`)."""
    cells = ((0, 0), (1, 0), (2, 0), (2, 1), (2, 2))
    return Layout(name, cells)


TEST_LAYOUTS: dict[str, Layout] = {
    "3x4": rect_layout("3x4", 3, 4),    # 3 columns, 4 rows: today's portrait lattice
    "4x3": rect_layout("4x3", 4, 3),    # 4 columns, 3 rows: today's landscape lattice
    "6": rect_layout("6", 3, 2),        # 6 locations
    "20": rect_layout("20", 4, 5),      # 20 locations
    "L": l_layout("L"),                 # explicit non-rectangular cells
}


@dataclass(frozen=True)
class Cell:
    kind: str            # location | filler | margin
    column: int
    row: int
    state: str           # occupied | empty | locked | none
    pivot: tuple[int, int]
    plate: Rect          # the 70 x 35 plate bounding box
    tile: Rect           # the 70 x 54 floor tile box (the lattice cell)


@dataclass(frozen=True)
class Lane:
    kind: str            # trunk | branch | spine
    row: int | None
    column: int | None
    rect: Rect


@dataclass
class Lattice:
    layout: Layout
    consts: Consts
    params: CompositionParams
    orientation: str
    columns: int
    rows: int
    extent: tuple[int, int]
    cells: list[Cell] = field(default_factory=list)
    trunks: list[Lane] = field(default_factory=list)
    branches: list[Lane] = field(default_factory=list)
    spine: Lane | None = None
    depot: Cell | None = None
    deposit: Cell | None = None

    @property
    def lanes(self) -> list[Lane]:
        return self.trunks + self.branches + ([self.spine] if self.spine else [])

    def location(self, row: int, column: int) -> Cell:
        for c in self.cells:
            if c.kind == "location" and (c.row, c.column) == (row, column):
                return c
        raise KeyError((row, column))

    def locations(self) -> list[Cell]:
        return [c for c in self.cells if c.kind == "location"]


def pivot_of(consts: Consts, rows: int, column: int, row: int) -> tuple[int, int]:
    """The pivot of the cell (row from the back row 0, column), valid for margin cells too (negative or past the ends)."""
    return (consts.plate_w // 2 + column * consts.pitch_x, consts.pivot_above + (rows - 1 - row) * consts.pitch_y)


def plate_rect(consts: Consts, pivot: tuple[int, int]) -> Rect:
    return (pivot[0] - consts.plate_w // 2, pivot[1] - consts.pivot_above, pivot[0] + consts.plate_w // 2, pivot[1] - consts.pivot_above + consts.plate_h)


def tile_rect(consts: Consts, pivot: tuple[int, int]) -> Rect:
    half = consts.pitch_y // 2
    return (pivot[0] - consts.pitch_x // 2, pivot[1] - half, pivot[0] + consts.pitch_x // 2, pivot[1] + half)


def orientation_of(columns: int, rows: int) -> str:
    """`SlotGridLayout.ColumnsFor`: 3 columns on a portrait screen, 4 on a landscape one."""
    return "landscape" if columns > rows else "portrait"


def build_lattice(layout: Layout, consts: Consts, params: CompositionParams | None = None, orientation: str | None = None) -> Lattice:
    params = params or CompositionParams()
    if params.margin_cells < 1:
        raise ValueError("margin_cells must be at least 1: the depot and the deposit sit in the margin band")
    cells = sorted(layout.cells)
    if len(set(cells)) != len(cells) or not cells:
        raise ValueError("a layout needs distinct, non-empty cells")
    if min(r for r, _ in cells) != 0 or min(c for _, c in cells) != 0:
        raise ValueError("layout cells must start at row 0 and column 0")
    rows = max(r for r, _ in cells) + 1
    columns = max(c for _, c in cells) + 1
    orient = orientation or orientation_of(columns, rows)
    m = params.margin_cells
    extent = ((columns - 1) * consts.pitch_x + consts.plate_w, (rows - 1) * consts.pitch_y + consts.pivot_above + consts.content_above)
    lat = Lattice(layout, consts, params, orient, columns, rows, extent)

    located = set(cells)
    for row in range(-m, rows + m):
        for col in range(-m, columns + m):
            inside = 0 <= row < rows and 0 <= col < columns
            kind = "margin" if not inside else ("location" if (row, col) in located else "filler")
            state = layout.state_of((row, col)) if kind == "location" else "none"
            pv = pivot_of(consts, rows, col, row)
            lat.cells.append(Cell(kind, col, row, state, pv, plate_rect(consts, pv), tile_rect(consts, pv)))

    # Depot and deposit (R22): margin cells.
    side = params.depot_side_of(orient)
    depot_col = columns + m - 1 if side == "right" else -m
    depot_row = {"middle": (rows - 1) // 2, "front": rows - 1, "back": 0}[params.depot_row]
    lat.depot = _cell_at(lat, depot_row, depot_col)
    corner = params.deposit_corner_of(orient)
    v, h = corner.split("-")
    dep_row = -m if v == "top" else rows + m - 1
    dep_col = -m if h == "left" else columns + m - 1
    lat.deposit = _cell_at(lat, dep_row, dep_col)

    # Trunks: per row, in the row gap behind the row's plates (R07), toward the depot side.
    gap_lo = consts.plate_top_above
    lane_lo = gap_lo + (consts.row_gap - TRUNK_WIDTH) // 2
    depot_x = lat.depot.pivot[0]
    spine_x0 = depot_x - BRANCH_WIDTH // 2
    for row in range(rows):
        cols_in_row = [c for r, c in cells if r == row]
        py = pivot_of(consts, rows, 0, row)[1]
        left = pivot_of(consts, rows, min(cols_in_row), row)[0] - consts.plate_w // 2
        x0, x1 = (left, depot_x + 1) if side == "right" else (depot_x, pivot_of(consts, rows, max(cols_in_row), row)[0] + consts.plate_w // 2)
        lat.trunks.append(Lane("trunk", row, None, (x0, py + lane_lo, x1, py + lane_lo + TRUNK_WIDTH)))
    lat.spine = Lane("spine", None, None, (spine_x0, min(t.rect[1] for t in lat.trunks), spine_x0 + BRANCH_WIDTH, max(t.rect[3] for t in lat.trunks)))

    # Branches: occupied locations only, from the plate's rear apex to the trunk of the same row.
    for c in lat.locations():
        if c.state != "occupied":
            continue
        x0 = c.pivot[0] - BRANCH_WIDTH // 2
        lat.branches.append(Lane("branch", c.row, c.column, (x0, c.pivot[1] + gap_lo, x0 + BRANCH_WIDTH, c.pivot[1] + lane_lo)))
    return lat


def _cell_at(lat: Lattice, row: int, col: int) -> Cell:
    for c in lat.cells:
        if (c.row, c.column) == (row, col):
            return c
    raise KeyError((row, col))


# --------------------------------------------------------------------------------------------------- keep-out checks

def rect_intersects(a: Rect, b: Rect) -> bool:
    return a[0] < b[2] and b[0] < a[2] and a[1] < b[3] and b[1] < a[3]


def rect_touches(a: Rect, b: Rect) -> bool:
    """Intersect or share an edge segment."""
    return a[0] <= b[2] and b[0] <= a[2] and a[1] <= b[3] and b[1] <= a[3]


def clip_rect(a: Rect, b: Rect) -> Rect | None:
    r = (max(a[0], b[0]), max(a[1], b[1]), min(a[2], b[2]), min(a[3], b[3]))
    return r if r[0] < r[2] and r[1] < r[3] else None


def diamond_hits_rect(centre: tuple[float, float], half_w: float, half_h: float, rect: Rect | None) -> bool:
    """Strict interior intersection of the diamond |dx|/hw + |dy|/hh < 1 with a pixel rect (nearest-point test)."""
    if rect is None:
        return False
    nx = min(max(centre[0], rect[0]), rect[2])
    ny = min(max(centre[1], rect[1]), rect[3])
    return abs(nx - centre[0]) / half_w + abs(ny - centre[1]) / half_h < 1.0


def hit_diamond(lat: Lattice, cell: Cell) -> tuple[tuple[float, float], float, float]:
    c = lat.consts
    return ((cell.pivot[0], cell.pivot[1] + c.centre_above_pivot), c.hit_w / 2, c.hit_h / 2)


def plate_diamond(lat: Lattice, cell: Cell) -> tuple[tuple[float, float], float, float]:
    c = lat.consts
    return ((cell.pivot[0], cell.pivot[1] + c.centre_above_pivot), c.plate_w / 2, c.plate_h / 2)


def lane_violations(lat: Lattice) -> list[str]:
    """Every breach of the R07 lane rules and of the keep-outs; an empty list is a pass.

    - a trunk lies wholly in the row gap behind its row (between the row's plate top and the plate bottom of the row behind),
    - a branch runs from the rear apex of its own plate to its trunk and has no trunk of another row,
    - no lane touches any location's hit diamond (clipped to the 70 x 54 lattice cell, as `SlotGridLayout.HitTest`) except the own branch, and none touches a plate diamond except at the own rear apex,
    - there is no lane between machines: no lane crosses a plate bounding box of any location,
    - the depot and deposit are margin cells, the deposit touches no lane and no hit diamond, the depot touches only the spine.
    Badge and marker boxes sit in the same 19 px gap as the trunk (Art Bible 3.2: badge 16..39 px above the pivot) and draw above
    the lanes: the contract's layers are WorldLinks < Overlay, so they are a layer rule, not a geometric keep-out.
    """
    c = lat.consts
    problems: list[str] = []
    locs = lat.locations()
    by_row_trunk = {t.row: t for t in lat.trunks}
    for row in range(lat.rows):
        if row not in by_row_trunk:
            problems.append(f"row {row} has no trunk")
    for t in lat.trunks:
        py = pivot_of(c, lat.rows, 0, t.row)[1]
        gap = (py + c.plate_top_above, py + c.plate_top_above + c.row_gap)
        if not (gap[0] <= t.rect[1] and t.rect[3] <= gap[1]):
            problems.append(f"trunk of row {t.row} leaves the row gap {gap}: {t.rect}")
    for b in lat.branches:
        cell = lat.location(b.row, b.column)
        t = by_row_trunk[b.row]
        if b.rect[1] != cell.pivot[1] + c.plate_top_above:
            problems.append(f"branch ({b.row},{b.column}) does not start at the plate rear apex")
        if b.rect[3] != t.rect[1]:
            problems.append(f"branch ({b.row},{b.column}) does not end at its row trunk")
        if not rect_touches(b.rect, t.rect):
            problems.append(f"branch ({b.row},{b.column}) does not touch its trunk")
        if lat.layout.state_of((b.row, b.column)) != "occupied":
            problems.append(f"branch on a non-occupied location ({b.row},{b.column})")
    occupied = sum(1 for cell in locs if cell.state == "occupied")
    if len(lat.branches) != occupied:
        problems.append(f"{len(lat.branches)} branches for {occupied} occupied locations")
    for lane in lat.lanes:
        for cell in locs:
            own = lane.kind == "branch" and (lane.row, lane.column) == (cell.row, cell.column)
            if rect_intersects(lane.rect, cell.plate) and not own:
                problems.append(f"{lane.kind} {lane.rect} crosses the plate box of ({cell.row},{cell.column})")
            ctr, hw, hh = hit_diamond(lat, cell)
            if not own and diamond_hits_rect(ctr, hw, hh, clip_rect(lane.rect, cell.tile)):
                problems.append(f"{lane.kind} {lane.rect} enters the hit diamond of ({cell.row},{cell.column})")
            pc, pw, ph = plate_diamond(lat, cell)
            if diamond_hits_rect(pc, pw, ph, lane.rect):
                problems.append(f"{lane.kind} {lane.rect} enters the plate diamond of ({cell.row},{cell.column})")
    for name, cell in (("depot", lat.depot), ("deposit", lat.deposit)):
        if cell.kind != "margin":
            problems.append(f"{name} is not in the margin band")
        for cell2 in locs:
            if rect_intersects(cell.plate, cell2.plate):
                problems.append(f"{name} overlaps location ({cell2.row},{cell2.column})")
            ctr, hw, hh = hit_diamond(lat, cell2)
            if diamond_hits_rect(ctr, hw, hh, clip_rect(cell.plate, cell2.tile)):
                problems.append(f"{name} cell touches the hit diamond of ({cell2.row},{cell2.column})")
    for lane in lat.lanes:
        if rect_intersects(lane.rect, lat.deposit.plate):
            problems.append(f"{lane.kind} {lane.rect} touches the deposit cell")
    if lat.depot.column != (lat.columns + lat.params.margin_cells - 1 if lat.params.depot_side_of(lat.orientation) == "right" else -lat.params.margin_cells):
        problems.append("depot is not at the depot-side margin column")
    for t in lat.trunks:
        if not rect_touches(t.rect, lat.spine.rect):
            problems.append(f"trunk of row {t.row} does not reach the depot spine")
    if (lat.depot.row, lat.depot.column) == (lat.deposit.row, lat.deposit.column):
        problems.append("depot and deposit share a cell")
    return problems
