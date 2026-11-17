using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using IdleFactory.Unity.Presentation;
using NUnit.Framework;
using UnityEngine;

namespace IdleFactory.Tests.EditMode.F006
{
    /// <summary>
    /// T527 (Feature 006, FR-011, FR-012, FR-014, FR-015, FR-037; research R06; contracts/world-presentation.md section 1): the deck layout.
    /// The rectangle grid and its seam test, the deck and hatch art per unlock state, the prop mask with its measured free area, the front-row
    /// edge pieces with their keep-out proof on real pixels, and the P1-F1, P1-F4 and P1-F6 settings items. Every number here is measured on the
    /// imported PNGs or on the layout geometry; nothing is read from a mock.
    /// </summary>
    public sealed class DeckLayoutTests
    {
        private const float Density = SlotGridLayout.Density;

        private static IEnumerable<string> Names => DeckTestSupport.LayoutNames;

        // ---- Grid and seam -------------------------------------------------------------------------------------------------------

        [Test]
        public void TheGridIsARectangleOf70By54ArtPixelsAndCellCentresAreTheFootprintPivots()
        {
            Assert.That(FactoryDeckView.CellSize.x, Is.EqualTo(70f / 24f).Within(1e-6f));
            Assert.That(FactoryDeckView.CellSize.y, Is.EqualTo(54f / 24f).Within(1e-6f));
            for (var y = -4; y <= 25; y++)
            {
                for (var x = -4; x <= 25; x++)
                {
                    var centre = new Vector2(
                        FactoryDeckView.GridOrigin.x + (x + 0.5f) * FactoryDeckView.CellSize.x,
                        FactoryDeckView.GridOrigin.y + (y + 0.5f) * FactoryDeckView.CellSize.y);
                    var pivot = FactoryWorldLayout.PivotOfCell(new Vector2Int(x, y));
                    Assert.That(Vector2.Distance(centre, pivot), Is.LessThan(2e-5f), "cell " + x + "," + y + ": the tile pivot is the slot pivot of the same lattice cell");
                }
            }
        }

        [Test]
        public void TheSeamTestForTheFloatCellSizeEveryCellEdgeIsAWholeArtPixelAndNeighboursTouchWithoutGapOrOverlap()
        {
            // 70/24 and 54/24 are not exact in binary floating point. A Tilemap places cell n at n * cellSize, so the error must stay far below the
            // 1/24 world unit of one art pixel for every cell a layout can use (the 20 location test layout reaches column 7 and row 6 with margins).
            for (var n = -8; n <= 40; n++)
            {
                var left = (FactoryDeckView.GridOrigin.x + n * FactoryDeckView.CellSize.x) * Density;
                var right = (FactoryDeckView.GridOrigin.x + (n + 1) * FactoryDeckView.CellSize.x) * Density;
                var bottom = (FactoryDeckView.GridOrigin.y + n * FactoryDeckView.CellSize.y) * Density;
                var top = (FactoryDeckView.GridOrigin.y + (n + 1) * FactoryDeckView.CellSize.y) * Density;
                Assert.That(left, Is.EqualTo(70 * n).Within(2e-3f), "left edge of column " + n);
                Assert.That(right, Is.EqualTo(70 * (n + 1)).Within(2e-3f), "right edge of column " + n);
                Assert.That(bottom, Is.EqualTo(54 * n - 12).Within(2e-3f), "bottom edge of row " + n);
                Assert.That(top, Is.EqualTo(54 * (n + 1) - 12).Within(2e-3f), "top edge of row " + n);
                Assert.That(Mathf.Abs(right - (70 * (n + 1))), Is.LessThan(1f / 100f), "the seam to column " + (n + 1) + " is under a hundredth of an art pixel");
                Assert.That(Mathf.RoundToInt(left), Is.EqualTo(PixelRect.FromWorld(FactoryWorldLayout.DeckTileRect(new Vector2Int(n, 0))).X));
            }

            var a = PixelRect.FromWorld(FactoryWorldLayout.DeckTileRect(new Vector2Int(3, 2)));
            var b = PixelRect.FromWorld(FactoryWorldLayout.DeckTileRect(new Vector2Int(4, 2)));
            var c = PixelRect.FromWorld(FactoryWorldLayout.DeckTileRect(new Vector2Int(3, 3)));
            Assert.That(a.XMax, Is.EqualTo(b.X), "horizontal neighbours share an edge");
            Assert.That(a.YMax, Is.EqualTo(c.Y), "vertical neighbours share an edge");
            Assert.That(a.Overlaps(b) || a.Overlaps(c), Is.False);
        }

