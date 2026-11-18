using System;
using System.Collections.Generic;
using IdleFactory.Run.Application;
using UnityEngine;

namespace IdleFactory.Unity.Presentation
{
    /// <summary>The LINK tile a lattice cell of the link layer carries (the plan): a plain trunk piece or the junction at the depot column.</summary>
    public enum LinkTileKind
    {
        /// <summary>A static row trunk piece (<c>F006-LINK-TRUNK</c>). On the cell of an occupied location the view swaps it for the branch tile.</summary>
        Trunk = 0,

        /// <summary>The trunk end at the depot column (<c>F006-LINK-JUNCTION</c>): the row trunk turns into the collector spine.</summary>
        Junction = 1,
    }

    /// <summary>One tile of the link layer: its lattice cell, its kind and, for a trunk piece on a location's cell, that location (otherwise -1).</summary>
    public readonly struct LinkTile
    {
        public LinkTile(Vector2Int cell, LinkTileKind kind, bool mirrored, int location)
        {
            Cell = cell;
            Kind = kind;
            Mirrored = mirrored;
            Location = location;
        }

        public Vector2Int Cell { get; }
        public LinkTileKind Kind { get; }

        /// <summary>True for a junction of a depot that lies left of the lattice: the horizontal arm comes from the right, so the tile is drawn flipped.</summary>
        public bool Mirrored { get; }

        /// <summary>The location whose cell this is (the cell where its branch shows when it is occupied), or -1.</summary>
        public int Location { get; }
    }

    /// <summary>What a location's branch shows now.</summary>
    public enum BranchState
    {
        /// <summary>No branch: the location is empty or locked (FR-013: a branch exists for an occupied location only).</summary>
        None = 0,

        /// <summary>A branch of an occupied location whose machine is not Running: the plain branch tile, no pip.</summary>
        Idle = 1,

        /// <summary>A branch of a Running machine: a pip travels it (normal motion) or the static active tile shows (reduced motion).</summary>
        Running = 2,
    }

    /// <summary>The four depot fill sprites of a stage (Art Bible 21.4): empty, low, high and the full state.</summary>
    public enum DepotFillState
    {
        Empty = 0,
        Low = 1,
        High = 2,
        Full = 3,
    }

    /// <summary>
    /// The static geometry of the link layer for one layout (T528, FR-013, research R07, contracts/world-presentation.md section 2): a trunk per row in the
    /// 19 art px row gap toward the depot, a junction on the depot column, and per location the pip path along its branch. It depends on the layout alone, so
    /// trunks are static by construction: no condition, state or time enters. An immutable value built by <see cref="DepotLinkGeometry"/>.
    /// </summary>
    public sealed class DepotLinkPlan
    {
        private readonly int[] pipStartY;
        private readonly int[] pipEndY;
        private readonly int[] pipX;

        public DepotLinkPlan(IReadOnlyList<LinkTile> tiles, int[] pipX, int[] pipStartY, int[] pipEndY)
        {
            Tiles = tiles;
            this.pipX = pipX;
            this.pipStartY = pipStartY;
            this.pipEndY = pipEndY;
        }

        /// <summary>The tiles of the link layer, row by row from the back.</summary>
        public IReadOnlyList<LinkTile> Tiles { get; }

        public int LocationCount => pipX.Length;

        /// <summary>The world art pixel column of a location's branch (the plate centre line).</summary>
        public int PipX(int location) => pipX[location];

        /// <summary>The world art pixel row where a pip enters a location's branch: the plate's rear edge.</summary>
        public int PipStartY(int location) => pipStartY[location];

        /// <summary>The world art pixel row where a pip leaves a location's branch: the lane of its row trunk.</summary>
        public int PipEndY(int location) => pipEndY[location];
    }

    /// <summary>
    /// Builds the <see cref="DepotLinkPlan"/> of a layout and holds the placement constants of the link tiles. Pure: no engine object, so Edit Mode tests
    /// measure it on the 3 x 4, 4 x 3, 6, 20 and L layouts.
    /// </summary>
    public static class DepotLinkGeometry
    {
        /// <summary>A link tile is 70 x 54 art px and sits 12 art px above its cell pivot (centre), so its bottom is 15 px below the pivot: the placement the art pipeline's ring proof uses.</summary>
        public const int TileCentreAbovePivotPixels = 12;

        /// <summary>The trunk's lane centre row inside the tile, in art px above the cell pivot (rows 8 to 12 of the tile are the lane: 27 to 31 above the pivot, centre 29).</summary>
        public const int TrunkCentreAbovePivotPixels = FactoryWorldLayout.LaneAbovePivotPixels;

