using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using IdleFactory.Run.Application;
using IdleFactory.Run.Content;
using IdleFactory.Run.Domain;
using IdleFactory.Tests.EditMode.Fixtures.Run;
using IdleFactory.Unity.Content;
using IdleFactory.Unity.Persistence;
using NUnit.Framework;

namespace IdleFactory.Tests.EditMode.SessionLog
{
    /// <summary>
    /// T364 (#483, owner decision 2026-10-21 on #403): the Core raise sites of the session log (Prestige, Purchase, Bottleneck, the contract commands),
    /// the immediate flushes, the neutrality of the log towards the run, the US8 Independent Test end to end, and the T338 review items.
    /// </summary>
    public sealed class SessionLogRaiseSiteTests
    {
        private const long Second = RunSimHarness.TicksPerSecond;
        private const long StartUtcMs = RunCommandFixtures.StartUtcMs;

        /// <summary>Records every event of a session.</summary>
        private sealed class RaiseSiteRecorder : IRunEventSink
        {
            public readonly List<RunContractEvent> Contracts = new List<RunContractEvent>();
            public readonly List<RunPurchaseEvent> Purchases = new List<RunPurchaseEvent>();
            public readonly List<RunPrestigeEvent> Prestiges = new List<RunPrestigeEvent>();
            public readonly List<RunBottleneckEvent> Bottlenecks = new List<RunBottleneckEvent>();
            public readonly List<string> Order = new List<string>();
            public void OnHeader(in RunHeaderEvent e) { }
            public void OnSample(in RunSampleEvent e) { }
            public void OnMilestone(in RunMilestoneEvent e) { }
            public void OnSale(in RunSaleEvent e) => Order.Add("sale");
            public void OnMining(in RunMiningEvent e) { }
            public void OnContract(in RunContractEvent e) { Contracts.Add(e); Order.Add("contract"); }
            public void OnPrestige(in RunPrestigeEvent e) { Prestiges.Add(e); Order.Add("prestige"); }
            public void OnOfflineReturn(in RunOfflineReturnEvent e) { }
            public void OnBottleneck(in RunBottleneckEvent e) => Bottlenecks.Add(e);
            public void OnPurchase(in RunPurchaseEvent e) { Purchases.Add(e); Order.Add("purchase"); }
            public void OnClock(in RunClockEvent e) { }
            public void OnLifecycle(in RunLifecycleEvent e) { }
        }

        private sealed class ThrowingSink : IRunEventSink
        {
            public void OnHeader(in RunHeaderEvent e) => throw new InvalidOperationException("sink");
            public void OnSample(in RunSampleEvent e) => throw new InvalidOperationException("sink");
            public void OnMilestone(in RunMilestoneEvent e) => throw new InvalidOperationException("sink");
            public void OnSale(in RunSaleEvent e) => throw new InvalidOperationException("sink");
            public void OnMining(in RunMiningEvent e) => throw new InvalidOperationException("sink");
            public void OnContract(in RunContractEvent e) => throw new InvalidOperationException("sink");
            public void OnPrestige(in RunPrestigeEvent e) => throw new InvalidOperationException("sink");
            public void OnOfflineReturn(in RunOfflineReturnEvent e) => throw new InvalidOperationException("sink");
            public void OnBottleneck(in RunBottleneckEvent e) => throw new InvalidOperationException("sink");
            public void OnPurchase(in RunPurchaseEvent e) => throw new InvalidOperationException("sink");
            public void OnClock(in RunClockEvent e) => throw new InvalidOperationException("sink");
            public void OnLifecycle(in RunLifecycleEvent e) => throw new InvalidOperationException("sink");
        }

        private static IndustryUpgradeDef Industry() => new IndustryUpgradeDef("industry.a", "A", 3, new Curve(1, 1_100_000, "ceil"),
            new EffectDef("machineSpeed", "allMachines", null, 1_100_000));

