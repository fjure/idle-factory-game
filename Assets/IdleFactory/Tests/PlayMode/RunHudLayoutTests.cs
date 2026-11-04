using System.Collections;
using System.Collections.Generic;
using System.Linq;
using IdleFactory.Run.Application;
using IdleFactory.Unity.Presentation;
using IdleFactory.Unity.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;
using static IdleFactory.Tests.PlayMode.RunShellLayoutAssertions;

#if UNITY_EDITOR
namespace IdleFactory.Tests.PlayMode
{
    /// <summary>
    /// T112 (UX-1, UX-3): renders the run shell into an off-screen panel at every supported screen class, with rotations, and
    /// asserts the density rules of contracts/ui-surfaces.md (Global): no overlap, clipping contained, the focused control
    /// visible, 44 pt targets, a bottom sheet of at most 60 percent of the safe-area height (side sheet of at most 50 percent of
    /// the width in phone landscape), the Overdrive control in the side-panel header on tablet and desktop, the selected slot
    /// in view above an open sheet, and all 12 slots readable at fit-all. Extends the HudLayoutOverlapTests and
    /// ZoomReadabilityTests patterns.
    /// </summary>
    public sealed class RunHudLayoutTests
    {
        private const long Huge = 9_000_000_000_000_000_000L;

        private readonly List<ShellRig> rigs = new List<ShellRig>();
        private readonly List<GameObject> objects = new List<GameObject>();
        private ShellRig rig;

        public static IEnumerable<ShellScreen> Screens() => ShellScreens.All();

        public static IEnumerable<ShellScreen> PhoneScreens() => ShellScreens.Phones();

        public static IEnumerable<ShellScreen> SidePanelScreens() => ShellScreens.SidePanels();

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

        private IEnumerator Open(ShellScreen screen)
        {
            rig = ShellRig.Create(screen);
            rigs.Add(rig);
            yield return rig.Layout();
        }

        private IEnumerator Open(ShellScreen screen, int percent)
        {
            rig = ShellRig.Create(screen, null, percent);
            rigs.Add(rig);
            yield return rig.Layout();
        }

        public static IEnumerable<int> TextScales() => new[] { 100, 200 };

        private static void ApplyWorstCaseStrip(RunShellDocument shell)
        {
            shell.SetCash(Huge);
            shell.SetRp(Huge, true);
            shell.SetStore(Huge, Huge);
            shell.SetOverdrive(RunShellFixtures.OverdriveStatusIn(OverdriveStage.CoolingDown));
        }

        // ---------------------------------------------------------------------------------------------------
        // Shell at rest
        // ---------------------------------------------------------------------------------------------------

        [UnityTest]
        public IEnumerator TheClosedShellHasNoOverlapAndStaysInsideTheSafeArea([ValueSource(nameof(Screens))] ShellScreen screen)
        {
            yield return Open(screen);
            AssertShellLayout(rig, "closed, initial values");

            rig.Shell.SetCash(1234);
            rig.Shell.SetRp(56, true);
            rig.Shell.SetStore(120, 500);
            yield return ShellRig.Settle();
            AssertShellLayout(rig, "closed, RP unlocked");
        }

        [UnityTest]
        public IEnumerator TheTopStripKeepsItsFourControlsOnOneRowAtTheirWorstCase([ValueSource(nameof(Screens))] ShellScreen screen)
        {
            yield return Open(screen);
            ApplyWorstCaseStrip(rig.Shell);
            yield return ShellRig.Settle();

            AssertShellLayout(rig, "worst-case top strip");
            var shell = rig.Shell;
            Assert.That(shell.TopStrip.worldBound.height, Is.LessThanOrEqualTo(56f), $"{screen}: the top strip is one row, not a wrapped block");
            Assert.That(shell.StatusText, Does.Contain("Qi").Or.Contain("Q"), "the huge value is shown compactly");
            var cells = new VisualElement[] { shell.CashButton, shell.RpButton, shell.StoreButton };
            foreach (var cell in cells)
            {
                Assert.That(IsDisplayed(cell), Is.True, cell.name);
                AssertWithin(shell.TopStrip.worldBound, cell.worldBound, $"{screen}: {cell.name} stays in the top strip");
            }

            if (!shell.IsSidePanelLayout)
            {
                AssertWithin(shell.TopStrip.worldBound, shell.OverdriveButton.worldBound, $"{screen}: Overdrive stays in the top strip on a phone");
                for (var i = 0; i < cells.Length; i++)
                {
                    Assert.That(Intersects(cells[i].worldBound, shell.OverdriveButton.worldBound), Is.False, $"{screen}: {cells[i].name} overlaps Overdrive");
                    for (var j = i + 1; j < cells.Length; j++)
                    {
                        Assert.That(Intersects(cells[i].worldBound, cells[j].worldBound), Is.False, $"{screen}: {cells[i].name} overlaps {cells[j].name}");
                    }
                }
            }
        }

        [UnityTest]
        public IEnumerator NoticeAndDetailLineStayInsideTheWorldColumnAtTheirWorstCase([ValueSource(nameof(Screens))] ShellScreen screen)
        {
            yield return Open(screen);
            var shell = rig.Shell;
            ApplyWorstCaseStrip(shell);
            shell.ShowNotice("Cancelled by rotation. Nothing was spent, placed or confirmed, and the selected slot is unchanged.", ShellNoticeKind.Warning);
            shell.CashButton.Focus();
            yield return ShellRig.Settle();

            Assert.That(shell.DetailText, Does.StartWith("Cash: 9,000,000,000,000,000,000"), "focus shows the exact value");
            AssertShellLayout(rig, "notice and detail line");

            shell.SelectTab(ShellTab.Market);
            Assert.That(shell.NoticeText, Is.Empty, "a tab change clears a transient notice");
            shell.ShowNotice("Cancelled by rotation. Nothing was spent, placed or confirmed, and the selected slot is unchanged.", ShellNoticeKind.Warning);
            yield return ShellRig.Settle();
            AssertShellLayout(rig, "notice and detail line with an open sheet");
        }

        // ---------------------------------------------------------------------------------------------------
        // Density rules (UX-1)
        // ---------------------------------------------------------------------------------------------------

