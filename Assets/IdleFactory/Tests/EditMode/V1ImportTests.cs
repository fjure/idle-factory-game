using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using IdleFactory.Core.Application;
using IdleFactory.Core.Numerics;
using IdleFactory.Domain;
using IdleFactory.Run.Application;
using IdleFactory.Run.Content;
using IdleFactory.Run.Domain;
using IdleFactory.Tests.EditMode.Fixtures;
using IdleFactory.Tests.EditMode.Fixtures.Run;
using IdleFactory.Unity.Persistence;
using NUnit.Framework;
using UnityEngine;

namespace IdleFactory.Tests.EditMode
{
    /// <summary>
    /// The V1 import and the cold-start source selection (T106, FR-028, SC-005, R10, contracts/save-v2-and-v1-import.md):
    /// every accepted V1 fixture imports exactly once with mapped stock, machines and proportional progress; the failure
    /// fixtures and one test per failure code leave V1 byte-identical and publish nothing (QA-1, QA-9); a V2 directory
    /// with files but no valid active checkpoint never imports (SEC-1); Start new run followed by a restart selects V2
    /// (QA-8); and an interrupted promotion retries with the same bytes, a restart selects the promoted V2, and a restart
    /// that finds only an unpromoted pending file recomputes the import with a newly captured return instant (OPS-2).
    /// V1 is only ever read through a write-denying file-system fake whose whole-directory byte hash is compared after
    /// every case.
    /// </summary>
    public sealed class V1ImportTests
    {
        private const string FixtureDirectoryRelativeToAssets = "IdleFactory/Tests/EditMode/Fixtures/SaveV1";
        private const string W001Fixture = "06-w001-scale-checkpoint.json";
        private const long Hour = 3_600_000L;

        private static readonly long DepartureMs = V1ImportFixtures.DepartureUtc.ToUnixTimeMilliseconds();
        private static readonly CheckpointId FirstId = new CheckpointId("33333333-3333-3333-3333-000000000001");

        private bool logToUnity;

        [SetUp]
        public void SetUp()
        {
            logToUnity = CheckpointDiagnostics.LogToUnity;
            CheckpointDiagnostics.LogToUnity = false;
        }

        [TearDown]
        public void TearDown()
        {
            CheckpointDiagnostics.LogToUnity = logToUnity;
        }

        // ------------------------------------------------------------------------------------------------------
        // Environment
        // ------------------------------------------------------------------------------------------------------

        private sealed class V1Source
        {
            public Dictionary<CheckpointStorageFile, byte[]> Files;
            public DefinitionCatalog Catalog;
        }

        private sealed class Env
        {
            public ScriptedBackend V2;
            public WriteDenyingV1Directory V1Dir;
            public string V1Hash;
            public DefinitionCatalog LegacyCatalog;
            public FakeLegacySource Legacy;
            public RunCommandFixtures.Rig Rig;
            public JsonRunCheckpointStore Store;
            public FixedUtcClock Clock;
            public RunPersistence Persistence;
            public Guid InitialRunId;

            public RunSession Session => Rig.Session;
            public RunCatalog Catalog => Rig.Catalog;
        }

        private static string FixturePath(string name) =>
            Path.Combine(Application.dataPath, FixtureDirectoryRelativeToAssets, name);

        private static DefinitionCatalog LegacyCatalogOf(string fixture)
        {
            switch (fixture)
            {
                case W001Fixture: return FoundationFixtures.CreateW001().Catalog;
                case V1ImportFixtures.OverCapacityFileName: return V1ImportFixtures.OverCapacityCatalog();
                case V1ImportFixtures.UnsupportedIdFileName: return V1ImportFixtures.UnsupportedIdCatalog();
                default: return AcceptedFoundationCatalogFixture.CreateCatalog();
            }
        }

        /// <summary>A V1 directory holding the committed fixture, published through the legacy two-generation protocol.</summary>
        private static V1Source V1Of(string fixture)
        {
            DefinitionCatalog catalog = LegacyCatalogOf(fixture);
            byte[] bytes = File.ReadAllBytes(FixturePath(fixture));
            var seed = new ScriptedBackend("v1-seed");
            if (fixture == V1ImportFixtures.CorruptFileName)
            {
                // A hash-valid generation whose data file does not parse: the manifest is real, the document is corrupt.
                var id = new CheckpointId("11111111-1111-1111-1111-111111110010");
                var files = new CheckpointGenerationFiles<RawV1Checkpoint>(seed, new RawV1Codec(id, 0));
                Assert.That(files.Promote(new RawV1Checkpoint(id, 0, bytes)).Succeeded, Is.True);
            }
            else
            {
                var decoded = SaveV1Mapper.Deserialize(bytes, catalog);
                Assert.That(decoded.Succeeded, Is.True, fixture);
                Assert.That(new JsonCheckpointStore("memory-root", catalog, seed).Promote(decoded.Checkpoint).Succeeded, Is.True);
            }

            return new V1Source { Files = seed.Files, Catalog = catalog };
        }

        private static V1Source V1Of(Checkpoint checkpoint, DefinitionCatalog catalog)
        {
            var seed = new ScriptedBackend("v1-seed");
            Assert.That(new JsonCheckpointStore("memory-root", catalog, seed).Promote(checkpoint).Succeeded, Is.True);
            return new V1Source { Files = seed.Files, Catalog = catalog };
        }

        private static RunCommandFixtures.Rig NewRig()
        {
            // The run content locks Bar, Plate, Gear and every machine but the Miner until a condition holds, so a mapped
            // resource or Smelter is unlocked only because the import marks it (OD-5).
            return RunCommandFixtures.NewRig(new RunCommandFixtures.Options(), 50,
                lockedResources: new[] { "iron.bar", "iron.plate", "iron.gear" }, unlockedMachines: new[] { RunCommandFixtures.Miner });
        }