        private static RunCommandFixtures.Options Options(long prestigeThreshold = 10_000_000)
        {
            RunCommandFixtures.Options o = ContractFixtures.Options();
            o.DiscountResearch = true;
            o.Industry = new[] { Industry() };
            o.Prestige = new PrestigeDef(prestigeThreshold, "sqrt", 10, prestigeThreshold);
            return o;
        }

        private static RunCommandFixtures.Rig NewRig(IRunEventSink sink, long cash, long prestigeThreshold = 10_000_000, long rp = 0)
        {
            RunCommandFixtures.Rig rig = RunCommandFixtures.NewRig(Options(prestigeThreshold), cash, rp: rp);
            PermanentState permanent = new PermanentState(rig.Catalog);
            rig.Session = sink == null
                ? new RunSession(rig.Catalog, rig.State, permanent, StartUtcMs)
                : new RunSession(rig.Catalog, rig.State, permanent, StartUtcMs, sink);
            rig.Commands = new RunCommands(rig.Session, () => RunSimHarness.Id(rig.NextId++), () => RunSimHarness.Id(7000));
            rig.Advance(0);
            return rig;
        }

        // ---- contract commands --------------------------------------------------------------------------------------------------

        [Test]
        public void AcceptDeliverAndAbandonEachRaiseOneContractRecordAfterTheyApply_ARejectionRaisesNone()
        {
            var sink = new RaiseSiteRecorder();
            RunCommandFixtures.Rig rig = NewRig(sink, 1_000);

            Assert.That(rig.Commands.AcceptContract(ContractFixtures.Deliver).IsSuccess, Is.True);
            Assert.That(rig.Commands.AcceptContract(ContractFixtures.Deliver).IsSuccess, Is.False, "one contract at a time");
            Assert.That(rig.Commands.AcceptContract("contract.unknown").IsSuccess, Is.False);
            Assert.That(sink.Contracts, Has.Count.EqualTo(1), "the two rejections raised nothing");
            RunContractEvent accept = sink.Contracts[0];
            Assert.That(accept.Kind, Is.EqualTo(RunContractEventKind.Accept));
            Assert.That(accept.OfferId, Is.EqualTo(ContractFixtures.Deliver));
            Assert.That(accept.Index, Is.EqualTo(0));
            Assert.That(accept.Required, Is.EqualTo(5));
            Assert.That(accept.Reward, Is.EqualTo(0), "the reward is only known at the delivery");
            Assert.That(accept.RatioPpm, Is.EqualTo(1_500_000));

            Assert.That(rig.Commands.DeliverContract().IsSuccess, Is.False, "no stock yet");
            Assert.That(sink.Contracts, Has.Count.EqualTo(1));
            RunSimHarness.Stock(rig.State, ContractFixtures.Ore, 5);
            Assert.That(rig.Commands.DeliverContract().IsSuccess, Is.True);
            RunContractEvent deliver = sink.Contracts[1];
            Assert.That(deliver.Kind, Is.EqualTo(RunContractEventKind.Deliver));
            Assert.That(deliver.Index, Is.EqualTo(0), "the completion count before the ending, like the simulator-driven kinds");
            Assert.That(deliver.Required, Is.EqualTo(5));
            Assert.That(deliver.Reward, Is.EqualTo(7));

            rig.Advance(31 * Second); // the 30 s cooldown passes
            Assert.That(rig.Commands.AcceptContract(ContractFixtures.Deliver).IsSuccess, Is.True);
            Assert.That(rig.Commands.AbandonContract().IsSuccess, Is.True);
            Assert.That(rig.Commands.AbandonContract().IsSuccess, Is.False, "nothing left to abandon");
            Assert.That(sink.Contracts.Select(c => c.Kind), Is.EqualTo(new[]
            {
                RunContractEventKind.Accept, RunContractEventKind.Deliver, RunContractEventKind.Accept, RunContractEventKind.Abandon,
            }));
            Assert.That(sink.Contracts[3].Index, Is.EqualTo(1), "the second acceptance of the offer scales by completion count 1");
            Assert.That(sink.Contracts[3].Reward, Is.EqualTo(0));
        }

