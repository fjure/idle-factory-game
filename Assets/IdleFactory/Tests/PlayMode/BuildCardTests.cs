using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using IdleFactory.Run.Application;
using IdleFactory.Tests.PlayMode.F006;
using IdleFactory.Unity.Presentation;
using IdleFactory.Unity.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;
using static IdleFactory.Tests.PlayMode.RunShellLayoutAssertions;

#if UNITY_EDITOR
using UnityEditor;

namespace IdleFactory.Tests.PlayMode
{
    /// <summary>
    /// T539 (FR-032, FR-033, FR-034, research R13, OD-9b C1): the docked build card. Enough and insufficient Cash, Back cancels, rotation, the preview and its
    /// visibility, the 44 pt side-by-side actions at every screen class and text scale, the empty and error states, and the exclusivity with the machine card.
    /// </summary>
    public sealed class BuildCardTests
    {
        private readonly List<CardHarness> harnesses = new List<CardHarness>();
        private readonly List<GameObject> objects = new List<GameObject>();
        private CardHarness h;
        private BuildCard card;
        private RecordingPreview preview;

        public static IEnumerable<ShellScreen> Screens() => ShellScreens.All();

        public static IEnumerable<int> Percents() => new[] { 100, 200 };

        [TearDown]
        public void TearDown()
        {
            foreach (var item in harnesses)
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
            objects.Clear();
            h = null;
            card = null;
            preview = null;
        }

        private static ShellScreen PhoneScreen() => ShellScreens.All().First(screen => screen.Name.StartsWith("phone-portrait 390", StringComparison.Ordinal));

        private IEnumerator Make(ShellScreen screen, int percent, long cash, bool unlockEverything = false, bool withEffects = false, bool reduced = false,
            int factoryLevel = 1, ShellDeviceProfile? deviceProfile = null)
        {
            CardHarness made = null;
            yield return CardHarness.Create(screen, percent, cash, built => made = built, reduced, withEffects, factoryLevel, unlockEverything, deviceProfile);
            harnesses.Add(made);
            h = made;
            preview = new RecordingPreview();
            card = h.AttachBuildCard(preview);
        }

        private IEnumerator OpenBuild(int slot)
        {
            Assert.That(h.Shell.OpenSlot(slot, SlotCondition.Empty), Is.True, "an empty location opens a card");
            yield return ShellRig.Settle();
            h.Shell.RefreshCard();
        }

        private int MachineCount() => RunSnapshots.Factory(h.Session).Machines.Count;

        private static void Press(VisualElement control) => CardHarness.Press(control);

        // ---------------------------------------------------------------------------------------------------
        // Opening, exclusivity, listing
        // ---------------------------------------------------------------------------------------------------

        [UnityTest]
        public IEnumerator AnEmptyLocationOpensTheBuildCardAndItIsExclusiveWithTheMachineCard()
        {
            yield return Make(PhoneScreen(), 100, RunShellFixtures.ComfortableCash);
            var shell = h.Shell;
            h.Build(1, "machine.miner");

            yield return OpenBuild(0);
            Assert.That(shell.IsBuildCardOpen, Is.True);
            Assert.That(shell.BuildCardSlot, Is.EqualTo(0));
            Assert.That(shell.IsCardOpen, Is.False, "the machine card is not the open frame");
            Assert.That(shell.IsSheetOpen, Is.False, "the build card replaces the build sheet");
            Assert.That(shell.ActivePanelId, Is.Null);
            Assert.That(shell.SelectedSlot, Is.EqualTo(0));
            Assert.That(card.Root.parent, Is.Not.Null, "the build card is mounted");
            Assert.That(shell.CardRoot.parent, Is.Null, "the machine card is not mounted while the build card is");
            Assert.That(IsDisplayed(card.Root), Is.True);

            // A tap on a machine hands the dock to the machine card and clears the preview.
            Assert.That(shell.OpenSlot(1, SlotCondition.Occupied), Is.True);
            yield return ShellRig.Settle();
            Assert.That(shell.IsCardOpen, Is.True);
            Assert.That(shell.IsBuildCardOpen, Is.False);
            Assert.That(card.Root.parent, Is.Null, "the build card is gone");
            Assert.That(shell.CardRoot.parent, Is.Not.Null);
            Assert.That(preview.PreviewSlot, Is.EqualTo(-1), "the preview is cleared");
            Assert.That(preview.Available, Is.Empty, "and so is the highlight");

            // And back: an empty location takes the dock again.
            yield return OpenBuild(0);
            Assert.That(shell.IsBuildCardOpen, Is.True);
            Assert.That(shell.CardRoot.parent, Is.Null);
            Assert.That(preview.PreviewSlot, Is.EqualTo(0));
        }

        [UnityTest]
        public IEnumerator EveryTypeShowsItsIconCostAndAffordabilityAndALockedTypeCannotBeSelected()
        {
            // Level 1 without unlocks: the Miner is unlocked, the others are locked behind a requirement.
            yield return Make(PhoneScreen(), 100, RunShellFixtures.MinerCost);
            yield return OpenBuild(0);

            var snapshot = RunSnapshots.Factory(h.Session);
            Assert.That(card.TypeButtons.Count, Is.EqualTo(snapshot.MachineTypes.Count), "one row per machine type");
            var miner = card.TypeButton("machine.miner");
            Assert.That(miner.Q<Label>(className: "bcard__name").text, Is.EqualTo("Miner"));
            Assert.That(miner.Q<VisualElement>(className: "bcard__icon"), Is.Not.Null, "every row has its icon element");
            Assert.That(miner.Q<Label>("build-card-cost-" + card.TypeButtons.ToList().IndexOf(miner)).text, Is.EqualTo("Cost: $" + RunShellFixtures.MinerCost));
            Assert.That(miner.Q<Label>("build-card-status-" + card.TypeButtons.ToList().IndexOf(miner)).text, Is.EqualTo("Affordable"));
            Assert.That(miner.tooltip, Does.Contain("Miner").And.Contain("Affordable").And.Contain("selected"), "the accessible name carries name, price, affordability and selection");
            Assert.That(card.SelectedMachineId, Is.EqualTo("machine.miner"));

            var locked = snapshot.MachineTypes.First(type => !type.Unlocked);
            var lockedButton = card.TypeButton(locked.MachineId);
            Assert.That(RunShellDocument.IsSoftDisabled(lockedButton), Is.True, "a locked type is disabled");
            Press(lockedButton);
            Assert.That(card.SelectedMachineId, Is.EqualTo("machine.miner"), "a locked type is not selected");
            Assert.That(card.FeedbackLabel.style.display.value, Is.EqualTo(DisplayStyle.Flex), "its reason shows as text, not only as a tooltip");
            Assert.That(card.FeedbackLabel.text, Is.EqualTo(SlotSheetText.UnlockReason(h.Session, locked)));
        }

