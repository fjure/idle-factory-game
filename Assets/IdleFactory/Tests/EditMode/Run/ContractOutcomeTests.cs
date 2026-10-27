using System;
using System.Linq;
using IdleFactory.Core.Numerics;
using IdleFactory.Run.Application;
using IdleFactory.Run.Content;
using IdleFactory.Run.Domain;
using IdleFactory.Run.Simulation;
using IdleFactory.Tests.EditMode.Fixtures.Run;
using NUnit.Framework;
using static IdleFactory.Tests.EditMode.Fixtures.Run.ContractFixtures;

namespace IdleFactory.Tests.EditMode.Run
{
    /// <summary>
    /// T327 (issue #392, F5-R05, AR-13, AR-14, AR2-4, the #391 QA follow-up): the outputs of the simulator-driven contract endings. The board keeps
    /// <see cref="ContractBoard.LastOutcome"/> (a value type, also on <see cref="ContractsSnapshot.LastOutcome"/>); the session raises the session-log
    /// events from a before/after comparison of the outcome serial and the blocked state, never from the simulator; the offline summary carries
    /// <c>AutoDelivered</c>, <c>ProduceCompleted</c>, <c>Expired</c> and the reward totals. Fixture: see <see cref="ContractFixtures"/>.
    /// </summary>
    public sealed class ContractOutcomeTests
    {
        private const long D = StartUtcMs;

        private static RunCommandFixtures.Rig AutoRig(ContractEventRecorder sink)
        {
            RunCommandFixtures.Rig rig = MiningRig(Deliver, sink);
            Assert.That(rig.Commands.SetAutoDeliver(true).IsSuccess, Is.True);
            return rig;
        }

        // ---- LastOutcome ----------------------------------------------------------------------------------------------

        [Test]
        public void AnAutoDeliveryIsRecordedAsTheLastOutcome()
        {
            RunCommandFixtures.Rig rig = AutoRig(null);
            Assert.That(Board(rig).LastOutcome.Kind, Is.EqualTo(ContractOutcomeKind.None));

            rig.Advance(7 * Second);

            ContractOutcome o = RunSnapshots.Contracts(rig.Session).LastOutcome;
            Assert.That(o.Kind, Is.EqualTo(ContractOutcomeKind.AutoDelivered));
            Assert.That(o.OfferId, Is.EqualTo(Deliver));
            Assert.That(o.RequiredUnits, Is.EqualTo(5));
            Assert.That(o.CompletionIndex, Is.Zero);
            Assert.That(o.EndedUtcMs, Is.EqualTo(D + 6_000));
            Assert.That(o.CashReward, Is.EqualTo(7));
            Assert.That(o.RpReward, Is.EqualTo(2));
            Assert.That(o.ItemCount, Is.EqualTo(1));
            Assert.That(o.Serial, Is.EqualTo(1));
            Assert.That(Board(rig).OutcomeSerial, Is.EqualTo(1));
        }

        [Test]
        public void AnExpiryIsRecordedAsTheLastOutcomeAndTheOldExpiredViewStaysInStep()
        {
            RunCommandFixtures.Rig rig = MiningRig(ProduceTight);
            rig.Advance(7 * Second);

            ContractOutcome o = Board(rig).LastOutcome;
            Assert.That(o.Kind, Is.EqualTo(ContractOutcomeKind.Expired));
            Assert.That(o.OfferId, Is.EqualTo(ProduceTight));
            Assert.That(o.EndedUtcMs, Is.EqualTo(D + 5_999 + 1), "deadline + 1 ms");
            Assert.That(o.CashReward, Is.Zero);
            Assert.That(o.RpReward, Is.Zero);
            Assert.That(o.ItemCount, Is.Zero);
            Assert.That(Board(rig).LastExpired.Value.OfferId, Is.EqualTo(ProduceTight));
            Assert.That(Board(rig).CompletionCount(ProduceTight), Is.Zero, "an expiry counts no completion");
        }

