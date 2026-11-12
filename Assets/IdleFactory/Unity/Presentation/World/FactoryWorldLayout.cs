using System;
using System.Collections.Generic;
using UnityEngine;

namespace IdleFactory.Unity.Presentation
{
    /// <summary>The screen orientation a layout is built for: 3 columns in portrait and 4 in landscape for the F004 lattice.</summary>
    public enum WorldOrientation
    {
        Portrait = 0,
        Landscape = 1,
    }

    /// <summary>
    /// The tap shape of one location: a diamond (<c>nx + ny &lt;= 1</c>) around the plate centre, clipped to the location's lattice
    /// <see cref="Cell"/>, so the shapes of two locations never overlap.
    /// </summary>
    public readonly struct HitShape
    {
        public HitShape(Vector2 centre, float halfWidth, float halfHeight, Rect cell)
        {
            Centre = centre;
            HalfWidth = halfWidth;
            HalfHeight = halfHeight;
            Cell = cell;
        }

        public Vector2 Centre { get; }
        public float HalfWidth { get; }
        public float HalfHeight { get; }

        /// <summary>The lattice cell (70 x 54 art px) the diamond is clipped to.</summary>
        public Rect Cell { get; }

        public bool Contains(Vector2 world)
        {
            if (world.x < Cell.xMin || world.x >= Cell.xMax || world.y < Cell.yMin || world.y >= Cell.yMax)
            {
                return false;
            }

            return Mathf.Abs(world.x - Centre.x) / HalfWidth + Mathf.Abs(world.y - Centre.y) / HalfHeight <= 1f;
        }
    }

    /// <summary>
    /// A static lane as an axis-aligned polyline in world units: a branch (plate rear edge to its row trunk) or a row trunk (the far end of the
    /// row to the depot). Immutable.
    /// </summary>
    public sealed class LanePath
    {
        public static readonly LanePath Empty = new LanePath(Array.Empty<Vector2>());

        private readonly Vector2[] points;

        public LanePath(Vector2[] points)
        {
            this.points = points ?? throw new ArgumentNullException(nameof(points));
        }

        public IReadOnlyList<Vector2> Points => points;

        public bool IsEmpty => points.Length < 2;

        public int SegmentCount => Math.Max(0, points.Length - 1);

        public Vector2 Start => points[0];

        public Vector2 End => points[points.Length - 1];

        /// <summary>The bounding rectangle of one segment (zero thickness on its constant axis).</summary>
        public Rect SegmentRect(int index)
        {
            var a = points[index];
            var b = points[index + 1];
            return Rect.MinMaxRect(Mathf.Min(a.x, b.x), Mathf.Min(a.y, b.y), Mathf.Max(a.x, b.x), Mathf.Max(a.y, b.y));
        }