        [UnityTest]
        public IEnumerator SelectingAnotherTypeMovesTheSelectedLineAndThePreview()
        {
            yield return Make(PhoneScreen(), 100, RunShellFixtures.ComfortableCash * 100_000L, unlockEverything: true, factoryLevel: RunShellFixtures.FactoryLevelCount);
            yield return OpenBuild(2);
            var first = RunSnapshots.Factory(h.Session).MachineTypes.First(type => type.Unlocked && type.Affordable);
            Assert.That(card.SelectedMachineId, Is.EqualTo(first.MachineId), "a fresh open selects the first affordable type");
            Assert.That(card.SelectedLabel.text, Is.EqualTo("Selected: " + first.Label + " at location 3"));
            Assert.That(preview.PreviewSlot, Is.EqualTo(2));
            Assert.That(preview.PreviewKey, Is.EqualTo(h.Session.Catalog.Machines[first.MachineId].PresentationKey));

            Assert.That(first.MachineId, Is.Not.EqualTo("machine.smelter"), "the fixture's first type is another one");
            Press(card.TypeButton("machine.smelter"));
            Assert.That(card.SelectedMachineId, Is.EqualTo("machine.smelter"));
            Assert.That(card.SelectedLabel.text, Is.EqualTo("Selected: Smelter at location 3"));
            Assert.That(preview.PreviewKey, Is.EqualTo(h.Session.Catalog.Machines["machine.smelter"].PresentationKey), "the ghost follows the selected type");
            Assert.That(card.TypeButton("machine.smelter").ClassListContains("bcard__type--selected"), Is.True);
            Assert.That(card.TypeButton(first.MachineId).ClassListContains("bcard__type--selected"), Is.False);
            Assert.That(preview.Available, Has.Member(2).And.Member(3), "every free location is outlined");
            Assert.That(preview.Available, Has.No.Member(-1));

            // Retargeting to another free location keeps the selected type.
            Assert.That(h.Shell.OpenSlot(4, SlotCondition.Empty), Is.True);
            yield return ShellRig.Settle();
            Assert.That(card.Slot, Is.EqualTo(4));
            Assert.That(card.SelectedLabel.text, Is.EqualTo("Selected: Smelter at location 5"));
            Assert.That(preview.PreviewSlot, Is.EqualTo(4));
            Assert.That(h.Shell.SelectedSlot, Is.EqualTo(4));
        }

        // ---------------------------------------------------------------------------------------------------
        // Enough Cash, not enough Cash
        // ---------------------------------------------------------------------------------------------------

        [UnityTest]
        public IEnumerator WithEnoughCashConfirmBuildsPlaysTheDustAndHandsOverToTheMachineCard()
        {
            yield return Make(PhoneScreen(), 100, RunShellFixtures.ComfortableCash, withEffects: true);
            yield return OpenBuild(0);
            var cashBefore = h.Session.ReadCash();

            Assert.That(PanelScaffold.IsDisabled(card.ConfirmButton), Is.False, "Confirm is enabled");
            Assert.That(card.ReasonLabel.style.display.value, Is.EqualTo(DisplayStyle.None));
            Assert.That(card.ConfirmButton.tooltip, Does.Contain("Selected: Miner at location 1"));

            Press(card.ConfirmButton);
            yield return ShellRig.Settle();

            Assert.That(MachineCount(), Is.EqualTo(1));
            Assert.That(h.MachineAt(0), Is.Not.Null, "the machine stands at the location");
            Assert.That(h.Session.ReadCash(), Is.EqualTo(cashBefore - RunShellFixtures.MinerCost));
            Assert.That(h.Effects.Spawned, Is.EqualTo(new[] { (MachineResponseKind.BuildDust, 0) }), "the build dust plays at the built location");
            Assert.That(h.Shell.IsBuildCardOpen, Is.False);
            Assert.That(h.Shell.IsCardOpen, Is.True, "the card hands over to the new machine's card");
            Assert.That(h.Shell.CardSlot, Is.EqualTo(0));
            Assert.That(h.Card.Slot, Is.EqualTo(0));
            Assert.That(preview.PreviewSlot, Is.EqualTo(-1), "the ghost is gone");
            Assert.That(preview.Available, Is.Empty);
            Assert.That(h.Shell.SelectedSlot, Is.EqualTo(0), "the new machine stays selected");
        }

        [UnityTest]
        public IEnumerator UnderReducedMotionTheDustIsTheStaticHighlightOnTheNewMachineCard()
        {
            yield return Make(PhoneScreen(), 100, RunShellFixtures.ComfortableCash, withEffects: true, reduced: true);
            yield return OpenBuild(0);
            Press(card.ConfirmButton);
            yield return ShellRig.Settle();

            Assert.That(MachineCount(), Is.EqualTo(1));
            Assert.That(h.Effects.Spawned, Is.Empty, "no effect ever spawns under reduced motion");
            Assert.That(h.Feedback.LastKind, Is.EqualTo(MachineResponseKind.BuildDust));
            Assert.That(h.Feedback.LastWasWorldEffect, Is.False);
            Assert.That(h.Card.IsHighlighted, Is.True, "the static response is the one-second highlight of the new machine's card");
        }

        // C-48 (T560): the build card's not-available states. A location that is taken or locked by the time the card shows it keeps Confirm disabled with the visible
        // reason; the warning and danger text keep 4.5:1 against the card surface.
        [UnityTest]
        public IEnumerator ATakenOrLockedLocationShowsTheNotAvailableStateWithADisabledConfirmAndTheReason()
        {
            yield return Make(PhoneScreen(), 100, RunShellFixtures.ComfortableCash);
            h.Build(1, "machine.miner");
            Assert.That(h.Shell.OpenSlot(1, SlotCondition.Empty), Is.True, "a stale empty condition still opens the card");
            yield return ShellRig.Settle();
            h.Shell.RefreshCard();
            Assert.That(PanelScaffold.IsDisabled(card.ConfirmButton), Is.True, "Confirm is disabled on a taken location");
            Assert.That(card.ReasonLabel.text, Is.EqualTo(BuildCardCopy.LocationTaken));
            Assert.That(card.ReasonLabel.style.display.value, Is.EqualTo(DisplayStyle.Flex));

            Assert.That(h.Shell.OpenSlot(11, SlotCondition.Empty), Is.True);
            yield return ShellRig.Settle();
            h.Shell.RefreshCard();
            Assert.That(PanelScaffold.IsDisabled(card.ConfirmButton), Is.True, "and on a locked one");
            Assert.That(card.ReasonLabel.text, Is.EqualTo(BuildCardCopy.LocationLocked));
        }