        private static Env NewEnv(V1Source v1, long clockMs, ScriptedBackend v2 = null, int firstId = 1, WriteDenyingV1Directory dir = null)
        {
            RunCommandFixtures.Rig rig = NewRig();
            var backend = v2 ?? new ScriptedBackend("v2");
            var store = new JsonRunCheckpointStore("memory-root", rig.Catalog, backend);
            var clock = new FixedUtcClock(clockMs);
            var env = new Env
            {
                V2 = backend,
                Rig = rig,
                Store = store,
                Clock = clock,
                InitialRunId = rig.State.RunId,
                Persistence = new RunPersistence(rig.Session, store, clock, new SequenceCheckpointIds(firstId),
                    new OfflineRunReturnReconciler(rig.Catalog)),
            };
            if (v1 != null)
            {
                env.LegacyCatalog = v1.Catalog;
                env.V1Dir = dir ?? new WriteDenyingV1Directory(v1.Files);
                env.V1Hash = env.V1Dir.DirectoryHash();
                env.Legacy = new FakeLegacySource(new SaveV1CheckpointSource(env.V1Dir, v1.Catalog));
            }

            return env;
        }

        /// <summary>A restart: a new process (new catalog, session and persistence) over the same V2 and V1 storage.</summary>
        private static Env Restart(Env previous, long clockMs, int firstId = 100)
        {
            var env = NewEnv(null, clockMs, previous.V2, firstId);
            env.LegacyCatalog = previous.LegacyCatalog;
            env.V1Dir = previous.V1Dir;
            env.V1Hash = previous.V1Hash;
            if (previous.V1Dir != null)
                env.Legacy = new FakeLegacySource(new SaveV1CheckpointSource(previous.V1Dir, previous.LegacyCatalog));
            return env;
        }

        private static RunPersistenceOutcome Ok(RunCommandResult<RunPersistenceOutcome> result, string context = "")
        {
            Assert.That(result.IsSuccess, Is.True, result.ReasonCode + " " + context);
            return result.Value;
        }

        private static void Rejected(RunCommandResult<RunPersistenceOutcome> result, string reason)
        {
            Assert.That(result.IsSuccess, Is.False, "expected " + reason);
            Assert.That(result.ReasonCode, Is.EqualTo(reason));
        }

        private static string Dump(RunSession session) => SaveV2Scenarios.Dump(session.State, session.Permanent);

        private static string IdOf(int fixtureNumber) => "11111111-1111-1111-1111-11111111000" + fixtureNumber;

        private static long SmelterCycle(RunCatalog catalog) => catalog.Recipes[catalog.Machines[V1Import.SmelterMachineId].RecipeId].DurationTicks;

        private static long Proportional(RunCatalog catalog, long v1Progress)
        {
            Assert.That(MulDiv.MulDivFloor(v1Progress, SmelterCycle(catalog), 50_000_000L, out long mapped), Is.True);
            return mapped;
        }

        private static void AssertV1Untouched(Env env)
        {
            Assert.That(env.V1Dir.WriteAttempts, Is.Zero, "No V1 file may be opened for writing.");
            Assert.That(env.V1Dir.DirectoryHash(), Is.EqualTo(env.V1Hash), "The whole V1 directory must stay byte-identical.");
        }

        /// <summary>The import failed: a static code, nothing published, V1 untouched and only the OD-3 actions admitted.</summary>
        private static void AssertBlocked(Env env, RunCommandResult<RunPersistenceOutcome> result, string code)
        {
            Rejected(result, code);
            Assert.That(env.Persistence.BlockedImportCode, Is.EqualTo(code));
            Assert.That(env.Persistence.Lifecycle, Is.EqualTo(LifecycleState.Loading));
            Assert.That(env.Session.State.RunId, Is.EqualTo(env.InitialRunId), "A failed import must not replace the session's run.");
            Assert.That(env.Store.ReadActive().Outcome, Is.EqualTo(CommandOutcome.NoCheckpoint), "Nothing may be published.");
            AssertV1Untouched(env);
            AssertNoFileText(env);

            // The "no checkpoint" path of the return must not turn a blocked import into a fresh run, and nothing else is admitted.
            Rejected(env.Persistence.ResumeAndReconcile(), RunPersistenceReasons.LifecycleNoOp);
            Rejected(env.Persistence.SaveNow(), RunPersistenceReasons.LifecycleNoOp);
            Rejected(env.Persistence.RecordSuspension(), RunPersistenceReasons.LifecycleNoOp);
            Assert.That(env.Persistence.Lifecycle, Is.EqualTo(LifecycleState.Loading));
        }

        private static void AssertNoFileText(Env env)
        {
            string shown = env.Persistence.Status.Message;
            Assert.That(shown, Is.Not.Empty);
            foreach (string forbidden in new[] { "dev.", "1111", "2222", "unsupported", ".json", "fixture", "Checkpoints", "v1" })
                Assert.That(shown, Does.Not.Contain(forbidden), "Static copy only (SEC-5).");
        }

        private static void AssertNothingInV2(Env env)
        {
            Assert.That(env.V2.Files, Is.Empty, "The V2 directory must not hold a single file.");
        }

        // ------------------------------------------------------------------------------------------------------
        // Accepted fixtures: imported exactly once
        // ------------------------------------------------------------------------------------------------------

