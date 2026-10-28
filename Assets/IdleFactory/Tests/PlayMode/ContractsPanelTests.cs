#if UNITY_EDITOR
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

        /// <summary>The three F003 offers these tests unlock (<c>UnlockContracts</c>); T363 adds a bonus and a production offer that stay locked here.</summary>
        private static readonly string[] F003Offers = { Gear, Plate, TimedBar };

        // Content values come from the shipped catalog (T344), never from a typed revision.
        private static long PlateRequired => RunShellFixtures.OfferQuantity(Plate);
        private static long BarRequired => RunShellFixtures.OfferQuantity(TimedBar);
        private static long BarLimitMs => RunShellFixtures.OfferDurationMs(TimedBar);
        private static string Qty(long units) => QuantityFormatter.Format(units);

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

        private IEnumerator Open(ShellScreen screen, int percent = 100, bool unlock = true, IdleFactory.Unity.Presentation.ShellDeviceProfile? profile = null, int factoryLevel = 1)
        {
            rig = ShellRig.Create(screen, profile, percent);
            rigs.Add(rig);
            yield return rig.Layout();
            session = RunShellFixtures.NewSession(out commands, factoryLevel: factoryLevel, cash: 25, unlockEverything: true);
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
            Assert.That(panel.OfferIds, Is.SupersetOf(F003Offers));
            Assert.That(panel.OfferIds, Does.Contain("contract.bonus-plate").And.Contain("contract.produce-plate"), "T363 content slice");

            Assert.That(panel.OfferRequirement(Plate).text, Is.EqualTo("Deliver " + Qty(PlateRequired) + " Iron Plate"));
            Assert.That(panel.OfferDuration(Plate).text, Is.EqualTo("No time limit."));
            Assert.That(panel.OfferRewards(Plate).text, Does.Contain("$" + RunShellFixtures.ContractCashText(Plate)).And.Contain(RunShellFixtures.OfferRp(Plate) + " RP"));
            foreach (var later in RunShellFixtures.DueItemLabels(1))
            {
                Assert.That(panel.OfferRewards(Plate).text, Does.Not.Contain(later), "an Item due on a later completion is not announced yet");
            }

            Assert.That(panel.OfferRequirement(TimedBar).text, Is.EqualTo("Deliver " + Qty(BarRequired) + " Iron Bar"));
            Assert.That(panel.OfferDuration(TimedBar).text, Is.EqualTo("Time limit: " + RunShellFixtures.Clock(BarLimitMs)));
            Assert.That(panel.OfferRewards(TimedBar).text, Does.Contain("$" + RunShellFixtures.ContractCashText(TimedBar)).And.Contain(RunShellFixtures.OfferRp(TimedBar) + " RP"));
            foreach (var first in RunShellFixtures.DueItemLabels(0))
            {
                Assert.That(panel.OfferRewards(TimedBar).text, Does.Contain(first + " x1"), "the Item of the first completion");
            }

            foreach (var id in F003Offers)
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
            Assert.That(panel.ActiveRequirement.text, Is.EqualTo("Deliver " + Qty(PlateRequired) + " Iron Plate"));
            Assert.That(panel.ActiveDeadline.text, Is.EqualTo("No time limit."));
            Assert.That(Shown(panel.AbandonButton), Is.True, "an untimed contract can be abandoned");
            foreach (var id in F003Offers)
            {
                Assert.That(RunShellDocument.IsSoftDisabled(panel.AcceptButton(id)), Is.True, id + " is disabled while a contract is active");
                if (id == Plate)
                {
                    // R-07: the accepted offer is the active contract, so it has no "finish the active contract first" line of its own.
                    Assert.That(Shown(panel.OfferReason(id)), Is.False, id + " is the active contract");
                    continue;
                }

                Assert.That(panel.OfferReason(id).text, Is.EqualTo(RunRejectionCopy.For(RunReasons.ContractActiveExists)), id + " says why");
            }
        }

        [UnityTest]
        public IEnumerator ProgressFollowsTheStockAndDeliverEnablesOnceTheStockCovers()
        {
            yield return Open(ShellScreens.All().First());
            Submit(panel.AcceptButton(Plate));
            var part = PlateRequired * 2 / 5;
            AddStock(session, "iron.plate", part);
            yield return Sync();

            Assert.That(panel.ActiveProgress.text, Is.EqualTo("In stock: " + Qty(part) + " of " + Qty(PlateRequired)));
            Assert.That(panel.ActiveMissing.text, Is.EqualTo("Missing " + Qty(PlateRequired - part) + " Iron Plate."));
            Assert.That(RunShellDocument.IsSoftDisabled(panel.DeliverButton), Is.True);
            Assert.That(panel.DeliverButton.text, Does.Contain("missing " + Qty(PlateRequired - part)));

            AddStock(session, "iron.plate", PlateRequired - part);
            yield return Sync();
            Assert.That(panel.ActiveProgress.text, Is.EqualTo("In stock: " + Qty(PlateRequired) + " of " + Qty(PlateRequired)));
            Assert.That(RunShellDocument.IsSoftDisabled(panel.DeliverButton), Is.False);
            Assert.That(panel.DeliverButton.text, Is.EqualTo("Deliver"));
            Assert.That(panel.ActiveMissing.text, Is.EqualTo("Ready to deliver."));
        }

        [UnityTest]
        public IEnumerator DeliveringGrantsTheRewardsAndShowsTheOutcome()
        {
            yield return Open(ShellScreens.All().First());
            Submit(panel.AcceptButton(Plate));
            AddStock(session, "iron.plate", PlateRequired);
            yield return Sync();
            var cash = session.State.Cash;
            var rp = session.State.Rp;

            panel.DeliverButton.Focus();
            Submit(panel.DeliverButton);
            yield return ShellRig.Settle();

            Assert.That(session.State.Cash, Is.EqualTo(cash + RunShellFixtures.ContractCash(Plate)));
            Assert.That(session.State.Rp, Is.EqualTo(rp + RunShellFixtures.OfferRp(Plate)));
            Assert.That(session.State.Inventory.Units("iron.plate"), Is.Zero);
            Assert.That(session.State.Contracts.Active, Is.Null);
            Assert.That(Shown(panel.ActiveCard), Is.False);
            Assert.That(rig.Shell.NoticeText, Does.Contain("Delivered " + Qty(PlateRequired)).And.Contain("$" + RunShellFixtures.ContractCashText(Plate)).And.Contain(RunShellFixtures.OfferRp(Plate) + " RP"));
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
            AddStock(session, "iron.plate", PlateRequired);
            Submit(panel.DeliverButton);
            yield return Sync();
            foreach (var label in RunShellFixtures.DueItemLabels(1))
            {
                Assert.That(panel.OfferRewards(Plate).text, Does.Contain(label + " x1"), "completion 1 grants the Item of ladder index 1");
            }
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
            AddStock(session, "iron.plate", PlateRequired - 1);
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
            Assert.That(session.State.Inventory.Units("iron.plate"), Is.EqualTo(PlateRequired - 1));
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
            // One unit short of the shipped Plate quantity (catalog-derived, T345 follow-up from #409 QA): the deliver rejection needs stock below it.
            AddStock(session, "iron.plate", PlateRequired - 1);
            yield return Sync();
            Submit(panel.DeliverButton);
            yield return ShellRig.Settle();
            var where = $"{screen} @{percent}%";
            AssertIconBesideText(panel.ActiveFeedback, PanelIcons.Warning, where + " deliver rejection");

            // Every command clears the earlier rejection, so the accept rejection follows on its own.
            Submit(panel.AcceptButton(Gear));
            yield return ShellRig.Settle();
            AssertIconBesideText(panel.OfferFeedback(Gear), PanelIcons.Warning, where + " accept rejection");
            // R-06: "finish the active contract first" is a normal state of the offer, so it carries the neutral icon; the warning is for the rejection.
            AssertIconBesideText(panel.OfferReason(Gear), PanelIcons.Info, where + " blocked offer reason");
            AssertTextButtonsHaveNoChildren(panel.Root, where);
            AssertShellLayout(rig, "contracts with warning icons");
        }

        [UnityTest]
        public IEnumerator ContractsWorkWithATouchTapAndWithAPointerClick([Values(true, false)] bool touch)
        {
            var screen = ShellScreens.All().First();
            yield return Open(screen, 100, true, InputModeSupport.ProfileFor(screen, touch));
            var where = touch ? "touch" : "pointer";

            // Accept with the device's own press; the reasons of the blocked offers are text, so no hover is needed to read them.
            yield return InputModeSupport.Tap(panel.AcceptButton(Plate), touch);
            yield return ShellRig.Settle();
            Assert.That(session.State.Contracts.Active?.OfferId, Is.EqualTo(Plate), where + ": the press accepted the offer");
            Assert.That(Shown(panel.OfferReason(Gear)), Is.True, where + ": the block is visible text");
            Assert.That(panel.OfferReason(Gear).text, Is.EqualTo(RunRejectionCopy.For(RunReasons.ContractActiveExists)));
            InputModeSupport.AssertTargets(DisplayedButtons(panel.Root).Cast<VisualElement>(), where);

            // A delivery with short stock shows its rejection next to the control and changes nothing.
            yield return InputModeSupport.Tap(panel.DeliverButton, touch);
            yield return ShellRig.Settle();
            Assert.That(session.State.Contracts.Active?.OfferId, Is.EqualTo(Plate), where + ": nothing was delivered");
            Assert.That(Shown(panel.ActiveFeedback), Is.True, where + ": the rejection is visible");

            // The exact stock: a touch tap pins it, a pointer shows it on hover and never pins.
            if (touch)
            {
                Assert.That(rig.Shell.IsDetailPinned, Is.True, "a tap pins the exact value");
            }
            else
            {
                InputModeSupport.Hover(panel.DeliverButton);
                Assert.That(rig.Shell.IsDetailPinned, Is.False, "a click does not pin on a pointer device");
            }

            if (!touch)
            {
                Assert.That(rig.Shell.DetailText, Does.StartWith("In stock "), "hover shows the exact stock");
            }
            else
            {
                Assert.That(rig.Shell.DetailText, Does.StartWith("In stock "), "the pinned detail is the exact stock");
            }
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

        // ---------------------------------------------------------------------------------------------------
        // T451 (FR-031, FR-032, SC-010): the reason line of every live offer that cannot be accepted, and focus when cards move
        // ---------------------------------------------------------------------------------------------------

        /// <summary>The icon class per accept-block reason (Feature 005 R-06): neutral for a normal state, warning for a blocker.</summary>
        [TestCase(RunReasons.ContractActiveExists, PanelIcons.Info)]
        [TestCase(RunReasons.ContractCooldown, PanelIcons.Info)]
        [TestCase(RunReasons.ContractBonusUnavailable, PanelIcons.Info)]
        [TestCase(RunReasons.ContractCapacity, PanelIcons.Warning)]
        [TestCase(RunReasons.CommandInvalid, PanelIcons.Warning)]
        [TestCase(RunReasons.LifecycleBlocked, PanelIcons.Warning)]
        public void EveryAcceptBlockReasonHasATextAndAnIconClass(string reason, string expectedIcon)
        {
            Assert.That(ContractsPanel.ReasonIconFor(reason), Is.EqualTo(expectedIcon));
            Assert.That(RunRejectionCopy.For(reason), Is.Not.Empty.And.Not.EqualTo("That action is not available."), "the reason has its own copy");
        }

        [UnityTest]
        public IEnumerator WhenNoOfferCanBeAcceptedEveryLiveOfferStillShowsItsReasonAsTextWithTheNeutralIcon([ValueSource(nameof(Representative))] ShellScreen screen)
        {
            yield return Open(screen);
            Submit(panel.AcceptButton(Plate));
            yield return ShellRig.Settle();

            foreach (var id in new[] { Gear, TimedBar })
            {
                Assert.That(RunShellDocument.IsSoftDisabled(panel.AcceptButton(id)), Is.True, id + " cannot be accepted");
                Assert.That(panel.OfferReason(id).text, Is.EqualTo("Finish or abandon the active contract first."), id + ": the reason is readable text");
                AssertIconBesideText(panel.OfferReason(id), PanelIcons.Info, screen + " " + id + " active-exists reason");
            }
        }

        [UnityTest]
        public IEnumerator AStorageTooSmallForTheOfferShowsTheReasonWithTheWarningIcon([ValueSource(nameof(Representative))] ShellScreen screen)
        {
            yield return Open(screen);
            var table = RunShellFixtures.ShippedCatalog.ContractOffers[Plate].QuantityTable;
            var count = Enumerable.Range(0, table.Count).First(index => table[index] > session.State.Inventory.Capacity);
            Assert.That(session.State.Contracts.TrySetCompletionCount(Plate, count), Is.EqualTo(IdleFactory.Run.Domain.RunStateResult.Ok));
            yield return Sync();

            Assert.That(panel.OfferReason(Plate).text, Is.EqualTo("Your storage is too small for that contract. Expand storage first."));
            AssertIconBesideText(panel.OfferReason(Plate), PanelIcons.Warning, screen + " capacity reason");
            Assert.That(Shown(panel.OfferReason(Gear)), Is.False, "an offer that can be accepted has no reason line");
        }

        [UnityTest]
        public IEnumerator FocusStaysOnTheFocusedOfferCardWhenItMovesThroughTheSort()
        {
            yield return Open(ShellScreens.All().First());
            Submit(panel.AcceptButton(Plate));
            AddStock(session, "iron.plate", PlateRequired);
            yield return Sync();
            Assert.That(panel.OfferDisplayOrder.Take(3), Is.EqualTo(new[] { Gear, Plate, TimedBar }), "fixture: Plate sits in the middle");

            panel.AcceptButton(Plate).Focus();
            Assert.That(Focused(rig.Root), Is.SameAs(panel.AcceptButton(Plate)), "fixture: the Plate card holds focus");
            Assert.That(commands.DeliverContract().IsSuccess, Is.True);
            yield return Sync();
            yield return ShellRig.Settle();

            Assert.That(panel.OfferDisplayOrder.Take(3), Is.EqualTo(new[] { Gear, TimedBar, Plate }), "the delivered offer cools down and moves behind the live ones");
            Assert.That(Focused(rig.Root), Is.SameAs(panel.AcceptButton(Plate)), "focus followed the card that moved (R-06)");
        }

        [UnityTest]
        public IEnumerator FocusFollowsAFocusedCardThatLeavesTheLiveOffersForTheEndOfTheList()
        {
            const string bonus = "contract.bonus-plate";
            yield return Open(ShellScreens.All().First(), factoryLevel: 2);
            UnlockContracts(session, bonus);
            Advance(session, 1_000);
            yield return Sync();
            Submit(panel.AcceptButton(bonus));
            AddStock(session, "iron.plate", RunShellFixtures.OfferQuantity(bonus));
            yield return Sync();
            var before = panel.OfferDisplayOrder.ToList().IndexOf(bonus);

            panel.AcceptButton(bonus).Focus();
            Assert.That(Focused(rig.Root), Is.SameAs(panel.AcceptButton(bonus)));
            Assert.That(commands.DeliverContract().IsSuccess, Is.True);
            yield return Sync();
            yield return ShellRig.Settle();

            Assert.That(panel.OfferCooldown(bonus).text, Is.EqualTo("Bonus used"));
            Assert.That(panel.OfferDisplayOrder.ToList().IndexOf(bonus), Is.GreaterThan(before), "the used bonus sorts behind the live offers");
            Assert.That(Focused(rig.Root), Is.SameAs(panel.AcceptButton(bonus)), "focus stays with the card that left the live group");
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
            Assert.That(panel.ActiveDeadline.text, Is.EqualTo("Time left: " + RunShellFixtures.Clock(BarLimitMs)));
            Assert.That(Shown(panel.AbandonButton), Is.False, "a timed contract only expires");

            Advance(session, 1000);
            panel.Refresh();
            Assert.That(panel.ActiveDeadline.text, Is.EqualTo("Time left: " + RunShellFixtures.Clock(BarLimitMs - 1000)));

            // Within the same second the panel does not redraw: a marker written to the label survives.
            panel.ActiveDeadline.text = "marker";
            Advance(session, 100);
            panel.Refresh();
            Assert.That(panel.ActiveDeadline.text, Is.EqualTo("marker"), "the countdown updates at 1 Hz, not every frame");
            Advance(session, 900);
            panel.Refresh();
            Assert.That(panel.ActiveDeadline.text, Is.EqualTo("Time left: " + RunShellFixtures.Clock(BarLimitMs - 2000)));
        }

        [UnityTest]
        public IEnumerator DeliveryAtTheDeadlineInstantSucceeds()
        {
            yield return Open(ShellScreens.All().First());
            AddStock(session, "iron.bar", BarRequired);
            Submit(panel.AcceptButton(TimedBar));
            Advance(session, BarLimitMs);
            yield return Sync();

            Assert.That(session.TimelineUtcMs, Is.EqualTo(session.State.Contracts.Active.DeadlineUtcMs.Value), "the clock is exactly on the deadline");
            Assert.That(panel.ActiveDeadline.text, Is.EqualTo("Time left: 0:00"));
            Assert.That(RunShellDocument.IsSoftDisabled(panel.DeliverButton), Is.False, "the deadline is inclusive");
            Submit(panel.DeliverButton);
            yield return ShellRig.Settle();

            Assert.That(session.State.Contracts.Active, Is.Null);
            Assert.That(session.State.Contracts.LastExpired, Is.Null);
            Assert.That(Shown(panel.ExpiredCard), Is.False);
            Assert.That(rig.Shell.NoticeText, Does.StartWith("Delivered " + Qty(BarRequired)));
        }

        [UnityTest]
        public IEnumerator OneMillisecondAfterTheDeadlineTheContractExpiresWithNoRewardAndNoPenalty()
        {
            yield return Open(ShellScreens.All().First());
            AddStock(session, "iron.bar", BarRequired);
            Submit(panel.AcceptButton(TimedBar));
            var cash = session.State.Cash;
            Advance(session, BarLimitMs + 1);
            yield return Sync();

            Assert.That(session.State.Contracts.Active, Is.Null);
            Assert.That(Shown(panel.ExpiredCard), Is.True, "the outcome card replaces the active contract");
            Assert.That(panel.ExpiredLine.text, Does.Contain("No reward, no penalty.").And.Contain(Qty(BarRequired) + " Iron Bar"));
            Assert.That(session.State.Cash, Is.EqualTo(cash));
            Assert.That(session.State.Inventory.Units("iron.bar"), Is.EqualTo(BarRequired), "the stock is kept");
            Assert.That(Shown(panel.ActiveCard), Is.False);
            // An expired offer cools down like a completed one (FR-005): soft-disabled exactly when the content gives the timed Bar a cooldown.
            Assert.That(RunShellDocument.IsSoftDisabled(panel.AcceptButton(TimedBar)), Is.EqualTo(RunShellFixtures.OfferCooldownMs(TimedBar) > 0));

            Submit(panel.AcceptButton(Plate));
            yield return ShellRig.Settle();
            Assert.That(Shown(panel.ExpiredCard), Is.False, "accepting an offer clears the kept outcome");
        }

        [UnityTest]
        public IEnumerator ADeliverTapAfterTheDeadlineIsRejectedWithTheExpiredReason()
        {
            yield return Open(ShellScreens.All().First());
            AddStock(session, "iron.bar", BarRequired);
            Submit(panel.AcceptButton(TimedBar));
            var cashBefore = session.State.Cash;
            Advance(session, BarLimitMs);
            yield return Sync();
            Advance(session, 1);
            // The panel still shows the instant before; the command re-validates.
            Submit(panel.DeliverButton);
            yield return ShellRig.Settle();
            Assert.That(session.State.Contracts.Active, Is.Null);
            Assert.That(session.State.Cash, Is.EqualTo(cashBefore), "no reward");
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
            AddStock(session, "iron.plate", PlateRequired * 3 / 10);
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
            Assert.That(RunShellDocument.IsSoftDisabled(panel.AcceptButton(Gear)), Is.False, "the other offers are free again");
            Assert.That(RunShellDocument.IsSoftDisabled(panel.AcceptButton(Plate)), Is.True, "T363: an abandoned Plate offer starts its cooldown (FR-005)");
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
            AddStock(session, "iron.bar", BarRequired);
            Submit(panel.AcceptButton(TimedBar));
            Advance(session, BarLimitMs + 1);
            yield return Sync();
            Assert.That(Shown(panel.ExpiredCard), Is.True);
            Submit(panel.AcceptButton(Gear));
            Assert.That(session.State.TrySetAutoSell("iron.gear", true), Is.EqualTo(IdleFactory.Run.Domain.RunStateResult.Ok));
            yield return Sync();
            Assert.That(Shown(panel.ActiveCard), Is.True);
            Submit(panel.DeliverButton);
            foreach (var id in F003Offers)
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
