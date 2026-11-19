using System;
using System.Collections.Generic;
using System.Linq;
using IdleFactory.Run.Application;
using IdleFactory.Unity.Presentation;
using NUnit.Framework;
using UnityEngine;

namespace IdleFactory.Tests.EditMode.F006
{
    /// <summary>
    /// T528 (Feature 006, FR-013, FR-015, FR-029, research R07, contracts/world-presentation.md section 2): the geometry of the trunks, junctions and branches on
    /// the 3 x 4, 4 x 3, 6, 20 and L layouts (measured against the real LINK pixels), the depot fill index, branch presence per machine condition and the pip
    /// pace. The model is pure, so everything here runs below the engine boundary.
    /// </summary>
    public sealed class DepotLinkModelTests
    {
        private static readonly string[] Layouts = DeckTestSupport.LayoutNames;

        private static MachineStateView Row(int slot, bool active = true, MachinePhase phase = MachinePhase.Running, MachineBlocker blocker = MachineBlocker.None) =>
            new MachineStateView(slot, 0, 1, active, phase, blocker, 1, 0);

        // ---------------------------------------------------------------------------------------------------
        // Geometry
        // ---------------------------------------------------------------------------------------------------

        [TestCaseSource(nameof(Layouts))]
        public void EveryRowHasOneTrunkFromItsFarLocationToAJunctionOnTheDepotColumn(string name)
        {
            var layout = DeckTestSupport.Layout(name);
            var plan = DepotLinkGeometry.Build(layout);
            var depotColumn = layout.DepotCell.x;
            for (var row = 0; row < layout.RowCount; row++)
            {
                var rowFromBottom = layout.RowCount - 1 - row;
                var tiles = plan.Tiles.Where(t => t.Cell.y == rowFromBottom).OrderBy(t => t.Cell.x).ToList();
                var columns = Enumerable.Range(0, layout.LocationCount)
                    .Select(i => layout.CellOf(new BuildingLocationId(i))).Where(c => c.y == rowFromBottom).Select(c => c.x).ToList();
                Assert.That(columns, Is.Not.Empty, name + " row " + row + " holds locations");
                var far = layout.DepotOnRight ? columns.Min() : columns.Max();
                Assert.That(tiles.Select(t => t.Cell.x), Is.EqualTo(Enumerable.Range(Math.Min(far, depotColumn), Math.Abs(depotColumn - far) + 1)),
                    name + " row " + row + ": a contiguous run of cells from the far location to the depot column");
                Assert.That(tiles.Count(t => t.Kind == LinkTileKind.Junction), Is.EqualTo(1), name + " row " + row);
                Assert.That(tiles.Single(t => t.Kind == LinkTileKind.Junction).Cell.x, Is.EqualTo(depotColumn), name + " row " + row + ": the junction is on the depot column");
                Assert.That(tiles.Single(t => t.Kind == LinkTileKind.Junction).Mirrored, Is.EqualTo(!layout.DepotOnRight), name + ": a depot on the left mirrors the junction");
            }

            Assert.That(plan.Tiles.Select(t => t.Cell).Distinct().Count(), Is.EqualTo(plan.Tiles.Count), name + ": no cell carries two tiles");
        }

        [TestCaseSource(nameof(Layouts))]
        public void EveryLocationOwnsItsTrunkCellAndNoLaneRunsBetweenMachines(string name)
        {
            var layout = DeckTestSupport.Layout(name);
            var plan = DepotLinkGeometry.Build(layout);
            for (var i = 0; i < layout.LocationCount; i++)
            {
                var cell = layout.CellOf(new BuildingLocationId(i));
                var tile = plan.Tiles.Single(t => t.Location == i);
                Assert.That(tile.Cell, Is.EqualTo(cell), name + " location " + i + ": its trunk cell is its own lattice cell");
                Assert.That(tile.Kind, Is.EqualTo(LinkTileKind.Trunk));
            }

            // R07: the trunk lies in the row gap behind its row, so the lane of a row never reaches a plate of the row in front or behind. Measured: the lane
            // band of the trunk tile lies above the rear edge of its own plate and below the plate bottom of the row behind (19 px gap).
            var trunk = DeckTestSupport.PixelsOfKey("F006-LINK-TRUNK");
            var band = OpaqueRows(trunk, 0);
            foreach (var tile in plan.Tiles)
            {
                var pivot = DeckPlanner.CellPivot(tile.Cell);
                var bottom = DepotLinkGeometry.TileCentre(tile.Cell).y - trunk.Height / 2;
                var laneLow = bottom + band.Min();
                var laneHigh = bottom + band.Max() + 1;
                Assert.That(laneLow, Is.GreaterThan(pivot.y + FactoryWorldLayout.RearEdgeAbovePivotPixels), name + " " + tile.Cell + ": the lane lies behind its own plate");
                Assert.That(laneHigh, Is.LessThanOrEqualTo(pivot.y + SlotGridLayout.PitchYPixels - SlotGridLayout.PivotAbovePlateBottomPixels),
                    name + " " + tile.Cell + ": below the plate bottom of the row behind");
            }
        }

