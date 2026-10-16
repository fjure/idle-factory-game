using System.Linq;
using IdleFactory.Core.Numerics;
using IdleFactory.Run.Application;
using IdleFactory.Run.Content;
using IdleFactory.Run.Domain;
using IdleFactory.Run.Simulation;
using IdleFactory.Tests.EditMode.Fixtures;
using IdleFactory.Tests.EditMode.Fixtures.Run;
using NUnit.Framework;
using static IdleFactory.Tests.EditMode.Fixtures.Run.ContractFixtures;

namespace IdleFactory.Tests.EditMode.Run
{
    /// <summary>
    /// T327 (issue #392, F5-R05, FR-004, AR-3, AR2-3): the bonus trigger in the unlock pass. A bonus offer fires once per run when its content
    /// condition holds, at <c>RealUtc(tick)</c>; the board stores <c>Triggered</c> and the window end <c>trigger + window</c> and nothing else. The
    /// window and the cooldowns are stamps evaluated lazily: no boundary event exists for them, so crossing a window end changes no state, and a
    /// lapsed trigger never fires again in the run. Fixture: see <see cref="ContractFixtures"/> (four bonus offers that differ in their trigger).
    /// </summary>
    public sealed class BonusTriggerTests
    {
        // ---- the trigger -----------------------------------------------------------------------------------------------

        [Test]
        public void AnAlwaysTriggerFiresAtTheFirstSettleAndStampsTheWindowEnd()
        {
            RunCommandFixtures.Rig rig = Rig(settle: false);
            ContractBoard board = Board(rig);
            Assert.That(board.BonusState(BonusAlways).Triggered, Is.False, "the unlock pass has not run");
            long now = rig.Session.TimelineUtcMs;
            long version = board.Version;

            rig.Advance(0);

            BonusOfferState state = board.BonusState(BonusAlways);
            Assert.That(state.Triggered, Is.True);
            Assert.That(state.WindowEndUtcMs, Is.EqualTo(now + BonusWindowMs), "RealUtc(0) + window");
            Assert.That(state.Consumed, Is.False);
            Assert.That(board.Version, Is.GreaterThan(version));
            Assert.That(board.IsBonusAvailable(BonusAlways, now), Is.True);
            foreach (string untouched in new[] { BonusOre, BonusLevel, BonusNever })
                Assert.That(board.BonusState(untouched).Triggered, Is.False, untouched);
            Assert.That(board.BonusState(Produce).Triggered, Is.False, "a standing offer has no trigger");
            Assert.That(board.BonusState(Deliver).Triggered, Is.False);
        }

        [Test]
        public void ARecordedTriggerNeverMovesTheSimulatedState()
        {
            RunCommandFixtures.Rig withTrigger = Rig(settle: false);
            ContractOfferDef[] standingOnly = Offers().Where(o => !o.IsBonus).ToArray();
            RunCommandFixtures.Rig without = Rig(options: Options(standingOnly), settle: false);
            withTrigger.Advance(0);
            without.Advance(0);
            Assert.That(WithoutUnlockList(withTrigger.State), Is.EqualTo(WithoutUnlockList(without.State)));
        }

        /// <summary>The state as text without its unlock lists (the bonus offers are extra catalog entries that unlock too).</summary>
        private static string WithoutUnlockList(RunState state) =>
            string.Join("|", RunCommandFixtures.Snapshot(state).Split((char)10).Where(l => !l.StartsWith("unlocked=")));

        [Test]
        public void AResourceProducedTriggerFiresAtTheTickOfTheFirstUnitAndThePartitionDoesNotMatter()
        {
            RunCommandFixtures.Rig single = MiningRig(null);
            AdvanceCut(single, 10 * Second);
            BonusOfferState expected = Board(single).BonusState(BonusOre);
            Assert.That(expected.Triggered, Is.True);
            Assert.That(expected.WindowEndUtcMs, Is.EqualTo(StartUtcMs + 2_000 + BonusWindowMs), "the first Ore is made at 2 s");

            RunCommandFixtures.Rig cut = MiningRig(null);
            AdvanceCut(cut, 10 * Second, BoundaryCuts);
            Assert.That(Digest(cut), Is.EqualTo(Digest(single)));

            RunCommandFixtures.Rig frames = MiningRig(null);
            for (int i = 0; i < 1000; i++) frames.Advance(10 * Second / 1000 + (i % 7 == 0 ? 1 : 0) - (i % 7 == 1 ? 1 : 0));
            Assert.That(Digest(frames), Is.EqualTo(Digest(single)));
        }

