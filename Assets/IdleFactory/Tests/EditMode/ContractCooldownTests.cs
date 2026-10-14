using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using IdleFactory.Core.Numerics;
using IdleFactory.Run.Application;
using IdleFactory.Run.Content;
using IdleFactory.Run.Domain;
using IdleFactory.Run.Simulation;
using IdleFactory.Tests.EditMode.Fixtures.Run;
using NUnit.Framework;

namespace IdleFactory.Tests.EditMode
{
    /// <summary>
    /// T321 (issue #388, F5-R05, FR-005, FR-007, FR-007a): offer cooldowns as lazily evaluated UTC stamps started by every ending, the
    /// <c>contract.cooldown</c> and <c>contract.capacity</c> accept checks, the snapshot fields, the board <c>Version</c> in the publish tokens and
    /// the prestige reset (QA-8, QA-9).
    ///
    /// Fixture offers: plate (untimed, cooldown 30 s), timed bar (60 s, cooldown 45 s), bonus gear (untimed, cooldown 10 s, window 120 s), a free
    /// offer without cooldown, and two quantity probes at the capacity of storage level 1 (1,000) and one above.
    /// </summary>
    public sealed class ContractCooldownTests
    {
        internal const string Plate = "contract.plate";
        internal const string TimedBar = "contract.timed-bar";
        internal const string Bonus = "contract.bonus";
        internal const string Free = "contract.free";
        internal const string AtCapacity = "contract.at-capacity";
        internal const string OverCapacity = "contract.over-capacity";
        internal const long PlateCooldownMs = 30_000;
        internal const long TimedCooldownMs = 45_000;
        internal const long BonusCooldownMs = 10_000;
        internal const long TimedDurationMs = 60_000;
        internal const long BonusWindowMs = 120_000;
        internal const long Ms = RunSession.TicksPerMs;

        public enum Ending { Delivery, Abandon, Expiry }

        internal static ContractOfferDef[] Offers()
        {
            var always = new UnlockCondition("always");
            ContractRewardDef[] rp = { new ContractRewardDef("rp", new Curve(1, 1_000_000, "floor")) };
            return new[]
            {
                new ContractOfferDef(Plate, "plate", "iron.plate", new Curve(5, 1_010_000, "ceil"), null, rp, always,
                    rewardRatioPpm: 1_500_000, cooldownMs: PlateCooldownMs),
                new ContractOfferDef(TimedBar, "timedBar", "iron.bar", new Curve(10, 1_010_000, "ceil"), TimedDurationMs, rp, always,
                    rewardRatioPpm: 1_500_000, cooldownMs: TimedCooldownMs),
                new ContractOfferDef(Bonus, "gear", "iron.gear", new Curve(3, 1_000_000, "ceil"), null, rp, always, offerClass: "bonus",
                    rewardRatioPpm: 1_800_000, cooldownMs: BonusCooldownMs, bonus: new BonusDef(always, BonusWindowMs)),
                new ContractOfferDef(Free, "plate", "iron.ore", new Curve(4, 1_000_000, "ceil"), null, rp, always, rewardRatioPpm: 1_000_000),
                new ContractOfferDef(AtCapacity, "gear", "iron.gear", new Curve(1_000, 1_000_000, "ceil"), null, rp, always, rewardRatioPpm: 1_000_000),
                new ContractOfferDef(OverCapacity, "gear", "iron.gear", new Curve(1_001, 1_000_000, "ceil"), null, rp, always, rewardRatioPpm: 1_000_000),
            };
        }

        internal static RunCommandFixtures.Rig NewRig(long cash = 1_000)
        {
            RunCommandFixtures.Rig rig = RunCommandFixtures.NewRig(new RunCommandFixtures.Options { Offers = Offers() }, cash);
            rig.Advance(0); // the first settle unlocks the offers
            return rig;
        }

        internal static void AdvanceTo(RunCommandFixtures.Rig rig, long utcMs, long stepMs = 0)
        {
            while (rig.Session.TimelineUtcMs < utcMs)
            {
                long next = stepMs > 0 ? Math.Min(utcMs, rig.Session.TimelineUtcMs + stepMs) : utcMs;
                rig.Advance((next - rig.Session.TimelineUtcMs) * Ms);
            }
        }

        internal static ContractOfferSnapshot Offer(RunCommandFixtures.Rig rig, string offerId) =>
            RunSnapshots.Contracts(rig.Session).Offers.Single(o => o.OfferId == offerId);

