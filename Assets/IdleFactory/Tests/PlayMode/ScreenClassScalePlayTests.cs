#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using IdleFactory.Run.Application;
using IdleFactory.Unity.Presentation;
using IdleFactory.Unity.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace IdleFactory.Tests.PlayMode
{
    /// <summary>
    /// The Play Mode variant of <c>ScreenClassScaleTableTests</c> (contracts/camera-scale.md, Tests; owner decision S2 = D24, condition 1):
    /// the same rule, <c>n_fit &gt;= n_min</c> at D = 24, but driven from the real <c>TryGetUnobstructedScreenRect</c> of a shell built
    /// with the authored panel settings and the device profile of each reference device, with the worst-case overlays the
    /// rectangle excludes: the ore-vein overlay, the empty-grid hint bar and an open sheet. A negative case with an oversized HUD must
    /// fail, and the 1280x720 phone at 320 dpi (2.0046 px/pt, n_min 3 without the 1 % tolerance) is proven on its own.
    /// </summary>
    public sealed class ScreenClassScalePlayTests
    {
        public readonly struct ScaleDevice
        {
            public ScaleDevice(string name, int width, int height, float dpi, bool touch, FactoryHudLayout layout)
            {
                Name = name;
                Width = width;
                Height = height;
                Dpi = dpi;
                Touch = touch;
                Layout = layout;
            }

            public string Name { get; }
            public int Width { get; }
            public int Height { get; }
            public float Dpi { get; }
            public bool Touch { get; }
            public FactoryHudLayout Layout { get; }

            public override string ToString() => Name;
        }

        private readonly List<ShellRig> rigs = new List<ShellRig>();
        private readonly List<GameObject> objects = new List<GameObject>();
        private ShellRig rig;
        private RunSession session;
        private RunCommands commands;

        public static IEnumerable<ScaleDevice> Devices()
        {
            yield return new ScaleDevice("phone-portrait 720x1280 xhdpi", 720, 1280, 320f, true, FactoryHudLayout.CompactPortrait);
            yield return new ScaleDevice("phone-landscape 1280x720 xhdpi", 1280, 720, 320f, true, FactoryHudLayout.CompactLandscape);
            yield return new ScaleDevice("phone-portrait 1080x2400 420dpi", 1080, 2400, 420f, true, FactoryHudLayout.CompactPortrait);
            yield return new ScaleDevice("phone-landscape 2400x1080 420dpi", 2400, 1080, 420f, true, FactoryHudLayout.CompactLandscape);
            yield return new ScaleDevice("tablet-portrait 1536x2048 264dpi", 1536, 2048, 264f, true, FactoryHudLayout.TabletPortrait);
            yield return new ScaleDevice("tablet-landscape 2048x1536 264dpi", 2048, 1536, 264f, true, FactoryHudLayout.TabletLandscape);
            yield return new ScaleDevice("desktop 1280x720", 1280, 720, 0f, false, FactoryHudLayout.Desktop);
            yield return new ScaleDevice("desktop 1920x1080", 1920, 1080, 0f, false, FactoryHudLayout.Desktop);
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var item in rigs)
            {
                item.Dispose();
            }

            foreach (var item in objects)
            {
                if (item != null)
                {
                    Object.DestroyImmediate(item);
                }
            }

            rigs.Clear();
            objects.Clear();
            rig = null;
        }

        /// <summary>
        /// Landscape phones whose real HUD leaves less than n_min for the grid once the ore-vein overlay and the empty-grid hint bar
        /// are both shown (finding of T427, reported to the owner): the rule holds for the closed shell, not for the worst case.
        /// </summary>
        private static readonly string[] OverlayGaps = { "phone-landscape 1280x720 xhdpi", "phone-landscape 2400x1080 420dpi" };

        private IEnumerator OpenWorstCase(ScaleDevice device, bool overlays = true)
        {
            var profile = ShellDeviceProfile.Create(device.Touch ? ShellPrimaryInput.Touch : ShellPrimaryInput.Pointer, device.Width, device.Height, device.Dpi);
            var screen = new ShellScreen(device.Name, device.Width, device.Height, 0, 0, 0, 0, device.Layout);
            rig = ShellRig.CreateAuthored(screen, profile);
            rigs.Add(rig);
            yield return rig.Layout();

            // The worst-case overlays the unobstructed rectangle excludes: the ore-vein overlay and the empty-grid hint bar (a phone).
            if (overlays)
            {
                session = RunShellFixtures.NewSession(out commands, 1, 25);
                rig.Shell.BindOreVein(new RunOreVeinSource(session, commands));
                rig.Shell.SetEmptyHint(true);
            }

            rig.Shell.Panels.Register(new TestShellPanel(ShellPanelIds.Build, "Build", 8));
            yield return ShellRig.Settle();
        }

        private Camera CreateCamera(out FactoryCameraController controller, List<string> log)
        {
            var host = new GameObject("scale-play-camera");
            objects.Add(host);
            host.transform.position = new Vector3(0f, 0f, -10f);
            var camera = host.AddComponent<Camera>();
            camera.orthographic = true;
            camera.targetTexture = rig.Texture;
            camera.enabled = false;
            controller = host.AddComponent<FactoryCameraController>();
            controller.Configure(camera, new RunZoomSettings().ToScaleSettings(), message => log?.Add(message));
            return camera;
        }

        private static (Vector2 Low, Vector2 High) ScreenRectOf(Camera camera, Rect world)
        {
            var low = camera.WorldToScreenPoint(new Vector3(world.xMin, world.yMin, 0f));
            var high = camera.WorldToScreenPoint(new Vector3(world.xMax, world.yMax, 0f));
            return (new Vector2(Mathf.Min(low.x, high.x), Mathf.Min(low.y, high.y)), new Vector2(Mathf.Max(low.x, high.x), Mathf.Max(low.y, high.y)));
        }

        [UnityTest]
        public IEnumerator EveryReferenceDeviceKeepsNFitAtOrAboveNMinWithTheRealHudAndAKeptSlotAboveAnOpenSheet([ValueSource(nameof(Devices))] ScaleDevice device, [Values(false, true)] bool overlays)
        {
            yield return OpenWorstCase(device, overlays);
            var shell = rig.Shell;
            var pixelsPerPoint = rig.PixelsPerPoint;
            var framing = new RunCameraFraming(new RunZoomSettings());
            var columns = SlotGridLayout.ColumnsFor(device.Width / pixelsPerPoint, device.Height / pixelsPerPoint);
            var camera = CreateCamera(out var controller, null);

            if (overlays)
            {
                Assert.That(shell.OreVein.Root.resolvedStyle.display, Is.Not.EqualTo(DisplayStyle.None), $"{device}: the ore-vein overlay is part of the rectangle");
            }

            Assert.That(shell.TryGetUnobstructedScreenRect(device.Height, out var closed), Is.True);
            var range = framing.ConfigureLimits(pixelsPerPoint);
            var nFit = framing.FitAll(closed, columns, pixelsPerPoint);
            var largest = CameraScaleModel.LargestFittingScale(closed, columns, 24);
            TestContext.Out.WriteLine($"{device}: {pixelsPerPoint:0.####} px/pt, rect {closed.width:0}x{closed.height:0} px, n_min {range.Min}, n_fit {nFit}, largest fitting {largest}, levels {range.LevelCount}");
            var gap = overlays && System.Array.IndexOf(OverlayGaps, device.Name) >= 0;
            if (gap)
            {
                // Pinned finding: the rule fails for this device with both overlays. A HUD change that fixes it flips this assertion.
                Assert.That(largest, Is.LessThan(range.Min), $"{device}: the known worst-case overlay gap (ore vein + hint bar) is still there");
                yield break;
            }

            Assert.That(largest, Is.GreaterThanOrEqualTo(range.Min), $"{device}: n_fit >= n_min with the real HUD (overlays {overlays})");
            controller.Fit(RunCameraFraming.Layout(camera, columns, pixelsPerPoint, closed));
            Assert.That(controller.Model.BelowFloor, Is.False, $"{device}: no runtime degrade");
            Assert.That(controller.Scale, Is.EqualTo(nFit));

            // The open sheet: every slot stays whole in the band at a scale of at least n_min, or its footprint is aligned to the band top.
            for (var slot = 0; slot < SlotGridLayout.SlotCount; slot++)
            {
                Assert.That(shell.OpenSlot(slot, SlotCondition.Empty), Is.True);
                yield return ShellRig.Settle();
                Assert.That(shell.TryGetUnobstructedScreenRect(device.Height, out var band), Is.True);
                var outcome = controller.OpenSheet(band, framing.FrameSlot(slot, columns));
                Assert.That(controller.Scale, Is.GreaterThanOrEqualTo(range.Min), $"{device}: slot {slot + 1} {outcome}");
                var (low, high) = ScreenRectOf(camera, SlotGridLayout.SlotRect(slot, columns));
                if (outcome == SheetOutcome.TopAligned)
                {
                    Assert.That(high.y, Is.EqualTo(band.yMax).Within(1.5f), $"{device}: slot {slot + 1} footprint top on the band top");
                }
                else
                {
                    Assert.That(low.x, Is.GreaterThanOrEqualTo(band.xMin - 1.5f), $"{device}: slot {slot + 1} left ({outcome})");
                    Assert.That(low.y, Is.GreaterThanOrEqualTo(band.yMin - 1.5f), $"{device}: slot {slot + 1} bottom ({outcome})");
                    Assert.That(high.x, Is.LessThanOrEqualTo(band.xMax + 1.5f), $"{device}: slot {slot + 1} right ({outcome})");
                    Assert.That(high.y, Is.LessThanOrEqualTo(band.yMax + 1.5f), $"{device}: slot {slot + 1} top ({outcome})");
                }

                shell.CloseSheet();
                yield return ShellRig.Settle();
                Assert.That(shell.TryGetUnobstructedScreenRect(device.Height, out closed), Is.True);
                controller.CloseSheet(closed);
                Assert.That(controller.Scale, Is.EqualTo(nFit), $"{device}: the fit-all scale returns on close");
            }
        }

        [UnityTest]
        public IEnumerator The1280x720PhoneAt320DpiNeedsTheOnePercentToleranceAndPassesWithIt([Values(false, true)] bool overlays)
        {
            // Owner decisions 2026-10-28 and 2026-10-29 (S2 = D24): 2.0046 px/pt gives n_min 3 with the exact ceiling but n_fit is 2.
            var device = new ScaleDevice("phone-landscape 1280x720 xhdpi", 1280, 720, 320f, true, FactoryHudLayout.CompactLandscape);
            yield return OpenWorstCase(device, overlays);
            var pixelsPerPoint = rig.PixelsPerPoint;
            Assert.That(pixelsPerPoint, Is.EqualTo(2.0046f).Within(0.0006f), "the 7 mm rule raises the panel scale");
            var zoom = new RunZoomSettings();
            var exactCeiling = Mathf.CeilToInt(zoom.readableCellPoints * pixelsPerPoint / zoom.artPixelsPerCell);
            Assert.That(exactCeiling, Is.EqualTo(3), "without the tolerance n_min would be 3");
            var framing = new RunCameraFraming(zoom);
            var columns = SlotGridLayout.ColumnsFor(device.Width / pixelsPerPoint, device.Height / pixelsPerPoint);
            Assert.That(rig.Shell.TryGetUnobstructedScreenRect(device.Height, out var rect), Is.True);
            var range = framing.ConfigureLimits(pixelsPerPoint);
            var largest = CameraScaleModel.LargestFittingScale(rect, columns, zoom.artPixelsPerCell);
            TestContext.Out.WriteLine($"1280x720 phone overlays={overlays}: rect {rect.width:0}x{rect.height:0} px, n_min {range.Min}, largest fitting {largest}");
            Assert.That(range.Min, Is.EqualTo(2), "n_min with the 1 % tolerance");
            if (overlays)
            {
                Assert.That(largest, Is.LessThan(range.Min), "pinned finding: with the ore vein and the hint bar both shown the real rectangle holds only n = 1");
            }
            else
            {
                Assert.That(largest, Is.GreaterThanOrEqualTo(range.Min), "n_fit >= n_min with the real closed shell at D = 24");
                Assert.That(largest, Is.LessThan(exactCeiling), "the exact ceiling would have been a violation here");
            }

            var cell = range.Min * zoom.artPixelsPerCell / pixelsPerPoint;
            Assert.That(cell, Is.GreaterThanOrEqualTo(zoom.readableCellPoints * 0.99f), "the cell at n_min is within 1 % of the readable floor");
        }

        [UnityTest]
        public IEnumerator AnOversizedHudViolatesTheRuleAndTheControllerDegradesAndLogsIt()
        {
            var screen = new ScaleDevice("phone-portrait 360x640 oversized HUD", 360, 640, 0f, false, FactoryHudLayout.CompactPortrait);
            yield return OpenWorstCase(screen);
            var shell = rig.Shell;

            // An oversized HUD: a top strip tall enough to leave far less than a 12-slot grid at the smallest scale.
            var oversized = shell.Root.panel.visualTree.worldBound.height * 0.7f;
            shell.TopStrip.style.minHeight = oversized;
            shell.TopStrip.style.height = oversized;
            yield return ShellRig.Settle();

            var pixelsPerPoint = rig.PixelsPerPoint;
            var framing = new RunCameraFraming(new RunZoomSettings());
            var columns = SlotGridLayout.ColumnsFor(screen.Width / pixelsPerPoint, screen.Height / pixelsPerPoint);
            Assert.That(shell.TryGetUnobstructedScreenRect(screen.Height, out var rect), Is.True);
            var range = framing.ConfigureLimits(pixelsPerPoint);
            var largest = CameraScaleModel.LargestFittingScale(rect, columns, 24);
            Assert.That(largest, Is.LessThan(range.Min), "the rule n_fit >= n_min fails with an oversized HUD instead of passing silently");

            var log = new List<string>();
            var camera = CreateCamera(out var controller, log);
            controller.Fit(RunCameraFraming.Layout(camera, columns, pixelsPerPoint, rect));
            Assert.That(controller.Model.BelowFloor, Is.True);
            Assert.That(controller.Scale, Is.GreaterThanOrEqualTo(1), "never a fractional or zero scale");
            Assert.That(log.Count, Is.EqualTo(1));
            Assert.That(log[0], Does.StartWith(CameraScaleModel.BelowFloorCode));
        }
    }
}
#endif
