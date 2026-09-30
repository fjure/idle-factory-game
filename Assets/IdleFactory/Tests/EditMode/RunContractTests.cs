using System;
using System.Linq;
using IdleFactory.Core.Numerics;
using IdleFactory.Run.Application;
using IdleFactory.Run.Content;
using IdleFactory.Run.Domain;
using IdleFactory.Tests.EditMode.Fixtures.Run;
using NUnit.Framework;

namespace IdleFactory.Tests.EditMode
{
    /// <summary>
    /// The contract commands and the ContractsSnapshot (T100, contracts/run-application.md, FR-012, FR-014, FR-015,
    /// FR-016, US2 scenarios 1-3 online part, OD-4): acceptance fixes the requirement and deadline, delivery at the
    /// inclusive deadline and one millisecond later, exactly-once grants, re-accept scaling, abandonment, expiry that
    /// grants nothing and keeps its outcome, the auto-sell versus contract stock conflict, each contract kind and the
    /// atomic rejection of an overflowing reward.
    ///
    /// Fixture offers (quantity / cash / RP by completion index 0, 1, 2):
    /// plate 5, 6, 7 / 100, 101, 102 / 2 and an Item at indices 0 and 2; gear 3, 3, 3 / 500; timed bar 10, 11, 12 /
    /// 200 / 1 with a 60 s duration.
    /// </summary>
    public sealed class RunContractTests
    {
        private const string Plate = "contract.plate";
        private const string Gear = "contract.gear";
        private const string TimedBar = "contract.timed-bar";
        private const string Boost = "item.boost";
        private const long DurationMs = 60_000;
        private const long Ms = RunSession.TicksPerMs;

        private static ContractOfferDef[] Offers(UnlockCondition gearUnlock = null)
        {
            var always = new UnlockCondition("always");
            return new[]
            {
                new ContractOfferDef(Plate, "plate", "iron.plate", new Curve(5, 1_010_000, "ceil"), null,
                    new[]
                    {
                        new ContractRewardDef("cash", new Curve(100, 1_010_000, "floor")),
                        new ContractRewardDef("rp", new Curve(2, 1_000_000, "floor")),
                        new ContractRewardDef("item", null, Boost, new[] { 0, 2 }),
                    }, always),
                new ContractOfferDef(Gear, "gear", "iron.gear", new Curve(3, 1_000_000, "ceil"), null,
                    new[] { new ContractRewardDef("cash", new Curve(500, 1_000_000, "floor")) }, gearUnlock ?? always),
                new ContractOfferDef(TimedBar, "timedBar", "iron.bar", new Curve(10, 1_010_000, "ceil"), DurationMs,
                    new[]
                    {
                        new ContractRewardDef("cash", new Curve(200, 1_000_000, "floor")),
                        new ContractRewardDef("rp", new Curve(1, 1_000_000, "floor")),
                    }, always),
            };
        }

        private static RunCommandFixtures.Rig Rig(long cash = 1000, long rp = 0, UnlockCondition gearUnlock = null)
        {
            var o = new RunCommandFixtures.Options
            {
                Offers = Offers(gearUnlock),
                Items = new[]
                {
                    new ItemDef(Boost, 1, "Boost", "F003-ITEM-BOOST", 60_000, "factory", new string[0],
                        new EffectDef("machineSpeed", "allMachines", null, 1_500_000)),
                },
            };
            RunCommandFixtures.Rig rig = RunCommandFixtures.NewRig(o, cash, rp: rp);
            rig.Advance(0); // the first settle unlocks the offers (T140)
            return rig;
        }

        private static void Stock(RunCommandFixtures.Rig rig, string resourceId, long units) => RunSimHarness.Stock(rig.State, resourceId, units);

        private static ContractAcceptResult Accept(RunCommandFixtures.Rig rig, string offerId)
        {
            RunCommandResult<ContractAcceptResult> r = rig.Commands.AcceptContract(offerId);
            Assert.That(r.IsSuccess, Is.True, r.ReasonCode);
            return r.Value;
        }

