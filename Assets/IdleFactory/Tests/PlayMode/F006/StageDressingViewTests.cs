using System.Collections;
using System.Linq;
using IdleFactory.Unity.Presentation;
using IdleFactory.Unity.UI;
using IdleFactory.Unity.UI.Panels;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace IdleFactory.Tests.PlayMode.F006
{
    /// <summary>
    /// T540 (Feature 006, FR-035, FR-036, research R14, R15): the deck view's stage dressing (the lit margin ring, props only on deck), the milestone feedback (the 2 s
    /// foundation highlight and the deck reveal, static under reduced motion), and the T539 follow-ups C-42 and C-43. The world is the synthetic rig of the deck tests.
    /// </summary>
    public sealed class StageDressingViewTests
    {
        private DeckTestRig rig;

        [SetUp]
        public void SetUp() => rig = new DeckTestRig();

        [TearDown]
        public void TearDown() => rig.Dispose();

        private FactoryDeckView World(int occupied, out SlotGridView grid)
        {
            var layout = LatticeLayoutBuilder.ShippedForColumns(3);
            var catalog = rig.Catalog();
            grid = rig.NewGrid(catalog, layout);
            var deck = rig.NewDeck(catalog, rig.Settings(), grid);
            grid.SetProjectedPixelsPerCell(24f);
            grid.Render(DeckTestRig.Frame(12, i => i < occupied ? SlotCondition.Occupied : SlotCondition.Locked));
            return deck;
        }

        [Test]
        public void TheLitRingGrowsWithTheStageAndASwappedBaseTileKeepsTheTileCount()
        {
            var deck = World(12, out _);
            var baseCountBefore = DeckTestRig.TileCount(deck.BaseTilemap);
            var counts = new int[3];
            for (var stage = 0; stage < 3; stage++)
            {
                deck.SetDevelopment(new DevelopmentStageValue(stage, 0, 0));
                Assert.That(deck.Stage, Is.EqualTo(stage));
                Assert.That(deck.Plan.Dressed, Is.True);
                counts[stage] = deck.Plan.LitRingCells.Count;
                Assert.That(DeckTestRig.TileCount(deck.BaseTilemap), Is.EqualTo(deck.Plan.BaseCells.Count), "every base cell keeps one tile");
                var deckSprite = deck.DeckTilemap.GetSprite(new Vector3Int(deck.Plan.LocationCells[0].x, deck.Plan.LocationCells[0].y, 0));
                foreach (var cell in deck.Plan.LitRingCells)
                {
                    Assert.That(deck.BaseTilemap.GetSprite(new Vector3Int(cell.x, cell.y, 0)), Is.SameAs(deckSprite), "a lit ring cell shows the deck tile, stage " + stage);
                }

                var lit = deck.Plan.LitRingCells.ToList();
                foreach (var cell in deck.Plan.BaseCells.Where(c => !lit.Contains(c) && !deck.Plan.LocationCells.Contains(c)).Take(3))
                {
                    Assert.That(deck.BaseTilemap.GetSprite(new Vector3Int(cell.x, cell.y, 0)), Is.Not.SameAs(deckSprite), "an unlit cell keeps the dark base, stage " + stage);
                }

                foreach (var prop in deck.Plan.Props)
                {
                    Assert.That(lit.Contains(prop.Cell) || deck.Plan.LocationCells.Contains(prop.Cell), Is.True, "no prop over the dark base");
                }
            }

            Assert.That(baseCountBefore, Is.GreaterThan(0));
            Assert.That(counts[0], Is.Zero);
            Assert.That(counts[1], Is.GreaterThan(0).And.LessThan(counts[2]));
        }

        [Test]
        public void AnUnchangedDevelopmentValueRedrawsNothing()
        {
            var deck = World(12, out _);
            var value = new DevelopmentStageValue(2, 1, 1);
            deck.SetDevelopment(value);
            var plan = deck.Plan;
            deck.SetDevelopment(value);
            Assert.That(deck.Plan, Is.SameAs(plan), "the same value does not re-plan");
            Assert.That(deck.DensityBin, Is.EqualTo(2));
            deck.SetDevelopment(new DevelopmentStageValue(2, 2, 1));
            Assert.That(deck.Plan, Is.Not.SameAs(plan), "a new bin does");
        }

        [Test]
        public void AMilestoneHighlightsTheNewFoundationsForTwoSecondsAndFadesOnlyInNormalMotion()
        {
            var deck = World(8, out var grid);
            deck.SetDevelopment(new DevelopmentStageValue(2, 0, 0));
            deck.ShowMilestone(4, 2, true, 10_000, false);
            Assert.That(deck.ActiveHighlightCount, Is.EqualTo(2));
            Assert.That(deck.RevealActive, Is.True, "a stage raise eases the deck up in normal motion");
            for (var i = 0; i < 2; i++)
            {
                var renderer = deck.HighlightRenderers[i];
                Assert.That(renderer.enabled, Is.True);
                var anchor = grid.Layout.AnchorOf(new BuildingLocationId(4 + i));
                Assert.That(renderer.transform.localPosition.x, Is.EqualTo(anchor.x).Within(1f / 24f));
                Assert.That(renderer.transform.localPosition.y, Is.EqualTo(anchor.y).Within(1f / 24f));
                Assert.That(renderer.sortingLayerID, Is.EqualTo(SortingLayer.NameToID(SlotGridView.OverlaySortingLayer)));
            }

            deck.TickFeedback(10_300);
            Assert.That(deck.BaseTilemap.color.r, Is.InRange(0.55f, 0.99f), "mid-reveal the deck is dimmed");
            deck.TickFeedback(10_700);
            Assert.That(deck.RevealActive, Is.False);
            Assert.That(deck.BaseTilemap.color.r, Is.EqualTo(1f).Within(1e-4f));
            deck.TickFeedback(11_500);
            Assert.That(deck.HighlightRenderers[0].color.a, Is.EqualTo(1f).Within(1e-4f), "still fully drawn before the fade");
            deck.TickFeedback(11_900);
            Assert.That(deck.HighlightRenderers[0].color.a, Is.InRange(0.01f, 0.99f), "fading over the last 0.4 s");
            deck.TickFeedback(12_000);
            Assert.That(deck.ActiveHighlightCount, Is.Zero, "gone after 2 s");
            Assert.That(deck.HighlightRenderers.All(r => !r.enabled), Is.True);
        }

        [Test]
        public void UnderReducedMotionTheHighlightIsStaticForTwoSecondsAndNothingEases()
        {
            var deck = World(8, out _);
            deck.SetDevelopment(new DevelopmentStageValue(2, 0, 0));
            deck.ShowMilestone(4, 2, true, 5_000, true);
            Assert.That(deck.RevealActive, Is.False, "no reveal under reduced motion");
            Assert.That(deck.BaseTilemap.color.r, Is.EqualTo(1f).Within(1e-4f), "the deck is simply there");
            foreach (var ms in new long[] { 5_100, 5_800, 6_500, 6_900, 6_999 })
            {
                deck.TickFeedback(ms);
                Assert.That(deck.ActiveHighlightCount, Is.EqualTo(2), "static, unchanged at " + ms);
                Assert.That(deck.HighlightRenderers[0].color.a, Is.EqualTo(1f).Within(1e-4f), "no fade at " + ms);
            }

            deck.TickFeedback(7_000);
            Assert.That(deck.ActiveHighlightCount, Is.Zero, "off after 2 s");
        }

        [Test]
        public void AMilestoneWithoutNewLocationsOrAMissingSpriteDrawsNothingAndNeverThrows()
        {
            var deck = World(8, out _);
            deck.ShowMilestone(-1, 0, false, 1_000, false);
            Assert.That(deck.ActiveHighlightCount, Is.Zero);
            deck.ShowMilestone(11, 5, false, 1_000, false);
            Assert.That(deck.ActiveHighlightCount, Is.EqualTo(1), "locations past the layout are cut off");
            deck.ClearFeedback();
            Assert.That(deck.ActiveHighlightCount, Is.Zero);
        }

        // ---- follow-ups ---------------------------------------------------------------------------------------------------------

        [Test]
        public void C42ADestroyedPreviewViewIsNotAliveThroughTheInterface()
        {
            var host = new GameObject("c42-grid");
            var grid = host.AddComponent<SlotGridView>();
            IBuildPreview preview = grid;
            Assert.That(BuildCard.Alive(preview), Is.SameAs(preview), "a live view stays");
            Assert.That(BuildCard.Alive(null), Is.Null);
            Object.DestroyImmediate(host);
            Assert.That(preview == null, Is.False, "the interface reference is not null: the trap C-42 names");
            Assert.That(BuildCard.Alive(preview), Is.Null, "the guard sees the destroyed Unity object");
        }

        [Test]
        public void C43TheRebindPicksTheCardPanelFromTheBuildCardState()
        {
            var machine = new FakePanel("machine");
            var build = new FakePanel("build");
            Assert.That(RunShellDocument.PickCardPanel(false, machine, build), Is.SameAs(machine));
            Assert.That(RunShellDocument.PickCardPanel(true, machine, build), Is.SameAs(build), "an open build card keeps the build panel");
            Assert.That(RunShellDocument.PickCardPanel(true, machine, null), Is.SameAs(machine), "no build panel registered: the machine one");
            Assert.That(RunShellDocument.PickCardPanel(false, null, build), Is.SameAs(build));
            Assert.That(RunShellDocument.PickCardPanel(false, null, null), Is.Null);
        }

        private sealed class FakePanel : IShellPanel
        {
            public FakePanel(string id) => Id = id;

            public string Id { get; }
            public string Title => Id;
            public UnityEngine.UIElements.VisualElement Root => null;
            public void OnShown(ShellPanelContext context) { }
            public void OnHidden() { }
            public void Refresh() { }
        }
    }
}
