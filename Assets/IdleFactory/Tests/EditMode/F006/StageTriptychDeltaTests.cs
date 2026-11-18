using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using IdleFactory.Unity.Presentation;
using NUnit.Framework;
using UnityEngine;

namespace IdleFactory.Tests.EditMode.F006
{
    /// <summary>
    /// T540 (Feature 006, FR-036, SC-006, research R15, contracts/world-presentation.md section 3): the minimum measurable delta between adjacent development stages,
    /// measured on the real planner and the composited triptych of the shipped pixels. (1) The deck-lit share of the deck rectangle differs by at least 25
    /// percentage points between adjacent stages for every possible number of unlocked locations. (2) A grayscale difference is visible between adjacent stages at
    /// n_min. (3) Stage 3 still reads on a fully built factory (C-13). (4) No prop floats over the dark base. (5) The census stays inside Art Bible 21.12.
    /// </summary>
    public sealed class StageTriptychDeltaTests
    {
        private const float MinimumLitShareDelta = 0.25f;

        private static IEnumerable<string> Names => DeckTestSupport.LayoutNames;

        private static IReadOnlyList<SlotCondition> Unlocked(int count, int unlocked, SlotCondition unlockedCondition)
        {
            var result = new SlotCondition[count];
            for (var i = 0; i < count; i++)
            {
                result[i] = i < unlocked ? unlockedCondition : SlotCondition.Locked;
            }

            return result;
        }

        private static DeckPlan Plan(FactoryWorldLayout layout, int unlocked, SlotCondition unlockedCondition, int bin = 0)
        {
            var stage = DevelopmentStage.StageOf(unlocked, layout.LocationCount);
            return DeckPlanner.Build(layout, Unlocked(layout.LocationCount, unlocked, unlockedCondition), stage, DeckTestSupport.Settings(), new DeckDressing(bin));
        }

        // ---- (1) deck-lit share ---------------------------------------------------------------------------------------------------

        [TestCaseSource(nameof(Names))]
        public void TheDeckLitShareDiffersByAtLeastTwentyFivePointsBetweenAdjacentStagesForEveryUnlockCount(string name)
        {
            var layout = DeckTestSupport.Layout(name);
            var total = layout.LocationCount;
            var low = new float[3];
            var high = new float[3];
            for (var s = 0; s < 3; s++)
            {
                low[s] = float.MaxValue;
                high[s] = float.MinValue;
            }

            for (var unlocked = 0; unlocked <= total; unlocked++)
            {
                var plan = Plan(layout, unlocked, SlotCondition.Empty);
                var stage = DevelopmentStage.StageOf(unlocked, total);
                low[stage] = Mathf.Min(low[stage], plan.LitShare);
                high[stage] = Mathf.Max(high[stage], plan.LitShare);
            }

            var report = new StringBuilder("[T540 deck-lit share] " + name + ": ");
            for (var s = 0; s < 3; s++)
            {
                report.Append("stage " + s + " " + (low[s] * 100f).ToString("0.0") + " to " + (high[s] * 100f).ToString("0.0") + " %; ");
            }

            Debug.Log(report);
            Assert.That(low[1] - high[0], Is.GreaterThanOrEqualTo(MinimumLitShareDelta), name + ": Early to Partial, worst case (most Early against fewest Partial)");
            Assert.That(low[2] - high[1], Is.GreaterThanOrEqualTo(MinimumLitShareDelta), name + ": Partial to Established, worst case");
        }

        [Test]
        public void TheLitRingGrowsFromTheDepotOutwardAndIsNestedAcrossStages()
        {
            var layout = DeckTestSupport.Layout("3x4");
            var ring = DeckPlanner.RingCellsInLightingOrder(layout);
            Assert.That(ring.Contains(layout.DepotCell), Is.False);
            Assert.That(ring.Contains(layout.DepositCell), Is.False);
            var early = DeckPlanner.LitRingCells(layout, 0);
            var partial = DeckPlanner.LitRingCells(layout, 1);
            var established = DeckPlanner.LitRingCells(layout, 2);
            Assert.That(early, Is.Empty);
            Assert.That(partial.Count, Is.EqualTo((ring.Count + 1) / 2));
            Assert.That(established.Count, Is.EqualTo(ring.Count));
            Assert.That(partial.All(c => established.Contains(c)), Is.True, "a later stage only adds lit cells: a stage never takes light away");
            Assert.That(partial[0], Is.EqualTo(ring[0]));
            Assert.That(DeckPlanner.LitRingCells(layout, 1), Is.EqualTo(partial), "deterministic");
        }

