using System;
using System.Collections.Generic;
using System.Linq;
using IdleFactory.Unity.Presentation;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace IdleFactory.Tests.EditMode.F006
{
    /// <summary>
    /// T512 (Feature 006, data-model.md FactoryWorldLayout and WorldCompositionSettings validation, contracts/world-presentation.md sections
    /// 2 and 4): the layout rules hold for the shipped lattice, for lattices of any location count, and for the explicit-cells test layouts
    /// (6, 20 and an L shape): footprints and hit shapes never overlap, every branch reaches its trunk, every trunk reaches the depot, no
    /// lane crosses a footprint and no lane links two machines, and the depot and the deposit lie in the margin band outside every hit shape
    /// and keep-out. The composition asset and its values are checked here too (R22).
    /// </summary>
    public sealed class WorldLayoutValidationTests
    {
        private readonly List<UnityEngine.Object> created = new List<UnityEngine.Object>();

        [TearDown]
        public void DestroyCreated()
        {
            foreach (var item in created)
            {
                if (item != null)
                {
                    UnityEngine.Object.DestroyImmediate(item);
                }
            }

            created.Clear();
        }

        private WorldCompositionSettings NewSettings(Action<WorldCompositionSettings> edit = null)
        {
            var settings = ScriptableObject.CreateInstance<WorldCompositionSettings>();
            created.Add(settings);
            edit?.Invoke(settings);
            return settings;
        }

        private static void AssertValid(FactoryWorldLayout layout, string context)
        {
            var problems = layout.Validate();
            Assert.That(problems, Is.Empty, context + ": " + string.Join(" | ", problems));
        }

        // ------------------------------------------------------------------------------------------------ layout rules

        [Test]
        public void TheShippedLatticesAreValid([Values(WorldOrientation.Portrait, WorldOrientation.Landscape)] WorldOrientation orientation)
        {
            AssertValid(LatticeLayoutBuilder.Shipped(orientation), orientation.ToString());
        }

        [Test]
        public void ALatticeOfAnyLocationCountIsValid([Values(WorldOrientation.Portrait, WorldOrientation.Landscape)] WorldOrientation orientation)
        {
            for (var count = 1; count <= 40; count++)
            {
                var layout = new LatticeLayoutBuilder(count, orientation).Build();
                Assert.That(layout.LocationCount, Is.EqualTo(count));
                AssertValid(layout, orientation + " x " + count);
                Assert.That(layout.Bounds.xMin, Is.EqualTo(0f));
                Assert.That(layout.Bounds.yMin, Is.EqualTo(0f));
            }
        }

        [Test]
        public void TheTestLayoutsAreValid([ValueSource(typeof(TestLayouts), nameof(TestLayouts.Names))] string name)
        {
            AssertValid(TestLayouts.ByName(name), name);
        }

        [Test]
        public void TheTestLayoutsHaveTheExpectedShape()
        {
            Assert.That(TestLayouts.Six().LocationCount, Is.EqualTo(6));
            Assert.That(TestLayouts.Twenty().LocationCount, Is.EqualTo(20));
            var l = TestLayouts.LShape();
            Assert.That(l.LocationCount, Is.EqualTo(7));
            Assert.That(l.ColumnCount, Is.EqualTo(4));
            Assert.That(l.RowCount, Is.EqualTo(4));
            Assert.That(TestLayouts.Six().Bounds, Is.EqualTo(new Rect(0f, 0f, (2 * 70 + 70) / 24f, (1 * 54 + 15 + 39) / 24f)));
            Assert.That(TestLayouts.Twenty().Bounds, Is.EqualTo(new Rect(0f, 0f, (3 * 70 + 70) / 24f, (4 * 54 + 15 + 39) / 24f)));
            Assert.That(l.Bounds, Is.EqualTo(new Rect(0f, 0f, (3 * 70 + 70) / 24f, (3 * 54 + 15 + 39) / 24f)), "an L shape keeps its bounding extent");
        }

        [Test]
        public void ALatticeWithTheShippedCountIsTheSameAsTheCachedF004Layout()
        {
            var built = new LatticeLayoutBuilder(LatticeLayoutBuilder.ShippedLocationCount, WorldOrientation.Landscape).Build();
            var shipped = LatticeLayoutBuilder.Shipped(WorldOrientation.Landscape);
            Assert.That(built.Bounds, Is.EqualTo(shipped.Bounds));
            Assert.That(built.LayoutId, Is.EqualTo(shipped.LayoutId));
            Assert.That(built.DepotCell, Is.EqualTo(shipped.DepotCell));
            Assert.That(built.DepositCell, Is.EqualTo(shipped.DepositCell));
        }

        [Test]
        public void EveryLayoutPlacesEachLocationOnceAndIdentifiesItById([ValueSource(typeof(TestLayouts), nameof(TestLayouts.Names))] string name)
        {
            var layout = TestLayouts.ByName(name);
            var cells = new HashSet<Vector2Int>();
            for (var i = 0; i < layout.LocationCount; i++)
            {
                var id = new BuildingLocationId(i);
                Assert.That(cells.Add(layout.CellOf(id)), Is.True, "no two ids share a cell");
                Assert.That(layout.TryHit(layout.PlateCentreOf(id), out var hit), Is.True);
                Assert.That(hit, Is.EqualTo(id), "the position resolves back to the id (FR-038)");
            }

            Assert.Throws<ArgumentOutOfRangeException>(() => layout.AnchorOf(new BuildingLocationId(layout.LocationCount)));
            Assert.Throws<ArgumentOutOfRangeException>(() => layout.FootprintOf(new BuildingLocationId(-1)));
            Assert.That(layout.IsValidId(new BuildingLocationId(layout.LocationCount)), Is.False);
        }

        [Test]
        public void HitShapesAreDisjointAndAgreeWithTryHit([ValueSource(typeof(TestLayouts), nameof(TestLayouts.Names))] string name)
        {
            var layout = TestLayouts.ByName(name);
            var bounds = layout.Bounds;
            const float step = 0.0831f; // not a divisor of the cell pitch, so no sample lies exactly on a cell edge
            for (var y = bounds.yMin - 1f + 0.0007f; y <= bounds.yMax + 1f; y += step)
            {
                for (var x = bounds.xMin - 1f + 0.0007f; x <= bounds.xMax + 1f; x += step)
                {
                    var world = new Vector2(x, y);
                    var containing = 0;
                    for (var i = 0; i < layout.LocationCount; i++)
                    {
                        containing += layout.HitShapeOf(new BuildingLocationId(i)).Contains(world) ? 1 : 0;
                    }

                    Assert.That(containing, Is.LessThanOrEqualTo(1), $"({x}, {y}) lies in two hit shapes");
                    var hit = layout.TryHit(world, out var id);
                    if (hit)
                    {
                        Assert.That(layout.HitShapeOf(id).Contains(world) || OnCellEdge(layout, id, world), Is.True, $"({x}, {y}) hits {id} outside its shape");
                    }
                }
            }
        }

        private static bool OnCellEdge(FactoryWorldLayout layout, BuildingLocationId id, Vector2 world)
        {
            var cell = layout.LocationCellRectOf(id);
            const float tolerance = 1e-3f;
            return Mathf.Abs(world.x - cell.xMin) < tolerance || Mathf.Abs(world.x - cell.xMax) < tolerance ||
                   Mathf.Abs(world.y - cell.yMin) < tolerance || Mathf.Abs(world.y - cell.yMax) < tolerance;
        }

        [Test]
        public void TheDeckCoversEveryLocationAndTheMarginBand([ValueSource(typeof(TestLayouts), nameof(TestLayouts.Names))] string name)
        {
            var layout = TestLayouts.ByName(name);
            var cells = new HashSet<Vector2Int>(layout.DeckCells);
            Assert.That(cells.Count, Is.EqualTo(layout.DeckCells.Count), "no cell is listed twice");
            var band = layout.DeckBandCells;
            Assert.That(cells.Count, Is.EqualTo((layout.ColumnCount + 2 * band) * (layout.RowCount + 2 * band)));
            for (var i = 0; i < layout.LocationCount; i++)
            {
                Assert.That(cells, Does.Contain(layout.CellOf(new BuildingLocationId(i))));
            }

            Assert.That(cells, Does.Contain(layout.DepotCell));
            Assert.That(cells, Does.Contain(layout.DepositCell));
            var deck = layout.DeckBounds;
            Assert.That(deck.xMin, Is.LessThan(layout.Bounds.xMin));
            Assert.That(deck.yMin, Is.LessThan(layout.Bounds.yMin));
            Assert.That(deck.xMax, Is.GreaterThan(layout.Bounds.xMax));
            Assert.That(deck.yMax, Is.GreaterThan(layout.Bounds.yMax));
            foreach (var cell in layout.DeckCells)
            {
                Assert.That(FactoryWorldLayout.DeckTileRect(cell).size, Is.EqualTo(new Vector2(70f / 24f, 54f / 24f)));
            }
        }

        // ------------------------------------------------------------------------------------------------ depot, trunks, branches

        [Test]
        public void TrunksEndAtTheDepotAndBranchesReachTheirTrunk([ValueSource(typeof(TestLayouts), nameof(TestLayouts.Names))] string name)
        {
            var layout = TestLayouts.ByName(name);
            var occupiedRows = new HashSet<int>();
            for (var i = 0; i < layout.LocationCount; i++)
            {
                var id = new BuildingLocationId(i);
                var branch = layout.BranchOf(id);
                var row = layout.RowOf(id);
                occupiedRows.Add(row);
                Assert.That(branch.IsEmpty, Is.False);
                Assert.That(layout.TrunkOf(row).Touches(branch.End), Is.True, id + ": the branch reaches the trunk of its row");
                Assert.That(branch.Start.y, Is.EqualTo(layout.FootprintOf(id).yMax).Within(1e-4f), id + ": the branch starts at the plate's rear edge");
                Assert.That(branch.Start.x, Is.EqualTo(layout.AnchorOf(id).x).Within(1e-4f));
                Assert.That(branch.SegmentCount, Is.EqualTo(1), "a branch is one straight segment");
            }

            for (var row = 0; row < layout.RowCount; row++)
            {
                var trunk = layout.TrunkOf(row);
                Assert.That(trunk.IsEmpty, Is.EqualTo(!occupiedRows.Contains(row)), "one trunk per occupied row, none for an empty row");
                if (!trunk.IsEmpty)
                {
                    Assert.That(Vector2.Distance(trunk.End, layout.DepotAnchor), Is.LessThan(1e-4f), "row " + row + ": the trunk ends at the depot");
                }
            }
        }

        [Test]
        public void NoLaneCrossesAFootprintAndNoLaneLinksTwoMachines([ValueSource(typeof(TestLayouts), nameof(TestLayouts.Names))] string name)
        {
            var layout = TestLayouts.ByName(name);
            var footprints = Enumerable.Range(0, layout.LocationCount).Select(i => layout.FootprintOf(new BuildingLocationId(i))).ToArray();
            var lanes = 0;
            for (var row = 0; row < layout.RowCount; row++)
            {
                var trunk = layout.TrunkOf(row);
                if (trunk.IsEmpty)
                {
                    continue;
                }

                lanes++;
                foreach (var footprint in footprints)
                {
                    Assert.That(trunk.CrossesInterior(footprint), Is.False, "a trunk crosses a footprint");
                    // A trunk never touches a machine: the only lane that meets a footprint is that machine's own branch.
                    Assert.That(Touches(trunk, footprint), Is.False, "a trunk touches a footprint, which would link a machine to a trunk without a branch");
                }

                Assert.That(Vector2.Distance(trunk.End, layout.DepotAnchor), Is.LessThan(1e-4f));
            }

            for (var i = 0; i < layout.LocationCount; i++)
            {
                lanes++;
                var branch = layout.BranchOf(new BuildingLocationId(i));
                for (var j = 0; j < footprints.Length; j++)
                {
                    Assert.That(branch.CrossesInterior(footprints[j]), Is.False);
                    if (j != i)
                    {
                        Assert.That(Touches(branch, footprints[j]), Is.False, $"the branch of {i} touches the footprint of {j}: a machine-to-machine link");
                    }
                }
            }

            Assert.That(lanes, Is.EqualTo(layout.LocationCount + Enumerable.Range(0, layout.RowCount).Count(r => !layout.TrunkOf(r).IsEmpty)));
        }

        private static bool Touches(LanePath path, Rect rect)
        {
            const float epsilon = 1e-4f;
            for (var i = 0; i < path.SegmentCount; i++)
            {
                var segment = path.SegmentRect(i);
                if (segment.xMax >= rect.xMin - epsilon && segment.xMin <= rect.xMax + epsilon &&
                    segment.yMax >= rect.yMin - epsilon && segment.yMin <= rect.yMax + epsilon)
                {
                    return true;
                }
            }

            return false;
        }

        [Test]
        public void TheDepotAndTheDepositLieInTheMarginBandOutsideEveryHitShapeAndKeepOut([ValueSource(typeof(TestLayouts), nameof(TestLayouts.Names))] string name)
        {
            var layout = TestLayouts.ByName(name);
            Assert.That(layout.DepotCell, Is.Not.EqualTo(layout.DepositCell), "one depot and one deposit");
            foreach (var cell in new[] { layout.DepotCell, layout.DepositCell })
            {
                Assert.That(cell.x < 0 || cell.x >= layout.ColumnCount || cell.y < 0 || cell.y >= layout.RowCount, Is.True, "in the margin band");
                var tile = FactoryWorldLayout.DeckTileRect(cell);
                for (var i = 0; i < layout.LocationCount; i++)
                {
                    var id = new BuildingLocationId(i);
                    Assert.That(FactoryWorldLayout.Overlaps(tile, layout.LocationCellRectOf(id)), Is.False, "a hit shape of " + id);
                    for (var k = 0; k < FactoryWorldLayout.KeepOutCount; k++)
                    {
                        Assert.That(FactoryWorldLayout.Overlaps(tile, layout.KeepOutOf(id, k)), Is.False, $"keep-out {k} of {id}");
                    }
                }
            }

            Assert.That(layout.DeckBounds.Contains(layout.DepotAnchor), Is.True, "framing code can query the depot");
            Assert.That(layout.DeckBounds.Contains(layout.DepositAnchor), Is.True, "framing code can query the deposit");
        }

        [Test]
        public void TheDepotCanSitOnEitherSideAndTheTrunksFollow([Values(MarginSide.Right, MarginSide.Left)] MarginSide side, [Values(0, 1, -1)] int along)
        {
            var settings = NewSettings(s =>
            {
                s.depotRulePortrait = new MarginCellRule(side, along);
                s.depotRuleLandscape = new MarginCellRule(side, along);
                s.depositRule = new MarginCellRule(MarginSide.Bottom, 0);
            });
            foreach (var name in TestLayouts.Names)
            {
                var layout = TestLayouts.ByName(name, settings);
                AssertValid(layout, name + " depot " + side + " " + along);
                Assert.That(layout.DepotOnRight, Is.EqualTo(side == MarginSide.Right));
                Assert.That(layout.DepotCell.x, Is.EqualTo(side == MarginSide.Right ? layout.ColumnCount : -1));
            }
        }

        [Test]
        public void TheDepositMayUseTheLeftRightOrBottomMarginAndNeverTheTop([Values(MarginSide.Left, MarginSide.Right, MarginSide.Bottom)] MarginSide side)
        {
            var settings = NewSettings(s => s.depositRule = new MarginCellRule(side, -1));
            Assert.That(settings.Validate(), Is.Empty);
            foreach (var name in TestLayouts.Names)
            {
                AssertValid(TestLayouts.ByName(name, settings), name + " deposit " + side);
            }

            var top = NewSettings(s => s.depositRule = new MarginCellRule(MarginSide.Top, 0));
            Assert.That(top.Validate(), Is.Not.Empty, "the badge band of the back row reaches into the top margin cell");
        }

        [Test]
        public void ASharedDepotAndDepositCellIsReported()
        {
            var settings = NewSettings(s =>
            {
                s.depotRulePortrait = new MarginCellRule(MarginSide.Right, 0);
                s.depositRule = new MarginCellRule(MarginSide.Right, 0);
            });
            var problems = TestLayouts.Six(settings).Validate();
            Assert.That(problems, Has.Some.Contain("share a cell"));
        }

        [Test]
        public void ABadLayoutIsRejected()
        {
            Assert.Throws<ArgumentException>(() => new FactoryWorldLayout("x", new List<Vector2Int>(), WorldOrientation.Portrait));
            Assert.Throws<ArgumentException>(() => new FactoryWorldLayout("x", new[] { new Vector2Int(0, 0), new Vector2Int(0, 0) }, WorldOrientation.Portrait));
            Assert.Throws<ArgumentException>(() => new FactoryWorldLayout("", new[] { new Vector2Int(0, 0) }, WorldOrientation.Portrait));
            Assert.Throws<ArgumentOutOfRangeException>(() => new LatticeLayoutBuilder(0, WorldOrientation.Portrait));
            Assert.Throws<ArgumentOutOfRangeException>(() => TestLayouts.Six().TrunkOf(99));
        }

        [Test]
        public void ExplicitCellsAreShiftedToTheOriginAndKeepTheirOrder()
        {
            var shifted = new FactoryWorldLayout("shift", new[] { new Vector2Int(5, 7), new Vector2Int(6, 7), new Vector2Int(5, 6) }, WorldOrientation.Portrait);
            Assert.That(shifted.CellOf(new BuildingLocationId(0)), Is.EqualTo(new Vector2Int(0, 1)));
            Assert.That(shifted.CellOf(new BuildingLocationId(1)), Is.EqualTo(new Vector2Int(1, 1)));
            Assert.That(shifted.CellOf(new BuildingLocationId(2)), Is.EqualTo(new Vector2Int(0, 0)));
            Assert.That(shifted.Bounds.xMin, Is.EqualTo(0f));
            AssertValid(shifted, "shifted");
        }

        [Test]
        public void IsUnlockedFollowsTheUnlockOrder()
        {
            var layout = TestLayouts.Six();
            Assert.That(layout.IsUnlocked(new BuildingLocationId(0), 0), Is.False);
            Assert.That(layout.IsUnlocked(new BuildingLocationId(0), 1), Is.True);
            Assert.That(layout.IsUnlocked(new BuildingLocationId(5), 5), Is.False);
            Assert.That(layout.IsUnlocked(new BuildingLocationId(5), 6), Is.True);
            Assert.That(layout.IsUnlocked(new BuildingLocationId(6), 99), Is.False, "an unknown id is never unlocked");
        }

        // ------------------------------------------------------------------------------------------------ composition settings

        [Test]
        public void TheDefaultSettingsAreValidAndReproduceF004()
        {
            var settings = NewSettings();
            Assert.That(settings.Validate(), Is.Empty);
            Assert.That(settings.marginCells, Is.EqualTo(SlotGridLayout.HalfCellMargin));
            Assert.That(settings.maxPropHeightArtPx, Is.EqualTo(24));
            Assert.That(settings.propDensityPerStage, Is.EqualTo(new float[3]), "F004 has no props");
            Assert.That(settings.edgeRulePerStage, Is.EqualTo(new[] { EdgeRule.Railing, EdgeRule.Wall, EdgeRule.Gantry }));
            foreach (var orientation in new[] { WorldOrientation.Portrait, WorldOrientation.Landscape })
            {
                var layout = new LatticeLayoutBuilder(LatticeLayoutBuilder.ShippedLocationCount, orientation, settings).Build();
                var columns = LatticeLayoutBuilder.ColumnsFor(orientation);
                Assert.That(layout.Bounds, Is.EqualTo(SlotGridLayout.Extent(columns)), "the default lattice reproduces the F004 Extent");
                Assert.That(layout.Margin, Is.EqualTo(SlotGridLayout.HalfCellMargin));
            }
        }

        [Test]
        public void TheAuthoredAssetLoadsAndEqualsTheDefaults()
        {
            var asset = AssetDatabase.LoadAssetAtPath<WorldCompositionSettings>(WorldCompositionSettings.AssetPath);
            Assert.That(asset, Is.Not.Null, WorldCompositionSettings.AssetPath + " exists");
            Assert.That(asset.Validate(), Is.Empty);
            var defaults = NewSettings();
            Assert.That(JsonUtility.ToJson(asset), Is.EqualTo(JsonUtility.ToJson(defaults)), "the asset's defaults reproduce F004 until the World Designer edits them");
            Assert.That(asset.ComposeFor(WorldOrientation.Portrait).MarginCells, Is.EqualTo(SlotGridLayout.HalfCellMargin));
            var layout = new LatticeLayoutBuilder(LatticeLayoutBuilder.ShippedLocationCount, WorldOrientation.Portrait, asset).Build();
            Assert.That(layout.Bounds, Is.EqualTo(SlotGridLayout.Extent(3)));
            AssertValid(layout, "asset lattice");
        }

        [Test]
        public void OutOfRangeSettingsAreReported()
        {
            Assert.That(NewSettings(s => s.marginCells = -1f).Validate(), Is.Not.Empty);
            Assert.That(NewSettings(s => s.marginCells = float.NaN).Validate(), Is.Not.Empty);
            Assert.That(NewSettings(s => s.marginCells = 99f).Validate(), Is.Not.Empty);
            Assert.That(NewSettings(s => s.deckBandCells = 0).Validate(), Is.Not.Empty);
            Assert.That(NewSettings(s => s.maxPropHeightArtPx = 25).Validate(), Is.Not.Empty);
            Assert.That(NewSettings(s => s.maxPropHeightArtPx = 0).Validate(), Is.Not.Empty);
            Assert.That(NewSettings(s => s.propDensityPerStage = new[] { 0f, 1.5f, 0f }).Validate(), Is.Not.Empty);
            Assert.That(NewSettings(s => s.propDensityPerStage = new[] { 0f, 0f }).Validate(), Is.Not.Empty);
            Assert.That(NewSettings(s => s.edgeRulePerStage = new[] { EdgeRule.Railing }).Validate(), Is.Not.Empty);
            Assert.That(NewSettings(s => s.depotRulePortrait = new MarginCellRule(MarginSide.Top, 0)).Validate(), Is.Not.Empty, "trunks run in row gaps: Left or Right only");
            Assert.That(NewSettings(s => s.depotRuleLandscape = new MarginCellRule(MarginSide.Bottom, 0)).Validate(), Is.Not.Empty);
            Assert.That(NewSettings(s => s.propWeightsPerStage[1].weights = new[] { new PropWeight { kind = "crate", weight = -1f } }).Validate(), Is.Not.Empty);
            Assert.That(NewSettings(s => s.propWeightsPerStage[1].weights = new[] { new PropWeight { kind = "crate", weight = 2f } }).Validate(), Is.Empty);
        }

        [Test]
        public void ALargerMarginChangesOnlyTheMarginNeverTheBounds()
        {
            var settings = NewSettings(s => s.marginCells = 1.25f);
            var layout = new LatticeLayoutBuilder(LatticeLayoutBuilder.ShippedLocationCount, WorldOrientation.Portrait, settings).Build();
            Assert.That(layout.Margin, Is.EqualTo(1.25f));
            Assert.That(layout.Bounds, Is.EqualTo(SlotGridLayout.Extent(3)), "Bounds excludes the margin");
        }
    }
}