        /// <summary>True when the point lies on one of the segments (within a small tolerance).</summary>
        public bool Touches(Vector2 point)
        {
            const float tolerance = 1e-4f;
            for (var i = 0; i < SegmentCount; i++)
            {
                var rect = SegmentRect(i);
                if (point.x >= rect.xMin - tolerance && point.x <= rect.xMax + tolerance && point.y >= rect.yMin - tolerance && point.y <= rect.yMax + tolerance)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>True when a segment passes through the open interior of the rectangle (touching an edge is allowed).</summary>
        public bool CrossesInterior(Rect rect)
        {
            const float epsilon = 1e-4f;
            for (var i = 0; i < SegmentCount; i++)
            {
                var segment = SegmentRect(i);
                if (segment.xMax > rect.xMin + epsilon && segment.xMin < rect.xMax - epsilon &&
                    segment.yMax > rect.yMin + epsilon && segment.yMin < rect.yMax - epsilon)
                {
                    return true;
                }
            }

            return false;
        }
    }

    /// <summary>
    /// The immutable world layout of the building locations (Feature 006, R04, data-model.md, contracts/world-presentation.md section 4).
    /// Everything that positions or measures the world reads it: the camera model and framing (<see cref="Bounds"/>, <see cref="LayoutId"/>),
    /// the slot view and its hit test (<see cref="TryHit"/>, <see cref="AnchorOf"/>, <see cref="FootprintOf"/>, <see cref="SortKeyOf"/>), the
    /// deck (<see cref="DeckCells"/>), and the depot, trunks, branches and deposit. No code outside this class assumes 12 locations or a fixed
    /// column count (FR-037).
    /// <para>
    /// <see cref="Bounds"/> is exactly the F004 <see cref="SlotGridLayout.Extent"/> (plates plus the badge band, margin not included);
    /// <see cref="Margin"/> is kept separate and the camera model adds it. Locations sit on the F004 lattice (pitch 70 x 54 art px, plates 70
    /// x 35, tap shape 90 x 45 clipped to the lattice cell). A location is placed by a lattice cell (column from the left, row from the
    /// bottom); the builder or a test supplies the cells and the geometry follows from them. Positions are whole art pixels (rule P3).
    /// World origin: the lower-left corner of <see cref="Bounds"/>.
    /// </para>
    /// <para>
    /// Links (R07): one static trunk per occupied row runs in the 19 art px row gap behind the row to the depot side and ends at the depot;
    /// each location has one branch from the plate's rear edge to its row trunk. There is no lane between machines (a trunk touches no
    /// footprint, a branch touches only its own).
    /// </para>
    /// </summary>
    public sealed class FactoryWorldLayout
    {
        /// <summary>The lane centreline of a row trunk, art pixels above the pivot of the row behind which it runs (the middle of the 19 px gap, whole pixels).</summary>
        public const int LaneAbovePivotPixels = 29;

        /// <summary>The plate rear (back) edge, art pixels above the pivot: the plate centre plus half the plate height.</summary>
        public const int RearEdgeAbovePivotPixels = 20;

        /// <summary>Badge box and two marker boxes: the per-location areas that nothing else may cover.</summary>
        public const int KeepOutCount = 3;

        private const float Density = SlotGridLayout.Density;

        private readonly Vector2Int[] cells;
        private readonly int[] gridToLocation;
        private readonly int columnCount;
        private readonly int rowCount;
        private readonly Vector2Int[] deckCells;
        private readonly LanePath[] branches;
        private readonly LanePath[] trunks;

        /// <summary>
        /// Builds a layout from explicit lattice cells, one per location in id order: <c>cells[i]</c> is (column from the left, row from the
        /// bottom) of location <c>i</c>. The cells are shifted so that the smallest column and row are 0. Without settings the
        /// <see cref="WorldCompositionSettings.DefaultComposition"/> values apply.
        /// </summary>
        public FactoryWorldLayout(string layoutId, IReadOnlyList<Vector2Int> cells, WorldOrientation orientation, WorldCompositionSettings settings = null)
            : this(layoutId, cells, orientation, settings != null ? settings.ComposeFor(orientation) : WorldCompositionSettings.DefaultComposition(orientation))
        {
        }

        public FactoryWorldLayout(string layoutId, IReadOnlyList<Vector2Int> cells, WorldOrientation orientation, LayoutComposition composition)
        {
            if (string.IsNullOrEmpty(layoutId))
            {
                throw new ArgumentException("A layout needs an id.", nameof(layoutId));
            }

            if (cells == null || cells.Count == 0)
            {
                throw new ArgumentException("A layout needs at least one location.", nameof(cells));
            }

            LayoutId = layoutId;
            Orientation = orientation;
            Margin = composition.MarginCells;
            DeckBandCells = Math.Max(1, composition.DeckBandCells);

            var minColumn = int.MaxValue;
            var minRow = int.MaxValue;
            var maxColumn = int.MinValue;
            var maxRow = int.MinValue;
            for (var i = 0; i < cells.Count; i++)
            {
                minColumn = Math.Min(minColumn, cells[i].x);
                minRow = Math.Min(minRow, cells[i].y);
                maxColumn = Math.Max(maxColumn, cells[i].x);
                maxRow = Math.Max(maxRow, cells[i].y);
            }

            columnCount = maxColumn - minColumn + 1;
            rowCount = maxRow - minRow + 1;
            this.cells = new Vector2Int[cells.Count];
            gridToLocation = new int[columnCount * rowCount];
            for (var i = 0; i < gridToLocation.Length; i++)
            {
                gridToLocation[i] = -1;
            }

            for (var i = 0; i < cells.Count; i++)
            {
                var cell = new Vector2Int(cells[i].x - minColumn, cells[i].y - minRow);
                var slot = cell.y * columnCount + cell.x;
                if (gridToLocation[slot] >= 0)
                {
                    throw new ArgumentException("Two locations share the lattice cell (" + cell.x + ", " + cell.y + ").", nameof(cells));
                }

                gridToLocation[slot] = i;
                this.cells[i] = cell;
            }

            // The F004 extent: every plate plus the badge band above the back row (SlotGridLayout.Extent).
            var widthPixels = (columnCount - 1) * SlotGridLayout.PitchXPixels + SlotGridLayout.PlateWidthPixels;
            var heightPixels = (rowCount - 1) * SlotGridLayout.PitchYPixels + SlotGridLayout.PivotAbovePlateBottomPixels + SlotGridLayout.ContentAbovePivotPixels;
            Bounds = new Rect(0f, 0f, widthPixels / Density, heightPixels / Density);

            deckCells = BuildDeckCells();
            DepotCell = ResolveRule(composition.DepotRule);
            DepositCell = ResolveRule(composition.DepositRule);
            DepotAnchor = PivotOfCell(DepotCell);
            DepositAnchor = PivotOfCell(DepositCell);
            DepotOnRight = DepotCell.x >= columnCount;

            branches = new LanePath[this.cells.Length];
            trunks = new LanePath[rowCount];
            BuildLanes();
        }

        /// <summary>The identity the camera model keys on (log, cache and layout change detection).</summary>
        public string LayoutId { get; }

        public int LocationCount => cells.Length;

        public WorldOrientation Orientation { get; }

        /// <summary>The lattice columns and rows that hold at least the cells of the locations (the bounding grid).</summary>
        public int ColumnCount => columnCount;

        /// <summary>The number of rows from the back (row 0) to the front. Also the number of trunks, some of which may be empty.</summary>
        public int RowCount => rowCount;

        /// <summary>The content extent: equals <see cref="SlotGridLayout.Extent"/> for the F004 lattice. The margin is not included.</summary>
        public Rect Bounds { get; }

        /// <summary>The fit margin in world units that the camera model adds around <see cref="Bounds"/> (F004: half a cell).</summary>
        public float Margin { get; }

        /// <summary>The width of the deck margin band in lattice cells.</summary>
        public int DeckBandCells { get; }

        /// <summary>Every lattice cell the deck covers: all locations plus the margin band, row by row from the front-left (rectangle grid of 70 x 54 art px).</summary>
        public IReadOnlyList<Vector2Int> DeckCells => deckCells;

        /// <summary>The union of the deck tiles: <see cref="Bounds"/> grown by the margin band, at least.</summary>
        public Rect DeckBounds
        {
            get
            {
                var first = DeckTileRect(deckCells[0]);
                var last = DeckTileRect(deckCells[deckCells.Length - 1]);
                return Rect.MinMaxRect(first.xMin, first.yMin, last.xMax, last.yMax);
            }
        }

        /// <summary>The margin-band cell of the depot (at the end of the row trunks).</summary>
        public Vector2Int DepotCell { get; }

        /// <summary>The world point where every trunk ends: the depot's anchor (the pivot of its cell).</summary>
        public Vector2 DepotAnchor { get; }

        /// <summary>The margin-band cell of the ore-vein deposit (scenery; never a hit target).</summary>
        public Vector2Int DepositCell { get; }

        public Vector2 DepositAnchor { get; }

        /// <summary>True when the depot lies right of the lattice, false when it lies left of it.</summary>
        public bool DepotOnRight { get; }

        public bool IsValidId(BuildingLocationId id) => id.Value >= 0 && id.Value < cells.Length;

        /// <summary>The lattice cell (column from the left, row from the bottom) of a location.</summary>
        public Vector2Int CellOf(BuildingLocationId id) => cells[Checked(id)];

        /// <summary>The footprint pivot of a location in world units: the origin of its canvas layers and of every overlay anchor.</summary>
        public Vector2 AnchorOf(BuildingLocationId id) => PivotOfCell(cells[Checked(id)]);

        /// <summary>The plate centre of a location (and the centre of its lattice cell).</summary>
        public Vector2 PlateCentreOf(BuildingLocationId id) => PlateCentreOfCell(cells[Checked(id)]);

        /// <summary>The bounding box of the plate diamond (70 x 35 art px): the footprint a sheet has to keep in view.</summary>
        public Rect FootprintOf(BuildingLocationId id)
        {
            var size = new Vector2(SlotGridLayout.PlateWidthPixels, SlotGridLayout.PlateHeightPixels) / SlotGridLayout.Density;
            return new Rect(PlateCentreOf(id) - size * 0.5f, size);
        }

        /// <summary>The lattice cell of a location around its plate centre (70 x 54 art px): the clip of its tap shape.</summary>
        public Rect LocationCellRectOf(BuildingLocationId id) =>
            new Rect(PlateCentreOf(id) - new Vector2(SlotGridLayout.PitchX, SlotGridLayout.PitchY) * 0.5f, new Vector2(SlotGridLayout.PitchX, SlotGridLayout.PitchY));

        /// <summary>The tap shape: the plate diamond grown to 90 x 45 art px, clipped to the lattice cell.</summary>
        public HitShape HitShapeOf(BuildingLocationId id) => new HitShape(
            PlateCentreOf(id),
            SlotGridLayout.HitWidthPixels * 0.5f / SlotGridLayout.Density,
            SlotGridLayout.HitHeightPixels * 0.5f / SlotGridLayout.Density,
            LocationCellRectOf(id));

        /// <summary>The sorting depth of a location: its row counted from the back (top) row; a front row draws later.</summary>
        public int SortKeyOf(BuildingLocationId id) => rowCount - 1 - cells[Checked(id)].y;

        /// <summary>The row of a location counted from the back (0); the index <see cref="TrunkOf"/> takes.</summary>
        public int RowOf(BuildingLocationId id) => SortKeyOf(id);

        /// <summary>The badge box (0) and the two marker boxes (1, 2) of a location in world units: no lane, deposit or depot may cover them.</summary>
        public Rect KeepOutOf(BuildingLocationId id, int index)
        {
            if (index < 0 || index >= KeepOutCount)
            {
                throw new ArgumentOutOfRangeException(nameof(index));
            }

            var box = index == 0 ? StateCueGeometry.BadgeBox() : StateCueGeometry.MarkerBox(index - 1);
            return new Rect(AnchorOf(id) + box.position, box.size);
        }

        /// <summary>The location under a world point, or false. A closed-form lattice lookup: no loop over locations, no allocation.</summary>
        public bool TryHit(Vector2 world, out BuildingLocationId id)
        {
            var first = PlateCentreOfCell(Vector2Int.zero); // bottom-left cell centre
            var column = Mathf.FloorToInt((world.x - first.x) / SlotGridLayout.PitchX + 0.5f);
            var rowFromBottom = Mathf.FloorToInt((world.y - first.y) / SlotGridLayout.PitchY + 0.5f);
            id = default;
            if (column < 0 || column >= columnCount || rowFromBottom < 0 || rowFromBottom >= rowCount)
            {
                return false;
            }

            var location = gridToLocation[rowFromBottom * columnCount + column];
            if (location < 0)
            {
                return false;
            }

            var centre = PlateCentreOfCell(cells[location]);
            var nx = Mathf.Abs(world.x - centre.x) / (SlotGridLayout.HitWidthPixels * 0.5f / SlotGridLayout.Density);
            var ny = Mathf.Abs(world.y - centre.y) / (SlotGridLayout.HitHeightPixels * 0.5f / SlotGridLayout.Density);
            if (nx + ny > 1f)
            {
                return false;
            }

            id = new BuildingLocationId(location);
            return true;
        }

        /// <summary>True when the location is one of the first <paramref name="unlockedCount"/> locations (the unlock order is the id order).</summary>
        public bool IsUnlocked(BuildingLocationId id, int unlockedCount) => IsValidId(id) && id.Value < unlockedCount;

        /// <summary>The footprint pivot of a deck cell (the same lattice phase as every location): the pivot of a floor tile.</summary>
        public static Vector2 PivotOfCell(Vector2Int cell) => new Vector2(
            (SlotGridLayout.PlateWidthPixels / 2 + cell.x * SlotGridLayout.PitchXPixels) / Density,
            (SlotGridLayout.PivotAbovePlateBottomPixels + cell.y * SlotGridLayout.PitchYPixels) / Density);

        /// <summary>The plate centre of a lattice cell.</summary>
        public static Vector2 PlateCentreOfCell(Vector2Int cell) => PivotOfCell(cell) + new Vector2(0f, SlotGridLayout.PlateCentreAbovePivot);

        /// <summary>The deck tile of a cell: 70 x 54 art px around the cell's pivot (27 px above and below it).</summary>
        public static Rect DeckTileRect(Vector2Int cell)
        {
            var pivot = PivotOfCell(cell);
            return new Rect(
                pivot.x - SlotGridLayout.PitchXPixels / 2 / Density,
                pivot.y - SlotGridLayout.FloorTilePivotYPixels / Density,
                SlotGridLayout.PitchXPixels / Density,
                SlotGridLayout.PitchYPixels / Density);
        }

        /// <summary>The trunk of a row, counted from the back (0): from the far end of the row to the depot. Empty when the row holds no location.</summary>
        public LanePath TrunkOf(int row)
        {
            if (row < 0 || row >= rowCount)
            {
                throw new ArgumentOutOfRangeException(nameof(row));
            }

            return trunks[row];
        }

        /// <summary>The branch of a location: from the middle of the plate's rear edge to the trunk of its row.</summary>
        public LanePath BranchOf(BuildingLocationId id) => branches[Checked(id)];

        /// <summary>True when two rectangles share an area (touching edges and float rounding of 1e-3 world units do not count).</summary>
        public static bool Overlaps(Rect a, Rect b)
        {
            const float tolerance = 1e-3f;
            return a.xMax > b.xMin + tolerance && b.xMax > a.xMin + tolerance && a.yMax > b.yMin + tolerance && b.yMax > a.yMin + tolerance;
        }

        /// <summary>The problems of the layout against the contract rules; empty when the layout is valid (data-model.md FactoryWorldLayout validation).</summary>
        public IReadOnlyList<string> Validate()
        {
            var problems = new List<string>();
            var count = LocationCount;
            for (var i = 0; i < count; i++)
            {
                var a = new BuildingLocationId(i);
                var footprint = FootprintOf(a);
                var shape = HitShapeOf(a);
                if (footprint.xMin < Bounds.xMin - 1e-4f || footprint.yMin < Bounds.yMin - 1e-4f || footprint.xMax > Bounds.xMax + 1e-4f || footprint.yMax > Bounds.yMax + 1e-4f)
                {
                    problems.Add(a + ": the footprint lies outside the bounds.");
                }

                if (!shape.Cell.Contains(shape.Centre))
                {
                    problems.Add(a + ": the hit shape centre lies outside its cell.");
                }

                for (var j = i + 1; j < count; j++)
                {
                    var b = new BuildingLocationId(j);
                    if (Overlaps(footprint, FootprintOf(b)))
                    {
                        problems.Add(a + " and " + b + ": the footprints overlap.");
                    }

                    if (Overlaps(shape.Cell, LocationCellRectOf(b)))
                    {
                        problems.Add(a + " and " + b + ": the hit shapes are not disjoint.");
                    }
                }
            }

            for (var row = 0; row < rowCount; row++)
            {
                var trunk = trunks[row];
                if (trunk.IsEmpty)
                {
                    continue;
                }

                if (Vector2.Distance(trunk.End, DepotAnchor) > 1e-4f)
                {
                    problems.Add("trunk " + row + " does not end at the depot.");
                }

                for (var i = 0; i < count; i++)
                {
                    if (trunk.CrossesInterior(FootprintOf(new BuildingLocationId(i))))
                    {
                        problems.Add("trunk " + row + " crosses the footprint of " + new BuildingLocationId(i) + ".");
                    }

                }
            }

            for (var i = 0; i < count; i++)
            {
                var id = new BuildingLocationId(i);
                var branch = branches[i];
                var footprint = FootprintOf(id);
                var trunk = trunks[RowOf(id)];
                if (branch.IsEmpty)
                {
                    problems.Add(id + ": the branch is missing.");
                    continue;
                }

                if (Mathf.Abs(branch.Start.y - footprint.yMax) > 1e-4f || branch.Start.x < footprint.xMin || branch.Start.x > footprint.xMax)
                {
                    problems.Add(id + ": the branch does not start on the plate's rear edge.");
                }

                if (trunk.IsEmpty || !trunk.Touches(branch.End))
                {
                    problems.Add(id + ": the branch does not reach its trunk.");
                }

                for (var j = 0; j < count; j++)
                {
                    if (branch.CrossesInterior(FootprintOf(new BuildingLocationId(j))))
                    {
                        problems.Add(id + ": the branch crosses the footprint of " + new BuildingLocationId(j) + ".");
                    }
                }
            }

            CheckMarginCell(DepotCell, "DepotCell", problems);
            CheckMarginCell(DepositCell, "DepositCell", problems);
            if (DepotCell == DepositCell)
            {
                problems.Add("The depot and the deposit share a cell.");
            }

            return problems;
        }

        private void CheckMarginCell(Vector2Int cell, string name, List<string> problems)
        {
            if (cell.x >= 0 && cell.x < columnCount && cell.y >= 0 && cell.y < rowCount)
            {
                problems.Add(name + " lies inside the lattice, not in the margin band.");
            }

            if (Array.IndexOf(deckCells, cell) < 0)
            {
                problems.Add(name + " lies outside the deck cells.");
            }

            var tile = DeckTileRect(cell);
            for (var i = 0; i < LocationCount; i++)
            {
                var id = new BuildingLocationId(i);
                if (Overlaps(tile, LocationCellRectOf(id)))
                {
                    problems.Add(name + " overlaps the hit shape of " + id + ".");
                }

                for (var k = 0; k < KeepOutCount; k++)
                {
                    if (Overlaps(tile, KeepOutOf(id, k)))
                    {
                        problems.Add(name + " overlaps keep-out " + k + " of " + id + ".");
                    }
                }
            }
        }

        private int Checked(BuildingLocationId id)
        {
            if (id.Value < 0 || id.Value >= cells.Length)
            {
                throw new ArgumentOutOfRangeException(nameof(id), id.Value, "Unknown building location.");
            }

            return id.Value;
        }

        private Vector2Int[] BuildDeckCells()
        {
            var band = DeckBandCells;
            var columns = columnCount + 2 * band;
            var rows = rowCount + 2 * band;
            var result = new Vector2Int[columns * rows];
            var index = 0;
            for (var row = -band; row < rowCount + band; row++)
            {
                for (var column = -band; column < columnCount + band; column++)
                {
                    result[index++] = new Vector2Int(column, row);
                }
            }

            return result;
        }

        private Vector2Int ResolveRule(MarginCellRule rule)
        {
            var horizontal = rule.side == MarginSide.Bottom || rule.side == MarginSide.Top;
            var length = horizontal ? columnCount : rowCount;
            var along = rule.along < 0 ? length + rule.along : rule.along;
            along = Mathf.Clamp(along, 0, length - 1);
            switch (rule.side)
            {
                case MarginSide.Left:
                    return new Vector2Int(-1, along);
                case MarginSide.Bottom:
                    return new Vector2Int(along, -1);
                case MarginSide.Top:
                    return new Vector2Int(along, rowCount);
                default:
                    return new Vector2Int(columnCount, along);
            }
        }

        private void BuildLanes()
        {
            var minX = new float[rowCount];
            var maxX = new float[rowCount];
            var any = new bool[rowCount];
            for (var i = 0; i < cells.Length; i++)
            {
                var row = rowCount - 1 - cells[i].y;
                var x = PivotOfCell(cells[i]).x;
                minX[row] = any[row] ? Mathf.Min(minX[row], x) : x;
                maxX[row] = any[row] ? Mathf.Max(maxX[row], x) : x;
                any[row] = true;
            }

            var collectorX = DepotAnchor.x;
            for (var row = 0; row < rowCount; row++)
            {
                if (!any[row])
                {
                    trunks[row] = LanePath.Empty;
                    continue;
                }

                var rowFromBottom = rowCount - 1 - row;
                var laneY = (SlotGridLayout.PivotAbovePlateBottomPixels + rowFromBottom * SlotGridLayout.PitchYPixels + LaneAbovePivotPixels) / Density;
                var startX = DepotOnRight ? minX[row] : maxX[row];
                var points = new List<Vector2> { new Vector2(startX, laneY), new Vector2(collectorX, laneY) };
                if (Mathf.Abs(laneY - DepotAnchor.y) > 1e-4f)
                {
                    points.Add(DepotAnchor);
                }

                trunks[row] = new LanePath(points.ToArray());
            }

            for (var i = 0; i < cells.Length; i++)
            {
                var pivot = PivotOfCell(cells[i]);
                var row = rowCount - 1 - cells[i].y;
                var rearY = (SlotGridLayout.PivotAbovePlateBottomPixels + cells[i].y * SlotGridLayout.PitchYPixels + RearEdgeAbovePivotPixels) / Density;
                var laneY = trunks[row].Start.y;
                branches[i] = new LanePath(new[] { new Vector2(pivot.x, rearY), new Vector2(pivot.x, laneY) });
            }
        }
    }
}