        private static ContractDeliveryResult Deliver(RunCommandFixtures.Rig rig)
        {
            RunCommandResult<ContractDeliveryResult> r = rig.Commands.DeliverContract();
            Assert.That(r.IsSuccess, Is.True, r.ReasonCode);
            return r.Value;
        }

        private static long Items(RunCommandFixtures.Rig rig) => rig.Session.Permanent.ItemCount(Boost);

        /// <summary>Everything a rejected command must leave alone, including Cash, RP, Items and the contract board.</summary>
        private static string Full(RunCommandFixtures.Rig rig)
        {
            RunState s = rig.State;
            ContractBoard b = s.Contracts;
            return RunCommandFixtures.Snapshot(s) + "cash=" + s.Cash + " rp=" + s.Rp + " items=" + Items(rig) +
                " active=" + (b.Active == null ? "-" : b.Active.OfferId + "/" + b.Active.RequiredUnits + "/" + b.Active.DeadlineUtcMs) +
                " counts=" + string.Join(",", b.CompletionCounts().Select(c => c.Id + ":" + c.Count)) +
                " expired=" + (b.LastExpired.HasValue ? b.LastExpired.Value.OfferId : "-") + " tl=" + rig.Session.ActiveTicksSinceAnchor;
        }

        private static void AssertRejected<T>(RunCommandFixtures.Rig rig, Func<RunCommandResult<T>> command, string code)
        {
            string before = Full(rig);
            long structure = rig.Session.StructureRevision, quantity = rig.Session.QuantityRevision;

            RunCommandResult<T> result = command();

            Assert.That(result.IsSuccess, Is.False, "expected " + code);
            Assert.That(result.Outcome, Is.EqualTo(RunCommandOutcome.Rejected));
            Assert.That(result.ReasonCode, Is.EqualTo(code));
            Assert.That(result.Value, Is.EqualTo(default(T)), "a rejection carries no value");
            Assert.That(Full(rig), Is.EqualTo(before), "a rejection never changes state");
            Assert.That(rig.Session.StructureRevision, Is.EqualTo(structure));
            Assert.That(rig.Session.QuantityRevision, Is.EqualTo(quantity));
        }

        // ------------------------------------------------------------------------------------------------
        // AcceptContract
        // ------------------------------------------------------------------------------------------------

        [Test]
        public void Offers_AreUnlockedByTheFirstSettle_AndAcceptFixesTheRequirementAtTheCompletionCount()
        {
            var rig = Rig();
            Assert.That(rig.State.IsUnlocked(Plate), Is.True);
            long structure = rig.Session.StructureRevision;

            ContractAcceptResult accepted = Accept(rig, Plate);

            Assert.That(accepted.OfferId, Is.EqualTo(Plate));
            Assert.That(accepted.CompletionIndex, Is.Zero);
            Assert.That(accepted.RequiredUnits, Is.EqualTo(5));
            Assert.That(accepted.DeadlineUtcMs, Is.Null, "an untimed contract has no deadline");
            Assert.That(rig.State.Contracts.Active.OfferId, Is.EqualTo(Plate));
            Assert.That(rig.State.Contracts.Active.RequiredUnits, Is.EqualTo(5));
            Assert.That(rig.Session.StructureRevision, Is.GreaterThan(structure));
        }

        [Test]
        public void Accept_TimedBar_SetsTheDeadlineToTimelinePlusDuration()
        {
            var rig = Rig();
            rig.Advance(5_000 * Ms);
            long timeline = rig.Session.TimelineUtcMs;

            ContractAcceptResult accepted = Accept(rig, TimedBar);

            Assert.That(accepted.AcceptedUtcMs, Is.EqualTo(timeline));
            Assert.That(accepted.DeadlineUtcMs, Is.EqualTo(timeline + DurationMs));
            Assert.That(accepted.RequiredUnits, Is.EqualTo(10));
            Assert.That(rig.State.Contracts.Active.DeadlineUtcMs, Is.EqualTo(timeline + DurationMs));
        }