        [TestCase("01-empty-factory.json", 1, 0, 0L, 0L, -1L)]
        [TestCase("02-idle-placed-d001.json", 2, 1, 4L, 0L, 0L)]
        [TestCase("03-d001-mid-cycle-partial-progress.json", 3, 1, 2L, 0L, 25_000_000L)]
        [TestCase("04-d001-blocked-output-full.json", 4, 1, 4L, 1L, 0L)]
        [TestCase("05-checkpoint-departure-offline-return.json", 5, 1, 4L, 0L, 0L)]
        public void AcceptedFixture_ImportsExactlyOnce_WithMappedStockMachinesAndProportionalProgress(
            string fixture, int number, int machines, long ore, long bar, long v1Progress)
        {
            Env env = NewEnv(V1Of(fixture), DepartureMs);

            RunPersistenceOutcome outcome = Ok(env.Persistence.ColdStart(env.Legacy));

            Assert.That(outcome.Imported, Is.True);
            Assert.That(outcome.StateReplaced, Is.True);
            Assert.That(outcome.NoCheckpoint, Is.False);
            Assert.That(outcome.CheckpointId, Is.EqualTo(FirstId));
            Assert.That(outcome.Generation, Is.EqualTo(1L));
            Assert.That(env.Persistence.Lifecycle, Is.EqualTo(LifecycleState.Running));
            Assert.That(env.Persistence.BlockedImportCode, Is.Null);
            Assert.That(env.Legacy.Reads, Is.EqualTo(1), "V1 is read once.");

            RunState run = env.Session.State;
            Assert.That(run.Inventory.Units("iron.ore"), Is.EqualTo(ore));
            Assert.That(run.Inventory.Units("iron.bar"), Is.EqualTo(bar));
            Assert.That(run.Machines.Count, Is.EqualTo(machines));
            for (int i = 0; i < run.Machines.Count; i++)
            {
                MachineInstance m = run.Machines[i];
                Assert.That(m.MachineId, Is.EqualTo(V1Import.SmelterMachineId));
                Assert.That(m.Level, Is.EqualTo(1));
                Assert.That(m.InvestedCash, Is.Zero, "Placement was free in V1.");
                Assert.That(m.SlotIndex, Is.EqualTo(i));
                Assert.That(m.IsActive, Is.True);
                Assert.That(m.InstanceId, Is.EqualTo(new Guid("22222222-2222-2222-2222-222222220001")), "The instance ID is kept.");
                Assert.That(m.CycleDurationTicks, Is.EqualTo(SmelterCycle(env.Catalog)));
                Assert.That(m.ProgressTicks, Is.EqualTo(Proportional(env.Catalog, v1Progress)), "Progress keeps its fraction of the cycle.");
            }

            // OD-5: fresh-run defaults, with Ore, Bar and the Smelter marked unlocked after the initial unlocks.
            Assert.That(run.Cash, Is.EqualTo(env.Catalog.RunStart.StartingCash));
            Assert.That(run.Rp, Is.Zero);
            Assert.That(run.RunNumber, Is.EqualTo(1));
            Assert.That(run.PreviousRunId, Is.Null);
            Assert.That(run.StorageLevel, Is.EqualTo(1));
            Assert.That(run.FactoryLevel, Is.EqualTo(1));
            Assert.That(run.Unlocked.Take(4), Is.EqualTo(new[] { "iron.ore", RunCommandFixtures.Miner, "iron.bar", RunCommandFixtures.Smelter }));
            Assert.That(run.IsUnlocked("iron.plate"), Is.False, "Only the mapped IDs are opened by the import.");
            Assert.That(env.Session.Permanent.Ip, Is.Zero);
            Assert.That(env.Session.Permanent.PrestigeCount, Is.Zero);

            // The durable checkpoint: the complete candidate with the import block, written once, departure = r.
            RunCheckpoint durable = env.Store.ReadActive().Checkpoint;
            Assert.That(durable.Id, Is.EqualTo(FirstId));
            Assert.That(durable.Generation, Is.EqualTo(1L));
            Assert.That(durable.DepartureUtcMs, Is.EqualTo(DepartureMs));
            Assert.That(durable.ReconciledFromCheckpointId, Is.Null);
            Assert.That(durable.Import, Is.Not.Null);
            Assert.That(durable.Import.SourceSchemaVersion, Is.EqualTo(1));
            Assert.That(durable.Import.SourceCheckpointId, Is.EqualTo(IdOf(number)), "Canonical lowercase UUID text.");
            Assert.That(durable.Import.SourceGeneration, Is.EqualTo(0L));
            Assert.That(env.V2.Files.Keys.Where(k => k == CheckpointStorageFile.DataA || k == CheckpointStorageFile.ManifestA).Count(), Is.EqualTo(2));
            Assert.That(env.Store.ReadPrevious().Outcome, Is.EqualTo(CommandOutcome.NoCheckpoint), "One generation: published once.");

            // Settle ownership: the import settled before publication, so the install settle left the state as promoted.
            Assert.That(Dump(env.Session), Is.EqualTo(SaveV2Scenarios.Dump(durable.Run, durable.Permanent)));
            Assert.That(durable.Run.IsUnlocked(RunState.OverdriveUnlockId), Is.True, "The unlock pass ran before the bytes were written.");
            AssertV1Untouched(env);
        }

        [Test]
        public void AcceptedFixture05_WithTheFullEightHourReturn_ImportsTheReconciledStock()
        {
            // 05 is the Feature 001 offline-return fixture: 8 h later its candidate holds input 2 and output 1, progress 0.
            Env env = NewEnv(V1Of("05-checkpoint-departure-offline-return.json"), DepartureMs + 8 * Hour);

            Ok(env.Persistence.ColdStart(env.Legacy));

            Assert.That(env.Session.State.Inventory.Units("iron.ore"), Is.EqualTo(2L));
            Assert.That(env.Session.State.Inventory.Units("iron.bar"), Is.EqualTo(1L));
            Assert.That(env.Session.State.Machines.Single().ProgressTicks, Is.Zero);
            Assert.That(env.Store.ReadActive().Checkpoint.DepartureUtcMs, Is.EqualTo(DepartureMs + 8 * Hour), "The departure is the return instant r.");
            Assert.That(env.Session.TimelineUtcMs, Is.EqualTo(DepartureMs + 8 * Hour));
            AssertV1Untouched(env);
        }

