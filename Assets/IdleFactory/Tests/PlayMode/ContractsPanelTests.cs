#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using IdleFactory.Run.Application;
using IdleFactory.Unity.UI;
using IdleFactory.Unity.UI.Panels;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;
using static IdleFactory.Tests.PlayMode.ContractsPanelTestSupport;
using static IdleFactory.Tests.PlayMode.RunShellLayoutAssertions;

namespace IdleFactory.Tests.PlayMode
{
    /// <summary>
    /// T115 (FR-014, FR-015, FR-032, US2 scenarios 2 and 3, UX-2, UX-3, UX-5): the Contracts surface in its normal, empty, disabled and
    /// error states, the countdown, delivery at the deadline instant, the expiry card, the Abandon confirmation with its focus
    /// behaviour and its cancel on rotation, and the worst-case layout at every screen class and at 100 and 200 percent text scale.
    /// </summary>
    public sealed class ContractsPanelTests
    {
        private readonly List<ShellRig> rigs = new List<ShellRig>();
        private ShellRig rig;
        private RunSession session;
        private RunCommands commands;
        private ContractsPanel panel;

        public static IEnumerable<ShellScreen> Screens() => ShellScreens.All();

        public static IEnumerable<int> Percents() => new[] { 100, 200 };

        public static IEnumerable<ShellScreen> Representative()
        {
            var all = ShellScreens.All().ToList();
            yield return all.First(screen => screen.Name.StartsWith("phone-portrait 360", System.StringComparison.Ordinal));
            yield return all.First(screen => screen.Name.StartsWith("phone-landscape 640", System.StringComparison.Ordinal));
            yield return all.First(screen => screen.Name.StartsWith("tablet-portrait", System.StringComparison.Ordinal));
            yield return all.First(screen => screen.Name.StartsWith("desktop 1280", System.StringComparison.Ordinal));
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var item in rigs)
            {
                item.Dispose();
            }

            rigs.Clear();
            rig = null;
            panel = null;
        }

        private IEnumerator Open(ShellScreen screen, int percent = 100, bool unlock = true)
        {
            rig = ShellRig.Create(screen, null, percent);
            rigs.Add(rig);
            yield return rig.Layout();
            session = NewSession(out commands);
            if (unlock)
            {
                UnlockContracts(session);
            }

            panel = new ContractsPanel(session, commands, Styles("ContractsPanel.uss"), Icon);
            rig.Shell.Panels.Register(panel);
            rig.Shell.SelectTab(ShellTab.Contracts);
            yield return ShellRig.Settle();
        }

        /// <summary>Redraws the panel after a state change made outside it, then lets the layout settle.</summary>
        private IEnumerator Sync()
        {
            panel.Invalidate();
            panel.Refresh();
            yield return ShellRig.Settle();
        }

        private static bool Shown(VisualElement element) => IsDisplayed(element);

        // ---------------------------------------------------------------------------------------------------
        // Normal
        // ---------------------------------------------------------------------------------------------------

        [UnityTest]
        public IEnumerator TheShellMountsThePanelUnderTheContractsTab([ValueSource(nameof(Representative))] ShellScreen screen)
        {
            yield return Open(screen);
            Assert.That(rig.Shell.ActivePanelId, Is.EqualTo(ShellPanelIds.Contracts));
            Assert.That(panel.Id, Is.EqualTo(ShellPanelIds.Contracts));
            Assert.That(panel.Root.parent, Is.SameAs(rig.Shell.PanelContent));
            Assert.That(Shown(panel.Root), Is.True);
        }

