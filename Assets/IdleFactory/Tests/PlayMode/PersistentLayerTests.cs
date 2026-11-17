using System.Collections;
using System.Collections.Generic;
using System.Linq;
using IdleFactory.Run.Application;
using IdleFactory.Unity.Presentation;
using IdleFactory.Unity.UI;
using IdleFactory.Unity.UI.Panels;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;
using static IdleFactory.Tests.PlayMode.RunShellLayoutAssertions;

#if UNITY_EDITOR
namespace IdleFactory.Tests.PlayMode
{
    /// <summary>
    /// T537 (FR-007 to FR-010, FR-008a, contracts/ui-surfaces-f006.md section 1): the persistent layer at every screen class and text scale. The contents
    /// are present, nothing clips, every target is at least 44 pt, the hub entry names itself in a visible word that never truncates, and the layer never
    /// covers the world the camera frames. The zoom matrix proves the zoom buttons: pointer profiles show them, touch profiles only with the preference.
    /// </summary>
    public sealed class PersistentLayerTests
    {
        private readonly List<ShellRig> rigs = new List<ShellRig>();
        private ShellRig rig;

        public static IEnumerable<ShellScreen> Screens() => ShellScreens.All();

        public static IEnumerable<int> Percents() => new[] { 100, 150, 200 };

        [TearDown]
        public void TearDown()
        {
            foreach (var item in rigs)
            {
                item.Dispose();
            }

            rigs.Clear();
            rig = null;
        }

        private IEnumerator Open(ShellScreen screen, int percent, ShellDeviceProfile? profile = null)
        {
            rig = ShellRig.Create(screen, profile, percent);
            rigs.Add(rig);
            yield return rig.Layout();
        }

        private static void Press(VisualElement control)
        {
            using (var evt = NavigationSubmitEvent.GetPooled())
            {
                evt.target = control;
                control.SendEvent(evt);
            }
        }

        private IEnumerator FillWorstCase(RunShellDocument shell)
        {
            shell.SetCash(9_000_000_000_000_000_000L);
            shell.SetStore(9_000_000_000_000_000_000L, 9_000_000_000_000_000_000L);
            shell.SetOverdrive(RunShellFixtures.OverdriveStatusIn(OverdriveStage.CoolingDown));
            shell.SetProductionAttention(new AttentionStrip(AttentionKind.Shortage, "resource.iron-ore", 12, 2, new BuildingLocationId(3)), "Short on Iron Ore", "F003-UI-ICON-WARNING");
            shell.Attention.Set(HubCard.Research, true);
            var session = RunShellFixtures.NewSession(out var commands, 1, 25);
            shell.BindOreVein(new RunOreVeinSource(session, commands));
            yield return ShellRig.Settle();
        }

        // ---------------------------------------------------------------------------------------------------
        // Contents, clipping, targets, label
        // ---------------------------------------------------------------------------------------------------