        [Test]
        public void Import_AssignsSlotsAndPriorityByAnchorYThenXAndKeepsInstanceIdAndActiveState()
        {
            DefinitionCatalog catalog = AcceptedFoundationCatalogFixture.CreateCatalog();
            string basic = AcceptedFoundationCatalogFixture.BuildingId;
            Checkpoint v1 = V1ImportFixtures.CheckpointOf(
                IdOf(5), 3, catalog,
                V1ImportFixtures.Building(catalog, basic, V1ImportFixtures.InstanceId(3), 4, 2, 4, 0, 10_000_000, true),
                V1ImportFixtures.Building(catalog, basic, V1ImportFixtures.InstanceId(1), 0, 2, 2, 0, 25_000_000, false),
                V1ImportFixtures.Building(catalog, basic, V1ImportFixtures.InstanceId(2), 2, 0, 4, 1, 49_999_999, true));
            Env env = NewEnv(V1Of(v1, catalog), DepartureMs);

            Ok(env.Persistence.ColdStart(env.Legacy));

            RunState run = env.Session.State;
            Guid[] expectedOrder =
            {
                V1ImportFixtures.InstanceId(2).Uuid, // (x2, y0): the lowest anchorY
                V1ImportFixtures.InstanceId(1).Uuid, // (x0, y2): then the lowest anchorX
                V1ImportFixtures.InstanceId(3).Uuid, // (x4, y2)
            };
            Assert.That(run.Machines.Select(m => m.InstanceId), Is.EqualTo(expectedOrder));
            Assert.That(run.Machines.Select(m => m.SlotIndex), Is.EqualTo(new[] { 0, 1, 2 }));
            Assert.That(run.Priority.ToArray(), Is.EqualTo(expectedOrder), "Priority follows the slot order.");
            Assert.That(run.Machines.Select(m => m.IsActive), Is.EqualTo(new[] { true, false, true }));
            Assert.That(run.Machines.Select(m => m.ProgressTicks), Is.EqualTo(new[]
            {
                Proportional(env.Catalog, 49_999_999), Proportional(env.Catalog, 25_000_000), Proportional(env.Catalog, 10_000_000),
            }));
            Assert.That(run.Machines.All(m => m.Level == 1 && m.InvestedCash == 0), Is.True);
            Assert.That(run.Inventory.Units("iron.ore"), Is.EqualTo(10L), "Ore is the sum of every dev.input buffer.");
            Assert.That(run.Inventory.Units("iron.bar"), Is.EqualTo(1L), "Bar is the sum of every dev.output buffer.");
            AssertV1Untouched(env);
        }

        [Test]
        public void Import_NeverCreditsTheV1IntervalTwice_ARestartSelectsThePromotedV2()
        {
            Env first = NewEnv(V1Of("05-checkpoint-departure-offline-return.json"), DepartureMs + 8 * Hour);
            Ok(first.Persistence.ColdStart(first.Legacy));
            string imported = Dump(first.Session);
            CheckpointId importedId = first.Store.ReadActive().Checkpoint.Id;

            // The restart at the same instant: V2 is selected by rule 1, V1 is not even read, nothing is credited again.
            Env restarted = Restart(first, DepartureMs + 8 * Hour);
            RunPersistenceOutcome outcome = Ok(restarted.Persistence.ColdStart(restarted.Legacy));

            Assert.That(outcome.Imported, Is.False);
            Assert.That(restarted.Legacy.Reads, Is.Zero, "A V2 active checkpoint wins before V1 is consulted.");
            Assert.That(restarted.Persistence.Lifecycle, Is.EqualTo(LifecycleState.Running));
            Assert.That(Dump(restarted.Session), Is.EqualTo(imported), "The import credited the interval once; the return added nothing.");
            RunCheckpoint returned = restarted.Store.ReadActive().Checkpoint;
            Assert.That(returned.ReconciledFromCheckpointId, Is.EqualTo(importedId), "The V2 return is linked to the import generation.");
            Assert.That(returned.Import, Is.Null);
            Assert.That(returned.Generation, Is.EqualTo(2L));

            // A second restart a day later: only the V2 return from r applies; the V1 departure is never used again.
            Env later = Restart(restarted, DepartureMs + 32 * Hour, 200);
            Ok(later.Persistence.ColdStart(later.Legacy));
            Assert.That(later.Legacy.Reads, Is.Zero);
            Assert.That(later.Store.ReadActive().Checkpoint.DepartureUtcMs, Is.EqualTo(DepartureMs + 32 * Hour));
            AssertV1Untouched(later);
        }

        [Test]
        public void Import_SettlesOnceBeforePublication_TheInstallSettleIsANoOp()
        {
            Env env = NewEnv(V1Of("03-d001-mid-cycle-partial-progress.json"), DepartureMs);

            Ok(env.Persistence.ColdStart(env.Legacy));

            // The promoted bytes already carry the first-settle unlocks (always-unlocked content), i.e. V1Import settled
            // the candidate itself. The session then installed a fresh read and its own settle changed nothing.
            RunCheckpoint durable = env.Store.ReadActive().Checkpoint;
            Assert.That(durable.Run.IsUnlocked(RunState.OverdriveUnlockId), Is.True);
            Assert.That(Dump(env.Session), Is.EqualTo(SaveV2Scenarios.Dump(durable.Run, durable.Permanent)));
            long quantityRevision = env.Session.QuantityRevision;
            string before = Dump(env.Session);
            env.Session.BeginCommand();
            Assert.That(env.Session.EndCommand(true, false).IsSuccess, Is.True);
            Assert.That(Dump(env.Session), Is.EqualTo(before), "The state is a fixed point of the settle.");
            Assert.That(env.Session.QuantityRevision, Is.EqualTo(quantityRevision));
        }

        // ------------------------------------------------------------------------------------------------------
        // Failure fixtures and codes (QA-1, QA-9)
        // ------------------------------------------------------------------------------------------------------

        [Test]
        public void W001ScaleFixture_ExceedsTheLevelOneSlots_AndFailsWithSlotsExceeded()
        {
            V1Source source = V1Of(W001Fixture);
            Env env = NewEnv(source, DepartureMs);
            int machines = SaveV1Mapper.Deserialize(File.ReadAllBytes(FixturePath(W001Fixture)), source.Catalog)
                .Checkpoint.CreateFactoryCopy().Buildings.Count;
            Assert.That(machines, Is.GreaterThan(env.Catalog.FactoryLevels[0].SlotCount), "QA-9: the fixture really is over the slot limit.");

            AssertBlocked(env, env.Persistence.ColdStart(env.Legacy), V1ImportCodes.SlotsExceeded);
            AssertNothingInV2(env);
        }

