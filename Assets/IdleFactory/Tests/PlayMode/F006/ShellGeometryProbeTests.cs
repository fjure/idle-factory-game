#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using IdleFactory.Unity.Presentation;
using IdleFactory.Unity.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace IdleFactory.Tests.PlayMode.F006
{
    /// <summary>
    /// Self-test of the F006 baseline probe (T502): its arithmetic on synthetic layouts, its visible-label rule on a synthetic tree and
    /// its live reading of a run shell. A probe that is wrong would make every later before/after comparison wrong.
    /// </summary>
    public sealed class ShellGeometryProbeTests
    {
        private readonly List<ShellRig> rigs = new List<ShellRig>();

        [TearDown]
        public void TearDown()
        {
            foreach (var rig in rigs)
            {
                rig.Dispose();
            }

            rigs.Clear();
        }

        // ---------------------------------------------------------------------------------------------------
        // Synthetic geometry
        // ---------------------------------------------------------------------------------------------------

        [Test]
        public void TheUnionCountsOverlapsOnceAndClipsToTheSafeArea()
        {
            var safe = new Rect(0f, 0f, 100f, 200f);

            Assert.That(ShellGeometryProbe.UnionArea(new List<Rect>(), safe), Is.EqualTo(0d));
            Assert.That(ShellGeometryProbe.UnionArea(new[] { new Rect(0f, 0f, 100f, 30f) }, safe), Is.EqualTo(3000d), "one strip");
            Assert.That(ShellGeometryProbe.UnionArea(new[] { new Rect(0f, 0f, 100f, 30f), new Rect(0f, 170f, 100f, 30f) }, safe), Is.EqualTo(6000d), "two disjoint strips");
            Assert.That(ShellGeometryProbe.UnionArea(new[] { new Rect(0f, 0f, 100f, 30f), new Rect(0f, 10f, 100f, 30f) }, safe), Is.EqualTo(4000d), "two overlapping strips count once");
            Assert.That(ShellGeometryProbe.UnionArea(new[] { new Rect(0f, 0f, 100f, 30f), new Rect(10f, 5f, 20f, 10f) }, safe), Is.EqualTo(3000d), "a contained rect adds nothing");
            Assert.That(ShellGeometryProbe.UnionArea(new[] { new Rect(-50f, -50f, 100f, 100f) }, safe), Is.EqualTo(2500d), "clipped at the corner of the safe area");
            Assert.That(ShellGeometryProbe.UnionArea(new[] { new Rect(200f, 200f, 10f, 10f) }, safe), Is.EqualTo(0d), "outside the safe area");

            // An L shape of two rectangles that overlap in one corner: 40*10 + 10*40 - 10*10.
            Assert.That(ShellGeometryProbe.UnionArea(new[] { new Rect(0f, 0f, 40f, 10f), new Rect(0f, 0f, 10f, 40f) }, safe), Is.EqualTo(700d));
        }

        [Test]
        public void SharesFollowTheirDefinitionOnASyntheticPhoneLayout()
        {
            var safe = new Rect(0f, 24f, 100f, 200f);
            var result = new ShellGeometryResult
            {
                Screen = "synthetic",
                State = "no panel",
                TextScalePercent = 100,
                PixelsPerPoint = 1f,
                Safe = safe,
                World = new Rect(0f, 54f, 100f, 140f),
                Sheet = null,
            };
            result.Chrome.Add(new NamedRect("top-strip", new Rect(0f, 0f, 100f, 54f)));
            result.Chrome.Add(new NamedRect("tab-bar", new Rect(0f, 194f, 100f, 30f)));
            result.Chrome.Add(new NamedRect("zoom-column", new Rect(0f, 54f, 20f, 40f)));

            Assert.That(result.WorldShare, Is.EqualTo(0.7d).Within(1e-9), "140 of 200");
            // The strip reaches above the safe area; its part inside is 30 high. The tab bar adds 30, the zoom column 800.
            Assert.That(result.ChromeShare, Is.EqualTo((3000d + 3000d + 800d) / 20000d).Within(1e-9));
            Assert.That(result.SheetShare, Is.EqualTo(0d));
            Assert.That(result.OverlayShare, Is.EqualTo(0d));
        }

        [Test]
        public void AContainedShareIsTheFractionOfTheInnerRectInsideTheOuterOne()
        {
            var outer = new Rect(0f, 0f, 100f, 100f);
            Assert.That(ShellGeometryProbe.ContainedShare(new Rect(10f, 10f, 20f, 20f), outer), Is.EqualTo(1d));
            Assert.That(ShellGeometryProbe.ContainedShare(new Rect(90f, 0f, 20f, 10f), outer), Is.EqualTo(0.5d).Within(1e-9));
            Assert.That(ShellGeometryProbe.ContainedShare(new Rect(200f, 0f, 20f, 10f), outer), Is.EqualTo(0d));
        }

        [Test]
        public void TheJsonIsDeterministicAndCarriesTheCounts()
        {
            var result = new ShellGeometryResult { Screen = "synthetic", State = "no panel", TextScalePercent = 100, PixelsPerPoint = 1f, Safe = new Rect(0f, 0f, 100f, 100f), World = new Rect(0f, 20f, 100f, 60f) };
            result.Chrome.Add(new NamedRect("top-strip", new Rect(0f, 0f, 100f, 20f)));
            result.Texts.Add(new ProbeText("top-strip", "Cash \"1\"", true));
            result.Texts.Add(new ProbeText("tab-bar", "Market", false));
            result.Notes["a"] = 1.5d;

            var first = ShellGeometryProbe.ToJson(result, string.Empty);
            var second = ShellGeometryProbe.ToJson(result, string.Empty);

            Assert.That(second, Is.EqualTo(first), "no state leaks between two serialisations");
            Assert.That(first, Does.Contain("\"labelCount\": 1"));
            Assert.That(first, Does.Contain("\"textElementCount\": 2"));
            Assert.That(first, Does.Contain("\"worldShare\": 0.6"));
            Assert.That(first, Does.Contain("\"chromeShare\": 0.2"));
            Assert.That(first, Does.Contain("Cash \\\"1\\\""), "text is escaped");
            Assert.That(first, Does.Contain("\"a\": 1.5"));
        }

        // ---------------------------------------------------------------------------------------------------
        // Synthetic tree: which text counts
        // ---------------------------------------------------------------------------------------------------

        [UnityTest]
        public IEnumerator OnlyVisibleLabelsWithTextAreCounted()
        {
            var screen = new ShellScreen("synthetic 300x300", 300, 300, 0, 0, 0, 0, FactoryHudLayout.CompactPortrait);
            using (var rig = new OfflineSummaryPanelTestSupport.StandaloneRig(screen))
            {
                var root = new VisualElement { name = "synthetic-root" };
                root.style.position = Position.Absolute;
                root.style.left = 0;
                root.style.top = 0;
                root.style.width = 300;
                root.style.height = 300;
                rig.Root.Add(root);

                Label Add(VisualElement parent, string name, string text, float x, float y)
                {
                    var label = new Label(text) { name = name };
                    label.style.position = Position.Absolute;
                    label.style.left = x;
                    label.style.top = y;
                    label.style.width = 80;
                    label.style.height = 20;
                    parent.Add(label);
                    return label;
                }

                Add(root, "counted-1", "Cash", 0, 0);
                Add(root, "counted-2", "Store 4/16", 0, 30);
                Add(root, "empty", string.Empty, 0, 60);
                Add(root, "blank", "   ", 0, 90);
                var hidden = Add(root, "display-none", "Hidden", 0, 120);
                hidden.style.display = DisplayStyle.None;
                var invisible = Add(root, "invisible", "Invisible", 0, 150);
                invisible.style.visibility = Visibility.Hidden;
                var transparent = Add(root, "transparent", "Transparent", 0, 180);
                transparent.style.opacity = 0f;
                var folded = new VisualElement { name = "folded" };
                folded.style.display = DisplayStyle.None;
                root.Add(folded);
                Add(folded, "in-folded-parent", "In a folded parent", 0, 210);
                Add(root, "outside", "Outside the safe area", 500, 0);
                var button = new Button { name = "a-button", text = "Open" };
                button.style.position = Position.Absolute;
                button.style.left = 100;
                button.style.top = 0;
                button.style.width = 80;
                button.style.height = 20;
                root.Add(button);

                yield return ShellRig.Settle();
                yield return ShellRig.Settle();

                var safe = new Rect(0f, 0f, 300f, 300f);
                var texts = ShellGeometryProbe.CollectTexts(new VisualElement[] { root }, safe);

                CollectionAssert.AreEquivalent(new[] { "Cash", "Store 4/16", "Open" }, texts.Select(item => item.Text).ToArray(), "visible text only");
                Assert.That(texts.Count(item => item.IsLabel), Is.EqualTo(2), "two labels");
                Assert.That(texts.Count(item => !item.IsLabel), Is.EqualTo(1), "one button, counted apart from the labels");
                Assert.That(texts.All(item => item.Owner == "synthetic-root"));

                // The same tree under a safe area that excludes the lower rows: the safe area limits what counts.
                var upper = new Rect(0f, 0f, 300f, 20f);
                Assert.That(ShellGeometryProbe.CollectTexts(new VisualElement[] { root }, upper).Select(item => item.Text), Is.EquivalentTo(new[] { "Cash", "Open" }));

                // An element listed under two roots is counted once, under the first.
                var twice = ShellGeometryProbe.CollectTexts(new VisualElement[] { root, root.Q<VisualElement>("counted-1") }, safe);
                Assert.That(twice.Count(item => item.Text == "Cash"), Is.EqualTo(1));
            }
        }

        // ---------------------------------------------------------------------------------------------------
        // Live shell
        // ---------------------------------------------------------------------------------------------------

        private static ShellScreen Phone() => ShellScreens.All().First(screen => screen.Name.StartsWith("phone-portrait 390", System.StringComparison.Ordinal));

        [UnityTest]
        public IEnumerator ALiveShellIsReadFromItsGeometry([ValueSource(nameof(AllScreens))] ShellScreen screen)
        {
            var rig = ShellRig.Create(screen, null, 100);
            rigs.Add(rig);
            rig.Shell.Panels.Register(new TestShellPanel(ShellPanelIds.Market, "Market", 12));
            yield return rig.Layout();

            var closed = ShellGeometryProbe.Measure(rig, "no panel");
            Assert.That(closed.World.HasValue, Is.True, screen + ": the shell reports a world rect");
            Assert.That(closed.WorldShare, Is.InRange(0.05d, 1d), screen.Name);
            Assert.That(closed.ChromeShare, Is.InRange(0.01d, 0.95d), screen.Name);
            Assert.That(closed.WorldShare + closed.ChromeShare, Is.LessThanOrEqualTo(1.0001d), screen + ": the world rect and the chrome do not overlap beyond the safe area");
            Assert.That(closed.Chrome.Select(item => item.Name), Does.Contain("top-strip").And.Contain("tab-bar"), screen.Name);
            Assert.That(closed.LabelCount, Is.GreaterThan(0), screen + ": the strip and tab bar carry labels");
            Assert.That(closed.Texts.Select(item => item.Owner), Does.Contain("top-strip").And.Contain("tab-bar"), screen.Name);
            Assert.That(closed.Overlay.HasValue, Is.False);

            // The probe does not change the shell: a second reading is identical.
            Assert.That(ShellGeometryProbe.ToJson(ShellGeometryProbe.Measure(rig, "no panel"), string.Empty), Is.EqualTo(ShellGeometryProbe.ToJson(closed, string.Empty)));

            rig.Shell.SelectTab(ShellTab.Market);
            yield return ShellRig.Settle();
            var open = ShellGeometryProbe.Measure(rig, "market");
            Assert.That(open.Sheet.HasValue, Is.True, screen + ": the sheet or side panel is displayed");
            Assert.That(open.SheetShare, Is.GreaterThan(0.05d), screen.Name);
            Assert.That(open.World.HasValue, Is.True);
            Assert.That(ShellGeometryProbe.Area(open.World.Value), Is.LessThanOrEqualTo(ShellGeometryProbe.Area(closed.World.Value) + 1d), screen + ": an open panel never enlarges the world rect");
            if (!rig.Shell.IsSidePanelLayout)
            {
                Assert.That(ShellGeometryProbe.Area(open.World.Value), Is.LessThan(ShellGeometryProbe.Area(closed.World.Value)), screen + ": a phone sheet takes world area");
            }
        }

        public static IEnumerable<ShellScreen> AllScreens() => ShellScreens.All();

        [UnityTest]
        public IEnumerator TheWorldRectIsConvertedFromScreenPixelsToTopLeftPoints()
        {
            var screen = Phone();
            var rig = ShellRig.Create(screen, null, 100);
            rigs.Add(rig);
            yield return rig.Layout();

            Assert.That(rig.Shell.TryGetUnobstructedScreenRect(screen.Height, out var pixels), Is.True);
            var points = ShellGeometryProbe.WorldPoints(pixels, screen.Height, rig.PixelsPerPoint);
            var column = rig.Shell.WorldColumn.worldBound;
            Assert.That(points.xMax, Is.LessThanOrEqualTo(column.xMax + 0.5f));
            Assert.That(points.yMax, Is.EqualTo(column.yMax).Within(0.5f), "the world rect ends where the world column ends");
            Assert.That(points.yMin, Is.GreaterThanOrEqualTo(rig.Shell.TopStrip.worldBound.yMax - 0.5f), "and starts below the top strip");
        }
    }
}
#endif