        [UnityTest]
        public IEnumerator TheWarningAndDangerTextOfTheBuildCardMeetContrastAgainstTheCardSurface([Values(100, 200)] int percent)
        {
            yield return Make(PhoneScreen(), percent, RunShellFixtures.MinerCost - 1);
            yield return OpenBuild(0);
            var surface = card.Root.resolvedStyle.backgroundColor;
            Assert.That(surface.a, Is.GreaterThan(0f), "the docked card has its own surface");
            Assert.That(PanelScaffold.IsDisabled(card.ConfirmButton), Is.True);
            var warning = card.ReasonLabel.resolvedStyle.color;
            Assert.That(ContrastRatio(warning, surface), Is.GreaterThanOrEqualTo(4.5f), "the warning reason line");
            var danger = card.Root.Q<Label>("build-card-feedback");
            Assert.That(danger, Is.Not.Null);
            Assert.That(ContrastRatio(danger.resolvedStyle.color, surface), Is.GreaterThanOrEqualTo(4.5f), "the danger feedback line");
        }

        private static float ContrastRatio(Color foreground, Color background)
        {
            Func<float, float> channel = value => value <= 0.03928f ? value / 12.92f : Mathf.Pow((value + 0.055f) / 1.055f, 2.4f);
            Func<Color, float> luminance = color => 0.2126f * channel(color.r) + 0.7152f * channel(color.g) + 0.0722f * channel(color.b);
            var a = luminance(foreground);
            var b = luminance(background);
            return (Mathf.Max(a, b) + 0.05f) / (Mathf.Min(a, b) + 0.05f);
        }

        public static IEnumerable<ShellScreen> CompactScreens() =>
            ShellScreens.All().Where(item => item.Name.StartsWith("phone-landscape 640", StringComparison.Ordinal) || item.Name.StartsWith("phone-portrait 360", StringComparison.Ordinal) ||
                                             item.Name.StartsWith("phone-landscape 844", StringComparison.Ordinal));

        // C-59 and C-46 (T560): at 200 % text on the compact classes the actions keep one word per label, the heading stays within two lines and the list either shows
        // two type rows or scrolls the selected row into view.
        [UnityTest]
        public IEnumerator TheBuildCardKeepsItsWordsWholeItsHeadingShortAndTheSelectedRowReachableAtTwoHundredPercent([ValueSource(nameof(CompactScreens))] ShellScreen screen)
        {
            yield return Make(screen, 200, RunShellFixtures.ComfortableCash, unlockEverything: true, factoryLevel: 2);
            yield return OpenBuild(0);
            yield return ShellRig.Settle();
            var where = screen.Name + " @200%";

            foreach (var button in new[] { card.ConfirmButton, card.CancelButton })
            {
                var text = button.text;
                var size = button.MeasureTextSize(text, 0f, VisualElement.MeasureMode.Undefined, 0f, VisualElement.MeasureMode.Undefined);
                var inner = button.worldBound.width - button.resolvedStyle.paddingLeft - button.resolvedStyle.paddingRight;
                Assert.That(size.x, Is.LessThanOrEqualTo(inner + Tolerance), where + ": " + text + " fits on one line without breaking inside the word");
            }

            var heading = card.SelectedLabel;
            // UI/UX N2: the real line height is the measured height of one line of the same text at the same font (never a guessed factor).
            var oneLine = heading.MeasureTextSize("Wg", 0f, VisualElement.MeasureMode.Undefined, 0f, VisualElement.MeasureMode.Undefined).y;
            Assert.That(oneLine, Is.GreaterThan(0f));
            var lines = heading.worldBound.height / oneLine;
            // 640x360 at 200 %: the 40 percent column is 256 pt wide and the caption-size heading still wraps to three lines; it is recorded as the one class exempt from the
            // two-line goal (shell rule: text is never clipped, so no ellipsis). Every other compact class holds two lines.
            var maxLines = screen.Name.StartsWith("phone-landscape 640", StringComparison.Ordinal) ? 3.05f : 2.05f;
            Assert.That(lines, Is.LessThanOrEqualTo(maxLines), where + ": the heading line count (" + lines + ")");

            var visibleRows = 0;
            Button selectedRow = null;
            foreach (var row in card.TypeButtons)
            {
                if (row.ClassListContains("bcard__type--selected"))
                {
                    selectedRow = row;
                }

                var inside = row.worldBound.yMin >= card.Types.worldBound.yMin - Tolerance && row.worldBound.yMax <= card.Types.worldBound.yMax + Tolerance;
                visibleRows += inside && IsDisplayed(row) ? 1 : 0;
            }

            Assert.That(selectedRow, Is.Not.Null, where + ": a type is selected");
            var selectedVisible = selectedRow.worldBound.yMin >= card.Types.worldBound.yMin - Tolerance && selectedRow.worldBound.yMax <= card.Types.worldBound.yMax + Tolerance;
            // The two-row goal is asserted where the class has the room (the wide landscape 844x390); on 360x640 and 640x360 at 200 % the card band is too short for
            // two rows (360x640 shows one: the 45 percent cap), so there the selected row scrolled into view is the contract (C-46).
            var roomy = screen.Name.StartsWith("phone-landscape 844", StringComparison.Ordinal);
            if (roomy)
            {
                Assert.That(visibleRows, Is.GreaterThanOrEqualTo(2), where + ": two type rows show");
            }

            Assert.That(selectedVisible, Is.True, where + ": the selected row is in view (" + visibleRows + " rows show)");
        }

        // B-13 (T560): later USS rules must not undercut the 48 pt touch floor of the scaffold controls on the card.
        [UnityTest]
        public IEnumerator TheBuildCardControlsKeepTheFortyEightPointFloorAtEveryTextScale(
            [ValueSource(nameof(CompactScreens))] ShellScreen screen, [Values(100, 200)] int percent)
        {
            yield return Make(screen, percent, RunShellFixtures.ComfortableCash, unlockEverything: true);
            yield return OpenBuild(0);
            foreach (var button in new[] { card.ConfirmButton, card.CancelButton }.Concat(card.TypeButtons))
            {
                if (!IsDisplayed(button) || button.worldBound.height <= 0f)
                {
                    continue;
                }

                Assert.That(button.worldBound.height, Is.GreaterThanOrEqualTo(48f - Tolerance), button.name + " at " + screen.Name + " " + percent + " %");
            }
        }

