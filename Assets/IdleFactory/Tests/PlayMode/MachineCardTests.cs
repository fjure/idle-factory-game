using System;
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
    /// T534 (FR-016, FR-019, FR-034, SC-002, research R10, OD-9b UI/UX C2, C4, C5): the docked machine card. First view and Upgrade without scrolling at 200 %
    /// with worst-case copy on every screen class (including 640x360), the docked layouts, in-place upgrade with focus kept, the world effect or the static
    /// one-second highlight (FR-034), the stale-action line, the removed-machine close, the models (P2-F10, P2-F12, P2-F13) and the 48 pt targets.
    /// </summary>
    public sealed class MachineCardTests
    {
        private readonly List<CardHarness> harnesses = new List<CardHarness>();
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

            harnesses.Clear();
            h = null;
        }

        private static ShellScreen PhoneScreen() =>
            ShellScreens.All().First(screen => screen.Name.StartsWith("phone-portrait 390", StringComparison.Ordinal));

        private IEnumerator Make(
            ShellScreen screen, int percent, long cash, bool reduced = false, bool effects = false, int level = 1, bool unlockEverything = false)
        {
            CardHarness made = null;
            yield return CardHarness.Create(screen, percent, cash, built => made = built, reduced, effects, level, unlockEverything);
            harnesses.Add(made);
            h = made;
        }

        private static long MinerCost => RunShellFixtures.MinerCost;

        private VisualElement FocusedElement => h.Shell.Root.panel.focusController.focusedElement as VisualElement;

        // ---------------------------------------------------------------------------------------------------
        // First view
        // ---------------------------------------------------------------------------------------------------

        [UnityTest]
        public IEnumerator TheFirstViewShowsNameStateAndUpgradeWithTheCostInsideTheButton()
        {
            yield return Make(PhoneScreen(), 100, RunShellFixtures.ComfortableCash);
            h.Build(0, "machine.miner");
            yield return h.OpenCard(0);

            var card = h.Card;
            Assert.That(h.Shell.IsCardOpen, Is.True);
            Assert.That(h.Shell.CardSlot, Is.EqualTo(0));
            Assert.That(h.Shell.SelectedSlot, Is.EqualTo(0), "selection drives the camera");
            Assert.That(card.NameLabel.text, Is.EqualTo("Miner - level 1"));
            Assert.That(card.StateLabel.text, Is.EqualTo("Running"));
            Assert.That(card.UpgradeButton.text, Does.StartWith("Upgrade: $"), "the cost is inside the button");
            Assert.That(PanelScaffold.IsDisabled(card.UpgradeButton), Is.False);
            Assert.That(card.CauseLabel.style.display.value, Is.EqualTo(DisplayStyle.None), "a running machine has no cause line");
            Assert.That(card.Root.Q<VisualElement>("machine-remove"), Is.Null, "Remove is not on the card (FR-019)");
            Assert.That(card.Root.Query<Button>().ToList().Any(button => button.text == "Remove"), Is.False);
            Assert.That(card.DetailsButton.text, Does.StartWith("Details"));
            Assert.That(card.CloseButton.tooltip, Is.EqualTo("Close"), "the icon-only close has its accessible name");
        }

        [UnityTest]
        public IEnumerator AnUnaffordableUpgradeShowsTheShortfallInsideTheButtonAndKeepsItsReason()
        {
            yield return Make(PhoneScreen(), 100, MinerCost);
            h.Build(0, "machine.miner");
            yield return h.OpenCard(0);

            var card = h.Card;
            var next = h.MachineAt(0).NextUpgrade;
            Assert.That(next.Affordable, Is.False);
            Assert.That(card.UpgradeButton.text, Does.Contain("Need $"), "the shortfall is inside the button");
            Assert.That(PanelScaffold.IsDisabled(card.UpgradeButton), Is.True, "the one disabled state");
            Assert.That(card.UpgradeButton.tooltip, Does.Contain("Unavailable"), "the reason is the accessible description, not only colour");
            var cash = h.Session.ReadCash();
            
            CardHarness.Press(card.UpgradeButton);
            Assert.That(card.InlineLabel.text, Is.EqualTo(RunRejectionCopy.For(RunReasons.CashInsufficient)), "a press shows the rejection inline and changes nothing");
            Assert.That(card.InlineLabel.style.display.value, Is.EqualTo(DisplayStyle.Flex));
            Assert.That(h.Session.ReadCash(), Is.EqualTo(cash));
            Assert.That(h.MachineAt(0).Level, Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator ThePausedMachineNamesItsCauseFromTheModelWithTheResourceIcon()
        {
            yield return Make(PhoneScreen(), 100, RunShellFixtures.ComfortableCash * 4, unlockEverything: true, level: 2);
            h.Build(0, "machine.smelter");
            yield return h.OpenCard(0);
            h.Session.Advance(2_000L * RunSession.TicksPerMs);
            h.Shell.RefreshCard();
            yield return ShellRig.Settle();

            var card = h.Card;
            Assert.That(card.VisualState, Is.EqualTo(MachineVisualState.MissingInput));
            Assert.That(card.StateLabel.text, Is.EqualTo("Paused"));
            var presentation = card.Model.Get(new BuildingLocationId(0));
            Assert.That(presentation.LimitingResourceId, Is.Not.Null, "the limiting resource comes from the presentation model (P2-F13)");
            Assert.That(card.CauseLabel.text, Is.EqualTo(AttentionModel.ShortOnCopy("Iron Ore")), "the copy comes from the attention model, not derived again");
            var iconKey = h.Session.Catalog.Resources[presentation.LimitingResourceId].PresentationKey;
            if (ContractsPanelTestSupport.Icon(iconKey) != null)
            {
                Assert.That(card.CauseLabel.style.backgroundImage.value.sprite, Is.Not.Null, "the cause line carries the resource icon");
            }
            Assert.That(card.CauseLabel.style.display.value, Is.EqualTo(DisplayStyle.Flex));
        }

        [UnityTest]
        public IEnumerator ASwitchedOffMachineSaysSoAndPointsToDetails()
        {
            yield return Make(PhoneScreen(), 100, RunShellFixtures.ComfortableCash);
            var id = h.Build(0, "machine.miner");
            Assert.That(h.Commands.SetActive(id, false).IsSuccess, Is.True);
            yield return h.OpenCard(0);

            Assert.That(h.Card.StateLabel.text, Is.EqualTo("Switched off"));
            Assert.That(h.Card.CauseLabel.text, Is.EqualTo(MachineCardCopy.SwitchedOffCause));
        }

        // ---------------------------------------------------------------------------------------------------
        // In place, focus, responses
        // ---------------------------------------------------------------------------------------------------

        [UnityTest]
        public IEnumerator UpgradeHappensInPlaceAndFocusStaysOnUpgrade()
        {
            yield return Make(PhoneScreen(), 100, RunShellFixtures.ComfortableCash * 4);
            h.Build(0, "machine.miner");
            yield return h.OpenCard(0);
            var card = h.Card;
            card.UpgradeButton.Focus();
            Assert.That(FocusedElement, Is.SameAs(card.UpgradeButton));
            var cash = h.Session.ReadCash();

            CardHarness.Press(card.UpgradeButton);
            yield return ShellRig.Settle();

            Assert.That(h.MachineAt(0).Level, Is.EqualTo(2), "the upgrade went through RunCommands");
            Assert.That(h.Session.ReadCash(), Is.LessThan(cash));
            Assert.That(h.Shell.IsCardOpen, Is.True, "the card stays open");
            Assert.That(card.NameLabel.text, Does.Contain("level 2"), "the card updated in place");
            Assert.That(FocusedElement, Is.SameAs(card.UpgradeButton), "focus stays on Upgrade");
            Assert.That(h.Shell.FocusRingTarget, Is.SameAs(card.UpgradeButton), "the shell focus ring surrounds Upgrade");
            Assert.That(card.InlineLabel.style.display.value, Is.EqualTo(DisplayStyle.None));
        }

        [UnityTest]
        public IEnumerator ASuccessfulUpgradeSpawnsTheGlintThroughTheEffectSinkWhenMotionIsOn()
        {
            yield return Make(PhoneScreen(), 100, RunShellFixtures.ComfortableCash * 4, effects: true);
            h.Build(0, "machine.miner");
            yield return h.OpenCard(0);

            CardHarness.Press(h.Card.UpgradeButton);

            Assert.That(h.Effects.Spawned, Is.EqualTo(new[] { (MachineResponseKind.UpgradeGlint, 0) }), "the UpgradeGlint goes to the EffectPool seam");
            Assert.That(h.Feedback.LastWasWorldEffect, Is.True);
            Assert.That(h.Card.IsHighlighted, Is.False, "motion on and a sink wired: no static highlight");
        }

        [UnityTest]
        public IEnumerator UnderReducedMotionNoEffectSpawnsAndAnUpgradeShowsAStaticOneSecondHighlight()
        {
            yield return Make(PhoneScreen(), 100, RunShellFixtures.ComfortableCash * 4, reduced: true, effects: true);
            h.Build(0, "machine.miner");
            yield return h.OpenCard(0);
            h.Card.HighlightSeconds = 0.25f;

            CardHarness.Press(h.Card.UpgradeButton);

            Assert.That(h.Effects.Spawned, Is.Empty, "no effect spawns under reduced motion (FR-034)");
            Assert.That(h.Feedback.LastWasWorldEffect, Is.False);
            Assert.That(h.Card.IsHighlighted, Is.True, "the response is a static highlight");
            Assert.That(h.Card.UpgradeButton.worldBound.height, Is.GreaterThan(0f));
            var before = h.Card.Root.worldBound;
            yield return ShellRig.Settle();
            Assert.That(h.Card.Root.worldBound, Is.EqualTo(before), "nothing moves: the highlight changes colour only");
            yield return new WaitForSecondsRealtime(0.5f);
            Assert.That(h.Card.IsHighlighted, Is.False, "the highlight ends after its second");
        }

        [UnityTest]
        public IEnumerator WithoutAWorldSinkTheResponseIsTheStaticHighlightAlso()
        {
            yield return Make(PhoneScreen(), 100, RunShellFixtures.ComfortableCash * 4);
            h.Build(0, "machine.miner");
            yield return h.OpenCard(0);

            CardHarness.Press(h.Card.UpgradeButton);

            Assert.That(h.Feedback.Responses, Is.EqualTo(1));
            Assert.That(h.Feedback.LastKind, Is.EqualTo(MachineResponseKind.UpgradeGlint));
            Assert.That(h.Card.IsHighlighted, Is.True, "until T529 wires the pool the player still gets a response");
        }

        [UnityTest]
        public IEnumerator ASwitchOnInDetailsAnswersWithARestrainedEffectOrTheStaticHighlight()
        {
            yield return Make(PhoneScreen(), 100, RunShellFixtures.ComfortableCash, effects: true);
            var id = h.Build(0, "machine.miner");
            Assert.That(h.Commands.SetActive(id, false).IsSuccess, Is.True);
            yield return h.OpenCard(0);
            CardHarness.Press(h.Card.DetailsButton);
            yield return ShellRig.Settle();

            CardHarness.Press(h.Details.ActiveButton);
            Assert.That(h.Effects.Spawned, Is.EqualTo(new[] { (MachineResponseKind.SwitchOn, 0) }));
            Assert.That(h.Details.IsHighlighted, Is.False);

            h.Reduced = true;
            h.Details.HighlightSeconds = 0.25f;
            CardHarness.Press(h.Details.ActiveButton); // switch off: no response
            Assert.That(h.Effects.Spawned.Count, Is.EqualTo(1));
            CardHarness.Press(h.Details.ActiveButton); // switch on again under reduced motion
            Assert.That(h.Effects.Spawned.Count, Is.EqualTo(1), "no effect under reduced motion");
            Assert.That(h.Details.IsHighlighted, Is.True, "a static one-second highlight instead");
            yield return new WaitForSecondsRealtime(0.5f);
            Assert.That(h.Details.IsHighlighted, Is.False);
        }

        // ---------------------------------------------------------------------------------------------------
        // Stale and removed
        // ---------------------------------------------------------------------------------------------------

        [UnityTest]
        public IEnumerator AStaleActionShowsAnInlineLineAndNeverExecutes()
        {
            yield return Make(PhoneScreen(), 100, RunShellFixtures.ComfortableCash * 4);
            var id = h.Build(0, "machine.miner");
            yield return h.OpenCard(0);
            var cash = h.Session.ReadCash();
            var removed = h.Commands.Remove(id, true);
            Assert.That(removed.IsSuccess, Is.True, "the machine is removed behind the card's back");
            var cashAfterRemoval = h.Session.ReadCash();
            Assert.That(cashAfterRemoval, Is.GreaterThan(cash - 1), "the refund is not an upgrade charge");

            CardHarness.Press(h.Card.UpgradeButton);

            Assert.That(h.Card.InlineLabel.text, Is.Not.Empty, "the stale action shows its rejection copy inline");
            Assert.That(h.Session.ReadCash(), Is.EqualTo(cashAfterRemoval), "the stale action charged nothing");
            Assert.That(RunSnapshots.Factory(h.Session).Machines, Is.Empty, "nothing was upgraded, rebuilt or resurrected");
        }

        [UnityTest]
        public IEnumerator ARemovedMachineClosesTheCardWithANotice()
        {
            yield return Make(PhoneScreen(), 100, RunShellFixtures.ComfortableCash);
            var id = h.Build(0, "machine.miner");
            yield return h.OpenCard(0);
            h.Card.UpgradeButton.Focus();
            Assert.That(h.Commands.Remove(id, true).IsSuccess, Is.True);

            h.Shell.RefreshCard();
            yield return ShellRig.Settle();

            Assert.That(h.Shell.IsCardOpen, Is.False, "the card closes");
            Assert.That(h.Shell.SelectedSlot, Is.EqualTo(-1));
            Assert.That(h.Shell.NoticeText, Is.EqualTo(MachineCardCopy.Gone), "and says why");
            Assert.That(FocusedElement, Is.SameAs(h.Shell.LocationStop), "focus returns to the location, not to a hidden control");
        }

        [UnityTest]
        public IEnumerator AReplacedMachineAtTheSameLocationClosesTheCardToo()
        {
            yield return Make(PhoneScreen(), 100, RunShellFixtures.ComfortableCash * 4);
            var id = h.Build(0, "machine.miner");
            yield return h.OpenCard(0);
            Assert.That(h.Commands.Remove(id, true).IsSuccess, Is.True);
            h.Build(0, "machine.miner");

            h.Shell.RefreshCard();

            Assert.That(h.Shell.IsCardOpen, Is.False, "another instance stands at the location: the card does not silently switch to it");
            Assert.That(h.Shell.NoticeText, Is.EqualTo(MachineCardCopy.Gone));
        }

        [UnityTest]
        public IEnumerator TheSlotRouteClosesTheCardWhenTheLocationBecomesEmpty()
        {
            yield return Make(PhoneScreen(), 100, RunShellFixtures.ComfortableCash);
            var id = h.Build(0, "machine.miner");
            yield return h.OpenCard(0);
            Assert.That(h.Commands.Remove(id, true).IsSuccess, Is.True);

            h.Shell.UpdateSlotRoute(0, SlotCondition.Empty);

            Assert.That(h.Shell.IsCardOpen, Is.False);
            Assert.That(h.Shell.NoticeText, Is.EqualTo(MachineCardCopy.Gone));
        }

        // ---------------------------------------------------------------------------------------------------
        // Models (P2-F10, P2-F12)
        // ---------------------------------------------------------------------------------------------------

        [UnityTest]
        public IEnumerator TheModelRefreshesEveryFrameSoADeltaIsNeverRepeated()
        {
            yield return Make(PhoneScreen(), 100, RunShellFixtures.ComfortableCash);
            h.Build(0, "machine.miner");
            yield return h.OpenCard(0);
            h.Session.Advance(RunShellFixtures.CycleMs("machine.miner") * 2L * RunSession.TicksPerMs);
            h.Shell.RefreshCard();
            var first = h.Card.Model.Get(new BuildingLocationId(0)).CompletionsDelta;
            Assert.That(first, Is.GreaterThan(0), "completions since the last refresh are reported once");

            for (var frame = 0; frame < 4; frame++)
            {
                h.Shell.RefreshCard();
                Assert.That(h.Card.Model.Get(new BuildingLocationId(0)).CompletionsDelta, Is.EqualTo(0), "frame " + frame + ": the same delta is not read again");
            }
        }

        [UnityTest]
        public IEnumerator ASessionReplaceOrAnOfflineReturnResetsTheBaselineAndReportsNoDelta()
        {
            yield return Make(PhoneScreen(), 100, RunShellFixtures.ComfortableCash);
            h.Build(0, "machine.miner");
            yield return h.OpenCard(0);
            h.Session.Advance(RunShellFixtures.CycleMs("machine.miner") * 3L * RunSession.TicksPerMs);

            h.Card.ResetModels(true);
            h.Shell.RefreshCard();

            Assert.That(h.Card.Model.Get(new BuildingLocationId(0)).CompletionsDelta, Is.EqualTo(0), "the refresh after an offline return reports no delta");
            h.Session.Advance(RunShellFixtures.CycleMs("machine.miner") * 2L * RunSession.TicksPerMs);
            h.Shell.RefreshCard();
            Assert.That(h.Card.Model.Get(new BuildingLocationId(0)).CompletionsDelta, Is.GreaterThan(0), "later completions count again");
        }

        // ---------------------------------------------------------------------------------------------------
        // Docked layouts
        // ---------------------------------------------------------------------------------------------------

        [UnityTest]
        public IEnumerator TheCardIsDockedPerClassAndNeverCoversTheWorld([ValueSource(nameof(Screens))] ShellScreen screen, [ValueSource(nameof(Percents))] int percent)
        {
            yield return Make(screen, percent, RunShellFixtures.ComfortableCash);
            h.Build(0, "machine.miner");
            yield return h.OpenCard(0);

            var shell = h.Shell;
            var card = h.Card.Root.worldBound;
            var world = shell.WorldColumn.worldBound;
            var safe = h.Rig.SafeRect();
            var context = $"{screen} @{percent}%";
            Assert.That(IsDisplayed(h.Card.Root), Is.True, context);
            AssertWithin(safe, card, context + ": the card stays inside the safe area");
            Assert.That(Intersects(card, world), Is.False, context + ": the card never covers the world column");
            Assert.That(card.height, Is.LessThan(safe.height * 0.95f).And.GreaterThan(0f), context + ": the card is docked, not a full-screen fill");

            if (screen.Expected == FactoryHudLayout.CompactPortrait)
            {
                Assert.That(h.Card.Root.parent.name, Is.EqualTo("run-main"), context);
                Assert.That(card.width, Is.EqualTo(safe.width).Within(1f), context + ": a bottom band across the width");
                Assert.That(card.yMin, Is.GreaterThanOrEqualTo(world.yMax - 0.5f), context + ": below the world");
                Assert.That(card.yMax, Is.LessThanOrEqualTo(shell.TabBar.worldBound.yMin + 0.5f), context + ": above the tab bar");
                var allowed = RunShellDocument.CardBandFraction(percent);
                Assert.That(card.height, Is.LessThanOrEqualTo(safe.height * allowed + 0.5f), context + ": the band cap (30 % up to 140 % text, 45 % hard cap above)");
            }
            else if (screen.Expected == FactoryHudLayout.CompactLandscape)
            {
                Assert.That(h.Card.Root.parent.name, Is.EqualTo("run-main"), context);
                Assert.That(card.width / safe.width, Is.EqualTo(0.4f).Within(0.02f), context + ": a side column of about 40 % of the width");
                Assert.That(card.xMin, Is.GreaterThanOrEqualTo(world.xMax - 0.5f), context + ": beside the world");
            }
            else
            {
                Assert.That(h.Card.Root.parent, Is.SameAs(shell.PanelHost), context + ": the side panel");
                Assert.That(card.xMin, Is.GreaterThanOrEqualTo(world.xMax - 0.5f), context + ": beside the world");
            }

            AssertShellLayout(h.Rig, "card open");
        }

        [UnityTest]
        public IEnumerator ThePortraitBandStaysWithinThirtyPercentAtNormalText([ValueSource(nameof(Screens))] ShellScreen screen)
        {
            if (screen.Expected != FactoryHudLayout.CompactPortrait)
            {
                Assert.Pass("The 30 % band applies to phone portrait.");
            }

            foreach (var percent in new[] { 100, 120, 140 })
            {
                yield return Make(screen, percent, RunShellFixtures.ComfortableCash);
                h.Build(0, "machine.miner");
                yield return h.OpenCard(0);
                var safe = h.Rig.SafeRect();
                Assert.That(h.Card.Root.worldBound.height, Is.LessThanOrEqualTo(safe.height * 0.30f + 0.5f), $"{screen} @{percent}%: at most 30 % of the safe height");
                AssertWithin(h.Card.Root.worldBound, h.Card.UpgradeButton.worldBound, $"{screen} @{percent}%: Upgrade is in the first view");
                h.Dispose();
                harnesses.Remove(h);
            }
        }

        [UnityTest]
        public IEnumerator UpgradeStaysInTheFirstViewAtTwoHundredPercentWithWorstCaseCopy([ValueSource(nameof(Screens))] ShellScreen screen)
        {
            yield return Make(screen, 200, RunShellFixtures.ComfortableCash * 8, unlockEverything: true, level: 3);
            h.Build(0, "machine.assembler");
            yield return h.OpenCard(0);
            var card = h.Card;

            // Worst-case copy on every line: the longest name and level, the longest state, an unaffordable Upgrade with the largest
            // numbers (cost and shortfall), the longest cause and a stale-action line.
            card.NameLabel.text = "Assembler - level 999";
            card.StateLabel.text = MachineCardCopy.SwitchedOff;
            card.UpgradeButton.text = "Upgrade: $999.9Qa\nNeed $999.9Qa";
            PanelScaffold.SetDisabled(card.UpgradeButton, true, "Not enough Cash. Need $999.9Qa more.");
            card.CauseLabel.text = MachineCardCopy.WaitingCause;
            card.CauseLabel.style.display = DisplayStyle.Flex;
            card.InlineLabel.text = RunRejectionCopy.For(RunReasons.CashInsufficient) + " Need $999.9Qa more.";
            card.InlineLabel.style.display = DisplayStyle.Flex;
            yield return ShellRig.Settle();

            var context = $"{screen} @200%";
            var cardRect = card.Root.worldBound;
            AssertWithin(h.Rig.SafeRect(), cardRect, context + ": the card inside the safe area");
            AssertWithin(cardRect, card.NameLabel.worldBound, context + ": the name is in the first view");
            AssertWithin(cardRect, card.StateLabel.worldBound, context + ": the state is in the first view");
            AssertWithin(cardRect, card.UpgradeButton.worldBound, context + ": Upgrade is in the first view, no scrolling needed");
            Assert.That(card.UpgradeButton.worldBound.height, Is.GreaterThanOrEqualTo(48f - Tolerance), context + ": Upgrade is a 48 pt target");
            Assert.That(card.More.scrollOffset.y, Is.EqualTo(0f), context + ": nothing was scrolled to reach Upgrade");
            Assert.That(h.Shell.Root.panel.Pick(card.UpgradeButton.worldBound.center), Is.SameAs(card.UpgradeButton), context + ": Upgrade is the element a tap reaches (not clipped)");
            Assert.That(Intersects(card.NameLabel.worldBound, card.UpgradeButton.worldBound), Is.False, context);
            Assert.That(Intersects(card.StateLabel.worldBound, card.UpgradeButton.worldBound), Is.False, context);
            if (screen.Expected == FactoryHudLayout.CompactPortrait)
            {
                Assert.That(cardRect.height, Is.LessThanOrEqualTo(h.Rig.SafeRect().height * 0.45f + 0.5f), context + ": the 45 % hard cap");
            }
        }

        [UnityTest]
        public IEnumerator EveryCardControlIsAtLeastFortyEightPointsAtTheLargestText([ValueSource(nameof(Screens))] ShellScreen screen, [ValueSource(nameof(Percents))] int percent)
        {
            yield return Make(screen, percent, RunShellFixtures.ComfortableCash);
            h.Build(0, "machine.miner");
            yield return h.OpenCard(0);
            // The scroll area below Upgrade may need a scroll to reach the Details control at the largest text; scroll it into view first.
            h.Card.More.ScrollTo(h.Card.DetailsButton);
            yield return ShellRig.Settle();

            foreach (var button in new[] { h.Card.UpgradeButton, h.Card.DetailsButton, h.Card.CloseButton })
            {
                Assert.That(button.worldBound.width, Is.GreaterThanOrEqualTo(48f - Tolerance), $"{screen} @{percent}%: {button.name} width");
                Assert.That(button.worldBound.height, Is.GreaterThanOrEqualTo(48f - Tolerance), $"{screen} @{percent}%: {button.name} height");
            }
        }

        [UnityTest]
        public IEnumerator DetailsIsReachableWithoutLeavingTheCardAtEveryClass([ValueSource(nameof(Screens))] ShellScreen screen)
        {
            yield return Make(screen, 200, RunShellFixtures.ComfortableCash);
            h.Build(0, "machine.miner");
            yield return h.OpenCard(0);
            h.Card.DetailsButton.Focus();
            yield return ShellRig.Settle();

            // Focus scrolls the control into view (the scroll area below Upgrade), so the keyboard reaches Details at any text scale.
            var viewport = h.Card.More.contentViewport.worldBound;
            Assert.That(viewport.Contains(h.Card.DetailsButton.worldBound.center), Is.True, $"{screen}: the focused Details control is scrolled into the visible area");
            CardHarness.Press(h.Card.DetailsButton);
            yield return ShellRig.Settle();
            Assert.That(h.Shell.ActivePanelId, Is.EqualTo(ShellPanelIds.Machine));
        }

        // ---------------------------------------------------------------------------------------------------
        // Exclusivity, back, notices
        // ---------------------------------------------------------------------------------------------------

        [UnityTest]
        public IEnumerator TheCardIsExclusiveWithTabPanelsSettingsAndTheBuildSheet()
        {
            yield return Make(PhoneScreen(), 100, RunShellFixtures.ComfortableCash);
            h.Build(0, "machine.miner");
            yield return h.OpenCard(0);

            h.Shell.SelectTab(ShellTab.Market);
            yield return ShellRig.Settle();
            Assert.That(h.Shell.IsCardOpen, Is.False, "a tab panel closes the card");
            Assert.That(h.Shell.SelectedSlot, Is.EqualTo(-1));
            Assert.That(h.Shell.ActivePanelId, Is.EqualTo(ShellPanelIds.Market));

            yield return h.OpenCard(0);
            Assert.That(h.Shell.ActivePanelId, Is.Null, "the card closes the sheet: one surface at a time");
            Assert.That(h.Shell.IsSheetOpen, Is.False);

            h.Shell.OpenSettings();
            Assert.That(h.Shell.IsCardOpen, Is.False, "Settings closes the card");
            h.Shell.CloseSheet();

            yield return h.OpenCard(0);
            Assert.That(h.Shell.OpenSlot(5, SlotCondition.Empty), Is.True);
            Assert.That(h.Shell.IsCardOpen, Is.False, "the build sheet closes the card (card and build card are exclusive)");
            Assert.That(h.Shell.ActivePanelId, Is.EqualTo(ShellPanelIds.Build));
        }

        [UnityTest]
        public IEnumerator BackAndEscapeCloseTheCardAndNoticesLeaveUpgradeAndBackUsable([ValueSource(nameof(Screens))] ShellScreen screen)
        {
            yield return Make(screen, 200, RunShellFixtures.ComfortableCash);
            h.Build(0, "machine.miner");
            yield return h.OpenCard(0);
            h.Shell.ShowNotice("Cancelled by rotation. Nothing was spent, placed or confirmed, and the selected location is unchanged.", ShellNoticeKind.Warning);
            h.Card.InlineLabel.text = RunRejectionCopy.For(RunReasons.CashInsufficient);
            h.Card.InlineLabel.style.display = DisplayStyle.Flex;
            yield return ShellRig.Settle();

            // P2-F19: the notice overlays the world column, so it never covers Upgrade or the controls Back reaches.
            Assert.That(Intersects(h.Shell.NoticeBar.worldBound, h.Card.UpgradeButton.worldBound), Is.False, $"{screen}: the notice never covers Upgrade");
            Assert.That(Intersects(h.Shell.NoticeBar.worldBound, h.Card.Root.worldBound), Is.False, $"{screen}: the notice never covers the card");
            Assert.That(h.Shell.HandleBack(), Is.True);
            Assert.That(h.Shell.IsCardOpen, Is.False, $"{screen}: Back closes the card");
            Assert.That(h.Shell.SelectedSlot, Is.EqualTo(-1));
        }

        [UnityTest]
        public IEnumerator DetailsClosesBackToTheCardAndASecondBackClosesTheCard()
        {
            yield return Make(PhoneScreen(), 100, RunShellFixtures.ComfortableCash);
            h.Build(0, "machine.miner");
            yield return h.OpenCard(0);

            CardHarness.Press(h.Card.DetailsButton);
            yield return ShellRig.Settle();
            Assert.That(h.Shell.IsCardOpen, Is.False, "the card hides while Details shows");
            Assert.That(h.Shell.IsDetailsFromCard, Is.True);
            Assert.That(h.Shell.SelectedSlot, Is.EqualTo(0), "the selection stays");

            Assert.That(h.Shell.HandleBack(), Is.True);
            yield return ShellRig.Settle();
            Assert.That(h.Shell.IsCardOpen, Is.True, "Back from Details returns to the card");
            Assert.That(h.Shell.IsSheetOpen, Is.False);
            Assert.That(FocusedElement, Is.SameAs(h.Card.DetailsButton), "focus returns to the control that opened Details");

            h.Shell.HandleBack();
            Assert.That(h.Shell.IsCardOpen, Is.False);
            Assert.That(h.Shell.SelectedSlot, Is.EqualTo(-1));
        }

        [UnityTest]
        public IEnumerator ACardWithoutAnyRegisteredCardFallsBackToTheDetailsSheet()
        {
            yield return Make(PhoneScreen(), 100, RunShellFixtures.ComfortableCash);
            h.Build(0, "machine.miner");
            // A failed card composition leaves the former behaviour: a machine tap opens the machine panel.
            var other = ShellRig.Create(PhoneScreen());
            try
            {
                yield return other.Layout();
                var details = new MachinePanelPresenter(
                    h.Session, h.Commands, UnityEditor.AssetDatabase.LoadAssetAtPath<VisualTreeAsset>("Assets/IdleFactory/Unity/UI/Panels/MachinePanel.uxml"),
                    UnityEditor.AssetDatabase.LoadAssetAtPath<StyleSheet>("Assets/IdleFactory/Unity/UI/Panels/MachinePanel.uss"), ContractsPanelTestSupport.Icon);
                other.Shell.Panels.Register(details);
                Assert.That(other.Shell.HasMachineCard, Is.False);
                Assert.That(other.Shell.OpenSlot(0, SlotCondition.Occupied), Is.True);
                Assert.That(other.Shell.ActivePanelId, Is.EqualTo(ShellPanelIds.Machine));
                Assert.That(other.Shell.IsCardOpen, Is.False);
            }
            finally
            {
                other.Dispose();
            }
        }
    }
}
#endif