        [Test]
        public void Accept_RejectsAnActiveContract_AnUnknownOrLockedOffer_AndABlockedLifecycle()
        {
            var rig = Rig(gearUnlock: new UnlockCondition("factoryLevelAtLeast", null, 5));
            Assert.That(rig.State.IsUnlocked(Gear), Is.False);

            AssertRejected(rig, () => rig.Commands.AcceptContract(Gear), RunReasons.CommandInvalid);
            AssertRejected(rig, () => rig.Commands.AcceptContract("contract.unknown"), RunReasons.CommandInvalid);
            AssertRejected(rig, () => rig.Commands.AcceptContract(null), RunReasons.CommandInvalid);

            Accept(rig, Plate);
            AssertRejected(rig, () => rig.Commands.AcceptContract(TimedBar), RunReasons.ContractActiveExists);
            AssertRejected(rig, () => rig.Commands.AcceptContract(Plate), RunReasons.ContractActiveExists);

            rig.Session.SetLifecycleBlocked(true);
            AssertRejected(rig, () => rig.Commands.AcceptContract(TimedBar), RunReasons.LifecycleBlocked);
            AssertRejected(rig, () => rig.Commands.DeliverContract(), RunReasons.LifecycleBlocked);
            AssertRejected(rig, () => rig.Commands.AbandonContract(), RunReasons.LifecycleBlocked);
        }

        // ------------------------------------------------------------------------------------------------
        // DeliverContract
        // ------------------------------------------------------------------------------------------------

        [Test]
        public void Deliver_ConsumesExactlyTheRequiredUnits_AndGrantsEveryRewardOnce()
        {
            var rig = Rig(cash: 1000, rp: 7);
            Accept(rig, Plate);
            Stock(rig, "iron.plate", 8);
            long structure = rig.Session.StructureRevision, quantity = rig.Session.QuantityRevision;

            ContractDeliveryResult delivered = Deliver(rig);

            Assert.That(delivered.UnitsConsumed, Is.EqualTo(5));
            Assert.That(delivered.CashReward, Is.EqualTo(100));
            Assert.That(delivered.RpReward, Is.EqualTo(2));
            Assert.That(delivered.NewCompletionCount, Is.EqualTo(1));
            Assert.That(delivered.ItemsGranted.Select(i => i.Id + ":" + i.Count), Is.EqualTo(new[] { Boost + ":1" }));
            Assert.That(rig.State.Inventory.Units("iron.plate"), Is.EqualTo(3), "exactly the required units are consumed");
            Assert.That(rig.State.Cash, Is.EqualTo(1100));
            Assert.That(rig.State.Rp, Is.EqualTo(9));
            Assert.That(Items(rig), Is.EqualTo(1));
            Assert.That(rig.State.Contracts.Active, Is.Null);
            Assert.That(rig.State.Contracts.CompletionCount(Plate), Is.EqualTo(1));
            Assert.That(rig.Session.StructureRevision, Is.GreaterThan(structure));
            Assert.That(rig.Session.QuantityRevision, Is.GreaterThan(quantity));
        }

        [Test]
        public void Deliver_ARepeatDeliveryCannotGrantTwice()
        {
            var rig = Rig();
            Accept(rig, Plate);
            Stock(rig, "iron.plate", 20);
            Deliver(rig);
            long cash = rig.State.Cash, rp = rig.State.Rp, items = Items(rig);

            AssertRejected(rig, () => rig.Commands.DeliverContract(), RunReasons.ContractNoneActive);

            Assert.That(rig.State.Cash, Is.EqualTo(cash));
            Assert.That(rig.State.Rp, Is.EqualTo(rp));
            Assert.That(Items(rig), Is.EqualTo(items));
            Assert.That(rig.State.Contracts.CompletionCount(Plate), Is.EqualTo(1));
            Assert.That(rig.State.Inventory.Units("iron.plate"), Is.EqualTo(15));
        }

