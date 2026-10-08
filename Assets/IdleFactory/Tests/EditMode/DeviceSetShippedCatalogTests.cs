using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using IdleFactory.Core.Application;
using IdleFactory.Run.Application;
using IdleFactory.Run.Content;
using IdleFactory.Run.Domain;
using IdleFactory.Run.Simulation;
using IdleFactory.Unity.Persistence;
using NUnit.Framework;
using UnityEngine;

namespace IdleFactory.Tests.EditMode
{
    /// <summary>
    /// The shipped-catalog device checkpoint sets (T245, PE-6, SC-008, OPS-5). The first emulator run (#206) showed that the
    /// player rejects the W003 set because every W003 fixture is generated against the test-local catalog of
    /// <c>W003Workload</c>. These tests build the SHIPPED catalog the way the player does (<c>RunCatalogBuilder</c> over
    /// <c>iron-factory-content.json</c>), regenerate each new set byte for byte, load it through the player cold-start
    /// composition (<see cref="RunPersistence"/> over the real file store) and pin why the old set is rejected.
    /// </summary>
    public sealed class DeviceSetShippedCatalogTests
    {
        private const string RegenerateEnvironmentVariable = "IDLEFACTORY_REGENERATE_SAVEV2_FIXTURES";
        private const long EightHoursMs = 8L * 3600 * 1000;

        private bool _logToUnity;
        private string _root;
        private List<CheckpointDiagnostic> _diagnostics;
        private Action<CheckpointDiagnostic> _listener;

        private static string FixturesDirectory => Path.Combine(Application.dataPath, "IdleFactory/Tests/EditMode/Fixtures/SaveV2");

        [SetUp]
        public void SetUp()
        {
            _logToUnity = CheckpointDiagnostics.LogToUnity;
            CheckpointDiagnostics.LogToUnity = false;
            _diagnostics = new List<CheckpointDiagnostic>();
            _listener = d => _diagnostics.Add(d);
            CheckpointDiagnostics.Reported += _listener;
            _root = RunCheckpointTestSupport.NewTempRoot();
        }

        [TearDown]
        public void TearDown()
        {
            CheckpointDiagnostics.Reported -= _listener;
            CheckpointDiagnostics.LogToUnity = _logToUnity;
            RunCheckpointTestSupport.DeleteQuietly(_root);
        }

        private static ShippedDeviceScenario Scenario(string directory)
        {
            if (directory == ShippedDeviceSets.W003SShippedDirectory) return ShippedDeviceSets.BuildW003SShipped();
            if (directory == ShippedDeviceSets.PrestigeReadyDirectory) return ShippedDeviceSets.BuildPrestigeReady();
            throw new ArgumentException("Unknown device set " + directory);
        }

        private static string Describe(IEnumerable<CheckpointDiagnostic> diagnostics) =>
            diagnostics.Count() == 0 ? "none" : string.Join("; ", diagnostics.Select(d => d.Operation + "/" + d.Stage));

        // Copies the committed files into IdleFactory/Checkpoints/v2/ of a fresh root, as the journey script pushes them.
        private string InstallCommittedSet(string directory)
        {
            string source = Path.Combine(FixturesDirectory, directory);
            string target = RunCheckpointTestSupport.V2Directory(_root);
            Directory.CreateDirectory(target);
            foreach (string name in ShippedDeviceSets.Files)
            {
                Assert.That(File.Exists(Path.Combine(source, name)), Is.True, "Committed device file is missing: " + directory + "/" + name);
                File.Copy(Path.Combine(source, name), Path.Combine(target, name));
            }

            return _root;
        }

        // ------------------------------------------------------------------------------------------------------
        // Byte identity (drift fails, never skips)
        // ------------------------------------------------------------------------------------------------------