        [TestCaseSource(nameof(Layouts))]
        public void TheTrunkTileLaneContainsTheLayoutTrunkLineOnEveryRow(string name)
        {
            var layout = DeckTestSupport.Layout(name);
            var plan = DepotLinkGeometry.Build(layout);
            var trunk = DeckTestSupport.PixelsOfKey("F006-LINK-TRUNK");
            var band = OpaqueRows(trunk, 0);
            foreach (var tile in plan.Tiles)
            {
                var bottom = DepotLinkGeometry.TileCentre(tile.Cell).y - trunk.Height / 2;
                var row = layout.RowCount - 1 - tile.Cell.y;
                var laneY = Mathf.RoundToInt(layout.TrunkOf(row).Start.y * SlotGridLayout.Density);
                Assert.That(laneY, Is.EqualTo(DepotLinkGeometry.LaneRowOf(tile.Cell)), name + ": the constant lane row is the layout's trunk line");
                Assert.That(laneY, Is.InRange(bottom + band.Min(), bottom + band.Max() + 1), name + " " + tile.Cell + ": the drawn lane band holds the layout trunk line");
            }
        }

        [TestCaseSource(nameof(Layouts))]
        public void TheBranchTileSpineHoldsEveryBranchPipPathFromThePlateRearEdgeToTheTrunk(string name)
        {
            var layout = DeckTestSupport.Layout(name);
            var plan = DepotLinkGeometry.Build(layout);
            var branch = DeckTestSupport.PixelsOfKey("F006-LINK-BRANCH");
            var spineColumns = Enumerable.Range(0, branch.Width).Where(x => HasOpaqueBelowTrunk(branch, x)).ToList();
            var spineRows = Enumerable.Range(0, branch.Height).Where(y => y < TrunkBandBottomRow && branch.IsOpaque(branch.Width / 2, y)).ToList();
            Assert.That(spineColumns, Is.Not.Empty);
            Assert.That(spineRows, Is.Not.Empty);
            for (var i = 0; i < layout.LocationCount; i++)
            {
                var id = new BuildingLocationId(i);
                var pivot = DeckPlanner.CellPivot(layout.CellOf(id));
                var bottom = DepotLinkGeometry.TileCentre(layout.CellOf(id)).y - branch.Height / 2;
                var left = pivot.x - branch.Width / 2;
                Assert.That(plan.PipX(i), Is.EqualTo(pivot.x), name + " location " + i + ": the pip column is the plate centre line");
                Assert.That(plan.PipX(i), Is.InRange(left + spineColumns.Min(), left + spineColumns.Max() + 1), name + " " + i + ": inside the drawn spine");
                Assert.That(plan.PipStartY(i), Is.EqualTo(pivot.y + FactoryWorldLayout.RearEdgeAbovePivotPixels), name + " " + i + ": a pip starts on the plate rear edge");
                Assert.That(plan.PipEndY(i), Is.EqualTo(DepotLinkGeometry.LaneRowOf(layout.CellOf(id))), name + " " + i + ": and ends on the trunk lane");
                Assert.That(plan.PipStartY(i), Is.InRange(bottom + spineRows.Min(), bottom + spineRows.Max() + 1), name + " " + i + ": the start lies on the drawn spine");
                Assert.That(plan.PipEndY(i), Is.GreaterThan(plan.PipStartY(i)));
                var geometric = layout.BranchOf(id);
                Assert.That(plan.PipStartY(i), Is.EqualTo(Mathf.RoundToInt(geometric.Start.y * SlotGridLayout.Density)), name + ": the layout branch is the single source");
            }
        }