        [TestCase(V1ImportFixtures.OverCapacityFileName, V1ImportCodes.CapacityExceeded)]
        [TestCase(V1ImportFixtures.OverSlotFileName, V1ImportCodes.SlotsExceeded)]
        [TestCase(V1ImportFixtures.UnsupportedIdFileName, V1ImportCodes.IdUnsupported)]
        [TestCase(V1ImportFixtures.CorruptFileName, V1ImportCodes.Invalid)]
        public void FailureFixture_FailsWithItsRuleCode_LeavesV1ByteIdenticalAndPublishesNothing(string fixture, string code)
        {
            Env env = NewEnv(V1Of(fixture), DepartureMs);

            AssertBlocked(env, env.Persistence.ColdStart(env.Legacy), code);
            AssertNothingInV2(env);
            Assert.That(env.Legacy.Reads, Is.EqualTo(1));
        }

        [Test]
        public void CorruptFixture_IsReallyRejectedByTheUnchangedSaveV1Mapper()
        {
            var result = SaveV1Mapper.Deserialize(File.ReadAllBytes(FixturePath(V1ImportFixtures.CorruptFileName)),
                AcceptedFoundationCatalogFixture.CreateCatalog());
            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.Outcome, Is.EqualTo(CommandOutcome.InvalidCheckpoint));
        }

        [Test]
        public void UnreadableV1Save_IsAnInvalidImport_NeverASilentFreshRun()
        {
            Env env = NewEnv(null, DepartureMs);
            env.V1Dir = new WriteDenyingV1Directory(new Dictionary<CheckpointStorageFile, byte[]>());
            env.V1Hash = env.V1Dir.DirectoryHash();
            var legacy = new FakeLegacySource(null, CommandOutcome.PersistenceFailed);

            AssertBlocked(env, env.Persistence.ColdStart(legacy), V1ImportCodes.Invalid);
            AssertNothingInV2(env);
        }

        [Test]
        public void ClockFailure_FailsWithClockCode_AndRetryRecomputesWithTheNewInstant()
        {
            Env env = NewEnv(V1Of("02-idle-placed-d001.json"), DepartureMs);
            env.Clock.Fail = true;

            AssertBlocked(env, env.Persistence.ColdStart(env.Legacy), V1ImportCodes.Clock);
            AssertNothingInV2(env);

            // An unrepresentable instant (below zero, above the exact bound) is the same code, and the retry is allowed.
            env.Clock.Fail = false;
            env.Clock.UnixMs = -1;
            AssertBlocked(env, env.Persistence.ColdStart(env.Legacy), V1ImportCodes.Clock);
            env.Clock.UnixMs = ExactBound.Value + 1;
            AssertBlocked(env, env.Persistence.ColdStart(env.Legacy), V1ImportCodes.Clock);

            env.Clock.UnixMs = DepartureMs + Hour;
            RunPersistenceOutcome outcome = Ok(env.Persistence.ColdStart(env.Legacy));
            Assert.That(outcome.Imported, Is.True);
            Assert.That(env.Store.ReadActive().Checkpoint.DepartureUtcMs, Is.EqualTo(DepartureMs + Hour), "A newly captured r.");
            Assert.That(env.Legacy.Reads, Is.EqualTo(4), "Nothing was published, so each retry recomputes from V1.");
            AssertV1Untouched(env);
        }

        [Test]
        public void ArithmeticOverflow_ThroughThePortFake_FailsWithArithmeticCode()
        {
            // A legacy port that hands over two buffers of 10^18 units: their sum exceeds the exact bound.
            DefinitionCatalog huge = V1ImportFixtures.Catalog(AcceptedFoundationCatalogFixture.BuildingId, ExactBound.Value, 1);
            string basic = AcceptedFoundationCatalogFixture.BuildingId;
            Checkpoint v1 = V1ImportFixtures.CheckpointOf(
                IdOf(5), 0, huge,
                V1ImportFixtures.Building(huge, basic, V1ImportFixtures.InstanceId(1), 0, 0, ExactBound.Value, 0, 0, false),
                V1ImportFixtures.Building(huge, basic, V1ImportFixtures.InstanceId(2), 2, 0, ExactBound.Value, 0, 0, false));
            Env env = NewEnv(null, DepartureMs);
            env.V1Dir = new WriteDenyingV1Directory(new Dictionary<CheckpointStorageFile, byte[]>());
            env.V1Hash = env.V1Dir.DirectoryHash();
            var legacy = new FakeLegacySource(v1);

            AssertBlocked(env, env.Persistence.ColdStart(legacy), V1ImportCodes.Arithmetic);
            AssertNothingInV2(env);
            Assert.That(legacy.Reads, Is.EqualTo(1));
        }

        [Test]
        public void PersistenceFailure_FailsWithPersistenceCode_AndPublishesNothing()
        {
            Env env = NewEnv(V1Of("02-idle-placed-d001.json"), DepartureMs);
            env.V2.FailOnFirst("Create", CheckpointStorageFile.Pending);

            AssertBlocked(env, env.Persistence.ColdStart(env.Legacy), V1ImportCodes.Persistence);
            AssertNothingInV2(env);
        }

        [Test]
        public void ImportCodes_AreTheSevenStaticCodesOfTheContract()
        {
            Assert.That(V1ImportCodes.All, Is.EqualTo(new[]
            {
                "import.v1.invalid", "import.v1.clock", "import.id.unsupported", "import.arithmetic", "import.slots.exceeded",
                "import.capacity.exceeded", "import.persistence",
            }));
            foreach (string code in V1ImportCodes.All) Assert.That(code, Does.Match("^import(\\.[a-z0-9]+)+$"));
        }

        // ------------------------------------------------------------------------------------------------------
        // Cold-start source selection
        // ------------------------------------------------------------------------------------------------------