        [UnityTest]
        public IEnumerator TheLayerHasItsContentsClipsNothingAndKeepsTheTargetsAndTheLabel(
            [ValueSource(nameof(Screens))] ShellScreen screen, [ValueSource(nameof(Percents))] int percent)
        {
            yield return Open(screen, percent);
            var shell = rig.Shell;
            yield return FillWorstCase(shell);
            var context = $"{screen} @{percent}%";

            // Contents (FR-008): Cash, the store fill, the hub entry, production attention, manual mining, Overdrive, zoom (the rig turns the preference on) and the gear.
            foreach (var control in new VisualElement[] { shell.CashButton, shell.StoreButton, shell.HubEntryButton, shell.AttentionButton, shell.OverdriveButton, shell.OreVein.Root, shell.SettingsButton, shell.ZoomInButton })
            {
                Assert.That(IsDisplayed(control), Is.True, $"{context}: {control.name} is shown");
                AssertWithin(rig.SafeRect(), control.worldBound, $"{context}: {control.name} stays inside the safe area");
            }

            Assert.That(shell.Root.Q<VisualElement>("rp"), Is.Null, context + ": the RP cell is gone");
            Assert.That(shell.Root.Q<VisualElement>("tab-bar"), Is.Null, context + ": the six-tab bar is gone");

            // No overlap, no clipped text, every target at least 44 pt, text at the scaled minimum (the shared layout assertions).
            AssertShellLayout(rig, "persistent layer, worst case");

            // Every persistent control is at least 44 x 44 points.
            foreach (var control in shell.Root.Query<Button>(className: "run-control").ToList().Where(item => IsDisplayed(item) && !shell.PanelHost.Contains(item)))
            {
                Assert.That(control.worldBound.width, Is.GreaterThanOrEqualTo(MinimumTarget - Tolerance), $"{context}: {control.name} width");
                Assert.That(control.worldBound.height, Is.GreaterThanOrEqualTo(MinimumTarget - Tolerance), $"{context}: {control.name} height");
            }

            // The hub entry: icon plus a visible word from world-copy.md, never "Systems", with an accessible name and a 44 pt (48 dp) target.
            var label = shell.HubEntryButton.Q<Label>("hub-entry-label");
            Assert.That(label.text, Is.EqualTo(HubCards.HubLabel), context + ": the visible word");
            Assert.That(label.text, Is.Not.EqualTo("Systems"));
            Assert.That(shell.HubEntryText, Is.EqualTo("Office"));
            Assert.That(IsDisplayed(label), Is.True, context);
            var natural = label.MeasureTextSize(label.text, 0f, VisualElement.MeasureMode.Undefined, 0f, VisualElement.MeasureMode.Undefined);
            Assert.That(label.contentRect.width, Is.GreaterThanOrEqualTo(natural.x - 1f), $"{context}: the label is not truncated ({label.contentRect.width:0.#} < {natural.x:0.#})");
            Assert.That(label.resolvedStyle.whiteSpace, Is.EqualTo(WhiteSpace.NoWrap), context + ": one word on one line");
            Assert.That(label.resolvedStyle.fontSize, Is.GreaterThanOrEqualTo(12f * percent / 100f - 0.01f), context + ": readable at the text scale");
            Assert.That(shell.HubEntryButton.worldBound.height, Is.GreaterThanOrEqualTo(48f - Tolerance), context + ": the entry is 48 dp");
            Assert.That(shell.HubEntryButton.tooltip, Does.StartWith("Office"), context + ": accessible name");
            Assert.That(shell.IsHubDotShown, Is.True, context + ": the system-attention dot");
            AssertWithin(shell.HubEntryButton.worldBound, label.worldBound, context + ": the word stays inside the entry");
            var icon = shell.HubEntryButton.Q<VisualElement>("hub-entry-icon");
            Assert.That(Intersects(icon.worldBound, label.worldBound), Is.False, context + ": icon and word do not overlap");

            // The production attention indicator: the count and "+N" are text, the accessible name says the cause.
            Assert.That(shell.ProductionAttentionCountText, Is.EqualTo("12"), context);
            Assert.That(shell.ProductionAttentionMoreText, Is.EqualTo("+2"), context);
            Assert.That(shell.AttentionButton.tooltip, Does.StartWith("Short on Iron Ore, 12 machines"), context);

            // Overdrive is compact: the state word and the icon, not a wide block.
            Assert.That(shell.OverdriveButton.tooltip, Does.StartWith("Overdrive "), context + ": the name is in the accessible name");
        }

