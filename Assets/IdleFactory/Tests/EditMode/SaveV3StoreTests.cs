using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using IdleFactory.Core.Application;
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
    /// Save V3 at the store (T335, issue #400): the V2 regression over every existing Save V2 fixture (the committed top-level files, the
    /// device sets W003-S, W003-S shipped and prestige-ready, and the outputs of the V1 import written as V2), the fault injection of a failed
    /// first V3 write after a V2 load, and the forward-compatibility refusal of <c>schemaVersion</c> 4 without any write and without a V1
    /// import (SEC-14).
    /// </summary>
    public sealed class SaveV3StoreTests
    {
        private const string Memory = "memory-root";
        private const string SaveV2FixtureDirectory = "IdleFactory/Tests/EditMode/Fixtures/SaveV2";
        private const string SaveV1FixtureDirectory = "IdleFactory/Tests/EditMode/Fixtures/SaveV1";

        private bool _logToUnity;
        private string _root;

        [SetUp]
        public void SetUp()
        {
            _logToUnity = CheckpointDiagnostics.LogToUnity;
            CheckpointDiagnostics.LogToUnity = false;
            _root = RunCheckpointTestSupport.NewTempRoot();
        }

        [TearDown]
        public void TearDown()
        {
            CheckpointDiagnostics.LogToUnity = _logToUnity;
            RunCheckpointTestSupport.DeleteQuietly(_root);
        }

        private static string Text(byte[] bytes) => new UTF8Encoding(false, true).GetString(bytes);

        private static byte[] Committed(string relative) => File.ReadAllBytes(Path.Combine(Application.dataPath, SaveV2FixtureDirectory, relative));

        /// <summary>The V3 defaults of save-v3.md on a board loaded from a V2 file, and nothing dropped.</summary>
        private static void AssertV2Defaults(RunCheckpoint checkpoint, string context)
        {
            ContractBoard board = checkpoint.Run.Contracts;
            if (board.Active != null)
            {
                Assert.That(board.Active.AutoDeliver, Is.False, context + ": autoDeliver");
                Assert.That(board.Active.Produced, Is.Zero, context + ": produced");
            }

            Assert.That(board.CooldownStamps(), Is.Empty, context + ": cooldowns");
            Assert.That(board.BonusOffers(), Is.Empty, context + ": bonus state");
            Assert.That(board.RunCompletionTotal, Is.EqualTo(board.CompletionCounts().Sum(c => c.Count)), context + ": runCompletionTotal");
            Assert.That(checkpoint.DroppedContractEntries, Is.Zero, context + ": nothing dropped");
        }

        /// <summary>
        /// Equivalence of a V2 load: the state written as V3 reads back to the same state, and that V3 state still writes the original V2
        /// bytes (nothing was lost or invented on the way).
        /// </summary>
        private static void AssertLosslessUpgrade(RunCatalog catalog, RunCheckpoint loaded, byte[] v2Bytes, string context)
        {
            var player = new SaveV2CheckpointCodec(catalog);
            CommandResult<byte[]> v3 = player.Serialize(loaded);
            Assert.That(v3.Succeeded, Is.True, context + ": " + v3.Message);
            Assert.That(Text(v3.Value), Does.StartWith("{\"schemaVersion\":3,"), context);
            CheckpointDecodeResult<RunCheckpoint> upgraded = player.Deserialize(v3.Value);
            Assert.That(upgraded.Succeeded, Is.True, context + ": " + upgraded.Message);
            Assert.That(SaveV2Scenarios.Dump(upgraded.Checkpoint.Run, upgraded.Checkpoint.Permanent),
                Is.EqualTo(SaveV2Scenarios.Dump(loaded.Run, loaded.Permanent)), context + ": equivalent state");
            CommandResult<byte[]> back = new SaveV2CheckpointCodec(catalog, SaveV2Mapper.LegacySchemaVersion).Serialize(upgraded.Checkpoint);
            Assert.That(back.Succeeded, Is.True, context + ": " + back.Message);
            Assert.That(back.Value, Is.EqualTo(v2Bytes), context + ": the V3 state is the V2 state");
            Assert.That(player.ValidateBytes(loaded, v2Bytes), Is.True, context + ": ValidateBytes compares against the version it read");
        }

        // ------------------------------------------------------------------------------------------------------
        // V2 regression (QA-11 row v2/*)
        // ------------------------------------------------------------------------------------------------------

        [Test]
        public void EveryCommittedTopLevelV2Fixture_LoadsUnderTheV3Reader_WithTheV3Defaults_AndAnEquivalentState()
        {
            int count = 0;
            foreach (SaveV2Fixture fixture in SaveV2Scenarios.All().Where(f => !f.IsCorrupt))
            {
                byte[] bytes = Committed(fixture.FileName);
                Assert.That(Text(bytes), Does.StartWith("{\"schemaVersion\":2,"), fixture.FileName);
                SaveV2ReadResult read = SaveV2Mapper.Deserialize(bytes, fixture.Catalog);
                Assert.That(read.IsSuccess, Is.True, fixture.FileName + " " + read.Code);
                Assert.That(read.Document.SchemaVersion, Is.EqualTo(SaveV2Mapper.LegacySchemaVersion), fixture.FileName);

                CheckpointDecodeResult<RunCheckpoint> decoded = new SaveV2CheckpointCodec(fixture.Catalog).Deserialize(bytes);
                Assert.That(decoded.Succeeded, Is.True, fixture.FileName + " " + decoded.Message);
                AssertV2Defaults(decoded.Checkpoint, fixture.FileName);
                Assert.That(SaveV2Scenarios.Dump(decoded.Checkpoint.Run, decoded.Checkpoint.Permanent),
                    Is.EqualTo(SaveV2Scenarios.Dump(fixture.Run, fixture.Permanent)), fixture.FileName);
                AssertLosslessUpgrade(fixture.Catalog, decoded.Checkpoint, bytes, fixture.FileName);
                count++;
            }

            Assert.That(count, Is.EqualTo(7), "01 to 07: fresh, mid-run, pending prestige, post-prestige and the three W003 workloads");
        }

        [Test]
        public void EveryCommittedCorruptV2Fixture_StaysRejectedWithItsCode()
        {
            foreach (SaveV2Fixture fixture in SaveV2Scenarios.All().Where(f => f.IsCorrupt))
            {
                SaveV2ReadResult read = SaveV2Mapper.Deserialize(Committed(fixture.FileName), fixture.Catalog);
                Assert.That(read.IsSuccess, Is.False, fixture.FileName);
                Assert.That(read.Code, Is.EqualTo(fixture.ExpectedCode), fixture.FileName);
            }
        }

        [Test]
        public void EveryCommittedDeviceSet_LoadsThroughTheStoreAsV2_WithTheV3Defaults()
        {
            RunCatalog w003 = SaveV2Scenarios.All().Single(f => f.FileName == "06-w003-s-stress.json").Catalog;
            RunCatalog shipped = ShippedDeviceSets.LoadShippedCatalog();
            var sets = new (string Directory, RunCatalog Catalog)[]
            {
                (SaveV2DeviceSets.W003SDirectory, w003),
                (ShippedDeviceSets.W003SShippedDirectory, shipped),
                (ShippedDeviceSets.PrestigeReadyDirectory, shipped),
            };

            foreach (var set in sets)
            {
                string root = RunCheckpointTestSupport.NewTempRoot();
                try
                {
                    string directory = RunCheckpointTestSupport.V2Directory(root);
                    Directory.CreateDirectory(directory);
                    foreach (string name in SaveV2DeviceSets.W003SFiles)
                    {
                        string source = Path.Combine(Application.dataPath, SaveV2FixtureDirectory, set.Directory, name);
                        if (File.Exists(source)) File.Copy(source, Path.Combine(directory, name));
                    }

                    byte[] data = File.ReadAllBytes(Path.Combine(directory, SaveV2CheckpointFiles.DataAFileName));
                    Assert.That(Text(data), Does.StartWith("{\"schemaVersion\":2,"), set.Directory);
                    RunCheckpointReadResult read = new JsonRunCheckpointStore(root, set.Catalog).ReadActive();
                    Assert.That(read.Succeeded, Is.True, set.Directory + " " + read.Message);
                    AssertV2Defaults(read.Checkpoint, set.Directory);
                    AssertLosslessUpgrade(set.Catalog, read.Checkpoint, data, set.Directory);
                }
                finally
                {
                    RunCheckpointTestSupport.DeleteQuietly(root);
                }
            }
        }

        [TestCase("01-empty-factory.json")]
        [TestCase("02-idle-placed-d001.json")]
        [TestCase("03-d001-mid-cycle-partial-progress.json")]
        [TestCase("04-d001-blocked-output-full.json")]
        [TestCase("05-checkpoint-departure-offline-return.json")]
        public void TheV1ImportOutputWrittenAsV2_LoadsUnderTheV3Reader_WithItsImportRecord(string fixture)
        {
            DefinitionCatalog legacy = AcceptedFoundationCatalogFixture.CreateCatalog();
            CheckpointReadResult v1 = SaveV1Mapper.Deserialize(File.ReadAllBytes(Path.Combine(Application.dataPath, SaveV1FixtureDirectory, fixture)), legacy);
            Assert.That(v1.Succeeded, Is.True, fixture);
            RunCommandFixtures.Rig rig = RunCommandFixtures.NewRig(new RunCommandFixtures.Options(), 50,
                lockedResources: new[] { "iron.bar", "iron.plate", "iron.gear" }, unlockedMachines: new[] { RunCommandFixtures.Miner });
            long returnUtcMs = V1ImportFixtures.DepartureUtc.ToUnixTimeMilliseconds();
            V1ImportResult built = new V1Import(rig.Catalog, new FakeLegacySource(v1.Checkpoint))
                .Build(v1.Checkpoint, returnUtcMs, new CheckpointId(SaveV2Scenarios.Uuid(601)), 1, SaveV2Scenarios.Uuid(602));
            Assert.That(built.IsSuccess, Is.True, fixture + " " + built.Code);

            // The import output as F003 wrote it (Save V2), through the real generation protocol.
            var backend = new ScriptedBackend("v2-import");
            Assert.That(new CheckpointGenerationFiles<RunCheckpoint>(backend, new SaveV2CheckpointCodec(rig.Catalog, SaveV2Mapper.LegacySchemaVersion))
                .Promote(built.Candidate).Succeeded, Is.True, fixture);
            byte[] v2 = backend.Files[CheckpointStorageFile.DataA];
            Assert.That(Text(v2), Does.StartWith("{\"schemaVersion\":2,"), fixture);

            RunCheckpointReadResult read = new JsonRunCheckpointStore(Memory, rig.Catalog, backend).ReadActive();
            Assert.That(read.Succeeded, Is.True, fixture + " " + read.Message);
            Assert.That(read.Checkpoint.Import, Is.Not.Null, fixture);
            Assert.That(read.Checkpoint.Import.SourceSchemaVersion, Is.EqualTo(1), fixture);
            Assert.That(read.Checkpoint.Import.SourceCheckpointId, Is.EqualTo(built.Candidate.Import.SourceCheckpointId), fixture);
            AssertV2Defaults(read.Checkpoint, fixture);
            AssertLosslessUpgrade(rig.Catalog, read.Checkpoint, v2, fixture);
        }

        // ------------------------------------------------------------------------------------------------------
        // Fault injection: a failed first V3 write after a V2 load keeps the V2 generation readable
        // ------------------------------------------------------------------------------------------------------

        [Test]
        public void AFailedFirstV3WriteAfterAV2Load_AtEveryBackendCall_KeepsTheV2GenerationReadable()
        {
            SaveV2Fixture mid = SaveV2Scenarios.PlayedGame()[0];
            var backend = new ScriptedBackend("v2-then-v3");
            RunCheckpoint v2Checkpoint = RunCheckpointTestSupport.CheckpointOf(mid);
            Assert.That(new CheckpointGenerationFiles<RunCheckpoint>(backend, new SaveV2CheckpointCodec(mid.Catalog, SaveV2Mapper.LegacySchemaVersion))
                .Promote(v2Checkpoint).Succeeded, Is.True);
            byte[] v2Bytes = backend.Files[CheckpointStorageFile.DataA];
            Assert.That(v2Bytes, Is.EqualTo(mid.Bytes), "the committed V2 file is the active generation");
            Dictionary<CheckpointStorageFile, byte[]> snapshot = backend.Snapshot();

            // The player loads the V2 generation, then its first write is V3: the loaded state under a new identity and generation.
            RunCheckpointReadResult loaded = new JsonRunCheckpointStore(Memory, mid.Catalog, backend).ReadActive();
            Assert.That(loaded.Succeeded, Is.True, loaded.Message);
            RunCheckpoint first = loaded.Checkpoint;
            var upgrade = new RunCheckpoint(new CheckpointId(SaveV2Scenarios.Uuid(800)), first.Generation + 1, first.DepartureUtcMs, first.OfflinePolicyRevision,
                first.Id, first.ContentRevision, null, first.Run, first.Permanent);

            backend.ResetCounters();
            Assert.That(new JsonRunCheckpointStore(Memory, mid.Catalog, backend).Promote(upgrade).Succeeded, Is.True, "the dry run publishes");
            Assert.That(Text(backend.Files[CheckpointStorageFile.DataB]), Does.StartWith("{\"schemaVersion\":3,"), "the first write after a V2 load is V3");
            List<string> log = backend.Log.ToList();
            int manifestWrite = log.IndexOf("Create:" + CheckpointStorageFile.ManifestB);
            Assert.That(manifestWrite, Is.GreaterThan(0));

            for (int call = 0; call < log.Count; call++)
            {
                backend.Restore(snapshot);
                backend.ResetCounters();
                backend.FailAtCall(call);
                CommandResult<CheckpointId> result = new JsonRunCheckpointStore(Memory, mid.Catalog, backend).Promote(upgrade);
                Assert.That(backend.FaultTriggered, Is.True, "call " + call + " " + log[call]);
                Assert.That(result.Succeeded, Is.False, "call " + call + " " + log[call]);
                backend.ClearFault();

                // The V2 generation's data slot is untouched and still decodes, as the active or as the retained previous generation.
                Assert.That(backend.Files[CheckpointStorageFile.DataA], Is.EqualTo(v2Bytes), "call " + call + " " + log[call]);
                var restarted = new JsonRunCheckpointStore(Memory, mid.Catalog, backend);
                RunCheckpointReadResult v2Read = restarted.Read(first.Id);
                Assert.That(v2Read.Succeeded, Is.True, "call " + call + " " + log[call] + " " + v2Read.Message);
                Assert.That(SaveV2Scenarios.Dump(v2Read.Checkpoint.Run, v2Read.Checkpoint.Permanent), Is.EqualTo(SaveV2Scenarios.Dump(mid.Run, mid.Permanent)),
                    "call " + call);
                RunCheckpointReadResult active = restarted.ReadActive();
                Assert.That(active.Succeeded, Is.True, "call " + call + " " + log[call] + " " + active.Message);
                if (call <= manifestWrite)
                    Assert.That(active.Checkpoint.Id, Is.EqualTo(first.Id), "before the manifest is written the V2 generation stays active: call " + call + " " + log[call]);
                else
                    Assert.That(active.Checkpoint.Id == first.Id || active.Checkpoint.Id == upgrade.Id, Is.True, "call " + call);

                // The retry publishes the V3 generation and keeps the V2 one as the previous generation.
                Assert.That(restarted.Promote(upgrade).Succeeded, Is.True, "retry after call " + call + " " + log[call]);
                Assert.That(restarted.ReadActive().Checkpoint.Id, Is.EqualTo(upgrade.Id), "call " + call);
                Assert.That(restarted.ReadPrevious().Checkpoint.Id, Is.EqualTo(first.Id), "call " + call);
            }
        }

        // ------------------------------------------------------------------------------------------------------
        // Forward compatibility (SEC-14): schemaVersion 4
        // ------------------------------------------------------------------------------------------------------

        private static ScriptedBackend Version4Store(out RunCatalog catalog, out byte[] future)
        {
            RunCommandFixtures.Rig rig = SaveV3Fixtures.AllMembers();
            catalog = rig.Catalog;
            future = SaveV3Fixtures.Rx(SaveV3Fixtures.Write(rig), "\"schemaVersion\":3", "\"schemaVersion\":4");
            var backend = new ScriptedBackend("v4-future");
            var id = new CheckpointId("55555555-5555-5555-5555-555555555555");
            Assert.That(new CheckpointGenerationFiles<RawV1Checkpoint>(backend, new RawV1Codec(id, 3)).Promote(new RawV1Checkpoint(id, 3, future)).Succeeded, Is.True);
            return backend;
        }

        [Test]
        public void ASchemaVersion4Save_IsRefusedAtTheStoreWithCheckpointVersion_AndNoOperationWritesAFile()
        {
            ScriptedBackend backend = Version4Store(out RunCatalog catalog, out byte[] future);
            Dictionary<CheckpointStorageFile, byte[]> before = backend.Snapshot();
            var store = new JsonRunCheckpointStore(Memory, catalog, backend);

            Assert.That(RunPersistenceReasons.From(store.ReadActive().Outcome), Is.EqualTo(RunPersistenceReasons.UnsupportedVersion));
            Assert.That(RunPersistenceReasons.From(store.Read(new CheckpointId("55555555-5555-5555-5555-555555555555")).Outcome),
                Is.EqualTo(RunPersistenceReasons.UnsupportedVersion));
            Assert.That(store.ProbeFiles().Value, Is.EqualTo(RunCheckpointPresence.PublishedFiles), "the cold start sees a save, so it never imports V1");

            var state = new RunState(catalog, RunSimHarness.Id(9300), 1, null, 25, 0, 1, 1, new[] { ContractFixtures.Ore });
            var candidate = new RunCheckpoint(new CheckpointId(SaveV2Scenarios.Uuid(900)), 9, RunSimHarness.StartUtcMs, catalog.OfflinePolicy.Revision, null,
                catalog.ContentRevision, null, state, new PermanentState(catalog));
            backend.ResetCounters();
            CommandResult<CheckpointId> promoted = store.Promote(candidate);
            CommandResult<CheckpointId> superseded = store.SupersedeDamaged(candidate);
            Assert.That(promoted.Succeeded, Is.False);
            Assert.That(RunPersistenceReasons.From(promoted.Outcome), Is.EqualTo(RunPersistenceReasons.UnsupportedVersion));
            Assert.That(superseded.Succeeded, Is.False, "another build's save is never discarded as damaged");
            Assert.That(RunPersistenceReasons.From(superseded.Outcome), Is.EqualTo(RunPersistenceReasons.UnsupportedVersion));
            Assert.That(backend.Log.Where(e => e.StartsWith("Create:", StringComparison.Ordinal) || e.StartsWith("Write:", StringComparison.Ordinal)), Is.Empty,
                "no write was even started");
            foreach (var entry in before) Assert.That(backend.Files[entry.Key], Is.EqualTo(entry.Value), entry.Key.ToString());
            Assert.That(backend.Files[CheckpointStorageFile.DataA], Is.EqualTo(future));
        }

        [Test]
        public void ASchemaVersion4Save_BlocksTheColdStart_WithoutAWrite_AndWithoutAV1Import()
        {
            ScriptedBackend backend = Version4Store(out RunCatalog catalog, out byte[] future);
            Dictionary<CheckpointStorageFile, byte[]> before = backend.Snapshot();
            var legacy = new FakeLegacySource((Checkpoint)null, CommandOutcome.NoCheckpoint);
            var state = new RunState(catalog, RunSimHarness.Id(9301), 1, null, 25, 0, 1, 1, new[] { ContractFixtures.Ore });
            var session = new RunSession(catalog, state, new PermanentState(catalog), RunSimHarness.StartUtcMs);
            var persistence = new RunPersistence(session, new JsonRunCheckpointStore(Memory, catalog, backend), new FixedUtcClock(RunSimHarness.StartUtcMs + 3_600_000),
                new SequenceCheckpointIds(), new OfflineRunReturnReconciler(catalog));

            backend.ResetCounters();
            RunCommandResult<RunPersistenceOutcome> started = persistence.ColdStart(legacy);
            Assert.That(started.IsSuccess, Is.False);
            Assert.That(started.ReasonCode, Is.EqualTo(RunPersistenceReasons.ReturnBlocked));
            Assert.That(persistence.Lifecycle, Is.EqualTo(LifecycleState.ReturnBlocked));
            Assert.That(legacy.Reads, Is.Zero, "no V1 import is attempted (SEC-14)");
            Assert.That(persistence.CanDiscardDamagedSave, Is.False, "another build's save is not offered for discard");
            Assert.That(session.State.RunId, Is.EqualTo(RunSimHarness.Id(9301)), "nothing replaced the session's run");

            // Neither a save nor a retry writes over it.
            Assert.That(persistence.SaveNow().IsSuccess, Is.False);
            Assert.That(persistence.RetryReturn().IsSuccess, Is.False);
            Assert.That(backend.Log.Where(e => e.StartsWith("Create:", StringComparison.Ordinal) || e.StartsWith("Write:", StringComparison.Ordinal)), Is.Empty);
            foreach (var entry in before) Assert.That(backend.Files[entry.Key], Is.EqualTo(entry.Value), entry.Key.ToString());
            Assert.That(backend.Files.Keys, Is.EquivalentTo(before.Keys), "no file was added");
        }
    }
}
