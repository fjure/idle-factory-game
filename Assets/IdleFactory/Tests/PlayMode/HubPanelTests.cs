using System;
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
using UnityEditor;

namespace IdleFactory.Tests.PlayMode
{
    /// <summary>
    /// T535 (FR-020 to FR-022, research R11, OD-9b C3 to C6, P2-F9, P2-F16, P2-F17, P2-F19): the hub. Every registered system has a card, flags show on
    /// Contracts, Research, Prestige and Overdrive only, the entry always shows the word "Office", rows are 48 dp, the hub is exclusive with the card and
    /// the build sheet, return takes one interaction, and the empty, error and disabled states are visible text.
    /// </summary>
    public sealed class HubPanelTests
    {
        private readonly List<HubHarness> harnesses = new List<HubHarness>();
        private readonly List<ShellRig> rigs = new List<ShellRig>();
        private HubHarness h;

        public static IEnumerable<ShellScreen> Screens() => ShellScreens.All();

        public static IEnumerable<int> Percents() => new[] { 100, 200 };

        [TearDown]
        public void TearDown()
        {
            foreach (var item in harnesses)
            {
                item.Dispose();
            }

            foreach (var rig in rigs)
            {
                rig.Dispose();
            }

            harnesses.Clear();
            rigs.Clear();
            h = null;
        }

        private IEnumerator Make(ShellScreen screen, int percent, bool register = true)
        {
            HubHarness made = null;
            yield return HubHarness.Create(screen, percent, built => made = built, register);
            harnesses.Add(made);
            h = made;
        }

        private static ShellScreen Phone() =>
            ShellScreens.All().First(screen => screen.Name.StartsWith("phone-portrait 390", StringComparison.Ordinal));

        private static readonly (HubCard Card, string Id)[] Openable =
        {
            (HubCard.Market, ShellPanelIds.Market), (HubCard.Contracts, ShellPanelIds.Contracts), (HubCard.Items, ShellPanelIds.Items),
            (HubCard.Research, ShellPanelIds.Research), (HubCard.Industry, ShellPanelIds.Industry), (HubCard.Prestige, ShellPanelIds.Prestige),
            (HubCard.Settings, ShellPanelIds.Settings),
        };

        // ---------------------------------------------------------------------------------------------------
        // Cards and registry
        // ---------------------------------------------------------------------------------------------------

        [Test]
        public void EveryRegistrySystemViewHasACardAndTheIndustryIdIsKnown()
        {
            var ids = new HashSet<string>();
            foreach (HubCard card in Enum.GetValues(typeof(HubCard)))
            {
                var id = HubCards.PanelIdOf(card);
                if (id != null)
                {
                    Assert.That(ShellPanelIds.IsKnown(id), Is.True, id);
                    ids.Add(id);
                }
            }

            Assert.That(HubCards.Count, Is.EqualTo(8));
            foreach (var id in new[]
                     {
                         ShellPanelIds.Market, ShellPanelIds.Contracts, ShellPanelIds.Items, ShellPanelIds.Research, ShellPanelIds.Industry,
                         ShellPanelIds.Prestige, ShellPanelIds.Settings,
                     })
            {
                Assert.That(ids, Does.Contain(id), "a card opens " + id);
            }

            Assert.That(ShellPanelIds.IsKnown(ShellPanelIds.Industry), Is.True, "the additive Industry id is known to the registry");
            Assert.That(ShellPanelIds.IsKnown(ShellPanelIds.Hub), Is.True);
            Assert.That(HubCards.PanelIdOf(HubCard.Overdrive), Is.Null, "Overdrive activates and opens no view");
        }