        /// <summary>Builds the plan of a layout: trunk tiles from the row's far location to the depot column, a junction there, and the pip path of every branch.</summary>
        public static DepotLinkPlan Build(FactoryWorldLayout layout)
        {
            if (layout == null)
            {
                throw new ArgumentNullException(nameof(layout));
            }

            var tiles = new List<LinkTile>();
            var depotColumn = layout.DepotCell.x;
            var locationOfCell = new Dictionary<Vector2Int, int>(layout.LocationCount);
            for (var i = 0; i < layout.LocationCount; i++)
            {
                locationOfCell[layout.CellOf(new BuildingLocationId(i))] = i;
            }

            for (var row = 0; row < layout.RowCount; row++)
            {
                if (layout.TrunkOf(row).IsEmpty)
                {
                    continue;
                }

                var rowFromBottom = layout.RowCount - 1 - row;
                var low = int.MaxValue;
                var high = int.MinValue;
                for (var column = 0; column < layout.ColumnCount; column++)
                {
                    if (locationOfCell.ContainsKey(new Vector2Int(column, rowFromBottom)))
                    {
                        low = Math.Min(low, column);
                        high = Math.Max(high, column);
                    }
                }

                var from = layout.DepotOnRight ? low : depotColumn;
                var to = layout.DepotOnRight ? depotColumn : high;
                for (var column = from; column <= to; column++)
                {
                    var cell = new Vector2Int(column, rowFromBottom);
                    if (column == depotColumn)
                    {
                        tiles.Add(new LinkTile(cell, LinkTileKind.Junction, !layout.DepotOnRight, -1));
                    }
                    else
                    {
                        tiles.Add(new LinkTile(cell, LinkTileKind.Trunk, false, locationOfCell.TryGetValue(cell, out var location) ? location : -1));
                    }
                }
            }

            var count = layout.LocationCount;
            var x = new int[count];
            var y0 = new int[count];
            var y1 = new int[count];
            for (var i = 0; i < count; i++)
            {
                var branch = layout.BranchOf(new BuildingLocationId(i));
                x[i] = Mathf.RoundToInt(branch.Start.x * SlotGridLayout.Density);
                y0[i] = Mathf.RoundToInt(branch.Start.y * SlotGridLayout.Density);
                y1[i] = Mathf.RoundToInt(branch.End.y * SlotGridLayout.Density);
            }

            return new DepotLinkPlan(tiles, x, y0, y1);
        }

        /// <summary>The world art pixel of a tile's centre: 12 px above the cell pivot.</summary>
        public static Vector2Int TileCentre(Vector2Int cell)
        {
            var pivot = DeckPlanner.CellPivot(cell);
            return new Vector2Int(pivot.x, pivot.y + TileCentreAbovePivotPixels);
        }

        /// <summary>The world art pixel row of the lane centre of the trunk tile on a cell.</summary>
        public static int LaneRowOf(Vector2Int cell) => DeckPlanner.CellPivot(cell).y + TrunkCentreAbovePivotPixels;

        public static string TileKey(LinkTileKind kind) => kind == LinkTileKind.Junction ? "F006-LINK-JUNCTION" : "F006-LINK-TRUNK";

        public const string BranchKey = "F006-LINK-BRANCH";
        public const string BranchActiveKey = "F006-LINK-BRANCH-ACTIVE";
        public const string PipKey = "F006-FX-BRANCH-PIP";

        private static readonly string[] DepotKeys = BuildDepotKeys();

        private static string[] BuildDepotKeys()
        {
            var names = new[] { "EMPTY", "LOW", "HIGH", "FULL" };
            var keys = new string[WorldCompositionSettings.StageCount * names.Length];
            for (var stage = 0; stage < WorldCompositionSettings.StageCount; stage++)
            {
                for (var state = 0; state < names.Length; state++)
                {
                    keys[stage * names.Length + state] = "F006-DEPOT-S" + (stage + 1) + "-" + names[state];
                }
            }

            return keys;
        }

        /// <summary>The catalog key of a depot sprite: stage 0 to 2 (the sprites are S1 to S3) times the fill state.</summary>
        public static string DepotKey(int stage, DepotFillState state)
        {
            stage = Mathf.Clamp(stage, 0, WorldCompositionSettings.StageCount - 1);
            return DepotKeys[stage * 4 + (int)state];
        }

        /// <summary>The catalog key of a deposit rest sprite for a stage (0 to 2 give S1 to S3).</summary>
        public static string DepositRestKey(int stage) => DepositRestKeys[Mathf.Clamp(stage, 0, DepositRestKeys.Length - 1)];

        private static readonly string[] DepositRestKeys = { "F006-DEPOSIT-REST-S1", "F006-DEPOSIT-REST-S2", "F006-DEPOSIT-REST-S3" };

        public const string DepositHighlightKey = "F006-DEPOSIT-HIGHLIGHT";
        public const string VeinChipKey = "F006-FX-VEIN-CHIP";

        /// <summary>
        /// The depot fill index of the contract (FR-013, R15): <c>min(3, floor(4 x fill / capacity))</c>. An empty store or a store without capacity is 0;
        /// a full store is 3. The index selects <see cref="DepotFillState"/> one to one.
        /// </summary>
        public static int FillIndexOf(long fill, long capacity)
        {
            if (capacity <= 0 || fill <= 0)
            {
                return 0;
            }

            if (fill >= capacity)
            {
                return 3;
            }

            var index = (long)(fill * 4m / capacity);
            return index > 3 ? 3 : (int)index;
        }