        // ---- purchases ----------------------------------------------------------------------------------------------------------

        [Test]
        public void EveryPurchaseKindRaisesOneRecordWithTheLevelAndTheCostPaid_RejectionsRaiseNone()
        {
            var sink = new RaiseSiteRecorder();
            RunCommandFixtures.Rig rig = NewRig(sink, 100_000, rp: 20);
            rig.Session.Permanent.TryAddIp(10);

            long cash = rig.State.Cash;
            Assert.That(rig.Build(0, RunCommandFixtures.Miner).IsSuccess, Is.True);
            long buildCost = cash - rig.State.Cash;
            Assert.That(rig.Build(0, RunCommandFixtures.Miner).IsSuccess, Is.False, "the slot is taken");

            cash = rig.State.Cash;
            Assert.That(rig.Commands.Upgrade(rig.State.Machines[0].InstanceId).IsSuccess, Is.True);
            long upgradeCost = cash - rig.State.Cash;

            cash = rig.State.Cash;
            Assert.That(rig.Commands.UpgradeStorage().IsSuccess, Is.True);
            long storageCost = cash - rig.State.Cash;

            cash = rig.State.Cash;
            Assert.That(rig.Commands.UpgradeFactory().IsSuccess, Is.True);
            long factoryCost = cash - rig.State.Cash;

            Assert.That(rig.Commands.PurchaseResearch("research.unknown").IsSuccess, Is.False);
            Assert.That(rig.Commands.PurchaseResearch(RunCommandFixtures.MachineDiscount).IsSuccess, Is.True);
            Assert.That(rig.Commands.PurchaseIndustry("industry.a").IsSuccess, Is.True);
            Assert.That(rig.Commands.PurchaseIndustry("industry.unknown").IsSuccess, Is.False);

            Assert.That(sink.Purchases.Select(p => p.Kind), Is.EqualTo(new[]
            {
                RunPurchaseKind.Build, RunPurchaseKind.Upgrade, RunPurchaseKind.Storage, RunPurchaseKind.Factory, RunPurchaseKind.Research, RunPurchaseKind.Industry,
            }), "one record per applied purchase");
            Assert.That(sink.Purchases[0].TargetId, Is.EqualTo(RunCommandFixtures.Miner));
            Assert.That(sink.Purchases[0].Level, Is.EqualTo(1));
            Assert.That(sink.Purchases[0].Cost, Is.EqualTo(buildCost));
            Assert.That(sink.Purchases[1].TargetId, Is.EqualTo(RunCommandFixtures.Miner));
            Assert.That(sink.Purchases[1].Level, Is.EqualTo(2));
            Assert.That(sink.Purchases[1].Cost, Is.EqualTo(upgradeCost));
            Assert.That(sink.Purchases[2].TargetId, Is.EqualTo("storage"));
            Assert.That(sink.Purchases[2].Level, Is.EqualTo(2));
            Assert.That(sink.Purchases[2].Cost, Is.EqualTo(storageCost));
            Assert.That(sink.Purchases[3].TargetId, Is.EqualTo("factory"));
            Assert.That(sink.Purchases[3].Level, Is.EqualTo(2));
            Assert.That(sink.Purchases[3].Cost, Is.EqualTo(factoryCost));
            Assert.That(sink.Purchases[4].TargetId, Is.EqualTo(RunCommandFixtures.MachineDiscount));
            Assert.That(sink.Purchases[4].Level, Is.EqualTo(1));
            Assert.That(sink.Purchases[4].Cost, Is.EqualTo(5), "RP");
            Assert.That(sink.Purchases[5].TargetId, Is.EqualTo("industry.a"));
            Assert.That(sink.Purchases[5].Level, Is.EqualTo(1));
            Assert.That(sink.Purchases[5].Cost, Is.EqualTo(1), "IP");
            foreach (RunPurchaseEvent p in sink.Purchases) Assert.That(p.SecondsSinceAffordable, Is.Zero, "everything was affordable from the start of the session: no time passed");
        }