        [UnityTest]
        public IEnumerator EveryCardOpensItsRegisteredPanelAndTheHubShowsEightCards([ValueSource(nameof(Screens))] ShellScreen screen)
        {
            yield return Make(screen, 100);
            CardHarness.Press(h.Shell.HubEntryButton);
            yield return ShellRig.Settle();
            Assert.That(h.Shell.IsHubOpen, Is.True, screen.ToString());
            Assert.That(h.Shell.ActivePanelId, Is.EqualTo(ShellPanelIds.Hub));
            foreach (HubCard card in Enum.GetValues(typeof(HubCard)))
            {
                Assert.That(h.Hub.CardButton(card), Is.Not.Null, card.ToString());
                Assert.That(h.Hub.StatusOf(card), Is.Not.Empty, card + " has a one-line status");
            }

            foreach (var (card, id) in Openable)
            {
                if (!h.Shell.IsHubOpen)
                {
                    h.Shell.OpenHub();
                    yield return ShellRig.Settle();
                }

                CardHarness.Press(h.Hub.CardButton(card));
                yield return ShellRig.Settle();
                Assert.That(h.Shell.ActivePanelId, Is.EqualTo(id), card + " opens " + id + " through the registry");
                Assert.That(h.Shell.IsHubOpen, Is.False);
            }
        }

        [UnityTest]
        public IEnumerator TheResearchCardStatusShowsTheRpValueAndTheIndustryCardShowsIp()
        {
            yield return Make(Phone(), 100);
            h.Shell.OpenHub();
            yield return ShellRig.Settle();
            Assert.That(h.Hub.StatusOf(HubCard.Research), Does.StartWith("RP " + QuantityFormatter.Format(h.Session.ReadRp())));
            Assert.That(h.Hub.StatusOf(HubCard.Industry), Does.StartWith("IP "));
        }

        // ---------------------------------------------------------------------------------------------------
        // Attention flags
        // ---------------------------------------------------------------------------------------------------

        [Test]
        public void OnlyContractsResearchPrestigeAndOverdriveCanCarryAFlag()
        {
            foreach (HubCard card in Enum.GetValues(typeof(HubCard)))
            {
                var expected = card == HubCard.Contracts || card == HubCard.Research || card == HubCard.Prestige || card == HubCard.Overdrive;
                Assert.That(HubCards.CanFlag(card), Is.EqualTo(expected), card.ToString());
            }

            var attention = new HubAttention();
            foreach (HubCard card in Enum.GetValues(typeof(HubCard)))
            {
                attention.Set(card, true);
            }

            attention.SetContracts(1);
            Assert.That(attention.IsFlagged(HubCard.Market), Is.False);
            Assert.That(attention.IsFlagged(HubCard.Items), Is.False);
            Assert.That(attention.IsFlagged(HubCard.Industry), Is.False);
            Assert.That(attention.IsFlagged(HubCard.Settings), Is.False);
            Assert.That(attention.Count, Is.EqualTo(4));
            attention.Clear();
            Assert.That(attention.Any, Is.False);
        }

        [UnityTest]
        public IEnumerator FlagsShowOnTheFourCardsAndTheEntryDotAndNowhereElse()
        {
            yield return Make(Phone(), 100);
            Assert.That(h.Shell.IsHubDotShown, Is.False, "no attention, no dot");
            h.Shell.Attention.SetContracts(2);
            h.Shell.Attention.Set(HubCard.Research, true);
            h.Shell.Attention.Set(HubCard.Prestige, true);
            h.Shell.Attention.Set(HubCard.Overdrive, true);
            h.Shell.Attention.Set(HubCard.Market, true);
            Assert.That(h.Shell.IsHubDotShown, Is.True, "the entry dot shows with the hub closed");
            Assert.That(h.Shell.HubEntryButton.tooltip, Does.Contain("4 systems need attention"));
            Assert.That(h.Shell.ContractsDeliverable, Is.EqualTo(2), "the Contracts tab badge still follows until the tab bar goes (T537)");

            h.Shell.OpenHub();
            yield return ShellRig.Settle();
            foreach (HubCard card in Enum.GetValues(typeof(HubCard)))
            {
                Assert.That(h.Hub.IsFlagShown(card), Is.EqualTo(HubCards.CanFlag(card)), card.ToString());
            }

            Assert.That(h.Hub.StatusOf(HubCard.Contracts), Is.EqualTo("2 ready to deliver"));
            Assert.That(h.Hub.CardButton(HubCard.Contracts).tooltip, Does.Contain("Needs attention"), "the flag is part of the accessible name");
            h.Shell.Attention.Clear();
            Assert.That(h.Shell.IsHubDotShown, Is.False);
        }

