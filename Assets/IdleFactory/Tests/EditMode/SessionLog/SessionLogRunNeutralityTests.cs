using System.Collections.Generic;
using System.Linq;
using IdleFactory.Run.Application;
using IdleFactory.Run.Domain;
using IdleFactory.Run.Simulation;
using IdleFactory.Tests.EditMode.Fixtures.Run;
using IdleFactory.Unity.Persistence;
using NUnit.Framework;

namespace IdleFactory.Tests.EditMode.SessionLog
{
    /// <summary>
    /// T338 (QA from #369, W5 from #392): the session log never alters a save, and the offline outcomes of a return reach the log from the summary.
    /// </summary>
    public sealed class SessionLogRunNeutralityTests
    {
        private sealed class Played
        {
            public byte[] SaveBytes;
            public string Snapshot;
        }

        /// <summary>One scripted play of the Save V3 content (Build, sell, auto-sell, expansion, offline-free advance) over <paramref name="sink"/> (null: no sink).</summary>
        private static Played Play(IRunEventSink sink, SessionLogWriter writer)
        {
            RunCommandFixtures.Rig rig = RunCommandFixtures.NewRig(SaveV2Scenarios.Options(), 50_000_000);
            rig.Session = sink == null
                ? new RunSession(rig.Catalog, rig.State, new PermanentState(rig.Catalog), RunCommandFixtures.StartUtcMs)
                : new RunSession(rig.Catalog, rig.State, new PermanentState(rig.Catalog), RunCommandFixtures.StartUtcMs, sink);
            rig.Commands = new RunCommands(rig.Session, () => RunSimHarness.Id(rig.NextId++));
            writer?.Start();
            rig.Advance(0);

            Assert.That(rig.Build(0, RunCommandFixtures.Miner).IsSuccess, Is.True);
            rig.Advance(30_000_000);
            Assert.That(rig.Commands.Upgrade(rig.State.Machines[0].InstanceId).IsSuccess, Is.True); // T364: a purchase record in every variant
            Assert.That(rig.Commands.Sell("iron.ore", 1).IsSuccess, Is.True);
            Assert.That(rig.Commands.SetAutoSell("iron.ore", true).IsSuccess, Is.True);
            Assert.That(rig.Build(1, RunCommandFixtures.Smelter).IsSuccess, Is.True);
            Assert.That(rig.Commands.UpgradeStorage().IsSuccess, Is.True);
            rig.Advance(50_000_000);
            writer?.RecordLifecycle(RunLifecycleKind.FocusLossCheckpoint, rig.Session.DroppedEvents);

            SaveV2WriteResult save = SaveV2Mapper.Serialize(SaveV2Scenarios.Meta(rig.Catalog, 1, 1, rig.Session.TimelineUtcMs), rig.State, rig.Session.Permanent);
            Assert.That(save.IsSuccess, Is.True, save.Code);
            return new Played { SaveBytes = save.Bytes, Snapshot = RunCommandFixtures.Snapshot(rig.State) + "timeline=" + rig.Session.TimelineUtcMs };
        }

        [Test]
        public void SavesAreByteIdenticalWithLoggingOnOffAndFailing()
        {
            Played none = Play(null, null);

            Harness on = Harness.Create(withIds: false);
            Played withLog = Play(on.Writer, on.Writer);

            Harness off = Harness.Create(withIds: false);
            off.Writer.SetEnabled(false);
            Played withoutLog = Play(off.Writer, off.Writer);

            Harness failing = Harness.Create(withIds: false);
            failing.Files.FailAll = true;
            Played failingLog = Play(failing.Writer, failing.Writer);

            Assert.That(withLog.SaveBytes, Is.EqualTo(none.SaveBytes), "logging on: the save bytes are identical");
            Assert.That(withoutLog.SaveBytes, Is.EqualTo(none.SaveBytes), "logging off: identical");
            Assert.That(failingLog.SaveBytes, Is.EqualTo(none.SaveBytes), "a failing log: identical");
            Assert.That(withLog.Snapshot, Is.EqualTo(none.Snapshot));
            Assert.That(withoutLog.Snapshot, Is.EqualTo(none.Snapshot));
            Assert.That(failingLog.Snapshot, Is.EqualTo(none.Snapshot));

            on.Flush();
            List<Dictionary<string, object>> records = on.AllRecords();
            Assert.That(Harness.OfType(records, "sale"), Is.Not.Empty, "the run really raised events into the log");
            Assert.That(Harness.OfType(records, "purchase").Select(r => (string)r["kind"]), Does.Contain("upgrade"), "including the new purchase raise sites");
            Assert.That(Harness.OfType(off.AllRecords(), "sale"), Is.Empty, "and nothing while the toggle was off");
        }