        [Test]
        public void Deliver_RejectsWithoutAContract_AndWithInsufficientStock()
        {
            var rig = Rig();
            AssertRejected(rig, () => rig.Commands.DeliverContract(), RunReasons.ContractNoneActive);

            Accept(rig, Plate);
            Stock(rig, "iron.plate", 4);
            AssertRejected(rig, () => rig.Commands.DeliverContract(), RunReasons.StockInsufficient);
            Stock(rig, "iron.plate", 1);
            Deliver(rig);
            Assert.That(rig.State.Inventory.Units("iron.plate"), Is.Zero);
        }

        [Test]
        public void Deliver_ScalesTheQuantityAndTheRewardsWithTheCompletionCount_OfTheCurrentRun()
        {
            var rig = Rig();
            long[] required = { 5, 6, 7 };
            long[] cash = { 100, 101, 102 };
            long[] items = { 1, 1, 2 }; // Items are due at completion indices 0 and 2 only

            for (int i = 0; i < 3; i++)
            {
                ContractAcceptResult accepted = Accept(rig, Plate);
                Assert.That(accepted.CompletionIndex, Is.EqualTo(i));
                Assert.That(accepted.RequiredUnits, Is.EqualTo(required[i]), "re-accept scaling, completion " + i);
                Stock(rig, "iron.plate", required[i]);
                long before = rig.State.Cash;
                ContractDeliveryResult delivered = Deliver(rig);
                Assert.That(delivered.CashReward, Is.EqualTo(cash[i]));
                Assert.That(rig.State.Cash - before, Is.EqualTo(cash[i]));
                Assert.That(Items(rig), Is.EqualTo(items[i]), "Item due at indices 0 and 2");
                Assert.That(delivered.ItemsGranted.Count, Is.EqualTo(i == 1 ? 0 : 1));
            }
            Assert.That(rig.State.Contracts.CompletionCount(Plate), Is.EqualTo(3));
        }

        [Test]
        public void Deliver_EachKind_GearAndTimedBar_PayTheirOwnRewards()
        {
            var rig = Rig(cash: 0);
            Accept(rig, Gear);
            Stock(rig, "iron.gear", 3);
            ContractDeliveryResult gear = Deliver(rig);
            Assert.That(gear.CashReward, Is.EqualTo(500));
            Assert.That(gear.RpReward, Is.Zero);
            Assert.That(gear.ItemsGranted, Is.Empty);
            Assert.That(rig.State.Cash, Is.EqualTo(500));

            ContractAcceptResult bar = Accept(rig, TimedBar);
            Assert.That(bar.DeadlineUtcMs, Is.Not.Null);
            Stock(rig, "iron.bar", 10);
            ContractDeliveryResult delivered = Deliver(rig);
            Assert.That(delivered.CashReward, Is.EqualTo(200));
            Assert.That(delivered.RpReward, Is.EqualTo(1));
            Assert.That(rig.State.Cash, Is.EqualTo(700));
            Assert.That(rig.State.Rp, Is.EqualTo(1));
            Assert.That(rig.State.Inventory.Units("iron.bar"), Is.Zero);
        }

        // ------------------------------------------------------------------------------------------------
        // Deadline and expiry
        // ------------------------------------------------------------------------------------------------

        [Test]
        public void Deliver_ExactlyAtTheDeadlineSucceeds()
        {
            var rig = Rig();
            ContractAcceptResult accepted = Accept(rig, TimedBar);
            Stock(rig, "iron.bar", 10);
            rig.Advance(DurationMs * Ms);
            Assert.That(rig.Session.TimelineUtcMs, Is.EqualTo(accepted.DeadlineUtcMs.Value));
            Assert.That(rig.State.Contracts.Active, Is.Not.Null, "the deadline instant itself is not expiry");

            ContractDeliveryResult delivered = Deliver(rig);

            Assert.That(delivered.CashReward, Is.EqualTo(200));
            Assert.That(rig.State.Contracts.CompletionCount(TimedBar), Is.EqualTo(1));
        }