        [Test]
        public void ColdStart_WithoutAnySaveStartsAFreshRun_AndWithAnAbsentV1AsWell()
        {
            Env env = NewEnv(null, DepartureMs);
            var absent = new FakeLegacySource(null, CommandOutcome.NoCheckpoint);

            RunPersistenceOutcome outcome = Ok(env.Persistence.ColdStart(absent));

            Assert.That(outcome.Imported, Is.False);
            Assert.That(outcome.NoCheckpoint, Is.True);
            Assert.That(env.Persistence.Lifecycle, Is.EqualTo(LifecycleState.Running));
            Assert.That(env.Session.State.RunId, Is.EqualTo(env.InitialRunId), "The fresh run the session already holds is kept.");
            Assert.That(absent.Reads, Is.EqualTo(1));
            AssertNothingInV2(env);
        }

        [Test]
        public void ColdStart_WithAValidV2ActiveCheckpoint_NeverReadsV1()
        {
            Env first = NewEnv(null, DepartureMs);
            Ok(first.Persistence.ColdStart(null));
            Ok(first.Persistence.SaveNow());

            // V1 exists as well; the V2 checkpoint of the fresh run wins and V1 is not consulted at all.
            Env second = NewEnv(V1Of("02-idle-placed-d001.json"), DepartureMs, first.V2, 100);
            RunPersistenceOutcome outcome = Ok(second.Persistence.ColdStart(second.Legacy));

            Assert.That(outcome.Imported, Is.False);
            Assert.That(outcome.StateReplaced, Is.True, "The V2 checkpoint was loaded.");
            Assert.That(second.Legacy.Reads, Is.Zero);
            Assert.That(second.Session.State.RunId, Is.EqualTo(first.InitialRunId));
            Assert.That(second.Session.State.Machines, Is.Empty);
            AssertV1Untouched(second);
        }

        [Test]
        public void ColdStart_WhenRunning_IsRejected()
        {
            Env env = NewEnv(V1Of("02-idle-placed-d001.json"), DepartureMs);
            Ok(env.Persistence.ColdStart(env.Legacy));

            Rejected(env.Persistence.ColdStart(env.Legacy), RunPersistenceReasons.LifecycleNoOp);
            Rejected(env.Persistence.StartNewRunAfterBlockedImport(), RunPersistenceReasons.LifecycleNoOp);
            Assert.That(env.Legacy.Reads, Is.EqualTo(1));
        }

        [Test]
        public void CorruptNewestManifest_WithAValidV1_RecoversThePreviousGeneration_AndNeverImports()
        {
            Env first = NewEnv(V1Of("05-checkpoint-departure-offline-return.json"), DepartureMs + 8 * Hour);
            Ok(first.Persistence.ColdStart(first.Legacy));
            string imported = Dump(first.Session);
            Ok(first.Persistence.SaveNow());
            first.V2.Files[CheckpointStorageFile.ManifestB] = Flip(first.V2.Files[CheckpointStorageFile.ManifestB]);

            Env restarted = Restart(first, DepartureMs + 8 * Hour);
            RunPersistenceOutcome outcome = Ok(restarted.Persistence.ColdStart(restarted.Legacy));

            Assert.That(outcome.Imported, Is.False, "SEC-1: damaged V2 files never fall through to V1.");
            Assert.That(restarted.Legacy.Reads, Is.Zero);
            Assert.That(Dump(restarted.Session), Is.EqualTo(imported), "The previous generation (the import) is the recovered state.");
            Assert.That(restarted.Persistence.Lifecycle, Is.EqualTo(LifecycleState.Running));
            AssertV1Untouched(restarted);
        }

        [Test]
        public void V2FilesWithoutAValidActiveCheckpoint_TakeTheRecoveryPath_AndNeverImport()
        {
            Env first = NewEnv(V1Of("02-idle-placed-d001.json"), DepartureMs);
            Ok(first.Persistence.ColdStart(first.Legacy));
            first.V2.Files[CheckpointStorageFile.ManifestA] = Flip(first.V2.Files[CheckpointStorageFile.ManifestA]);
            var before = first.V2.Snapshot();

            Env restarted = Restart(first, DepartureMs + Hour);
            var result = restarted.Persistence.ColdStart(restarted.Legacy);

            Rejected(result, RunPersistenceReasons.ReturnBlocked);
            Assert.That(restarted.Persistence.Lifecycle, Is.EqualTo(LifecycleState.ReturnBlocked), "The Feature 001 blocked state with its actions.");
            Assert.That(restarted.Persistence.BlockedImportCode, Is.Null);
            Assert.That(restarted.Legacy.Reads, Is.Zero, "V1 is never consulted (SEC-1).");
            Assert.That(restarted.V2.Snapshot().Keys, Is.EquivalentTo(before.Keys));
            foreach (var entry in before) Assert.That(restarted.V2.Files[entry.Key], Is.EqualTo(entry.Value), "Nothing was written.");
            AssertV1Untouched(restarted);
        }

        [Test]
        public void StoreThatCannotBeRead_TakesTheRecoveryPath_AndNeverImports()
        {
            Env env = NewEnv(V1Of("02-idle-placed-d001.json"), DepartureMs);
            env.V2.FailAll = true;

            var result = env.Persistence.ColdStart(env.Legacy);

            Assert.That(result.IsSuccess, Is.False);
            Assert.That(env.Legacy.Reads, Is.Zero);
            Assert.That(env.Persistence.BlockedImportCode, Is.Null);
            env.V2.FailAll = false;
            AssertV1Untouched(env);
        }

        // ------------------------------------------------------------------------------------------------------
        // Start new run (OD-3, QA-8)
        // ------------------------------------------------------------------------------------------------------