        public static IEnumerable<ShellScreen> HandOverScreens() => CompactScreens().Concat(ShellScreens.All().Where(item => item.Name.StartsWith("phone-portrait 390", StringComparison.Ordinal)));

        // The device the T542 sweep ran on: 1080x2400 at 420 dpi in both orientations, with the authored panel settings (2.63 px per point after the touch-target raise).
        private const float DeviceDpi = 420f;

        public static IEnumerable<ShellScreen> DeviceScreens()
        {
            yield return new ShellScreen("device portrait 1080x2400", 1080, 2400, 0, 0, 0, 0, FactoryHudLayout.CompactPortrait);
            yield return new ShellScreen("device landscape 2400x1080", 2400, 1080, 0, 0, 0, 0, FactoryHudLayout.CompactLandscape);
        }

        private static ShellDeviceProfile DeviceProfileOf(ShellScreen screen) => ShellDeviceProfile.Touch(screen.Width, screen.Height, DeviceDpi);

        // T561 (C-60, C-80, D7): the hand-over from the build card to the new machine's card is a pan the camera needs only when the machine card leaves a smaller band
        // than the build card did. T542 measured about 180 px on the device; the world must move by no more than the difference of the two bands (and so never by more
        // than the card band itself). The camera is driven by the same RunViewportDriver the bootstrap uses.
        [UnityTest]
        public IEnumerator TheHandOverToTheMachineCardMovesTheWorldByNoMoreThanTheCardBandsDifference(
            [ValueSource(nameof(HandOverScreens))] ShellScreen screen, [Values(100, 200)] int percent, [Values(0, 11)] int slot) =>
            HandOver(screen, percent, slot, null);

        // The same on the device of the T542 sweep (1080x2400 at 420 dpi, authored panel settings, several zoom levels): the real scale range is where the camera has choices.
        [UnityTest]
        public IEnumerator OnTheDeviceTheHandOverToTheMachineCardMovesTheWorldByNoMoreThanTheCardBandsDifference(
            [ValueSource(nameof(DeviceScreens))] ShellScreen screen, [Values(100, 200)] int percent, [Values(0, 11)] int slot) =>
            HandOver(screen, percent, slot, DeviceProfileOf(screen));

        private IEnumerator HandOver(ShellScreen screen, int percent, int slot, ShellDeviceProfile? profile)
        {
            yield return Make(screen, percent, RunShellFixtures.ComfortableCash, unlockEverything: true, factoryLevel: RunShellFixtures.FactoryLevelCount, deviceProfile: profile);
            var host = new GameObject("handover-camera");
            objects.Add(host);
            host.transform.position = new Vector3(0f, 0f, -10f);
            var camera = host.AddComponent<Camera>();
            camera.orthographic = true;
            camera.targetTexture = h.Rig.Texture;
            camera.enabled = false;
            var controller = host.AddComponent<FactoryCameraController>();
            controller.Configure(camera, new RunZoomSettings().ToScaleSettings());
            var framing = new RunCameraFraming(new RunZoomSettings());
            var driver = new RunViewportDriver(controller);
            var layout = LatticeLayoutBuilder.ShippedForColumns(SlotGridLayout.ColumnsFor(screen.Width, screen.Height));
            var ppp = h.Rig.PixelsPerPoint;
            var context = $"{screen} @{percent}% slot {slot}";

            Assert.That(h.Shell.TryGetUnobstructedScreenRect(screen.Height, out var closedArea), Is.True, context);
            driver.Apply(RunCameraFraming.Layout(camera, layout, ppp, closedArea), closedArea, false, default);

            yield return OpenBuild(slot);
            yield return ShellRig.Settle();
            Assert.That(h.Shell.TryGetUnobstructedScreenRect(screen.Height, out var buildArea), Is.True, context);
            driver.Apply(RunCameraFraming.Layout(camera, layout, ppp, buildArea), buildArea, true, framing.FrameMachine(slot, layout));
            var scaleBefore = controller.Scale;
            var before = camera.WorldToScreenPoint(layout.Bounds.center);

            Press(card.ConfirmButton);
            yield return ShellRig.Settle();
            yield return ShellRig.Settle();
            Assert.That(h.Shell.IsCardOpen && !h.Shell.IsBuildCardOpen, Is.True, context + ": the machine card is open");
            Assert.That(h.Shell.TryGetUnobstructedScreenRect(screen.Height, out var machineArea), Is.True, context);
            driver.Apply(RunCameraFraming.Layout(camera, layout, ppp, machineArea), machineArea, true, framing.FrameMachine(slot, layout));
            var after = camera.WorldToScreenPoint(layout.Bounds.center);

            var moved = Mathf.Abs(after.y - before.y) + Mathf.Abs(after.x - before.x);
            var allowed = Mathf.Max(0f, buildArea.height - machineArea.height);
            TestContext.Out.WriteLine($"T561-HANDOVER | {context} ppp {ppp:0.00} | build band {buildArea.height:0} machine band {machineArea.height:0} | moved {moved:0.0} px | allowed {allowed:0.0} | scale {scaleBefore} -> {controller.Scale} | closed {closedArea} build {buildArea} machine {machineArea}");
            Assert.That(controller.Scale, Is.EqualTo(scaleBefore), context + ": the hand-over never changes the zoom");
            Assert.That(moved, Is.LessThanOrEqualTo(allowed + 1.5f), context + ": the world moves by no more than the shrink of the band");
            var cardBand = closedArea.height - machineArea.height;
            Assert.That(cardBand, Is.GreaterThan(0f), context + ": the machine card takes a band of its own");
            Assert.That(moved, Is.LessThanOrEqualTo(cardBand), context + ": and never by more than the machine card's band");

            // The built machine stays fully inside the band above the card, and the view is inside its pan limits (a pan by nothing is not clamped), so the next gesture does not jump it.
            var low = camera.WorldToScreenPoint(layout.MachineRectOf(new BuildingLocationId(slot)).min);
            var high = camera.WorldToScreenPoint(layout.MachineRectOf(new BuildingLocationId(slot)).max);
            Assert.That(low.x, Is.GreaterThanOrEqualTo(machineArea.xMin - 1.5f), context + ": machine inside the band (left)");
            Assert.That(low.y, Is.GreaterThanOrEqualTo(machineArea.yMin - 1.5f), context + ": machine inside the band (bottom)");
            Assert.That(high.x, Is.LessThanOrEqualTo(machineArea.xMax + 1.5f), context + ": machine inside the band (right)");
            Assert.That(high.y, Is.LessThanOrEqualTo(machineArea.yMax + 1.5f), context + ": machine inside the band (top)");
            Assert.That(controller.Model.Pan(Vector2.zero), Is.False, context + ": the view is inside its pan limits after the hand-over");
        }

