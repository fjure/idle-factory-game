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
            var framing = new RunCameraFraming(camera, controller, zoom);
            var columns = SlotGridLayout.ColumnsFor(screen.Width, screen.Height);
            framing.ConfigureLimits(columns, 1f);

            Assert.That(rig.Shell.TryGetUnobstructedScreenRect(screen.Height, out var area), Is.True);
            var ppc = framing.FitAll(area, columns, 1f);
            Assert.That(ppc, Is.GreaterThanOrEqualTo(zoom.fitAllFloorPixelsPerCell), $"{screen}: fit-all is at least {zoom.fitAllFloorPixelsPerCell} ppc (Art Bible 8.2)");
            Assert.That(RunCameraFraming.PixelsPerCell(camera, 1f), Is.EqualTo(ppc).Within(0.05f));

            for (var slot = 0; slot < SlotGridLayout.SlotCount; slot++)
            {
                var (low, high) = ScreenRectOf(camera, SlotGridLayout.SlotRect(slot, columns));
                Assert.That(low.x, Is.GreaterThanOrEqualTo(area.xMin - 1f), $"{screen}: slot {slot + 1} left of the world area");
                Assert.That(low.y, Is.GreaterThanOrEqualTo(area.yMin - 1f), $"{screen}: slot {slot + 1} below the world area");
                Assert.That(high.x, Is.LessThanOrEqualTo(area.xMax + 1f), $"{screen}: slot {slot + 1} right of the world area");
                Assert.That(high.y, Is.LessThanOrEqualTo(area.yMax + 1f), $"{screen}: slot {slot + 1} above the world area");
                Assert.That(high.x - low.x, Is.GreaterThanOrEqualTo(2f * 24f - 0.5f), $"{screen}: slot {slot + 1} is at least 48 pt wide (Z24)");
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
            var framing = new RunCameraFraming(camera, controller, new RunZoomSettings());
            var columns = SlotGridLayout.ColumnsFor(screen.Width, screen.Height);
            framing.ConfigureLimits(columns, 1f);
            Assert.That(shell.TryGetUnobstructedScreenRect(screen.Height, out var closedArea), Is.True);
            var fitted = framing.FitAll(closedArea, columns, 1f);

            for (var slot = 0; slot < SlotGridLayout.SlotCount; slot++)
            {
                Assert.That(shell.OpenSlot(slot, SlotCondition.Empty), Is.True);
                yield return ShellRig.Settle();
                Assert.That(shell.TryGetUnobstructedScreenRect(screen.Height, out var area), Is.True);
                controller.SetUnobstructedScreenRect(area);
                framing.FrameSlot(slot, columns);

                var (low, high) = ScreenRectOf(camera, SlotGridLayout.SlotRect(slot, columns));
                Assert.That(low.x, Is.GreaterThanOrEqualTo(area.xMin - 1.5f), $"{screen}: slot {slot + 1} left of the world area above the sheet");
                Assert.That(low.y, Is.GreaterThanOrEqualTo(area.yMin - 1.5f), $"{screen}: slot {slot + 1} below the world area above the sheet");
                Assert.That(high.x, Is.LessThanOrEqualTo(area.xMax + 1.5f), $"{screen}: slot {slot + 1} right of the world area above the sheet");
                Assert.That(high.y, Is.LessThanOrEqualTo(area.yMax + 1.5f), $"{screen}: slot {slot + 1} above the world area above the sheet");
                Assert.That(high.x - low.x, Is.GreaterThanOrEqualTo(2f * 24f - 0.5f), $"{screen}: the selected slot stays at least 48 pt wide");
                AssertShellLayout(rig, $"slot {slot + 1} selected");

                shell.CloseSheet();
                yield return ShellRig.Settle();
                Assert.That(shell.TryGetUnobstructedScreenRect(screen.Height, out closedArea), Is.True);
                controller.SetUnobstructedScreenRect(closedArea);
                framing.FitAll(closedArea, columns, 1f);
            }

            Assert.That(fitted, Is.GreaterThanOrEqualTo(24f));
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
            // At the Z24 floor a slot is 48 pt; the world band above (or beside) the sheet must show one with its half-cell margin.
            Assert.That(area.height, Is.GreaterThanOrEqualTo(48f + 12f), $"{screen}: world band height {area.height}");
            Assert.That(area.width, Is.GreaterThanOrEqualTo(48f + 12f), $"{screen}: world band width {area.width}");
        }
    }
}
#endif