        [TestCaseSource(nameof(Names))]
        public void TheTileCountIsUnchangedByTheDressingBecauseALitRingCellSwapsItsBaseTile(string name)
        {
            var layout = DeckTestSupport.Layout(name);
            var occupied = DeckTestSupport.All(layout.LocationCount, SlotCondition.Occupied);
            foreach (var stage in new[] { 0, 1, 2 })
            {
                var plain = DeckPlanner.Build(layout, occupied, stage, DeckTestSupport.Settings());
                var dressed = DeckPlanner.Build(layout, occupied, stage, DeckTestSupport.Settings(), new DeckDressing(4));
                Assert.That(dressed.TileCount, Is.EqualTo(plain.TileCount), name + " stage " + stage);
                Assert.That(dressed.Dressed, Is.True);
                Assert.That(plain.Dressed, Is.False);
                Assert.That(plain.LitRingCells, Is.Empty, "without the dressing the ring stays dark, as T527 built it");
                var budget = (layout.ColumnCount + 2 * layout.DeckBandCells) * (layout.RowCount + 2 * layout.DeckBandCells) * 2;
                Assert.That(dressed.TileCount, Is.LessThanOrEqualTo(budget), name + " stage " + stage + ": inside the world-layer tile budget");
            }
        }

        [TestCaseSource(nameof(Names))]
        public void LitGroundUsesOnlyRingSafeTones(string name)
        {
            // A lit ring cell draws the deck tile where the dark base tile was; the C8 ring proof allows the ground tones only if they are in set R.
            foreach (var key in new[] { "F006-GROUND-BASE", "F006-GROUND-DECK", "F006-GROUND-HATCH" })
            {
                var sprite = DeckTestSupport.PixelsOfKey(key);
                foreach (var pixel in sprite.Pixels.Where(p => p.a > 0).Distinct())
                {
                    var tone = ShippedArt.ToneOf(pixel);
                    Assert.That(DeckCompositeProofTests.InRingSafeSet(tone), Is.True, key + " uses " + tone);
                }
            }
        }

        // ---- (2) grayscale delta at n_min ------------------------------------------------------------------------------------------

        private static byte Gray(DeckComposite c, int x, int y)
        {
            if (c.TopAt(x, y) == DeckLayer.None)
            {
                return 0;
            }

            var p = c.PixelAt(x, y);
            return (byte)Mathf.RoundToInt(0.2126f * p.r + 0.7152f * p.g + 0.0722f * p.b);
        }

        private static DeckComposite Composite(FactoryWorldLayout layout, int unlocked)
        {
            var stage = DevelopmentStage.StageOf(unlocked, layout.LocationCount);
            var conditions = Unlocked(layout.LocationCount, unlocked, SlotCondition.Occupied);
            var plan = DeckPlanner.Build(layout, conditions, stage, DeckTestSupport.Settings(), new DeckDressing(2));
            return new DeckComposite(layout, plan, "F006-DEPOT-S" + (stage + 1) + "-HIGH", "F006-DEPOSIT-REST-S" + (stage + 1), true, conditions);
        }