        [UnityTest]
        public IEnumerator TheReadyOverdriveLooksDistinctFromTheOthers([ValueSource(nameof(Screens))] ShellScreen screen)
        {
            yield return Open(screen, 100);
            var shell = rig.Shell;
            shell.SetOverdrive(RunShellFixtures.OverdriveStatusIn(OverdriveStage.Ready));
            yield return ShellRig.Settle();
            Assert.That(shell.OverdriveButton.ClassListContains("run-overdrive--ready"), Is.True, $"{screen}: the ready class");
            Assert.That(RunShellDocument.IsSoftDisabled(shell.OverdriveButton), Is.False);
            var ready = shell.OverdriveButton.resolvedStyle.borderTopWidth;
            Assert.That(shell.OverdriveButton.Q<Label>("overdrive-caption").text, Is.EqualTo("Ready"));

            foreach (var stage in new[] { OverdriveStage.Locked, OverdriveStage.CoolingDown, OverdriveStage.Active })
            {
                shell.SetOverdrive(stage == OverdriveStage.Locked ? null : RunShellFixtures.OverdriveStatusIn(stage));
                yield return ShellRig.Settle();
                Assert.That(shell.OverdriveButton.ClassListContains("run-overdrive--ready"), Is.False, $"{screen}: {stage} is not the ready look");
                Assert.That(shell.OverdriveButton.resolvedStyle.borderTopWidth, Is.LessThan(ready), $"{screen}: the ready border is the thicker one (a shape, not colour alone)");
            }
        }

        // ---------------------------------------------------------------------------------------------------
        // The layer never covers the framed world; focus returns to the entry
        // ---------------------------------------------------------------------------------------------------

        [UnityTest]
        public IEnumerator NoPersistentControlCoversTheWorldTheCameraFrames(
            [ValueSource(nameof(Screens))] ShellScreen screen, [ValueSource(nameof(Percents))] int percent)
        {
            yield return Open(screen, percent);
            var shell = rig.Shell;
            yield return FillWorstCase(shell);
            Assert.That(shell.TryGetUnobstructedScreenRect(screen.Height, out var area), Is.True, $"{screen} @{percent}%: the camera rectangle resolves");
            var ppp = rig.PixelsPerPoint;
            var slotArea = Rect.MinMaxRect(area.xMin / ppp, screen.Height / ppp - area.yMax / ppp, area.xMax / ppp, screen.Height / ppp - area.yMin / ppp);
            Assert.That(slotArea.width, Is.GreaterThan(0f));
            Assert.That(slotArea.height, Is.GreaterThan(0f));
            foreach (var control in new VisualElement[] { shell.HubEntryButton, shell.OverdriveButton, shell.SettingsButton, shell.CashButton, shell.StoreButton, shell.AttentionButton, shell.TopStrip, shell.ActionBar })
            {
                if (!IsDisplayed(control))
                {
                    continue;
                }

                Assert.That(Intersects(control.worldBound, slotArea), Is.False,
                    $"{screen} @{percent}%: {control.name} {control.worldBound} covers the framed world {slotArea} (U-1, C1)");
            }
        }

        [UnityTest]
        public IEnumerator FocusReturnsToTheEntryAfterAHubLaunchedViewCloses([ValueSource(nameof(Screens))] ShellScreen screen, [ValueSource(nameof(Percents))] int percent)
        {
            yield return Open(screen, percent);
            var shell = rig.Shell;
            var session = RunShellFixtures.NewSession(out var commands, 1, 25);
            shell.Panels.Register(HubHarness.NewHub(session, commands));
            shell.Panels.Register(new TestShellPanel(ShellPanelIds.Market, "Market", 3, false));
            yield return ShellRig.Settle();

            shell.HubEntryButton.Focus();
            Press(shell.HubEntryButton);
            yield return ShellRig.Settle();
            Assert.That(shell.IsHubOpen, Is.True, $"{screen} @{percent}%");
            Press(shell.Root.Q<Button>("hub-card-market"));
            yield return ShellRig.Settle();
            Assert.That(shell.ActivePanelId, Is.EqualTo(ShellPanelIds.Market));
            (shell.PanelContent.Q<Button>("test-panel-button-0") ?? shell.CloseButton).Focus();

            Assert.That(shell.HandleBack(), Is.True);
            yield return ShellRig.Settle();
            yield return ShellRig.Settle();
            Assert.That(shell.IsSheetOpen, Is.False);
            Assert.That(shell.HubEntryButton.focusController.focusedElement, Is.SameAs(shell.HubEntryButton), $"{screen} @{percent}%: focus is back on the Office entry");
        }