        [Test]
        public void Deliver_OneMillisecondAfterTheDeadline_IsExpired_AndGrantsNothing()
        {
            var rig = Rig();
            Accept(rig, TimedBar);
            Stock(rig, "iron.bar", 10);
            rig.Advance((DurationMs + 1) * Ms);
            Assert.That(rig.State.Contracts.Active, Is.Null, "the simulator expires the contract at deadline + 1 ms");

            AssertRejected(rig, () => rig.Commands.DeliverContract(), RunReasons.ContractExpired);

            Assert.That(rig.State.Cash, Is.EqualTo(1000));
            Assert.That(rig.State.Rp, Is.Zero);
            Assert.That(rig.State.Inventory.Units("iron.bar"), Is.EqualTo(10));
        }

        [Test]
        public void Deliver_AfterTheClockPassedTheDeadlineBeforeTheNextSettle_IsExpired()
        {
            var rig = Rig();
            ContractAcceptResult accepted = Accept(rig, TimedBar);
            Stock(rig, "iron.bar", 10);
            RunCommandResult<long> rebound = rig.Session.RebindAnchor(accepted.DeadlineUtcMs.Value + 1);
            Assert.That(rebound.IsSuccess, Is.True);
            Assert.That(rig.State.Contracts.Active, Is.Not.Null, "a re-bound clock expires nothing by itself");

            AssertRejected(rig, () => rig.Commands.DeliverContract(), RunReasons.ContractExpired);
        }

        [Test]
        public void Expiry_Online_AdvancesNothingGrantsNothing_AndKeepsTheOutcome()
        {
            var rig = Rig(cash: 1000, rp: 3);
            ContractAcceptResult accepted = Accept(rig, TimedBar);
            Stock(rig, "iron.bar", 10);
            long structure = rig.Session.StructureRevision;

            rig.Advance((DurationMs + 500) * Ms);

            Assert.That(rig.State.Contracts.Active, Is.Null);
            Assert.That(rig.State.Contracts.CompletionCount(TimedBar), Is.Zero, "expiry does not advance the scaling index");
            Assert.That(rig.State.Cash, Is.EqualTo(1000));
            Assert.That(rig.State.Rp, Is.EqualTo(3));
            Assert.That(Items(rig), Is.Zero);
            Assert.That(rig.State.Inventory.Units("iron.bar"), Is.EqualTo(10));
            Assert.That(rig.Session.StructureRevision, Is.GreaterThan(structure), "the expiry is a structural change");

            ContractsSnapshot snapshot = RunSnapshots.Contracts(rig.Session);
            Assert.That(snapshot.Active, Is.Null);
            Assert.That(snapshot.LastExpired, Is.Not.Null, "the outcome card reads the expired outcome");
            Assert.That(snapshot.LastExpired.OfferId, Is.EqualTo(TimedBar));
            Assert.That(snapshot.LastExpired.Kind, Is.EqualTo("timedBar"));
            Assert.That(snapshot.LastExpired.RequiredUnits, Is.EqualTo(10));
            Assert.That(snapshot.LastExpired.DeadlineUtcMs, Is.EqualTo(accepted.DeadlineUtcMs.Value));

            // Abandon has no untimed contract to act on, and the outcome stays until the next acceptance.
            AssertRejected(rig, () => rig.Commands.AbandonContract(), RunReasons.ContractNoneActive);
            Assert.That(RunSnapshots.Contracts(rig.Session).LastExpired, Is.Not.Null);
            ContractAcceptResult again = Accept(rig, TimedBar);
            Assert.That(again.RequiredUnits, Is.EqualTo(10), "the count is unchanged, so the requirement is too");
            Assert.That(RunSnapshots.Contracts(rig.Session).LastExpired, Is.Null, "a new acceptance dismisses the outcome");
        }