        [TestCase(ShippedDeviceSets.W003SShippedDirectory)]
        [TestCase(ShippedDeviceSets.PrestigeReadyDirectory)]
        public void DeviceSet_IsByteIdenticalToTheStoreRegeneration(string setDirectory)
        {
            ShippedDeviceScenario scenario = Scenario(setDirectory);
            List<SaveV2DeviceFile> regenerated = ShippedDeviceSets.BuildFiles(scenario, out byte[] pending);
            if (Environment.GetEnvironmentVariable(RegenerateEnvironmentVariable) == "1") ShippedDeviceSets.Write(FixturesDirectory, scenario);

            string hint = " Set " + RegenerateEnvironmentVariable + "=1 and rerun this file and SaveV2FixtureTests to update the committed device set and README.";
            string directory = Path.Combine(FixturesDirectory, setDirectory);
            foreach (SaveV2DeviceFile file in regenerated)
            {
                string path = Path.Combine(directory, file.Name);
                Assert.That(File.Exists(path), Is.True, "Committed device file is missing: " + setDirectory + "/" + file.Name + "." + hint);
                Assert.That(File.ReadAllBytes(path), Is.EqualTo(file.Bytes), "Device file drifted: " + setDirectory + "/" + file.Name + "." + hint);
            }

            Assert.That(Directory.GetFiles(directory).Select(Path.GetFileName).Where(n => !n.EndsWith(".meta", StringComparison.Ordinal))
                .OrderBy(n => n, StringComparer.Ordinal), Is.EqualTo(ShippedDeviceSets.Files.OrderBy(n => n, StringComparer.Ordinal)),
                "no unlisted file in the device set");
            Assert.That(pending, Is.EqualTo(regenerated.Single(f => f.Name == SaveV2CheckpointFiles.DataAFileName).Bytes),
                "the pending slot only holds the candidate that was then published");
        }

        [Test]
        public void ShippedCatalog_IsTheOneThePlayerBuilds()
        {
            RunCatalog catalog = ShippedDeviceSets.LoadShippedCatalog();
            Assert.That(catalog.ContentRevision, Is.EqualTo(1));
            Assert.That(catalog.FactoryLevels[0].SlotCount, Is.EqualTo(4), "the shipped factory level 1 has four slots");
            Assert.That(catalog.OfflinePolicy.MaxCreditedTicks, Is.EqualTo(8L * 3600 * 10_000_000), "the capped eight hours");
        }

        // ------------------------------------------------------------------------------------------------------
        // The committed state, read by the store
        // ------------------------------------------------------------------------------------------------------

        [Test]
        public void W003SShipped_HoldsTheIntentOfW003SWithinTheShippedContent()
        {
            RunCatalog catalog = ShippedDeviceSets.LoadShippedCatalog();
            var store = new JsonRunCheckpointStore(InstallCommittedSet(ShippedDeviceSets.W003SShippedDirectory), catalog);
            var read = store.ReadActive();
            Assert.That(read.Succeeded, Is.True, read.Message);
            RunState run = read.Checkpoint.Run;
            Assert.That(run.SlotCount, Is.EqualTo(12));
            Assert.That(run.Machines.Select(m => m.SlotIndex), Is.EqualTo(Enumerable.Range(0, 12)), "twelve machines, slots 0 to 11");
            foreach (string family in new[] { "machine.miner", "machine.smelter", "machine.press", "machine.assembler" })
                Assert.That(run.Machines.Count(m => m.MachineId == family && m.IsActive), Is.GreaterThan(0), family + " is built and active");
            Assert.That(run.AutoSell.OrderBy(a => a, StringComparer.Ordinal), Is.EqualTo(catalog.Resources.Keys.OrderBy(a => a, StringComparer.Ordinal)),
                "auto-sell on every resource");
            Assert.That(run.Effects.Count, Is.EqualTo(1), "one Item effect");
            Assert.That(run.Overdrive.IsActive(read.Checkpoint.DepartureUtcMs), Is.True, "Overdrive is active at the departure");
            Assert.That(run.Effects.Effects[0].EndUtcMs, Is.GreaterThan(read.Checkpoint.DepartureUtcMs), "the Item effect is active at the departure");
        }