        // ---- Cells ---------------------------------------------------------------------------------------------------------------

        [TestCaseSource(nameof(Names))]
        public void TheDeckPlanCoversEveryDeckCellAndEveryLocationShowsDeckOrHatchByItsUnlockState(string name)
        {
            var layout = DeckTestSupport.Layout(name);
            var conditions = new SlotCondition[layout.LocationCount];
            for (var i = 0; i < conditions.Length; i++)
            {
                conditions[i] = i == 0 ? SlotCondition.Occupied : i == 1 ? SlotCondition.Empty : SlotCondition.Locked;
            }

            var plan = DeckPlanner.Build(layout, conditions, 0, DeckTestSupport.Settings());
            var baseSet = new HashSet<Vector2Int>(plan.BaseCells);
            Assert.That(baseSet.Count, Is.EqualTo(plan.BaseCells.Count), "no base cell twice");
            foreach (var cell in layout.DeckCells)
            {
                Assert.That(baseSet.Contains(cell), Is.True, name + ": every deck cell has a base tile " + cell);
            }

            Assert.That(plan.LocationCells.Count, Is.EqualTo(layout.LocationCount));
            for (var i = 0; i < layout.LocationCount; i++)
            {
                Assert.That(plan.LocationCells[i], Is.EqualTo(layout.CellOf(new BuildingLocationId(i))));
                Assert.That(plan.LocationArt[i], Is.EqualTo(conditions[i] == SlotCondition.Locked ? DeckCellArt.Hatch : DeckCellArt.Deck),
                    name + " location " + i + ": a locked location shows the hatch (the padlock and the digit stay the slot's label overlay), an unlocked one the deck");
            }

            var unknown = DeckPlanner.Build(layout, null, 0, null);
            Assert.That(unknown.LocationArt.All(art => art == DeckCellArt.Hatch), Is.True, "without conditions every location is locked");
            Assert.That(plan.TileCount, Is.EqualTo(plan.BaseCells.Count + layout.LocationCount));
        }

        [Test]
        public void TheTileCountStaysWithinTheWorldLayerBudgetAtFitAll()
        {
            // Contract section 5: visible tiles <= (columns + 2 * margin) x (rows + 2 * margin) x 2 at fit-all. The extra front rows of the edge V are
            // part of the deck, so they are counted; the margin is the deck band of 1.
            foreach (var name in Names)
            {
                var layout = DeckTestSupport.Layout(name);
                foreach (var stage in new[] { 0, 1, 2 })
                {
                    var plan = DeckPlanner.Build(layout, DeckTestSupport.All(layout.LocationCount, SlotCondition.Occupied), stage, DeckTestSupport.Settings());
                    var budget = (layout.ColumnCount + 2 * layout.DeckBandCells) * (layout.RowCount + 2 * layout.DeckBandCells) * 2;
                    Assert.That(plan.TileCount, Is.LessThanOrEqualTo(budget), name + " stage " + stage + ": " + plan.TileCount + " tiles against " + budget);
                }
            }
        }

        // ---- Edges ---------------------------------------------------------------------------------------------------------------

        [TestCaseSource(nameof(Names))]
        public void EdgesAreFrontRowOnlyAndStageOneHasNoWallAndNoGantry(string name)
        {
            var layout = DeckTestSupport.Layout(name);
            var settings = DeckTestSupport.Settings();
            var expected = new[] { "RAILING", "WALL", "GANTRY" };
            var frontMarginPivotY = DeckPlanner.CellPivot(new Vector2Int(0, -1)).y;
            for (var stage = 0; stage < 3; stage++)
            {
                var plan = DeckPlanner.Build(layout, DeckTestSupport.All(layout.LocationCount, SlotCondition.Occupied), stage, settings);
                Assert.That(plan.Edges.Count, Is.EqualTo(2 * DeckPlanner.EdgePiecesPerSide(layout) + 1), name + ": one diagonal of one piece per column each side plus the apex");
                foreach (var edge in plan.Edges)
                {
                    Assert.That(edge.Key, Does.Contain("-" + expected[stage] + "-"), name + " stage " + stage);
                    Assert.That(edge.Anchor.y, Is.LessThanOrEqualTo(frontMarginPivotY), name + ": the pieces lie on the front margin row or below, never beside the lattice");
                }

                if (stage == 0)
                {
                    Assert.That(plan.Edges.Any(e => e.Key.Contains("-WALL-") || e.Key.Contains("-GANTRY-")), Is.False, "Art Bible 21.5: no wall and no gantry in stage 1");
                }

                var corner = plan.Edges.Single(e => e.Key.EndsWith("-CORNER", StringComparison.Ordinal));
                Assert.That(plan.Edges.All(e => e.Anchor.y >= corner.Anchor.y), Is.True, "the apex is the lowest point of the V");
                Assert.That(plan.Edges.Max(e => e.Order), Is.EqualTo(corner.Order), "the apex draws last");
            }
        }