        // ------------------------------------------------------------------------------------------------
        // AbandonContract (OD-4)
        // ------------------------------------------------------------------------------------------------

        [Test]
        public void Abandon_ClearsAnUntimedContract_WithNoRewardNoPenaltyAndTheCountUnchanged()
        {
            var rig = Rig(cash: 1000, rp: 3);
            Accept(rig, Plate);
            Stock(rig, "iron.plate", 6);
            Deliver(rig); // count 1
            Accept(rig, Plate);
            long structure = rig.Session.StructureRevision;
            long cash = rig.State.Cash, rp = rig.State.Rp, items = Items(rig), plates = rig.State.Inventory.Units("iron.plate");

            RunCommandResult<ContractAbandonResult> abandoned = rig.Commands.AbandonContract();

            Assert.That(abandoned.IsSuccess, Is.True, abandoned.ReasonCode);
            Assert.That(abandoned.Value.OfferId, Is.EqualTo(Plate));
            Assert.That(abandoned.Value.CompletionCount, Is.EqualTo(1));
            Assert.That(rig.State.Contracts.Active, Is.Null);
            Assert.That(rig.State.Contracts.CompletionCount(Plate), Is.EqualTo(1), "OD-4: the count is unchanged");
            Assert.That(rig.State.Cash, Is.EqualTo(cash));
            Assert.That(rig.State.Rp, Is.EqualTo(rp));
            Assert.That(Items(rig), Is.EqualTo(items));
            Assert.That(rig.State.Inventory.Units("iron.plate"), Is.EqualTo(plates), "no penalty: no stock is taken");
            Assert.That(rig.Session.StructureRevision, Is.GreaterThan(structure));
            Assert.That(RunSnapshots.Contracts(rig.Session).LastExpired, Is.Null, "abandonment is not an expiry");
            Assert.That(Accept(rig, Plate).RequiredUnits, Is.EqualTo(6), "the next acceptance keeps the scaling index");
        }

        [Test]
        public void Abandon_RejectsWithoutAContract_AndATimedContract()
        {
            var rig = Rig();
            AssertRejected(rig, () => rig.Commands.AbandonContract(), RunReasons.ContractNoneActive);

            Accept(rig, TimedBar);
            AssertRejected(rig, () => rig.Commands.AbandonContract(), RunReasons.CommandInvalid);
            Assert.That(rig.State.Contracts.Active.OfferId, Is.EqualTo(TimedBar));
        }

        // ------------------------------------------------------------------------------------------------
        // Auto-sell versus contract stock
        // ------------------------------------------------------------------------------------------------

        [Test]
        public void AutoSell_SellsTheContractStockAtOnce_SoDeliveryNeedsItSwitchedOff()
        {
            var rig = Rig(cash: 0);
            Accept(rig, Plate);
            Stock(rig, "iron.plate", 10);

            Assert.That(rig.Commands.SetAutoSell("iron.plate", true).IsSuccess, Is.True);
            Assert.That(rig.State.Inventory.Units("iron.plate"), Is.Zero, "enabling auto-sell sold the whole stock");
            Assert.That(rig.State.Cash, Is.EqualTo(80));
            AssertRejected(rig, () => rig.Commands.DeliverContract(), RunReasons.StockInsufficient);
            ActiveContractSnapshot active = RunSnapshots.Contracts(rig.Session).Active;
            Assert.That(active.CanDeliver, Is.False);
            Assert.That(active.DeliverReason, Is.EqualTo(RunReasons.StockInsufficient));
            Assert.That(active.MissingUnits, Is.EqualTo(5));

            Assert.That(rig.Commands.SetAutoSell("iron.plate", false).IsSuccess, Is.True);
            Stock(rig, "iron.plate", 7);
            ContractDeliveryResult delivered = Deliver(rig);
            Assert.That(delivered.UnitsConsumed, Is.EqualTo(5));
            Assert.That(rig.State.Inventory.Units("iron.plate"), Is.EqualTo(2), "without auto-sell the rest stays");
            Assert.That(rig.State.Cash, Is.EqualTo(80 + 100));
        }