        [UnityTest]
        public IEnumerator PhoneDensityTopStripHoldsCashRpStoreAndOverdriveAndTheTabBarHoldsSixTabs([ValueSource(nameof(PhoneScreens))] ShellScreen screen)
        {
            yield return Open(screen);
            var shell = rig.Shell;
            shell.SetRp(10, true);
            shell.SetOverdrive(RunShellFixtures.OverdriveStatusIn(OverdriveStage.Ready));
            yield return ShellRig.Settle();

            Assert.That(shell.IsSidePanelLayout, Is.False);
            foreach (var control in new VisualElement[] { shell.CashButton, shell.RpButton, shell.StoreButton, shell.OverdriveButton })
            {
                Assert.That(shell.TopStrip.Contains(control), Is.True, $"{screen}: {control.name} is in the top strip");
            }

            Assert.That(shell.TabBar.parent.name, Is.EqualTo("run-body"), $"{screen}: the tab bar is the bottom bar of the shell");
            var tabs = System.Enum.GetValues(typeof(ShellTab)).Cast<ShellTab>().Select(shell.TabButton).ToArray();
            Assert.That(tabs.Length, Is.EqualTo(6));
            foreach (var tab in tabs)
            {
                Assert.That(tab.worldBound.width, Is.GreaterThanOrEqualTo(44f - Tolerance), $"{screen}: {tab.name} width");
                Assert.That(tab.worldBound.height, Is.GreaterThanOrEqualTo(44f - Tolerance), $"{screen}: {tab.name} height");
                AssertWithin(shell.TabBar.worldBound, tab.worldBound, $"{screen}: {tab.name} in the tab bar");
            }

            for (var i = 1; i < tabs.Length; i++)
            {
                Assert.That(tabs[i].worldBound.xMin, Is.GreaterThanOrEqualTo(tabs[i - 1].worldBound.xMax - Tolerance), $"{screen}: tabs do not overlap");
            }

            Assert.That(tabs.Select(tab => tab.Q<Label>().text), Is.EqualTo(new[] { "Factory", "Market", "Contracts", "Items", "Research", "Prestige" }));
            Assert.That(shell.TabBar.worldBound.yMax, Is.EqualTo(rig.SafeRect().yMax).Within(1f), $"{screen}: the tab bar sits on the bottom edge of the safe area");
            Assert.That(IsDisplayed(shell.PanelHost), Is.False, $"{screen}: no sheet is open until a tab or slot opens one");
        }

        [UnityTest]
        public IEnumerator PhoneSheetIsAtMostSixtyPercentOfTheSafeAreaHeightAndLandscapeSheetsAreSideSheetsOfAtMostHalfTheWidth(
            [ValueSource(nameof(PhoneScreens))] ShellScreen screen)
        {
            yield return Open(screen);
            var shell = rig.Shell;
            var panel = new TestShellPanel(ShellPanelIds.Contracts, "Contracts", 14);
            shell.Panels.Register(panel);
            shell.SelectTab(ShellTab.Contracts);
            yield return ShellRig.Settle();

            var safe = rig.SafeRect();
            var host = shell.PanelHost.worldBound;
            Assert.That(IsDisplayed(shell.PanelHost), Is.True);
            if (screen.Expected == FactoryHudLayout.CompactPortrait)
            {
                Assert.That(host.height, Is.LessThanOrEqualTo(safe.height * 0.6f + Tolerance), $"{screen}: bottom sheet height {host.height} of {safe.height}");
                Assert.That(host.width, Is.EqualTo(safe.width).Within(1f), $"{screen}: the bottom sheet spans the safe width");
                Assert.That(host.yMax, Is.LessThanOrEqualTo(shell.TabBar.worldBound.yMin + Tolerance), $"{screen}: the sheet sits above the tab bar");
            }
            else
            {
                Assert.That(host.width, Is.LessThanOrEqualTo(safe.width * 0.5f + Tolerance), $"{screen}: side sheet width {host.width} of {safe.width}");
                Assert.That(host.xMax, Is.EqualTo(safe.xMax).Within(1f), $"{screen}: the side sheet is on the right edge");
            }

            AssertShellLayout(rig, "open sheet with worst-case content");
            Assert.That(shell.PanelScroll.contentContainer.layout.height, Is.GreaterThan(shell.PanelScroll.layout.height),
                $"{screen}: worst-case content is taller than the sheet, so the sheet scrolls");
        }

        [UnityTest]
        public IEnumerator SidePanelDensityPutsOverdriveInThePanelHeaderAndKeepsTheTabsInThePanel([ValueSource(nameof(SidePanelScreens))] ShellScreen screen)
        {
            yield return Open(screen);
            var shell = rig.Shell;
            shell.SetOverdrive(RunShellFixtures.OverdriveStatusIn(OverdriveStage.Ready));
            yield return ShellRig.Settle();

            Assert.That(shell.IsSidePanelLayout, Is.True);
            Assert.That(IsDisplayed(shell.PanelHost), Is.True, $"{screen}: the side panel is always shown");
            Assert.That(shell.Root.Q<VisualElement>("panel-header-slot").Contains(shell.OverdriveButton), Is.True, $"{screen}: Overdrive is in the side-panel header");
            Assert.That(shell.TopStrip.Contains(shell.OverdriveButton), Is.False, $"{screen}: Overdrive is not in the top strip");
            Assert.That(shell.PanelHost.Contains(shell.TabBar), Is.True, $"{screen}: the tabs live in the side panel");
            Assert.That(shell.PanelHost.worldBound.width, Is.EqualTo(360f).Within(1f));
            var safe = rig.SafeRect();
            Assert.That(shell.PanelHost.worldBound.yMin, Is.EqualTo(safe.yMin).Within(1f));
            Assert.That(shell.PanelHost.worldBound.yMax, Is.EqualTo(safe.yMax).Within(1f));
            Assert.That(shell.PanelHost.worldBound.xMax, Is.EqualTo(safe.xMax).Within(1f));
            Assert.That(shell.Root.Q<VisualElement>("panel-close").resolvedStyle.display, Is.EqualTo(DisplayStyle.None), $"{screen}: no close button on a permanent panel");
            AssertShellLayout(rig, "side panel, idle");

            var panel = new TestShellPanel(ShellPanelIds.Market, "Market", 14);
            shell.Panels.Register(panel);
            shell.SelectTab(ShellTab.Market);
            yield return ShellRig.Settle();
            AssertShellLayout(rig, "side panel with worst-case content");
            var viewport = shell.PanelScroll.Q<VisualElement>(className: "unity-scroll-view__content-viewport").worldBound;
            Assert.That(viewport.height, Is.GreaterThanOrEqualTo(MinimumTarget), $"{screen}: the panel keeps room for a touch target");
        }

        // ---------------------------------------------------------------------------------------------------
        // Worst-case shell content and focus visibility (UX-3)
        // ---------------------------------------------------------------------------------------------------