        [Test]
        public void TheEdgeOpaqueTopConstantsMatchTheImportedPixels()
        {
            foreach (var rule in new[] { EdgeRule.Railing, EdgeRule.Wall, EdgeRule.Gantry })
            {
                foreach (var piece in new[] { "FL", "FR", "CORNER" })
                {
                    var key = DeckPlanner.EdgeKey(rule, piece);
                    Assert.That(DeckSpriteSize.TryOf(key, out var size), Is.True, key);
                    var pixels = DeckTestSupport.PixelsOfKey(key);
                    Assert.That(pixels.OpaqueTop() - size.PivotY, Is.LessThanOrEqualTo(DeckPlanner.EdgeOpaqueTop(rule)),
                        key + ": the planner's opaque top (a re-authored sprite must raise it)");
                }
            }
        }

        [TestCaseSource(nameof(Names))]
        public void KeepOutProofNoEdgePixelLiesInAnyHitShapeKeepOutRingExtentOrDepotAtAnyStage(string name)
        {
            // Art Bible 21.5 keep-out proof and 21.9 item 5 (P2-F7, P2-F23), measured on the opaque pixels of the placed sprites in the real layout.
            var layout = DeckTestSupport.Layout(name);
            var settings = DeckTestSupport.Settings();
            var extent = PixelRect.FromWorld(layout.Bounds);
            var keepOuts = new List<PixelRect>();
            for (var i = 0; i < layout.LocationCount; i++)
            {
                for (var k = 0; k < FactoryWorldLayout.KeepOutCount; k++)
                {
                    keepOuts.Add(PixelRect.FromWorld(layout.KeepOutOf(new BuildingLocationId(i), k)).Grown(DeckPlanner.RingPixels));
                }
            }

            DeckSpriteSize.TryOf("F006-DEPOT-S1-EMPTY", out var depotSize);
            var depot = depotSize.RectAt(new Vector2Int(Mathf.RoundToInt(layout.DepotAnchor.x * Density), Mathf.RoundToInt(layout.DepotAnchor.y * Density)));
            var deposit = DeckPlanner.CellRect(layout.DepositCell);
            for (var stage = 0; stage < 3; stage++)
            {
                var plan = DeckPlanner.Build(layout, DeckTestSupport.All(layout.LocationCount, SlotCondition.Occupied), stage, settings);
                var closest = int.MaxValue;
                var drawn = 0;
                foreach (var edge in plan.Edges)
                {
                    var pixels = DeckTestSupport.PixelsOfKey(edge.Key);
                    for (var y = 0; y < pixels.Height; y++)
                    {
                        for (var x = 0; x < pixels.Width; x++)
                        {
                            if (!pixels.IsOpaque(x, y))
                            {
                                continue;
                            }

                            drawn++;
                            var wx = edge.Rect.X + x;
                            var wy = edge.Rect.Y + y;
                            var probe = new PixelRect(wx, wy, 1, 1);
                            Assert.That(probe.Overlaps(extent), Is.False, name + " stage " + stage + " " + edge.Key + ": no pixel inside the content extent (plates, badge band) at " + probe);
                            foreach (var box in keepOuts)
                            {
                                Assert.That(probe.Overlaps(box), Is.False, name + " stage " + stage + " " + edge.Key + ": no pixel inside a badge or marker ring at " + probe);
                            }

                            Assert.That(probe.Overlaps(depot), Is.False, name + " stage " + stage + " " + edge.Key + ": no pixel on the depot canvas");
                            Assert.That(probe.Overlaps(deposit), Is.False, name + " stage " + stage + " " + edge.Key + ": no pixel on the deposit cell");
                            for (var i = 0; i < layout.LocationCount; i++)
                            {
                                Assert.That(layout.HitShapeOf(new BuildingLocationId(i)).Contains(new Vector2(wx + 0.5f, wy + 0.5f) / Density), Is.False,
                                    name + " stage " + stage + " " + edge.Key + ": no pixel inside a hit shape");
                            }

                            closest = Mathf.Min(closest, extent.Y - 1 - wy);
                        }
                    }
                }

                Assert.That(drawn, Is.GreaterThan(0));
                Assert.That(closest, Is.GreaterThanOrEqualTo(DeckPlanner.EdgeClearancePixels), name + " stage " + stage + ": the nearest edge pixel keeps the clearance below the extent");
                Debug.Log("[T527 keep-out] " + name + " stage " + stage + ": " + drawn + " opaque edge pixels checked; nearest to the content extent " + closest + " art px");
            }
        }