        [UnityTest]
        public IEnumerator OffersShowKindRequiredUnitsRewardsAndDuration([ValueSource(nameof(Representative))] ShellScreen screen)
        {
            yield return Open(screen);
            Assert.That(Shown(panel.EmptyLabel), Is.False);
            Assert.That(Shown(panel.ActiveCard), Is.False);
            Assert.That(panel.OfferIds, Is.EquivalentTo(new[] { Gear, Plate, TimedBar }));

            Assert.That(panel.OfferRequirement(Plate).text, Is.EqualTo("Deliver 100 Iron Plate"));
            Assert.That(panel.OfferDuration(Plate).text, Is.EqualTo("No time limit."));
            Assert.That(panel.OfferRewards(Plate).text, Does.Contain("1,280 Cash").And.Contain("4 RP"));
            Assert.That(panel.OfferRewards(Plate).text, Does.Not.Contain("Flux Catalyst"), "the Item is due on later completions only");

            Assert.That(panel.OfferRequirement(TimedBar).text, Is.EqualTo("Deliver 100 Iron Bar"));
            Assert.That(panel.OfferDuration(TimedBar).text, Is.EqualTo("Time limit: 4:00"));
            Assert.That(panel.OfferRewards(TimedBar).text, Does.Contain("510 Cash").And.Contain("2 RP").And.Contain("Diamond Drill Head x1"));

            foreach (var id in panel.OfferIds)
            {
                Assert.That(RunShellDocument.IsSoftDisabled(panel.AcceptButton(id)), Is.False, id + " can be accepted");
                Assert.That(Shown(panel.OfferReason(id)), Is.False, id + " has no reason to show");
            }
        }

        [UnityTest]
        public IEnumerator AcceptingAnOfferShowsTheActiveContractAndDisablesEveryAccept()
        {
            yield return Open(ShellScreens.All().First());
            Submit(panel.AcceptButton(Plate));
            yield return ShellRig.Settle();

            Assert.That(session.State.Contracts.Active?.OfferId, Is.EqualTo(Plate));
            Assert.That(Shown(panel.ActiveCard), Is.True);
            Assert.That(panel.ActiveRequirement.text, Is.EqualTo("Deliver 100 Iron Plate"));
            Assert.That(panel.ActiveDeadline.text, Is.EqualTo("No time limit."));
            Assert.That(Shown(panel.AbandonButton), Is.True, "an untimed contract can be abandoned");
            foreach (var id in panel.OfferIds)
            {
                Assert.That(RunShellDocument.IsSoftDisabled(panel.AcceptButton(id)), Is.True, id + " is disabled while a contract is active");
                Assert.That(panel.OfferReason(id).text, Is.EqualTo(RunRejectionCopy.For(RunReasons.ContractActiveExists)), id + " says why");
            }
        }

        [UnityTest]
        public IEnumerator ProgressFollowsTheStockAndDeliverEnablesOnceTheStockCovers()
        {
            yield return Open(ShellScreens.All().First());
            Submit(panel.AcceptButton(Plate));
            AddStock(session, "iron.plate", 40);
            yield return Sync();

            Assert.That(panel.ActiveProgress.text, Is.EqualTo("In stock: 40 of 100"));
            Assert.That(panel.ActiveMissing.text, Is.EqualTo("Missing 60 Iron Plate."));
            Assert.That(RunShellDocument.IsSoftDisabled(panel.DeliverButton), Is.True);
            Assert.That(panel.DeliverButton.text, Does.Contain("missing 60"));

            AddStock(session, "iron.plate", 60);
            yield return Sync();
            Assert.That(panel.ActiveProgress.text, Is.EqualTo("In stock: 100 of 100"));
            Assert.That(RunShellDocument.IsSoftDisabled(panel.DeliverButton), Is.False);
            Assert.That(panel.DeliverButton.text, Is.EqualTo("Deliver"));
            Assert.That(panel.ActiveMissing.text, Is.EqualTo("Ready to deliver."));
        }

        [UnityTest]
        public IEnumerator DeliveringGrantsTheRewardsAndShowsTheOutcome()
        {
            yield return Open(ShellScreens.All().First());
            Submit(panel.AcceptButton(Plate));
            AddStock(session, "iron.plate", 100);
            yield return Sync();
            var cash = session.State.Cash;
            var rp = session.State.Rp;

            panel.DeliverButton.Focus();
            Submit(panel.DeliverButton);
            yield return ShellRig.Settle();

            Assert.That(session.State.Cash, Is.EqualTo(cash + 1280));
            Assert.That(session.State.Rp, Is.EqualTo(rp + 4));
            Assert.That(session.State.Inventory.Units("iron.plate"), Is.Zero);
            Assert.That(session.State.Contracts.Active, Is.Null);
            Assert.That(Shown(panel.ActiveCard), Is.False);
            Assert.That(rig.Shell.NoticeText, Does.Contain("Delivered 100").And.Contain("1,280 Cash").And.Contain("4 RP"));
            Assert.That(panel.Snapshot.Offers.First(offer => offer.OfferId == Plate).CompletionCount, Is.EqualTo(1));
            var focused = Focused(rig.Root);
            Assert.That(focused, Is.Not.Null, "focus is not lost when the delivered contract disappears");
            Assert.That(Shown(focused), Is.True);
        }

