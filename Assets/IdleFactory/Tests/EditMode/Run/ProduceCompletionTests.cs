using System;
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
    /// T327 (issue #392, F5-R05, FR-006, FR-009, FR-012, AR-4, PF-6): production counting in the completion pass and the completion of a production
    /// objective inside the simulator. The count is kept by the machine completions (every completed output unit of the target resource anywhere in
    /// the factory), written to the board by one method that caps it, and the objective completes in the scratch with the reward of the shared
    /// <see cref="ContractRewards"/> arithmetic, no stock consumed, the cooldown stamped with the completion tick. Fixture: see <see cref="ContractFixtures"/>.
    /// </summary>
    public sealed class ProduceCompletionTests
    {
        // ---- counting ---------------------------------------------------------------------------------------------------

        [Test]
        public void EveryCompletedOutputUnitIsCounted_AndTheUnitsStayInStock()
        {
            RunCommandFixtures.Rig rig = MiningRig(Produce);
            rig.Advance(5 * Second);

            ContractBoard board = Board(rig);
            Assert.That(board.Active, Is.Not.Null, "two completions: 4 of 5 units");
            Assert.That(board.Active.Produced, Is.EqualTo(4));
            Assert.That(Stock(rig, Ore), Is.EqualTo(4), "production never removes stock");
            ActiveContractSnapshot snapshot = RunSnapshots.Contracts(rig.Session).Active;
            Assert.That(snapshot.Produced, Is.EqualTo(4));
            Assert.That(snapshot.Objective, Is.EqualTo("produce"));
        }

        [Test]
        public void UnitsFromEveryMachineCountAndSoldUnitsStayCounted()
        {
            RunCommandFixtures.Rig rig = MiningRig(Produce, miners: 2);
            Assert.That(rig.Commands.SetAutoSell(Ore, true).IsSuccess, Is.True);
            rig.Advance(2 * Second - 1);
            Assert.That(Board(rig).Active.Produced, Is.Zero);

            rig.Advance(1);
            Assert.That(Board(rig).Active.Produced, Is.EqualTo(4), "two Miners complete at 2 s: 2 + 2 units, sold at once");
            Assert.That(Stock(rig, Ore), Is.Zero, "the auto-sale took them; the count does not depend on the stock");

            rig.Advance(2 * Second);
            Assert.That(Board(rig).Active, Is.Null, "the second completions at 4 s reach 5 units");
            Assert.That(Board(rig).LastOutcome.Kind, Is.EqualTo(ContractOutcomeKind.ProduceCompleted));
        }

        [Test]
        public void OnlyTheTargetResourceCounts()
        {
            // A produce objective for Iron Bar: the Miner's Ore does not count, the Smelter's Bar does.
            var bar = new ContractOfferDef("contract.produce-bar", "plate", Bar, new Curve(1, 1_000_000, "ceil"), null,
                new[] { new ContractRewardDef("rp", new Curve(1, 1_000_000, "floor")) }, new UnlockCondition("always"), objective: "produce",
                rewardRatioPpm: 1_200_000);
            RunCommandFixtures.Rig barRig = RunCommandFixtures.NewRig(Options(new[] { bar }), 1_000);
            barRig.Advance(0);
            RunSimHarness.Add(barRig.State, RunCommandFixtures.Miner, 0, 100);
            RunSimHarness.Add(barRig.State, RunCommandFixtures.Smelter, 1, 101);
            Accept(barRig, "contract.produce-bar");

            barRig.Advance(2 * Second);
            Assert.That(Board(barRig).Active.Produced, Is.Zero, "Ore was produced, no Bar yet");
            Assert.That(Stock(barRig, Ore), Is.GreaterThan(0));
            barRig.Advance(2 * Second);
            Assert.That(Board(barRig).Active, Is.Null, "the Smelter made the one Bar");
        }

        [Test]
        public void ADeliveryObjectiveIsNeverCountedAndTheBoardRejectsTheCall()
        {
            RunCommandFixtures.Rig rig = MiningRig(Deliver);
            rig.Advance(3 * Second);
            Assert.That(Board(rig).Active.Produced, Is.Zero);
            Assert.That(Board(rig).TryCountProduced(1, -1, 0, 0, 0), Is.EqualTo(RunStateResult.InvalidArgument));
        }

        // ---- the single board method --------------------------------------------------------------------------------------

        [Test]
        public void TheBoardMethodCapsAtTheRequiredQuantityCountsTheVersionAndChecksBeforeItChangesAnything()
        {
            RunCommandFixtures.Rig rig = MiningRig(null);
            ContractBoard board = Board(rig);
            Assert.That(board.TryCountProduced(1, -1, 0, 0, 0), Is.EqualTo(RunStateResult.ContractNoneActive));
            Accept(rig, Produce);
            long version = board.Version;

            Assert.That(board.TryCountProduced(-1, -1, 0, 0, 0), Is.EqualTo(RunStateResult.InvalidArgument));
            Assert.That(board.Version, Is.EqualTo(version), "a rejection changes nothing");
            Assert.That(board.TryCountProduced(0, -1, 0, 0, 0), Is.EqualTo(RunStateResult.Ok));
            Assert.That(board.Version, Is.EqualTo(version), "0 units change nothing, the Version included");

            Assert.That(board.TryCountProduced(3, -1, 0, 0, 0), Is.EqualTo(RunStateResult.Ok));
            Assert.That(board.Active.Produced, Is.EqualTo(3));
            Assert.That(board.Version, Is.EqualTo(version + 1));

            Assert.That(board.TryCountProduced(long.MaxValue / 2, -1, 0, 0, 0), Is.EqualTo(RunStateResult.Ok));
            Assert.That(board.Active.Produced, Is.EqualTo(5), "capped at the required quantity, no overflow");
            Assert.That(board.Active, Is.Not.Null, "counting alone never completes");
            Assert.That(board.Version, Is.EqualTo(version + 2));
            Assert.That(board.TryCountProduced(7, -1, 0, 0, 0), Is.EqualTo(RunStateResult.Ok));
            Assert.That(board.Version, Is.EqualTo(version + 2), "a full count does not change again");
        }

        [Test]
        public void TheBoardMethodCompletesThroughTheSharedPathOrRejectsTheWholeCallUnchanged()
        {
            RunCommandFixtures.Rig rig = MiningRig(ProduceShort);
            ContractBoard board = Board(rig);
            long deadline = board.Active.DeadlineUtcMs.Value;
            long version = board.Version;

            // Past the inclusive deadline: rejected as a whole, the count stays where it was.
            Assert.That(board.TryCountProduced(5, deadline + 1, 6, 2, 1), Is.EqualTo(RunStateResult.ContractExpired));
            Assert.That(board.Active.Produced, Is.Zero);
            Assert.That(board.Version, Is.EqualTo(version));
            Assert.That(board.OutcomeSerial, Is.Zero);

            // At the deadline: count, check and completion in one transaction.
            Assert.That(board.TryCountProduced(5, deadline, 6, 2, 1), Is.EqualTo(RunStateResult.Ok));
            Assert.That(board.Active, Is.Null);
            Assert.That(board.CompletionCount(ProduceShort), Is.EqualTo(1));
            Assert.That(board.RunCompletionTotal, Is.EqualTo(1));
            Assert.That(board.CooldownReadyAtUtcMs(ProduceShort), Is.EqualTo(deadline + ShortCooldownMs));
            ContractOutcome outcome = board.LastOutcome;
            Assert.That(outcome.Kind, Is.EqualTo(ContractOutcomeKind.ProduceCompleted));
            Assert.That(outcome.EndedUtcMs, Is.EqualTo(deadline));
            Assert.That(outcome.CashReward, Is.EqualTo(6));
            Assert.That(outcome.RpReward, Is.EqualTo(2));
            Assert.That(outcome.ItemCount, Is.EqualTo(1));
            Assert.That(outcome.CompletionIndex, Is.Zero);
            Assert.That(outcome.RequiredUnits, Is.EqualTo(5));
            Assert.That(board.OutcomeSerial, Is.EqualTo(1));
        }

        [Test]
        public void ACompletionThatWouldPassTheCounterBoundIsRejectedWithoutHalfApplyingTheCount()
        {
            RunCommandFixtures.Rig rig = MiningRig(Produce);
            ContractBoard board = Board(rig);
            Assert.That(board.TrySetCompletionCount(Produce, ExactBound.Value), Is.EqualTo(RunStateResult.Ok));
            long version = board.Version;

            Assert.That(board.TryCountProduced(5, rig.Session.TimelineUtcMs, 6, 2, 0), Is.EqualTo(RunStateResult.ValueLimit));

            Assert.That(board.Active, Is.Not.Null);
            Assert.That(board.Active.Produced, Is.Zero, "the check ran before the count");
            Assert.That(board.Version, Is.EqualTo(version));
        }

        // ---- completion inside an advance ---------------------------------------------------------------------------------

        [Test]
        public void TheObjectiveCompletesAtTheTickOfTheUnitThatReachesTheQuantity()
        {
            RunCommandFixtures.Rig rig = MiningRig(Produce);
            long version;
            rig.Advance(6 * Second - 1);
            ContractBoard board = Board(rig);
            Assert.That(board.Active.Produced, Is.EqualTo(4), "one tick short of the third completion");
            version = board.Version;

            rig.Advance(1);

            Assert.That(board.Active, Is.Null);
            Assert.That(Stock(rig, Ore), Is.EqualTo(6), "the units stay in stock");
            Assert.That(rig.State.Cash, Is.EqualTo(1_000 + 6), "MulDivFloor(value of 5 x 1, 1.2)");
            Assert.That(rig.State.Rp, Is.EqualTo(2));
            Assert.That(Items(rig), Is.EqualTo(1), "the ladder grants Boost at run index 0, shared with delivery");
            Assert.That(board.CompletionCount(Produce), Is.EqualTo(1));
            Assert.That(board.RunCompletionTotal, Is.EqualTo(1));
            Assert.That(board.CooldownReadyAtUtcMs(Produce), Is.EqualTo(StartUtcMs + 6_000 + CooldownMs), "stamped with the real instant of the completion tick");
            Assert.That(board.Version, Is.GreaterThan(version));
            ContractOutcome outcome = board.LastOutcome;
            Assert.That(outcome.Kind, Is.EqualTo(ContractOutcomeKind.ProduceCompleted));
            Assert.That(outcome.EndedUtcMs, Is.EqualTo(StartUtcMs + 6_000));
            Assert.That(rig.Commands.AcceptContract(Produce).ReasonCode, Is.EqualTo(RunReasons.ContractCooldown));
            Assert.That(board.LastExpired, Is.Null);
        }

        [Test]
        public void TheCooldownOfACompletionIsAStampEvaluatedLazily_NotABoundaryEvent()
        {
            RunCommandFixtures.Rig rig = MiningRig(Produce);
            rig.Advance(6 * Second);
            ContractBoard board = Board(rig);
            long ready = board.CooldownReadyAtUtcMs(Produce).Value;
            Assert.That(RunSimHarness.Events(rig.State, StartUtcMs, 0, 60 * Second), Is.Empty, "the cooldown schedules nothing");
            rig.Advance((CooldownMs - 1) * RunSession.TicksPerMs);
            Assert.That(rig.Session.TimelineUtcMs, Is.EqualTo(ready - 1));
            Assert.That(rig.Commands.AcceptContract(Produce).ReasonCode, Is.EqualTo(RunReasons.ContractCooldown));
            long version = board.Version;

            rig.Advance(1 * RunSession.TicksPerMs);

            Assert.That(rig.Session.TimelineUtcMs, Is.EqualTo(ready));
            Assert.That(board.Version, Is.EqualTo(version), "crossing the end changes no state");
            Assert.That(rig.Commands.AcceptContract(Produce).IsSuccess, Is.True, "ready exactly at readyAt");
        }

        [Test]
        public void TheRewardIsTheSharedReward_IdenticalToAManualDeliveryOfTheSameSize()
        {
            // The Cash of FR-009: one expression for both kinds. A delivery offer with the production offer's ratio pays what the objective pays.
            RunCommandFixtures.Rig produced = MiningRig(Produce);
            produced.Advance(6 * Second);
            var deliver = new ContractOfferDef("contract.deliver-12", "plate", Ore, new Curve(5, 1_000_000, "ceil"), null,
                new[] { new ContractRewardDef("rp", new Curve(2, 1_000_000, "floor")) }, new UnlockCondition("always"), rewardRatioPpm: 1_200_000);
            RunCommandFixtures.Rig manual = RunCommandFixtures.NewRig(Options(new[] { deliver }), 1_000);
            manual.Advance(0);
            Accept(manual, "contract.deliver-12");
            RunSimHarness.Stock(manual.State, Ore, 5);
            RunCommandResult<ContractDeliveryResult> r = manual.Commands.DeliverContract();
            Assert.That(r.IsSuccess, Is.True, r.ReasonCode);

            Assert.That(produced.State.Cash - 1_000, Is.EqualTo(r.Value.CashReward));
            Assert.That(produced.State.Rp, Is.EqualTo(r.Value.RpReward));
            Assert.That(Items(produced), Is.EqualTo(Items(manual)));
            Assert.That(Board(produced).RunCompletionTotal, Is.EqualTo(Board(manual).RunCompletionTotal));
        }

        [Test]
        public void TheSalePriceModifierIsReadAtTheCompletionInstant()
        {
            // Research raising the Ore price is read when the objective completes, not at acceptance (FR-009): buying it in between pays more.
            var research = new ResearchDef("research.ore-price", "Ore", 1, new Curve(5, 1_000_000, "ceil"), new ResearchPrerequisite[0],
                new EffectDef("salePrice", "resource", Ore, 2_000_000));
            RunCommandFixtures.Options options = Options();
            options.Research = new[] { research };
            RunCommandFixtures.Rig rig = Rig(options: options, cash: 1_000);
            RunSimHarness.Add(rig.State, RunCommandFixtures.Miner, 0, 100);
            Accept(rig, Produce);
            Assert.That(rig.State.TryAddRp(5), Is.EqualTo(RunStateResult.Ok));
            Assert.That(rig.Commands.PurchaseResearch("research.ore-price").IsSuccess, Is.True);
            long cash = rig.State.Cash;

            rig.Advance(6 * Second);

            Assert.That(Board(rig).Active, Is.Null);
            Assert.That(rig.State.Cash - cash, Is.EqualTo(12), "5 x 1 x 2.0 sale price x 1.2 ratio");
        }

        [Test]
        public void ADeliveryContractAndAProductionObjectiveNeverCompleteEachOther()
        {
            RunCommandFixtures.Rig rig = MiningRig(Deliver);
            Assert.That(rig.Commands.SetAutoDeliver(true).IsSuccess, Is.True);
            rig.Advance(7 * Second);
            Assert.That(Board(rig).LastOutcome.Kind, Is.EqualTo(ContractOutcomeKind.AutoDelivered));
            Assert.That(Board(rig).LastOutcome.OfferId, Is.EqualTo(Deliver));
            Assert.That(Board(rig).CompletionCount(Produce), Is.Zero);
        }

        // ---- the deadline race --------------------------------------------------------------------------------------------

        [TestCase(ProduceShort, true)]
        [TestCase(ProduceTight, false)]
        public void TheDeadlineIsInclusive_AUnitAtTheDeadlineCounts_AUnitAtTheExpiryTickIsTooLate(string offer, bool completed)
        {
            RunCommandFixtures.Rig single = MiningRig(offer);
            AdvanceCut(single, 7 * Second);
            ContractBoard board = Board(single);
            Assert.That(board.Active, Is.Null);
            if (completed)
            {
                // Deadline accepted + 6,000 ms inclusive; the third completion is at 6,000 ms, before the expiry boundary at 6,001 ms.
                Assert.That(board.LastOutcome.Kind, Is.EqualTo(ContractOutcomeKind.ProduceCompleted));
                Assert.That(board.LastExpired, Is.Null, "completed, never expired as well");
                Assert.That(single.State.Cash, Is.EqualTo(1_000 + 6));
                Assert.That(board.CooldownReadyAtUtcMs(offer), Is.EqualTo(StartUtcMs + 6_000 + ShortCooldownMs));
            }
            else
            {
                // Deadline accepted + 5,999 ms; its expiry boundary, 6,000 ms, is the tick of the third completion, and boundary events come first.
                Assert.That(board.LastOutcome.Kind, Is.EqualTo(ContractOutcomeKind.Expired));
                Assert.That(board.LastExpired.HasValue, Is.True);
                Assert.That(single.State.Cash, Is.EqualTo(1_000), "no reward");
                Assert.That(board.CompletionCount(offer), Is.Zero);
                Assert.That(Stock(single, Ore), Is.EqualTo(6), "the production itself still happened");
                Assert.That(board.CooldownReadyAtUtcMs(offer), Is.EqualTo(StartUtcMs + 5_999 + 1 + ShortCooldownMs));
            }

            RunCommandFixtures.Rig cut = MiningRig(offer);
            AdvanceCut(cut, 7 * Second, BoundaryCuts);
            Assert.That(Digest(cut), Is.EqualTo(Digest(single)));
        }

        // ---- determinism ----------------------------------------------------------------------------------------------------

        [Test]
        public void TheCompletionIsTheSameForOneAdvanceAndForAnyPartition()
        {
            RunCommandFixtures.Rig single = MiningRig(Produce, miners: 2);
            AdvanceCut(single, 10 * Second);
            string expected = Digest(single);
            Assert.That(Board(single).LastOutcome.Kind, Is.EqualTo(ContractOutcomeKind.ProduceCompleted));

            RunCommandFixtures.Rig cut = MiningRig(Produce, miners: 2);
            AdvanceCut(cut, 10 * Second, BoundaryCuts);
            Assert.That(Digest(cut), Is.EqualTo(expected));

            RunCommandFixtures.Rig frames = MiningRig(Produce, miners: 2);
            for (int i = 0; i < 1000; i++) frames.Advance(10 * Second / 1000 + (i % 7 == 0 ? 1 : 0) - (i % 7 == 1 ? 1 : 0));
            Assert.That(frames.Session.ActiveTicksSinceAnchor, Is.EqualTo(single.Session.ActiveTicksSinceAnchor));
            Assert.That(Digest(frames), Is.EqualTo(expected));
        }

        [Test]
        public void TheCompletionStampIsTheSameLiveAndOffline()
        {
            // Offline the effective time runs at 4/5, so the third completion (effective 6 s) happens at real 7.5 s.
            RunCommandFixtures.Rig rig = MiningRig(Produce);
            long departure = rig.Session.TimelineUtcMs;
            var segmenter = new OfflineSegmenter(rig.Catalog);

            RunOfflineSummary summary = segmenter.Reconcile(rig.State, rig.Session.Permanent, departure, departure + 20_000);

            Assert.That(summary.Outcome, Is.EqualTo(RunOfflineOutcome.Applied));
            Assert.That(summary.ProduceCompleted, Is.True);
            Assert.That(summary.Contract.Kind, Is.EqualTo(RunOfflineContractKind.ProduceCompleted));
            Assert.That(summary.Contract.EndedUtcMs, Is.EqualTo(departure + 7_500));
            Assert.That(Board(rig).CooldownReadyAtUtcMs(Produce), Is.EqualTo(departure + 7_500 + CooldownMs));
        }

        // ---- the blocked completion ---------------------------------------------------------------------------------------

        [Test]
        public void ACompletionPastTheCashBoundIsBlockedAndRetriedUntilItFits()
        {
            var sink = new ContractEventRecorder();
            RunCommandFixtures.Rig rig = MiningRig(Produce, sink);
            Assert.That(rig.State.TryAddCash(ExactBound.Value - rig.State.Cash - 3), Is.EqualTo(RunStateResult.Ok)); // 10^18 - 3: the reward 6 does not fit

            rig.Advance(7 * Second);

            ContractBoard board = Board(rig);
            Assert.That(board.Active, Is.Not.Null, "nothing was granted and the contract stays active");
            Assert.That(board.Active.Produced, Is.EqualTo(5), "the count is full");
            Assert.That(board.Active.AutoDeliverBlocked, Is.True);
            Assert.That(rig.State.Cash, Is.EqualTo(ExactBound.Value - 3));
            Assert.That(Items(rig), Is.Zero);
            Assert.That(board.CompletionCount(Produce), Is.Zero);
            ActiveContractSnapshot snapshot = RunSnapshots.Contracts(rig.Session).Active;
            Assert.That(snapshot.CompletionBlocked, Is.True);
            Assert.That(snapshot.AutoDeliverBlocked, Is.False, "the toggle does not exist for a production objective");
            Assert.That(sink.Count(RunContractEventKind.Blocked), Is.EqualTo(1));

            long version = board.Version;
            rig.Advance(3 * Second);
            Assert.That(board.Version, Is.EqualTo(version), "a repeat of the blocked state changes nothing");
            Assert.That(sink.Count(RunContractEventKind.Blocked), Is.EqualTo(1), "the event fires on the change only");

            Assert.That(rig.State.TrySpendCash(100), Is.EqualTo(RunStateResult.Ok));
            rig.Advance(1);
            Assert.That(board.Active, Is.Null, "the first instant after room appeared completes it");
            Assert.That(board.LastOutcome.Kind, Is.EqualTo(ContractOutcomeKind.ProduceCompleted));
            Assert.That(rig.State.Cash, Is.EqualTo(ExactBound.Value - 103 + 6));
            Assert.That(sink.Count(RunContractEventKind.ProduceComplete), Is.EqualTo(1));
            Assert.That(sink.Count(RunContractEventKind.Blocked), Is.EqualTo(1));
        }

        // ---- SettleFits is never optimistic about a production reward --------------------------------------------------------

        private static RunCommandFixtures.Rig WaitingMinerRig(long producedAlready)
        {
            RunCommandFixtures.Rig rig = MiningRig(null);
            RunSimHarness.Add(rig.State, RunCommandFixtures.Miner, 1, 101, progress: 20_000_000); // waiting at full progress: completes at the settle
            Accept(rig, Produce);
            Assert.That(Board(rig).TryCountProduced(producedAlready, -1, 0, 0, 0), Is.EqualTo(RunStateResult.Ok));
            RunSimHarness.Stock(rig.State, Ore, 5);
            // Cash 10^18 - 8: the reward 6 fits, and the sale of the 7 units in stock afterwards (+7) would pass the bound together with it.
            Assert.That(rig.State.TryAddCash(ExactBound.Value - rig.State.Cash - 8), Is.EqualTo(RunStateResult.Ok));
            return rig;
        }

        [Test]
        public void ACommandIsRejectedWhenTheSettleCouldCompleteTheObjectiveAndThenOverflowOnASale()
        {
            RunCommandFixtures.Rig rig = WaitingMinerRig(producedAlready: 4); // one unit missing: the waiting Miner completes it at the settle
            string before = Digest(rig);

            RunCommandResult<bool> r = rig.Commands.SetAutoSell(Ore, true);

            Assert.That(r.ReasonCode, Is.EqualTo(RunReasons.ValueLimit), "reward 6 + sale of 5 + 2 units would pass 10^18: rejected before any mutation");
            Assert.That(Digest(rig), Is.EqualTo(before));
            Assert.That(rig.State.IsAutoSell(Ore), Is.False);
        }

        [Test]
        public void ACommandIsAcceptedWhenTheWaitingMachinesCannotCompleteTheObjective()
        {
            RunCommandFixtures.Rig rig = WaitingMinerRig(producedAlready: 0); // 5 units missing, the waiting Miner makes 2: no reward at the settle

            RunCommandResult<bool> r = rig.Commands.SetAutoSell(Ore, true);

            Assert.That(r.IsSuccess, Is.True, r.ReasonCode);
            Assert.That(rig.State.Cash, Is.EqualTo(ExactBound.Value - 1), "the 7 units were sold, no reward");
            Assert.That(Board(rig).Active.Produced, Is.EqualTo(2));
        }

        // ---- cost ---------------------------------------------------------------------------------------------------------

        [Test]
        public void CountingAndCompletingAllocateNothingOnAWarmedAdvance()
        {
            RunCommandFixtures.Rig rig = MiningRig(Produce, miners: 3);
            var sim = new RunSimulator(rig.Catalog);
            // Keep the objective from completing so every sample runs the counting path: a huge requirement via a restored active contract.
            Assert.That(Board(rig).TryAbandon(rig.Session.TimelineUtcMs), Is.EqualTo(RunStateResult.Ok));
            var big = new ActiveContract(Produce, StartUtcMs, null, 900_000_000_000L);
            Assert.That(Board(rig).TrySetActive(big), Is.EqualTo(RunStateResult.Ok));
            TimelineMapping.TryLiveAt(StartUtcMs, 0, out TimelineMapping mapping);
            Assert.That(sim.Advance(rig.State, rig.Session.Permanent, 1, mapping, null), Is.EqualTo(RunAdvanceResult.Ok));

            var counts = new int[6];
            RunAdvanceResult result = RunAdvanceResult.Ok;
            for (int i = 0; i < counts.Length; i++)
                counts[i] = GcAllocationCounter.Count(() => result = sim.Advance(rig.State, rig.Session.Permanent, Second, mapping, null));

            Assert.That(result, Is.EqualTo(RunAdvanceResult.Ok));
            Assert.That(counts, Is.All.Zero, "GC.Alloc samples per warmed advance with a counting objective");
            Assert.That(big.Produced, Is.GreaterThan(0));
        }
    }
}
