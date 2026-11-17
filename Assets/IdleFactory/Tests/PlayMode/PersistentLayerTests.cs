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

            foreach (var item in harnesses)
            {
                item.Dispose();
            }

            harnesses.Clear();
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

        // ---------------------------------------------------------------------------------------------------
        // T555 (owner decision 2026-11-17, "keep Cash and Overdrive visible"; U-B1): phone landscape with the card, Details or a sheet open
        // ---------------------------------------------------------------------------------------------------

        public enum Surface
        {
            Card,
            Details,
            Sheet,
            Hub,
        }

        private readonly List<CardHarness> harnesses = new List<CardHarness>();

        private IEnumerator OpenLandscape(ShellScreen screen, int percent, Surface surface, System.Action<CardHarness> result)
        {
            CardHarness made = null;
            yield return CardHarness.Create(
                screen, percent, 1_234_567L, built => made = built, unlockEverything: true, factoryLevel: RunShellFixtures.FactoryLevelCount);
            harnesses.Add(made);
            var shell = made.Shell;
            made.Build(0, "machine.miner");
            shell.SetCash(9_000_000_000_000_000_000L);
            shell.SetStore(9_000_000_000_000_000_000L, 9_000_000_000_000_000_000L);
            shell.SetOverdrive(RunShellFixtures.OverdriveStatusIn(OverdriveStage.CoolingDown));
            yield return ShellRig.Settle();
            switch (surface)
            {
                case Surface.Card:
                    yield return made.OpenCard(0);
                    break;
                case Surface.Details:
                    Assert.That(shell.OpenDetails(0), Is.True);
                    break;
                case Surface.Sheet:
                    RunShellTestNavigation.OpenPanel(shell, ShellTab.Market);
                    break;
                default:
                    shell.Panels.Register(HubHarness.NewHub(made.Session, made.Commands));
                    shell.OpenHub();
                    break;
            }

            yield return ShellRig.Settle();
            yield return ShellRig.Settle();
            result(made);
        }

        private static Rect WorldAreaInPoints(CardHarness h, ShellScreen screen, out Rect pixels)
        {
            Assert.That(h.Shell.TryGetUnobstructedScreenRect(screen.Height, out pixels), Is.True, screen.ToString());
            var ppp = h.Rig.PixelsPerPoint;
            return Rect.MinMaxRect(pixels.xMin / ppp, screen.Height / ppp - pixels.yMax / ppp, pixels.xMax / ppp, screen.Height / ppp - pixels.yMin / ppp);
        }

        [UnityTest]
        public IEnumerator CashTheStoreFillAndOverdriveStayOnScreenWhileTheCardDetailsOrASheetIsOpenOnAPhoneInLandscape(
            [ValueSource(nameof(LandscapePhones))] ShellScreen screen, [Values(100, 200)] int percent, [Values] Surface surface)
        {
            CardHarness h = null;
            yield return OpenLandscape(screen, percent, surface, built => h = built);
            var shell = h.Shell;
            var context = $"{screen} @{percent}% {surface}";
            Assert.That(shell.IsStatusBandShown, Is.True, context + ": the status band shows");
            Assert.That(IsDisplayed(shell.ActionBar), Is.False, context + ": the Office entry and the gear step aside");
            var safe = h.Rig.SafeRect();

            // Cash with the store fill, and the Overdrive state, are on screen and inside the safe area, at least 44 pt.
            foreach (var control in new VisualElement[] { shell.CashButton, shell.StoreButton, shell.OverdriveButton })
            {
                Assert.That(IsDisplayed(control), Is.True, $"{context}: {control.name} is shown");
                AssertWithin(safe, control.worldBound, $"{context}: {control.name} stays in the safe area");
                Assert.That(control.worldBound.height, Is.GreaterThanOrEqualTo(MinimumTarget - Tolerance), $"{context}: {control.name} height");
                Assert.That(control.worldBound.width, Is.GreaterThanOrEqualTo(MinimumTarget - Tolerance), $"{context}: {control.name} width");
            }

            var cash = shell.CashButton.Q<Label>("cash-value");
            var store = shell.StoreButton.Q<Label>("store-value");
            Assert.That(cash.text, Is.Not.Empty, context);
            Assert.That(store.text, Is.EqualTo("FULL"), context);
            Assert.That(IsDisplayed(shell.StoreButton.Q<VisualElement>("store-bar")), Is.True, context + ": the store fill bar");
            Assert.That(shell.OverdriveButton.Q<Label>("overdrive-caption").text, Does.StartWith("Cool"), context + ": the Overdrive state word");
            foreach (var label in new[] { cash, store, shell.OverdriveButton.Q<Label>("overdrive-caption") })
            {
                var natural = label.MeasureTextSize(label.text, 0f, VisualElement.MeasureMode.Undefined, 0f, VisualElement.MeasureMode.Undefined);
                Assert.That(label.contentRect.width, Is.GreaterThanOrEqualTo(natural.x - 1f), $"{context}: {label.name} is not truncated");
            }

            // Nothing covers the framed world (OD-9b C1): the band lies outside the camera rectangle, and the whole factory fits it at fit-all.
            var slotArea = WorldAreaInPoints(h, screen, out var area);
            foreach (var control in new VisualElement[] { shell.TopStrip, shell.CashButton, shell.StoreButton, shell.OverdriveButton })
            {
                Assert.That(Intersects(control.worldBound, slotArea), Is.False, $"{context}: {control.name} {control.worldBound} covers the framed world {slotArea}");
            }

            AssertShellLayout(h.Rig, "status band, " + surface);
            // Fit-all is asserted with the card (the state the camera frames at fit-all or a machine band); a 50 % side sheet frames with OpenSheet
            // at n_min, which was never a fit-all view (pre-T555 the same sheet leaves the same width).
            var host = new GameObject("status-band-camera");
            try
            {
                if (surface != Surface.Card)
                {
                    TestContext.Out.WriteLine($"T555-BAND | {context} | world band {area.width:0}x{area.height:0} px | strip {shell.TopStrip.worldBound.height:0} pt");
                }
                else
                {
                var camera = host.AddComponent<Camera>();
                camera.orthographic = true;
                camera.targetTexture = h.Rig.Texture;
                camera.enabled = false;
                var controller = host.AddComponent<FactoryCameraController>();
                controller.Configure(camera, new RunZoomSettings().ToScaleSettings());
                var layout = LatticeLayoutBuilder.ShippedForColumns(SlotGridLayout.ColumnsFor(screen.Width, screen.Height));
                controller.Fit(RunCameraFraming.Layout(camera, layout, h.Rig.PixelsPerPoint, area));
                var low = camera.WorldToScreenPoint(new Vector3(layout.Bounds.xMin, layout.Bounds.yMin, 0f));
                var high = camera.WorldToScreenPoint(new Vector3(layout.Bounds.xMax, layout.Bounds.yMax, 0f));
                Assert.That(Mathf.Min(low.x, high.x), Is.GreaterThanOrEqualTo(area.xMin - 1.5f), context + ": fit-all, no machine column is clipped (left)");
                Assert.That(Mathf.Max(low.x, high.x), Is.LessThanOrEqualTo(area.xMax + 1.5f), context + ": fit-all (right)");
                Assert.That(Mathf.Min(low.y, high.y), Is.GreaterThanOrEqualTo(area.yMin - 1.5f), context + ": fit-all (bottom)");
                Assert.That(Mathf.Max(low.y, high.y), Is.LessThanOrEqualTo(area.yMax + 1.5f), context + ": fit-all (top)");
                TestContext.Out.WriteLine($"T555-BAND | {context} | world band {area.width:0}x{area.height:0} px | strip {shell.TopStrip.worldBound.height:0} pt");
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(host);
            }

            // Closing brings the columns back: the strip is the left column again and Overdrive is in the action bar.
            switch (surface)
            {
                case Surface.Card: shell.CloseMachineCard(); break;
                case Surface.Details: shell.CloseSheetOrDetails(); shell.CloseMachineCard(); break;
                default: shell.CloseSheet(); break;
            }

            yield return ShellRig.Settle();
            yield return ShellRig.Settle();
            Assert.That(shell.IsStatusBandShown, Is.False, context + ": closed");
            Assert.That(IsDisplayed(shell.ActionBar), Is.True, context);
            Assert.That(shell.OverdriveButton.parent, Is.SameAs(shell.ActionBar), context + ": Overdrive is home again");
            Assert.That(shell.TopStrip.parent.name, Is.EqualTo("run-body"), context + ": the strip is the left column again");
            AssertShellLayout(h.Rig, "closed again");
        }

        [UnityTest]
        public IEnumerator OverdriveInTheBandShowsEveryStateDistinctlyAndTapsOnlyWhenReady([Values(100, 200)] int percent, [Values(Surface.Card, Surface.Sheet)] Surface surface)
        {
            var screen = LandscapePhones().First();
            CardHarness h = null;
            yield return OpenLandscape(screen, percent, surface, built => h = built);
            var shell = h.Shell;
            var taps = 0;
            shell.OverdriveRequested += () => taps++;
            var caption = shell.OverdriveButton.Q<Label>("overdrive-caption");
            var borders = new Dictionary<OverdriveStage, float>();
            foreach (var stage in new[] { OverdriveStage.Locked, OverdriveStage.CoolingDown, OverdriveStage.Active, OverdriveStage.Ready })
            {
                shell.SetOverdrive(stage == OverdriveStage.Locked ? null : RunShellFixtures.OverdriveStatusIn(stage));
                yield return ShellRig.Settle();
                Assert.That(shell.IsStatusBandShown, Is.True);
                Assert.That(IsDisplayed(shell.OverdriveButton), Is.True, $"{stage}: shown in the band");
                borders[stage] = shell.OverdriveButton.resolvedStyle.borderTopWidth;
                var before = taps;
                Press(shell.OverdriveButton);
                var expected = stage == OverdriveStage.Ready ? before + 1 : before;
                Assert.That(taps, Is.EqualTo(expected), $"{stage} ({caption.text}): a tap {(stage == OverdriveStage.Ready ? "activates Overdrive" : "does nothing")}");
                Assert.That(RunShellDocument.IsSoftDisabled(shell.OverdriveButton), Is.EqualTo(stage != OverdriveStage.Ready), stage.ToString());
            }

            Assert.That(shell.OverdriveButton.ClassListContains("run-overdrive--ready"), Is.True, "ready has its own look");
            Assert.That(borders[OverdriveStage.Ready], Is.GreaterThan(borders[OverdriveStage.Locked]), "the ready border is the thicker one (a shape, not colour alone)");
            Assert.That(caption.text, Is.EqualTo("Ready"));
        }

        // T555 (U-B6): Details opens scrolled to the top, with the machine name and the status line in view, also when focus moves into it and a prior
        // offset was left behind by the machine the player looked at before.
        public static IEnumerable<ShellScreen> PhoneScreens() => ShellScreens.Phones();

        [UnityTest]
        public IEnumerator DetailsOpensAtItsTopWithTheMachineNameAndStatusInView(
            [ValueSource(nameof(PhoneScreens))] ShellScreen screen, [Values(100, 200)] int percent)
        {
            CardHarness h = null;
            yield return CardHarness.Create(
                screen, percent, 1_234_567L, built => h = built, unlockEverything: true, factoryLevel: RunShellFixtures.FactoryLevelCount);
            harnesses.Add(h);
            var shell = h.Shell;
            h.Build(0, "machine.miner");
            h.Build(1, "machine.miner");
            foreach (var slot in new[] { 0, 1 })
            {
                yield return h.OpenCard(slot);
                Assert.That(shell.OpenDetails(slot), Is.True);
                yield return ShellRig.Settle();
                yield return ShellRig.Settle();
                var context = $"{screen} @{percent}% Details of slot {slot}";
                Assert.That(shell.PanelScroll.scrollOffset.y, Is.EqualTo(0f).Within(0.5f), context + ": scrolled to the top");
                var viewport = shell.PanelScroll.contentViewport.worldBound;
                foreach (var name in new[] { "machine-name", "machine-status" })
                {
                    var label = h.Details.Root.Q<Label>(name);
                    Assert.That(label.text, Is.Not.Empty, context + ": " + name);
                    AssertWithin(viewport, label.worldBound, $"{context}: {name} is in view");
                }

                Assert.That(shell.Root.Q<Label>("panel-title").text, Does.StartWith("Details"), context + ": the heading");

                // Leave the offset at the bottom; the next Details must not inherit it.
                shell.PanelScroll.scrollOffset = new Vector2(0f, 100000f);
                yield return ShellRig.Settle();
                shell.CloseSheetOrDetails();
                yield return ShellRig.Settle();
                shell.CloseMachineCard();
                yield return ShellRig.Settle();
            }
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
        // OD-9b C1 on a phone in landscape with the real default touch player (zoom preference off) and with it on
        // ---------------------------------------------------------------------------------------------------

        public static IEnumerable<ShellScreen> LandscapePhones() => ShellScreens.All().Where(item => item.Expected == FactoryHudLayout.CompactLandscape);

        [UnityTest]
        public IEnumerator TheWholeFactoryFitsTheWorldBandOnAPhoneInLandscapeWithAndWithoutTheZoomButtons(
            [ValueSource(nameof(LandscapePhones))] ShellScreen screen, [ValueSource(nameof(Percents))] int percent, [Values(false, true)] bool zoomPreference)
        {
            yield return Open(screen, percent, ShellDeviceProfile.Touch(screen.Width, screen.Height, 0f));
            var shell = rig.Shell;
            shell.ShowZoomButtonsPreference = zoomPreference;
            shell.SetZoomButtonStates(true, false, true, false);
            yield return FillWorstCase(shell);
            var context = $"{screen} @{percent}% zoom preference {(zoomPreference ? "on" : "off")}";
            Assert.That(shell.TryGetUnobstructedScreenRect(screen.Height, out var area), Is.True, context);
            var host = new GameObject("persistent-layer-camera");
            try
            {
                var camera = host.AddComponent<Camera>();
                camera.orthographic = true;
                camera.targetTexture = rig.Texture;
                camera.enabled = false;
                var controller = host.AddComponent<FactoryCameraController>();
                controller.Configure(camera, new RunZoomSettings().ToScaleSettings());
                var layout = LatticeLayoutBuilder.Shipped(LatticeLayoutBuilder.OrientationFor(screen.Width, screen.Height));
                controller.Fit(RunCameraFraming.Layout(camera, layout, rig.PixelsPerPoint, area));
                var low = camera.WorldToScreenPoint(new Vector3(layout.Bounds.xMin, layout.Bounds.yMin, 0f));
                var high = camera.WorldToScreenPoint(new Vector3(layout.Bounds.xMax, layout.Bounds.yMax, 0f));
                Assert.That(Mathf.Min(low.x, high.x), Is.GreaterThanOrEqualTo(area.xMin - 1.5f), context + ": no machine column is clipped (left)");
                Assert.That(Mathf.Max(low.x, high.x), Is.LessThanOrEqualTo(area.xMax + 1.5f), context + ": no machine column is clipped (right)");
                Assert.That(Mathf.Min(low.y, high.y), Is.GreaterThanOrEqualTo(area.yMin - 1.5f), context + ": bottom");
                Assert.That(Mathf.Max(low.y, high.y), Is.LessThanOrEqualTo(area.yMax + 1.5f), context + ": top");
                TestContext.Out.WriteLine($"C1 | {context} | world band {area.width:0}x{area.height:0} px | lattice at fit-all {Mathf.Abs(high.x - low.x):0}x{Mathf.Abs(high.y - low.y):0} px");
            }
            finally
            {
                Object.DestroyImmediate(host);
            }
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