        [UnityTest]
        public IEnumerator AnItemRewardIsNamedOnTheCompletionIndexThatGrantsIt()
        {
            yield return Open(ShellScreens.All().First());
            Submit(panel.AcceptButton(Plate));
            AddStock(session, "iron.plate", 100);
            Submit(panel.DeliverButton);
            yield return Sync();
            Assert.That(panel.OfferRewards(Plate).text, Does.Contain("Flux Catalyst x1"), "completion 1 grants the Item");
        }

        // ---------------------------------------------------------------------------------------------------
        // Empty
        // ---------------------------------------------------------------------------------------------------

        [UnityTest]
        public IEnumerator WithNoUnlockedOfferTheEmptyStateNamesWhatUnlocksContracts([ValueSource(nameof(Representative))] ShellScreen screen)
        {
            yield return Open(screen, 100, unlock: false);
            Assert.That(Shown(panel.EmptyLabel), Is.True);
            Assert.That(panel.EmptyLabel.text, Does.StartWith("Contracts unlock after "));
            Assert.That(panel.EmptyLabel.text, Does.Contain("Iron Plate"));
            Assert.That(Shown(panel.OffersSection), Is.False);
            Assert.That(Shown(panel.ActiveCard), Is.False);
            Assert.That(Shown(panel.ExpiredCard), Is.False);
        }

        [UnityTest]
        public IEnumerator UnlockingAnOfferReplacesTheEmptyStateAndKeepsLockedOffersWithTheirRequirement()
        {
            yield return Open(ShellScreens.All().First(), 100, unlock: false);
            UnlockContracts(session, Plate);
            yield return Sync();
            Assert.That(Shown(panel.EmptyLabel), Is.False);
            Assert.That(Shown(panel.OffersSection), Is.True);
            Assert.That(RunShellDocument.IsSoftDisabled(panel.AcceptButton(Plate)), Is.False);
            Assert.That(RunShellDocument.IsSoftDisabled(panel.AcceptButton(Gear)), Is.True);
            Assert.That(panel.OfferReason(Gear).text, Does.StartWith("Locked: unlocks after you produce Gear"));
        }

        // ---------------------------------------------------------------------------------------------------
        // Disabled and error
        // ---------------------------------------------------------------------------------------------------

        [UnityTest]
        public IEnumerator DeliverStaysDisabledWithTheMissingUnitsAndATapIsRejectedWithItsReason()
        {
            yield return Open(ShellScreens.All().First());
            Submit(panel.AcceptButton(Plate));
            AddStock(session, "iron.plate", 99);
            yield return Sync();
            var cash = session.State.Cash;

            Assert.That(RunShellDocument.IsSoftDisabled(panel.DeliverButton), Is.True);
            Assert.That(panel.ActiveMissing.text, Is.EqualTo("Missing 1 Iron Plate."));
            Submit(panel.DeliverButton);
            yield return ShellRig.Settle();

            Assert.That(Shown(panel.ActiveFeedback), Is.True);
            Assert.That(panel.ActiveFeedback.text, Is.EqualTo(RunRejectionCopy.For(RunReasons.StockInsufficient)));
            Assert.That(session.State.Cash, Is.EqualTo(cash), "a rejected delivery changes nothing");
            Assert.That(session.State.Contracts.Active, Is.Not.Null);
            Assert.That(session.State.Inventory.Units("iron.plate"), Is.EqualTo(99));
        }

        [UnityTest]
        public IEnumerator TheLockedRequirementShowsTheLockBesideItsTextAtEveryScreenClass([ValueSource(nameof(Screens))] ShellScreen screen, [ValueSource(nameof(Percents))] int percent)
        {
            yield return Open(screen, percent, unlock: false);
            AssertIconBesideText(panel.EmptyLabel, PanelIcons.Lock, $"{screen} @{percent}% empty state");
            AssertShellLayout(rig, "contracts empty state with the lock icon");

            UnlockContracts(session, Plate);
            yield return Sync();
            AssertIconBesideText(panel.OfferReason(Gear), PanelIcons.Lock, $"{screen} @{percent}% locked offer");
            AssertTextButtonsHaveNoChildren(panel.Root, $"{screen} @{percent}%");
        }

