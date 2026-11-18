using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using IdleFactory.Run.Application;
using IdleFactory.Unity.Presentation;
using IdleFactory.Unity.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

#if UNITY_EDITOR
namespace IdleFactory.Tests.PlayMode
{
    /// <summary>
    /// T558 (#771, batch-C gate fixes; FR-007, FR-032, FR-035, SC-001; OD-9b C1, C6). Three defects of the batch-C gate, each asserted per screen class on the real shell:
    /// U-C1 the closed-state camera frames the depot column (the depot, the collector, the store-full and shortage marker boxes); C-F2 a rotation under an open card re-fits
    /// the world when the card closes with Back; C-F1 the milestone banner shows only its main line on compact height or large text so it does not cover the top machine row.
    /// </summary>
    public sealed class GateCFixTests
    {
        private const string Main = "Level 3. 2 new locations open.";
        private const string Extra = "Deck extended.";

        private readonly List<CardHarness> harnesses = new List<CardHarness>();
        private readonly List<ShellRig> rigs = new List<ShellRig>();
        private readonly List<GameObject> objects = new List<GameObject>();
        private CardHarness h;

        public static IEnumerable<ShellScreen> Screens() => ShellScreens.All();

        public static IEnumerable<int> Percents() => new[] { 100, 200 };

        [TearDown]
        public void TearDown()
        {
            foreach (var item in harnesses)
            {
                item.Dispose();
            }

            foreach (var item in rigs)
            {
                item.Dispose();
            }

            foreach (var item in objects)
            {
                if (item != null)
                {
                    UnityEngine.Object.DestroyImmediate(item);
                }
            }

            harnesses.Clear();
            rigs.Clear();
            objects.Clear();
            h = null;
        }

        private IEnumerator Make(ShellScreen screen, int percent, int factoryLevel)
        {
            CardHarness made = null;
            yield return CardHarness.Create(screen, percent, RunShellFixtures.ComfortableCash * 100_000L, built => made = built, unlockEverything: true, factoryLevel: factoryLevel);
            harnesses.Add(made);
            h = made;
        }

        private Camera CreateCamera(RenderTexture target, out FactoryCameraController controller)
        {
            var host = new GameObject("gate-c-camera");
            objects.Add(host);
            host.transform.position = new Vector3(0f, 0f, -10f);
            var camera = host.AddComponent<Camera>();
            camera.orthographic = true;
            camera.targetTexture = target;
            camera.enabled = false;
            controller = host.AddComponent<FactoryCameraController>();
            controller.Configure(camera, new RunZoomSettings().ToScaleSettings(), _ => { });
            return camera;
        }

        private static FactoryWorldLayout LayoutFor(ShellScreen screen) =>
            LatticeLayoutBuilder.ShippedForColumns(SlotGridLayout.ColumnsFor(screen.Width, screen.Height));

        private static Rect OnScreen(CameraScaleModel model, Rect world)
        {
            var low = model.WorldToScreen(world.min, model.Position, model.Scale);
            var high = model.WorldToScreen(world.max, model.Position, model.Scale);
            return Rect.MinMaxRect(Mathf.Min(low.x, high.x), Mathf.Min(low.y, high.y), Mathf.Max(low.x, high.x), Mathf.Max(low.y, high.y));
        }

        private static bool Inside(Rect area, Rect inner, float tolerance = 1.5f) =>
            inner.xMin >= area.xMin - tolerance && inner.yMin >= area.yMin - tolerance && inner.xMax <= area.xMax + tolerance && inner.yMax <= area.yMax + tolerance;