        [Test]
        public void AManualDeliveryAndTheNextAcceptanceDoNotRecordOrKeepAnOutcome()
        {
            RunCommandFixtures.Rig rig = MiningRig(Deliver);
            RunSimHarness.Stock(rig.State, Ore, 5);
            Assert.That(rig.Commands.DeliverContract().IsSuccess, Is.True);
            Assert.That(Board(rig).OutcomeSerial, Is.Zero, "a command delivery is the command's own result, not a simulator outcome");

            rig.Advance(31 * Second);
            Accept(rig, Produce);
            rig.Advance(7 * Second);
            Assert.That(Board(rig).LastOutcome.Kind, Is.EqualTo(ContractOutcomeKind.ProduceCompleted));

            rig.Advance(31 * Second);
            Accept(rig, Produce);
            Assert.That(Board(rig).LastOutcome.Kind, Is.EqualTo(ContractOutcomeKind.None), "cleared by the next acceptance, like LastExpired");
            Assert.That(RunSnapshots.Contracts(rig.Session).LastOutcome.Kind, Is.EqualTo(ContractOutcomeKind.None));
            Assert.That(Board(rig).OutcomeSerial, Is.EqualTo(1), "the serial counts on");
        }

        // ---- session events -----------------------------------------------------------------------------------------------

        [Test]
        public void AnAutoDeliveryRaisesTheAutoDeliverEventOnce()
        {
            var sink = new ContractEventRecorder();
            RunCommandFixtures.Rig rig = AutoRig(sink);
            rig.Advance(7 * Second);
            rig.Advance(20 * Second);

            Assert.That(sink.Contracts.Count, Is.EqualTo(1));
            RunContractEvent e = sink.Contracts[0];
            Assert.That(e.Kind, Is.EqualTo(RunContractEventKind.AutoDeliver));
            Assert.That(e.OfferId, Is.EqualTo(Deliver));
            Assert.That(e.Index, Is.Zero);
            Assert.That(e.Required, Is.EqualTo(5));
            Assert.That(e.Reward, Is.EqualTo(7));
            Assert.That(e.RatioPpm, Is.EqualTo(1_500_000));
        }

        [Test]
        public void SetAutoDeliverOnAStockedContractRaisesTheEventFromTheCommandSettle()
        {
            var sink = new ContractEventRecorder();
            RunCommandFixtures.Rig rig = Rig(sink);
            Accept(rig, Deliver);
            RunSimHarness.Stock(rig.State, Ore, 5);
            Assert.That(rig.Commands.SetAutoDeliver(true).IsSuccess, Is.True);

            Assert.That(sink.Contracts.Select(c => c.Kind), Is.EqualTo(new[] { RunContractEventKind.AutoDeliver }));
        }

        [Test]
        public void AProductionCompletionRaisesTheProduceCompleteEvent()
        {
            var sink = new ContractEventRecorder();
            RunCommandFixtures.Rig rig = MiningRig(Produce, sink);
            rig.Advance(5 * Second);
            Assert.That(sink.Contracts, Is.Empty, "4 of 5 units");
            rig.Advance(2 * Second);

            Assert.That(sink.Contracts.Count, Is.EqualTo(1));
            RunContractEvent e = sink.Contracts[0];
            Assert.That(e.Kind, Is.EqualTo(RunContractEventKind.ProduceComplete));
            Assert.That(e.OfferId, Is.EqualTo(Produce));
            Assert.That(e.Required, Is.EqualTo(5));
            Assert.That(e.Reward, Is.EqualTo(6));
            Assert.That(e.RatioPpm, Is.EqualTo(1_200_000));
        }

        [Test]
        public void AnExpiryRaisesTheExpireEventWithNoReward()
        {
            var sink = new ContractEventRecorder();
            RunCommandFixtures.Rig rig = MiningRig(ProduceTight, sink);
            rig.Advance(7 * Second);

            Assert.That(sink.Contracts.Count, Is.EqualTo(1));
            Assert.That(sink.Contracts[0].Kind, Is.EqualTo(RunContractEventKind.Expire));
            Assert.That(sink.Contracts[0].Reward, Is.Zero);
            Assert.That(sink.Contracts[0].OfferId, Is.EqualTo(ProduceTight));
        }