        /// <summary>The board and the revisions as text, to prove a rejection changed nothing.</summary>
        internal static string Digest(RunCommandFixtures.Rig rig)
        {
            RunState state = rig.Session.State;
            ContractBoard board = state.Contracts;
            var b = new StringBuilder(RunCommandFixtures.Snapshot(state));
            b.Append("version=").Append(board.Version).Append(" total=").Append(board.RunCompletionTotal).Append('\n');
            b.Append("active=").Append(board.Active == null ? "-" : board.Active.OfferId + "@" + board.Active.AcceptedUtcMs + "/" + board.Active.DeadlineUtcMs).Append('\n');
            b.Append("expired=").Append(board.LastExpired.HasValue ? board.LastExpired.Value.OfferId + "@" + board.LastExpired.Value.DeadlineUtcMs : "-").Append('\n');
            foreach (string id in rig.Catalog.ContractOffers.Keys.OrderBy(k => k, StringComparer.Ordinal))
            {
                BonusOfferState bonus = board.BonusState(id);
                b.Append(id).Append(" ready=").Append(board.CooldownReadyAtUtcMs(id)).Append(" bonus=").Append(bonus.Triggered).Append('/')
                    .Append(bonus.WindowEndUtcMs).Append('/').Append(bonus.Consumed).Append(" count=").Append(board.CompletionCount(id)).Append('\n');
            }
            b.Append("rev=").Append(rig.Session.StructureRevision).Append('/').Append(rig.Session.QuantityRevision)
                .Append(" now=").Append(rig.Session.TimelineUtcMs);
            return b.ToString();
        }

        private static void Accept(RunCommandFixtures.Rig rig, string offerId)
        {
            RunCommandResult<ContractAcceptResult> r = rig.Commands.AcceptContract(offerId);
            Assert.That(r.IsSuccess, Is.True, offerId + ": " + r.ReasonCode);
        }

        private static void Deliver(RunCommandFixtures.Rig rig)
        {
            RunSimHarness.Stock(rig.State, rig.Catalog.ContractOffers[rig.State.Contracts.Active.OfferId].ResourceId, rig.State.Contracts.Active.RequiredUnits);
            RunCommandResult<ContractDeliveryResult> r = rig.Commands.DeliverContract();
            Assert.That(r.IsSuccess, Is.True, r.ReasonCode);
        }

        // ---- the three endings x live and offline x the exact readyAt (QA-9) --------------------------------------

        /// <summary>
        /// Ends a contract and returns (offer, end instant, departure instant of the absence). A delivery and an abandonment end at the command's
        /// timeline instant; an expiry ends at deadline + 1, whatever the way the time then passes.
        /// </summary>
        private static (string offer, long end, long departure) Begin(RunCommandFixtures.Rig rig, Ending ending)
        {
            switch (ending)
            {
                case Ending.Delivery:
                    Accept(rig, Plate);
                    AdvanceTo(rig, rig.Session.TimelineUtcMs + 1_234);
                    Deliver(rig);
                    return (Plate, rig.Session.TimelineUtcMs, rig.Session.TimelineUtcMs);
                case Ending.Abandon:
                    Accept(rig, Plate);
                    AdvanceTo(rig, rig.Session.TimelineUtcMs + 777);
                    Assert.That(rig.Commands.AbandonContract().IsSuccess, Is.True);
                    return (Plate, rig.Session.TimelineUtcMs, rig.Session.TimelineUtcMs);
                default:
                    Accept(rig, TimedBar);
                    long accepted = rig.Session.TimelineUtcMs;
                    return (TimedBar, accepted + TimedDurationMs + 1, accepted);
            }
        }