        [Test]
        public void SecondsSinceAffordableCountsFromTheMomentTheCashReachedTheCost()
        {
            var sink = new RaiseSiteRecorder();
            RunCommandFixtures.Rig rig = NewRig(sink, 5); // a Miner costs 10
            rig.Advance(10 * Second);
            Assert.That(rig.Commands.Build(0, RunCommandFixtures.Miner).ReasonCode, Is.EqualTo(RunReasons.CashInsufficient));
            Assert.That(sink.Purchases, Is.Empty);

            rig.State.TryAddCash(100); // affordable from here (the next publish records it)
            rig.Advance(0);
            rig.Advance(20 * Second);
            Assert.That(rig.Build(0, RunCommandFixtures.Miner).IsSuccess, Is.True);

            Assert.That(sink.Purchases, Has.Count.EqualTo(1));
            Assert.That(sink.Purchases[0].SecondsSinceAffordable, Is.EqualTo(20), "the cash was enough for 20 s before the purchase");
        }

        // ---- prestige -----------------------------------------------------------------------------------------------------------

        [Test]
        public void PrestigeIsRaisedOnceWhenTheTransitionIsPublished_NotAtTheConfirmationNorAtACancel()
        {
            var sink = new RaiseSiteRecorder();
            RunCommandFixtures.Rig rig = NewRig(sink, 10_000_000);
            var goals = new GoalsTracker();
            goals.Current(rig.Session, 0); // the goal line sees the Factory Sale available now
            rig.Advance(7_500 * RunSession.TicksPerMs);

            RunCommandResult<PrestigeConfirmation> confirmed = rig.Commands.ConfirmPrestige();
            Assert.That(confirmed.IsSuccess, Is.True, confirmed.ReasonCode);
            Assert.That(sink.Prestiges, Is.Empty, "a pending candidate is not a prestige yet");
            Assert.That(rig.Commands.CancelPendingTransition().IsSuccess, Is.True);
            Assert.That(sink.Prestiges, Is.Empty, "a cancelled one never is");

            Assert.That(rig.Commands.ConfirmPrestige().IsSuccess, Is.True);
            Assert.That(rig.Commands.ConfirmPrestige().IsSuccess, Is.False, "transition.pending");
            Assert.That(rig.Commands.PromotePrestige().IsSuccess, Is.True);

            Assert.That(sink.Prestiges, Has.Count.EqualTo(1));
            RunPrestigeEvent e = sink.Prestiges[0];
            Assert.That(e.Threshold, Is.EqualTo(10_000_000));
            Assert.That(e.FactoryValue, Is.GreaterThanOrEqualTo(10_000_000));
            Assert.That(e.Ip, Is.EqualTo(confirmed.Value.IpAwarded));
            Assert.That(e.SecondsSinceEligible, Is.EqualTo(7), "from the goal line's first sight of the Factory Sale to the confirmation instant, floored");
            Assert.That(e.IndustryPurchases, Is.Empty);
            Assert.That(rig.Session.State.RunNumber, Is.EqualTo(2));
        }