        [UnityTest]
        public IEnumerator RejectionTextsShowTheWarningBesideThemAtEveryScreenClass([ValueSource(nameof(Screens))] ShellScreen screen, [ValueSource(nameof(Percents))] int percent)
        {
            yield return Open(screen, percent);
            Submit(panel.AcceptButton(Plate));
            AddStock(session, "iron.plate", 99);
            yield return Sync();
            Submit(panel.DeliverButton);
            yield return ShellRig.Settle();
            var where = $"{screen} @{percent}%";
            AssertIconBesideText(panel.ActiveFeedback, PanelIcons.Warning, where + " deliver rejection");

            // Every command clears the earlier rejection, so the accept rejection follows on its own.
            Submit(panel.AcceptButton(Gear));
            yield return ShellRig.Settle();
            AssertIconBesideText(panel.OfferFeedback(Gear), PanelIcons.Warning, where + " accept rejection");
            AssertIconBesideText(panel.OfferReason(Gear), PanelIcons.Warning, where + " blocked offer reason");
            AssertTextButtonsHaveNoChildren(panel.Root, where);
            AssertShellLayout(rig, "contracts with warning icons");
        }

        [UnityTest]
        public IEnumerator AcceptIsRejectedWhileAContractIsActiveAndTheStateIsUnchanged()
        {
            yield return Open(ShellScreens.All().First());
            Submit(panel.AcceptButton(Plate));
            yield return ShellRig.Settle();
            Submit(panel.AcceptButton(Gear));
            yield return ShellRig.Settle();

            Assert.That(session.State.Contracts.Active.OfferId, Is.EqualTo(Plate));
            Assert.That(Shown(panel.OfferFeedback(Gear)), Is.True);
            Assert.That(panel.OfferFeedback(Gear).text, Is.EqualTo(RunRejectionCopy.For(RunReasons.ContractActiveExists)));
        }

        [UnityTest]
        public IEnumerator ALockedOfferCannotBeAcceptedAndShowsItsReason()
        {
            yield return Open(ShellScreens.All().First(), 100, unlock: false);
            UnlockContracts(session, Plate);
            yield return Sync();
            Submit(panel.AcceptButton(Gear));
            yield return ShellRig.Settle();
            Assert.That(session.State.Contracts.Active, Is.Null, "a locked offer is rejected");
            Assert.That(panel.OfferFeedback(Gear).text, Is.EqualTo(RunRejectionCopy.For(RunReasons.CommandInvalid)));
        }

        [UnityTest]
        public IEnumerator AnAutoSoldContractResourceIsExplainedOnTheActiveContract()
        {
            yield return Open(ShellScreens.All().First());
            Submit(panel.AcceptButton(Plate));
            Assert.That(session.State.TrySetAutoSell("iron.plate", true), Is.EqualTo(IdleFactory.Run.Domain.RunStateResult.Ok));
            yield return Sync();
            Assert.That(Shown(panel.ActiveHint), Is.True);
            Assert.That(panel.ActiveHint.text, Does.Contain("Auto-sell is on for Iron Plate"));
        }

        // ---------------------------------------------------------------------------------------------------
        // Countdown, deadline instant and expiry
        // ---------------------------------------------------------------------------------------------------

        [UnityTest]
        public IEnumerator ATimedContractCountsDownOncePerSecondAndHasNoAbandon()
        {
            yield return Open(ShellScreens.All().First());
            Submit(panel.AcceptButton(TimedBar));
            yield return ShellRig.Settle();
            Assert.That(panel.ActiveDeadline.text, Is.EqualTo("Time left: 4:00"));
            Assert.That(Shown(panel.AbandonButton), Is.False, "a timed contract only expires");

            Advance(session, 1000);
            panel.Refresh();
            Assert.That(panel.ActiveDeadline.text, Is.EqualTo("Time left: 3:59"));

            // Within the same second the panel does not redraw: a marker written to the label survives.
            panel.ActiveDeadline.text = "marker";
            Advance(session, 100);
            panel.Refresh();
            Assert.That(panel.ActiveDeadline.text, Is.EqualTo("marker"), "the countdown updates at 1 Hz, not every frame");
            Advance(session, 900);
            panel.Refresh();
            Assert.That(panel.ActiveDeadline.text, Is.EqualTo("Time left: 3:58"));
        }