        [TestCaseSource(nameof(Layouts))]
        public void TheLinkPlanDependsOnTheLayoutOnlySoTrunksAreStatic(string name)
        {
            var layout = DeckTestSupport.Layout(name);
            var a = DepotLinkGeometry.Build(layout);
            var b = DepotLinkGeometry.Build(layout);
            Assert.That(b.Tiles.Select(t => (t.Cell, t.Kind, t.Mirrored, t.Location)), Is.EqualTo(a.Tiles.Select(t => (t.Cell, t.Kind, t.Mirrored, t.Location))));
            Assert.That(a.Tiles.All(t => t.Kind == LinkTileKind.Trunk || t.Kind == LinkTileKind.Junction), Is.True, "a plan holds trunk and junction tiles only; a branch is a view-time swap");
        }

        [Test]
        public void ADepotOnTheLeftMirrorsItsJunctionAndRunsTheTrunkFromTheRightmostLocation()
        {
            var copy = UnityEngine.Object.Instantiate(DeckTestSupport.Settings());
            try
            {
                copy.depotRulePortrait = new MarginCellRule(MarginSide.Left, 0);
                copy.depositRule = new MarginCellRule(MarginSide.Right, -1);
                var layout = TestLayouts.FromRows("test-left", WorldOrientation.Portrait, copy, "###", "###");
                Assert.That(layout.DepotOnRight, Is.False, "the left depot rule took effect");
                var plan = DepotLinkGeometry.Build(layout);
                foreach (var junction in plan.Tiles.Where(t => t.Kind == LinkTileKind.Junction))
                {
                    Assert.That(junction.Cell.x, Is.EqualTo(layout.DepotCell.x));
                    Assert.That(junction.Mirrored, Is.True);
                }

                foreach (var tile in plan.Tiles.Where(t => t.Kind == LinkTileKind.Trunk))
                {
                    Assert.That(tile.Cell.x, Is.InRange(layout.DepotCell.x + 1, layout.ColumnCount - 1), "trunk cells lie between the depot and the far location");
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(copy);
            }
        }

        [TestCaseSource(nameof(Layouts))]
        public void TheVisibleTileCountIncludingTheLinkTilesStaysInsideTheBudgetPlusOneTrunkRunPerRow(string name)
        {
            var layout = DeckTestSupport.Layout(name);
            var linkTiles = DepotLinkGeometry.Build(layout).Tiles.Count;
            var m = layout.DeckBandCells;
            var budget = (layout.ColumnCount + 2 * m) * (layout.RowCount + 2 * m) * 2 + layout.RowCount * (layout.ColumnCount + 1);
            for (var stage = 0; stage < WorldCompositionSettings.StageCount; stage++)
            {
                var plan = DeckPlanner.Build(layout, DeckTestSupport.All(layout.LocationCount, SlotCondition.Occupied), stage, DeckTestSupport.Settings());
                Assert.That(plan.TileCount + linkTiles, Is.LessThanOrEqualTo(budget), name + " stage " + stage + ": base + deck + links");
            }

            Assert.That(linkTiles, Is.LessThanOrEqualTo(layout.RowCount * (layout.ColumnCount + 1)), name + ": at most one run of columns + junction per row");
        }

        // ---------------------------------------------------------------------------------------------------
        // Depot fill
        // ---------------------------------------------------------------------------------------------------

        [TestCase(0L, 100L, DepotFillState.Empty, TestName = "Zero")]
        [TestCase(1L, 100L, DepotFillState.Low, TestName = "OneUnit")]
        [TestCase(49L, 100L, DepotFillState.Low, TestName = "JustUnderFiftyPercent")]
        [TestCase(50L, 100L, DepotFillState.High, TestName = "FiftyPercent")]
        [TestCase(99L, 100L, DepotFillState.High, TestName = "CapacityMinusOne")]
        [TestCase(100L, 100L, DepotFillState.High, TestName = "Capacity (the model turns it into Full)")]
        [TestCase(-5L, 100L, DepotFillState.Empty, TestName = "Negative")]
        [TestCase(3L, 7L, DepotFillState.Low, TestName = "OddCapacityJustUnderHalf")]
        [TestCase(4L, 7L, DepotFillState.High, TestName = "OddCapacityJustOverHalf")]
        public void TheFillLevelIsEmptyOnlyAtZeroLowBelowHalfAndHighFromHalf(long fill, long capacity, DepotFillState expected)
        {
            Assert.That(DepotLinkGeometry.FillStateOf(fill, capacity), Is.EqualTo(expected));
        }

        [Test]
        public void TheFillLevelIsExactAtTheBoundsOfALargeStore()
        {
            const long Capacity = 4_000_000_000_000_000_000L;
            Assert.That(DepotLinkGeometry.FillStateOf(1, Capacity), Is.EqualTo(DepotFillState.Low));
            Assert.That(DepotLinkGeometry.FillStateOf(Capacity / 2 - 1, Capacity), Is.EqualTo(DepotFillState.Low));
            Assert.That(DepotLinkGeometry.FillStateOf(Capacity / 2, Capacity), Is.EqualTo(DepotFillState.High));
            Assert.That(DepotLinkGeometry.FillStateOf(long.MaxValue - 1, long.MaxValue), Is.EqualTo(DepotFillState.High));
        }

        [Test]
        public void TheDepotShowsTheFillLevelAndFullOnlyWhenTheStoreOrAMachineIsFull()
        {
            var model = new DepotLinkModel();
            var running = new[] { Row(0) };
            var cases = new[]
            {
                (0L, DepotFillState.Empty), (1L, DepotFillState.Low), (49L, DepotFillState.Low), (50L, DepotFillState.High),
                (99L, DepotFillState.High), (100L, DepotFillState.Full),
            };
            foreach (var (used, state) in cases)
            {
                model.Refresh(6, running, running.Length, null, used, 100);
                Assert.That(model.Fill, Is.EqualTo(state), "used " + used + " of 100");
                Assert.That(model.StoreFull, Is.EqualTo(state == DepotFillState.Full), "used " + used);
            }

            model.Refresh(6, running, running.Length, null, 150, 100);
            Assert.That(model.Fill, Is.EqualTo(DepotFillState.Full), "over capacity is full");

            var blocked = new[] { Row(0), Row(1, blocker: MachineBlocker.StorageFull, phase: MachinePhase.Paused) };
            model.Refresh(6, blocked, blocked.Length, null, 10, 100);
            Assert.That(model.FillLevel, Is.EqualTo(DepotFillState.Low), "the fill level is the store's");
            Assert.That(model.StoreFull, Is.True, "StorageFull at low fill: a machine that cannot store its output shows the depot full (contracts section 3)");
            Assert.That(model.Fill, Is.EqualTo(DepotFillState.Full));
        }

        // T528 re-QA carry (b): a store with no capacity but a positive fill is an inconsistent read (the inventory never holds units without capacity).
        // The decision: the model does not invent a "full" signal from it. The fill level reads High (anything stored on an unusable capacity counts as "at
        // least half"), StoreFull stays false (it needs a real capacity to be reached, so a transient zero-capacity read never shows the copper lip), and
        // nothing throws or divides by zero. A machine reporting StorageFull still makes the depot full.
        [TestCase(5L, 0L, TestName = "ZeroCapacity")]
        [TestCase(5L, -10L, TestName = "NegativeCapacity")]
        public void AStoreWithNoCapacityButSomeFillReadsHighAndNeverFull(long used, long capacity)
        {
            Assert.That(DepotLinkGeometry.FillStateOf(used, capacity), Is.EqualTo(DepotFillState.High));
            var model = new DepotLinkModel();
            var running = new[] { Row(0) };
            model.Refresh(6, running, running.Length, null, used, capacity);
            Assert.That(model.FillLevel, Is.EqualTo(DepotFillState.High));
            Assert.That(model.StoreFull, Is.False, "full needs a real capacity to be reached");
            Assert.That(model.Fill, Is.EqualTo(DepotFillState.High));

            var blocked = new[] { Row(0, blocker: MachineBlocker.StorageFull, phase: MachinePhase.Paused) };
            model.Refresh(6, blocked, blocked.Length, null, used, capacity);
            Assert.That(model.StoreFull, Is.True, "a machine that cannot store its output still shows the depot full");
        }

        [Test]
        public void TheDepotKeyIsTheStageVariantTimesTheFillState()
        {
            var seen = new HashSet<string>();
            for (var stage = 0; stage < WorldCompositionSettings.StageCount; stage++)
            {
                foreach (DepotFillState state in Enum.GetValues(typeof(DepotFillState)))
                {
                    var key = DepotLinkGeometry.DepotKey(stage, state);
                    Assert.That(key, Is.EqualTo("F006-DEPOT-S" + (stage + 1) + "-" + state.ToString().ToUpperInvariant()));
                    Assert.That(PresentationSpriteCatalog.TryGetRegistryRequirement(key, out _), Is.True, key + " has a registry row");
                    seen.Add(key);
                }
            }

            Assert.That(seen, Has.Count.EqualTo(12), "4 fill states times 3 stage variants");
            Assert.That(DepotLinkGeometry.DepotKey(-3, DepotFillState.Low), Is.EqualTo("F006-DEPOT-S1-LOW"), "a stage out of range clamps");
            Assert.That(DepotLinkGeometry.DepotKey(9, DepotFillState.Low), Is.EqualTo("F006-DEPOT-S3-LOW"));
        }

        // ---------------------------------------------------------------------------------------------------
        // Branch presence per machine condition
        // ---------------------------------------------------------------------------------------------------

        [Test]
        public void ABranchExistsForAnOccupiedLocationOnlyAndRunsOnlyWhileTheMachineIsRunning()
        {
            var rows = new[]
            {
                Row(0),
                Row(1, active: false, phase: MachinePhase.Paused, blocker: MachineBlocker.Inactive),
                Row(2, phase: MachinePhase.Paused, blocker: MachineBlocker.MissingInput),
                Row(3, phase: MachinePhase.Paused, blocker: MachineBlocker.StorageFull),
                Row(4, phase: MachinePhase.Waiting),
            };
            var model = new DepotLinkModel();
            model.Refresh(6, rows, rows.Length, null, 0, 100);
            Assert.That(model.BranchOf(0), Is.EqualTo(BranchState.Running));
            Assert.That(model.BranchOf(1), Is.EqualTo(BranchState.Idle), "a switched-off machine keeps its branch and shows no pip");
            Assert.That(model.BranchOf(2), Is.EqualTo(BranchState.Idle), "missing input");
            Assert.That(model.BranchOf(3), Is.EqualTo(BranchState.Idle), "store full");
            Assert.That(model.BranchOf(4), Is.EqualTo(BranchState.Idle), "waiting to deliver");
            Assert.That(model.BranchOf(5), Is.EqualTo(BranchState.None), "an empty or locked location has no branch");
            Assert.That(model.BranchOf(99), Is.EqualTo(BranchState.None), "an unknown location has no branch");
            Assert.That(model.OccupiedCount, Is.EqualTo(5));
            Assert.That(model.RunningCount, Is.EqualTo(1));
        }

        [Test]
        public void RunningIsTheSingleStateForPrecedenceForEveryCombination()
        {
            var model = new DepotLinkModel();
            foreach (var active in new[] { true, false })
            {
                foreach (MachineBlocker blocker in Enum.GetValues(typeof(MachineBlocker)))
                {
                    foreach (MachinePhase phase in Enum.GetValues(typeof(MachinePhase)))
                    {
                        var rows = new[] { Row(2, active, phase, blocker) };
                        model.Refresh(6, rows, 1, null, 0, 100);
                        var running = SlotGridModel.StateFor(active, blocker, phase) == MachineVisualState.Running;
                        Assert.That(model.BranchOf(2) == BranchState.Running, Is.EqualTo(running), "active " + active + " blocker " + blocker + " phase " + phase);
                    }
                }
            }
        }

        [Test]
        public void ARowForALocationOutsideTheLayoutIsIgnoredAndARemovedMachineLosesItsBranchAtOnce()
        {
            var model = new DepotLinkModel();
            var rows = new[] { Row(1), Row(40), Row(-1) };
            model.Refresh(6, rows, rows.Length, null, 0, 100);
            Assert.That(model.OccupiedCount, Is.EqualTo(1));
            Assert.That(model.BranchOf(1), Is.EqualTo(BranchState.Running));
            model.Refresh(6, rows, 0, null, 0, 100);
            Assert.That(model.BranchOf(1), Is.EqualTo(BranchState.None), "with the machine gone the branch is gone in the next refresh");
        }

        // ---------------------------------------------------------------------------------------------------
        // Pace
        // ---------------------------------------------------------------------------------------------------

        [TestCase(ActivityPace.Slow, 800)]
        [TestCase(ActivityPace.Normal, 600)]
        [TestCase(ActivityPace.Fast, 400)]
        public void ThePipCrossesALaneCellIn800600Or400MillisecondsByPace(ActivityPace pace, int milliseconds)
        {
            Assert.That(MachinePresentationModel.PipMillisecondsPerCell(pace), Is.EqualTo(milliseconds));
            const int Steps = 10;
            Assert.That(DepotLinkGeometry.PipStep(0, pace, Steps), Is.Zero, "a pip starts on the rear edge");
            Assert.That(DepotLinkGeometry.PipStep(milliseconds - 1, pace, Steps), Is.EqualTo(Steps - 1), "and has reached the trunk just before the period ends");
            Assert.That(DepotLinkGeometry.PipStep(milliseconds, pace, Steps), Is.Zero, "then it starts again");
            Assert.That(DepotLinkGeometry.PipStep(milliseconds * 7 + milliseconds / 2, pace, Steps), Is.EqualTo(Steps / 2), "the period repeats");
            var last = -1;
            for (var t = 0; t < milliseconds; t++)
            {
                var step = DepotLinkGeometry.PipStep(t, pace, Steps);
                Assert.That(step, Is.GreaterThanOrEqualTo(last).And.InRange(0, Steps - 1), "monotonic within a period at " + t);
                last = step;
            }
        }

        [Test]
        public void ThePaceComesFromTheCycleTimeBucketsOfThePresentationModel()
        {
            var layout = TestLayouts.Six();
            var machines = new[]
            {
                new SynthMachine { Slot = 0, CycleTicks = 50_000_000 },
                new SynthMachine { Slot = 1, CycleTicks = 20_000_000 },
                new SynthMachine { Slot = 2, CycleTicks = 5_000_000 },
            };
            var built = PresentationModelSynth.Build(1, 1, machines);
            var presentation = new MachinePresentationModel();
            presentation.Refresh(layout, built.Factory, built.Views, built.Views.Length, built.Rates);
            var model = new DepotLinkModel();
            model.Refresh(layout.LocationCount, built.Views, built.Views.Length, presentation, 0, 100);
            Assert.That(model.PaceOf(0), Is.EqualTo(ActivityPace.Slow), "4 s or longer");
            Assert.That(model.PaceOf(1), Is.EqualTo(ActivityPace.Normal));
            Assert.That(model.PaceOf(2), Is.EqualTo(ActivityPace.Fast), "under 1 s");
            Assert.That(MachinePresentationModel.PipMillisecondsPerCell(model.PaceOf(0)), Is.EqualTo(800));
            Assert.That(MachinePresentationModel.PipMillisecondsPerCell(model.PaceOf(1)), Is.EqualTo(600));
            Assert.That(MachinePresentationModel.PipMillisecondsPerCell(model.PaceOf(2)), Is.EqualTo(400));
        }

        // ---------------------------------------------------------------------------------------------------
        // helpers
        // ---------------------------------------------------------------------------------------------------

        // The first tile row (from the bottom) of the trunk band in the 70 x 54 link art: rows 8 to 12 from the top are 41 to 45 from the bottom.
        private const int TrunkBandBottomRow = 41;

        // The tile rows (from the bottom) that hold an opaque pixel in the given column.
        private static List<int> OpaqueRows(SpritePixels sprite, int column) =>
            Enumerable.Range(0, sprite.Height).Where(y => sprite.IsOpaque(column, y)).ToList();

        // True when the column has an opaque pixel below the trunk band: the branch stub starts there.
        private static bool HasOpaqueBelowTrunk(SpritePixels sprite, int x)
        {
            for (var y = 0; y < TrunkBandBottomRow; y++)
            {
                if (sprite.IsOpaque(x, y))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