        [Test]
        public void EveryEndingStartsTheCooldownAtTheEndInstant_LiveAndOffline_ReadyExactlyAtTheStamp(
            [Values] Ending ending, [Values] bool offline, [Values] bool atReadyAt)
        {
            RunCommandFixtures.Rig rig = NewRig();
            (string offer, long end, long departure) = Begin(rig, ending);
            long cooldown = rig.Catalog.ContractOffers[offer].CooldownMs;
            long readyAt = end + cooldown;
            long target = atReadyAt ? readyAt : readyAt - 1;

            if (offline)
            {
                RunOfflineSummary summary = new OfflineSegmenter(rig.Catalog).Reconcile(rig.State, rig.Session.Permanent, departure, target);
                Assert.That(summary.Outcome, Is.EqualTo(RunOfflineOutcome.Applied));
                Assert.That(rig.Session.RebindAnchor(target).IsSuccess, Is.True);
            }
            else
            {
                AdvanceTo(rig, target, 7_919); // an awkward step, crossing the deadline and the end instant mid-step
            }

            ContractBoard board = rig.Session.State.Contracts;
            Assert.That(rig.Session.TimelineUtcMs, Is.EqualTo(target));
            Assert.That(board.Active, Is.Null);
            Assert.That(board.CooldownReadyAtUtcMs(offer), Is.EqualTo(readyAt), "the stamp is end + cooldown");
            Assert.That(board.LastExpired.HasValue, Is.EqualTo(ending == Ending.Expiry));
            ContractOfferSnapshot snapshot = Offer(rig, offer);
            Assert.That(snapshot.CooldownRemainingMs, Is.EqualTo(atReadyAt ? 0 : 1));

            RunCommandResult<ContractAcceptResult> accept = rig.Commands.AcceptContract(offer);
            if (atReadyAt)
            {
                Assert.That(snapshot.CanAccept, Is.True);
                Assert.That(accept.IsSuccess, Is.True, accept.ReasonCode);
            }
            else
            {
                Assert.That(snapshot.CanAccept, Is.False);
                Assert.That(snapshot.AcceptReason, Is.EqualTo(RunReasons.ContractCooldown));
                Assert.That(accept.ReasonCode, Is.EqualTo(RunReasons.ContractCooldown));
            }
        }

        [Test]
        public void TheExpiryStampIsPartitionInvariant_AndLiveEqualsOffline()
        {
            string Run(Func<RunCommandFixtures.Rig, long, long> pass)
            {
                RunCommandFixtures.Rig rig = NewRig();
                Accept(rig, TimedBar);
                long accepted = rig.Session.TimelineUtcMs;
                long after = accepted + TimedDurationMs + 1 + 20_000; // 20 s into the cooldown
                pass(rig, after);
                Assert.That(rig.Session.TimelineUtcMs, Is.EqualTo(after));
                return Digest(rig).Replace("rev=", "revision-ignored=");
            }

            string StripRevisions(string digest) => string.Join("\n", digest.Split('\n').Where(l => !l.StartsWith("revision-ignored=")));

            string whole = StripRevisions(Run((rig, to) => { AdvanceTo(rig, to); return 0; }));
            string oneMs = StripRevisions(Run((rig, to) =>
            {
                AdvanceTo(rig, to - 20_100, 9_973);
                AdvanceTo(rig, to - 19_900, 1);
                AdvanceTo(rig, to);
                return 0;
            }));
            string primes = StripRevisions(Run((rig, to) => { AdvanceTo(rig, to, 4_999); return 0; }));
            string offline = StripRevisions(Run((rig, to) =>
            {
                new OfflineSegmenter(rig.Catalog).Reconcile(rig.State, rig.Session.Permanent, rig.Session.TimelineUtcMs, to);
                rig.Session.RebindAnchor(to);
                return 0;
            }));

            Assert.That(oneMs, Is.EqualTo(whole));
            Assert.That(primes, Is.EqualTo(whole));
            Assert.That(offline, Is.EqualTo(whole), "an offline return gives the identical readyAt, Version and state");
        }

        [Test]
        public void ADeadlineDeliveryStartsTheCooldownAtTheDeliveryInstant_NotAtTheDeadlineOrAtExpiry()
        {
            RunCommandFixtures.Rig rig = NewRig();
            Accept(rig, TimedBar);
            long deadline = rig.State.Contracts.Active.DeadlineUtcMs.Value;
            AdvanceTo(rig, deadline); // the inclusive deadline still delivers
            Deliver(rig);
            Assert.That(rig.State.Contracts.CooldownReadyAtUtcMs(TimedBar), Is.EqualTo(deadline + TimedCooldownMs));
        }