        [UnityTest]
        public IEnumerator DeliveryAtTheDeadlineInstantSucceeds()
        {
            yield return Open(ShellScreens.All().First());
            AddStock(session, "iron.bar", 100);
            Submit(panel.AcceptButton(TimedBar));
            Advance(session, 240_000);
            yield return Sync();

            Assert.That(session.TimelineUtcMs, Is.EqualTo(session.State.Contracts.Active.DeadlineUtcMs.Value), "the clock is exactly on the deadline");
            Assert.That(panel.ActiveDeadline.text, Is.EqualTo("Time left: 0:00"));
            Assert.That(RunShellDocument.IsSoftDisabled(panel.DeliverButton), Is.False, "the deadline is inclusive");
            Submit(panel.DeliverButton);
            yield return ShellRig.Settle();

            Assert.That(session.State.Contracts.Active, Is.Null);
            Assert.That(session.State.Contracts.LastExpired, Is.Null);
            Assert.That(Shown(panel.ExpiredCard), Is.False);
            Assert.That(rig.Shell.NoticeText, Does.StartWith("Delivered 100"));
        }

        [UnityTest]
        public IEnumerator OneMillisecondAfterTheDeadlineTheContractExpiresWithNoRewardAndNoPenalty()
        {
            yield return Open(ShellScreens.All().First());
            AddStock(session, "iron.bar", 100);
            Submit(panel.AcceptButton(TimedBar));
            var cash = session.State.Cash;
            Advance(session, 240_001);
            yield return Sync();

            Assert.That(session.State.Contracts.Active, Is.Null);
            Assert.That(Shown(panel.ExpiredCard), Is.True, "the outcome card replaces the active contract");
            Assert.That(panel.ExpiredLine.text, Does.Contain("No reward, no penalty.").And.Contain("100 Iron Bar"));
            Assert.That(session.State.Cash, Is.EqualTo(cash));
            Assert.That(session.State.Inventory.Units("iron.bar"), Is.EqualTo(100), "the stock is kept");
            Assert.That(Shown(panel.ActiveCard), Is.False);
            Assert.That(RunShellDocument.IsSoftDisabled(panel.AcceptButton(TimedBar)), Is.False);

            Submit(panel.AcceptButton(Plate));
            yield return ShellRig.Settle();
            Assert.That(Shown(panel.ExpiredCard), Is.False, "accepting an offer clears the kept outcome");
        }

        [UnityTest]
        public IEnumerator ADeliverTapAfterTheDeadlineIsRejectedWithTheExpiredReason()
        {
            yield return Open(ShellScreens.All().First());
            AddStock(session, "iron.bar", 100);
            Submit(panel.AcceptButton(TimedBar));
            Advance(session, 240_000);
            yield return Sync();
            Advance(session, 1);
            // The panel still shows the instant before; the command re-validates.
            Submit(panel.DeliverButton);
            yield return ShellRig.Settle();
            Assert.That(session.State.Contracts.Active, Is.Null);
            Assert.That(session.State.Cash, Is.EqualTo(25));
            Assert.That(Shown(panel.ExpiredCard), Is.True);
        }

        // ---------------------------------------------------------------------------------------------------
        // Abandon, its confirmation and focus (UX-2, UX-5)
        // ---------------------------------------------------------------------------------------------------

        [UnityTest]
        public IEnumerator AbandonAsksForAConfirmationThatStatesNoRewardAndNoPenaltyAndOpensOnCancel([ValueSource(nameof(Representative))] ShellScreen screen)
        {
            yield return Open(screen);
            Submit(panel.AcceptButton(Plate));
            yield return ShellRig.Settle();
            panel.AbandonButton.Focus();
            Submit(panel.AbandonButton);
            yield return ShellRig.Settle();

            var shell = rig.Shell;
            Assert.That(shell.IsConfirmationOpen, Is.True);
            Assert.That(panel.IsAbandonConfirmationOpen, Is.True);
            Assert.That(session.State.Contracts.Active, Is.Not.Null, "nothing is abandoned before the confirmation");
            Assert.That(DialogText(shell), Does.Contain("no reward and no penalty"));
            Assert.That(shell.DialogConfirmButton.ClassListContains("run-button--destructive"), Is.True);
            Assert.That(Focused(rig.Root), Is.SameAs(shell.DialogCancelButton), "the dialog opens with focus on Cancel (UX-2)");
        }