        [TestCase("3x4")]
        [TestCase("4x3")]
        public void AdjacentStagesDifferInGrayscaleAtNMin(string name)
        {
            // n_min is a whole-number device scale, so the composite at n_min is the art-pixel composite with every pixel repeated n x n: the share of differing pixels
            // is the same at every n and is measured at n = 1. The same factory is composed at u = 3, 6 and 10 of 12 (Early, Partial, Established), all unlocked
            // locations built, and a pixel counts as different when its gray (Rec. 709 luma of the sRGB value) moves by at least 12 of 255.
            var layout = DeckTestSupport.Layout(name);
            var unlockedPerStage = new[] { 3, 6, 10 };
            var composites = unlockedPerStage.Select(u => Composite(layout, u)).ToArray();
            var report = new StringBuilder("[T540 grayscale delta] " + name + ": ");
            for (var s = 0; s < 2; s++)
            {
                var a = composites[s];
                var b = composites[s + 1];
                var x0 = Mathf.Min(a.Area.X, b.Area.X);
                var y0 = Mathf.Min(a.Area.Y, b.Area.Y);
                var x1 = Mathf.Max(a.Area.XMax, b.Area.XMax);
                var y1 = Mathf.Max(a.Area.YMax, b.Area.YMax);
                var different = 0;
                long sumDelta = 0;
                for (var y = y0; y < y1; y++)
                {
                    for (var x = x0; x < x1; x++)
                    {
                        var d = Mathf.Abs(Gray(a, x, y) - Gray(b, x, y));
                        sumDelta += d;
                        different += d >= 12 ? 1 : 0;
                    }
                }

                var area = (x1 - x0) * (y1 - y0);
                var share = different / (float)area;
                var mean = sumDelta / (float)area;
                report.Append("stage " + s + " to " + (s + 1) + ": " + (share * 100f).ToString("0.0") + " % of pixels differ by >= 12, mean gray delta " + mean.ToString("0.00") + "; ");
                Assert.That(share, Is.GreaterThanOrEqualTo(0.05f), name + ": stage " + s + " to " + (s + 1) + " differs visibly in grayscale (at least 5 % of the composite)");
            }

            Debug.Log(report);
        }

        // ---- (3) stage 3 on a fully built factory (C-13) --------------------------------------------------------------------------

        [TestCaseSource(nameof(Names))]
        public void StageThreeStillReadsOnAFullyBuiltFactoryThroughPropsLitDeckEdgesAndTheDepot(string name)
        {
            var layout = DeckTestSupport.Layout(name);
            var full = DeckTestSupport.All(layout.LocationCount, SlotCondition.Occupied);
            var settings = DeckTestSupport.Settings();
            var counts = new int[3];
            var report = new StringBuilder("[T540 full factory] " + name + ": ");
            for (var stage = 0; stage < 3; stage++)
            {
                var plan = DeckPlanner.Build(layout, full, stage, settings, new DeckDressing(4));
                counts[stage] = plan.Props.Count;
                report.Append("stage " + stage + " props " + plan.Props.Count + " (eligible " + plan.EligiblePropCells + "), lit " + (plan.LitShare * 100f).ToString("0") + " %, edge " + plan.EdgeRule + "; ");
                Assert.That(plan.Props.All(p => plan.LitRingCells.Contains(p.Cell)), Is.True, name + " stage " + stage + ": with every location built a prop stands on a lit ring cell");
            }

            Debug.Log(report);
            Assert.That(counts[2], Is.GreaterThan(counts[1]), name + ": Established has more props than Partial on a full factory (the density still reads)");
            Assert.That(counts[2], Is.GreaterThan(counts[0]), name + ": and more than Early");
            Assert.That(counts[2], Is.GreaterThanOrEqualTo(3), name + ": a full factory at Established carries at least three props");
        }

        // ---- (4) no floating props -----------------------------------------------------------------------------------------------

        [TestCaseSource(nameof(Names))]
        public void NoPropStandsOverTheDarkBaseAtAnyStageOrCondition(string name)
        {
            var layout = DeckTestSupport.Layout(name);
            var settings = DeckTestSupport.Settings();
            var mixes = new Func<int, SlotCondition>[]
            {
                i => SlotCondition.Occupied,
                i => SlotCondition.Empty,
                i => i % 3 == 0 ? SlotCondition.Occupied : i % 3 == 1 ? SlotCondition.Empty : SlotCondition.Locked,
            };
            foreach (var mix in mixes)
            {
                var conditions = Enumerable.Range(0, layout.LocationCount).Select(mix).ToArray();
                for (var stage = 0; stage < 3; stage++)
                {
                    var plan = DeckPlanner.Build(layout, conditions, stage, settings, new DeckDressing(2));
                    var locationCells = new HashSet<Vector2Int>(plan.LocationCells);
                    foreach (var prop in plan.Props)
                    {
                        var onDeck = plan.LitRingCells.Contains(prop.Cell) || locationCells.Contains(prop.Cell);
                        Assert.That(onDeck, Is.True, name + " stage " + stage + ": prop " + prop.Key + " at " + prop.Cell + " would float over the dark base");
                    }
                }
            }
        }

