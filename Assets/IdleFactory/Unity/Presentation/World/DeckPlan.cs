using System;
using System.Collections.Generic;
using UnityEngine;

namespace IdleFactory.Unity.Presentation
{
    /// <summary>A rectangle in whole art pixels of the world (24 art px per world unit), lower-left origin, half-open: [x, x + width) x [y, y + height).</summary>
    public readonly struct PixelRect
    {
        public PixelRect(int x, int y, int width, int height)
        {
            X = x;
            Y = y;
            Width = width;
            Height = height;
        }

        public int X { get; }
        public int Y { get; }
        public int Width { get; }
        public int Height { get; }
        public int XMax => X + Width;
        public int YMax => Y + Height;

        public bool Overlaps(PixelRect other) => X < other.XMax && other.X < XMax && Y < other.YMax && other.Y < YMax;

        public PixelRect Grown(int amount) => new PixelRect(X - amount, Y - amount, Width + 2 * amount, Height + 2 * amount);

        public bool Contains(PixelRect inner) => inner.X >= X && inner.Y >= Y && inner.XMax <= XMax && inner.YMax <= YMax;

        public static PixelRect FromWorld(Rect world)
        {
            const float Density = SlotGridLayout.Density;
            var x0 = Mathf.RoundToInt(world.xMin * Density);
            var y0 = Mathf.RoundToInt(world.yMin * Density);
            return new PixelRect(x0, y0, Mathf.RoundToInt(world.xMax * Density) - x0, Mathf.RoundToInt(world.yMax * Density) - y0);
        }

        public override string ToString() => "[" + X + "," + Y + " " + Width + "x" + Height + "]";
    }

    /// <summary>The canvas of a world sprite in whole art pixels and its pivot from the lower-left corner, read from the registry row of its key.</summary>
    public readonly struct DeckSpriteSize
    {
        public DeckSpriteSize(int width, int height, int pivotX, int pivotY)
        {
            Width = width;
            Height = height;
            PivotX = pivotX;
            PivotY = pivotY;
        }

        public int Width { get; }
        public int Height { get; }
        public int PivotX { get; }
        public int PivotY { get; }

        /// <summary>The canvas rectangle when the pivot sits on <paramref name="anchor"/>.</summary>
        public PixelRect RectAt(Vector2Int anchor) => new PixelRect(anchor.x - PivotX, anchor.y - PivotY, Width, Height);

        /// <summary>The size of a registry key (<see cref="PresentationSpriteCatalog.TryGetRegistryRequirement"/>), or false when the key has no row.</summary>
        public static bool TryOf(string key, out DeckSpriteSize size)
        {
            size = default;
            if (!PresentationSpriteCatalog.TryGetRegistryRequirement(key, out var row))
            {
                return false;
            }

            var width = Mathf.RoundToInt(row.WorldWidth * SlotGridLayout.Density);
            var height = Mathf.RoundToInt(row.WorldHeight * SlotGridLayout.Density);
            size = new DeckSpriteSize(width, height, Mathf.RoundToInt(row.PivotX * width), Mathf.RoundToInt(row.PivotY * height));
            return true;
        }
    }

    /// <summary>What a location cell of the deck shows under its plate.</summary>
    public enum DeckCellArt
    {
        Deck = 0,
        Hatch = 1,
    }

    /// <summary>One prop of the deck: its catalog key, the cell it stands in and the whole-pixel world anchor of its pivot.</summary>
    public readonly struct DeckPropPlacement
    {
        public DeckPropPlacement(string key, Vector2Int cell, Vector2Int anchor, PixelRect rect)
        {
            Key = key;
            Cell = cell;
            Anchor = anchor;
            Rect = rect;
        }

        public string Key { get; }
        public Vector2Int Cell { get; }

        /// <summary>The world art pixel the sprite pivot sits on.</summary>
        public Vector2Int Anchor { get; }

        /// <summary>The sprite canvas in world art pixels.</summary>
        public PixelRect Rect { get; }
    }

    /// <summary>One edge piece of the front margin row: its catalog key and the whole-pixel world anchor of its pivot (the deck diamond centre).</summary>
    public readonly struct DeckEdgePlacement
    {
        public DeckEdgePlacement(string key, Vector2Int anchor, PixelRect rect, int order)
        {
            Key = key;
            Anchor = anchor;
            Rect = rect;
            Order = order;
        }

        public string Key { get; }
        public Vector2Int Anchor { get; }
        public PixelRect Rect { get; }

        /// <summary>The draw order inside the edge group: pieces nearer the apex draw later.</summary>
        public int Order { get; }
    }