        [Test]
        public void ThePrestigeRecordListsTheIndustryLevelsAndTheNextRunStartsItsMetersAgain()
        {
            var sink = new RaiseSiteRecorder();
            RunCommandFixtures.Rig rig = NewRig(sink, 10_000_000);
            rig.Session.Permanent.TryAddIp(5);
            Assert.That(rig.Commands.PurchaseIndustry("industry.a").IsSuccess, Is.True);
            Assert.That(rig.Commands.ConfirmPrestige().IsSuccess, Is.True);
            Assert.That(rig.Commands.PromotePrestige().IsSuccess, Is.True);

            RunPrestigeEvent e = sink.Prestiges.Single();
            Assert.That(e.SecondsSinceEligible, Is.Zero, "first noted at the confirmation");
            Assert.That(e.IndustryPurchases.Select(p => p.IndustryId + ":" + p.Level), Is.EqualTo(new[] { "industry.a:1" }));
            Assert.That(sink.Order.IndexOf("purchase"), Is.LessThan(sink.Order.IndexOf("prestige")));
        }

        // ---- bottleneck ---------------------------------------------------------------------------------------------------------

        [Test]
        public void ABlockedMachineAccruesMachineSecondsPerResource_IndependentOfHowTheTimeIsCut()
        {
            var sink = new RaiseSiteRecorder();
            RunCommandFixtures.Rig rig = NewRig(sink, 1_000);
            RunSimHarness.Add(rig.State, RunCommandFixtures.Miner, 0, 100);
            RunSimHarness.Stock(rig.State, ContractFixtures.Ore, 1_000); // a full store: not even one unit fits, so the Miner cannot run

            rig.Advance(3 * Second);                // blocked from here on, but not blocked after the previous step
            rig.Advance(4 * Second);                // blocked after both steps: 4 s
            rig.Advance(6 * Second);                // 6 s
            rig.Session.RaiseBottlenecks();

            RunBottleneckEvent e = sink.Bottlenecks.Single();
            Assert.That(e.ResourceId, Is.EqualTo(ContractFixtures.Ore), "the resource the stalled machine fails to produce");
            Assert.That(e.StoreFullSeconds, Is.EqualTo(10));
            Assert.That(e.MissingInputSeconds, Is.Zero);

            rig.Session.RaiseBottlenecks();
            Assert.That(sink.Bottlenecks, Has.Count.EqualTo(1), "the window was drained");
            rig.Advance(5 * Second);
            rig.Session.RaiseBottlenecks();
            Assert.That(sink.Bottlenecks.Last().StoreFullSeconds, Is.EqualTo(5), "the next window counts from the previous call");
        }

        [Test]
        public void AnUnblockedMachineAccruesNothing()
        {
            var sink = new RaiseSiteRecorder();
            RunCommandFixtures.Rig rig = NewRig(sink, 1_000);
            RunSimHarness.Add(rig.State, RunCommandFixtures.Miner, 0, 100);
            rig.Advance(30 * Second);
            rig.Session.RaiseBottlenecks();
            Assert.That(sink.Bottlenecks, Is.Empty);
        }

        // ---- the log never alters the run ---------------------------------------------------------------------------------------

        private static string Scenario(IRunEventSink sink)
        {
            RunCommandFixtures.Rig rig = NewRig(sink, 50_000, prestigeThreshold: 1_000, rp: 10);
            rig.Session.Permanent.TryAddIp(5);
            Assert.That(rig.Build(0, RunCommandFixtures.Miner).IsSuccess, Is.True);
            rig.Advance(30 * Second);
            Assert.That(rig.Commands.AcceptContract(ContractFixtures.Deliver).IsSuccess, Is.True);
            RunSimHarness.Stock(rig.State, ContractFixtures.Ore, 20);
            Assert.That(rig.Commands.DeliverContract().IsSuccess, Is.True);
            Assert.That(rig.Commands.PurchaseResearch(RunCommandFixtures.MachineDiscount).IsSuccess, Is.True);
            Assert.That(rig.Commands.PurchaseIndustry("industry.a").IsSuccess, Is.True);
            Assert.That(rig.Commands.UpgradeStorage().IsSuccess, Is.True);
            rig.Advance(20 * Second);
            rig.Session.RaiseBottlenecks();
            Assert.That(rig.Commands.ConfirmPrestige().IsSuccess, Is.True);
            Assert.That(rig.Commands.PromotePrestige().IsSuccess, Is.True);
            rig.Advance(10 * Second);
            return RunCommandFixtures.Snapshot(rig.Session.State) + "timeline=" + rig.Session.TimelineUtcMs + " ip=" + rig.Session.Permanent.Ip +
                   " prestiges=" + rig.Session.Permanent.PrestigeCount;
        }