        /// <summary>The depot column the closed-state fit frames: the depot sprite, the store-full marker box, the collector spine and the shortage marker box of each row.</summary>
        private static IEnumerable<(string Name, Rect World)> DepotColumn(FactoryWorldLayout layout)
        {
            yield return ("depot", layout.DepotRect);
            yield return ("store-full marker", AttentionModel.MarkerBoxAt(layout.DepotAnchor));
            for (var row = 0; row < layout.RowCount; row++)
            {
                var trunk = layout.TrunkOf(row);
                if (trunk.IsEmpty)
                {
                    continue;
                }

                var junction = trunk.Points[1];
                yield return ("shortage marker row " + row, AttentionModel.MarkerBoxAt(junction));
                yield return ("collector row " + row, new Rect(junction.x, Mathf.Min(junction.y, layout.DepotAnchor.y), 0f, Mathf.Abs(junction.y - layout.DepotAnchor.y)));
            }
        }

        // ---------------------------------------------------------------------------------------------------
        // U-C1: the closed-state camera frames the depot column
        // ---------------------------------------------------------------------------------------------------

        [UnityTest]
        public IEnumerator TheClosedStateCameraFramesTheDepotTheCollectorAndTheMarkerBoxesOnEveryClass(
            [ValueSource(nameof(Screens))] ShellScreen screen, [ValueSource(nameof(Percents))] int percent, [Values(2, 3)] int factoryLevel)
        {
            yield return Make(screen, percent, factoryLevel);
            var camera = CreateCamera(h.Rig.Texture, out var controller);
            var layout = LayoutFor(screen);
            Assert.That(h.Shell.TryGetUnobstructedScreenRect(screen.Height, out var area), Is.True);
            controller.Fit(RunCameraFraming.Layout(camera, layout, h.Rig.PixelsPerPoint, area));
            var model = controller.Model;

            var floor = CameraScaleModel.MinScale(model.Settings, h.Rig.PixelsPerPoint);
            var contentScale = CameraScaleModel.LargestFittingScale(area, layout.Bounds, 24, layout.Margin);
            var columnScale = CameraScaleModel.LargestFittingScale(area, layout.FitBounds, 24, layout.Margin);
            var framed = model.Extent == layout.FitBounds || model.Extent == layout.ViewBounds;
            TestContext.Out.WriteLine(
                $"T558-FRAMING | {screen.Name} | text {percent} % | level {factoryLevel} | world scale n={controller.Scale} | content n={contentScale} depot column n={columnScale} floor {floor} | depot column framed {framed}");

            // OD-9b C1 still holds: the whole factory is in view at fit-all.
            Assert.That(Inside(area, OnScreen(model, layout.Bounds)), Is.True, $"{screen} @{percent}%: the factory content is fully in view");

            // U-C1: whenever the depot column fits at or above the readable floor, it is framed, with the collector and every marker box inside the camera rectangle.
            if (columnScale >= Math.Max(1, Math.Min(contentScale, floor)))
            {
                Assert.That(framed, Is.True, $"{screen} @{percent}%: the fit extent includes the depot column");
                Assert.That(controller.Scale, Is.LessThanOrEqualTo(contentScale), "at the content's scale or the next integer scale down");
                foreach (var part in DepotColumn(layout))
                {
                    Assert.That(Inside(area, OnScreen(model, part.World)), Is.True, $"{screen} @{percent}%: the {part.Name} is inside the camera rectangle {OnScreen(model, part.World)} vs {area}");
                }
            }
        }