        [UnityTest]
        public IEnumerator ThePhoneLandscapeColumnsStepAsideWhileASheetIsOpenAndComeBack()
        {
            var screen = ShellScreens.All().First(item => item.Name.StartsWith("phone-landscape 640", System.StringComparison.Ordinal));
            yield return Open(screen, 200);
            var shell = rig.Shell;
            shell.Panels.Register(new TestShellPanel(ShellPanelIds.Market, "Market", 3, false));
            Assert.That(IsDisplayed(shell.TopStrip), Is.True);
            Assert.That(IsDisplayed(shell.ActionBar), Is.True);

            shell.SelectTab(ShellTab.Market);
            yield return ShellRig.Settle();
            Assert.That(IsDisplayed(shell.TopStrip), Is.False, "the left column steps aside for the sheet");
            Assert.That(IsDisplayed(shell.ActionBar), Is.False, "the right column steps aside for the sheet");
            AssertShellLayout(rig, "sheet open");

            shell.CloseSheet();
            yield return ShellRig.Settle();
            Assert.That(IsDisplayed(shell.TopStrip), Is.True);
            Assert.That(IsDisplayed(shell.ActionBar), Is.True);
            AssertShellLayout(rig, "sheet closed again");
        }

        // ---------------------------------------------------------------------------------------------------
        // Production attention taps
        // ---------------------------------------------------------------------------------------------------

        [UnityTest]
        public IEnumerator TheProductionAttentionIndicatorSelectsTheFirstAffectedLocationAndHidesWithoutAnyCause()
        {
            yield return Open(ShellScreens.All().First(), 100);
            var shell = rig.Shell;
            Assert.That(shell.IsProductionAttentionShown, Is.False, "no cause, no indicator");
            Assert.That(shell.ProductionAttentionSlot, Is.EqualTo(-1));

            var selected = new List<int>();
            shell.LocationActivated += selected.Add;
            shell.SetProductionAttention(new AttentionStrip(AttentionKind.Shortage, "resource.iron-ore", 1, 0, new BuildingLocationId(4)), "Short on Iron Ore", "F003-UI-ICON-WARNING");
            yield return ShellRig.Settle();
            Assert.That(shell.IsProductionAttentionShown, Is.True);
            Assert.That(shell.ProductionAttentionMoreText, Is.Empty, "no further cause, no +N");
            Assert.That(shell.AttentionButton.tooltip, Does.StartWith("Short on Iron Ore, 1 machine."));
            Press(shell.AttentionButton);
            Assert.That(selected, Is.EqualTo(new[] { 4 }), "a tap selects the first affected location");

            shell.SetProductionAttention(default, string.Empty, null);
            yield return ShellRig.Settle();
            Assert.That(shell.IsProductionAttentionShown, Is.False);
            Assert.That(shell.ProductionAttentionSlot, Is.EqualTo(-1));
        }

        // ---------------------------------------------------------------------------------------------------
        // Scaffold notices scroll (T536 UI/UX MAJOR, item 2)
        // ---------------------------------------------------------------------------------------------------