        // T561 (C-81, D8): the same 48 floor measured in DEVICE pixels at the applied scale of the device the T542 sweep used (1080x2400 at 420 dpi, 2.63 px per point), at
        // 100 and 200 % text: every build-card control and every machine-card control is at least 48 dp (126 px at 420 dpi) tall, so a screenshot read in dp shows 48.
        [UnityTest]
        public IEnumerator OnTheDeviceEveryCardControlIsAtLeastFortyEightDpInDevicePixels(
            [ValueSource(nameof(DeviceScreens))] ShellScreen screen, [Values(100, 200)] int percent)
        {
            yield return Make(screen, percent, RunShellFixtures.ComfortableCash, unlockEverything: true, factoryLevel: RunShellFixtures.FactoryLevelCount, deviceProfile: DeviceProfileOf(screen));
            var ppp = h.Rig.PixelsPerPoint;
            var floor = 48f * DeviceDpi / 160f - 1f;
            h.Build(1, "machine.miner");
            yield return OpenBuild(0);
            var context = screen.Name + " " + percent + " % (" + ppp.ToString("0.00") + " px per point)";
            var measured = 0;
            foreach (var button in new[] { card.ConfirmButton, card.CancelButton }.Concat(card.TypeButtons))
            {
                if (!IsDisplayed(button) || button.worldBound.height <= 0f)
                {
                    continue;
                }

                measured++;
                Assert.That(button.worldBound.height * ppp, Is.GreaterThanOrEqualTo(floor), "build card " + button.name + " at " + context);
            }

            Assert.That(measured, Is.GreaterThanOrEqualTo(3), "Confirm, Cancel and at least one type row were measured");
            Assert.That(h.Shell.OpenSlot(1, SlotCondition.Occupied), Is.True);
            yield return ShellRig.Settle();
            h.Shell.RefreshCard();
            h.Card.More.ScrollTo(h.Card.DetailsButton);
            yield return ShellRig.Settle();
            foreach (var button in new[] { h.Card.UpgradeButton, h.Card.DetailsButton, h.Card.CloseButton })
            {
                Assert.That(button.worldBound.height * ppp, Is.GreaterThanOrEqualTo(floor), "machine card " + button.name + " at " + context);
            }
        }

        // C-41 (T560, SC-008): production changes the quantity revision every frame, but the card shows only what Cash decides (affordability, the shortfall), so the
        // open card must not rebuild RunSnapshots.Factory then. 600 frames of a running factory with the card open allocate nothing.
        [UnityTest]
        public IEnumerator ARunningFactoryWithTheBuildCardOpenAllocatesNothingOver600Frames()
        {
            yield return Make(PhoneScreen(), 100, RunShellFixtures.ComfortableCash * 4, unlockEverything: true, factoryLevel: 2);
            h.Build(1, "machine.miner");
            h.Build(2, "machine.miner");
            yield return OpenBuild(0);
            for (var warm = 0; warm < 30; warm++)
            {
                h.Session.Advance(100L * RunSession.TicksPerMs);
                card.Refresh();
            }

            var revisionBefore = h.Session.QuantityRevision;
            var allocations = 0;
            for (var frame = 0; frame < 600; frame++)
            {
                h.Session.Advance(100L * RunSession.TicksPerMs);
                allocations += GcAllocationCounter.Count(() => card.Refresh());
            }

            Assert.That(h.Session.QuantityRevision, Is.GreaterThan(revisionBefore), "the quantities changed during the window");
            Assert.That(allocations, Is.Zero, "the open build card rebuilds nothing while only quantities change");
            Assert.That(IsDisplayed(card.Root), Is.True);
        }

        // C-41: the card still follows Cash where it shows it: crossing the price of the selected type enables Confirm without a structural change.
        [UnityTest]
        public IEnumerator TheCardStillRefreshesWhenCashCrossesThePriceOfTheSelectedType()
        {
            yield return Make(PhoneScreen(), 100, RunShellFixtures.MinerCost - 1, withEffects: true);
            yield return OpenBuild(0);
            Assert.That(PanelScaffold.IsDisabled(card.ConfirmButton), Is.True, "one short of the price");
            h.Session.BeginCommand();
            Assert.That(h.Session.State.TryAddCash(1), Is.EqualTo(IdleFactory.Run.Domain.RunStateResult.Ok));
            Assert.That(h.Session.EndCommand(0, false).IsSuccess, Is.True);
            card.Refresh();
            Assert.That(PanelScaffold.IsDisabled(card.ConfirmButton), Is.False, "affordable now, with no structural change");
        }

        [UnityTest]
        public IEnumerator WithoutEnoughCashConfirmIsDisabledAndTheCardSaysHowMuchIsMissing()
        {
            yield return Make(PhoneScreen(), 100, RunShellFixtures.MinerCost - 1, withEffects: true);
            yield return OpenBuild(0);
            var cashBefore = h.Session.ReadCash();

            Assert.That(PanelScaffold.IsDisabled(card.ConfirmButton), Is.True, "Confirm is disabled");
            Assert.That(card.ReasonLabel.text, Is.EqualTo("Need $1 more"), "the line the contract names");
            Assert.That(card.ReasonLabel.style.display.value, Is.EqualTo(DisplayStyle.Flex), "the reason is visible text, not only a tooltip");
            Assert.That(card.ConfirmButton.tooltip, Does.Contain("Need $1 more"), "and it is the accessible description too");
            Assert.That(card.SelectedLabel.text, Is.EqualTo("Selected: Miner at location 1"), "an unaffordable type still previews");
            Assert.That(preview.PreviewSlot, Is.EqualTo(0));
            Assert.That(card.HintLabel.text, Is.EqualTo(BuildCardCopy.NothingAffordable), "the empty state: nothing is affordable");
            Assert.That(card.HintLabel.style.display.value, Is.EqualTo(DisplayStyle.Flex));
            Assert.That(card.TypeButton("machine.miner").Q<Label>(className: "bcard__line", name: "build-card-status-" + card.TypeButtons.ToList().IndexOf(card.TypeButton("machine.miner"))).text, Is.EqualTo(BuildCardCopy.SelectedRow), "C-47: the shortfall shows once as text (the reason line); the selected row only says it is selected");

            // The control stays focusable and pressing it changes nothing.
            Assert.That(card.ConfirmButton.focusable, Is.True);
            Press(card.ConfirmButton);
            yield return ShellRig.Settle();
            Assert.That(MachineCount(), Is.EqualTo(0));
            Assert.That(h.Session.ReadCash(), Is.EqualTo(cashBefore));
            Assert.That(h.Shell.IsBuildCardOpen, Is.True, "the card stays open");
            Assert.That(h.Effects.Spawned, Is.Empty);

            // The reason sits directly below the actions.
            Assert.That(card.ReasonLabel.worldBound.yMin, Is.GreaterThanOrEqualTo(card.ConfirmButton.worldBound.yMax - Tolerance));
        }