    /// <summary>
    /// What the deck draws for one layout, stage and set of location conditions (T527, research R06, contracts/world-presentation.md section 1): the
    /// base cells, the art of every location cell, the front-row edge pieces and the props. An immutable value built by <see cref="DeckPlanner"/>.
    /// </summary>
    public sealed class DeckPlan
    {
        public DeckPlan(
            IReadOnlyList<Vector2Int> baseCells,
            IReadOnlyList<Vector2Int> locationCells,
            IReadOnlyList<DeckCellArt> locationArt,
            IReadOnlyList<DeckEdgePlacement> edges,
            IReadOnlyList<DeckPropPlacement> props,
            int eligiblePropCells,
            int stage,
            EdgeRule edgeRule,
            IReadOnlyList<Vector2Int> litRingCells = null,
            int deckCellCount = 0,
            bool dressed = false)
        {
            BaseCells = baseCells;
            LocationCells = locationCells;
            LocationArt = locationArt;
            Edges = edges;
            Props = props;
            EligiblePropCells = eligiblePropCells;
            Stage = stage;
            EdgeRule = edgeRule;
            LitRingCells = litRingCells ?? Array.Empty<Vector2Int>();
            DeckCellCount = deckCellCount;
            Dressed = dressed;
        }

        /// <summary>Every lattice cell that carries a base tile: all deck cells plus the extra front rows the edge V needs.</summary>
        public IReadOnlyList<Vector2Int> BaseCells { get; }

        /// <summary>The cells of the locations in id order; each carries a deck or hatch tile over the base.</summary>
        public IReadOnlyList<Vector2Int> LocationCells { get; }

        public IReadOnlyList<DeckCellArt> LocationArt { get; }

        public IReadOnlyList<DeckEdgePlacement> Edges { get; }

        public IReadOnlyList<DeckPropPlacement> Props { get; }

        /// <summary>The cells a prop may stand in: the margin ring and the empty or locked location cells, without the depot and the deposit cell.</summary>
        public int EligiblePropCells { get; }

        public int Stage { get; }

        public EdgeRule EdgeRule { get; }

        /// <summary>The tiles the two tilemaps hold: base cells plus one deck or hatch tile per location.</summary>
        public int TileCount => BaseCells.Count + LocationCells.Count;

        /// <summary>
        /// True when the plan carries the stage dressing of T540 (R15): the margin ring is lit stage by stage and props stand only on deck. Without it (tests and
        /// tools that pass no dressing) the ring stays dark and every ring cell is eligible, as T527 built it.
        /// </summary>
        public bool Dressed { get; }

        /// <summary>
        /// The margin-ring cells that show the lit deck tile at this stage (stage-driven deck-lit area, R15). Each is a base cell whose tile is the deck tile instead
        /// of the dark base tile, so the tile count does not change. Props stand only on these cells and on empty or locked location cells, so none floats over the
        /// dark base.
        /// </summary>
        public IReadOnlyList<Vector2Int> LitRingCells { get; }

        /// <summary>The cells of the deck rectangle (the lattice plus the margin band), which is what the lit share counts.</summary>
        public int DeckCellCount { get; }

        /// <summary>The cells that show the lit deck: every unlocked location and every lit ring cell.</summary>
        public int LitCellCount
        {
            get
            {
                var lit = LitRingCells.Count;
                for (var i = 0; i < LocationArt.Count; i++)
                {
                    lit += LocationArt[i] == DeckCellArt.Deck ? 1 : 0;
                }

                return lit;
            }
        }

        /// <summary>The deck-lit share of the deck rectangle, in [0, 1] (SC-006, R15: at least 25 percentage points between adjacent stages).</summary>
        public float LitShare => DeckCellCount <= 0 ? 0f : (float)LitCellCount / DeckCellCount;
    }