        // ---- Props ---------------------------------------------------------------------------------------------------------------

        [TestCaseSource(nameof(Names))]
        public void PropsFollowTheR06MaskTheyAreSmallDeterministicAndNeverOnAHitShapeKeepOutLaneDepotOrDeposit(string name)
        {
            var layout = DeckTestSupport.Layout(name);
            var settings = DeckTestSupport.Settings();
            var conditions = new SlotCondition[layout.LocationCount];
            for (var i = 0; i < conditions.Length; i++)
            {
                conditions[i] = i % 3 == 0 ? SlotCondition.Occupied : i % 3 == 1 ? SlotCondition.Empty : SlotCondition.Locked;
            }

            DeckSpriteSize.TryOf("F006-DEPOT-S1-EMPTY", out var depotSize);
            var depot = depotSize.RectAt(new Vector2Int(Mathf.RoundToInt(layout.DepotAnchor.x * Density), Mathf.RoundToInt(layout.DepotAnchor.y * Density)));
            var deposit = DeckPlanner.CellRect(layout.DepositCell);
            for (var stage = 0; stage < 3; stage++)
            {
                var plan = DeckPlanner.Build(layout, conditions, stage, settings);
                var again = DeckPlanner.Build(layout, conditions, stage, settings);
                Assert.That(again.Props.Select(p => p.Key + p.Anchor).ToArray(), Is.EqualTo(plan.Props.Select(p => p.Key + p.Anchor).ToArray()),
                    name + " stage " + stage + ": the cell hash makes the placement deterministic");
                Assert.That(plan.Props.Count, Is.LessThanOrEqualTo(Mathf.FloorToInt(plan.EligiblePropCells * DeckPlanner.MaxPropsPerEligibleCell)),
                    name + ": at most 0.5 props per eligible cell");
                Assert.That(plan.Props, Is.Not.Empty, name + " stage " + stage + ": the authored density places props");
                var cells = new HashSet<Vector2Int>();
                foreach (var prop in plan.Props)
                {
                    Assert.That(cells.Add(prop.Cell), Is.True, "one prop per cell");
                    Assert.That(prop.Rect.Height, Is.LessThanOrEqualTo(settings.maxPropHeightArtPx), prop.Key + ": at most 24 art px tall");
                    Assert.That(DeckPlanner.CellRect(prop.Cell).Contains(prop.Rect), Is.True, prop.Key + " stays inside its cell");
                    for (var y = prop.Rect.Y; y < prop.Rect.YMax; y++)
                    {
                        for (var x = prop.Rect.X; x < prop.Rect.XMax; x++)
                        {
                            var probe = new PixelRect(x, y, 1, 1);
                            var world = new Vector2(x + 0.5f, y + 0.5f) / Density;
                            for (var i = 0; i < layout.LocationCount; i++)
                            {
                                var id = new BuildingLocationId(i);
                                Assert.That(layout.HitShapeOf(id).Contains(world), Is.False, name + " " + prop.Key + ": never inside a hit shape");
                                for (var k = 0; k < FactoryWorldLayout.KeepOutCount; k++)
                                {
                                    Assert.That(probe.Overlaps(PixelRect.FromWorld(layout.KeepOutOf(id, k)).Grown(DeckPlanner.RingPixels)), Is.False,
                                        name + " " + prop.Key + ": never inside a badge or marker keep-out ring");
                                }

                                foreach (var lane in new[] { layout.BranchOf(id) })
                                {
                                    AssertOffLane(lane, probe, name + " " + prop.Key);
                                }
                            }

                            for (var row = 0; row < layout.RowCount; row++)
                            {
                                AssertOffLane(layout.TrunkOf(row), probe, name + " " + prop.Key);
                            }

                            Assert.That(probe.Overlaps(depot), Is.False, name + " " + prop.Key + ": never on the depot");
                            Assert.That(probe.Overlaps(deposit), Is.False, name + " " + prop.Key + ": never on the deposit");
                            foreach (var edge in plan.Edges)
                            {
                                Assert.That(probe.Overlaps(edge.Rect), Is.False, name + " " + prop.Key + ": never on an edge piece");
                            }
                        }
                    }

                    if (layout.DeckCells.Contains(prop.Cell) && IsLocationCell(layout, prop.Cell, out var locationId))
                    {
                        Assert.That(conditions[locationId], Is.Not.EqualTo(SlotCondition.Occupied), name + ": a prop stands only on an empty or locked cell");
                    }
                }

                // The settings carry no value, so a null composition places nothing (the F004-reproducing default).
                Assert.That(DeckPlanner.Build(layout, conditions, stage, null).Props, Is.Empty);
            }

            var occupied = DeckPlanner.Build(layout, DeckTestSupport.All(layout.LocationCount, SlotCondition.Occupied), 2, settings);
            Assert.That(occupied.Props.All(p => !IsLocationCell(layout, p.Cell, out _)), Is.True, name + ": with every location occupied the props stay in the margin ring");
        }