        [Test]
        public void ABlockedAutoDeliveryRaisesItsEventOnTheChangeOnly_ThenTheDeliveryEventFollows()
        {
            var sink = new ContractEventRecorder();
            RunCommandFixtures.Rig rig = Rig(sink, cash: 100);
            Accept(rig, Deliver);
            Assert.That(rig.Commands.SetAutoSell(Ore, true).IsSuccess, Is.True);
            Assert.That(rig.Commands.SetAutoDeliver(true).IsSuccess, Is.True);
            Assert.That(rig.State.TryAddCash(ExactBound.Value - 106), Is.EqualTo(RunStateResult.Ok)); // Cash = 10^18 - 6
            RunSimHarness.Stock(rig.State, Ore, 7); // the reward 7 cannot be granted after the 2 sold units (10^18 - 4)
            Assert.That(sink.Contracts, Is.Empty);

            rig.Advance(0);
            Assert.That(Board(rig).Active.AutoDeliverBlocked, Is.True);
            Assert.That(sink.Contracts.Select(c => c.Kind), Is.EqualTo(new[] { RunContractEventKind.Blocked }));
            RunContractEvent blocked = sink.Contracts[0];
            Assert.That(blocked.OfferId, Is.EqualTo(Deliver));
            Assert.That(blocked.Required, Is.EqualTo(5));
            Assert.That(blocked.Reward, Is.Zero);

            rig.Advance(Second);
            rig.Advance(0);
            rig.Advance(5 * Second);
            Assert.That(sink.Count(RunContractEventKind.Blocked), Is.EqualTo(1), "a repeat of the blocked state raises nothing (AR2-4)");

            Assert.That(rig.State.TrySpendCash(1_000), Is.EqualTo(RunStateResult.Ok));
            rig.Advance(1);
            Assert.That(Board(rig).Active, Is.Null);
            Assert.That(sink.Contracts.Select(c => c.Kind), Is.EqualTo(new[] { RunContractEventKind.Blocked, RunContractEventKind.AutoDeliver }));
        }

        [Test]
        public void ARejectedAdvanceAndAFailingSinkChangeNothing()
        {
            RunCommandFixtures.Rig plain = AutoRig(null);
            plain.Advance(7 * Second);

            var sink = new ThrowingSink();
            RunCommandFixtures.Rig rig = MiningRig(Deliver);
            rig.Session = new RunSession(rig.Catalog, rig.State, rig.Session.Permanent, StartUtcMs, sink);
            rig.Commands = new RunCommands(rig.Session, () => RunSimHarness.Id(rig.NextId++));
            Assert.That(rig.Commands.SetAutoDeliver(true).IsSuccess, Is.True);
            rig.Advance(7 * Second);

            Assert.That(sink.Calls, Is.EqualTo(1), "the event was raised");
            Assert.That(rig.Session.DroppedEvents, Is.EqualTo(1));
            Assert.That(Digest(rig), Is.EqualTo(Digest(plain)), "the sink cannot alter the run");
        }

        private sealed class ThrowingSink : IRunEventSink
        {
            public int Calls;
            public void OnHeader(in RunHeaderEvent e) { }
            public void OnSample(in RunSampleEvent e) { }
            public void OnMilestone(in RunMilestoneEvent e) { }
            public void OnSale(in RunSaleEvent e) { }
            public void OnMining(in RunMiningEvent e) { }
            public void OnContract(in RunContractEvent e) { Calls++; throw new InvalidOperationException("sink failure"); }
            public void OnPrestige(in RunPrestigeEvent e) { }
            public void OnOfflineReturn(in RunOfflineReturnEvent e) { }
            public void OnBottleneck(in RunBottleneckEvent e) { }
            public void OnPurchase(in RunPurchaseEvent e) { }
            public void OnClock(in RunClockEvent e) { }
            public void OnLifecycle(in RunLifecycleEvent e) { }
        }

        // ---- the offline summary --------------------------------------------------------------------------------------------