        [UnityTest]
        public IEnumerator ARejectedBuildShowsTheReasonInlineAndKeepsTheCardOpen()
        {
            yield return Make(PhoneScreen(), 100, RunShellFixtures.ComfortableCash);
            yield return OpenBuild(0);
            // A build at the location by another path while the card still shows it free: the command rejects, the card says why, nothing is charged.
            h.Build(0, "machine.miner");
            var cashBefore = h.Session.ReadCash();
            Press(card.ConfirmButton);
            yield return ShellRig.Settle();

            Assert.That(h.Session.ReadCash(), Is.EqualTo(cashBefore), "a rejected build charges nothing");
            Assert.That(MachineCount(), Is.EqualTo(1));
            Assert.That(h.Shell.IsBuildCardOpen, Is.True, "the card stays open");
            Assert.That(card.FeedbackLabel.style.display.value, Is.EqualTo(DisplayStyle.Flex), "the error state is text");
            Assert.That(card.FeedbackLabel.text, Is.EqualTo(RunRejectionCopy.For(RunReasons.SlotOccupied)));
            Assert.That(PanelScaffold.IsDisabled(card.ConfirmButton), Is.True, "and Confirm now says the location is taken");
            Assert.That(card.ReasonLabel.text, Is.EqualTo(BuildCardCopy.LocationTaken));
        }

        // ---------------------------------------------------------------------------------------------------
        // Back and Cancel
        // ---------------------------------------------------------------------------------------------------

        [UnityTest]
        public IEnumerator BackCancelsTheBuildAndClearsThePreview()
        {
            yield return Make(PhoneScreen(), 100, RunShellFixtures.ComfortableCash);
            yield return OpenBuild(0);
            var cashBefore = h.Session.ReadCash();
            Assert.That(preview.PreviewSlot, Is.EqualTo(0));

            Assert.That(h.Shell.HandleBack(), Is.True, "Back is consumed by the build card");
            yield return ShellRig.Settle();

            Assert.That(h.Shell.IsBuildCardOpen, Is.False);
            Assert.That(h.Shell.IsDockedCardOpen, Is.False);
            Assert.That(h.Shell.SelectedSlot, Is.EqualTo(-1));
            Assert.That(preview.PreviewSlot, Is.EqualTo(-1), "the ghost is gone");
            Assert.That(preview.Available, Is.Empty, "and the highlight");
            Assert.That(MachineCount(), Is.EqualTo(0), "nothing was built");
            Assert.That(h.Session.ReadCash(), Is.EqualTo(cashBefore), "nothing was charged");
            Assert.That(h.Shell.HandleBack(), Is.False, "the next Back has nothing left to pop");
        }

        [UnityTest]
        public IEnumerator CancelClosesTheCardWithoutBuilding()
        {
            yield return Make(PhoneScreen(), 100, RunShellFixtures.ComfortableCash);
            yield return OpenBuild(0);
            Press(card.CancelButton);
            yield return ShellRig.Settle();
            Assert.That(h.Shell.IsDockedCardOpen, Is.False);
            Assert.That(h.Shell.SelectedSlot, Is.EqualTo(-1));
            Assert.That(MachineCount(), Is.EqualTo(0));
            Assert.That(preview.PreviewSlot, Is.EqualTo(-1));
        }

        [UnityTest]
        public IEnumerator OpeningTheHubClosesTheBuildCard()
        {
            yield return Make(PhoneScreen(), 100, RunShellFixtures.ComfortableCash);
            h.Shell.Panels.Register(HubHarness.NewHub(h.Session, h.Commands));
            yield return OpenBuild(0);
            h.Shell.OpenHub();
            yield return ShellRig.Settle();
            Assert.That(h.Shell.IsBuildCardOpen, Is.False, "the hub is exclusive with the build card");
            Assert.That(h.Shell.IsHubOpen, Is.True);
            Assert.That(preview.PreviewSlot, Is.EqualTo(-1));
        }

        // ---------------------------------------------------------------------------------------------------
        // Layout: side by side, 44 pt, every class and scale, empty states
        // ---------------------------------------------------------------------------------------------------

        private void AssertActions(string context)
        {
            var confirm = card.ConfirmButton.worldBound;
            var cancel = card.CancelButton.worldBound;
            Assert.That(confirm.height, Is.GreaterThanOrEqualTo(MinimumTarget - Tolerance), $"{context}: Confirm height");
            Assert.That(confirm.width, Is.GreaterThanOrEqualTo(MinimumTarget - Tolerance), $"{context}: Confirm width");
            Assert.That(cancel.height, Is.GreaterThanOrEqualTo(MinimumTarget - Tolerance), $"{context}: Cancel height");
            Assert.That(cancel.width, Is.GreaterThanOrEqualTo(MinimumTarget - Tolerance), $"{context}: Cancel width");
            Assert.That(Mathf.Abs(confirm.yMin - cancel.yMin), Is.LessThan(1f), $"{context}: Confirm and Cancel share a row");
            Assert.That(cancel.xMax, Is.LessThanOrEqualTo(confirm.xMin + Tolerance), $"{context}: Cancel sits beside Confirm, not over it");
            var area = card.Root.worldBound;
            AssertWithin(area, confirm, $"{context}: Confirm stays inside the card");
            AssertWithin(area, cancel, $"{context}: Cancel stays inside the card");
            Assert.That(IsDisplayed(card.ConfirmButton) && IsDisplayed(card.CancelButton), Is.True);
        }