        [UnityTest]
        public IEnumerator WorstCaseSheetContentIsClippedToTheSheetAndEveryFocusedControlIsVisible([ValueSource(nameof(Screens))] ShellScreen screen)
        {
            yield return Open(screen);
            var shell = rig.Shell;
            ApplyWorstCaseStrip(shell);
            var panel = new TestShellPanel(ShellPanelIds.Items, "Items", 16);
            shell.Panels.Register(panel);
            shell.SelectTab(ShellTab.Items);
            yield return ShellRig.Settle();

            var scroll = shell.PanelScroll;
            var viewport = scroll.Q<VisualElement>(className: "unity-scroll-view__content-viewport");
            foreach (var offset in new[] { 0f, 0.5f, 1f })
            {
                var range = Mathf.Max(0f, scroll.contentContainer.layout.height - viewport.layout.height);
                scroll.scrollOffset = new Vector2(scroll.scrollOffset.x, range * offset);
                yield return ShellRig.Settle();
                AssertScrollContentClipped(scroll, $"{screen} scrolled to {offset:P0}");
            }

            // A clipped control scrolls into view when it receives focus.
            var safe = rig.SafeRect();
            foreach (var button in new[] { panel.Buttons.Last(), panel.Buttons.First(), panel.Buttons[panel.Buttons.Count / 2] })
            {
                scroll.scrollOffset = new Vector2(0f, button == panel.Buttons.Last() ? 0f : float.MaxValue);
                yield return ShellRig.Settle();
                button.Focus();
                yield return ShellRig.Settle();
                Assert.That(button.focusController.focusedElement, Is.SameAs(button), $"{screen}: '{button.name}' did not take focus");
                AssertWithin(viewport.worldBound, button.worldBound, $"{screen}: focused '{button.name}' is not scrolled into view");
                AssertWithin(safe, button.worldBound, $"{screen}: focused '{button.name}' leaves the safe area");
            }

            // The strip and tab controls stay visible when focused.
            foreach (var control in new VisualElement[] { shell.CashButton, shell.StoreButton, shell.TabButton(ShellTab.Prestige), shell.ZoomInButton })
            {
                control.Focus();
                yield return ShellRig.Settle();
                Assert.That(control.focusController.focusedElement, Is.SameAs(control), $"{screen}: {control.name}");
                AssertWithin(safe, control.worldBound, $"{screen}: focused {control.name} leaves the safe area");
                Assert.That(control.ClassListContains("run-control--focused"), Is.True, $"{screen}: {control.name} shows its focus ring");
            }

            AssertShellLayout(rig, "worst-case sheet after focus changes");
        }

        [UnityTest]
        public IEnumerator TheConfirmationDialogWithWorstCaseTextStaysInsideTheSafeArea([ValueSource(nameof(Screens))] ShellScreen screen)
        {
            yield return Open(screen);
            var shell = rig.Shell;
            var invoker = shell.TabButton(ShellTab.Prestige);
            shell.SelectTab(ShellTab.Prestige);
            Assert.That(shell.RequestConfirmation(new ConfirmationRequest
            {
                Title = "Sell the whole factory for Industry points and start a new run with all permanent upgrades kept?",
                Message = string.Concat(Enumerable.Repeat("Selling resets Cash, stock, machines and expansion. Industry points, Research and Industry upgrades are kept. ", 12)),
                ConfirmLabel = "Sell the factory and start run 12",
                CancelLabel = "Keep playing",
                Destructive = true,
                Invoker = invoker,
            }), Is.True);
            yield return ShellRig.Settle();

            var safe = rig.SafeRect();
            var card = shell.DialogCard.worldBound;
            AssertWithin(safe, card, $"{screen}: the dialog card stays inside the safe area");
            Assert.That(shell.DialogCancelButton.worldBound.width, Is.GreaterThanOrEqualTo(MinimumTarget - Tolerance));
            Assert.That(shell.DialogConfirmButton.worldBound.height, Is.GreaterThanOrEqualTo(MinimumTarget - Tolerance));
            AssertWithin(card, shell.DialogCancelButton.worldBound, $"{screen}: Cancel stays in the card");
            AssertWithin(card, shell.DialogConfirmButton.worldBound, $"{screen}: Confirm stays in the card");
            Assert.That(Intersects(shell.DialogCancelButton.worldBound, shell.DialogConfirmButton.worldBound), Is.False, $"{screen}: the two buttons do not overlap");
            Assert.That(shell.DialogCancelButton.focusController.focusedElement, Is.SameAs(shell.DialogCancelButton), "the dialog opens on Cancel");
            AssertTextAndTargets(shell.DialogLayer, safe, $"{screen} dialog");
        }

        // ---------------------------------------------------------------------------------------------------
        // Rotation (UX-9)
        // ---------------------------------------------------------------------------------------------------

        [UnityTest]
        public IEnumerator ARotationRelayoutsAnOpenSheetAndCancelsAnOpenDialogWithoutOverlap([ValueSource(nameof(Screens))] ShellScreen screen)
        {
            // The render texture keeps the target size; only the layout inputs rotate, as on a configuration change.
            var before = ShellScreens.Rotated(screen);
            yield return Open(screen);
            var shell = rig.Shell;
            shell.ApplyScreenLayout(before.Width, before.Height, before.SafeAreaPx);
            yield return ShellRig.Settle();
            ApplyWorstCaseStrip(shell);
            var panel = new TestShellPanel(ShellPanelIds.Prestige, "Prestige", 10);
            shell.Panels.Register(panel);
            shell.SelectTab(ShellTab.Prestige);
            var confirmed = 0;
            ConfirmationCloseReason? closed = null;
            var invoker = panel.Buttons[0];
            shell.RequestConfirmation(new ConfirmationRequest
            {
                Title = "Sell the factory?",
                Message = "This cannot be undone.",
                Destructive = true,
                Invoker = invoker,
                OnConfirm = () => confirmed++,
                OnCancel = reason => closed = reason,
            });
            yield return ShellRig.Settle();
            Assert.That(shell.Layout, Is.EqualTo(before.Expected), "the shell was laid out for the other orientation first");

            // The platform rotates: the bootstrap cancels transient actions, then re-lays the shell out.
            Assert.That(shell.CancelConfirmationForRotation(), Is.True);
            yield return rig.Layout(screen);
            Assert.That(confirmed, Is.EqualTo(0), "a rotation never confirms");
            Assert.That(closed, Is.EqualTo(ConfirmationCloseReason.Rotation));
            Assert.That(shell.IsConfirmationOpen, Is.False);
            Assert.That(shell.NoticeText, Is.EqualTo("Cancelled by rotation"));
            Assert.That(invoker.focusController.focusedElement, Is.SameAs(invoker), $"{screen}: focus returns to the invoking control after the rotation");
            AssertShellLayout(rig, "after the rotation");
        }

        // ---------------------------------------------------------------------------------------------------
        // Slot visibility above a sheet and readable zoom (extends ZoomReadabilityTests)
        // ---------------------------------------------------------------------------------------------------

        private Camera CreateFramingCamera(out FactoryCameraController controller)
        {
            var host = new GameObject("run-shell-camera");
            objects.Add(host);
            host.transform.position = new Vector3(0f, 0f, -10f);
            var camera = host.AddComponent<Camera>();
            camera.orthographic = true;
            camera.targetTexture = rig.Texture;
            camera.enabled = false;
            controller = host.AddComponent<FactoryCameraController>();
            controller.Configure(camera, new RunZoomSettings().ToScaleSettings());
            return camera;
        }