    /// <summary>
    /// The stage dressing of the deck (T540, R15): the secondary density of the development stage. Passing one to
    /// <see cref="DeckPlanner.Build(FactoryWorldLayout, IReadOnlyList{SlotCondition}, int, WorldCompositionSettings, DeckDressing?)"/> switches the stage-driven
    /// lit ring and the dressed prop rules on.
    /// </summary>
    public readonly struct DeckDressing
    {
        public DeckDressing(int densityBin)
        {
            DensityBin = densityBin < 0 ? 0 : densityBin;
        }

        /// <summary>The activity and level bins together (0 to 4): the stage prop density grows by <see cref="DeckPlanner.DensityStepPerBin"/> per bin.</summary>
        public int DensityBin { get; }
    }

    /// <summary>
    /// The deterministic deck planner (T527). Pure: it reads the layout, the settings and the registry rows, allocates only its result and has no
    /// engine objects, so Edit Mode tests measure it. Everything is placed on whole art pixels (rule P3).
    /// <para>
    /// <b>Props</b> (R06, contracts section 1): only in the margin ring and on empty or locked location cells; at most 24 art px tall
    /// (<c>maxPropHeightArtPx</c>); chosen by a cell hash against the stage density with kinds drawn by the stage weights; never inside a
    /// hit shape, a badge or marker keep-out (grown by the 1 px C8 ring), a lane, the depot, the deposit or an edge piece; at most half an
    /// eligible cell on average; always drawn under machines.
    /// </para>
    /// <para>
    /// <b>Edges</b> (Art Bible 21.9 item 5, P2-F7/P2-F23): front margin row only. The front-left (<c>fl</c>) and front-right (<c>fr</c>) pieces
    /// run along the two iso diagonals one half cell apart (35 x 17 art px steps, 2:1), the corner piece sits at the front apex. The upper ends
    /// of the V sit on the front margin row, or lower when the stage's pieces would otherwise reach the content extent, so every piece lies
    /// below everything the lattice draws. No back, left or right piece exists, because those fail the keep-out proof.
    /// </para>
    /// </summary>
    public static class DeckPlanner
    {
        /// <summary>The upper bound of props per eligible cell (Art Bible 21.3 world sprite budget).</summary>
        public const float MaxPropsPerEligibleCell = 0.5f;

        /// <summary>The extra prop density each secondary bin (activity, level) adds on top of the stage density (T540). The 0.5 cap still holds.</summary>
        public const float DensityStepPerBin = 0.03f;

        public const int EdgeStepXPixels = 35;
        public const int EdgeStepYPixels = 17;

        /// <summary>Clearance in art pixels between the top of an edge piece's opaque pixels and the content extent or the depot.</summary>
        public const int EdgeClearancePixels = 6;

        /// <summary>Half the thickness a trunk or branch is treated as covering when props are kept off lanes (the lane art is 5 px thick; 8 is the guard band).</summary>
        public const int LaneGuardPixels = 8;

        /// <summary>The C8 ring around a keep-out box.</summary>
        public const int RingPixels = 1;

        private const int CandidateTries = 24;

        // The opaque top of each edge family's pieces above the sprite pivot, in art pixels, measured from the imported PNGs (the canvases carry
        // transparent headroom). DeckLayoutTests re-measures them against the pixels, so a re-authored sprite fails the test, not the look.
        private static readonly int[] EdgeOpaqueTopAbovePivot = { 17, 17, 30 };

        private static readonly string[] EdgeFamily = { "RAILING", "WALL", "GANTRY" };

        /// <summary>The opaque top above the pivot of an edge rule's pieces, art pixels.</summary>
        public static int EdgeOpaqueTop(EdgeRule rule) => EdgeOpaqueTopAbovePivot[(int)rule];

        /// <summary>The catalog key of an edge piece: <paramref name="piece"/> is "FL", "FR", "CORNER", "QUIET-FL" or "QUIET-FR".</summary>
        public static string EdgeKey(EdgeRule rule, string piece) => "F006-EDGE-" + EdgeFamily[(int)rule] + "-" + piece;

        /// <summary>The catalog key of a prop kind (<c>crate-small</c> gives <c>F006-PROP-CRATE-SMALL</c>).</summary>
        public static string PropKey(string kind) => "F006-PROP-" + kind.ToUpperInvariant();

        public static uint Hash(int x, int y, uint salt)
        {
            unchecked
            {
                var h = 2166136261u;
                h = (h ^ (uint)x) * 16777619u;
                h = (h ^ (uint)y) * 16777619u;
                h = (h ^ salt) * 16777619u;
                h ^= h >> 15;
                h *= 2246822519u;
                h ^= h >> 13;
                h *= 3266489917u;
                h ^= h >> 16;
                return h;
            }
        }