        // ---- (5) census -----------------------------------------------------------------------------------------------------------

        [TestCase("3x4", 7)]
        [TestCase("4x3", 9)]
        public void TheWorldSpriteCensusStaysInsideArtBible2112AtTwelveLocations(string name, int edgePieces)
        {
            // 21.12: world sprite renderers at most 41 = 12 pips + 8 FX + props + 1 depot + 2 deposit + edge pieces; props therefore at most 9. Renderers in the camera:
            // F004 cap 61 + 41 + 3 Tilemaps = 105.
            var layout = DeckTestSupport.Layout(name);
            var settings = DeckTestSupport.Settings();
            var worst = 0;
            var worstEdges = 0;
            foreach (var stage in new[] { 0, 1, 2 })
            {
                foreach (var bin in new[] { 0, 4 })
                {
                    foreach (var mix in new Func<int, SlotCondition>[] { i => SlotCondition.Empty, i => SlotCondition.Locked, i => SlotCondition.Occupied, i => i % 2 == 0 ? SlotCondition.Empty : SlotCondition.Locked })
                    {
                        var conditions = Enumerable.Range(0, layout.LocationCount).Select(mix).ToArray();
                        var plan = DeckPlanner.Build(layout, conditions, stage, settings, new DeckDressing(bin));
                        worst = Mathf.Max(worst, plan.Props.Count);
                        worstEdges = Mathf.Max(worstEdges, plan.Edges.Count);
                    }
                }
            }

            var sprites = layout.LocationCount + EffectPool.RendererCount + worst + 1 + 2 + worstEdges;
            var renderers = 61 + sprites + 3;
            Debug.Log("[T540 census] " + name + ": worst props " + worst + ", edge pieces " + worstEdges + ", world sprites " + sprites + " (limit 41), camera renderers " + renderers + " (limit 105)");
            Assert.That(worstEdges, Is.EqualTo(edgePieces));
            Assert.That(worst, Is.LessThanOrEqualTo(9), "props at most 0.5 per ring cell");
            Assert.That(sprites, Is.LessThanOrEqualTo(41));
            Assert.That(renderers, Is.LessThanOrEqualTo(105));
        }

        [TestCaseSource(nameof(Names))]
        public void TheDressedPlanIsDeterministic(string name)
        {
            var layout = DeckTestSupport.Layout(name);
            var settings = DeckTestSupport.Settings();
            var conditions = DeckTestSupport.All(layout.LocationCount, SlotCondition.Occupied);
            var a = DeckPlanner.Build(layout, conditions, 2, settings, new DeckDressing(3));
            var b = DeckPlanner.Build(layout, conditions, 2, settings, new DeckDressing(3));
            Assert.That(b.Props.Select(p => p.Key + p.Anchor).ToArray(), Is.EqualTo(a.Props.Select(p => p.Key + p.Anchor).ToArray()));
            Assert.That(b.LitRingCells, Is.EqualTo(a.LitRingCells));
        }

        [Test]
        public void ASecondaryBinOnlyAddsProps()
        {
            var layout = DeckTestSupport.Layout("3x4");
            var settings = DeckTestSupport.Settings();
            var conditions = DeckTestSupport.All(layout.LocationCount, SlotCondition.Empty);
            var low = DeckPlanner.Build(layout, conditions, 1, settings, new DeckDressing(0)).Props.Select(p => p.Cell).ToList();
            var high = DeckPlanner.Build(layout, conditions, 1, settings, new DeckDressing(4)).Props.Select(p => p.Cell).ToList();
            Assert.That(high.Count, Is.GreaterThanOrEqualTo(low.Count));
        }
    }
}