        private static (Vector2 Low, Vector2 High) ScreenRectOf(Camera camera, Rect world)
        {
            var low = camera.WorldToScreenPoint(new Vector3(world.xMin, world.yMin, 0f));
            var high = camera.WorldToScreenPoint(new Vector3(world.xMax, world.yMax, 0f));
            return (new Vector2(Mathf.Min(low.x, high.x), Mathf.Min(low.y, high.y)), new Vector2(Mathf.Max(low.x, high.x), Mathf.Max(low.y, high.y)));
        }

        [UnityTest]
        public IEnumerator FitAllShowsAllTwelveSlotsReadableAtEveryScreenClass([ValueSource(nameof(Screens))] ShellScreen screen)
        {
            yield return Open(screen);
            var camera = CreateFramingCamera(out var controller);
            var zoom = new RunZoomSettings();
            var framing = new RunCameraFraming(zoom);
            var columns = SlotGridLayout.ColumnsFor(screen.Width, screen.Height);
            var pixelsPerPoint = rig.PixelsPerPoint;

            Assert.That(rig.Shell.TryGetUnobstructedScreenRect(screen.Height, out var area), Is.True);
            controller.Fit(RunCameraFraming.Layout(camera, columns, pixelsPerPoint, area));
            var range = framing.ConfigureLimits(pixelsPerPoint);
            Assert.That(controller.Scale, Is.EqualTo(framing.FitAll(area, columns, pixelsPerPoint)), $"{screen}: the controller fits the scale the framing computes");
            Assert.That(controller.Scale, Is.GreaterThanOrEqualTo(range.Min), $"{screen}: fit-all is at least n_min (Art Bible 8.2, readable cell {zoom.readableCellPoints} pt)");
            Assert.That(controller.Scale, Is.LessThanOrEqualTo(range.Max));
            Assert.That(camera.orthographicSize, Is.EqualTo(camera.pixelHeight / (2f * controller.Scale * zoom.artPixelsPerCell)).Within(1e-4f), $"{screen}: rule 1");
            var projected = RunCameraFraming.PixelsPerCell(camera, pixelsPerPoint);
            Assert.That(projected, Is.EqualTo(controller.Scale * zoom.artPixelsPerCell / pixelsPerPoint).Within(0.05f));

            // n_min guarantees a cell of at least readableCellPoints within the 1 % tolerance of the contract.
            var minimumCell = zoom.readableCellPoints - CameraScaleModel.MinScaleTolerance * zoom.artPixelsPerCell / pixelsPerPoint;
            for (var slot = 0; slot < SlotGridLayout.SlotCount; slot++)
            {
                var (low, high) = ScreenRectOf(camera, SlotGridLayout.SlotRect(slot, columns));
                Assert.That(low.x, Is.GreaterThanOrEqualTo(area.xMin - 1f), $"{screen}: slot {slot + 1} left of the world area");
                Assert.That(low.y, Is.GreaterThanOrEqualTo(area.yMin - 1f), $"{screen}: slot {slot + 1} below the world area");
                Assert.That(high.x, Is.LessThanOrEqualTo(area.xMax + 1f), $"{screen}: slot {slot + 1} right of the world area");
                Assert.That(high.y, Is.LessThanOrEqualTo(area.yMax + 1f), $"{screen}: slot {slot + 1} above the world area");
                Assert.That((high.x - low.x) / pixelsPerPoint, Is.GreaterThanOrEqualTo(2f * minimumCell - 0.5f), $"{screen}: slot {slot + 1} is at least two readable cells wide");
            }
        }

        [UnityTest]
        public IEnumerator TheSelectedSlotStaysInViewAboveAnOpenSheet([ValueSource(nameof(Screens))] ShellScreen screen)
        {
            yield return Open(screen);
            var shell = rig.Shell;
            var build = new TestShellPanel(ShellPanelIds.Build, "Build", 8);
            shell.Panels.Register(build);
            var camera = CreateFramingCamera(out var controller);
            var framing = new RunCameraFraming(new RunZoomSettings());
            var columns = SlotGridLayout.ColumnsFor(screen.Width, screen.Height);
            var pixelsPerPoint = rig.PixelsPerPoint;
            Assert.That(shell.TryGetUnobstructedScreenRect(screen.Height, out var closedArea), Is.True);
            controller.Fit(RunCameraFraming.Layout(camera, columns, pixelsPerPoint, closedArea));
            var fitted = controller.Scale;
            var range = framing.ConfigureLimits(pixelsPerPoint);

            for (var slot = 0; slot < SlotGridLayout.SlotCount; slot++)
            {
                Assert.That(shell.OpenSlot(slot, SlotCondition.Empty), Is.True);
                yield return ShellRig.Settle();
                Assert.That(shell.TryGetUnobstructedScreenRect(screen.Height, out var area), Is.True);
                var outcome = controller.OpenSheet(area, framing.FrameSlot(slot, columns));

                var (low, high) = ScreenRectOf(camera, SlotGridLayout.SlotRect(slot, columns));
                Assert.That(low.x, Is.GreaterThanOrEqualTo(area.xMin - 1.5f), $"{screen}: slot {slot + 1} left of the world area above the sheet ({outcome})");
                Assert.That(low.y, Is.GreaterThanOrEqualTo(area.yMin - 1.5f), $"{screen}: slot {slot + 1} below the world area above the sheet ({outcome})");
                Assert.That(high.x, Is.LessThanOrEqualTo(area.xMax + 1.5f), $"{screen}: slot {slot + 1} right of the world area above the sheet ({outcome})");
                Assert.That(high.y, Is.LessThanOrEqualTo(area.yMax + 1.5f), $"{screen}: slot {slot + 1} above the world area above the sheet ({outcome})");
                Assert.That(controller.Scale, Is.GreaterThanOrEqualTo(range.Min), $"{screen}: the sheet step never goes below n_min");
                Assert.That(controller.Scale, Is.LessThanOrEqualTo(fitted));
                AssertShellLayout(rig, $"slot {slot + 1} selected");

                shell.CloseSheet();
                yield return ShellRig.Settle();
                Assert.That(shell.TryGetUnobstructedScreenRect(screen.Height, out closedArea), Is.True);
                controller.CloseSheet(closedArea);
                Assert.That(controller.Scale, Is.EqualTo(fitted), $"{screen}: the closed sheet returns the fit-all scale");
            }

            Assert.That(fitted, Is.GreaterThanOrEqualTo(range.Min));
        }