        /// <summary>The hash as a number in [0, 1).</summary>
        public static float Hash01(int x, int y, uint salt) => (Hash(x, y, salt) >> 8) / 16777216f;

        /// <summary>The cell rectangle (70 x 54 art px) of a lattice cell in world art pixels.</summary>
        public static PixelRect CellRect(Vector2Int cell) => PixelRect.FromWorld(FactoryWorldLayout.DeckTileRect(cell));

        /// <summary>The whole-pixel footprint pivot of a lattice cell (the centre of its deck tile).</summary>
        public static Vector2Int CellPivot(Vector2Int cell) => new Vector2Int(
            SlotGridLayout.PlateWidthPixels / 2 + cell.x * SlotGridLayout.PitchXPixels,
            SlotGridLayout.PivotAbovePlateBottomPixels + cell.y * SlotGridLayout.PitchYPixels);

        /// <summary>
        /// Builds the plan. <paramref name="conditions"/> has one entry per location (a shorter list means the rest are locked, a null list all
        /// locked); <paramref name="settings"/> null means the F004-reproducing defaults, which carry no props.
        /// </summary>
        public static DeckPlan Build(FactoryWorldLayout layout, IReadOnlyList<SlotCondition> conditions, int stage, WorldCompositionSettings settings) =>
            Build(layout, conditions, stage, settings, null);

        /// <summary>
        /// Builds the plan with the stage dressing of T540 when <paramref name="dressing"/> is set: the margin ring is lit by stage (none at Early, the half
        /// nearest the depot at Partial, all of it at Established), props stand only on lit deck, and the secondary density bins add prop density.
        /// </summary>
        public static DeckPlan Build(FactoryWorldLayout layout, IReadOnlyList<SlotCondition> conditions, int stage, WorldCompositionSettings settings, DeckDressing? dressing)
        {
            if (layout == null)
            {
                throw new ArgumentNullException(nameof(layout));
            }

            stage = Mathf.Clamp(stage, 0, WorldCompositionSettings.StageCount - 1);
            var rule = settings != null ? settings.EdgeRuleOf(stage) : (EdgeRule)stage;
            var edges = BuildEdges(layout, rule);
            var extraRows = ExtraFrontRows(layout, edges);

            var baseCells = new List<Vector2Int>();
            var band = layout.DeckBandCells;
            for (var row = -band - extraRows; row < layout.RowCount + band; row++)
            {
                for (var column = -band; column < layout.ColumnCount + band; column++)
                {
                    baseCells.Add(new Vector2Int(column, row));
                }
            }

            var locationCells = new Vector2Int[layout.LocationCount];
            var art = new DeckCellArt[layout.LocationCount];
            for (var i = 0; i < locationCells.Length; i++)
            {
                var id = new BuildingLocationId(i);
                locationCells[i] = layout.CellOf(id);
                var condition = conditions != null && i < conditions.Count ? conditions[i] : SlotCondition.Locked;
                art[i] = condition == SlotCondition.Locked ? DeckCellArt.Hatch : DeckCellArt.Deck;
            }

            IReadOnlyList<Vector2Int> litRing = Array.Empty<Vector2Int>();
            HashSet<Vector2Int> litSet = null;
            if (dressing.HasValue)
            {
                litRing = LitRingCells(layout, stage);
                litSet = new HashSet<Vector2Int>(litRing);
            }

            var props = BuildProps(layout, conditions, stage, settings, edges, baseCells, litSet, dressing, out var eligible);
            return new DeckPlan(baseCells, locationCells, art, edges, props, eligible, stage, rule, litRing, layout.DeckCells.Count, dressing.HasValue);
        }