        [Test]
        public void StartNewRun_AfterABlockedImport_PromotesAFreshRun_AndARestartSelectsV2()
        {
            Env env = NewEnv(V1Of(V1ImportFixtures.OverSlotFileName), DepartureMs);
            AssertBlocked(env, env.Persistence.ColdStart(env.Legacy), V1ImportCodes.SlotsExceeded);

            RunPersistenceOutcome started = Ok(env.Persistence.StartNewRunAfterBlockedImport());

            Assert.That(started.Imported, Is.False);
            Assert.That(started.StateReplaced, Is.True);
            Assert.That(env.Persistence.Lifecycle, Is.EqualTo(LifecycleState.Running));
            Assert.That(env.Persistence.BlockedImportCode, Is.Null);
            RunCheckpoint durable = env.Store.ReadActive().Checkpoint;
            Assert.That(durable.Import, Is.Null);
            Assert.That(durable.Generation, Is.EqualTo(1L));
            Assert.That(durable.DepartureUtcMs, Is.EqualTo(DepartureMs));
            Assert.That(durable.Run.RunId, Is.EqualTo(env.Session.State.RunId));
            Assert.That(env.Session.State.RunId, Is.Not.EqualTo(env.InitialRunId), "A new run identity.");
            Assert.That(env.Session.State.RunNumber, Is.EqualTo(1));
            Assert.That(env.Session.State.Machines, Is.Empty);
            Assert.That(env.Session.State.Cash, Is.EqualTo(env.Catalog.RunStart.StartingCash));
            Assert.That(Dump(env.Session), Is.EqualTo(SaveV2Scenarios.Dump(durable.Run, durable.Permanent)), "The new run was settled before promotion.");
            AssertV1Untouched(env);

            // QA-8: the next start takes rule 1; V1 is not offered again and not even read.
            Env restarted = Restart(env, DepartureMs);
            RunPersistenceOutcome outcome = Ok(restarted.Persistence.ColdStart(restarted.Legacy));
            Assert.That(outcome.Imported, Is.False);
            Assert.That(restarted.Legacy.Reads, Is.Zero);
            Assert.That(restarted.Session.State.RunId, Is.EqualTo(durable.Run.RunId));
            AssertV1Untouched(restarted);
        }

        [Test]
        public void StartNewRun_WhenPromotionFails_KeepsTheBlockedState_AndTheRetryPromotesTheSameBytes()
        {
            Env env = NewEnv(V1Of(V1ImportFixtures.UnsupportedIdFileName), DepartureMs);
            AssertBlocked(env, env.Persistence.ColdStart(env.Legacy), V1ImportCodes.IdUnsupported);
            env.V2.FailOnFirst("Create", CheckpointStorageFile.Pending);

            Rejected(env.Persistence.StartNewRunAfterBlockedImport(), RunPersistenceReasons.PersistenceFailed);
            Assert.That(env.Persistence.BlockedImportCode, Is.EqualTo(V1ImportCodes.IdUnsupported));
            Assert.That(env.Persistence.Lifecycle, Is.EqualTo(LifecycleState.Loading));
            Assert.That(env.Session.State.RunId, Is.EqualTo(env.InitialRunId));
            Assert.That(env.Store.ReadActive().Outcome, Is.EqualTo(CommandOutcome.NoCheckpoint));

            env.V2.ClearFault();
            env.Clock.UnixMs = DepartureMs + 5 * Hour;
            Ok(env.Persistence.StartNewRunAfterBlockedImport());
            Assert.That(env.Store.ReadActive().Checkpoint.DepartureUtcMs, Is.EqualTo(DepartureMs), "The same candidate: its captured instant is kept.");
            Assert.That(env.Persistence.Lifecycle, Is.EqualTo(LifecycleState.Running));
            AssertV1Untouched(env);
        }

        [Test]
        public void StartNewRun_WithoutABlockedImport_IsRejected()
        {
            Env env = NewEnv(V1Of("02-idle-placed-d001.json"), DepartureMs);
            Rejected(env.Persistence.StartNewRunAfterBlockedImport(), RunPersistenceReasons.LifecycleNoOp);
            AssertNothingInV2(env);
        }

        [Test]
        public void RetryAfterABlockedImport_ImportsOnceTheCauseIsGone_AndStartNewRunIsThenGone()
        {
            Env env = NewEnv(V1Of("03-d001-mid-cycle-partial-progress.json"), DepartureMs);
            env.Clock.Fail = true;
            AssertBlocked(env, env.Persistence.ColdStart(env.Legacy), V1ImportCodes.Clock);
            env.Clock.Fail = false;

            Ok(env.Persistence.ColdStart(env.Legacy));

            Assert.That(env.Persistence.Lifecycle, Is.EqualTo(LifecycleState.Running));
            Rejected(env.Persistence.StartNewRunAfterBlockedImport(), RunPersistenceReasons.LifecycleNoOp);
        }

        // ------------------------------------------------------------------------------------------------------
        // Exactly once across retries and restarts (OPS-2)
        // ------------------------------------------------------------------------------------------------------