        [UnityTest]
        public IEnumerator ASheetOpenedOnAPhoneLeavesAWorldBandThatShowsAWholeSlot([ValueSource(nameof(PhoneScreens))] ShellScreen screen)
        {
            yield return Open(screen);
            var shell = rig.Shell;
            shell.Panels.Register(new TestShellPanel(ShellPanelIds.Market, "Market", 20));
            shell.SelectTab(ShellTab.Market);
            yield return ShellRig.Settle();

            Assert.That(shell.TryGetUnobstructedScreenRect(screen.Height, out var area), Is.True);
            // At n_min a slot footprint is two readable cells (48 pt at the Z24 floor); the world band above (or beside) the sheet
            // must show one with its half-cell margin.
            var zoom = new RunZoomSettings();
            var pixelsPerPoint = rig.PixelsPerPoint;
            var footprint = CameraScaleModel.FootprintPoints(new RunCameraFraming(zoom).ConfigureLimits(pixelsPerPoint).Min, zoom.artPixelsPerCell, pixelsPerPoint);
            Assert.That(area.height / pixelsPerPoint, Is.GreaterThanOrEqualTo(footprint + 12f), $"{screen}: world band height {area.height}");
            Assert.That(area.width / pixelsPerPoint, Is.GreaterThanOrEqualTo(footprint + 12f), $"{screen}: world band width {area.width}");
        }

        // ---------------------------------------------------------------------------------------------------
        // T192 shell polish (UI/UX review of the shell gate, F-1 to F-8)
        // ---------------------------------------------------------------------------------------------------

        private static void AssertHasIcon(VisualElement element, string context)
        {
            var background = element.style.backgroundImage;
            Assert.That(background.keyword == StyleKeyword.Undefined && background.value.sprite != null, Is.True, context);
        }

        [UnityTest]
        public IEnumerator F1_ANoticeCarriesTheIconOfItsKindAndLooksDistinctFromTheDetailLine(
            [ValueSource(nameof(Screens))] ShellScreen screen, [ValueSource(nameof(TextScales))] int percent)
        {
            yield return Open(screen, percent);
            var shell = rig.Shell;
            shell.CashButton.Focus();
            var expectedTint = new Dictionary<ShellNoticeKind, string>
            {
                { ShellNoticeKind.Info, "run-icon--tint-accent" },
                { ShellNoticeKind.Warning, "run-icon--tint-warning" },
                { ShellNoticeKind.Error, "run-icon--tint-danger" },
                { ShellNoticeKind.Lock, "run-icon--tint-muted" },
            };
            foreach (var pair in expectedTint)
            {
                shell.ShowNotice("Slot 5 unlocks at factory level 2, and this line is long enough to wrap at a large text scale.", pair.Key);
                yield return ShellRig.Settle();
                var context = $"{screen} @{percent}% {pair.Key}";
                AssertHasIcon(shell.NoticeIconElement, context + ": the notice has its icon");
                Assert.That(shell.NoticeIconElement.ClassListContains(pair.Value), Is.True, context + ": icon tint");
                Assert.That(shell.NoticeKind, Is.EqualTo(pair.Key));
                var text = shell.NoticeBar.Q<Label>("notice-text");
                AssertWithin(shell.NoticeBar.worldBound, shell.NoticeIconElement.worldBound, context + ": the icon stays in the notice");
                AssertWithin(shell.NoticeBar.worldBound, text.worldBound, context + ": the text stays in the notice");
                Assert.That(Intersects(shell.NoticeIconElement.worldBound, text.worldBound), Is.False, context + ": icon and text do not overlap");
                Assert.That(IsDisplayed(shell.DetailBar), Is.True, context + ": the detail line is shown beside it");
                Assert.That(shell.NoticeBar.resolvedStyle.backgroundColor, Is.Not.EqualTo(shell.DetailBar.resolvedStyle.backgroundColor), context + ": a different fill");
                Assert.That(shell.NoticeBar.resolvedStyle.borderLeftWidth, Is.GreaterThan(shell.DetailBar.resolvedStyle.borderLeftWidth), context + ": the colored bar marks a notice");
                AssertShellLayout(rig, context);
            }
        }

        [UnityTest]
        public IEnumerator F1_ATransientNoticeClearsAfterItsTimeoutOnASheetOrTabChangeAndOnAWorldPressButAStandingOneStays()
        {
            yield return Open(ShellScreens.All().First());
            var shell = rig.Shell;
            shell.NoticeSeconds = 0.2f;
            shell.ShowNotice("Slot 5 unlocks at factory level 2.", ShellNoticeKind.Lock);
            Assert.That(shell.NoticeText, Is.Not.Empty);
            yield return new WaitForSecondsRealtime(0.8f);
            Assert.That(shell.NoticeText, Is.Empty, "the notice clears itself after a timeout");
            Assert.That(shell.NoticeBar.resolvedStyle.display, Is.EqualTo(DisplayStyle.None));

            shell.NoticeSeconds = 60f;
            shell.ShowNotice("Not enough Cash.", ShellNoticeKind.Error);
            shell.SelectTab(ShellTab.Market);
            Assert.That(shell.NoticeText, Is.Empty, "a tab change clears it");
            shell.ShowNotice("Not enough Cash.", ShellNoticeKind.Error);
            shell.CloseSheet();
            Assert.That(shell.NoticeText, Is.Empty, "closing the sheet clears it");
            shell.ShowNotice("Not enough Cash.", ShellNoticeKind.Error);
            Assert.That(shell.OpenSlot(0, SlotCondition.Empty), Is.True);
            Assert.That(shell.NoticeText, Is.Empty, "opening a slot clears it");
            shell.ShowNotice("Not enough Cash.", ShellNoticeKind.Error);
            shell.NotifyWorldPress();
            Assert.That(shell.NoticeText, Is.Empty, "the next interaction (a world press) clears it");

            // A standing condition is not an event: it neither times out nor clears on an interaction, and it comes back after a clear.
            shell.NoticeSeconds = 0.2f;
            shell.SetGameplayAvailable(false, "The factory is not available right now.");
            yield return new WaitForSecondsRealtime(0.6f);
            Assert.That(shell.NoticeText, Is.EqualTo("The factory is not available right now."));
            shell.NotifyWorldPress();
            shell.SelectTab(ShellTab.Items);
            Assert.That(shell.NoticeText, Is.EqualTo("The factory is not available right now."));
            shell.ShowNotice("Cancelled by rotation");
            shell.ClearNotice();
            Assert.That(shell.NoticeText, Is.EqualTo("The factory is not available right now."), "the standing notice returns");
            shell.SetGameplayAvailable(true);
            Assert.That(shell.NoticeText, Is.Empty, "it clears when the condition stops being true");
        }