        [Test]
        public void ANewCooldownReplacesTheOldOne_AndAZeroCooldownOfferNeverHasAStamp()
        {
            RunCommandFixtures.Rig rig = NewRig();
            Accept(rig, Plate);
            Deliver(rig);
            long first = rig.Session.TimelineUtcMs + PlateCooldownMs;
            Assert.That(rig.State.Contracts.CooldownReadyAtUtcMs(Plate), Is.EqualTo(first));
            AdvanceTo(rig, first);
            Accept(rig, Plate);
            AdvanceTo(rig, first + 5_000);
            Assert.That(rig.Commands.AbandonContract().IsSuccess, Is.True);
            Assert.That(rig.State.Contracts.CooldownReadyAtUtcMs(Plate), Is.EqualTo(first + 5_000 + PlateCooldownMs));

            Accept(rig, Free);
            Deliver(rig);
            Assert.That(rig.State.Contracts.CooldownReadyAtUtcMs(Free), Is.Null);
            Accept(rig, Free);
            Assert.That(Offer(rig, Free).CooldownRemainingMs, Is.Zero);
        }

        [Test]
        public void AnOfferInCooldownDoesNotBlockOtherOffers_AndTheCooldownIsPerOffer()
        {
            RunCommandFixtures.Rig rig = NewRig();
            Accept(rig, Plate);
            Deliver(rig);
            Assert.That(rig.Commands.AcceptContract(Plate).ReasonCode, Is.EqualTo(RunReasons.ContractCooldown));
            Accept(rig, TimedBar);
            Assert.That(Offer(rig, Plate).CooldownRemainingMs, Is.EqualTo(PlateCooldownMs));
            Assert.That(Offer(rig, TimedBar).CooldownRemainingMs, Is.Zero);
        }

        [Test]
        public void ACooldownRejectionChangesNothing_VersionAndRevisionsIncluded()
        {
            RunCommandFixtures.Rig rig = NewRig();
            Accept(rig, Plate);
            Deliver(rig);
            AdvanceTo(rig, rig.Session.TimelineUtcMs + 1_000);
            string before = Digest(rig);
            long version = rig.State.Contracts.Version;

            Assert.That(rig.Commands.AcceptContract(Plate).ReasonCode, Is.EqualTo(RunReasons.ContractCooldown));

            Assert.That(Digest(rig), Is.EqualTo(before));
            Assert.That(rig.State.Contracts.Version, Is.EqualTo(version));
        }

        [Test]
        public void TheRejectionOrderIsLockedThenActiveThenCooldownThenBonusThenCapacity()
        {
            RunCommandFixtures.Rig rig = NewRig();
            Accept(rig, Plate);
            Assert.That(rig.Commands.AcceptContract(Plate).ReasonCode, Is.EqualTo(RunReasons.ContractActiveExists));
            Assert.That(rig.Commands.AcceptContract("contract.missing").ReasonCode, Is.EqualTo(RunReasons.CommandInvalid));
            Deliver(rig);
            Assert.That(rig.Commands.AcceptContract(Plate).ReasonCode, Is.EqualTo(RunReasons.ContractCooldown));
            Assert.That(rig.Commands.AcceptContract(Bonus).ReasonCode, Is.EqualTo(RunReasons.ContractBonusUnavailable), "a bonus offer that never triggered");
            Assert.That(rig.Commands.AcceptContract(OverCapacity).ReasonCode, Is.EqualTo(RunReasons.ContractCapacity));
        }

        // ---- capacity (FR-007) --------------------------------------------------------------------------------

        [Test]
        public void ARequiredQuantityAboveTheCurrentCapacityIsRejected_AndExactlyTheCapacityIsAccepted()
        {
            RunCommandFixtures.Rig rig = NewRig();
            Assert.That(rig.State.Inventory.Capacity, Is.EqualTo(1_000));
            string before = Digest(rig);

            Assert.That(rig.Commands.AcceptContract(OverCapacity).ReasonCode, Is.EqualTo(RunReasons.ContractCapacity));
            Assert.That(Digest(rig), Is.EqualTo(before), "a capacity rejection changes nothing");
            ContractOfferSnapshot over = Offer(rig, OverCapacity);
            Assert.That(over.CanAccept, Is.False);
            Assert.That(over.AcceptReason, Is.EqualTo(RunReasons.ContractCapacity));
            Assert.That(over.RequiredUnits, Is.EqualTo(1_001), "the soft-disabled offer still shows its quantity");

            Accept(rig, AtCapacity);
            Assert.That(rig.State.Contracts.Active.RequiredUnits, Is.EqualTo(1_000));
        }