        [UnityTest]
        public IEnumerator ThreeLongNoticesScrollAndEveryDismissButtonIsReachable([ValueSource(nameof(Percents))] int percent)
        {
            var screen = ShellScreens.All().First(item => item.Name.StartsWith("phone-landscape 640", System.StringComparison.Ordinal));
            yield return Open(screen, percent);
            var scaffold = new PanelScaffold(HubHarness.Load<VisualTreeAsset>("PanelScaffold.uxml"), HubHarness.Load<StyleSheet>("PanelScaffold.uss"), "Panel", () => { });
            var host = new VisualElement { name = "scaffold-notice-host" };
            host.style.position = Position.Absolute;
            host.style.left = 0;
            host.style.top = 0;
            host.style.right = 0;
            host.style.bottom = 0;
            host.Add(scaffold.Root);
            rig.Root.Add(host);
            var longText = string.Concat(Enumerable.Repeat("The saved factory could not be written to this device, so nothing was kept. ", 4));
            scaffold.ShowNotice(longText, ScaffoldNoticeKind.Error);
            scaffold.ShowNotice(longText, ScaffoldNoticeKind.Warning);
            scaffold.ShowNotice(longText);
            yield return ShellRig.Settle();

            var stage = scaffold.Root.Q<VisualElement>("scaffold-stage").worldBound;
            Assert.That(scaffold.NoticeLayer.worldBound.height, Is.LessThanOrEqualTo(stage.height * 0.7f + Tolerance), "the layer keeps its 70 % cap");
            var viewport = scaffold.NoticeScroll.contentViewport.worldBound;
            foreach (var notice in scaffold.Notices.ToList())
            {
                var dismiss = notice.Q<Button>("scaffold-notice-dismiss");
                scaffold.NoticeScroll.ScrollTo(dismiss);
                yield return ShellRig.Settle();
                AssertWithin(viewport, dismiss.worldBound, $"{percent}%: the dismiss button scrolls into the notice viewport");
                Assert.That(dismiss.worldBound.height, Is.GreaterThanOrEqualTo(MinimumTarget - Tolerance));
            }

            var count = scaffold.Notices.Count;
            Press(scaffold.Notices[0].Q<Button>("scaffold-notice-dismiss"));
            yield return ShellRig.Settle();
            Assert.That(scaffold.Notices.Count, Is.EqualTo(count - 1), "a dismiss removes its notice");
            scaffold.ClearNotices();
            host.RemoveFromHierarchy();
        }

        // ---------------------------------------------------------------------------------------------------
        // Zoom matrix (FR-008a): touch or pointer x preference x screen class
        // ---------------------------------------------------------------------------------------------------

        public static IEnumerable<ShellScreen> ZoomScreens()
        {
            foreach (var prefix in new[] { "phone-portrait 360", "phone-landscape 640", "tablet-landscape", "desktop 1280" })
            {
                yield return ShellScreens.All().First(item => item.Name.StartsWith(prefix, System.StringComparison.Ordinal));
            }
        }

        [UnityTest]
        public IEnumerator ZoomButtonsShowOnPointerProfilesAndOnTouchOnlyWithThePreference(
            [ValueSource(nameof(ZoomScreens))] ShellScreen screen, [Values(false, true)] bool pointer, [Values(false, true)] bool preference)
        {
            var profile = pointer ? ShellDeviceProfile.Pointer(screen.Width, screen.Height, 0f) : ShellDeviceProfile.Touch(screen.Width, screen.Height, 0f);
            yield return Open(screen, 100, profile);
            var shell = rig.Shell;
            shell.ShowZoomButtonsPreference = preference;
            shell.SetZoomButtonStates(true, false, true, false);
            yield return ShellRig.Settle();

            var expected = pointer || preference;
            var context = $"{screen} {(pointer ? "pointer" : "touch")} preference {(preference ? "on" : "off")}";
            Assert.That(shell.ZoomButtonsAllowed, Is.EqualTo(expected), context);
            Assert.That(IsDisplayed(shell.ZoomInButton), Is.EqualTo(expected), context + ": zoom in");
            Assert.That(IsDisplayed(shell.ZoomOutButton), Is.EqualTo(expected), context + ": zoom out");
            Assert.That(IsDisplayed(shell.Root.Q<VisualElement>("zoom-column")), Is.EqualTo(expected), context + ": the column takes no room while hidden");
            if (!expected)
            {
                Assert.That(shell.GetFocusOrder().Select(item => item.name), Does.Not.Contain("zoom-in").And.Not.Contain("zoom-out"), context + ": no hidden control in the focus order");
            }

            AssertShellLayout(rig, context);

            // The preference takes effect at once, both ways, and a single-level range keeps the buttons hidden whatever the preference says.
            shell.ShowZoomButtonsPreference = !preference;
            yield return ShellRig.Settle();
            Assert.That(IsDisplayed(shell.ZoomInButton), Is.EqualTo(pointer || !preference), context + ": after the preference flipped");
            shell.SetZoomButtonStates(false, false, false, false);
            shell.ShowZoomButtonsPreference = true;
            yield return ShellRig.Settle();
            Assert.That(IsDisplayed(shell.ZoomInButton), Is.False, context + ": a single-level range hides them");
        }
    }
}
#endif