        [UnityTest]
        public IEnumerator TheContractsFeedWritesTheHubCardFlag()
        {
            yield return Make(Phone(), 100);
            var feed = new ContractsShellFeed(h.Session, h.Shell);
            h.Shell.Attention.SetContracts(3);
            feed.Invalidate();
            feed.Update();
            Assert.That(h.Shell.Attention.ContractsDeliverable, Is.EqualTo(feed.Deliverable), "the feed output is the hub's Contracts flag");
            Assert.That(h.Shell.Attention.IsFlagged(HubCard.Contracts), Is.EqualTo(feed.Deliverable > 0));
        }

        [UnityTest]
        public IEnumerator TheAttentionFeedDoesNotFlagALockedOverdriveOrANotReadyPrestige()
        {
            yield return Make(Phone(), 100);
            var feed = new HubAttentionFeed(h.Session, h.Card.Commands, h.Shell.Attention);
            feed.Update(100f);
            Assert.That(h.Shell.Attention.IsFlagged(HubCard.Overdrive), Is.False, "Overdrive is locked at factory level 1");
            Assert.That(h.Shell.Attention.IsFlagged(HubCard.Prestige), Is.False);
        }

        // ---------------------------------------------------------------------------------------------------
        // Entry
        // ---------------------------------------------------------------------------------------------------

        [UnityTest]
        public IEnumerator TheEntryAlwaysShowsTheWordOfficeBesideTheIconAtEveryScaleAndClass(
            [ValueSource(nameof(Screens))] ShellScreen screen, [ValueSource(nameof(Percents))] int percent)
        {
            yield return Make(screen, percent);
            var where = screen + " @" + percent + "%";
            var shell = h.Shell;
            var label = shell.HubEntryButton.Q<Label>("hub-entry-label");
            Assert.That(shell.HubEntryText, Is.EqualTo("Office"), where);
            Assert.That(shell.HubEntryText, Is.Not.EqualTo("Systems"));
            Assert.That(IsDisplayed(label), Is.True, where + ": the word is never hidden, not even at 200 percent in the compact strip");
            var natural = label.MeasureTextSize(label.text, 0f, VisualElement.MeasureMode.Undefined, 0f, VisualElement.MeasureMode.Undefined);
            Assert.That(label.contentRect.width, Is.GreaterThanOrEqualTo(natural.x - 1f), where + ": the word is not truncated");
            var icon = shell.HubEntryButton.Q<VisualElement>("hub-entry-icon");
            Assert.That(IsDisplayed(icon), Is.True, where);
            Assert.That(label.worldBound.xMin, Is.GreaterThanOrEqualTo(icon.worldBound.xMax - Tolerance), where + ": the word sits beside the icon");
            Assert.That(shell.HubEntryButton.worldBound.height, Is.GreaterThanOrEqualTo(48f - Tolerance), where + ": 48 dp target");
            Assert.That(shell.HubEntryButton.worldBound.width, Is.GreaterThanOrEqualTo(48f - Tolerance), where);
            AssertWithin(h.Rig.SafeRect(), shell.HubEntryButton.worldBound, where + ": the entry stays on screen");
            AssertShellLayout(h.Rig, "entry");
            Assert.That(shell.HubEntryButton.tooltip, Does.StartWith("Office"), "the accessible name");
        }

