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
    /// T321 (issue #388, FR-004, F5-R05): the bonus-offer state of the board. There is no trigger in the unlock pass yet (a later task), so the
    /// tests set the state through <see cref="ContractBoard.TryTriggerBonus"/>, which records only <c>Triggered</c> and the window end. The window is a
    /// UTC stamp evaluated lazily: open while <c>timeline &lt; windowEnd</c>, a bonus offer is used once per run, and a rejection changes nothing.
    /// </summary>
    public sealed class BonusOfferTests
    {
        private const string Bonus = ContractCooldownTests.Bonus;
        private const long Window = ContractCooldownTests.BonusWindowMs;

        private static RunCommandFixtures.Rig Triggered(out long windowEnd)
        {
            RunCommandFixtures.Rig rig = ContractCooldownTests.NewRig();
            long now = rig.Session.TimelineUtcMs;
            Assert.That(rig.State.Contracts.TryTriggerBonus(Bonus, now), Is.EqualTo(RunStateResult.Ok));
            windowEnd = now + Window;
            return rig;
        }

        private static void Accept(RunCommandFixtures.Rig rig)
        {
            RunCommandResult<ContractAcceptResult> r = rig.Commands.AcceptContract(Bonus);
            Assert.That(r.IsSuccess, Is.True, r.ReasonCode);
        }

        // ---- the state ---------------------------------------------------------------------------------------

        [Test]
        public void ANewBoardHasNoBonusStateAndNoTrigger()
        {
            ContractBoard board = ContractCooldownTests.NewRig().State.Contracts;
            foreach (string id in new[] { Bonus, ContractCooldownTests.Plate })
            {
                BonusOfferState state = board.BonusState(id);
                Assert.That(state.Triggered, Is.False, id);
                Assert.That(state.WindowEndUtcMs, Is.Zero, id);
                Assert.That(state.Consumed, Is.False, id);
                Assert.That(board.BonusWindowRemainingMs(id, 0), Is.Null, id);
            }
            Assert.That(board.IsBonusAvailable(Bonus, 0), Is.False, "not triggered");
            Assert.That(board.IsBonusAvailable(ContractCooldownTests.Plate, 0), Is.True, "a standing offer has no bonus gate");
            Assert.That(board.IsBonusAvailable("contract.missing", 0), Is.False);
        }

        [Test]
        public void ATriggerRecordsOnlyTriggeredAndTheWindowEnd_AndSchedulesNothing()
        {
            RunCommandFixtures.Rig rig = ContractCooldownTests.NewRig();
            ContractBoard board = rig.State.Contracts;
            long now = rig.Session.TimelineUtcMs;
            long version = board.Version;
            string before = RunCommandFixtures.Snapshot(rig.State);

            Assert.That(board.TryTriggerBonus(Bonus, now), Is.EqualTo(RunStateResult.Ok));

            BonusOfferState state = board.BonusState(Bonus);
            Assert.That(state.Triggered, Is.True);
            Assert.That(state.WindowEndUtcMs, Is.EqualTo(now + Window));
            Assert.That(state.Consumed, Is.False);
            Assert.That(board.Version, Is.EqualTo(version + 1));
            Assert.That(RunCommandFixtures.Snapshot(rig.State), Is.EqualTo(before), "no simulated state moves");
            Assert.That(board.Active, Is.Null);
            Assert.That(board.CooldownReadyAtUtcMs(Bonus), Is.Null);
            Assert.That(board.CompletionCount(Bonus), Is.Zero);
        }

        [Test]
        public void ARejectedTriggerChangesNothing()
        {
            RunCommandFixtures.Rig rig = ContractCooldownTests.NewRig();
            ContractBoard board = rig.State.Contracts;
            Assert.That(board.TryTriggerBonus(Bonus, 1_000), Is.EqualTo(RunStateResult.Ok));
            string before = ContractCooldownTests.Digest(rig);

            Assert.That(board.TryTriggerBonus(Bonus, 5_000), Is.EqualTo(RunStateResult.InvalidArgument), "once per run");
            Assert.That(board.TryTriggerBonus(ContractCooldownTests.Plate, 5_000), Is.EqualTo(RunStateResult.InvalidArgument), "a standing offer has none");
            Assert.That(board.TryTriggerBonus("contract.missing", 5_000), Is.EqualTo(RunStateResult.InvalidArgument));
            Assert.That(board.TryTriggerBonus(null, 5_000), Is.EqualTo(RunStateResult.InvalidArgument));
            Assert.That(board.TryTriggerBonus(Bonus, -1), Is.EqualTo(RunStateResult.InvalidArgument));

            Assert.That(ContractCooldownTests.Digest(rig), Is.EqualTo(before));
            Assert.That(board.BonusState(Bonus).WindowEndUtcMs, Is.EqualTo(1_000 + Window), "the first window stays");
        }

        [Test]
        public void AWindowEndPastTenToTheEighteenthIsRejected()
        {
            var always = new UnlockCondition("always");
            var offer = new ContractOfferDef("contract.late", "plate", "iron.plate", new Curve(1, 1_000_000, "ceil"), null,
                new[] { new ContractRewardDef("rp", new Curve(1, 1_000_000, "floor")) }, always, offerClass: "bonus", rewardRatioPpm: 1_800_000,
                bonus: new BonusDef(always, 10));
            RunCommandFixtures.Rig first = RunCommandFixtures.NewRig(new RunCommandFixtures.Options { Offers = new[] { offer } });
            Assert.That(first.State.Contracts.TryTriggerBonus("contract.late", ExactBound.Value - 10), Is.EqualTo(RunStateResult.Ok), "the end is exactly 10^18");
            Assert.That(first.State.Contracts.BonusState("contract.late").WindowEndUtcMs, Is.EqualTo(ExactBound.Value));
            RunCommandFixtures.Rig second = RunCommandFixtures.NewRig(new RunCommandFixtures.Options { Offers = new[] { offer } });
            Assert.That(second.State.Contracts.TryTriggerBonus("contract.late", ExactBound.Value - 9), Is.EqualTo(RunStateResult.ValueLimit), "one past");
            Assert.That(second.State.Contracts.BonusState("contract.late").Triggered, Is.False);
            Assert.That(second.State.Contracts.Version, Is.Zero);
        }

        // ---- availability ------------------------------------------------------------------------------------

        [Test]
        public void ABonusOfferThatNeverTriggeredCannotBeAccepted_AndTheRejectionChangesNothing()
        {
            RunCommandFixtures.Rig rig = ContractCooldownTests.NewRig();
            string before = ContractCooldownTests.Digest(rig);

            Assert.That(rig.Commands.AcceptContract(Bonus).ReasonCode, Is.EqualTo(RunReasons.ContractBonusUnavailable));

            Assert.That(ContractCooldownTests.Digest(rig), Is.EqualTo(before));
            ContractOfferSnapshot offer = ContractCooldownTests.Offer(rig, Bonus);
            Assert.That(offer.CanAccept, Is.False);
            Assert.That(offer.AcceptReason, Is.EqualTo(RunReasons.ContractBonusUnavailable));
            Assert.That(offer.BonusWindowRemainingMs, Is.Null);
        }

        [Test]
        public void TheWindowIsOpenUntilItsEndExclusive_AcceptedAtTheLastMillisecond_ClosedAtTheEnd()
        {
            RunCommandFixtures.Rig rig = Triggered(out long end);
            ContractCooldownTests.AdvanceTo(rig, end - 1);
            ContractOfferSnapshot open = ContractCooldownTests.Offer(rig, Bonus);
            Assert.That(open.BonusWindowRemainingMs, Is.EqualTo(1));
            Assert.That(open.CanAccept, Is.True);
            Accept(rig);

            RunCommandFixtures.Rig late = Triggered(out long lateEnd);
            ContractCooldownTests.AdvanceTo(late, lateEnd);
            ContractOfferSnapshot closed = ContractCooldownTests.Offer(late, Bonus);
            Assert.That(closed.BonusWindowRemainingMs, Is.Zero, "0 means the window ended");
            Assert.That(closed.CanAccept, Is.False);
            Assert.That(closed.AcceptReason, Is.EqualTo(RunReasons.ContractBonusUnavailable));
            Assert.That(late.Commands.AcceptContract(Bonus).ReasonCode, Is.EqualTo(RunReasons.ContractBonusUnavailable));
            ContractCooldownTests.AdvanceTo(late, lateEnd + 10 * Window);
            Assert.That(ContractCooldownTests.Offer(late, Bonus).BonusWindowRemainingMs, Is.Zero, "gone for the run, never reopens");
            Assert.That(late.Commands.AcceptContract(Bonus).ReasonCode, Is.EqualTo(RunReasons.ContractBonusUnavailable));
        }

        [Test]
        public void TheWindowCountsDownOnTheTimeline_LiveAndAfterAnOfflineReturn()
        {
            RunCommandFixtures.Rig live = Triggered(out long end);
            ContractCooldownTests.AdvanceTo(live, end - 30_000, 3_001);
            Assert.That(ContractCooldownTests.Offer(live, Bonus).BonusWindowRemainingMs, Is.EqualTo(30_000));

            RunCommandFixtures.Rig offline = Triggered(out long offlineEnd);
            long departure = offline.Session.TimelineUtcMs;
            new IdleFactory.Run.Simulation.OfflineSegmenter(offline.Catalog).Reconcile(offline.State, offline.Session.Permanent, departure, offlineEnd - 30_000);
            Assert.That(offline.Session.RebindAnchor(offlineEnd - 30_000).IsSuccess, Is.True);
            Assert.That(ContractCooldownTests.Offer(offline, Bonus).BonusWindowRemainingMs, Is.EqualTo(30_000));
            Assert.That(ContractCooldownTests.Digest(offline).Split('\n').Single(l => l.StartsWith(Bonus)),
                Is.EqualTo(ContractCooldownTests.Digest(live).Split('\n').Single(l => l.StartsWith(Bonus))), "the same stamp");
        }

        [Test]
        public void ABonusContractAcceptedInsideTheWindowStillDeliversAfterTheWindowEnds()
        {
            RunCommandFixtures.Rig rig = Triggered(out long end);
            Accept(rig);
            ContractCooldownTests.AdvanceTo(rig, end + 5_000);
            RunSimHarness.Stock(rig.State, "iron.gear", rig.State.Contracts.Active.RequiredUnits);
            Assert.That(rig.Commands.DeliverContract().IsSuccess, Is.True, "the window gates the acceptance only");
        }

        // ---- once per run ------------------------------------------------------------------------------------

        [TestCase(ContractCooldownTests.Ending.Delivery)]
        [TestCase(ContractCooldownTests.Ending.Abandon)]
        public void EveryEndingSpendsTheSingleUse_EvenInsideTheWindowAndAfterTheCooldown(ContractCooldownTests.Ending ending)
        {
            RunCommandFixtures.Rig rig = Triggered(out long end);
            Accept(rig);
            BonusOfferState during = rig.State.Contracts.BonusState(Bonus);
            Assert.That(during.Consumed, Is.False, "consumed when the contract ends, not when it starts");

            if (ending == ContractCooldownTests.Ending.Delivery)
            {
                RunSimHarness.Stock(rig.State, "iron.gear", rig.State.Contracts.Active.RequiredUnits);
                Assert.That(rig.Commands.DeliverContract().IsSuccess, Is.True);
            }
            else
            {
                // The bonus offer is untimed, so it never expires: the expiry is covered by ATimedBonusOfferIsSpentByItsExpiry.
                Assert.That(rig.Commands.AbandonContract().IsSuccess, Is.True);
            }

            long readyAt = rig.State.Contracts.CooldownReadyAtUtcMs(Bonus).Value;
            Assert.That(rig.State.Contracts.BonusState(Bonus).Consumed, Is.True);
            ContractCooldownTests.AdvanceTo(rig, readyAt);
            Assert.That(rig.Session.TimelineUtcMs, Is.LessThan(end), "still inside the window");
            Assert.That(ContractCooldownTests.Offer(rig, Bonus).CooldownRemainingMs, Is.Zero);
            Assert.That(rig.Commands.AcceptContract(Bonus).ReasonCode, Is.EqualTo(RunReasons.ContractBonusUnavailable), "the use is spent");
            Assert.That(rig.State.Contracts.TryTriggerBonus(Bonus, rig.Session.TimelineUtcMs), Is.EqualTo(RunStateResult.InvalidArgument), "not triggered twice");
        }

        [Test]
        public void ATimedBonusOfferIsSpentByItsExpiry()
        {
            var always = new UnlockCondition("always");
            var timed = new ContractOfferDef("contract.timed-bonus", "timedBar", "iron.bar", new Curve(2, 1_000_000, "ceil"), 30_000,
                new[] { new ContractRewardDef("rp", new Curve(1, 1_000_000, "floor")) }, always, offerClass: "bonus", rewardRatioPpm: 1_900_000,
                cooldownMs: 5_000, bonus: new BonusDef(always, 600_000));
            RunCommandFixtures.Rig rig = RunCommandFixtures.NewRig(new RunCommandFixtures.Options { Offers = new[] { timed } });
            rig.Advance(0);
            Assert.That(rig.State.Contracts.TryTriggerBonus("contract.timed-bonus", rig.Session.TimelineUtcMs), Is.EqualTo(RunStateResult.Ok));
            Assert.That(rig.Commands.AcceptContract("contract.timed-bonus").IsSuccess, Is.True);
            long deadline = rig.State.Contracts.Active.DeadlineUtcMs.Value;

            ContractCooldownTests.AdvanceTo(rig, deadline + 5_001);

            Assert.That(rig.State.Contracts.Active, Is.Null);
            Assert.That(rig.State.Contracts.BonusState("contract.timed-bonus").Consumed, Is.True);
            Assert.That(rig.State.Contracts.CooldownReadyAtUtcMs("contract.timed-bonus"), Is.EqualTo(deadline + 1 + 5_000));
            Assert.That(rig.Commands.AcceptContract("contract.timed-bonus").ReasonCode, Is.EqualTo(RunReasons.ContractBonusUnavailable));
        }

        [Test]
        public void ABonusOfferInCooldownReportsTheCooldownBeforeTheBonusGate()
        {
            RunCommandFixtures.Rig rig = Triggered(out _);
            Accept(rig);
            Assert.That(rig.Commands.AbandonContract().IsSuccess, Is.True);
            Assert.That(rig.Commands.AcceptContract(Bonus).ReasonCode, Is.EqualTo(RunReasons.ContractCooldown));
        }
    }
}