        [Test]
        public void AnOfflineAutoDeliveryIsReportedWithItsRewardTotalsAndTheDeliveryNotice()
        {
            RunCommandFixtures.Rig rig = AutoRig(null);
            long cashBefore = rig.State.Cash;

            RunOfflineSummary s = new OfflineSegmenter(rig.Catalog).Reconcile(rig.State, rig.Session.Permanent, D, D + 20_000);

            Assert.That(s.Outcome, Is.EqualTo(RunOfflineOutcome.Applied));
            Assert.That(s.AutoDelivered, Is.True);
            Assert.That(s.ProduceCompleted, Is.False);
            Assert.That(s.Expired, Is.False);
            Assert.That(s.Contract.Kind, Is.EqualTo(RunOfflineContractKind.AutoDelivered));
            Assert.That(s.Contract.OfferId, Is.EqualTo(Deliver));
            Assert.That(s.Contract.EndedUtcMs, Is.EqualTo(D + 7_500), "effective 6 s at 4/5 is real 7.5 s");
            Assert.That(s.ContractRewardCash, Is.EqualTo(7));
            Assert.That(s.ContractRewardRp, Is.EqualTo(2));
            Assert.That(s.ContractRewardItems.Select(i => i.Id + ":" + i.Count), Is.EqualTo(new[] { Boost + ":1" }));
            Assert.That(s.CashDelta, Is.EqualTo(rig.State.Cash - cashBefore));
            Assert.That(s.SalesIncome, Is.EqualTo(s.CashDelta - 7), "the reward is in CashDelta and not in SalesIncome");
        }

        [Test]
        public void AnOfflineProductionCompletionIsReported()
        {
            RunCommandFixtures.Rig rig = MiningRig(Produce);

            RunOfflineSummary s = new OfflineSegmenter(rig.Catalog).Reconcile(rig.State, rig.Session.Permanent, D, D + 20_000);

            Assert.That(s.ProduceCompleted, Is.True);
            Assert.That(s.AutoDelivered, Is.False);
            Assert.That(s.Contract.Kind, Is.EqualTo(RunOfflineContractKind.ProduceCompleted));
            Assert.That(s.Contract.EndedUtcMs, Is.EqualTo(D + 7_500));
            Assert.That(s.ContractRewardCash, Is.EqualTo(6));
            Assert.That(s.ContractRewardRp, Is.EqualTo(2));
            Assert.That(s.ContractRewardItems.Count, Is.EqualTo(1));
            Assert.That(s.SalesIncome, Is.Zero);
            Assert.That(s.CashDelta, Is.EqualTo(6));
        }

        [Test]
        public void AnOfflineExpiryIsReportedWithNoReward()
        {
            RunCommandFixtures.Rig rig = MiningRig(ProduceTight);

            RunOfflineSummary s = new OfflineSegmenter(rig.Catalog).Reconcile(rig.State, rig.Session.Permanent, D, D + 20_000);

            Assert.That(s.Expired, Is.True);
            Assert.That(s.Contract.Kind, Is.EqualTo(RunOfflineContractKind.Expired));
            Assert.That(s.Contract.EndedUtcMs, Is.EqualTo(D + 5_999 + 1));
            Assert.That(s.ContractRewardCash, Is.Zero);
            Assert.That(s.ContractRewardRp, Is.Zero);
            Assert.That(s.ContractRewardItems, Is.Empty);
            Assert.That(s.CashDelta, Is.Zero);
        }

        [Test]
        public void AnUnfinishedContractReportsNoOutcomeAndNoReward()
        {
            RunCommandFixtures.Rig rig = MiningRig(Produce);

            RunOfflineSummary s = new OfflineSegmenter(rig.Catalog).Reconcile(rig.State, rig.Session.Permanent, D, D + 3_000);

            Assert.That(s.Contract.Kind, Is.EqualTo(RunOfflineContractKind.StillActive));
            Assert.That(s.AutoDelivered || s.ProduceCompleted || s.Expired, Is.False);
            Assert.That(s.ContractRewardCash, Is.Zero);
            Assert.That(s.Contract.EndedUtcMs, Is.Null);
            Assert.That(Board(rig).Active.Produced, Is.EqualTo(2), "progress: 2.4 s effective is one completion");
        }
    }
}