        [Test]
        public void AFactoryLevelTriggerFiresAtTheCommandInstantThatReachedTheLevel()
        {
            RunCommandFixtures.Rig rig = Rig(cash: 100_000);
            rig.Advance(12_345 * RunSession.TicksPerMs);
            ContractBoard board = Board(rig);
            Assert.That(board.BonusState(BonusLevel).Triggered, Is.False);
            long now = rig.Session.TimelineUtcMs;

            RunCommandResult<ExpansionResult> r = rig.Commands.UpgradeFactory();

            Assert.That(r.IsSuccess, Is.True, r.ReasonCode);
            Assert.That(board.BonusState(BonusLevel).Triggered, Is.True);
            Assert.That(board.BonusState(BonusLevel).WindowEndUtcMs, Is.EqualTo(now + BonusWindowMs));
        }

        [Test]
        public void AnOfflineReturnStampsTheTriggerWithTheRealInstantOfTheFirstUnit()
        {
            // Offline the effective time runs at 4/5: the first Ore at effective 2 s is real 2.5 s after the departure.
            RunCommandFixtures.Rig rig = MiningRig(null);
            long departure = rig.Session.TimelineUtcMs;
            var segmenter = new OfflineSegmenter(rig.Catalog);

            segmenter.Reconcile(rig.State, rig.Session.Permanent, departure, departure + 30_000);

            Assert.That(Board(rig).BonusState(BonusOre).Triggered, Is.True);
            Assert.That(Board(rig).BonusState(BonusOre).WindowEndUtcMs, Is.EqualTo(departure + 2_500 + BonusWindowMs));
        }

        // ---- once per run ----------------------------------------------------------------------------------------------

        [Test]
        public void ALapsedTriggerNeverFiresAgain_AlthoughItsConditionKeepsHolding()
        {
            RunCommandFixtures.Rig rig = MiningRig(null);
            rig.Advance(3 * Second);
            ContractBoard board = Board(rig);
            BonusOfferState first = board.BonusState(BonusOre);
            Assert.That(first.Triggered, Is.True);

            rig.Advance((BonusWindowMs + 1_000) * RunSession.TicksPerMs); // the window lapses unaccepted; Ore is still being produced
            long version = board.Version;

            BonusOfferState later = board.BonusState(BonusOre);
            Assert.That(later.Triggered, Is.True);
            Assert.That(later.WindowEndUtcMs, Is.EqualTo(first.WindowEndUtcMs), "the first window stays");
            Assert.That(board.IsBonusAvailable(BonusOre, rig.Session.TimelineUtcMs), Is.False);
            Assert.That(rig.Commands.AcceptContract(BonusOre).ReasonCode, Is.EqualTo(RunReasons.ContractBonusUnavailable));

            rig.Advance(10 * Second);
            Assert.That(board.Version, Is.EqualTo(version), "nothing fires again");
            Assert.That(board.BonusState(BonusOre).WindowEndUtcMs, Is.EqualTo(first.WindowEndUtcMs));
        }

        [Test]
        public void AUsedBonusOfferIsNotTriggeredAgainInTheSameRun()
        {
            RunCommandFixtures.Rig rig = Rig();
            ContractBoard board = Board(rig);
            Accept(rig, BonusAlways);
            RunSimHarness.Stock(rig.State, Ore, 3);
            Assert.That(rig.Commands.DeliverContract().IsSuccess, Is.True);
            BonusOfferState used = board.BonusState(BonusAlways);
            Assert.That(used.Consumed, Is.True);
            long window = used.WindowEndUtcMs;

            rig.Advance(30 * Second); // the cooldown (10 s) passes; the always-trigger still holds

            Assert.That(board.BonusState(BonusAlways).Triggered, Is.True);
            Assert.That(board.BonusState(BonusAlways).Consumed, Is.True);
            Assert.That(board.BonusState(BonusAlways).WindowEndUtcMs, Is.EqualTo(window));
            Assert.That(rig.Commands.AcceptContract(BonusAlways).ReasonCode, Is.EqualTo(RunReasons.ContractBonusUnavailable));
        }