        [Test]
        public void TheEntryIconIsOneSwappableReferenceAndTheLabelIsOffice()
        {
            Assert.That(HubCards.EntryIconKey, Is.Not.Empty);
            Assert.That(HubCards.HubLabel, Is.EqualTo("Office"));
            Assert.That(HubCards.TitleOf(HubCard.Contracts), Is.EqualTo("Contracts"));
        }

        // ---------------------------------------------------------------------------------------------------
        // Navigation: exclusivity and return
        // ---------------------------------------------------------------------------------------------------

        [UnityTest]
        public IEnumerator OpeningTheHubClosesTheMachineCardAndRestoresTheSelection()
        {
            yield return Make(Phone(), 100);
            h.Card.Build(0, "machine.miner");
            yield return h.Card.OpenCard(0);
            Assert.That(h.Shell.SelectedSlot, Is.EqualTo(0));
            var selections = new List<int>();
            h.Shell.SlotSelectionChanged += selections.Add;

            h.Shell.OpenHub();
            yield return ShellRig.Settle();

            Assert.That(h.Shell.IsCardOpen, Is.False, "exclusive with the card (C3)");
            Assert.That(h.Shell.SelectedSlot, Is.EqualTo(-1), "the selection clears, so the camera restores its prior view");
            Assert.That(selections, Does.Contain(-1));
            Assert.That(h.Shell.IsHubOpen, Is.True);

            // And the other way: a machine tap replaces the hub.
            Assert.That(h.Shell.OpenSlot(0, SlotCondition.Occupied), Is.True);
            Assert.That(h.Shell.IsHubOpen, Is.False);
            Assert.That(h.Shell.IsCardOpen, Is.True);
        }

        [UnityTest]
        public IEnumerator OpeningTheHubClosesTheBuildSheetAndADetailsView()
        {
            yield return Make(Phone(), 100);
            Assert.That(h.Shell.OpenSlot(1, SlotCondition.Empty), Is.True);
            Assert.That(h.Shell.ActivePanelId, Is.EqualTo(ShellPanelIds.Build));
            h.Shell.OpenHub();
            yield return ShellRig.Settle();
            Assert.That(h.Shell.ActivePanelId, Is.EqualTo(ShellPanelIds.Hub));
            Assert.That(h.Shell.SelectedSlot, Is.EqualTo(-1));

            h.Card.Build(0, "machine.miner");
            yield return h.Card.OpenCard(0);
            Assert.That(h.Shell.OpenDetails(0), Is.True);
            h.Shell.OpenHub();
            yield return ShellRig.Settle();
            Assert.That(h.Shell.IsHubOpen, Is.True);
            Assert.That(h.Shell.IsDetailsFromCard, Is.False);
            Assert.That(h.Shell.IsCardOpen, Is.False);
        }

        [UnityTest]
        public IEnumerator ReturningToTheFactoryTakesOneInteractionFromTheHubAndFromEverySystemView([ValueSource(nameof(Screens))] ShellScreen screen)
        {
            yield return Make(screen, 100);
            h.Shell.OpenHub();
            yield return ShellRig.Settle();
            Assert.That(h.Shell.HandleBack(), Is.True);
            Assert.That(h.Shell.IsSheetOpen, Is.False, screen + ": one Back from the hub is the factory");

            foreach (var (card, id) in Openable)
            {
                yield return null;
                h.Shell.OpenHub();
                yield return ShellRig.Settle();
                CardHarness.Press(h.Hub.CardButton(card));
                yield return ShellRig.Settle();
                Assert.That(h.Shell.ActivePanelId, Is.EqualTo(id));
                Assert.That(h.Shell.HandleBack(), Is.True, card + ": Back");
                Assert.That(h.Shell.IsSheetOpen, Is.False, card + ": one Back returns to the factory");
                Assert.That(h.Shell.ActivePanelId, Is.Null);
            }

            h.Shell.OpenHub();
            CardHarness.Press(h.Shell.CloseButton);
            Assert.That(h.Shell.IsSheetOpen, Is.False, "Close is one interaction too");
            h.Shell.OpenHub();
            CardHarness.Press(h.Shell.HubEntryButton);
            Assert.That(h.Shell.IsSheetOpen, Is.False, "the entry toggles the hub closed");
        }