        [Test]
        public void PrestigeReady_MeetsTheSaleThresholdSoThePrestigeConfirmationCanOpen()
        {
            RunCatalog catalog = ShippedDeviceSets.LoadShippedCatalog();
            var store = new JsonRunCheckpointStore(InstallCommittedSet(ShippedDeviceSets.PrestigeReadyDirectory), catalog);
            var read = store.ReadActive();
            Assert.That(read.Succeeded, Is.True, read.Message);
            Assert.That(FactoryValuation.TryCompute(catalog, read.Checkpoint.Run, read.Checkpoint.Permanent, out FactoryValueBreakdown value), Is.True);
            Assert.That(value.Total, Is.GreaterThanOrEqualTo(catalog.Prestige.ThresholdFactoryValue), "Factory Value meets the Sale threshold");
            Assert.That(read.Checkpoint.Run.Machines.Count, Is.GreaterThan(0), "a mid-run, not an empty factory");
        }

        // ------------------------------------------------------------------------------------------------------
        // The player cold start over the shipped catalog
        // ------------------------------------------------------------------------------------------------------

        // The composition of CoreGameFoundationBootstrap.InitializeRunRuntime: a fresh session over the catalog, the real file
        // store, the default reconciler, then ColdStart.
        private RunPersistence PlayerComposition(string root, RunCatalog catalog, long nowMs, out RunSession session)
        {
            var permanent = new PermanentState(catalog);
            NextRunStart start = NextRunStart.Compute(catalog, permanent);
            var state = new RunState(catalog, Guid.NewGuid(), 1, null, start.StartingCash, 0, start.StorageLevel, 1, catalog.RunStart.InitialUnlocks);
            Assert.That(new RunSimulator(catalog).Advance(state, permanent, 0, nowMs, null), Is.EqualTo(RunAdvanceResult.Ok));
            session = new RunSession(catalog, state, permanent, nowMs);
            return new RunPersistence(session, new JsonRunCheckpointStore(root, catalog), new FixedUtcClock(nowMs));
        }

        [TestCase(ShippedDeviceSets.W003SShippedDirectory)]
        [TestCase(ShippedDeviceSets.PrestigeReadyDirectory)]
        public void DeviceSet_ColdStartsOverTheShippedCatalogAndReconcilesTheCappedEightHours(string setDirectory)
        {
            RunCatalog catalog = ShippedDeviceSets.LoadShippedCatalog();
            string root = InstallCommittedSet(setDirectory);
            long nowMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            RunPersistence persistence = PlayerComposition(root, catalog, nowMs, out RunSession session);

            var result = persistence.ColdStart(null);

            Assert.That(result.IsSuccess, Is.True, setDirectory + " cold start: " + result.ReasonCode + " " + persistence.BlockedImportCode +
                                                    " diagnostics: " + Describe(_diagnostics));
            Assert.That(_diagnostics.Where(d => d.Stage.StartsWith("save.v2.", StringComparison.Ordinal)).ToList(), Is.Empty,
                "no save.v2.* diagnostic: " + Describe(_diagnostics));
            Assert.That(persistence.Lifecycle, Is.EqualTo(LifecycleState.Running));
            RunOfflineSummary summary = result.Value.OfflineSummary;
            Assert.That(summary, Is.Not.Null, "a cold start over a far-past departure returns an offline summary");
            Assert.That(summary.Outcome, Is.EqualTo(RunOfflineOutcome.Applied), "outcome " + summary.Outcome + " code " + summary.Code);
            Assert.That(summary.CapReached, Is.True, "the absence exceeds the cap");
            Assert.That(summary.CreditedTicks, Is.EqualTo(catalog.OfflinePolicy.MaxCreditedTicks), "the capped eight hours are credited");
            Assert.That(summary.CreditedTicks, Is.EqualTo(EightHoursMs * 10_000), "eight hours in ticks");
            Assert.That(summary.Completions, Is.GreaterThan(0), "the factory produced during the absence");
            Assert.That(session.State.Machines.Count, Is.EqualTo(Scenario(setDirectory).Session.State.Machines.Count));
        }