        [UnityTest]
        public IEnumerator EveryReferenceDeviceFramesTheDepotColumnWheneverItFitsAtOrAboveTheReadableFloor(
            [ValueSource(typeof(ScreenClassScalePlayTests), nameof(ScreenClassScalePlayTests.Devices))] ScreenClassScalePlayTests.ScaleDevice device)
        {
            // The real panel scale of each reference device (authored panel settings and device profile), with the worst-case overlays the camera rectangle excludes
            // (the ore-vein overlay and the empty-grid hint bar) and a run at factory level 2.
            var profile = ShellDeviceProfile.Create(device.Touch ? ShellPrimaryInput.Touch : ShellPrimaryInput.Pointer, device.Width, device.Height, device.Dpi);
            var screen = new ShellScreen(device.Name, device.Width, device.Height, 0, 0, 0, 0, device.Layout);
            var authored = ShellRig.CreateAuthored(screen, profile);
            rigs.Add(authored);
            yield return authored.Layout();
            var session = RunShellFixtures.NewSession(out var commands, 2, 25);
            authored.Shell.BindOreVein(new RunOreVeinSource(session, commands));
            authored.Shell.SetEmptyHint(true);
            yield return ShellRig.Settle();

            var camera = CreateCamera(authored.Texture, out var controller);
            var ppp = authored.PixelsPerPoint;
            var layout = LatticeLayoutBuilder.ShippedForColumns(SlotGridLayout.ColumnsFor(device.Width / ppp, device.Height / ppp));
            Assert.That(authored.Shell.TryGetUnobstructedScreenRect(device.Height, out var area), Is.True);
            controller.Fit(RunCameraFraming.Layout(camera, layout, ppp, area));
            var model = controller.Model;
            var floor = CameraScaleModel.MinScale(model.Settings, ppp);
            var contentScale = CameraScaleModel.LargestFittingScale(area, layout.Bounds, 24, layout.Margin);
            var columnScale = CameraScaleModel.LargestFittingScale(area, layout.FitBounds, 24, layout.Margin);
            var framed = model.Extent == layout.FitBounds || model.Extent == layout.ViewBounds;
            var inside = true;
            foreach (var part in DepotColumn(layout))
            {
                inside &= Inside(area, OnScreen(model, part.World));
            }

            TestContext.Out.WriteLine(
                $"T558-DEVICE | {device.Name} | {ppp.ToString("0.####", CultureInfo.InvariantCulture)} px/pt | rect {area.width:0}x{area.height:0} px | world scale n={controller.Scale} | content n={contentScale} depot column n={columnScale} floor {floor} | depot column inside {inside}");

            Assert.That(Inside(area, OnScreen(model, layout.Bounds)), Is.True, $"{device}: the factory content is fully in view (OD-9b C1)");
            Assert.That(controller.Model.BelowFloor, Is.False, $"{device}: no runtime degrade");
            Assert.That(controller.Scale, Is.GreaterThanOrEqualTo(floor), $"{device}: the readable floor holds");
            if (columnScale >= Math.Max(1, Math.Min(contentScale, floor)))
            {
                Assert.That(framed, Is.True, $"{device}: the fit extent includes the depot column");
                Assert.That(inside, Is.True, $"{device}: the depot, the collector and every marker box are inside the camera rectangle");
            }
        }

        // ---------------------------------------------------------------------------------------------------
        // C-F2: a rotation under an open card, then Back
        // ---------------------------------------------------------------------------------------------------

        private static void ApplyFrame(CardHarness harness, RunViewportDriver driver, ShellScreen screen, bool wantSheet, int slot)
        {
            Assert.That(harness.Shell.TryGetUnobstructedScreenRect(screen.Height, out var area), Is.True, screen.Name);
            var layout = LayoutFor(screen);
            var scaleLayout = ScaleLayout.WithView(screen.Width, screen.Height, harness.Rig.PixelsPerPoint, area, layout);
            driver.Apply(scaleLayout, area, wantSheet, wantSheet ? layout.MachineRectOf(new BuildingLocationId(slot)) : default);
        }

        /// <summary>
        /// The device turns with a card open. The rig's panel keeps its size, so the screen after the rotation is a second rig of the rotated class; the viewport driver is one
        /// and keeps its state across both, as the bootstrap's does across a configuration change.
        /// </summary>
        private IEnumerator RotateWithACardOpenThenBack(ShellScreen screen, int percent, bool machineCard)
        {
            yield return Make(screen, percent, 2);
            var before = h;
            CreateCamera(before.Rig.Texture, out var controller);
            var driver = new RunViewportDriver(controller) { FramingPending = true };
            ApplyFrame(before, driver, screen, false, 0);

            if (machineCard)
            {
                before.Build(0, "machine.miner");
            }

            Assert.That(before.Shell.OpenSlot(0, machineCard ? SlotCondition.Occupied : SlotCondition.Empty), Is.True);
            yield return ShellRig.Settle();
            driver.NotifySelectionChanged();
            ApplyFrame(before, driver, screen, true, 0);
            Assert.That(controller.SheetOpen, Is.True, $"{screen} @{percent}%: the card or sheet is open");

            // The rotation: the bootstrap asks for a fit of the whole world (ConfigureRunCamera(true)); the shell's rectangle is that of the new screen, card still open.
            var rotated = ShellScreens.Rotated(screen);
            yield return Make(rotated, percent, 2);
            var after = h;
            if (machineCard)
            {
                after.Build(0, "machine.miner");
            }

            Assert.That(after.Shell.OpenSlot(0, machineCard ? SlotCondition.Occupied : SlotCondition.Empty), Is.True);
            yield return ShellRig.Settle();
            driver.FramingPending = true;
            ApplyFrame(after, driver, rotated, true, 0);
            yield return ShellRig.Settle();
            ApplyFrame(after, driver, rotated, true, 0);

            // Back closes the card; the selection ends.
            if (machineCard)
            {
                after.Shell.CloseMachineCard();
            }
            else
            {
                after.Shell.CloseSheetOrDetails();
            }

            yield return ShellRig.Settle();
            driver.NotifySelectionChanged();
            ApplyFrame(after, driver, rotated, false, 0);
            yield return ShellRig.Settle();
            ApplyFrame(after, driver, rotated, false, 0);

            Assert.That(after.Shell.TryGetUnobstructedScreenRect(rotated.Height, out var area), Is.True);
            var layout = LayoutFor(rotated);
            var model = controller.Model;
            var where = $"{screen} @{percent}% rotated to {rotated.Width}x{rotated.Height}";
            Assert.That(controller.SheetOpen, Is.False, where + ": the card is closed");
            Assert.That(Inside(area, OnScreen(model, layout.Bounds)), Is.True,
                where + $": after Back the factory is in view: {OnScreen(model, layout.Bounds)} vs {area}");

            var fresh = new CameraScaleModel(new RunZoomSettings().ToScaleSettings());
            fresh.Fit(ScaleLayout.WithView(rotated.Width, rotated.Height, after.Rig.PixelsPerPoint, area, layout));
            Assert.That(controller.Scale, Is.EqualTo(fresh.Scale), where + ": the scale of a fresh fit");
            Assert.That(model.Position.x, Is.EqualTo(fresh.Position.x).Within(1e-3f), where + ": centred as a fresh fit");
            Assert.That(model.Position.y, Is.EqualTo(fresh.Position.y).Within(1e-3f), where);
        }

        [UnityTest]
        public IEnumerator RotatingWithTheBuildSurfaceOpenAndClosingItWithBackRefitsTheWorld(
            [ValueSource(nameof(Screens))] ShellScreen screen, [ValueSource(nameof(Percents))] int percent) =>
            RotateWithACardOpenThenBack(screen, percent, false);

        [UnityTest]
        public IEnumerator RotatingWithTheMachineCardOpenAndClosingItWithBackRefitsTheWorld(
            [ValueSource(nameof(Screens))] ShellScreen screen, [ValueSource(nameof(Percents))] int percent) =>
            RotateWithACardOpenThenBack(screen, percent, true);

        // ---------------------------------------------------------------------------------------------------
        // C-F1 / U-C4: the compact milestone banner
        // ---------------------------------------------------------------------------------------------------

        [UnityTest]
        public IEnumerator TheBannerShowsOnlyItsMainLineOnCompactHeightOrLargeTextAndKeepsTheFullTextAccessible(
            [ValueSource(nameof(Screens))] ShellScreen screen, [ValueSource(nameof(Percents))] int percent)
        {
            yield return Make(screen, percent, 2);
            var shell = h.Shell;
            var camera = CreateCamera(h.Rig.Texture, out var controller);
            var layout = LayoutFor(screen);
            Assert.That(shell.TryGetUnobstructedScreenRect(screen.Height, out var area), Is.True);
            controller.Fit(RunCameraFraming.Layout(camera, layout, h.Rig.PixelsPerPoint, area));

            shell.ShowMilestoneBanner(Main, Extra, false);
            shell.TickMilestoneBanner(0.3f);
            yield return ShellRig.Settle();
            shell.TickMilestoneBanner(0.01f);
            var banner = shell.MilestoneBanner;
            var box = banner.Root.worldBound;
            var panel = shell.Root.panel.visualTree.worldBound;
            var primaryOnly = screen.Expected == FactoryHudLayout.CompactLandscape || percent >= 150;
            var where = $"{screen} @{percent}%";

            Assert.That(shell.BannerPrimaryOnly, Is.EqualTo(primaryOnly), where + ": compact height or large text");
            Assert.That(banner.SecondaryLabel.resolvedStyle.display, Is.EqualTo(primaryOnly ? DisplayStyle.None : DisplayStyle.Flex), where + ": the second line");
            Assert.That(banner.PrimaryLabel.text, Is.EqualTo(Main), where);
            Assert.That(banner.AccessibleText, Is.EqualTo(Main + " " + Extra), where + ": the accessible text keeps the second line (B-04 and the others)");
            Assert.That(banner.Root.tooltip, Is.EqualTo(Main + " " + Extra), where);
            Assert.That(box.height, Is.GreaterThanOrEqualTo(43.5f), where + ": still a dismiss target");

            // Where the banner is short (compact height or large text) it must leave the machines of the top row alone: it ends above their visual extent (plate, body and
            // the badge band above), so the row is not hidden.
            var model = controller.Model;
            var columns = SlotGridLayout.ColumnsFor(screen.Width, screen.Height);
            var panelHeight = panel.height;
            var topRowPlateTop = float.MaxValue;
            var topRowMachineTop = float.MaxValue;
            var topRowMachineBottom = float.MaxValue;
            for (var slot = 0; slot < columns; slot++)
            {
                var plate = OnScreen(model, layout.FootprintOf(new BuildingLocationId(slot)));
                var machine = OnScreen(model, layout.MachineRectOf(new BuildingLocationId(slot)));

                // Screen pixels (bottom-left origin) to panel points (top-left origin); the rig keeps 1 pixel per point.
                topRowPlateTop = Mathf.Min(topRowPlateTop, panelHeight - plate.yMax);
                topRowMachineTop = Mathf.Min(topRowMachineTop, panelHeight - machine.yMax);
                topRowMachineBottom = Mathf.Min(topRowMachineBottom, panelHeight - machine.yMin);
            }

            var share = box.height / panel.height;
            TestContext.Out.WriteLine(
                $"T558-BANNER | {screen.Name} | text {percent} % | primary only {primaryOnly} | banner {box.height.ToString("0.0", CultureInfo.InvariantCulture)} pt of {panelHeight.ToString("0", CultureInfo.InvariantCulture)} pt = {(share * 100f).ToString("0.0", CultureInfo.InvariantCulture)} % | banner {box.yMin.ToString("0", CultureInfo.InvariantCulture)} to {box.yMax.ToString("0", CultureInfo.InvariantCulture)} | top row machines {topRowMachineTop.ToString("0", CultureInfo.InvariantCulture)} to {topRowMachineBottom.ToString("0", CultureInfo.InvariantCulture)}, plates start {topRowPlateTop.ToString("0", CultureInfo.InvariantCulture)}");
            if (primaryOnly)
            {
                Assert.That(box.yMax, Is.LessThanOrEqualTo(topRowMachineTop + 0.5f), where + ": the banner does not reach the top row's machines (plates, bodies or badge band)");
                Assert.That(share, Is.LessThanOrEqualTo(0.21f), where + ": the banner takes at most about a fifth of the screen height");
            }
        }
    }
}
#endif