        [UnityTest]
        public IEnumerator FocusMovesIntoTheHubAndReturnsToTheEntryWhenItCloses()
        {
            yield return Make(Phone(), 100);
            h.Shell.HubEntryButton.Focus();
            h.Shell.OpenHub();
            for (var i = 0; i < 6; i++)
            {
                yield return null;
            }

            var focused = h.Shell.Root.panel.focusController.focusedElement as VisualElement;
            Assert.That(focused, Is.Not.Null);
            Assert.That(h.Hub.Root.Contains(focused), Is.True, "focus moves into the hub");
            h.Shell.CloseSheet();
            yield return new WaitForSecondsRealtime(0.5f);

            Assert.That(h.Shell.Root.panel.focusController.focusedElement, Is.SameAs(h.Shell.HubEntryButton), "focus returns to the invoker");
        }

        // ---------------------------------------------------------------------------------------------------
        // Layout and accessibility
        // ---------------------------------------------------------------------------------------------------

        [UnityTest]
        public IEnumerator EveryCardIsAFortyEightDpRowThatFitsAtEveryClassAndScale(
            [ValueSource(nameof(Screens))] ShellScreen screen, [ValueSource(nameof(Percents))] int percent)
        {
            yield return Make(screen, percent);
            h.Shell.OpenHub();
            yield return ShellRig.Settle();
            var where = screen + " @" + percent + "%";
            AssertShellLayout(h.Rig, "hub");
            var viewport = h.Shell.PanelScroll.Q<VisualElement>(className: "unity-scroll-view__content-viewport").worldBound;
            foreach (HubCard card in Enum.GetValues(typeof(HubCard)))
            {
                var button = h.Hub.CardButton(card);
                Assert.That(button.worldBound.height, Is.GreaterThanOrEqualTo(48f - Tolerance), where + ": " + card + " is 48 dp");
                Assert.That(button.worldBound.xMin, Is.GreaterThanOrEqualTo(viewport.xMin - Tolerance), where + ": " + card + " left");
                Assert.That(button.worldBound.xMax, Is.LessThanOrEqualTo(viewport.xMax + Tolerance), where + ": " + card + " right");
                foreach (var suffix in new[] { "-title", "-status" })
                {
                    var text = button.Q<Label>(button.name + suffix);
                    var measured = text.MeasureTextSize(text.text, text.contentRect.width, VisualElement.MeasureMode.Exactly, 0f, VisualElement.MeasureMode.Undefined);
                    Assert.That(text.contentRect.height, Is.GreaterThanOrEqualTo(measured.y - 1f), where + ": " + card + suffix + " is not squeezed");
                }

                Assert.That(button.tooltip, Is.Not.Empty, where + ": " + card + " has an accessible name");
            }

            if (!h.Shell.IsSidePanelLayout)
            {
                // The bottom sheet has its Close; the side panel is left with Back, the entry (toggle) or the Factory tab.
                Assert.That(IsDisplayed(h.Shell.CloseButton), Is.True, where);
                Assert.That(h.Shell.CloseButton.worldBound.height, Is.GreaterThanOrEqualTo(44f - Tolerance), where);
            }

            Assert.That(IsDisplayed(h.Hub.RetryButton), Is.False, "no Retry while the cards show");
            Assert.That(h.Hub.Root.Query<Button>().ToList().Any(button => button.text == "Back"), Is.False, "the hub root offers no Back (P2-F19)");
        }