        [UnityTest]
        public IEnumerator F1_TheLockedSlotNoticeClearsOnceTheSlotIsNoLongerLocked()
        {
            yield return Open(ShellScreens.All().First());
            var shell = rig.Shell;
            Assert.That(shell.OpenSlot(4, SlotCondition.Locked, 2), Is.False);
            Assert.That(shell.NoticeText, Is.EqualTo("Slot 5 unlocks at factory level 2."));
            Assert.That(shell.NoticeKind, Is.EqualTo(ShellNoticeKind.Lock));
            Assert.That(shell.LockNoticeSlot, Is.EqualTo(4));
            shell.ReconcileLockNotice(3, SlotCondition.Empty);
            shell.ReconcileLockNotice(4, SlotCondition.Locked);
            Assert.That(shell.NoticeText, Is.Not.Empty, "another slot, or the slot still locked, keeps it");
            shell.ReconcileLockNotice(4, SlotCondition.Empty);
            Assert.That(shell.NoticeText, Is.Empty, "the slot opened, so the notice stopped being true");
            Assert.That(shell.LockNoticeSlot, Is.EqualTo(-1));
            yield return null;
        }

        [UnityTest]
        public IEnumerator F2_APhoneWithTheSheetClosedShowsTheBuildAMinerHintOfAnEmptyGrid(
            [ValueSource(nameof(Screens))] ShellScreen screen, [ValueSource(nameof(TextScales))] int percent)
        {
            yield return Open(screen, percent);
            var shell = rig.Shell;
            shell.SetEmptyHint(true);
            yield return ShellRig.Settle();
            var context = $"{screen} @{percent}%";
            if (screen.IsPhone)
            {
                Assert.That(shell.IsEmptyHintShown, Is.True, context + ": the hint is shown on a phone with the sheet closed");
                Assert.That(shell.WorldHint.Q<Label>("world-hint-text").text, Does.Contain("Build a Miner"));
                AssertHasIcon(shell.WorldHint.Q<VisualElement>("world-hint-icon"), context + ": the hint carries the build icon");
                AssertShellLayout(rig, context + " empty hint");

                shell.SelectTab(ShellTab.Market);
                yield return ShellRig.Settle();
                Assert.That(shell.IsEmptyHintShown, Is.False, context + ": an open sheet hides the hint");
                shell.CloseSheet();
                yield return ShellRig.Settle();
                Assert.That(shell.IsEmptyHintShown, Is.True, context + ": closing the sheet brings it back");
                shell.SetEmptyHint(false);
                yield return ShellRig.Settle();
                Assert.That(shell.IsEmptyHintShown, Is.False, context + ": a first machine removes the hint");
            }
            else
            {
                Assert.That(shell.IsEmptyHintShown, Is.False, context + ": the permanent side panel carries the guidance instead");
                AssertShellLayout(rig, context + " empty grid, side panel");
            }
        }

        [UnityTest]
        public IEnumerator F5_EveryPinnableStripValueCarriesAHintGlyphInsideItsCell(
            [ValueSource(nameof(Screens))] ShellScreen screen, [ValueSource(nameof(TextScales))] int percent)
        {
            yield return Open(screen, percent);
            var shell = rig.Shell;
            ApplyWorstCaseStrip(shell);
            yield return ShellRig.Settle();
            foreach (var host in new VisualElement[] { shell.CashButton, shell.RpButton, shell.StoreButton })
            {
                var context = $"{screen} @{percent}% {host.name}";
                var hint = shell.PinHintOf(host);
                Assert.That(hint, Is.Not.Null, context + ": has a hint glyph");
                Assert.That(IsDisplayed(hint), Is.True, context);
                AssertHasIcon(hint, context + ": the glyph has its icon");
                Assert.That(hint.pickingMode, Is.EqualTo(PickingMode.Ignore), context + ": the glyph never takes a press");
                AssertWithin(host.worldBound, hint.worldBound, context + ": the glyph stays in its cell");
                foreach (var label in host.Query<Label>().ToList())
                {
                    // The cell keeps a right margin for the glyph. A cell the layout squeezes (the worst-case value at 200 percent
                    // text) already lets its value run into that margin, which T191 accepts; there the glyph sits on the cell corner.
                    if (label.worldBound.xMax <= host.worldBound.xMax - 12f + Tolerance)
                    {
                        Assert.That(Intersects(hint.worldBound, label.worldBound), Is.False, context + ": the glyph does not cover '" + label.text + "'");
                    }
                }
            }

            AssertShellLayout(rig, $"{screen} @{percent}% pin hints");
        }

        [UnityTest]
        public IEnumerator F6_TheWarningIconOfADestructiveConfirmSitsOnTheConfirmButton(
            [ValueSource(nameof(Screens))] ShellScreen screen, [ValueSource(nameof(TextScales))] int percent)
        {
            yield return Open(screen, percent);
            var shell = rig.Shell;
            var request = new ConfirmationRequest
            {
                Title = "Remove the machine?",
                Message = "You get half of the Cash back.",
                ConfirmLabel = "Remove the machine",
                Destructive = true,
                Invoker = shell.TabButton(ShellTab.Prestige),
            };
            Assert.That(shell.RequestConfirmation(request), Is.True);
            yield return ShellRig.Settle();
            var context = $"{screen} @{percent}%";
            var confirm = shell.DialogConfirmButton;
            AssertHasIcon(confirm, context + ": the destructive Confirm button carries the warning icon");
            Assert.That(confirm.resolvedStyle.paddingLeft, Is.GreaterThanOrEqualTo(40f), context + ": the label keeps clear of the icon");
            AssertWithin(shell.DialogCard.worldBound, confirm.worldBound, context + ": Confirm stays in the dialog");
            Assert.That(confirm.worldBound.height, Is.GreaterThanOrEqualTo(MinimumTarget - Tolerance));
            AssertTextAndTargets(shell.DialogLayer, rig.SafeRect(), context + " destructive dialog", MinimumFontSize * percent / 100f);

            // A dialog that is not destructive shows no icon on its button.
            shell.HandleBack();
            yield return ShellRig.Settle();
            request.Destructive = false;
            Assert.That(shell.RequestConfirmation(request), Is.True);
            yield return ShellRig.Settle();
            Assert.That(shell.DialogConfirmButton.style.backgroundImage.keyword, Is.EqualTo(StyleKeyword.None), context + ": no icon on a plain confirm");
        }