        [UnityTest]
        public IEnumerator ConfirmAndCancelAreSideBySideAndAtLeast44PtOnEveryClassAndTextScale(
            [ValueSource(nameof(Screens))] ShellScreen screen, [ValueSource(nameof(Percents))] int percent)
        {
            var context = $"{screen} @{percent}%";
            // Worst case: every type listed, Confirm disabled with its reason line showing, a two-line empty-state hint.
            yield return Make(screen, percent, 0L, unlockEverything: true, factoryLevel: RunShellFixtures.FactoryLevelCount);
            yield return OpenBuild(0);
            yield return ShellRig.Settle();

            Assert.That(h.Shell.IsBuildCardOpen, Is.True, context);
            Assert.That(card.ReasonLabel.style.display.value, Is.EqualTo(DisplayStyle.Flex), context);
            AssertActions(context + " (short of Cash)");
            AssertWithin(card.Root.worldBound, card.ReasonLabel.worldBound, context + ": the reason line stays inside the card");
            AssertWithin(card.Root.worldBound, card.SelectedLabel.worldBound, context + ": the selected line stays inside the card");
            AssertTextAndTargets(card.Root, h.Rig.SafeRect(), context + " (build card)", MinimumFontSize * percent / 100f);

            // Every type row is a 44 pt target inside the scrolling list, and the list is the part that scrolls.
            foreach (var button in card.TypeButtons)
            {
                Assert.That(button.worldBound.height, Is.GreaterThanOrEqualTo(MinimumTarget - Tolerance), $"{context}: {button.name} height");
            }
        }

        [UnityTest]
        public IEnumerator TheEnabledStateAlsoKeepsBothActionsInTheCardOnEveryClassAndTextScale(
            [ValueSource(nameof(Screens))] ShellScreen screen, [ValueSource(nameof(Percents))] int percent)
        {
            var context = $"{screen} @{percent}%";
            yield return Make(screen, percent, RunShellFixtures.ComfortableCash * 100_000L, unlockEverything: true, factoryLevel: RunShellFixtures.FactoryLevelCount);
            yield return OpenBuild(0);
            yield return ShellRig.Settle();
            Assert.That(PanelScaffold.IsDisabled(card.ConfirmButton), Is.False, context);
            AssertActions(context + " (enough Cash)");
            AssertTextAndTargets(card.Root, h.Rig.SafeRect(), context + " (build card, enabled)", MinimumFontSize * percent / 100f);
        }

        [UnityTest]
        public IEnumerator ANoUnlockedTypeCardShowsAnEmptyStateAndADisabledConfirm()
        {
            // The variant content that locks even the Miner is built by the build-sheet fixtures; here the shipped content with nothing affordable stands in for
            // "no usable type": the empty-state line, the disabled Confirm and its reason are all text.
            yield return Make(PhoneScreen(), 100, 0L);
            yield return OpenBuild(0);
            Assert.That(PanelScaffold.IsDisabled(card.ConfirmButton), Is.True);
            Assert.That(card.HintLabel.style.display.value, Is.EqualTo(DisplayStyle.Flex));
            Assert.That(card.ReasonLabel.text, Does.StartWith("Need $"));
            Assert.That(card.ConfirmButton.focusable && card.CancelButton.focusable, Is.True, "both actions stay reachable from the keyboard");
        }

        [UnityTest]
        public IEnumerator EveryControlHasAnAccessibleNameAndTheKeyboardOrderIsTypesThenCancelThenConfirm()
        {
            yield return Make(PhoneScreen(), 100, RunShellFixtures.ComfortableCash, unlockEverything: true);
            yield return OpenBuild(0);
            foreach (var button in card.Root.Query<Button>().ToList())
            {
                Assert.That(!string.IsNullOrEmpty(button.text) || !string.IsNullOrEmpty(button.tooltip), Is.True, $"{button.name} has an accessible name");
                Assert.That(button.focusable, Is.True, $"{button.name} is focusable");
            }

            var order = card.Root.Query<Button>().ToList().Select(button => button.name).ToList();
            Assert.That(order.IndexOf("build-card-cancel"), Is.GreaterThan(order.IndexOf(BuildCard.FirstFocusName)), "the types come before the actions");
            Assert.That(order.IndexOf("build-card-confirm"), Is.GreaterThan(order.IndexOf("build-card-cancel")), "Cancel comes before Confirm");

            // Opened for the keyboard, focus lands on the first type.
            h.Shell.CloseBuildCard();
            Assert.That(h.Shell.OpenSlot(0, SlotCondition.Empty, 0, true), Is.True);
            yield return ShellRig.Settle();
            yield return ShellRig.Settle();
            Assert.That(card.Root.panel.focusController.focusedElement, Is.SameAs(card.TypeButtons[0]));
        }

        // ---------------------------------------------------------------------------------------------------
        // Rotation
        // ---------------------------------------------------------------------------------------------------

        [UnityTest]
        public IEnumerator ARotationKeepsTheCardTheSelectionAndThePreviewAndRedocksIt([ValueSource(nameof(Screens))] ShellScreen screen)
        {
            yield return Make(screen, 100, RunShellFixtures.ComfortableCash, unlockEverything: true, factoryLevel: RunShellFixtures.FactoryLevelCount);
            var shell = h.Shell;
            yield return OpenBuild(2);
            Press(card.TypeButton("machine.smelter"));
            Assert.That(shell.IsBuildCardOpen, Is.True);

            var rotated = ShellScreens.Rotated(screen);
            shell.ApplyScreenLayout(rotated.Width, rotated.Height, rotated.SafeAreaPx);
            Assert.That(shell.IsCardBandPending, Is.True, $"{screen}: the rotation makes the camera wait for the card's new band");
            yield return ShellRig.Settle();

            Assert.That(shell.Layout, Is.EqualTo(rotated.Expected), "the class of the rotated device");
            Assert.That(shell.IsBuildCardOpen, Is.True, "a rotation keeps the card");
            Assert.That(shell.SelectedSlot, Is.EqualTo(2), "and the selection");
            Assert.That(card.SelectedMachineId, Is.EqualTo("machine.smelter"), "and the selected type");
            Assert.That(preview.PreviewSlot, Is.EqualTo(2), "and the preview");
            Assert.That(shell.IsCardBandPending, Is.False);
            Assert.That(card.Root.parent, rotated.Expected == FactoryHudLayout.CompactPortrait || rotated.Expected == FactoryHudLayout.CompactLandscape
                ? Is.SameAs(shell.Root.Q<VisualElement>("run-main"))
                : Is.SameAs(shell.PanelHost), "the card re-docks for the new class");
            Assert.That(shell.TryGetUnobstructedScreenRect(rotated.Height, out var area), Is.True);
            Assert.That(Intersects(new Rect(area.xMin, shell.Root.panel.visualTree.worldBound.height - area.yMax, area.width, area.height), card.Root.worldBound), Is.False,
                $"{screen}: after the rotation the world band still leaves the card alone");
            AssertActions($"{screen} rotated to {rotated.Expected}");
        }

        // ---------------------------------------------------------------------------------------------------
        // Preview visibility (SC-002, OD-9b C1, U-B1)
        // ---------------------------------------------------------------------------------------------------