        [Test]
        public void ANewRunTriggersTheBonusAgainOnce()
        {
            RunCommandFixtures.Rig rig = Rig(cash: 10_000_000);
            ContractBoard oldBoard = Board(rig);
            long oldEnd = oldBoard.BonusState(BonusAlways).WindowEndUtcMs;
            Assert.That(oldBoard.BonusState(BonusAlways).Triggered, Is.True);
            rig.Advance(5_000 * RunSession.TicksPerMs);

            Assert.That(rig.Commands.ConfirmPrestige().IsSuccess, Is.True);
            Assert.That(rig.Commands.PromotePrestige().IsSuccess, Is.True);
            ContractBoard board = rig.Session.State.Contracts;
            rig.Session.Advance(0);

            Assert.That(board, Is.Not.SameAs(oldBoard));
            Assert.That(board.BonusState(BonusAlways).Triggered, Is.True);
            Assert.That(board.BonusState(BonusAlways).WindowEndUtcMs, Is.EqualTo(rig.Session.TimelineUtcMs + BonusWindowMs),
                "a fresh window of the new run, later than " + oldEnd);
            Assert.That(board.BonusState(BonusAlways).Consumed, Is.False);
        }

        // ---- lazy availability -----------------------------------------------------------------------------------------

        [Test]
        public void TheWindowEndIsNotAnEvent_CrossingItChangesNoStateAndTheAvailabilityFlipsLazily()
        {
            RunCommandFixtures.Rig rig = Rig();
            ContractBoard board = Board(rig);
            long end = board.BonusState(BonusAlways).WindowEndUtcMs;
            Assert.That(RunSimHarness.Events(rig.State, StartUtcMs, 0, (BonusWindowMs + 10_000) * RunSession.TicksPerMs), Is.Empty, "no boundary event for a window");
            rig.Advance((BonusWindowMs - 1) * RunSession.TicksPerMs);
            Assert.That(rig.Session.TimelineUtcMs, Is.EqualTo(end - 1));
            Assert.That(Offer(rig, BonusAlways).CanAccept, Is.True, "open while now < windowEnd");
            Assert.That(Offer(rig, BonusAlways).BonusWindowRemainingMs, Is.EqualTo(1));
            long structure = rig.Session.StructureRevision;
            long quantity = rig.Session.QuantityRevision;
            long version = board.Version;

            rig.Advance(1 * RunSession.TicksPerMs);

            Assert.That(rig.Session.TimelineUtcMs, Is.EqualTo(end));
            Assert.That(Offer(rig, BonusAlways).CanAccept, Is.False, "closed exactly at the end");
            Assert.That(Offer(rig, BonusAlways).AcceptReason, Is.EqualTo(RunReasons.ContractBonusUnavailable));
            Assert.That(Offer(rig, BonusAlways).BonusWindowRemainingMs, Is.Zero);
            Assert.That(board.Version, Is.EqualTo(version));
            Assert.That(rig.Session.StructureRevision, Is.EqualTo(structure));
            Assert.That(rig.Session.QuantityRevision, Is.EqualTo(quantity));
        }

        private static ContractOfferSnapshot Offer(RunCommandFixtures.Rig rig, string id) =>
            RunSnapshots.Contracts(rig.Session).Offers.Single(o => o.OfferId == id);

        [Test]
        public void AnAcceptedBonusContractStillCompletesAfterTheWindowEnds()
        {
            RunCommandFixtures.Rig rig = Rig();
            Accept(rig, BonusAlways);
            rig.Advance((BonusWindowMs + 5_000) * RunSession.TicksPerMs);
            RunSimHarness.Stock(rig.State, Ore, 3);
            Assert.That(rig.Commands.DeliverContract().IsSuccess, Is.True, "the window gates acceptance only");
            Assert.That(Board(rig).BonusState(BonusAlways).Consumed, Is.True);
        }

        // ---- cost ---------------------------------------------------------------------------------------------------------

        [Test]
        public void ThePendingTriggersCostNothingOnAWarmedAdvance()
        {
            RunCommandFixtures.Rig rig = MiningRig(null, miners: 3);
            var sim = new RunSimulator(rig.Catalog);
            TimelineMapping.TryLiveAt(StartUtcMs, 0, out TimelineMapping mapping);
            Assert.That(sim.Advance(rig.State, rig.Session.Permanent, 1, mapping, null), Is.EqualTo(RunAdvanceResult.Ok));
            Assert.That(Board(rig).BonusState(BonusNever).Triggered, Is.False, "one trigger stays pending for the whole test");

            var counts = new int[6];
            RunAdvanceResult result = RunAdvanceResult.Ok;
            for (int i = 0; i < counts.Length; i++)
                counts[i] = GcAllocationCounter.Count(() => result = sim.Advance(rig.State, rig.Session.Permanent, Second, mapping, null));

            Assert.That(result, Is.EqualTo(RunAdvanceResult.Ok));
            Assert.That(counts, Is.All.Zero, "GC.Alloc samples per warmed advance with a pending trigger");
        }
    }
}