        [Test]
        public void AutoSell_OnDuringDelivery_TheSettleSellsOnlyWhatTheContractLeft()
        {
            var rig = Rig(cash: 0);
            Assert.That(rig.Commands.SetAutoSell("iron.plate", true).IsSuccess, Is.True);
            Accept(rig, Plate);
            Stock(rig, "iron.plate", 7); // arrives without a settle, as a completion does between two settles

            ContractDeliveryResult delivered = Deliver(rig);

            Assert.That(delivered.UnitsConsumed, Is.EqualTo(5));
            Assert.That(rig.State.Inventory.Units("iron.plate"), Is.Zero, "the leftover 2 were auto-sold by the command settle");
            Assert.That(rig.State.Cash, Is.EqualTo(100 + 2 * 8), "the reward plus the sale of the leftover, and none of the delivered units");
        }

        // ------------------------------------------------------------------------------------------------
        // Overflow (atomic rejection)
        // ------------------------------------------------------------------------------------------------

        [Test]
        public void Deliver_RejectsAnOverflowingCashReward_Atomically()
        {
            var rig = Rig(cash: ExactBound.Value - 50);
            Accept(rig, Plate);
            Stock(rig, "iron.plate", 5);

            AssertRejected(rig, () => rig.Commands.DeliverContract(), RunReasons.ValueLimit);

            Assert.That(rig.State.Inventory.Units("iron.plate"), Is.EqualTo(5), "no stock consumed");
            Assert.That(rig.State.Contracts.Active.OfferId, Is.EqualTo(Plate), "the contract stays active");
            Assert.That(rig.State.Contracts.CompletionCount(Plate), Is.Zero);
            Assert.That(Items(rig), Is.Zero, "no Item granted");
        }

        [Test]
        public void Deliver_RejectsAnOverflowingRpReward_Atomically()
        {
            var rig = Rig(cash: 10, rp: ExactBound.Value - 1);
            Accept(rig, Plate);
            Stock(rig, "iron.plate", 5);

            AssertRejected(rig, () => rig.Commands.DeliverContract(), RunReasons.ValueLimit);

            Assert.That(rig.State.Inventory.Units("iron.plate"), Is.EqualTo(5));
            Assert.That(rig.State.Contracts.Active, Is.Not.Null);
            Assert.That(Items(rig), Is.Zero);
        }

        [Test]
        public void Deliver_RejectsWhenTheFollowingAutoSaleWouldOverflow_ButAcceptsWithoutTheLeftover()
        {
            // Cash after the reward fits (bound - 50), the sale of 10 leftover plates (80) does not.
            var rig = Rig(cash: ExactBound.Value - 150);
            Assert.That(rig.State.TrySetAutoSell("iron.plate", true), Is.EqualTo(RunStateResult.Ok));
            Accept(rig, Plate);
            Stock(rig, "iron.plate", 15);

            AssertRejected(rig, () => rig.Commands.DeliverContract(), RunReasons.ValueLimit);
            Assert.That(rig.State.Inventory.Units("iron.plate"), Is.EqualTo(15));
            Assert.That(rig.State.Cash, Is.EqualTo(ExactBound.Value - 150));

            // Exactly the required units: nothing is left to sell, so the delivery fits.
            var fits = Rig(cash: ExactBound.Value - 150);
            Assert.That(fits.State.TrySetAutoSell("iron.plate", true), Is.EqualTo(RunStateResult.Ok));
            Accept(fits, Plate);
            Stock(fits, "iron.plate", 5);
            Deliver(fits);
            Assert.That(fits.State.Cash, Is.EqualTo(ExactBound.Value - 50));
        }

        // ------------------------------------------------------------------------------------------------
        // ContractsSnapshot
        // ------------------------------------------------------------------------------------------------