        [Test]
        public void PrestigeReady_StillOffersThePrestigeAfterTheColdStartReconcile()
        {
            RunCatalog catalog = ShippedDeviceSets.LoadShippedCatalog();
            string root = InstallCommittedSet(ShippedDeviceSets.PrestigeReadyDirectory);
            RunPersistence persistence = PlayerComposition(root, catalog, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(), out RunSession session);
            Assert.That(persistence.ColdStart(null).IsSuccess, Is.True, Describe(_diagnostics));

            var preview = new RunCommands(session).GetPrestigePreview();
            Assert.That(preview.IsSuccess, Is.True, preview.ReasonCode);
            Assert.That(preview.Value.IsEligible, Is.True, "reason " + preview.Value.Reason);
        }

        // ------------------------------------------------------------------------------------------------------
        // The old W003-S set is rejected by the shipped catalog (why T124 was blocked)
        // ------------------------------------------------------------------------------------------------------

        [Test]
        public void ShippedCatalog_RejectsTheOldW003SSetWithTheInvariantOfTheSlotCount()
        {
            RunCatalog shipped = ShippedDeviceSets.LoadShippedCatalog();
            string root = InstallCommittedSet(SaveV2DeviceSets.W003SDirectory);

            // The invariant, replayed with the load rule: the old state puts machines in slots 0 to 11 at factory level 1, and
            // the shipped factory level 1 has four slots, so RunState.TryAddMachine reports SlotUnavailable at slot 4.
            SaveV2Fixture w003s = SaveV2Scenarios.All().Single(f => f.FileName == "06-w003-s-stress.json");
            RunState replay = new RunState(shipped, Guid.NewGuid(), 1, null, 0, 0, 1, w003s.Run.FactoryLevel, w003s.Run.Unlocked);
            int firstRefusedSlot = -1;
            RunStateResult refusal = RunStateResult.Ok;
            foreach (MachineInstance machine in w003s.Run.Machines)
            {
                var instance = new MachineInstance(machine.InstanceId, shipped.Machines[machine.MachineId], machine.SlotIndex, machine.Level,
                    machine.IsActive, machine.ProgressTicks, machine.CycleDurationTicks, machine.InvestedCash);
                refusal = replay.TryAddMachine(instance);
                if (refusal != RunStateResult.Ok)
                {
                    firstRefusedSlot = machine.SlotIndex;
                    break;
                }
            }

            string invariant = "the old W003-S state has machines in slots 0 to " + w003s.Run.Machines.Max(m => m.SlotIndex) + " at factory level " +
                               w003s.Run.FactoryLevel + ", but the shipped content gives factory level 1 only " + shipped.FactoryLevels[0].SlotCount +
                               " slots (a machine's slot must be below the slot count of the factory level, RunState.TryAddMachine)";
            Assert.That(w003s.Run.FactoryLevel, Is.EqualTo(1));
            Assert.That(firstRefusedSlot, Is.EqualTo(shipped.FactoryLevels[0].SlotCount), invariant);
            Assert.That(refusal, Is.EqualTo(RunStateResult.SlotUnavailable), invariant);

            // The real load path rejects it with exactly the code the player logged.
            var read = new JsonRunCheckpointStore(root, shipped).ReadActive();
            Assert.That(read.Succeeded, Is.False, "the shipped catalog must reject the old set");
            CheckpointDiagnostic rejection = _diagnostics.FirstOrDefault(d => d.Operation == "SaveV2.Deserialize");
            Assert.That(rejection, Is.Not.Null, "a SaveV2.Deserialize diagnostic; got " + Describe(_diagnostics));
            Assert.That(rejection.Stage, Is.EqualTo(SaveV2Codes.Invariant), "violated invariant: " + invariant);

            // And the player cold start therefore blocks the return.
            RunPersistence persistence = PlayerComposition(root, shipped, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(), out _);
            var cold = persistence.ColdStart(null);
            Assert.That(cold.IsSuccess, Is.False, "cold start over the old set must not succeed; violated invariant: " + invariant);
            Assert.That(cold.ReasonCode, Is.EqualTo(RunPersistenceReasons.ReturnBlocked), "violated invariant: " + invariant);
        }
    }
}