        [UnityTest]
        public IEnumerator F8_TheCameraAreaExcludesTheNoticeDetailAndHintBarsAndTheBarsTakeNoWorldPointers(
            [ValueSource(nameof(Screens))] ShellScreen screen, [ValueSource(nameof(TextScales))] int percent)
        {
            yield return Open(screen, percent);
            var shell = rig.Shell;
            Assert.That(shell.TryGetUnobstructedScreenRect(screen.Height, out var clear), Is.True);

            shell.SetEmptyHint(true);
            shell.ShowNotice("Slot 5 unlocks at factory level 2.", ShellNoticeKind.Lock);
            shell.CashButton.Focus();
            yield return ShellRig.Settle();
            Assert.That(shell.TryGetUnobstructedScreenRect(screen.Height, out var covered), Is.True);

            var context = $"{screen} @{percent}%";
            var bars = new List<VisualElement> { shell.DetailBar, shell.NoticeBar };
            if (screen.IsPhone)
            {
                bars.Add(shell.WorldHint);
            }

            var lowestBar = 0f;
            foreach (var bar in bars)
            {
                Assert.That(IsDisplayed(bar), Is.True, context + ": " + bar.name + " is shown");
                lowestBar = Mathf.Max(lowestBar, bar.worldBound.yMax);
            }

            var coveredTop = screen.Height - covered.yMax;
            Assert.That(coveredTop, Is.GreaterThanOrEqualTo(lowestBar - 1f), context + ": the area starts below the lowest bar");
            Assert.That(covered.height, Is.LessThan(clear.height - 1f), context + ": the bars shrink the area");

            // The slots fit-all frames into that area lie below the bars.
            var camera = CreateFramingCamera(out var controller);
            var columns = SlotGridLayout.ColumnsFor(screen.Width, screen.Height);
            controller.Fit(RunCameraFraming.Layout(camera, columns, rig.PixelsPerPoint, covered));
            var gridFits = CameraScaleModel.LargestFittingScale(covered, columns, new RunZoomSettings().artPixelsPerCell) >= 1;
            for (var slot = 0; gridFits && slot < SlotGridLayout.SlotCount; slot++)
            {
                // Whenever the whole grid fits the area (n_fit >= 1), no slot reaches under a bar.
                var (low, high) = ScreenRectOf(camera, SlotGridLayout.SlotRect(slot, columns));
                Assert.That(high.y, Is.LessThanOrEqualTo(covered.yMax + 1f), $"{context}: slot {slot + 1} stays below the bars");
                Assert.That(low.y, Is.GreaterThanOrEqualTo(covered.yMin - 1f), $"{context}: slot {slot + 1} bottom");
            }

            // The bars do not take pointers: a press anywhere over them reaches the world.
            foreach (var bar in bars)
            {
                var picked = shell.Root.panel.Pick(bar.worldBound.center);
                Assert.That(picked != null && (picked == bar || bar.Contains(picked)), Is.False, context + ": " + bar.name + " takes the pointer");
                Assert.That(bar.pickingMode, Is.EqualTo(PickingMode.Ignore), context + ": " + bar.name);
            }
        }

        [UnityTest]
        public IEnumerator F3_TheFocusRingOfAControlAtThePanelEdgeStaysInsideThePanel(
            [ValueSource(nameof(Screens))] ShellScreen screen, [ValueSource(nameof(TextScales))] int percent)
        {
            yield return Open(screen, percent);
            var shell = rig.Shell;
            shell.SetRp(10, true);
            yield return ShellRig.Settle();
            var panel = shell.Root.panel.visualTree.worldBound;
            var edgeControls = new List<VisualElement>
            {
                shell.CashButton, shell.TabButton(ShellTab.Factory), shell.TabButton(ShellTab.Prestige), shell.ZoomInButton, shell.OverdriveButton,
            };
            foreach (var control in edgeControls)
            {
                control.Focus();
                yield return ShellRig.Settle();
                var context = $"{screen} @{percent}% {control.name}";
                Assert.That(shell.FocusRingTarget, Is.SameAs(control), context);
                var ring = shell.FocusRing.worldBound;
                AssertWithin(panel, ring, context + ": the whole ring is inside the panel");
                AssertWithin(ring, control.worldBound, context + ": the ring still encloses the control");
                Assert.That(shell.FocusRing.resolvedStyle.display, Is.EqualTo(DisplayStyle.Flex), context);
            }
        }

        // ---------------------------------------------------------------------------------------------------
        // T463: the camera rectangle is corner-aware for the ore vein
        // ---------------------------------------------------------------------------------------------------

        [UnityTest]
        public IEnumerator TheRectangleCutsTheOreVeinCornerWhicheverWayKeepsTheLargerScaleAndNeverOverlapsIt(
            [ValueSource(nameof(PhoneScreens))] ShellScreen screen, [ValueSource(nameof(TextScales))] int percent)
        {
            yield return Open(screen, percent);
            var shell = rig.Shell;
            shell.SetEmptyHint(true);
            yield return ShellRig.Settle();
            Assert.That(shell.TryGetUnobstructedScreenRect(screen.Height, out var withoutVein), Is.True);

            var session = RunShellFixtures.NewSession(out var commands, 1, 25);
            shell.BindOreVein(new RunOreVeinSource(session, commands));
            yield return ShellRig.Settle();
            var context = $"{screen} @{percent}%";
            Assert.That(IsDisplayed(shell.OreVein.Root), Is.True, context + ": the vein is shown");
            Assert.That(shell.TryGetUnobstructedScreenRect(screen.Height, out var rect), Is.True);

            var ppp = rig.PixelsPerPoint;
            var vein = shell.OreVein.Root.worldBound;
            var veinPixels = Rect.MinMaxRect(vein.xMin * ppp, screen.Height - vein.yMax * ppp, vein.xMax * ppp, screen.Height - vein.yMin * ppp);
            Assert.That(rect.Overlaps(veinPixels), Is.False, context + ": the rectangle excludes the vein");
            Assert.That(rect.xMin, Is.EqualTo(withoutVein.xMin).Within(0.5f), context + ": the left edge is unchanged");
            Assert.That(rect.yMax, Is.EqualTo(withoutVein.yMax).Within(0.5f), context + ": the top edge is unchanged");

            // The cut taken is at least as good as cutting above the vein (the former behaviour).
            var columns = SlotGridLayout.ColumnsFor(screen.Width, screen.Height);
            var aboveEdge = veinPixels.yMax;
            var above = Rect.MinMaxRect(withoutVein.xMin, aboveEdge, withoutVein.xMax, withoutVein.yMax);
            var density = SlotGridLayout.Density;
            var scaleAbove = above.height > 0f ? CameraScaleModel.LargestFittingScale(above, columns, density) : 0;
            var scaleNow = CameraScaleModel.LargestFittingScale(rect, columns, density);
            Assert.That(scaleNow, Is.GreaterThanOrEqualTo(scaleAbove), context + ": the corner-aware rectangle fits at least the scale of the full-width cut");
        }

        // ---------------------------------------------------------------------------------------------------
        // T454: zoom buttons per range (contracts/camera-scale.md rule 4, owner decision B1)
        // ---------------------------------------------------------------------------------------------------

        private static void Press(VisualElement control)
        {
            using (var evt = NavigationSubmitEvent.GetPooled())
            {
                evt.target = control;
                control.SendEvent(evt);
            }
        }