        [UnityTest]
        public IEnumerator CancellingTheConfirmationKeepsTheContractAndReturnsFocusToAbandon()
        {
            yield return Open(ShellScreens.All().First());
            Submit(panel.AcceptButton(Plate));
            yield return ShellRig.Settle();
            panel.AbandonButton.Focus();
            Submit(panel.AbandonButton);
            yield return ShellRig.Settle();

            Submit(rig.Shell.DialogCancelButton);
            yield return ShellRig.Settle();
            Assert.That(rig.Shell.IsConfirmationOpen, Is.False);
            Assert.That(panel.IsAbandonConfirmationOpen, Is.False);
            Assert.That(session.State.Contracts.Active, Is.Not.Null);
            Assert.That(Focused(rig.Root), Is.SameAs(panel.AbandonButton), "focus returns to the invoking control (UX-2)");

            // Escape and Android Back are Cancel as well.
            Submit(panel.AbandonButton);
            yield return ShellRig.Settle();
            Assert.That(rig.Shell.HandleBack(), Is.True);
            yield return ShellRig.Settle();
            Assert.That(rig.Shell.IsConfirmationOpen, Is.False);
            Assert.That(session.State.Contracts.Active, Is.Not.Null);
            Assert.That(Focused(rig.Root), Is.SameAs(panel.AbandonButton));
        }

        [UnityTest]
        public IEnumerator ConfirmingAbandonsWithNoRewardNoPenaltyAndFreesTheOffers()
        {
            yield return Open(ShellScreens.All().First());
            AddStock(session, "iron.plate", 30);
            Submit(panel.AcceptButton(Plate));
            yield return ShellRig.Settle();
            var cash = session.State.Cash;
            panel.AbandonButton.Focus();
            Submit(panel.AbandonButton);
            yield return ShellRig.Settle();
            Submit(rig.Shell.DialogConfirmButton);
            yield return ShellRig.Settle();

            Assert.That(session.State.Contracts.Active, Is.Null);
            Assert.That(session.State.Cash, Is.EqualTo(cash), "no reward");
            Assert.That(session.State.Inventory.Units("iron.plate"), Is.EqualTo(30), "no penalty");
            Assert.That(session.State.Contracts.CompletionCount(Plate), Is.Zero, "the completion count is unchanged");
            Assert.That(Shown(panel.ActiveCard), Is.False);
            Assert.That(RunShellDocument.IsSoftDisabled(panel.AcceptButton(Plate)), Is.False);
            Assert.That(rig.Shell.NoticeText, Does.Contain("no penalty"));
            var focused = Focused(rig.Root);
            Assert.That(focused, Is.Not.Null, "focus is not lost when the Abandon control disappears");
            Assert.That(Shown(focused), Is.True);
        }

        [UnityTest]
        public IEnumerator ARotationCancelsTheAbandonConfirmationWithoutAbandoningAndReturnsFocus([ValueSource(nameof(Representative))] ShellScreen screen)
        {
            yield return Open(screen);
            Submit(panel.AcceptButton(Plate));
            yield return ShellRig.Settle();
            panel.AbandonButton.Focus();
            Submit(panel.AbandonButton);
            yield return ShellRig.Settle();
            Assert.That(rig.Shell.IsConfirmationOpen, Is.True);

            Assert.That(panel.CancelForRotation(), Is.True);
            yield return rig.Layout(ShellScreens.Rotated(screen));
            Assert.That(rig.Shell.IsConfirmationOpen, Is.False);
            Assert.That(panel.IsAbandonConfirmationOpen, Is.False);
            Assert.That(session.State.Contracts.Active, Is.Not.Null, "a rotation never abandons");
            Assert.That(rig.Shell.NoticeText, Is.EqualTo(RunShellDocument.RotationCancelledNotice));
            Assert.That(Focused(rig.Root), Is.SameAs(panel.AbandonButton), "focus returns to the invoking control after a rotation cancel");
            Assert.That(panel.CancelForRotation(), Is.False, "nothing is left to cancel");
        }

        // ---------------------------------------------------------------------------------------------------
        // Keyboard
        // ---------------------------------------------------------------------------------------------------