        // ---- W5 (#392): offline outcomes reach the log from the summary ---------------------------------------------------------

        [Test]
        public void AnOfflineAutoDeliveryReachesTheLogFromTheSummary()
        {
            Harness h = Harness.Create();
            h.Writer.Start();
            RunCommandFixtures.Rig rig = RunCommandFixtures.NewRig(ContractFixtures.Options(), 1_000);
            rig.Session = new RunSession(rig.Catalog, rig.State, new PermanentState(rig.Catalog), ContractFixtures.StartUtcMs, h.Writer);
            rig.Commands = new RunCommands(rig.Session, () => RunSimHarness.Id(rig.NextId++));
            rig.Advance(0);
            RunSimHarness.Add(rig.State, RunCommandFixtures.Miner, 0, 100);
            ContractFixtures.Accept(rig, ContractFixtures.Deliver);
            Assert.That(rig.Commands.SetAutoDeliver(true).IsSuccess, Is.True);

            RunOfflineSummary summary = new OfflineSegmenter(rig.Catalog)
                .Reconcile(rig.State, rig.Session.Permanent, ContractFixtures.StartUtcMs, ContractFixtures.StartUtcMs + 10_000);
            Assert.That(summary.Contract.Kind, Is.EqualTo(RunOfflineContractKind.AutoDelivered));
            Assert.That(h.Records(0).Count(r => (string)r["t"] == "contract"), Is.Zero, "the segmenter advances the simulator directly: no live event was raised");

            SessionLogRaiser.RaiseOfflineReturn(rig.Session, summary, false);
            h.Flush();

            List<Dictionary<string, object>> records = h.AllRecords();
            Dictionary<string, object> offline = Harness.OfType(records, "offlineReturn").Single();
            Assert.That(offline["absenceMs"], Is.EqualTo(10_000L));
            Assert.That(offline["creditedMs"], Is.EqualTo(10_000L));
            Assert.That(offline["capHit"], Is.EqualTo(false));
            Assert.That(offline["completions"], Is.EqualTo(summary.Completions));
            Assert.That(offline["cashDelta"], Is.EqualTo(summary.CashDelta));
            Assert.That(offline["soldUnits"], Is.EqualTo(0L));
            Assert.That(offline["storeFullStallSeconds"], Is.EqualTo(0L), "the summary does not measure it");
            Assert.That(offline["reclassifiedLiveGap"], Is.EqualTo(false));

            Assert.That(Harness.OfType(records, "contract").Select(r => (string)r["event"]), Is.EqualTo(new[] { "accept", "autoDeliver" }), "the live Accept, then the offline outcome");
            Dictionary<string, object> contract = Harness.OfType(records, "contract").Single(r => (string)r["event"] == "autoDeliver");
            Assert.That(contract["event"], Is.EqualTo("autoDeliver"));
            Assert.That(contract["offerId"], Is.EqualTo(ContractFixtures.Deliver));
            Assert.That(contract["required"], Is.EqualTo(summary.Contract.RequiredUnits));
            Assert.That(contract["reward"], Is.EqualTo(summary.Contract.CashReward));
            Assert.That(contract["reward"], Is.EqualTo(7L));
            Assert.That(contract["ratioPpm"], Is.EqualTo(1_500_000L));
            Assert.That(contract["index"], Is.EqualTo(rig.State.Contracts.LastOutcome.CompletionIndex));
        }