        [UnityTest]
        public IEnumerator TheZoomButtonsAreHiddenInASingleLevelRangeAndTheLayoutHoldsWithoutThem(
            [ValueSource(nameof(Screens))] ShellScreen screen, [ValueSource(nameof(TextScales))] int percent)
        {
            yield return Open(screen, percent);
            var shell = rig.Shell;
            shell.SetZoomButtonStates(false, false, false, false);
            yield return ShellRig.Settle();

            Assert.That(IsDisplayed(shell.ZoomInButton), Is.False, $"{screen} @{percent}%: zoom in is hidden");
            Assert.That(IsDisplayed(shell.ZoomOutButton), Is.False, $"{screen} @{percent}%: zoom out is hidden");
            Assert.That(shell.ZoomHintText, Is.Empty);
            AssertShellLayout(rig, "single-level range");
        }

        [UnityTest]
        public IEnumerator TheZoomButtonsAreEnabledInTheMiddleOfARangeAndZoom(
            [ValueSource(nameof(Screens))] ShellScreen screen, [ValueSource(nameof(TextScales))] int percent)
        {
            yield return Open(screen, percent);
            var shell = rig.Shell;
            var steps = new List<int>();
            shell.ZoomRequested += steps.Add;
            shell.SetZoomButtonStates(true, false, true, false);
            yield return ShellRig.Settle();

            foreach (var button in new[] { shell.ZoomInButton, shell.ZoomOutButton })
            {
                Assert.That(IsDisplayed(button), Is.True, $"{screen} @{percent}%: {button.name} shows");
                Assert.That(RunShellDocument.IsSoftDisabled(button), Is.False, button.name);
            }

            Assert.That(shell.ZoomInButton.tooltip, Is.EqualTo(RunShellDocument.ZoomInName), "plain accessible name away from the limit");
            Assert.That(shell.ZoomOutButton.tooltip, Is.EqualTo(RunShellDocument.ZoomOutName), "plain accessible name away from the limit");

            Press(shell.ZoomInButton);
            Press(shell.ZoomOutButton);
            Assert.That(steps, Is.EqualTo(new[] { 1, -1 }));
            Assert.That(shell.ZoomHintText, Is.Empty, "no hint away from the ends");
            AssertShellLayout(rig, "mid range");
        }

        [UnityTest]
        public IEnumerator AZoomButtonAtTheEndOfTheRangeSaysWhyOnTapWithoutShiftingTheLayout(
            [ValueSource(nameof(Screens))] ShellScreen screen, [ValueSource(nameof(TextScales))] int percent)
        {
            yield return Open(screen, percent);
            var shell = rig.Shell;
            var steps = new List<int>();
            shell.ZoomRequested += steps.Add;
            shell.SetZoomButtonStates(true, true, true, false);
            yield return ShellRig.Settle();

            var context = $"{screen} @{percent}%";
            Assert.That(RunShellDocument.IsSoftDisabled(shell.ZoomInButton), Is.True, context);
            Assert.That(shell.ZoomInButton.tooltip, Is.EqualTo("Zoom in, maximum zoom reached"), "the limit state is part of the accessible name and the hover tooltip");
            Assert.That(shell.ZoomOutButton.tooltip, Is.EqualTo("Zoom out"), "the other end keeps its plain name");
            Assert.That(RunShellDocument.IsSoftDisabled(shell.ZoomOutButton), Is.False, context);
            Assert.That(shell.ZoomHintText, Is.Empty, "no permanent caption");
            var columnBefore = shell.ZoomInButton.parent.worldBound;
            var rectBefore = shell.TryGetUnobstructedScreenRect(screen.Height, out var before);
            AssertShellLayout(rig, "range end, before the tap");

            // Reachable by keyboard: the soft-disabled button stays focusable.
            shell.ZoomInButton.Focus();
            yield return ShellRig.Settle();
            Assert.That(shell.ZoomInButton.focusController.focusedElement, Is.SameAs(shell.ZoomInButton), context);

            Press(shell.ZoomInButton);
            yield return ShellRig.Settle();

            Assert.That(steps, Is.Empty, context + ": at the limit the button does not zoom");
            Assert.That(shell.ZoomHintText, Is.EqualTo(RunShellDocument.ZoomMaximumReason), context);
            var hint = shell.ZoomHint.worldBound;
            var safe = rig.Root.panel.visualTree.worldBound;
            AssertWithin(safe, hint, context + ": the hint stays on screen");
            Assert.That(hint.xMin, Is.GreaterThanOrEqualTo(shell.ZoomInButton.worldBound.xMax - 0.5f), context + ": the hint sits beside the column, not over a button");
            Assert.That(shell.ZoomInButton.parent.worldBound, Is.EqualTo(columnBefore), context + ": the hint does not change the zoom column");
            Assert.That(shell.TryGetUnobstructedScreenRect(screen.Height, out var after), Is.EqualTo(rectBefore));
            Assert.That(after, Is.EqualTo(before), context + ": the camera rectangle does not move");

            // The other end says its own reason; leaving the limit clears the hint.
            shell.SetZoomButtonStates(true, false, true, true);
            Assert.That(shell.ZoomHintText, Is.Empty, context + ": the hint clears when the button leaves its limit");
            Assert.That(shell.ZoomInButton.tooltip, Is.EqualTo("Zoom in"), context + ": the name is plain again away from the limit");
            Assert.That(shell.ZoomOutButton.tooltip, Is.EqualTo("Zoom out, minimum zoom reached"), context);
            Press(shell.ZoomOutButton);
            Assert.That(shell.ZoomHintText, Is.EqualTo(RunShellDocument.ZoomMinimumReason), context);
            Assert.That(steps, Is.Empty, context);
        }

        public static IEnumerable<ShellScreen> ZoomHintScreens() => ShellScreens.All().Where(screen =>
            (screen.Width == 360 && screen.Height == 640) || (screen.Width == 844 && screen.Height == 390));

        [UnityTest]
        public IEnumerator TheZoomHintStaysInsideThePanelForBothLimitsAtTheNarrowestScreens(
            [ValueSource(nameof(ZoomHintScreens))] ShellScreen screen, [ValueSource(nameof(TextScales))] int percent)
        {
            yield return Open(screen, percent);
            var shell = rig.Shell;
            var panel = rig.Root.panel.visualTree.worldBound;
            Assert.That(panel.width, Is.GreaterThan(0f));
            shell.SetZoomButtonStates(true, true, true, true);
            yield return ShellRig.Settle();

            foreach (var button in new[] { shell.ZoomInButton, shell.ZoomOutButton })
            {
                Press(button);
                yield return ShellRig.Settle();
                var context = $"{screen} @{percent}% {button.name}";
                Assert.That(shell.ZoomHintText, Is.Not.Empty, context);
                AssertWithin(panel, shell.ZoomHint.worldBound, context + ": the hint lies inside the panel");
            }
        }
    }
}
#endif