        [UnityTest]
        public IEnumerator EveryPanelControlIsReachableByFocusAndTheTraversalOrderCoversThem([ValueSource(nameof(Representative))] ShellScreen screen)
        {
            yield return Open(screen);
            Submit(panel.AcceptButton(Plate));
            yield return ShellRig.Settle();
            var order = rig.Shell.GetFocusOrder().Select(element => element.name).ToList();
            foreach (var expected in new[] { "contracts-deliver", "contracts-abandon", "contracts-accept-" + Gear, "contracts-accept-" + Plate, "contracts-accept-" + TimedBar })
            {
                Assert.That(order, Does.Contain(expected), $"{screen}: '{expected}' is in the focus order");
            }

            foreach (var button in DisplayedButtons(panel.Root))
            {
                Assert.That(button.focusable, Is.True, button.name);
                Assert.That(button.worldBound.width, Is.GreaterThanOrEqualTo(MinimumTarget - Tolerance), button.name);
                Assert.That(button.worldBound.height, Is.GreaterThanOrEqualTo(MinimumTarget - Tolerance), button.name);
            }
        }

        // ---------------------------------------------------------------------------------------------------
        // Layout (UX-3, F-4): worst-case content at every screen class, rotations included, at 100 and 200 percent
        // ---------------------------------------------------------------------------------------------------

        /// <summary>
        /// The worst case of the panel: an expired outcome card, an active contract with the longest copy (a disabled Deliver
        /// with missing units, the auto-sell hint and a rejection), and every offer with its reason and a rejection.
        /// </summary>
        private IEnumerator BuildWorstCase()
        {
            AddStock(session, "iron.bar", 100);
            Submit(panel.AcceptButton(TimedBar));
            Advance(session, 240_001);
            yield return Sync();
            Assert.That(Shown(panel.ExpiredCard), Is.True);
            Submit(panel.AcceptButton(Gear));
            Assert.That(session.State.TrySetAutoSell("iron.gear", true), Is.EqualTo(IdleFactory.Run.Domain.RunStateResult.Ok));
            yield return Sync();
            Assert.That(Shown(panel.ActiveCard), Is.True);
            Submit(panel.DeliverButton);
            foreach (var id in panel.OfferIds)
            {
                Submit(panel.AcceptButton(id));
            }

            yield return ShellRig.Settle();
        }

        [UnityTest]
        public IEnumerator TheWorstCasePanelStaysContainedAtEveryScreenClass([ValueSource(nameof(Screens))] ShellScreen screen, [ValueSource(nameof(Percents))] int percent)
        {
            yield return Open(screen, percent);
            yield return BuildWorstCase();
            yield return AssertPanelLayout(rig, panel.Root, "worst-case contracts", percent);
        }

        [UnityTest]
        public IEnumerator TheEmptyStateStaysContainedAtEveryScreenClass([ValueSource(nameof(Screens))] ShellScreen screen, [ValueSource(nameof(Percents))] int percent)
        {
            yield return Open(screen, percent, unlock: false);
            yield return ShellRig.Settle();
            AssertShellLayout(rig, "empty contracts");
        }

        [UnityTest]
        public IEnumerator TheAbandonConfirmationStaysContainedAtEveryScreenClass([ValueSource(nameof(Screens))] ShellScreen screen, [ValueSource(nameof(Percents))] int percent)
        {
            yield return Open(screen, percent);
            Submit(panel.AcceptButton(Plate));
            yield return ShellRig.Settle();
            panel.AbandonButton.Focus();
            Submit(panel.AbandonButton);
            yield return ShellRig.Settle();
            Assert.That(rig.Shell.IsConfirmationOpen, Is.True);
            AssertDialogLayout(rig, "abandon", percent);
            Assert.That(Focused(rig.Root), Is.SameAs(rig.Shell.DialogCancelButton));
        }

        [UnityTest]
        public IEnumerator PanelTextFollowsTheScaleVariablesAndNeverFallsBelowTheMinimum([ValueSource(nameof(Percents))] int percent)
        {
            yield return Open(ShellScreens.All().First(), percent);
            yield return BuildWorstCase();
            var expected = percent / 100f;
            foreach (var label in panel.Root.Query<Label>().ToList().Where(item => IsDisplayed(item) && !string.IsNullOrEmpty(item.text)))
            {
                Assert.That(label.resolvedStyle.fontSize, Is.GreaterThanOrEqualTo(12f * expected - 0.51f), label.name);
                Assert.That(label.resolvedStyle.whiteSpace, Is.Not.EqualTo(WhiteSpace.NoWrap), label.name + " wraps");
            }
        }
    }
}
#endif