        /// <summary>
        /// The margin-ring cells in the order the lit deck grows (R15): every deck cell that is not a location, the depot cell or the deposit cell, nearest the depot
        /// first (the yard grows outward from where the trunks end), then by row and column. Deterministic and independent of any state.
        /// </summary>
        public static IReadOnlyList<Vector2Int> RingCellsInLightingOrder(FactoryWorldLayout layout)
        {
            var locations = new HashSet<Vector2Int>();
            for (var i = 0; i < layout.LocationCount; i++)
            {
                locations.Add(layout.CellOf(new BuildingLocationId(i)));
            }

            var ring = new List<Vector2Int>();
            foreach (var cell in layout.DeckCells)
            {
                if (cell != layout.DepotCell && cell != layout.DepositCell && !locations.Contains(cell))
                {
                    ring.Add(cell);
                }
            }

            var depot = layout.DepotCell;
            ring.Sort((a, b) =>
            {
                var da = Mathf.Abs(a.x - depot.x) + Mathf.Abs(a.y - depot.y);
                var db = Mathf.Abs(b.x - depot.x) + Mathf.Abs(b.y - depot.y);
                if (da != db)
                {
                    return da.CompareTo(db);
                }

                return a.y != b.y ? a.y.CompareTo(b.y) : a.x.CompareTo(b.x);
            });
            return ring;
        }

        /// <summary>How many of <paramref name="ringCount"/> ring cells are lit at a stage: none at Early, the nearer half (rounded up) at Partial, all at Established.</summary>
        public static int LitRingCount(int ringCount, int stage) =>
            stage <= 0 ? 0 : stage == 1 ? (ringCount + 1) / 2 : ringCount;

        /// <summary>The lit ring cells of a stage, in lighting order.</summary>
        public static IReadOnlyList<Vector2Int> LitRingCells(FactoryWorldLayout layout, int stage)
        {
            var ring = RingCellsInLightingOrder(layout);
            var count = LitRingCount(ring.Count, Mathf.Clamp(stage, 0, WorldCompositionSettings.StageCount - 1));
            var lit = new Vector2Int[count];
            for (var i = 0; i < count; i++)
            {
                lit[i] = ring[i];
            }

            return lit;
        }

        // ------------------------------------------------------------------------------------------------------------------------------
        // Edges
        // ------------------------------------------------------------------------------------------------------------------------------

        /// <summary>The piece count per diagonal: one per lattice column, so a diagonal reaches from the apex to the lattice edge.</summary>
        public static int EdgePiecesPerSide(FactoryWorldLayout layout) => layout.ColumnCount;

        public static IReadOnlyList<DeckEdgePlacement> BuildEdges(FactoryWorldLayout layout, EdgeRule rule)
        {
            var result = new List<DeckEdgePlacement>();
            var perSide = EdgePiecesPerSide(layout);
            var frontMarginPivotY = CellPivot(new Vector2Int(0, -1)).y;
            var topBound = -EdgeClearancePixels - EdgeOpaqueTop(rule);
            var endY = Mathf.Min(frontMarginPivotY, topBound);
            var apexX = SlotGridLayout.PitchXPixels * layout.ColumnCount / 2;
            var apexY = endY - EdgeStepYPixels * perSide;
            var depotX = Mathf.RoundToInt(layout.DepotAnchor.x * SlotGridLayout.Density);

            // Apex corner first (it draws last: highest order), then the pieces from the ends toward the apex.
            var order = 0;
            for (var k = perSide; k >= 1; k--)
            {
                result.Add(Piece(rule, "FL", apexX - EdgeStepXPixels * k, apexY + EdgeStepYPixels * k, depotX, order++));
                result.Add(Piece(rule, "FR", apexX + EdgeStepXPixels * k, apexY + EdgeStepYPixels * k, depotX, order++));
            }

            result.Add(Piece(rule, "CORNER", apexX, apexY, depotX, order));
            return result;
        }

        private static DeckEdgePlacement Piece(EdgeRule rule, string piece, int x, int y, int depotX, int order)
        {
            // The quiet railing variants carry fewer posts near the depot (UI/UX F6, Art Director A4): used for a piece within one lattice cell of it.
            var quiet = rule == EdgeRule.Railing && piece != "CORNER" && Mathf.Abs(x - depotX) <= SlotGridLayout.PitchXPixels;
            var key = EdgeKey(rule, quiet ? "QUIET-" + piece : piece);
            DeckSpriteSize.TryOf(key, out var size);
            var anchor = new Vector2Int(x, y);
            return new DeckEdgePlacement(key, anchor, size.RectAt(anchor), order);
        }