        [UnityTest]
        public IEnumerator ThreeNoticesLeaveTheCardsAndTheCloseControlUsableAtTheWorstCase()
        {
            var worst = ShellScreens.All().First(screen => screen.Name.StartsWith("phone-landscape 640x360", StringComparison.Ordinal));
            yield return Make(worst, 200);
            h.Shell.OpenHub();
            yield return ShellRig.Settle();
            // The shell shows one notice at a time (the scaffold's own layer holds up to three, which the hub does not use); with the detail line and the
            // hint that is up to three bars over the world. Each kind at the worst-case length leaves the layout, the cards and Close usable.
            h.Shell.CashButton.Focus();
            foreach (var kind in new[] { ShellNoticeKind.Info, ShellNoticeKind.Warning, ShellNoticeKind.Error })
            {
                h.Shell.ShowNotice("Slot 5 unlocks at factory level 2.", kind);
                yield return ShellRig.Settle();
                AssertShellLayout(h.Rig, "hub with a " + kind + " notice");
            }

            Assert.That(h.Shell.IsHubOpen, Is.True);
            Assert.That(IsDisplayed(h.Shell.CloseButton), Is.True);
            var notice = h.Shell.NoticeBar.worldBound;
            Assert.That(Intersects(notice, h.Shell.CloseButton.worldBound), Is.False, "the notice does not cover Close");
            Assert.That(Intersects(notice, h.Shell.PanelHost.worldBound), Is.False, "the notice does not cover the hub");
            CardHarness.Press(h.Hub.CardButton(HubCard.Market));
            Assert.That(h.Shell.ActivePanelId, Is.EqualTo(ShellPanelIds.Market), "a card still works under notices");
        }

        [UnityTest]
        public IEnumerator RotationKeepsTheHubOpenAndSwitchesItsContainer()
        {
            var portrait = Phone();
            yield return Make(portrait, 100);
            h.Shell.OpenHub();
            yield return ShellRig.Settle();
            Assert.That(h.Shell.IsSidePanelLayout, Is.False, "phone portrait: the bottom sheet");
            yield return h.Rig.Layout(ShellScreens.Rotated(portrait));
            Assert.That(h.Shell.IsHubOpen, Is.True, "rotation does not close the hub");
            // The rig keeps its panel size across the relayout, so the strip of the rotated class is not measured here (the 844x390 class is covered
            // by EveryCardIsAFortyEightDpRowThatFitsAtEveryClassAndScale).
            var tablet = ShellScreens.All().First(screen => screen.Name.StartsWith("tablet-landscape", StringComparison.Ordinal));
            yield return h.Rig.Layout(tablet);
            Assert.That(h.Shell.IsSidePanelLayout, Is.True, "tablet: the side panel");
            Assert.That(h.Shell.IsHubOpen, Is.True);
        }

        // ---------------------------------------------------------------------------------------------------
        // Disabled, empty and error states
        // ---------------------------------------------------------------------------------------------------

        [UnityTest]
        public IEnumerator ALockedOverdriveCardIsDisabledWithItsReasonAsVisibleText()
        {
            yield return Make(Phone(), 100);
            h.Shell.OpenHub();
            yield return ShellRig.Settle();
            var card = h.Hub.CardButton(HubCard.Overdrive);
            Assert.That(PanelScaffold.IsDisabled(card), Is.True);
            Assert.That(h.Hub.StatusOf(HubCard.Overdrive), Is.EqualTo("Locked"));
            Assert.That(h.Hub.ReasonOf(HubCard.Overdrive), Is.Not.Empty, "the reason is visible text for touch users (P2-F19)");
            Assert.That(card.tooltip, Does.Contain("Unavailable"));
            Assert.That(card.focusable, Is.True, "soft-disabled controls stay focusable");
            Assert.That(h.Hub.Activate(HubCard.Overdrive), Is.False);
            Assert.That(h.Shell.IsHubOpen, Is.True, "nothing changed");
        }