        private Camera CreateFramingCamera(out FactoryCameraController controller)
        {
            var host = new GameObject("build-card-camera");
            objects.Add(host);
            host.transform.position = new Vector3(0f, 0f, -10f);
            var camera = host.AddComponent<Camera>();
            camera.orthographic = true;
            camera.targetTexture = h.Rig.Texture;
            camera.enabled = false;
            controller = host.AddComponent<FactoryCameraController>();
            controller.Configure(camera, new RunZoomSettings().ToScaleSettings());
            return camera;
        }

        private static Rect ScreenRectOf(Camera camera, Rect world)
        {
            var low = camera.WorldToScreenPoint(new Vector3(world.xMin, world.yMin, 0f));
            var high = camera.WorldToScreenPoint(new Vector3(world.xMax, world.yMax, 0f));
            return Rect.MinMaxRect(Mathf.Min(low.x, high.x), Mathf.Min(low.y, high.y), Mathf.Max(low.x, high.x), Mathf.Max(low.y, high.y));
        }

        private static void AssertInside(Rect area, Rect inner, string message, float tolerance = 1.5f)
        {
            Assert.That(inner.xMin, Is.GreaterThanOrEqualTo(area.xMin - tolerance), message + " (left)");
            Assert.That(inner.yMin, Is.GreaterThanOrEqualTo(area.yMin - tolerance), message + " (bottom)");
            Assert.That(inner.xMax, Is.LessThanOrEqualTo(area.xMax + tolerance), message + " (right)");
            Assert.That(inner.yMax, Is.LessThanOrEqualTo(area.yMax + tolerance), message + " (top)");
        }

        [UnityTest]
        public IEnumerator ThePreviewLocationStaysFullyVisibleAboveTheCardOnEveryClassAndTextScale(
            [ValueSource(nameof(Screens))] ShellScreen screen, [ValueSource(nameof(Percents))] int percent)
        {
            yield return Make(screen, percent, RunShellFixtures.ComfortableCash * 100_000L, unlockEverything: true, factoryLevel: RunShellFixtures.FactoryLevelCount);
            var shell = h.Shell;
            var camera = CreateFramingCamera(out var controller);
            var framing = new RunCameraFraming(new RunZoomSettings());
            var layout = LatticeLayoutBuilder.ShippedForColumns(SlotGridLayout.ColumnsFor(screen.Width, screen.Height));
            var context = $"{screen} @{percent}%";
            Assert.That(shell.TryGetUnobstructedScreenRect(screen.Height, out var closedArea), Is.True, context);
            controller.Fit(RunCameraFraming.Layout(camera, layout, h.Rig.PixelsPerPoint, closedArea));
            var fitted = controller.Scale;
            var count = Math.Min(layout.LocationCount, (int)RunShellFixtures.FactorySlots(RunShellFixtures.FactoryLevelCount));

            foreach (var slot in new[] { 0, count / 2, count - 1 })
            {
                Assert.That(shell.OpenSlot(slot, SlotCondition.Empty), Is.True, context);
                Assert.That(shell.IsCardBandPending, Is.True, $"{context}: slot {slot}: the card's band is pending until it is laid out");
                yield return ShellRig.Settle();
                Assert.That(shell.TryGetUnobstructedScreenRect(screen.Height, out var area), Is.True, context);
                controller.OpenSheet(area, framing.FrameMachine(slot, layout));

                var machine = ScreenRectOf(camera, layout.MachineRectOf(new BuildingLocationId(slot)));
                AssertInside(area, machine, $"{context}: slot {slot}: the previewed location is 100 % inside the band above the card");
                Assert.That(Intersects(new Rect(area.xMin, screen.Height - area.yMax, area.width, area.height), card.Root.worldBound), Is.False,
                    $"{context}: slot {slot}: the world band does not overlap the card");
                Assert.That(controller.Scale, Is.LessThanOrEqualTo(fitted), $"{context}: a card never zooms in");
                Assert.That(preview.PreviewSlot, Is.EqualTo(slot), context);

                shell.CloseBuildCard();
                yield return ShellRig.Settle();
                Assert.That(shell.TryGetUnobstructedScreenRect(screen.Height, out var reopened), Is.True, context);
                controller.CloseSheet(reopened);
                Assert.That(controller.Scale, Is.EqualTo(fitted), $"{context}: the prior scale returns");
            }
        }

        [UnityTest]
        public IEnumerator CashAndOverdriveStayOnScreenWhileTheBuildCardIsOpenOnAPhoneInLandscape(
            [ValueSource(nameof(LandscapePhones))] ShellScreen screen, [ValueSource(nameof(Percents))] int percent)
        {
            yield return Make(screen, percent, 1_234_567L, unlockEverything: true, factoryLevel: RunShellFixtures.FactoryLevelCount);
            var shell = h.Shell;
            shell.SetCash(9_000_000_000_000_000_000L);
            shell.SetStore(9_000_000_000_000_000_000L, 9_000_000_000_000_000_000L);
            shell.SetOverdrive(RunShellFixtures.OverdriveStatusIn(OverdriveStage.CoolingDown));
            yield return OpenBuild(0);
            yield return ShellRig.Settle();
            var context = $"{screen} @{percent}%";

            Assert.That(shell.IsStatusBandShown, Is.True, context + ": the status band shows while the build card is open (U-B1)");
            var safe = h.Rig.SafeRect();
            foreach (var control in new VisualElement[] { shell.CashButton, shell.StoreButton, shell.OverdriveButton })
            {
                Assert.That(IsDisplayed(control), Is.True, $"{context}: {control.name} is shown");
                AssertWithin(safe, control.worldBound, $"{context}: {control.name} stays in the safe area");
                Assert.That(control.worldBound.height, Is.GreaterThanOrEqualTo(MinimumTarget - Tolerance), $"{context}: {control.name} height");
                Assert.That(Intersects(control.worldBound, card.Root.worldBound), Is.False, $"{context}: {control.name} does not overlap the card");
            }

            Assert.That(shell.OverdriveButton.Q<Label>("overdrive-caption").text, Does.StartWith("Cool"), context + ": the Overdrive state word");
            AssertActions(context);
            shell.CloseBuildCard();
            yield return ShellRig.Settle();
            Assert.That(shell.IsStatusBandShown, Is.False, context + ": closed again");
        }

        public static IEnumerable<ShellScreen> LandscapePhones() =>
            ShellScreens.All().Where(screen => screen.Expected == FactoryHudLayout.CompactLandscape);
    }
}
#endif
