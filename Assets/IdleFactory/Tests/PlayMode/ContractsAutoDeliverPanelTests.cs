#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using IdleFactory.Run.Application;
using IdleFactory.Run.Domain;
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
    /// T330 (US3, FR-010, FR-011, FR-038; issue #395; contracts/ui-surfaces-f005.md and the QA-12 rows "Auto-deliver toggle", "Offer cooldown and
    /// bonus", "Production progress" and "Contracts badge"): the auto-deliver toggle and its hint, the auto-delivered notice, the short-stock
    /// expiry line, the offer countdown and the bonus-ended transition without a layout jump, the Accept rejection copy for
    /// <c>contract.cooldown</c>, <c>contract.bonus.unavailable</c> and <c>contract.capacity</c>, the production progress that never shows the
    /// stock fields, and the Contracts tab badge, at every screen class at 100 and 200 percent text. The panel and the feed read snapshots and
    /// call commands only; every number here comes from a real session.
    /// </summary>
    public sealed class ContractsAutoDeliverPanelTests
    {
        private const string BonusPlate = "contract.bonus-plate";
        private const string ProducePlate = "contract.produce-plate";
        private const string PlateResource = "iron.plate";
        private const string GearResource = "iron.gear";

        // Content values come from the shipped catalog (T344), never from a typed revision.
        private static long PlateRequired => RunShellFixtures.OfferQuantity(Plate);
        private static long GearRequired => RunShellFixtures.OfferQuantity(Gear);
        private static long ProduceRequired => RunShellFixtures.OfferQuantity(ProducePlate);
        private static long BarLimitMs => RunShellFixtures.OfferDurationMs(TimedBar);
        private static long PlateCooldownMs => RunShellFixtures.OfferCooldownMs(Plate);
        private static long BonusWindowMs => RunShellFixtures.ShippedCatalog.ContractOffers[BonusPlate].Bonus.WindowMs;
        private static string Qty(long units) => QuantityFormatter.Format(units);

        private readonly List<ShellRig> rigs = new List<ShellRig>();
        private ShellRig rig;
        private RunSession session;
        private RunCommands commands;
        private ContractsPanel panel;
        private ContractsShellFeed feed;

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
            feed = null;
        }

        /// <summary>The shell at a screen class with the Contracts panel mounted and the shell feed seeded; <paramref name="offers"/> are unlocked.</summary>
        private IEnumerator Open(ShellScreen screen, int percent = 100, int factoryLevel = 1, params string[] offers)
        {
            rig = ShellRig.Create(screen, null, percent);
            rigs.Add(rig);
            yield return rig.Layout();
            session = RunShellFixtures.NewSession(out commands, factoryLevel: factoryLevel, cash: 25, unlockEverything: true);
            if (offers.Length > 0)
            {
                UnlockContracts(session, offers);
            }

            panel = new ContractsPanel(session, commands, Styles("ContractsPanel.uss"), Icon);
            rig.Shell.Panels.Register(panel);
            rig.Shell.SelectTab(ShellTab.Contracts);
            feed = new ContractsShellFeed(session, rig.Shell);
            feed.Update();
            yield return ShellRig.Settle();
        }

        /// <summary>Redraws the panel and the shell feed after a state change made outside them, then lets the layout settle.</summary>
        private IEnumerator Sync()
        {
            panel.Invalidate();
            panel.Refresh();
            feed.Invalidate();
            feed.Update();
            yield return ShellRig.Settle();
        }

        private static bool Shown(VisualElement element) => IsDisplayed(element);

        private ContractOfferSnapshot Offer(string offerId) => RunSnapshots.Contracts(session).Offers.First(offer => offer.OfferId == offerId);

        private ActiveContractSnapshot Active() => RunSnapshots.Contracts(session).Active;

        private string Label(string resourceId) => session.Catalog.Resources[resourceId].Label;

        // ---------------------------------------------------------------------------------------------------
        // Auto-deliver toggle
        // ---------------------------------------------------------------------------------------------------

        [UnityTest]
        public IEnumerator TheToggleIsOffAfterAcceptCarriesItsStateInTheNameAndSwitchesThroughTheCommand([ValueSource(nameof(Representative))] ShellScreen screen)
        {
            yield return Open(screen, 100, 1, Plate);
            Assert.That(Shown(panel.AutoDeliverButton), Is.False, "no active contract, no toggle");

            Submit(panel.AcceptButton(Plate));
            yield return ShellRig.Settle();
            var toggle = panel.AutoDeliverButton;
            Assert.That(Shown(toggle), Is.True);
            Assert.That(toggle.name, Is.EqualTo("contracts-autodeliver"));
            Assert.That(toggle.text, Is.EqualTo("Auto-deliver: Off"), "Off after accept");
            Assert.That(toggle.tooltip, Is.EqualTo("Auto-deliver, off"), "the accessible name carries the state");
            Assert.That(Active().AutoDeliver, Is.False);
            Assert.That(toggle.focusable, Is.True);
            Assert.That(toggle.worldBound.height, Is.GreaterThanOrEqualTo(43.5f), "at least 44 pt");
            Assert.That(toggle.worldBound.width, Is.GreaterThanOrEqualTo(43.5f));

            Assert.That(Shown(panel.ActiveAutoHint), Is.True);
            Assert.That(panel.ActiveAutoHint.text, Is.EqualTo(ContractsPanel.AutoDeliverHint));
            Assert.That(panel.ActiveAutoHint.text, Does.Contain("Applies to this contract only"));
            Assert.That(panel.ActiveAutoHint.text, Does.Contain("auto-sell"), "the hint also says the stock is kept out of auto-sell");

            Submit(toggle);
            yield return ShellRig.Settle();
            Assert.That(Active().AutoDeliver, Is.True, "the command turned it on");
            Assert.That(toggle.text, Is.EqualTo("Auto-deliver: On"));
            Assert.That(toggle.tooltip, Is.EqualTo("Auto-deliver, on"));
            Assert.That(toggle.ClassListContains("contracts-autodeliver--on"), Is.True);

            Submit(toggle);
            yield return ShellRig.Settle();
            Assert.That(Active().AutoDeliver, Is.False);
            Assert.That(toggle.text, Is.EqualTo("Auto-deliver: Off"));
            Assert.That(toggle.tooltip, Is.EqualTo("Auto-deliver, off"));
        }

        [UnityTest]
        public IEnumerator TheToggleIsInTheFocusOrderBetweenDeliverAndAbandonAndKeepsFocusWhenUsed()
        {
            yield return Open(ShellScreens.All().First(), 100, 1, Plate);
            Submit(panel.AcceptButton(Plate));
            yield return ShellRig.Settle();

            var order = rig.Shell.GetFocusOrder().ToList();
            var deliver = order.IndexOf(panel.DeliverButton);
            var toggle = order.IndexOf(panel.AutoDeliverButton);
            var abandon = order.IndexOf(panel.AbandonButton);
            Assert.That(deliver, Is.GreaterThanOrEqualTo(0));
            Assert.That(toggle, Is.GreaterThan(deliver), "reading order: Deliver, then Auto-deliver");
            Assert.That(abandon, Is.GreaterThan(toggle), "then Abandon");

            panel.AutoDeliverButton.Focus();
            Submit(panel.AutoDeliverButton);
            yield return ShellRig.Settle();
            Assert.That(Focused(rig.Root), Is.SameAs(panel.AutoDeliverButton), "focus stays on the toggle after use");
            Assert.That(panel.AutoDeliverButton.ClassListContains("run-control--focused"), Is.True);
        }

        [UnityTest]
        public IEnumerator AProductionObjectiveHasNoToggleAndSaysItCompletesAutomatically([ValueSource(nameof(Representative))] ShellScreen screen)
        {
            yield return Open(screen, 100, 1, ProducePlate);
            Assert.That(panel.OfferRequirement(ProducePlate).text, Does.StartWith("Produce "), "an offer row says produce, not deliver");

            Submit(panel.AcceptButton(ProducePlate));
            yield return ShellRig.Settle();
            Assert.That(Active().Objective, Is.EqualTo("produce"));
            Assert.That(Shown(panel.AutoDeliverButton), Is.False, "the toggle is for delivery objectives only");
            Assert.That(Shown(panel.DeliverButton), Is.False, "a production objective is never delivered from stock");
            Assert.That(Shown(panel.ActiveAutoHint), Is.True);
            Assert.That(panel.ActiveAutoHint.text, Is.EqualTo("Completes automatically."));
            Assert.That(panel.ActiveRequirement.text, Does.StartWith("Produce "));
        }

        [UnityTest]
        public IEnumerator TheSettingIsOffAgainWhenTheOfferIsAcceptedAfterAnAutoDelivery()
        {
            yield return Open(ShellScreens.All().First(), 100, 1, Gear);
            AddStock(session, GearResource, GearRequired);
            Submit(panel.AcceptButton(Gear));
            yield return ShellRig.Settle();
            panel.AutoDeliverButton.Focus();
            Submit(panel.AutoDeliverButton);
            yield return ShellRig.Settle();

            Assert.That(Active(), Is.Null, "the stock was there, so turning it on delivered the contract at once");
            Assert.That(Shown(panel.ActiveCard), Is.False);
            Assert.That(Shown(panel.AutoDeliverButton), Is.False, "no contract, no toggle");
            var focused = Focused(rig.Root);
            Assert.That(focused, Is.Not.Null, "focus is not lost when the toggle goes away");
            Assert.That(Shown(focused), Is.True);

            // Revision 3: the Gear offer cools down after a completion, so the offer is re-accepted once the cooldown has run (catalog-derived).
            Advance(session, RunShellFixtures.OfferCooldownMs(Gear) + 1000);
            yield return ShellRig.Settle();
            Submit(panel.AcceptButton(Gear));
            yield return ShellRig.Settle();
            Assert.That(Active()?.OfferId, Is.EqualTo(Gear));
            Assert.That(Active().AutoDeliver, Is.False, "the setting does not carry over to a re-accepted offer");
            Assert.That(panel.AutoDeliverButton.text, Is.EqualTo("Auto-deliver: Off"));
            Assert.That(panel.AutoDeliverButton.tooltip, Is.EqualTo("Auto-deliver, off"));
        }

        [UnityTest]
        public IEnumerator ATapOnAStaleToggleShowsTheNoActiveContractCopyNextToIt()
        {
            yield return Open(ShellScreens.All().First(), 100, 1, Plate);
            Submit(panel.AcceptButton(Plate));
            yield return ShellRig.Settle();
            Assert.That(commands.AbandonContract().IsSuccess, Is.True, "the contract goes away outside the panel");

            Submit(panel.AutoDeliverButton);
            yield return ShellRig.Settle();
            Assert.That(Shown(panel.ActiveCard), Is.False, "the panel re-rendered without the card");
            Assert.That(Active(), Is.Null);
            Assert.That(session.State.Contracts.Active, Is.Null, "a rejection changes nothing");
        }

        [UnityTest]
        public IEnumerator ABlockedAutoDeliveryShowsTheWaitingStateAndClearsWhenItRetries()
        {
            yield return Open(ShellScreens.All().First(), 100, 1, Plate);
            Submit(panel.AcceptButton(Plate));
            Submit(panel.AutoDeliverButton);
            yield return Sync();
            Assert.That(Shown(panel.ActiveBlocked), Is.False);

            session.State.Contracts.SetAutoDeliverBlocked(true);
            yield return Sync();
            Assert.That(Active().CompletionBlocked, Is.True);
            Assert.That(Shown(panel.ActiveBlocked), Is.True);
            Assert.That(panel.ActiveBlocked.text, Is.EqualTo(ContractsPanel.CompletionBlockedText));
            Assert.That(panel.ActiveBlocked.text, Does.Contain("too large"), "the value.limit wording");
            Assert.That(panel.ActiveBlocked.text, Does.Contain("tries again"));

            session.State.Contracts.SetAutoDeliverBlocked(false);
            yield return Sync();
            Assert.That(Shown(panel.ActiveBlocked), Is.False);
        }

        [UnityTest]
        public IEnumerator AutoSellOnTheContractResourceIsExplainedAsKeptOutOfAutoSell()
        {
            yield return Open(ShellScreens.All().First(), 100, 1, Plate);
            Submit(panel.AcceptButton(Plate));
            Assert.That(session.State.TrySetAutoSell(PlateResource, true), Is.EqualTo(RunStateResult.Ok));
            yield return Sync();
            Assert.That(Shown(panel.ActiveHint), Is.True);
            Assert.That(panel.ActiveHint.text, Does.Contain("Auto-sell is on for " + Label(PlateResource)).And.Contain("keeps the stock this contract needs"));
        }

        // ---------------------------------------------------------------------------------------------------
        // Auto-delivered notice and the short-stock expiry line
        // ---------------------------------------------------------------------------------------------------

        [UnityTest]
        public IEnumerator AnAutoDeliveryIsAnnouncedOnAnyTabAndOnlyOnce()
        {
            yield return Open(ShellScreens.All().First(), 100, 1, Gear);
            AddStock(session, GearResource, GearRequired);
            Submit(panel.AcceptButton(Gear));
            Assert.That(commands.SetAutoDeliver(true).IsSuccess, Is.True, "turned on outside the panel: the stock is there, so it delivers at once");
            rig.Shell.SelectTab(ShellTab.Factory);
            yield return Sync();

            Assert.That(session.State.Contracts.LastOutcome.Kind, Is.EqualTo(ContractOutcomeKind.AutoDelivered));
            Assert.That(rig.Shell.NoticeText, Does.StartWith("Auto-delivered " + Qty(GearRequired) + " " + Label(GearResource)));
            Assert.That(rig.Shell.NoticeText, Does.Contain("$").And.Contain("RP"));
            Assert.That(rig.Shell.NoticeKind, Is.EqualTo(ShellNoticeKind.Info));

            rig.Shell.ClearNotice();
            yield return Sync();
            Assert.That(rig.Shell.NoticeText, Is.Empty, "the same outcome is not announced twice");
        }

        [UnityTest]
        public IEnumerator AnOutcomeThatAlreadyExistsWhenTheFeedStartsIsNotAnnounced()
        {
            yield return Open(ShellScreens.All().First(), 100, 1, Gear);
            AddStock(session, GearResource, GearRequired);
            Submit(panel.AcceptButton(Gear));
            Assert.That(commands.SetAutoDeliver(true).IsSuccess, Is.True);

            var late = new ContractsShellFeed(session, rig.Shell);
            late.Update();
            yield return ShellRig.Settle();
            Assert.That(rig.Shell.NoticeText, Is.Empty, "a load or an offline return shows its own summary, not a notice");
        }

        [UnityTest]
        public IEnumerator AProductionCompletionIsAnnouncedWithItsReward()
        {
            yield return Open(ShellScreens.All().First(), 100, 1, ProducePlate);
            Submit(panel.AcceptButton(ProducePlate));
            yield return ShellRig.Settle();
            var required = Active().RequiredUnits;
            Assert.That(session.State.Contracts.TryCountProduced(required, session.TimelineUtcMs, 50, 2, 1), Is.EqualTo(RunStateResult.Ok));
            yield return Sync();

            Assert.That(Shown(panel.ActiveCard), Is.False, "the objective completed by itself");
            Assert.That(rig.Shell.NoticeText, Does.StartWith("Produced " + QuantityFormatter.Format(required) + " " + Label(PlateResource)));
            Assert.That(rig.Shell.NoticeText, Does.Contain("contract complete").And.Contain("$50").And.Contain("2 RP").And.Contain("1 Item"));
            Assert.That(panel.OfferCooldown(ProducePlate).text, Does.StartWith("Available in "), "its cooldown runs from the completion");
        }

        [Test]
        public void TheNoticeCopyIsEmptyForAnExpiryAndForNoOutcome()
        {
            var session = RunShellFixtures.NewSession(out _, unlockEverything: true);
            Assert.That(ContractOutcomeCopy.Notice(session.Catalog, default(ContractOutcome)), Is.Null);
            Assert.That(ContractOutcomeCopy.Notice(session.Catalog, new ContractOutcome(ContractOutcomeKind.Expired, Plate, 100, 0, 0, 0, 0, 0, 1)), Is.Null);
            Assert.That(ContractOutcomeCopy.Notice(session.Catalog, new ContractOutcome(ContractOutcomeKind.AutoDelivered, Plate, 100, 0, 0, 0, 0, 0, 1)),
                Is.EqualTo("Auto-delivered 100 " + session.Catalog.Resources[PlateResource].Label + "."));
            Assert.That(ContractOutcomeCopy.Rewards(900, 4, 2), Is.EqualTo("+$900, +4 RP, 2 Items"));
        }

        [UnityTest]
        public IEnumerator AnExpiryWithAutoDeliverOnSaysTheStockWasShort([ValueSource(nameof(Representative))] ShellScreen screen)
        {
            yield return Open(screen, 100, 1, TimedBar);
            Submit(panel.AcceptButton(TimedBar));
            Submit(panel.AutoDeliverButton);
            yield return ShellRig.Settle();
            Assert.That(Active().AutoDeliver, Is.True);

            Advance(session, BarLimitMs + 1000);
            yield return Sync();
            Assert.That(Shown(panel.ActiveCard), Is.False);
            Assert.That(Shown(panel.ExpiredCard), Is.True);
            Assert.That(Shown(panel.ExpiredAutoNote), Is.True);
            Assert.That(panel.ExpiredAutoNote.text, Is.EqualTo("Auto-deliver was on; stock was short."));
            Assert.That(panel.ExpiredLine.text, Does.Contain(ContractsPanel.ExpiredNoPenalty));
        }

        [UnityTest]
        public IEnumerator AnExpiryWithAutoDeliverOffHasNoShortStockLine()
        {
            yield return Open(ShellScreens.All().First(), 100, 1, TimedBar);
            Submit(panel.AcceptButton(TimedBar));
            yield return ShellRig.Settle();

            Advance(session, BarLimitMs + 1000);
            yield return Sync();
            Assert.That(Shown(panel.ExpiredCard), Is.True);
            Assert.That(Shown(panel.ExpiredAutoNote), Is.False);
        }

        // ---------------------------------------------------------------------------------------------------
        // Offer cooldown and bonus
        // ---------------------------------------------------------------------------------------------------

        [UnityTest]
        public IEnumerator TheCooldownCountsDownOncePerSecondAndTheOfferIsReadyAtTheEnd([ValueSource(nameof(Representative))] ShellScreen screen)
        {
            yield return Open(screen, 100, 1, Plate);
            Assert.That(Shown(panel.OfferCooldown(Plate)), Is.False, "an offer that never cooled down shows no countdown");

            Submit(panel.AcceptButton(Plate));
            AddStock(session, PlateResource, PlateRequired);
            yield return Sync();
            Submit(panel.DeliverButton);
            yield return Sync();

            var line = panel.OfferCooldown(Plate);
            Assert.That(line.name, Is.EqualTo("contracts-offer-cooldown-" + Plate));
            Assert.That(Shown(line), Is.True);
            Assert.That(line.text, Is.EqualTo("Available in " + RunShellFixtures.Clock(PlateCooldownMs)));
            Assert.That(RunShellDocument.IsSoftDisabled(panel.AcceptButton(Plate)), Is.True, "Accept stays soft-disabled while it cools down");
            Assert.That(Shown(panel.OfferReason(Plate)), Is.False, "R-07: the state label is the only line; the reason would repeat it");

            Advance(session, 1000);
            panel.Refresh();
            Assert.That(line.text, Is.EqualTo("Available in " + RunShellFixtures.Clock(PlateCooldownMs - 1000)));

            // Within the same second the panel does not redraw: a marker written to the label survives.
            line.text = "marker";
            panel.Refresh();
            Assert.That(line.text, Is.EqualTo("marker"));
            Advance(session, 1000);
            panel.Refresh();
            Assert.That(line.text, Is.EqualTo("Available in " + RunShellFixtures.Clock(PlateCooldownMs - 2000)));

            Advance(session, PlateCooldownMs - 2000);
            yield return Sync();
            Assert.That(line.text, Is.EqualTo("Available now"), "ready exactly at the stamp");
            Assert.That(RunShellDocument.IsSoftDisabled(panel.AcceptButton(Plate)), Is.False);
            Assert.That(Shown(panel.OfferReason(Plate)), Is.False);
        }

        [UnityTest]
        public IEnumerator ATapOnAnOfferThatCoolsDownShowsTheCooldownCopyNextToAccept()
        {
            yield return Open(ShellScreens.All().First(), 100, 1, Plate);
            Submit(panel.AcceptButton(Plate));
            AddStock(session, PlateResource, PlateRequired);
            yield return Sync();
            Submit(panel.DeliverButton);
            yield return Sync();

            panel.AcceptButton(Plate).Focus();
            Submit(panel.AcceptButton(Plate));
            yield return ShellRig.Settle();
            Assert.That(session.State.Contracts.Active, Is.Null, "a rejection changes nothing");
            Assert.That(Shown(panel.OfferFeedback(Plate)), Is.True);
            Assert.That(panel.OfferFeedback(Plate).text, Is.EqualTo(RunRejectionCopy.For(RunReasons.ContractCooldown)));
            Assert.That(panel.OfferFeedback(Plate).text, Does.Contain("cooling down"));
            Assert.That(panel.OfferFeedback(Plate).parent, Is.SameAs(panel.OfferCard(Plate)), "next to the control that was used");
            Assert.That(Focused(rig.Root), Is.SameAs(panel.AcceptButton(Plate)), "focus stays on the soft-disabled Accept");
        }

        [UnityTest]
        public IEnumerator ABonusOfferThatHasNotFiredSaysSoAndRejectsWithTheBonusCopy()
        {
            yield return Open(ShellScreens.All().First(), 100, 1, BonusPlate);
            Assert.That(Shown(panel.OfferCooldown(BonusPlate)), Is.True);
            Assert.That(panel.OfferCooldown(BonusPlate).text, Is.EqualTo("Bonus not active yet."));
            Assert.That(RunShellDocument.IsSoftDisabled(panel.AcceptButton(BonusPlate)), Is.True);

            Submit(panel.AcceptButton(BonusPlate));
            yield return ShellRig.Settle();
            Assert.That(session.State.Contracts.Active, Is.Null);
            Assert.That(panel.OfferFeedback(BonusPlate).text, Is.EqualTo(RunRejectionCopy.For(RunReasons.ContractBonusUnavailable)));
            Assert.That(panel.OfferFeedback(BonusPlate).text, Does.Contain("not available"));
        }

        [UnityTest]
        public IEnumerator ABonusWindowCountsDownAndEndsWithoutAJumpInTheLayout([ValueSource(nameof(Representative))] ShellScreen screen)
        {
            yield return Open(screen, 200, 2, BonusPlate);
            Advance(session, 1000);
            yield return Sync();
            var line = panel.OfferCooldown(BonusPlate);
            Assert.That(line.text, Does.Match(@"^Bonus, ends in (" + (BonusWindowMs / 60_000) + "|" + (BonusWindowMs / 60_000 - 1) + @"):\d\d$"), line.text);
            Assert.That(RunShellDocument.IsSoftDisabled(panel.AcceptButton(BonusPlate)), Is.False, "the window is open, so it can be accepted");

            var left = Offer(BonusPlate).BonusWindowRemainingMs.Value;
            Advance(session, left - 1500);
            yield return Sync();
            Assert.That(line.text, Does.StartWith("Bonus, ends in 0:0"));
            var card = panel.OfferCard(BonusPlate);
            var cardBefore = card.worldBound;
            var acceptBefore = panel.AcceptButton(BonusPlate).worldBound;
            var rowsBelowBefore = panel.OffersSection.worldBound.height;

            Advance(session, 2000);
            yield return Sync();
            Assert.That(line.text, Is.EqualTo("Bonus ended"));
            Assert.That(RunShellDocument.IsSoftDisabled(panel.AcceptButton(BonusPlate)), Is.True);
            Assert.That(Shown(panel.OfferReason(BonusPlate)), Is.False, "R-07: the one state label says Bonus ended; no second line repeats it");
            Assert.That(Shown(line), Is.True, "the line stays, so nothing moves");

            // The reason line appears at the transition (that is new information); the countdown line itself must not make the card shrink.
            Assert.That(card.worldBound.height, Is.GreaterThanOrEqualTo(cardBefore.height - 0.5f), "the card does not shrink");
            Assert.That(panel.AcceptButton(BonusPlate).worldBound.y, Is.GreaterThanOrEqualTo(acceptBefore.y - 0.5f), "Accept does not move up");
            Assert.That(panel.OffersSection.worldBound.height, Is.GreaterThanOrEqualTo(rowsBelowBefore - 0.5f));

            Submit(panel.AcceptButton(BonusPlate));
            yield return ShellRig.Settle();
            Assert.That(panel.OfferFeedback(BonusPlate).text, Is.EqualTo(RunRejectionCopy.For(RunReasons.ContractBonusUnavailable)));
        }

        [UnityTest]
        public IEnumerator AnOfferWhoseQuantityExceedsTheStoreRejectsWithTheCapacityCopy()
        {
            yield return Open(ShellScreens.All().First(), 100, 1, Plate);
            // The first completion count whose quantity no longer fits the store, from the shipped quantity curve.
            var table = RunShellFixtures.ShippedCatalog.ContractOffers[Plate].QuantityTable;
            var count = Enumerable.Range(0, table.Count).First(index => table[index] > session.State.Inventory.Capacity);
            Assert.That(session.State.Contracts.TrySetCompletionCount(Plate, count), Is.EqualTo(RunStateResult.Ok));
            yield return Sync();
            var offer = Offer(Plate);
            Assert.That(offer.RequiredUnits, Is.GreaterThan(session.State.Inventory.Capacity), "fixture: the quantity exceeds the store");
            Assert.That(offer.AcceptReason, Is.EqualTo(RunReasons.ContractCapacity));
            Assert.That(RunShellDocument.IsSoftDisabled(panel.AcceptButton(Plate)), Is.True);
            Assert.That(panel.OfferReason(Plate).text, Is.EqualTo(RunRejectionCopy.For(RunReasons.ContractCapacity)));

            Submit(panel.AcceptButton(Plate));
            yield return ShellRig.Settle();
            Assert.That(session.State.Contracts.Active, Is.Null);
            Assert.That(panel.OfferFeedback(Plate).text, Is.EqualTo(RunRejectionCopy.For(RunReasons.ContractCapacity)));
            Assert.That(panel.OfferFeedback(Plate).text, Does.Contain("Expand storage"));
        }

        // ---------------------------------------------------------------------------------------------------
        // Production progress
        // ---------------------------------------------------------------------------------------------------

        [UnityTest]
        public IEnumerator ProductionProgressReadsProducedOfRequiredAndNeverShowsTheStockFields([ValueSource(nameof(Representative))] ShellScreen screen)
        {
            yield return Open(screen, 100, 1, ProducePlate);
            Submit(panel.AcceptButton(ProducePlate));
            yield return ShellRig.Settle();
            var required = Active().RequiredUnits;
            Assert.That(required, Is.EqualTo(ProduceRequired));
            var part = ProduceRequired * 2 / 5;

            Assert.That(session.State.Contracts.TryCountProduced(part, -1, 0, 0, 0), Is.EqualTo(RunStateResult.Ok));
            AddStock(session, PlateResource, 500);
            yield return Sync();

            Assert.That(panel.ProduceProgress.name, Is.EqualTo("contracts-produce-progress"));
            Assert.That(Shown(panel.ProduceProgress), Is.True);
            Assert.That(panel.ProduceProgressText.text, Is.EqualTo("Produced " + part + " of " + ProduceRequired));
            Assert.That(panel.ProduceProgress.tooltip, Is.EqualTo("Produced " + part + " of " + ProduceRequired));
            Assert.That(panel.ProduceProgressFill.style.width.value.value, Is.EqualTo(part * 100f / ProduceRequired).Within(0.01f), "a bounded bar at two fifths");
            Assert.That(Shown(panel.ActiveProgress), Is.False, "the stock line is delivery-only");
            Assert.That(Shown(panel.ActiveMissing), Is.False, "the missing line is delivery-only");
            var texts = panel.ActiveCard.Query<Label>().ToList().Where(label => Shown(label)).Select(label => label.text).ToList();
            Assert.That(texts.Any(text => text.Contains("In stock") || text.StartsWith("Missing")), Is.False, string.Join(" | ", texts));
            Assert.That(panel.ProduceProgress.focusable, Is.True, "its exact value can be inspected");
            Assert.That(panel.ProduceProgress.worldBound.height, Is.GreaterThanOrEqualTo(43.5f));

            Assert.That(session.State.Contracts.TryCountProduced(ProduceRequired + 80, -1, 0, 0, 0), Is.EqualTo(RunStateResult.Ok), "the count is capped at the quantity");
            yield return Sync();
            Assert.That(panel.ProduceProgressText.text, Is.EqualTo("Produced " + ProduceRequired + " of " + ProduceRequired));
            Assert.That(panel.ProduceProgressFill.style.width.value.value, Is.EqualTo(100f).Within(0.01f));
        }

        [UnityTest]
        public IEnumerator AProductionCardThatCannotCompleteYetShowsTheWaitingState()
        {
            yield return Open(ShellScreens.All().First(), 100, 1, ProducePlate);
            Submit(panel.AcceptButton(ProducePlate));
            Assert.That(session.State.Contracts.TryCountProduced(ProduceRequired + 80, -1, 0, 0, 0), Is.EqualTo(RunStateResult.Ok));
            session.State.Contracts.SetAutoDeliverBlocked(true);
            yield return Sync();

            Assert.That(Active().CompletionBlocked, Is.True, "the blocked flag covers production too");
            Assert.That(Shown(panel.ActiveBlocked), Is.True);
            Assert.That(panel.ProduceProgressText.text, Is.EqualTo("Produced " + ProduceRequired + " of " + ProduceRequired));
        }

        // ---------------------------------------------------------------------------------------------------
        // Contracts tab badge
        // ---------------------------------------------------------------------------------------------------

        /// <summary>The tab text the form implies: the label carries "(n)" or "!", the dot is its own overlay element.</summary>
        private static string ExpectedTabText(RunShellDocument shell, int count)
        {
            switch (shell.ContractsBadgeForm)
            {
                case RunShellDocument.ContractsBadgeMode.Count: return "Contracts (" + (count > 9 ? "9+" : count.ToString()) + ")";
                case RunShellDocument.ContractsBadgeMode.Bang: return "Contracts !";
                default: return "Contracts";
            }
        }

        [UnityTest]
        public IEnumerator TheBadgeCountsDeliverableContractsNamesThemAndClearsAfterASale([ValueSource(nameof(Representative))] ShellScreen screen)
        {
            yield return Open(screen, 100, 1, Plate, Gear);
            var shell = rig.Shell;
            var badge = shell.ContractsBadge;
            Assert.That(badge.name, Is.EqualTo("tab-contracts-badge"));
            Assert.That(shell.ContractsBadgeForm, Is.EqualTo(RunShellDocument.ContractsBadgeMode.None));
            Assert.That(Shown(badge), Is.False, "nothing is deliverable");
            Assert.That(shell.ContractsTabText, Is.EqualTo("Contracts"));
            Assert.That(shell.TabButton(ShellTab.Contracts).tooltip, Is.EqualTo("Contracts"));

            AddStock(session, PlateResource, PlateRequired);
            yield return Sync();
            Assert.That(shell.ContractsDeliverable, Is.EqualTo(1));
            Assert.That(shell.ContractsBadgeForm, Is.Not.EqualTo(RunShellDocument.ContractsBadgeMode.None));
            Assert.That(shell.ContractsTabText, Is.EqualTo(ExpectedTabText(shell, 1)));
            Assert.That(Shown(badge), Is.EqualTo(shell.ContractsBadgeForm == RunShellDocument.ContractsBadgeMode.Dot), "only the compact dot is an overlay");
            Assert.That(shell.TabButton(ShellTab.Contracts).tooltip, Is.EqualTo("Contracts, 1 deliverable"), "the accessible name carries the number");

            AddStock(session, GearResource, GearRequired);
            yield return Sync();
            Assert.That(shell.ContractsDeliverable, Is.EqualTo(2));
            Assert.That(shell.ContractsTabText, Is.EqualTo(ExpectedTabText(shell, 2)));
            Assert.That(shell.TabButton(ShellTab.Contracts).tooltip, Is.EqualTo("Contracts, 2 deliverable"));

            Assert.That(commands.Sell(PlateResource, PlateRequired).IsSuccess, Is.True);
            Assert.That(commands.Sell(GearResource, GearRequired).IsSuccess, Is.True);
            yield return Sync();
            Assert.That(shell.ContractsDeliverable, Is.Zero, "cleared after a sale lowered the stock");
            Assert.That(Shown(badge), Is.False);
            Assert.That(badge.text, Is.Empty);
            Assert.That(shell.ContractsTabText, Is.EqualTo("Contracts"));
            Assert.That(shell.TabButton(ShellTab.Contracts).tooltip, Is.EqualTo("Contracts"));
        }

        [UnityTest]
        public IEnumerator TheBadgeFollowsTheActiveContractUntilItIsDelivered()
        {
            yield return Open(ShellScreens.All().First(), 100, 1, Plate);
            Submit(panel.AcceptButton(Plate));
            yield return Sync();
            Assert.That(rig.Shell.ContractsDeliverable, Is.Zero, "accepted, but the stock is short");

            AddStock(session, PlateResource, PlateRequired);
            yield return Sync();
            Assert.That(rig.Shell.ContractsDeliverable, Is.EqualTo(1), "the active contract can be delivered");

            Submit(panel.DeliverButton);
            yield return Sync();
            Assert.That(rig.Shell.ContractsDeliverable, Is.Zero, "delivered, nothing left to deliver");
        }

        [UnityTest]
        public IEnumerator TheBadgeNeverChangesTheTabSizeAndStaysInsideTheTab([ValueSource(nameof(Screens))] ShellScreen screen, [ValueSource(nameof(Percents))] int percent)
        {
            rig = ShellRig.Create(screen, null, percent);
            rigs.Add(rig);
            yield return rig.Layout();
            var shell = rig.Shell;
            var tab = shell.TabButton(ShellTab.Contracts);
            yield return ShellRig.Settle();
            var before = tab.worldBound;
            AssertShellLayout(rig, "badge cleared");

            foreach (var count in new[] { 1, 7, 12 })
            {
                shell.SetContractsBadge(count);
                yield return ShellRig.Settle();
                var state = $"badge {count}, form {shell.ContractsBadgeForm}";
                Assert.That(shell.ContractsBadgeForm, Is.Not.EqualTo(RunShellDocument.ContractsBadgeMode.None), state);
                Assert.That(shell.ContractsTabText, Is.EqualTo(ExpectedTabText(shell, count)), state);
                Assert.That(tab.worldBound.width, Is.EqualTo(before.width).Within(0.5f), $"{screen} @{percent}%: {state}: the tab keeps its width");
                Assert.That(tab.worldBound.height, Is.EqualTo(before.height).Within(0.5f), $"{screen} @{percent}%: {state}: the tab keeps its height");
                if (shell.ContractsBadgeForm == RunShellDocument.ContractsBadgeMode.Dot)
                {
                    AssertWithin(tab.worldBound, shell.ContractsBadge.worldBound, $"{screen} @{percent}%: {state}: the dot stays inside its tab");
                }
                Assert.That(shell.ContractsBadge.focusable, Is.False, "the badge is no focus stop");
                AssertShellLayout(rig, $"{screen} @{percent}% {state}");
            }

            // A focused tab draws a thicker border (existing shell behaviour), so focus is checked after the size checks.
            tab.Focus();
            yield return ShellRig.Settle();
            Assert.That(Focused(tab), Is.SameAs(tab), "the tab stays focusable with a badge");

            shell.SetContractsBadge(0);
            yield return ShellRig.Settle();
            Assert.That(Shown(shell.ContractsBadge), Is.False);
            Assert.That(shell.ContractsTabText, Is.EqualTo("Contracts"));
        }

        [UnityTest]
        public IEnumerator TheBadgeFallsBackToTheDotWhereTheLabelDoesNotFit()
        {
            var phone = ShellScreens.All().First(screen => screen.Name.StartsWith("phone-portrait 360", System.StringComparison.Ordinal));
            rig = ShellRig.Create(phone, null, 200);
            rigs.Add(rig);
            yield return rig.Layout();
            rig.Shell.SetContractsBadge(3);
            yield return ShellRig.Settle();
            Assert.That(rig.Shell.ContractsBadgeForm, Is.EqualTo(RunShellDocument.ContractsBadgeMode.Dot), "a 360 pt phone at 200 percent text has no room for a count");
            Assert.That(rig.Shell.ContractsBadge.text, Is.EqualTo(RunShellDocument.DotGlyph));
            Assert.That(rig.Shell.TabButton(ShellTab.Contracts).tooltip, Is.EqualTo("Contracts, 3 deliverable"), "the name still carries the number");
        }

        // ---------------------------------------------------------------------------------------------------
        // Layout at every screen class and text scale
        // ---------------------------------------------------------------------------------------------------

        [UnityTest]
        public IEnumerator TheActiveDeliveryCardWithEveryLineFitsAtEveryScreenClassAndTextScale([ValueSource(nameof(Screens))] ShellScreen screen, [ValueSource(nameof(Percents))] int percent)
        {
            yield return Open(screen, percent, 1, Plate, Gear, TimedBar);
            Submit(panel.AcceptButton(Plate));
            Assert.That(session.State.TrySetAutoSell(PlateResource, true), Is.EqualTo(RunStateResult.Ok));
            Submit(panel.AutoDeliverButton);
            session.State.Contracts.SetAutoDeliverBlocked(true);
            feed.Invalidate();
            yield return Sync();

            Assert.That(Shown(panel.AutoDeliverButton), Is.True);
            Assert.That(Shown(panel.ActiveAutoHint), Is.True);
            Assert.That(Shown(panel.ActiveBlocked), Is.True);
            Assert.That(Shown(panel.ActiveHint), Is.True);
            yield return AssertPanelLayout(rig, panel.Root, "active delivery card with auto-deliver, blocked and auto-sell lines", percent);
            AssertTextButtonsHaveNoChildren(panel.Root, "contracts panel");
        }

        [UnityTest]
        public IEnumerator TheProductionCardFitsAtEveryScreenClassAndTextScale([ValueSource(nameof(Screens))] ShellScreen screen, [ValueSource(nameof(Percents))] int percent)
        {
            yield return Open(screen, percent, 1, ProducePlate, Plate);
            Submit(panel.AcceptButton(ProducePlate));
            Assert.That(session.State.Contracts.TryCountProduced(ProduceRequired * 2 / 5, -1, 0, 0, 0), Is.EqualTo(RunStateResult.Ok));
            yield return Sync();

            Assert.That(Shown(panel.ProduceProgress), Is.True);
            yield return AssertPanelLayout(rig, panel.Root, "active production card", percent);
        }

        [UnityTest]
        public IEnumerator TheOffersWithCooldownAndBonusLinesFitAtEveryScreenClassAndTextScale([ValueSource(nameof(Screens))] ShellScreen screen, [ValueSource(nameof(Percents))] int percent)
        {
            yield return Open(screen, percent, 2, Plate, Gear, TimedBar, BonusPlate, ProducePlate);
            Advance(session, 1000);
            Submit(panel.AcceptButton(Plate));
            AddStock(session, PlateResource, PlateRequired);
            yield return Sync();
            Submit(panel.DeliverButton);
            yield return Sync();

            Assert.That(Shown(panel.OfferCooldown(Plate)), Is.True);
            Assert.That(Shown(panel.OfferCooldown(BonusPlate)), Is.True);
            yield return AssertPanelLayout(rig, panel.Root, "offers with cooldown and bonus lines", percent);

            var left = Offer(BonusPlate).BonusWindowRemainingMs.Value;
            Advance(session, left + 1000);
            yield return Sync();
            Assert.That(panel.OfferCooldown(BonusPlate).text, Is.EqualTo("Bonus ended"));
            yield return AssertPanelLayout(rig, panel.Root, "offers after the bonus ended", percent);
        }

        [UnityTest]
        public IEnumerator TheExpiredCardWithTheShortStockLineFitsAtEveryScreenClassAndTextScale([ValueSource(nameof(Screens))] ShellScreen screen, [ValueSource(nameof(Percents))] int percent)
        {
            yield return Open(screen, percent, 1, TimedBar, Plate);
            Submit(panel.AcceptButton(TimedBar));
            Submit(panel.AutoDeliverButton);
            yield return ShellRig.Settle();
            Advance(session, BarLimitMs + 1000);
            yield return Sync();

            Assert.That(Shown(panel.ExpiredAutoNote), Is.True);
            yield return AssertPanelLayout(rig, panel.Root, "expired card with the short-stock line", percent);
        }
    }
}
#endif