        [UnityTest]
        public IEnumerator AMissingSystemPanelDisablesItsCardWithAReason()
        {
            yield return Make(Phone(), 100, false);
            h.Shell.OpenHub();
            yield return ShellRig.Settle();
            Assert.That(PanelScaffold.IsDisabled(h.Hub.CardButton(HubCard.Market)), Is.False, "Market is registered by the card harness");
            Assert.That(PanelScaffold.IsDisabled(h.Hub.CardButton(HubCard.Research)), Is.True);
            Assert.That(h.Hub.StatusOf(HubCard.Research), Is.EqualTo("Not available right now"));
            Assert.That(h.Hub.ReasonOf(HubCard.Research), Is.Not.Empty);
        }

        [UnityTest]
        public IEnumerator WithoutAnySystemPanelTheHubShowsTheEmptyStateWithoutBackOrRetry()
        {
            var rig = ShellRig.Create(Phone());
            rigs.Add(rig);
            var session = RunShellFixtures.NewSession(out var commands);
            var hub = HubHarness.NewHub(session, commands);
            rig.Shell.Panels.Register(hub);
            yield return rig.Layout();
            rig.Shell.OpenHub();
            yield return ShellRig.Settle();

            Assert.That(hub.StateKind, Is.EqualTo("empty"));
            Assert.That(hub.StateTitleLabel.text, Is.Not.Empty);
            Assert.That(IsDisplayed(hub.RetryButton), Is.False);
            Assert.That(hub.Root.Query<Button>().ToList().Any(button => button.text == "Back" && IsDisplayed(button)), Is.False);
            Assert.That(rig.Shell.IsHubOpen, Is.True);
            Assert.That(rig.Shell.HandleBack(), Is.True, "the sheet still closes with Back");
        }

        [UnityTest]
        public IEnumerator AReadFailureShowsTheErrorStateWithRetryAndNoBack()
        {
            yield return Make(Phone(), 100);
            var failing = true;
            h.Hub.FailureProbe = () => failing;
            h.Shell.OpenHub();
            yield return ShellRig.Settle();
            Assert.That(h.Hub.StateKind, Is.EqualTo("error"));
            Assert.That(h.Hub.StateTitleLabel.text, Is.EqualTo("Something went wrong"));
            Assert.That(IsDisplayed(h.Hub.RetryButton), Is.True);
            Assert.That(h.Hub.RetryButton.worldBound.height, Is.GreaterThanOrEqualTo(48f - Tolerance));
            Assert.That(h.Hub.Root.Query<Button>().ToList().Any(button => button.text == "Back" && IsDisplayed(button)), Is.False,
                "the hub root offers no Back (P2-F19)");

            failing = false;
            CardHarness.Press(h.Hub.RetryButton);
            yield return ShellRig.Settle();
            Assert.That(h.Hub.StateKind, Is.Null, "Retry recovers");
            Assert.That(IsDisplayed(h.Hub.CardButton(HubCard.Market)), Is.True);
        }

        [UnityTest]
        public IEnumerator TheScaffoldStateCardOffersNoBackWhenTheRootHidesIt()
        {
            yield return Make(Phone(), 100);
            var tree = HubHarness.Load<VisualTreeAsset>("PanelScaffold.uxml");
            var scaffold = new PanelScaffold(tree, HubHarness.Load<StyleSheet>("PanelScaffold.uss"), "Root", () => { });
            h.Shell.PanelContent.Add(scaffold.Root);
            scaffold.BackVisible = false;
            scaffold.ShowEmpty("Nothing", "Nothing here.");
            Assert.That(scaffold.StateSecondaryButton.style.display.value, Is.EqualTo(DisplayStyle.None), "P2-F19: the state card follows BackVisible");
            Assert.That(scaffold.StateSecondaryButton.focusable, Is.False);
            scaffold.BackVisible = true;
            Assert.That(scaffold.StateSecondaryButton.style.display.value, Is.EqualTo(DisplayStyle.Flex));
            Assert.That(scaffold.Sheet, Is.Not.Null, "P2-F17: the scaffold has a sheet mount point");
            Assert.That(PanelScaffold.RowClass, Is.EqualTo("scaffold-row"));
            scaffold.Root.RemoveFromHierarchy();
        }
    }
}
#endif