        [TestCase(RunOfflineContractKind.ProduceCompleted, "produceComplete", 6L)]
        [TestCase(RunOfflineContractKind.Expired, "expire", 0L)]
        public void EveryAutomaticOfflineEndingIsMappedToItsEvent(RunOfflineContractKind kind, string token, long reward)
        {
            Harness h = Harness.Create();
            RunCommandFixtures.Rig rig = RunCommandFixtures.NewRig(ContractFixtures.Options(), 1_000);
            rig.Session = new RunSession(rig.Catalog, rig.State, new PermanentState(rig.Catalog), ContractFixtures.StartUtcMs, h.Writer);
            var summary = new RunOfflineSummary
            {
                Outcome = RunOfflineOutcome.Applied,
                MeasuredMs = 5_000,
                CreditedTicks = 5_000 * RunSession.TicksPerMs,
                Contract = new RunOfflineContractOutcome(kind, ContractFixtures.Produce, 5, null, 1, reward, 2, null),
            };

            SessionLogRaiser.RaiseOfflineReturn(rig.Session, summary, true);
            h.Flush();

            Dictionary<string, object> contract = Harness.OfType(h.AllRecords(), "contract").Single();
            Assert.That(contract["event"], Is.EqualTo(token));
            Assert.That(contract["reward"], Is.EqualTo(reward));
            Assert.That(Harness.OfType(h.AllRecords(), "offlineReturn").Single()["reclassifiedLiveGap"], Is.EqualTo(true));
        }

        [Test]
        public void AStillActiveOrAbsentContractRaisesNoContractEventAndABackwardClockRaisesAClockRecord()
        {
            Harness h = Harness.Create();
            RunCommandFixtures.Rig rig = RunCommandFixtures.NewRig(ContractFixtures.Options(), 1_000);
            rig.Session = new RunSession(rig.Catalog, rig.State, new PermanentState(rig.Catalog), ContractFixtures.StartUtcMs, h.Writer);

            foreach (RunOfflineContractKind kind in new[] { RunOfflineContractKind.None, RunOfflineContractKind.StillActive })
            {
                var summary = new RunOfflineSummary
                {
                    Outcome = RunOfflineOutcome.Applied,
                    Contract = new RunOfflineContractOutcome(kind, kind == RunOfflineContractKind.None ? null : ContractFixtures.Deliver, 5, null),
                };
                SessionLogRaiser.RaiseOfflineReturn(rig.Session, summary, false);
            }

            var backward = new RunOfflineSummary { Outcome = RunOfflineOutcome.BackwardClock, DepartureUtcMs = 1_000, ReturnUtcMs = 400 };
            SessionLogRaiser.RaiseOfflineReturn(rig.Session, backward, false);
            h.Flush();

            List<Dictionary<string, object>> records = h.AllRecords();
            Assert.That(Harness.OfType(records, "contract"), Is.Empty);
            Assert.That(Harness.OfType(records, "offlineReturn"), Has.Count.EqualTo(3));
            Dictionary<string, object> clock = Harness.OfType(records, "clock").Single();
            Assert.That(clock["kind"], Is.EqualTo("backward"));
            Assert.That(clock["deltaMs"], Is.EqualTo(-600L));
        }

        [Test]
        public void TheCatalogIdSetCoversEveryContentFamilyAndTheTwoExpansionTargets()
        {
            RunCommandFixtures.Rig rig = RunCommandFixtures.NewRig(SaveV2Scenarios.Options(), 100);
            var ids = new SessionLogCatalogIds(rig.Catalog);

            foreach (string id in rig.Catalog.Resources.Keys.Concat(rig.Catalog.Machines.Keys).Concat(rig.Catalog.ContractOffers.Keys)
                         .Concat(rig.Catalog.Items.Keys).Concat(rig.Catalog.Research.Keys).Concat(rig.Catalog.IndustryUpgrades.Keys))
            {
                Assert.That(ids.Contains(id), Is.True, id);
                Assert.That(SessionLogIds.IsValid(id), Is.True, "a shipped-style ID passes the strict pattern: " + id);
            }

            Assert.That(ids.Contains("storage"), Is.True);
            Assert.That(ids.Contains("factory"), Is.True);
            Assert.That(ids.Contains(null), Is.False);
            Assert.That(ids.Contains("unknown.id"), Is.False);
        }
    }
}