        private static void AssertOffLane(LanePath lane, PixelRect probe, string where)
        {
            if (lane == null || lane.IsEmpty)
            {
                return;
            }

            for (var s = 0; s < lane.SegmentCount; s++)
            {
                Assert.That(probe.Overlaps(PixelRect.FromWorld(lane.SegmentRect(s)).Grown(DeckPlanner.LaneGuardPixels)), Is.False, where + ": never on a lane");
            }
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

        [Test]
        public void TheFreeAreaOfThePropMaskIsMeasuredPerEligibleCellAndLogged()
        {
            // Free area = the art pixels of a cell left after every hit shape, keep-out ring, lane guard band, the depot, the deposit and the stage's
            // edge pieces are removed. A margin cell far from all of them is free except for its lane band; a location cell keeps its four corners.
            var settings = DeckTestSupport.Settings();
            var report = new StringBuilder("[T527 prop mask] free art pixels per eligible cell (cell area 3780), stage 0, every location locked\n");
            foreach (var name in Names)
            {
                var layout = DeckTestSupport.Layout(name);
                var eligible = DeckPlanner.EligibleCells(layout, null);
                Assert.That(eligible.Contains(layout.DepotCell), Is.False, "the depot cell is not eligible");
                Assert.That(eligible.Contains(layout.DepositCell), Is.False, "the deposit cell is not eligible");
                var margin = new List<int>();
                var location = new List<int>();
                foreach (var cell in eligible)
                {
                    var free = DeckPlanner.FreeAreaPixels(layout, cell, 0, settings);
                    Assert.That(free, Is.InRange(0, 70 * 54), name + " " + cell);
                    (IsLocationCell(layout, cell, out _) ? location : margin).Add(free);
                }

                Assert.That(margin, Is.Not.Empty, name);
                Assert.That(location, Is.Not.Empty, name);
                Assert.That(margin.Max(), Is.GreaterThan(3000), name + ": a free margin cell keeps almost its whole area");
                Assert.That(location.Max(), Is.LessThan(margin.Max()), name + ": the hit shape and the keep-outs take area from a location cell");
                Assert.That(location.All(free => free > 0), Is.True, name + ": every location cell keeps its corners");
                report.AppendLine(name + ": " + eligible.Count + " eligible (" + margin.Count + " margin, " + location.Count + " location); margin free min " + margin.Min() + " median "
                    + margin.OrderBy(v => v).ElementAt(margin.Count / 2) + " max " + margin.Max() + "; location free min " + location.Min() + " median "
                    + location.OrderBy(v => v).ElementAt(location.Count / 2) + " max " + location.Max());
            }

            Debug.Log(report.ToString());
        }

        [Test]
        public void AUsedPropKindOutsideTheCatalogOrTallerThanTheLimitIsSkippedNotDrawn()
        {
            var settings = UnityEngine.Object.Instantiate(DeckTestSupport.Settings());
            try
            {
                settings.propWeightsPerStage[0].weights = new[] { new PropWeight { kind = "no-such-prop", weight = 1f } };
                settings.propDensityPerStage[0] = 0.5f;
                var layout = DeckTestSupport.Layout("3x4");
                Assert.That(DeckPlanner.Build(layout, null, 0, settings).Props, Is.Empty, "an unknown kind places nothing");
                settings.propWeightsPerStage[0].weights = new[] { new PropWeight { kind = "crate-stack", weight = 1f } };
                settings.maxPropHeightArtPx = 20;
                Assert.That(DeckPlanner.Build(layout, null, 0, settings).Props, Is.Empty, "crate-stack is 24 px tall: over a 20 px limit it is never placed");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(settings);
            }
        }
    }
}