        /// <summary>
        /// The whole-pixel step of a pip along its branch at presentation time <paramref name="milliseconds"/>: it crosses the branch in
        /// <see cref="MachinePresentationModel.PipMillisecondsPerCell"/> milliseconds (800, 600 or 400 by pace; the branch is one lane cell) and starts again.
        /// 0 is the plate's rear edge, <paramref name="steps"/> - 1 the trunk lane.
        /// </summary>
        public static int PipStep(long milliseconds, ActivityPace pace, int steps)
        {
            if (steps <= 1)
            {
                return 0;
            }

            var period = MachinePresentationModel.PipMillisecondsPerCell(pace);
            var within = (int)(((milliseconds % period) + period) % period);
            return within * steps / period;
        }
    }

    /// <summary>
    /// What the link layer and the depot show this frame (T528, FR-013, FR-029, R07). A plain C# model: no scene, deterministic, allocation-free once its
    /// buffers are sized. It reads the machine rows of the session (<see cref="RunSession.CopyMachineStates(MachineStateView[])"/>) every frame, so a pip
    /// stops in the frame after a machine stops Running (the world presentation model is up to one second old and is not used for state), and takes only
    /// the pace from the presentation model, because a pace change applies from the next loop start anyway (R12).
    /// <para>
    /// Rules: a branch exists for an occupied location only; it runs while the machine is Running (<see cref="SlotGridModel.StateFor"/>, the single state
    /// precedence); the depot shows the fill index of the shared store, and the full state when the store is full or any machine cannot store its output.
    /// Nothing here is saved and no economy rule reads it.
    /// </para>
    /// </summary>
    public sealed class DepotLinkModel
    {
        private BranchState[] branches = Array.Empty<BranchState>();
        private ActivityPace[] paces = Array.Empty<ActivityPace>();
        private int locationCount;
        private int fillIndex;
        private bool storeFull;
        private int runningCount;
        private int occupiedCount;

        public int LocationCount => locationCount;

        /// <summary>The number of occupied locations (branches).</summary>
        public int OccupiedCount => occupiedCount;

        /// <summary>The number of Running machines (pips in normal motion).</summary>
        public int RunningCount => runningCount;

        public BranchState BranchOf(int location) => location >= 0 && location < locationCount ? branches[location] : BranchState.None;

        public ActivityPace PaceOf(int location) => location >= 0 && location < locationCount ? paces[location] : ActivityPace.Normal;

        /// <summary><c>min(3, floor(4 x fill / capacity))</c> of the shared store.</summary>
        public int FillIndex => fillIndex;

        /// <summary>True when the store is at capacity or a machine cannot store its output: the depot shows its full sprite.</summary>
        public bool StoreFull => storeFull;

        /// <summary>The depot sprite state: the fill index, or full when the store is full.</summary>
        public DepotFillState Fill => storeFull ? DepotFillState.Full : (DepotFillState)fillIndex;

        /// <summary>
        /// Recomputes the model. <paramref name="paceSource"/> supplies <see cref="ActivityPace"/> per location (null means Normal). Allocation-free once the
        /// buffers hold <paramref name="locations"/> entries.
        /// </summary>
        public void Refresh(int locations, MachineStateView[] rows, int rowCount, MachinePresentationModel paceSource, long storeUsed, long storeCapacity)
        {
            if (rows == null) throw new ArgumentNullException(nameof(rows));
            if (branches.Length < locations)
            {
                Array.Resize(ref branches, locations);
                Array.Resize(ref paces, locations);
            }

            locationCount = locations;
            for (var i = 0; i < locations; i++)
            {
                branches[i] = BranchState.None;
                paces[i] = ActivityPace.Normal;
            }

            runningCount = 0;
            occupiedCount = 0;
            var anyStorageFull = false;
            var count = rowCount < rows.Length ? rowCount : rows.Length;
            for (var m = 0; m < count; m++)
            {
                var row = rows[m];
                if (row.SlotIndex < 0 || row.SlotIndex >= locations)
                {
                    continue;
                }

                var state = SlotGridModel.StateFor(row.IsActive, row.Blocker, row.Phase);
                var running = state == MachineVisualState.Running;
                branches[row.SlotIndex] = running ? BranchState.Running : BranchState.Idle;
                paces[row.SlotIndex] = paceSource != null ? paceSource.Get(new BuildingLocationId(row.SlotIndex)).Pace : ActivityPace.Normal;
                occupiedCount++;
                runningCount += running ? 1 : 0;
                anyStorageFull |= state == MachineVisualState.StorageFull;
            }

            fillIndex = DepotLinkGeometry.FillIndexOf(storeUsed, storeCapacity);
            storeFull = anyStorageFull || (storeCapacity > 0 && storeUsed >= storeCapacity);
        }
    }
}