        [Test]
        public void Snapshot_ListsOffersInOrdinalOrder_WithTheNextRequirementAndRewards()
        {
            var rig = Rig(gearUnlock: new UnlockCondition("factoryLevelAtLeast", null, 5));
            ContractsSnapshot snapshot = RunSnapshots.Contracts(rig.Session);

            Assert.That(snapshot.Offers.Select(o => o.OfferId), Is.EqualTo(new[] { Gear, Plate, TimedBar }));
            Assert.That(snapshot.Active, Is.Null);
            Assert.That(snapshot.LastExpired, Is.Null);
            ContractOfferSnapshot gear = snapshot.Offers[0];
            Assert.That(gear.Unlocked, Is.False);
            Assert.That(gear.CanAccept, Is.False);
            Assert.That(gear.AcceptReason, Is.EqualTo(RunReasons.CommandInvalid));
            Assert.That(gear.Unlock.Kind, Is.EqualTo("factoryLevelAtLeast"));

            ContractOfferSnapshot plate = snapshot.Offers[1];
            Assert.That(plate.Unlocked, Is.True);
            Assert.That(plate.CanAccept, Is.True);
            Assert.That(plate.AcceptReason, Is.Null);
            Assert.That(plate.RequiredUnits, Is.EqualTo(5));
            Assert.That(plate.DurationMs, Is.Null);
            Assert.That(plate.Rewards.Select(r => r.Kind + ":" + r.Amount + ":" + r.ItemId),
                Is.EqualTo(new[] { "cash:100:", "rp:2:", "item:1:" + Boost }));
            Assert.That(snapshot.Offers[2].DurationMs, Is.EqualTo(DurationMs));

            Accept(rig, Plate);
            snapshot = RunSnapshots.Contracts(rig.Session);
            Assert.That(snapshot.Offers[1].CanAccept, Is.False);
            Assert.That(snapshot.Offers[1].AcceptReason, Is.EqualTo(RunReasons.ContractActiveExists));
            Assert.That(snapshot.Offers[2].AcceptReason, Is.EqualTo(RunReasons.ContractActiveExists));
        }

        [Test]
        public void Snapshot_ActiveContract_ShowsProgressAgainstStockAndTheCountdown()
        {
            var rig = Rig();
            Accept(rig, TimedBar);
            Stock(rig, "iron.bar", 4);
            rig.Advance(15_000 * Ms);

            ActiveContractSnapshot active = RunSnapshots.Contracts(rig.Session).Active;

            Assert.That(active.OfferId, Is.EqualTo(TimedBar));
            Assert.That(active.Kind, Is.EqualTo("timedBar"));
            Assert.That(active.RequiredUnits, Is.EqualTo(10));
            Assert.That(active.StockUnits, Is.EqualTo(4));
            Assert.That(active.MissingUnits, Is.EqualTo(6));
            Assert.That(active.RemainingMs, Is.EqualTo(DurationMs - 15_000));
            Assert.That(active.CanDeliver, Is.False);
            Assert.That(active.DeliverReason, Is.EqualTo(RunReasons.StockInsufficient));
            Assert.That(active.CanAbandon, Is.False, "Abandon is absent for a timed contract");
            Assert.That(active.Rewards.Select(r => r.Kind + ":" + r.Amount), Is.EqualTo(new[] { "cash:200", "rp:1" }));

            Stock(rig, "iron.bar", 6);
            active = RunSnapshots.Contracts(rig.Session).Active;
            Assert.That(active.CanDeliver, Is.True);
            Assert.That(active.DeliverReason, Is.Null);
            Assert.That(active.MissingUnits, Is.Zero);

            var untimed = Rig();
            Accept(untimed, Plate);
            ActiveContractSnapshot plate = RunSnapshots.Contracts(untimed.Session).Active;
            Assert.That(plate.CanAbandon, Is.True);
            Assert.That(plate.RemainingMs, Is.Null);
            Assert.That(plate.DeadlineUtcMs, Is.Null);
        }
    }
}