        [Test]
        public void ACapacityRejectionIsLiftedByExpandingStorage_AtTheCurrentCapacityNotTheTop()
        {
            RunCommandFixtures.Rig rig = NewRig(cash: 10_000);
            Assert.That(rig.Commands.AcceptContract(OverCapacity).ReasonCode, Is.EqualTo(RunReasons.ContractCapacity));
            Assert.That(rig.Commands.UpgradeStorage().IsSuccess, Is.True);
            Assert.That(rig.State.Inventory.Capacity, Is.EqualTo(2_000));
            Assert.That(Offer(rig, OverCapacity).CanAccept, Is.True);
            Accept(rig, OverCapacity);
        }

        // ---- snapshot fields ----------------------------------------------------------------------------------

        [Test]
        public void TheSnapshotCarriesObjectiveClassCooldownBonusWindowAndDeliverable()
        {
            RunCommandFixtures.Rig rig = NewRig();
            ContractOfferSnapshot plate = Offer(rig, Plate);
            Assert.That(plate.Objective, Is.EqualTo("deliver"));
            Assert.That(plate.Class, Is.EqualTo("standing"));
            Assert.That(plate.CooldownRemainingMs, Is.Zero);
            Assert.That(plate.BonusWindowRemainingMs, Is.Null, "a standing offer has no window");
            Assert.That(plate.Deliverable, Is.False, "no stock yet");
            Assert.That(Offer(rig, Bonus).Class, Is.EqualTo("bonus"));
            Assert.That(Offer(rig, Bonus).BonusWindowRemainingMs, Is.Null, "not triggered");

            RunSimHarness.Stock(rig.State, "iron.plate", 4);
            Assert.That(Offer(rig, Plate).Deliverable, Is.False, "4 of 5");
            RunSimHarness.Stock(rig.State, "iron.plate", 1);
            Assert.That(Offer(rig, Plate).Deliverable, Is.True, "stock covers the quantity the next acceptance fixes");

            Accept(rig, Plate);
            Deliver(rig);
            AdvanceTo(rig, rig.Session.TimelineUtcMs + 10_000);
            plate = Offer(rig, Plate);
            Assert.That(plate.CooldownRemainingMs, Is.EqualTo(PlateCooldownMs - 10_000));
            Assert.That(plate.CanAccept, Is.False);
            Assert.That(plate.RequiredUnits, Is.EqualTo(6), "the next acceptance scales by the completion count");
        }

        // ---- Version and the publish tokens (GD-7) ------------------------------------------------------------

        [Test]
        public void TheVersionCountsEveryStateChange_AndARejectionLeavesItAlone()
        {
            RunCommandFixtures.Rig rig = NewRig();
            ContractBoard board = rig.State.Contracts;
            Assert.That(board.Version, Is.Zero);
            Accept(rig, Plate);
            long afterAccept = board.Version;
            Assert.That(afterAccept, Is.GreaterThan(0));
            Assert.That(rig.Commands.DeliverContract().ReasonCode, Is.EqualTo(RunReasons.StockInsufficient));
            Assert.That(board.Version, Is.EqualTo(afterAccept), "a rejected delivery");
            Deliver(rig);
            long afterDeliver = board.Version;
            Assert.That(afterDeliver, Is.GreaterThan(afterAccept));
            Assert.That(rig.Commands.AcceptContract(Plate).IsSuccess, Is.False);
            Assert.That(board.Version, Is.EqualTo(afterDeliver), "a rejected accept");
            Accept(rig, TimedBar);
            long afterTimed = board.Version;
            AdvanceTo(rig, rig.Session.TimelineUtcMs + TimedDurationMs + 1);
            Assert.That(board.Active, Is.Null);
            Assert.That(board.Version, Is.GreaterThan(afterTimed), "the expiry in an advance");
            long afterExpiry = board.Version;
            AdvanceTo(rig, rig.Session.TimelineUtcMs + 1_000);
            Assert.That(board.Version, Is.EqualTo(afterExpiry), "time passing without a change");
        }

        [Test]
        public void TheBoardVersionIsPartOfThePublishTokens_AStateChangeWithoutAContractSwapStillMovesTheStructureRevision()
        {
            RunCommandFixtures.Rig rig = NewRig();
            long structure = rig.Session.StructureRevision;

            // Neither Active nor anything else of the older tokens changes here: only the bonus state, hence only the Version.
            rig.Session.BeginCommand();
            Assert.That(rig.State.Contracts.TryTriggerBonus(Bonus, rig.Session.TimelineUtcMs), Is.EqualTo(RunStateResult.Ok));
            Assert.That(rig.Session.EndCommand(0, false).IsSuccess, Is.True);

            Assert.That(rig.Session.StructureRevision, Is.EqualTo(structure + 1));

            // Without a change the revision stays.
            rig.Session.BeginCommand();
            Assert.That(rig.Session.EndCommand(0, false).IsSuccess, Is.True);
            Assert.That(rig.Session.StructureRevision, Is.EqualTo(structure + 1));
        }