        [Test]
        public void TheNewRaiseSitesNeverChangeTheRun_WithARecorderAThrowingSinkTheWriterAndAFailingWriter()
        {
            string baseline = Scenario(null);

            Assert.That(Scenario(new RaiseSiteRecorder()), Is.EqualTo(baseline), "a recording sink");
            Assert.That(Scenario(new ThrowingSink()), Is.EqualTo(baseline), "a sink that throws on every event");

            Harness on = Harness.Create(withIds: false);
            Assert.That(Scenario(on.Writer), Is.EqualTo(baseline), "the writer");
            on.Flush();
            Assert.That(Harness.OfType(on.AllRecords(), "purchase"), Has.Count.EqualTo(4));
            Assert.That(Harness.OfType(on.AllRecords(), "prestige"), Has.Count.EqualTo(1));
            Assert.That(Harness.OfType(on.AllRecords(), "contract").Select(r => (string)r["event"]), Is.EqualTo(new[] { "accept", "deliver" }));

            Harness failing = Harness.Create(withIds: false);
            failing.Files.FailAll = true;
            Assert.That(Scenario(failing.Writer), Is.EqualTo(baseline), "a writer over a failing file layer");

            Harness off = Harness.Create(withIds: false);
            off.Writer.SetEnabled(false);
            Assert.That(Scenario(off.Writer), Is.EqualTo(baseline), "recording off");
            Assert.That(off.AllRecords().Where(r => (string)r["t"] != "lifecycle"), Is.Empty, "only the toggle record itself");
        }

        // ---- immediate flushes (the contract's lifecycle points; T338 QA W1) ---------------------------------------------------------

        [Test]
        public void APrestigeAnOfflineReturnAndALiveGapAreOnDiskWithoutAFlushCall_AnOrdinaryRecordIsNot()
        {
            Harness h = Harness.Create(withIds: false);
            h.Writer.OnContract(new RunContractEvent(RunContractEventKind.Accept, "contract.deliver", 0, 5, 0, 1_500_000));
            Assert.That(h.AllRecords(), Is.Empty, "buffered");

            h.Writer.OnPrestige(new RunPrestigeEvent(10, 10, 1, 0, new RunIndustryLevel[0]));
            Assert.That(Harness.OfType(h.AllRecords(), "prestige"), Has.Count.EqualTo(1), "flushed at once");
            Assert.That(Harness.OfType(h.AllRecords(), "contract"), Has.Count.EqualTo(1), "and the buffered line before it with it");

            h.Writer.OnClock(new RunClockEvent(RunClockKind.Backward, -5, false));
            Assert.That(Harness.OfType(h.AllRecords(), "clock"), Is.Empty, "a backward clock is an ordinary record");

            h.Writer.OnOfflineReturn(new RunOfflineReturnEvent(1, 1, false, 0, 0, 0, 0, false));
            Assert.That(Harness.OfType(h.AllRecords(), "offlineReturn"), Has.Count.EqualTo(1));
            Assert.That(Harness.OfType(h.AllRecords(), "clock"), Has.Count.EqualTo(1), "the earlier clock record went with it");

            h.Writer.OnClock(new RunClockEvent(RunClockKind.ForwardJump, 9_000, false));
            Assert.That(Harness.OfType(h.AllRecords(), "clock"), Has.Count.EqualTo(1), "a forward jump is ordinary");
            h.Writer.OnClock(new RunClockEvent(RunClockKind.LiveGap, 61_000, true));
            Assert.That(Harness.OfType(h.AllRecords(), "clock"), Has.Count.EqualTo(3), "a live gap flushes");
        }