        private static int ExtraFrontRows(FactoryWorldLayout layout, IReadOnlyList<DeckEdgePlacement> edges)
        {
            var lowest = int.MaxValue;
            foreach (var edge in edges)
            {
                lowest = Mathf.Min(lowest, edge.Anchor.y);
            }

            var rows = 0;
            // The deck field must reach the lowest pivot (the rail line of the apex), so the V never floats over bare background.
            while (rows < 8 && CellPivot(new Vector2Int(0, -layout.DeckBandCells - rows)).y - SlotGridLayout.FloorTilePivotYPixels > lowest)
            {
                rows++;
            }

            return rows;
        }

        // ------------------------------------------------------------------------------------------------------------------------------
        // Props
        // ------------------------------------------------------------------------------------------------------------------------------

        private sealed class Obstacles
        {
            public readonly List<PixelRect> Rects = new List<PixelRect>();
            public readonly List<PixelRect> Cells = new List<PixelRect>();
            public readonly List<Vector2> DiamondCentres = new List<Vector2>();
            public readonly List<Vector2> DiamondHalves = new List<Vector2>();
            public PixelRect Coverage;
        }

        /// <summary>The cells a prop may stand in for the given conditions (cell, whether it is a location cell), in deck order.</summary>
        public static IReadOnlyList<Vector2Int> EligibleCells(FactoryWorldLayout layout, IReadOnlyList<SlotCondition> conditions) =>
            EligibleCells(layout, conditions, null);

        /// <summary>
        /// The eligible cells when the stage dressing is on: a ring cell is eligible only when it is lit (<paramref name="litRing"/>), so a prop never stands over the
        /// dark base (T540). A null set keeps the T527 rule (every ring cell is eligible).
        /// </summary>
        public static IReadOnlyList<Vector2Int> EligibleCells(FactoryWorldLayout layout, IReadOnlyList<SlotCondition> conditions, ISet<Vector2Int> litRing)
        {
            var result = new List<Vector2Int>();
            foreach (var cell in layout.DeckCells)
            {
                if (cell == layout.DepotCell || cell == layout.DepositCell)
                {
                    continue;
                }

                if (IsLocationCell(layout, cell, out var id))
                {
                    var condition = conditions != null && id < conditions.Count ? conditions[id] : SlotCondition.Locked;
                    if (condition == SlotCondition.Occupied)
                    {
                        continue;
                    }
                }
                else if (litRing != null && !litRing.Contains(cell))
                {
                    continue;
                }

                result.Add(cell);
            }

            return result;
        }

        private static bool IsLocationCell(FactoryWorldLayout layout, Vector2Int cell, out int id)
        {
            for (var i = 0; i < layout.LocationCount; i++)
            {
                if (layout.CellOf(new BuildingLocationId(i)) == cell)
                {
                    id = i;
                    return true;
                }
            }

            id = -1;
            return false;
        }

        private static Obstacles CollectObstacles(FactoryWorldLayout layout, IReadOnlyList<DeckEdgePlacement> edges, IReadOnlyList<Vector2Int> baseCells)
        {
            var obstacles = new Obstacles();
            var minX = int.MaxValue;
            var minY = int.MaxValue;
            var maxX = int.MinValue;
            var maxY = int.MinValue;
            foreach (var cell in baseCells)
            {
                var rect = CellRect(cell);
                minX = Mathf.Min(minX, rect.X);
                minY = Mathf.Min(minY, rect.Y);
                maxX = Mathf.Max(maxX, rect.XMax);
                maxY = Mathf.Max(maxY, rect.YMax);
            }

            obstacles.Coverage = new PixelRect(minX, minY, maxX - minX, maxY - minY);
            for (var i = 0; i < layout.LocationCount; i++)
            {
                var id = new BuildingLocationId(i);
                var shape = layout.HitShapeOf(id);
                obstacles.Cells.Add(PixelRect.FromWorld(shape.Cell));
                obstacles.DiamondCentres.Add(shape.Centre * SlotGridLayout.Density);
                obstacles.DiamondHalves.Add(new Vector2(shape.HalfWidth, shape.HalfHeight) * SlotGridLayout.Density);
                for (var k = 0; k < FactoryWorldLayout.KeepOutCount; k++)
                {
                    obstacles.Rects.Add(PixelRect.FromWorld(layout.KeepOutOf(id, k)).Grown(RingPixels));
                }

                AddLane(obstacles, layout.BranchOf(id));
            }

            for (var row = 0; row < layout.RowCount; row++)
            {
                AddLane(obstacles, layout.TrunkOf(row));
            }

            if (DeckSpriteSize.TryOf("F006-DEPOT-S1-EMPTY", out var depot))
            {
                obstacles.Rects.Add(depot.RectAt(new Vector2Int(Mathf.RoundToInt(layout.DepotAnchor.x * SlotGridLayout.Density), Mathf.RoundToInt(layout.DepotAnchor.y * SlotGridLayout.Density))).Grown(1));
            }

            obstacles.Rects.Add(CellRect(layout.DepositCell));
            foreach (var edge in edges)
            {
                obstacles.Rects.Add(edge.Rect);
            }

            return obstacles;
        }