        [Test]
        public void ADeliveryAnAbandonmentAndAnExpiryEachMoveTheStructureRevisionOnce_AsBefore()
        {
            RunCommandFixtures.Rig rig = NewRig();
            Accept(rig, Plate);
            long s1 = rig.Session.StructureRevision;
            Deliver(rig);
            Assert.That(rig.Session.StructureRevision, Is.EqualTo(s1 + 1));
            AdvanceTo(rig, rig.Session.TimelineUtcMs + PlateCooldownMs);
            Accept(rig, Plate);
            long s2 = rig.Session.StructureRevision;
            Assert.That(rig.Commands.AbandonContract().IsSuccess, Is.True);
            Assert.That(rig.Session.StructureRevision, Is.EqualTo(s2 + 1));
        }

        // ---- reason codes -------------------------------------------------------------------------------------

        [Test]
        public void TheThreeNewCodesStayOutsideTheTwentyFourPinnedCodes()
        {
            Assert.That(RunReasons.All.Count, Is.EqualTo(24));
            Assert.That(RunReasons.All, Does.Not.Contain(RunReasons.ContractCooldown));
            Assert.That(RunReasons.All, Does.Not.Contain(RunReasons.ContractBonusUnavailable));
            Assert.That(RunReasons.All, Does.Not.Contain(RunReasons.ContractCapacity));
            Assert.That(RunReasons.ContractCooldown, Is.EqualTo("contract.cooldown"));
            Assert.That(RunReasons.ContractBonusUnavailable, Is.EqualTo("contract.bonus.unavailable"));
            Assert.That(RunReasons.ContractCapacity, Is.EqualTo("contract.capacity"));
        }

        // ---- 10^18 -------------------------------------------------------------------------------------------

        [Test]
        public void ACooldownStampThatPassesTenToTheEighteenthRejectsTheEndingWithValueLimit_AndChangesNothing()
        {
            var always = new UnlockCondition("always");
            ContractOfferDef huge = new ContractOfferDef("contract.huge", "plate", "iron.plate", new Curve(1, 1_000_000, "ceil"), null,
                new[] { new ContractRewardDef("rp", new Curve(1, 1_000_000, "floor")) }, always, rewardRatioPpm: 1_000_000, cooldownMs: ExactBound.Value);
            RunCommandFixtures.Rig rig = RunCommandFixtures.NewRig(new RunCommandFixtures.Options { Offers = new[] { huge } }, 1_000);
            rig.Advance(0);
            Assert.That(rig.Commands.AcceptContract("contract.huge").IsSuccess, Is.True);
            RunSimHarness.Stock(rig.State, "iron.plate", 1);
            string before = Digest(rig);

            Assert.That(rig.Commands.DeliverContract().ReasonCode, Is.EqualTo(RunReasons.ValueLimit));
            Assert.That(rig.Commands.AbandonContract().ReasonCode, Is.EqualTo(RunReasons.ValueLimit));

            Assert.That(Digest(rig), Is.EqualTo(before));
            Assert.That(rig.State.Contracts.Active, Is.Not.Null, "still active, nothing half applied");
        }

        // ---- FR-007a: the prestige reset (QA-8) ---------------------------------------------------------------