        [Test]
        public void ARaisedOfflineReturnPutsTheContractAndClockRecordsOnDiskBeforeItself()
        {
            Harness h = Harness.Create(withIds: false);
            RunCommandFixtures.Rig rig = NewRig(h.Writer, 1_000);
            var summary = new RunOfflineSummary
            {
                Outcome = RunOfflineOutcome.Applied,
                MeasuredMs = 5_000,
                CreditedTicks = 5_000 * RunSession.TicksPerMs,
                Contract = new RunOfflineContractOutcome(RunOfflineContractKind.Expired, ContractFixtures.Produce, 5, null, 1, 0, 2, null),
            };

            SessionLogRaiser.RaiseOfflineReturn(rig.Session, summary, false);

            string[] types = h.AllRecords().Select(r => (string)r["t"]).ToArray();
            Assert.That(types, Is.EqualTo(new[] { "contract", "offlineReturn" }), "on disk without a flush call, the return record last");
        }

        // ---- US8 Independent Test, end to end ------------------------------------------------------------------------------------

        private static readonly Dictionary<string, string[]> Allowed = new Dictionary<string, string[]>
        {
            { "header", new[] { "schema", "contentRevision", "buildKind", "platformClass", "timeScale", "sessionId" } },
            { "sample", new[] { "playSeconds", "factoryValue", "cash", "machines", "storageLevel", "factoryLevel" } },
            { "milestone", new[] { "kind", "playSeconds" } },
            { "sale", new[] { "resourceId", "units", "cash", "manual" } },
            { "mining", new[] { "resourceId", "counted", "ignored", "blocked", "ore" } },
            { "contract", new[] { "event", "offerId", "index", "required", "reward", "ratioPpm" } },
            { "prestige", new[] { "factoryValue", "threshold", "ip", "secondsSinceEligible", "industryPurchases" } },
            { "offlineReturn", new[] { "absenceMs", "creditedMs", "capHit", "completions", "storeFullStallSeconds", "soldUnits", "cashDelta", "reclassifiedLiveGap" } },
            { "bottleneck", new[] { "resourceId", "missingInputSeconds", "storeFullSeconds" } },
            { "purchase", new[] { "kind", "targetId", "level", "cost", "secondsSinceAffordable" } },
            { "clock", new[] { "kind", "deltaMs", "reclassified" } },
            { "lifecycle", new[] { "event", "droppedEvents" } },
        };