        private static void AddLane(Obstacles obstacles, LanePath lane)
        {
            if (lane == null || lane.IsEmpty)
            {
                return;
            }

            for (var i = 0; i < lane.SegmentCount; i++)
            {
                obstacles.Rects.Add(PixelRect.FromWorld(lane.SegmentRect(i)).Grown(LaneGuardPixels));
            }
        }

        private static bool Blocked(Obstacles obstacles, PixelRect rect)
        {
            if (!obstacles.Coverage.Contains(rect))
            {
                return true;
            }

            foreach (var other in obstacles.Rects)
            {
                if (rect.Overlaps(other))
                {
                    return true;
                }
            }

            for (var i = 0; i < obstacles.Cells.Count; i++)
            {
                var cell = obstacles.Cells[i];
                if (!rect.Overlaps(cell))
                {
                    continue;
                }

                // The hit shape is the diamond clipped to its cell: the rectangle meets it when the nearest point of the clipped
                // rectangle lies inside the diamond (the scaled L1 distance is separable, so clamping each axis finds the minimum).
                var x0 = Mathf.Max(rect.X, cell.X);
                var x1 = Mathf.Min(rect.XMax, cell.XMax);
                var y0 = Mathf.Max(rect.Y, cell.Y);
                var y1 = Mathf.Min(rect.YMax, cell.YMax);
                var centre = obstacles.DiamondCentres[i];
                var half = obstacles.DiamondHalves[i];
                var nearestX = Mathf.Clamp(centre.x, x0, x1);
                var nearestY = Mathf.Clamp(centre.y, y0, y1);
                if (Mathf.Abs(nearestX - centre.x) / half.x + Mathf.Abs(nearestY - centre.y) / half.y <= 1f)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// The number of free art pixels of a cell for a 1 x 1 art-pixel prop footprint: the cell minus every hit shape, keep-out ring, lane guard band,
        /// the depot, the deposit and the edge pieces of the stage. This is the prop mask the free-area measurement reports (DeckLayoutTests).
        /// </summary>
        public static int FreeAreaPixels(FactoryWorldLayout layout, Vector2Int cell, int stage, WorldCompositionSettings settings)
        {
            stage = Mathf.Clamp(stage, 0, WorldCompositionSettings.StageCount - 1);
            var rule = settings != null ? settings.EdgeRuleOf(stage) : (EdgeRule)stage;
            var edges = BuildEdges(layout, rule);
            var baseCells = new List<Vector2Int>(layout.DeckCells);
            var obstacles = CollectObstacles(layout, edges, baseCells);
            var rect = CellRect(cell);
            var free = 0;
            for (var y = rect.Y; y < rect.YMax; y++)
            {
                for (var x = rect.X; x < rect.XMax; x++)
                {
                    free += Blocked(obstacles, new PixelRect(x, y, 1, 1)) ? 0 : 1;
                }
            }

            return free;
        }

        private static List<DeckPropPlacement> BuildProps(
            FactoryWorldLayout layout,
            IReadOnlyList<SlotCondition> conditions,
            int stage,
            WorldCompositionSettings settings,
            IReadOnlyList<DeckEdgePlacement> edges,
            IReadOnlyList<Vector2Int> baseCells,
            ISet<Vector2Int> litRing,
            DeckDressing? dressing,
            out int eligibleCount)
        {
            var placements = new List<DeckPropPlacement>();
            var eligible = EligibleCells(layout, conditions, litRing);
            eligibleCount = eligible.Count;
            if (settings == null)
            {
                return placements;
            }

            var density = settings.PropDensityOf(stage);
            if (dressing.HasValue)
            {
                density = Mathf.Min(MaxPropsPerEligibleCell, density + DensityStepPerBin * dressing.Value.DensityBin);
            }

            var weights = settings.PropWeightsOf(stage);
            var totalWeight = 0f;
            foreach (var weight in weights)
            {
                if (weight.weight > 0f && DeckSpriteSize.TryOf(PropKey(weight.kind), out var size) && size.Height <= settings.maxPropHeightArtPx)
                {
                    totalWeight += weight.weight;
                }
            }

            if (density <= 0f || totalWeight <= 0f)
            {
                return placements;
            }

            var obstacles = CollectObstacles(layout, edges, baseCells);
            var chosen = new List<(Vector2Int Cell, float Roll)>();
            foreach (var cell in eligible)
            {
                var roll = Hash01(cell.x, cell.y, 0xA1u);
                if (roll < density)
                {
                    chosen.Add((cell, roll));
                }
            }

            // At most half an eligible cell on average (Art Bible 21.3): the lowest rolls win, so a denser stage only adds props.
            // With the dressing on, the census cap counts at most the margin ring: 0.5 per ring cell is the 9 props of the 41-sprite budget at 12 locations (Art Bible
            // 21.12), however many empty location cells are eligible besides.
            var capBase = dressing.HasValue ? Mathf.Min(eligible.Count, layout.DeckCells.Count - layout.LocationCount) : eligible.Count;
            var cap = Mathf.FloorToInt(capBase * MaxPropsPerEligibleCell);
            chosen.Sort((a, b) => a.Roll.CompareTo(b.Roll));
            if (chosen.Count > cap)
            {
                chosen.RemoveRange(cap, chosen.Count - cap);
            }

            var taken = new List<PixelRect>();
            foreach (var entry in chosen)
            {
                var cell = entry.Cell;
                var key = PickKind(cell, weights, totalWeight, settings.maxPropHeightArtPx);
                if (key == null || !DeckSpriteSize.TryOf(key, out var size))
                {
                    continue;
                }

                var cellRect = CellRect(cell);
                if (!FitsInCell(cellRect, size))
                {
                    // A kind wider or taller than the cell cannot sit inside it: skip it (no modulus by zero or a wrapped negative span).
                    continue;
                }

                for (var attempt = 0; attempt < CandidateTries; attempt++)
                {
                    var x0 = cellRect.X + PropOffset(cell, attempt, 0xB100u, cellRect.Width - size.Width);
                    var y0 = cellRect.Y + PropOffset(cell, attempt, 0xC200u, cellRect.Height - size.Height);
                    var rect = new PixelRect(x0, y0, size.Width, size.Height);
                    if (Blocked(obstacles, rect) || Overlaps(taken, rect))
                    {
                        continue;
                    }

                    taken.Add(rect);
                    placements.Add(new DeckPropPlacement(key, cell, new Vector2Int(x0 + size.PivotX, y0 + size.PivotY), rect));
                    break;
                }
            }

            return placements;
        }

        /// <summary>True when a sprite of <paramref name="size"/> fits inside <paramref name="cellRect"/> (a wider or taller kind is skipped).</summary>
        public static bool FitsInCell(PixelRect cellRect, DeckSpriteSize size) => size.Width <= cellRect.Width && size.Height <= cellRect.Height;

        /// <summary>The deterministic offset (0 to <paramref name="span"/> inclusive) of a candidate; 0 when the span is not positive (no modulus by 0).</summary>
        public static int PropOffset(Vector2Int cell, int attempt, uint salt, int span) =>
            span <= 0 ? 0 : (int)(Hash(cell.x, cell.y, salt + (uint)attempt) % (uint)(span + 1));

        private static bool Overlaps(List<PixelRect> taken, PixelRect rect)
        {
            foreach (var other in taken)
            {
                if (rect.Overlaps(other))
                {
                    return true;
                }
            }

            return false;
        }

        private static string PickKind(Vector2Int cell, IReadOnlyList<PropWeight> weights, float totalWeight, int maxHeight)
        {
            var target = Hash01(cell.x, cell.y, 0xD3u) * totalWeight;
            string last = null;
            foreach (var weight in weights)
            {
                var key = PropKey(weight.kind);
                if (weight.weight <= 0f || !DeckSpriteSize.TryOf(key, out var size) || size.Height > maxHeight)
                {
                    continue;
                }

                last = key;
                if (target < weight.weight)
                {
                    return key;
                }

                target -= weight.weight;
            }

            return last;
        }
    }
}
