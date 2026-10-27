#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using IdleFactory.DevTools;
using IdleFactory.Tests.PlayMode;
using IdleFactory.Unity.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEngine.TestTools;
using static IdleFactory.Tests.PlayMode.RunShellLayoutAssertions;
using Object = UnityEngine.Object;

namespace IdleFactory.Tests.DevTools
{
    /// <summary>
    /// T340 (owner decision of 2026-10-21, F6 finding on #408; QA-12 row "DevTools overlay"): the time-scale row used to sit at the bottom centre and
    /// covered <c>factory-ore-vein</c> (Mine) in the 16:9 windows. The overlay now sits at the top left directly below the top strip of the shell
    /// and inside the safe area. These tests build the real shell at every <see cref="ShellScreens"/> class (and rotated), at 100 and 200 percent
    /// text, with the ore vein bound and an accelerated factor selected (marker visible), render the overlay into the same pixel space (a panel of
    /// the same texture at one pixel per point) and assert that neither the row nor the marker overlaps the ore vein or any other shell control, and
    /// that both stay inside the safe area. Define-gated like the rest of this assembly.
    /// </summary>
    public sealed class DevToolsOverlayPlacementTests
    {
        private readonly List<ShellRig> rigs = new List<ShellRig>();
        private GameObject host;

        [TearDown]
        public void TearDown()
        {
            if (host != null)
            {
                Object.DestroyImmediate(host);
            }

            foreach (var item in rigs)
            {
                item.Dispose();
            }

            rigs.Clear();
            host = null;
        }

        private static IEnumerable<ShellScreen> Screens() =>
            ShellScreens.All().SelectMany(screen => new[] { screen, ShellScreens.Rotated(screen) });

        private static IEnumerable<int> Percents() => new[] { 100, 200 };

        [UnityTest]
        public IEnumerator TheRowAndTheMarkerNeverOverlapTheOreVeinOrAnyShellControlAndStayInTheSafeArea(
            [ValueSource(nameof(Screens))] ShellScreen screen, [ValueSource(nameof(Percents))] int percent)
        {
            var rig = ShellRig.Create(screen, null, percent);
            rigs.Add(rig);
            yield return rig.Layout();
            var session = RunShellFixtures.NewSession(out var commands, 1, 25);
            rig.Shell.BindOreVein(new RunOreVeinSource(session, commands));
            yield return ShellRig.Settle();

            var timeScale = new DevTimeScale();
            host = new GameObject("DevTools Overlay Placement Test");
            var overlay = DevToolsOverlay.Create(timeScale, host);
            overlay.AutoPlacement = false;
            // The overlay renders into the texture of the shell at one pixel per point, so both panels share one coordinate space.
            overlay.Settings.targetTexture = rig.Texture;
            overlay.Settings.scaleMode = PanelScaleMode.ConstantPixelSize;
            overlay.Settings.scale = 1f;
            timeScale.Select(2);
            yield return ShellRig.Settle();
            overlay.RefreshPlacement(screen.SafeAreaPx, screen.Width, screen.Height);
            yield return ShellRig.Settle();

            var context = $"{screen} @{percent}%";
            var vein = rig.Shell.OreVein.Root;
            Assert.That(IsDisplayed(vein), Is.True, context + ": the ore vein is shown, so the test means something");
            Assert.That(vein.worldBound.width, Is.GreaterThan(0f), context);

            var controls = new List<(string Name, VisualElement Element)>
            {
                ("ore vein", vein),
                ("top strip", rig.Shell.TopStrip),
                ("zoom in", rig.Root.Q<VisualElement>("zoom-in")),
                ("zoom out", rig.Root.Q<VisualElement>("zoom-out")),
            };
            if (!rig.Shell.IsSidePanelLayout)
            {
                controls.Add(("tab bar", rig.Shell.TabBar));
            }

            if (IsDisplayed(rig.Shell.PanelHost))
            {
                controls.Add(("panel host", rig.Shell.PanelHost));
            }

            var overlayParts = new[] { ("time-scale row", overlay.Row.worldBound), ("accelerated marker", overlay.Marker.worldBound) };
            var safe = rig.SafeRect();
            foreach (var part in overlayParts)
            {
                Assert.That(part.Item2.width, Is.GreaterThan(0f), context + ": " + part.Item1 + " has a layout");
                AssertWithin(safe, part.Item2, context + ": " + part.Item1 + " stays inside the safe area");
                foreach (var control in controls)
                {
                    Assert.That(control.Element, Is.Not.Null, context + ": " + control.Name);
                    Assert.That(Intersects(part.Item2, control.Element.worldBound), Is.False,
                        $"{context}: the {part.Item1} {part.Item2} overlaps the {control.Name} {control.Element.worldBound}");
                }
            }

            var buttons = Enumerable.Range(0, DevTimeScale.MaxIndex + 1).Select(overlay.ButtonAt).ToList();
            foreach (var button in buttons)
            {
                foreach (var control in controls)
                {
                    Assert.That(Intersects(button.worldBound, control.Element.worldBound), Is.False,
                        $"{context}: button {button.text} overlaps the {control.Name}");
                }
            }
        }

        [UnityTest]
        public IEnumerator TheOverlayStaysBelowAShownNoticeAndLeavesAGapBetweenTheMarkerAndTheRow(
            [ValueSource(nameof(Screens))] ShellScreen screen)
        {
            var rig = ShellRig.Create(screen, null, 100);
            rigs.Add(rig);
            yield return rig.Layout();
            var timeScale = new DevTimeScale();
            host = new GameObject("DevTools Overlay Notice Test");
            var overlay = DevToolsOverlay.Create(timeScale, host);
            overlay.AutoPlacement = false;
            overlay.Settings.targetTexture = rig.Texture;
            overlay.Settings.scaleMode = PanelScaleMode.ConstantPixelSize;
            overlay.Settings.scale = 1f;
            timeScale.Select(2);
            rig.Shell.ShowNotice("Session log exported.", ShellNoticeKind.Info, true);
            yield return ShellRig.Settle();
            overlay.RefreshPlacement(screen.SafeAreaPx, screen.Width, screen.Height);
            yield return ShellRig.Settle();

            var context = screen.ToString();
            var notice = rig.Shell.NoticeBar;
            Assert.That(IsDisplayed(notice), Is.True, context + ": the notice is shown");
            Assert.That(notice.worldBound.width, Is.GreaterThan(0f), context);
            Assert.That(Intersects(overlay.Marker.worldBound, notice.worldBound), Is.False, context + ": the marker does not cover the notice");
            Assert.That(Intersects(overlay.Row.worldBound, notice.worldBound), Is.False, context + ": the row does not cover the notice");

            var gap = overlay.Row.worldBound.yMin - overlay.Marker.worldBound.yMax;
            Assert.That(gap, Is.InRange(DevToolsOverlay.MarkerGap - 0.5f, 4.5f), context + ": 2 to 4 pt between the marker and the row");
        }
    }
}
#endif