        [TestCase("Create", CheckpointStorageFile.Pending)]
        [TestCase("Write", CheckpointStorageFile.Pending)]
        [TestCase("Create", CheckpointStorageFile.DataA)]
        [TestCase("Create", CheckpointStorageFile.ManifestA)]
        public void InterruptedPromotion_RetryInTheSameSession_RePromotesTheSameCandidateBytes(string operation, CheckpointStorageFile file)
        {
            long r = DepartureMs + 8 * Hour;
            Env env = NewEnv(V1Of("05-checkpoint-departure-offline-return.json"), r);
            env.V2.FailOnFirst(operation, file);

            Rejected(env.Persistence.ColdStart(env.Legacy), V1ImportCodes.Persistence);

            Assert.That(env.V2.FaultTriggered, Is.True);
            Assert.That(env.Persistence.BlockedImportCode, Is.EqualTo(V1ImportCodes.Persistence));
            Assert.That(env.Persistence.Lifecycle, Is.EqualTo(LifecycleState.Loading));
            Assert.That(env.Store.ReadActive().Outcome, Is.EqualTo(CommandOutcome.NoCheckpoint), "Nothing was published.");
            Assert.That(env.Session.State.RunId, Is.EqualTo(env.InitialRunId));
            AssertV1Untouched(env);

            // The retry, a long while later: same bytes, same checkpoint ID, same r. V1 is not read again.
            env.V2.ClearFault();
            env.Clock.UnixMs = r + 100 * Hour;
            RunPersistenceOutcome outcome = Ok(env.Persistence.ColdStart(env.Legacy));

            Assert.That(outcome.Imported, Is.True);
            Assert.That(env.Legacy.Reads, Is.EqualTo(1), "The candidate was built once.");
            RunCheckpoint durable = env.Store.ReadActive().Checkpoint;
            Assert.That(durable.Id, Is.EqualTo(FirstId));
            Assert.That(durable.Generation, Is.EqualTo(1L));
            Assert.That(durable.DepartureUtcMs, Is.EqualTo(r), "The departure is the r captured once.");
            Assert.That(env.Session.TimelineUtcMs, Is.EqualTo(r));
            Assert.That(env.Session.State.Inventory.Units("iron.ore"), Is.EqualTo(2L));
            Assert.That(env.Session.State.Inventory.Units("iron.bar"), Is.EqualTo(1L), "The V1 interval was credited once.");
            Assert.That(env.Persistence.Lifecycle, Is.EqualTo(LifecycleState.Running));
            AssertV1Untouched(env);
        }

        [Test]
        public void RestartWithAPromotedV2_SelectsV2_AndTheV1IntervalIsNeverCreditedTwice()
        {
            // The promotion succeeded and the process ended before anything else happened.
            long r = DepartureMs + 8 * Hour;
            Env first = NewEnv(V1Of("05-checkpoint-departure-offline-return.json"), r);
            Ok(first.Persistence.ColdStart(first.Legacy));
            string imported = Dump(first.Session);

            Env restarted = Restart(first, r);
            RunPersistenceOutcome outcome = Ok(restarted.Persistence.ColdStart(restarted.Legacy));

            Assert.That(outcome.Imported, Is.False);
            Assert.That(restarted.Legacy.Reads, Is.Zero);
            Assert.That(Dump(restarted.Session), Is.EqualTo(imported));
            AssertV1Untouched(restarted);
        }

        [Test]
        public void RestartWithOnlyAnUnpromotedPendingFile_RecomputesTheImportWithANewlyCapturedInstant()
        {
            long r1 = DepartureMs + Hour;
            long r2 = DepartureMs + 10 * Hour;
            Env first = NewEnv(V1Of("02-idle-placed-d001.json"), r1);
            first.V2.FailOnFirst("Create", CheckpointStorageFile.DataA);
            Rejected(first.Persistence.ColdStart(first.Legacy), V1ImportCodes.Persistence);
            Assert.That(first.V2.Files.Keys, Is.EquivalentTo(new[] { CheckpointStorageFile.Pending }), "Only the unpromoted pending file exists.");
            Assert.That(first.Store.ProbeFiles().Value, Is.EqualTo(RunCheckpointPresence.PendingOnly));
            first.V2.ClearFault();

            Env restarted = Restart(first, r2);
            RunPersistenceOutcome outcome = Ok(restarted.Persistence.ColdStart(restarted.Legacy));

            Assert.That(outcome.Imported, Is.True, "A lone pending file counts as no checkpoint file (OPS-2).");
            Assert.That(restarted.Legacy.Reads, Is.EqualTo(1));
            RunCheckpoint durable = restarted.Store.ReadActive().Checkpoint;
            Assert.That(durable.DepartureUtcMs, Is.EqualTo(r2), "A newly captured return instant.");
            Assert.That(durable.Generation, Is.EqualTo(1L));
            Assert.That(durable.Import.SourceCheckpointId, Is.EqualTo(IdOf(2)));
            Assert.That(restarted.Session.State.Inventory.Units("iron.ore"), Is.EqualTo(2L));
            Assert.That(restarted.Session.State.Inventory.Units("iron.bar"), Is.EqualTo(1L), "Credited once: nothing had been published before.");
            AssertV1Untouched(restarted);
        }

        [Test]
        public void RestartWithOnlyAPendingFile_AndNoV1_StartsAFreshRun()
        {
            Env first = NewEnv(null, DepartureMs);
            first.V2.Files[CheckpointStorageFile.Pending] = new byte[] { 1, 2, 3 };

            Env restarted = NewEnv(null, DepartureMs, first.V2, 100);
            RunPersistenceOutcome outcome = Ok(restarted.Persistence.ColdStart(new FakeLegacySource(null, CommandOutcome.NoCheckpoint)));

            Assert.That(outcome.NoCheckpoint, Is.True);
            Assert.That(restarted.Persistence.Lifecycle, Is.EqualTo(LifecycleState.Running));
        }

        [Test]
        public void InterruptedBetweenDataAndManifest_ARestartNeverImports()
        {
            // The data slot was written but the manifest never was: V2 holds a data file, so this is the recovery path (SEC-1).
            Env first = NewEnv(V1Of("02-idle-placed-d001.json"), DepartureMs);
            first.V2.FailOnFirst("Create", CheckpointStorageFile.ManifestA);
            Rejected(first.Persistence.ColdStart(first.Legacy), V1ImportCodes.Persistence);
            Assert.That(first.V2.Files.ContainsKey(CheckpointStorageFile.DataA), Is.True);
            Assert.That(first.V2.Files.ContainsKey(CheckpointStorageFile.ManifestA), Is.False);
            first.V2.ClearFault();

            Env restarted = Restart(first, DepartureMs + Hour);
            Rejected(restarted.Persistence.ColdStart(restarted.Legacy), RunPersistenceReasons.ReturnBlocked);

            Assert.That(restarted.Legacy.Reads, Is.Zero);
            Assert.That(restarted.Persistence.BlockedImportCode, Is.Null);
            AssertV1Untouched(restarted);
        }

        private static byte[] Flip(byte[] bytes)
        {
            var copy = (byte[])bytes.Clone();
            copy[copy.Length / 2] ^= 0x5A;
            return copy;
        }
    }
}