        [Test]
        public void ASessionWithASaleAContractAPrestigeAndAnOfflineReturnExportsAValidLogWithinTheBound()
        {
            Harness h = Harness.Create(withIds: false);
            h.Writer.Start();
            RunCommandFixtures.Rig rig = NewRig(h.Writer, 10_000_000);
            RunSimHarness.Add(rig.State, RunCommandFixtures.Miner, 0, 100);
            RunSimHarness.Stock(rig.State, ContractFixtures.Ore, 12);

            Assert.That(rig.Commands.Sell(ContractFixtures.Ore, 1).IsSuccess, Is.True, "a sale");
            Assert.That(rig.Commands.AcceptContract(ContractFixtures.Deliver).IsSuccess, Is.True);
            Assert.That(rig.Commands.DeliverContract().IsSuccess, Is.True, "a contract");
            Assert.That(rig.Commands.ConfirmPrestige().IsSuccess, Is.True);
            Assert.That(rig.Commands.PromotePrestige().IsSuccess, Is.True, "a prestige");
            var summary = new RunOfflineSummary { Outcome = RunOfflineOutcome.Applied, MeasuredMs = 90_000, CreditedTicks = 90_000 * RunSession.TicksPerMs };
            SessionLogRaiser.RaiseOfflineReturn(rig.Session, summary, false); // an offline return

            SessionLogExportResult result = new SessionLogExport(h.Writer, h.Files, () => new DateTime(2026, 10, 21, 12, 0, 0, DateTimeKind.Utc)).Export();
            Assert.That(result.Status, Is.EqualTo(SessionLogExportStatus.Exported));
            byte[] exported = h.Files.Bytes(result.FilePath);
            Assert.That(exported.Length, Is.LessThanOrEqualTo(2 * SessionLogWriter.SegmentCapacityBytes), "within the 256 KB bound");

            List<Dictionary<string, object>> records = Harness.Parse(exported);
            Assert.That(records, Has.Count.EqualTo(result.Lines), "every exported line is a whole line with a valid CRC");
            string[] types = records.Select(r => (string)r["t"]).ToArray();
            foreach (string required in new[] { "header", "sale", "contract", "prestige", "offlineReturn", "lifecycle" })
                Assert.That(types, Does.Contain(required), required);
            foreach (Dictionary<string, object> record in records)
            {
                string type = (string)record["t"];
                Assert.That(Allowed.ContainsKey(type), Is.True, "record type " + type);
                var expected = new HashSet<string>(Allowed[type]) { "t", "v", "crc" };
                Assert.That(record.Keys.All(expected.Contains), Is.True, "schema fields only: " + type + " " + string.Join(",", record.Keys));
            }
        }

        // ---- T338 review items ----------------------------------------------------------------------------------------------------------

        [Test]
        public void EveryShippedContentIdPassesTheStrictIdPattern()
        {
            var loaded = GameplayContentDispatcher.Load(File.ReadAllBytes("Assets/IdleFactory/Content/Iron/iron-factory-content.json"));
            Assert.That(loaded.IsSuccess, Is.True);
            RunCatalog catalog = loaded.RunCatalog;
            var ids = new SessionLogCatalogIds(catalog);
            var all = catalog.Resources.Keys.Concat(catalog.Machines.Keys).Concat(catalog.ContractOffers.Keys).Concat(catalog.Items.Keys)
                .Concat(catalog.Research.Keys).Concat(catalog.IndustryUpgrades.Keys).ToList();
            Assert.That(all, Is.Not.Empty);
            foreach (string id in all)
            {
                Assert.That(SessionLogIds.IsValid(id), Is.True, "shipped ID fails the strict pattern: " + id);
                Assert.That(ids.Contains(id), Is.True, id);
            }
        }

        [Test]
        public void OpeningTheSegmentsReadsOnlyTheLastByteNeverTheWholeFile()
        {
            Harness h = Harness.Create();
            byte[] big = new byte[100_000];
            for (int i = 0; i < big.Length; i++) big[i] = (byte)'a';
            big[big.Length - 1] = (byte)'\n';
            h.Files.Put(h.Segment(0), big);
            h.Files.Log.Clear();

            h.Writer.Start();

            Assert.That(h.Files.Log, Does.Not.Contain("ReadAll segment-0.jsonl"), "no whole-file read at start-up");
            Assert.That(h.Files.Log, Does.Contain("ReadLastByte segment-0.jsonl"));
            Assert.That(h.Files.Bytes(h.Segment(0)).Length, Is.GreaterThan(big.Length), "the new records were appended after the old tail");
        }

        [Test]
        public void ATornTailIsEndedWithANewlineSoTheNextRecordStartsClean()
        {
            Harness h = Harness.Create();
            h.Files.Put(h.Segment(0), new[] { (byte)'{', (byte)'"', (byte)'t' });

            h.Writer.Start();

            byte[] bytes = h.Files.Bytes(h.Segment(0));
            Assert.That(bytes[3], Is.EqualTo((byte)'\n'), "the torn line is ended");
            Assert.That(Harness.Parse(bytes).Select(r => (string)r["t"]), Does.Contain("header"));
        }
    }
}