        /// <summary>
        /// Prestige builds the next run with a new <see cref="RunState"/> and so a new <see cref="ContractBoard"/>: every run-scoped contract state
        /// starts empty. Auto-deliver and production progress join the board with T322 and are reset by the same construction; their rows are added
        /// to this test there.
        /// </summary>
        [Test]
        public void APrestigeResetsTheCooldownsTheBonusStateTheActiveContractAndTheVersion()
        {
            RunCommandFixtures.Rig rig = NewRig(cash: 10_000_000);
            ContractBoard oldBoard = rig.State.Contracts;
            Accept(rig, Plate);
            Deliver(rig); // plate cooldown running
            Accept(rig, TimedBar);
            Assert.That(oldBoard.TryTriggerBonus(Bonus, rig.Session.TimelineUtcMs), Is.EqualTo(RunStateResult.Ok)); // bonus triggered, timed bar active
            Assert.That(oldBoard.CooldownReadyAtUtcMs(Plate), Is.Not.Null);
            Assert.That(oldBoard.Version, Is.GreaterThan(0));

            Assert.That(rig.Commands.ConfirmPrestige().IsSuccess, Is.True);
            Assert.That(rig.Commands.PromotePrestige().IsSuccess, Is.True);
            rig.Session.Advance(0); // the first settle of the new run unlocks the offers

            RunState next = rig.Session.State;
            Assert.That(next, Is.Not.SameAs(rig.State));
            ContractBoard board = next.Contracts;
            Assert.That(board.Active, Is.Null);
            Assert.That(board.LastExpired, Is.Null);
            Assert.That(board.Version, Is.Zero, "a new board counts from its own start");
            Assert.That(board.RunCompletionTotal, Is.Zero);
            foreach (string id in rig.Catalog.ContractOffers.Keys)
            {
                Assert.That(board.CooldownReadyAtUtcMs(id), Is.Null, id);
                Assert.That(board.BonusState(id).Triggered, Is.False, id);
                Assert.That(board.BonusState(id).Consumed, Is.False, id);
            }
            Assert.That(Offer(rig, Plate).CooldownRemainingMs, Is.Zero, "the cooldown of the ended run does not carry over");
            Assert.That(Offer(rig, Bonus).BonusWindowRemainingMs, Is.Null);

            Assert.That(rig.Commands.AcceptContract(Plate).IsSuccess, Is.True, "no cooldown in the new run");
            Assert.That(rig.Commands.AbandonContract().IsSuccess, Is.True);
            Assert.That(rig.Commands.AcceptContract(Bonus).ReasonCode, Is.EqualTo(RunReasons.ContractBonusUnavailable), "the bonus is not triggered in the new run");
            Assert.That(oldBoard.CooldownReadyAtUtcMs(Plate), Is.Not.Null, "the ended run is not touched");
        }

        [Test]
        public void APrestigeMovesTheStructureRevisionEvenThoughTheNewBoardRestartsAtVersionZero()
        {
            RunCommandFixtures.Rig rig = NewRig(cash: 10_000_000);
            Accept(rig, Plate);
            Deliver(rig);
            long structure = rig.Session.StructureRevision;
            long quantity = rig.Session.QuantityRevision;
            Assert.That(rig.State.Contracts.Version, Is.GreaterThan(0));

            Assert.That(rig.Commands.ConfirmPrestige().IsSuccess, Is.True);
            Assert.That(rig.Commands.PromotePrestige().IsSuccess, Is.True);

            Assert.That(rig.Session.State.Contracts.Version, Is.Zero);
            Assert.That(rig.Session.StructureRevision, Is.GreaterThan(structure), "presenters refresh although the Version went back to 0");
            Assert.That(rig.Session.QuantityRevision, Is.GreaterThan(quantity));
            // The next publish compares against fresh baselines of the new board, not against the old Version.
            long after = rig.Session.StructureRevision;
            rig.Advance(0);
            Assert.That(rig.Session.StructureRevision, Is.EqualTo(after + 1).Or.EqualTo(after), "no stale-token storm");
            Accept(rig, Plate);
        }

        [Test]
        public void AConsumedBonusOfferIsAvailableToTriggerAgainInTheNextRun()
        {
            RunCommandFixtures.Rig rig = NewRig(cash: 10_000_000);
            Assert.That(rig.State.Contracts.TryTriggerBonus(Bonus, rig.Session.TimelineUtcMs), Is.EqualTo(RunStateResult.Ok));
            Accept(rig, Bonus);
            Assert.That(rig.Commands.AbandonContract().IsSuccess, Is.True);
            Assert.That(rig.State.Contracts.BonusState(Bonus).Consumed, Is.True);

            Assert.That(rig.Commands.ConfirmPrestige().IsSuccess, Is.True);
            Assert.That(rig.Commands.PromotePrestige().IsSuccess, Is.True);
            rig.Session.Advance(0);

            ContractBoard board = rig.Session.State.Contracts;
            Assert.That(board.BonusState(Bonus).Consumed, Is.False);
            Assert.That(board.TryTriggerBonus(Bonus, rig.Session.TimelineUtcMs), Is.EqualTo(RunStateResult.Ok), "once per run, not once per game");
            Accept(rig, Bonus);
        }
    }
}
