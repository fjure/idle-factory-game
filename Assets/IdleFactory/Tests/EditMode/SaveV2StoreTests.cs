using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using IdleFactory.Core.Application;
using IdleFactory.Domain;
using IdleFactory.Run.Application;
using IdleFactory.Unity.Persistence;
using NUnit.Framework;
using UnityEngine;

namespace IdleFactory.Tests.EditMode
{
    /// <summary>
    /// The Save V2 checkpoint store (T138, contracts/save-v2-and-v1-import.md, FR-027, SC-002): the generation protocol
    /// over <c>v2/</c> with the real file backend, fault injection at every backend call of a publish (write, flush, read
    /// back, retain previous, manifest, verify), restart with only a pending file, the file probe for the cold-start
    /// source selection, static rejection codes (SEC-5), recovery of the previous generation, V1 isolation and the
    /// committed W003-S device set (PE-6, OPS-5).
    /// </summary>
    public sealed class SaveV2StoreTests
    {
        private const string Memory = "memory-root";
        private const string RegenerateEnvironmentVariable = "IDLEFACTORY_REGENERATE_SAVEV2_FIXTURES";

        private static List<SaveV2Fixture> _game;
        private bool _logToUnity;
        private string _root;

        [OneTimeSetUp]
        public void BuildGame()
        {
            _game = SaveV2Scenarios.PlayedGame();
        }

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

        // Generation 7, 8 and 9 of one played game: the mid-run, the pending prestige candidate and the state after it.
        private static SaveV2Fixture Mid => _game[0];
        private static SaveV2Fixture Candidate => _game[1];
        private static SaveV2Fixture Post => _game[2];

        private static RunCheckpoint Checkpoint(SaveV2Fixture fixture) => RunCheckpointTestSupport.CheckpointOf(fixture);

        private static JsonRunCheckpointStore Store(ScriptedBackend backend) =>
            new JsonRunCheckpointStore(Memory, Mid.Catalog, backend);

        private JsonRunCheckpointStore DiskStore() => new JsonRunCheckpointStore(_root, Mid.Catalog);

        private static void AssertPromoted(JsonRunCheckpointStore store, SaveV2Fixture fixture)
        {
            var result = store.Promote(Checkpoint(fixture));
            Assert.That(result.Succeeded, Is.True, result.Message);
            Assert.That(result.Value, Is.EqualTo(Checkpoint(fixture).Id));
        }

        // ------------------------------------------------------------------------------------------------------
        // Layout and round trip
        // ------------------------------------------------------------------------------------------------------

        [Test]
        public void Promote_WritesTheContractSlotNamesUnderV2_AndNothingUnderV1()
        {
            AssertPromoted(DiskStore(), Mid);
            string directory = Path.Combine(_root, "IdleFactory", "Checkpoints", "v2");
            Assert.That(RunCheckpointTestSupport.V2Directory(_root), Is.EqualTo(Path.GetFullPath(directory)));
            Assert.That(Directory.GetFiles(directory).Select(Path.GetFileName).OrderBy(n => n, StringComparer.Ordinal),
                Is.EqualTo(new[] { "run-checkpoint.data-a.json", "run-checkpoint.manifest-a.bin", "run-checkpoint.pending.json" }));
            Assert.That(Directory.Exists(Path.Combine(_root, "IdleFactory", "Checkpoints", "v1")), Is.False);
            Assert.That(File.ReadAllBytes(Path.Combine(directory, "run-checkpoint.data-a.json")), Is.EqualTo(Mid.Bytes));
        }

        [Test]
        public void EveryValidFixture_PromotesAndReadsBackToIdenticalBytesAndState()
        {
            foreach (SaveV2Fixture fixture in SaveV2Scenarios.All().Where(f => !f.IsCorrupt))
            {
                string root = RunCheckpointTestSupport.NewTempRoot();
                try
                {
                    var store = new JsonRunCheckpointStore(root, fixture.Catalog);
                    Assert.That(store.Promote(Checkpoint(fixture)).Succeeded, Is.True, fixture.FileName);
                    var read = new JsonRunCheckpointStore(root, fixture.Catalog).ReadActive();
                    Assert.That(read.Succeeded, Is.True, fixture.FileName + " " + read.Message);
                    Assert.That(RunCheckpointTestSupport.BytesOf(read.Checkpoint, fixture), Is.EqualTo(fixture.Bytes), fixture.FileName);
                    Assert.That(SaveV2Scenarios.Dump(read.Checkpoint.Run, read.Checkpoint.Permanent),
                        Is.EqualTo(SaveV2Scenarios.Dump(fixture.Run, fixture.Permanent)), fixture.FileName);
                    Assert.That(read.Checkpoint.Id.Uuid, Is.EqualTo(fixture.Meta.CheckpointId), fixture.FileName);
                    Assert.That(read.Checkpoint.Generation, Is.EqualTo(fixture.Meta.Generation), fixture.FileName);
                    Assert.That(read.Checkpoint.DepartureUtcMs, Is.EqualTo(fixture.Meta.DepartureUtcMs), fixture.FileName);
                    Assert.That(read.Checkpoint.ReconciledFromCheckpointId?.Uuid, Is.EqualTo(fixture.Meta.ReconciledFromCheckpointId), fixture.FileName);
                }
                finally
                {
                    RunCheckpointTestSupport.DeleteQuietly(root);
                }
            }
        }

        [Test]
        public void ImportRecord_RoundTripsThroughTheStore()
        {
            RunCheckpoint plain = Checkpoint(Mid);
            var imported = new RunCheckpoint(plain.Id, plain.Generation, plain.DepartureUtcMs, plain.OfflinePolicyRevision, null,
                plain.ContentRevision, new RunCheckpointImport(1, SaveV2Scenarios.Uuid(77).ToString("D"), 12), plain.Run, plain.Permanent);
            var store = DiskStore();
            Assert.That(store.Promote(imported).Succeeded, Is.True);
            RunCheckpoint read = DiskStore().ReadActive().Checkpoint;
            Assert.That(read.Import, Is.Not.Null);
            Assert.That(read.Import.SourceSchemaVersion, Is.EqualTo(1));
            Assert.That(read.Import.SourceCheckpointId, Is.EqualTo(SaveV2Scenarios.Uuid(77).ToString("D")));
            Assert.That(read.Import.SourceGeneration, Is.EqualTo(12));
        }

        [Test]
        public void Promote_AlternatesSlots_AndRetainsThePreviousGeneration()
        {
            var store = DiskStore();
            AssertPromoted(store, Mid);
            AssertPromoted(store, Candidate);
            AssertPromoted(store, Post);

            Assert.That(store.ReadActive().Checkpoint.Id.Uuid, Is.EqualTo(Post.Meta.CheckpointId));
            Assert.That(store.ReadPrevious().Checkpoint.Id.Uuid, Is.EqualTo(Candidate.Meta.CheckpointId));
            Assert.That(store.Read(new CheckpointId(Candidate.Meta.CheckpointId)).Succeeded, Is.True);
            Assert.That(store.Read(new CheckpointId(Mid.Meta.CheckpointId)).Outcome, Is.EqualTo(CommandOutcome.NoCheckpoint), "the third generation drops the first");
            string directory = RunCheckpointTestSupport.V2Directory(_root);
            Assert.That(File.Exists(Path.Combine(directory, "run-checkpoint.data-b.json")), Is.True);
            Assert.That(File.Exists(Path.Combine(directory, "run-checkpoint.manifest-b.bin")), Is.True);
        }

        [Test]
        public void Promote_RejectsAGenerationThatDoesNotAdvance_AndConfirmsTheSameCheckpointIdempotently()
        {
            var store = DiskStore();
            AssertPromoted(store, Candidate);
            AssertPromoted(store, Candidate);
            Assert.That(store.ReadPrevious().Outcome, Is.EqualTo(CommandOutcome.NoCheckpoint), "an idempotent promote retains nothing new");

            var older = store.Promote(Checkpoint(Mid));
            Assert.That(older.Succeeded, Is.False);
            Assert.That(older.Outcome, Is.EqualTo(CommandOutcome.InvalidCheckpoint));
            Assert.That(store.ReadActive().Checkpoint.Id.Uuid, Is.EqualTo(Candidate.Meta.CheckpointId));
        }

        // ------------------------------------------------------------------------------------------------------
        // Fault injection: every backend call of a publish
        // ------------------------------------------------------------------------------------------------------

        [Test]
        public void SecondPromote_FailingAtEveryBackendCall_KeepsTheOldGenerationUntilTheManifestIsWritten_AndRetrySucceeds()
        {
            RunCheckpoint first = Checkpoint(Mid);
            RunCheckpoint second = Checkpoint(Candidate);
            var backend = new ScriptedBackend();
            Assert.That(Store(backend).Promote(first).Succeeded, Is.True);
            var snapshot = backend.Snapshot();

            backend.ResetCounters();
            Assert.That(Store(backend).Promote(second).Succeeded, Is.True);
            int total = backend.Calls;
            var log = backend.Log.ToList();
            int manifestWrite = log.IndexOf("Create:" + CheckpointStorageFile.ManifestB);
            Assert.That(manifestWrite, Is.GreaterThan(0));
            Assert.That(log, Does.Contain("Create:" + CheckpointStorageFile.Pending), "the candidate goes to the pending slot first");
            Assert.That(log, Does.Contain("Flush:" + CheckpointStorageFile.Pending));
            Assert.That(log.Count(entry => entry.StartsWith("ReadExactly:")), Is.GreaterThan(3), "the candidate is read back before it is published");
            Assert.That(log, Does.Contain("FlushExisting:" + CheckpointStorageFile.DataA), "the retained generation is stabilized");

            int keptOld = 0;
            for (int call = 0; call < total; call++)
            {
                backend.Restore(snapshot);
                backend.ResetCounters();
                backend.FailAtCall(call);
                var result = Store(backend).Promote(second);
                Assert.That(backend.FaultTriggered, Is.True, "call " + call + " " + log[call]);
                Assert.That(result.Succeeded, Is.False, "call " + call + " " + log[call]);
                Assert.That(result.Message, Does.Not.Contain("Injected"), "static copy only");

                backend.ClearFault();
                var restarted = Store(backend);
                var active = restarted.ReadActive();
                Assert.That(active.Succeeded, Is.True, "call " + call + " " + log[call] + " " + active.Message);
                bool isOld = active.Checkpoint.Id == first.Id;
                Assert.That(isOld || active.Checkpoint.Id == second.Id, Is.True, "call " + call + " " + log[call]);
                if (call <= manifestWrite)
                {
                    Assert.That(isOld, Is.True, "before the manifest is written the old generation is authoritative: call " + call + " " + log[call]);
                    keptOld++;
                }

                var retried = restarted.Promote(second);
                Assert.That(retried.Succeeded, Is.True, "retry after call " + call + " " + log[call] + " " + retried.Message);
                Assert.That(restarted.ReadActive().Checkpoint.Id, Is.EqualTo(second.Id), "call " + call);
                Assert.That(restarted.ReadPrevious().Checkpoint.Id, Is.EqualTo(first.Id), "call " + call);
            }

            Assert.That(keptOld, Is.EqualTo(manifestWrite + 1));
        }

        [Test]
        public void FirstPromote_FailingAtEveryBackendCall_PublishesNothingBeforeTheManifest_AndRetrySucceeds()
        {
            RunCheckpoint first = Checkpoint(Mid);
            var backend = new ScriptedBackend();
            Assert.That(Store(backend).Promote(first).Succeeded, Is.True);
            int total = backend.Calls;
            int manifestWrite = backend.Log.IndexOf("Create:" + CheckpointStorageFile.ManifestA);
            Assert.That(manifestWrite, Is.GreaterThan(0));

            for (int call = 0; call < total; call++)
            {
                backend.Restore(new Dictionary<CheckpointStorageFile, byte[]>());
                backend.ResetCounters();
                backend.FailAtCall(call);
                var result = Store(backend).Promote(first);
                Assert.That(backend.FaultTriggered, Is.True, "call " + call);
                Assert.That(result.Succeeded, Is.False, "call " + call);

                backend.ClearFault();
                var restarted = Store(backend);
                var active = restarted.ReadActive();
                if (call <= manifestWrite)
                {
                    Assert.That(active.Outcome, Is.EqualTo(CommandOutcome.NoCheckpoint), "nothing is published before the manifest: call " + call);
                    Assert.That(restarted.ProbeFiles().Succeeded, Is.True, "the probe is answerable");
                }
                else
                {
                    Assert.That(active.Succeeded || active.Outcome == CommandOutcome.InvalidCheckpoint, Is.True, "call " + call);
                }

                Assert.That(restarted.Promote(first).Succeeded, Is.True, "retry after call " + call);
                Assert.That(restarted.ReadActive().Checkpoint.Id, Is.EqualTo(first.Id), "call " + call);
            }
        }

        [Test]
        public void SecondPromote_WithACorruptedReadAtEveryReadCall_NeverPublishesABadGeneration_AndRetrySucceeds()
        {
            RunCheckpoint first = Checkpoint(Mid);
            RunCheckpoint second = Checkpoint(Candidate);
            var backend = new ScriptedBackend();
            Assert.That(Store(backend).Promote(first).Succeeded, Is.True);
            var snapshot = backend.Snapshot();
            backend.ResetCounters();
            Assert.That(Store(backend).Promote(second).Succeeded, Is.True);
            var log = backend.Log.ToList();

            int corrupted = 0;
            for (int call = 0; call < log.Count; call++)
            {
                if (!log[call].StartsWith("ReadExactly:", StringComparison.Ordinal)) continue;
                backend.Restore(snapshot);
                backend.ResetCounters();
                backend.CorruptReadAtCall(call);
                var result = Store(backend).Promote(second);
                Assert.That(backend.FaultTriggered, Is.True, "call " + call);
                if (result.Succeeded) Assert.That(result.Value, Is.EqualTo(second.Id), "call " + call);
                corrupted++;

                backend.ClearFault();
                var restarted = Store(backend);
                var active = restarted.ReadActive();
                Assert.That(active.Succeeded, Is.True, "call " + call + " " + log[call] + " " + active.Message);
                Assert.That(active.Checkpoint.Id == first.Id || active.Checkpoint.Id == second.Id, Is.True, "call " + call + " " + log[call]);
                Assert.That(restarted.Promote(second).Succeeded, Is.True, "retry after call " + call);
                Assert.That(restarted.ReadActive().Checkpoint.Id, Is.EqualTo(second.Id), "call " + call);
            }

            Assert.That(corrupted, Is.GreaterThanOrEqualTo(6));
        }

        [Test]
        public void EveryStorageFailure_IsATypedRejection_NotAnException()
        {
            var backend = new ScriptedBackend();
            var store = Store(backend);
            Assert.That(store.Promote(Checkpoint(Mid)).Succeeded, Is.True);

            backend.FailAll = true;
            Assert.That(store.ReadActive().Outcome, Is.EqualTo(CommandOutcome.PersistenceFailed));
            Assert.That(store.ReadPrevious().Outcome, Is.EqualTo(CommandOutcome.PersistenceFailed));
            Assert.That(store.Read(Checkpoint(Mid).Id).Outcome, Is.EqualTo(CommandOutcome.PersistenceFailed));
            Assert.That(store.Promote(Checkpoint(Candidate)).Outcome, Is.EqualTo(CommandOutcome.PersistenceFailed));
            Assert.That(store.RecoverPrevious(Checkpoint(Candidate)).Succeeded, Is.False);
            Assert.That(store.ProbeFiles().Outcome, Is.EqualTo(CommandOutcome.PersistenceFailed));
        }

        // ------------------------------------------------------------------------------------------------------
        // Restart, cold-start probe and rejection codes
        // ------------------------------------------------------------------------------------------------------

        [Test]
        public void RestartWithOnlyAnUnpromotedPendingFile_DiscardsIt_AndTheNextPromoteSucceeds()
        {
            var backend = new ScriptedBackend();
            backend.Files[CheckpointStorageFile.Pending] = (byte[])Mid.Bytes.Clone();
            var store = Store(backend);

            Assert.That(store.ReadActive().Outcome, Is.EqualTo(CommandOutcome.NoCheckpoint), "a pending file is never authoritative");
            Assert.That(store.ReadPrevious().Outcome, Is.EqualTo(CommandOutcome.NoCheckpoint));
            Assert.That(store.ProbeFiles().Value, Is.EqualTo(RunCheckpointPresence.PendingOnly));

            AssertPromoted(store, Candidate);
            Assert.That(store.ReadActive().Checkpoint.Id.Uuid, Is.EqualTo(Candidate.Meta.CheckpointId));
            Assert.That(store.ReadPrevious().Outcome, Is.EqualTo(CommandOutcome.NoCheckpoint), "the discarded pending candidate is not retained");
        }

        [Test]
        public void ProbeFiles_DistinguishesNoFiles_PendingOnly_AndPublishedFiles_AndReadDistinguishesInvalid()
        {
            var backend = new ScriptedBackend();
            var store = Store(backend);
            Assert.That(store.ProbeFiles().Value, Is.EqualTo(RunCheckpointPresence.NoFiles));
            Assert.That(store.ReadActive().Outcome, Is.EqualTo(CommandOutcome.NoCheckpoint));

            backend.Files[CheckpointStorageFile.DataA] = (byte[])Mid.Bytes.Clone();
            Assert.That(store.ProbeFiles().Value, Is.EqualTo(RunCheckpointPresence.PublishedFiles), "a data file without a manifest");
            Assert.That(store.ReadActive().Outcome, Is.EqualTo(CommandOutcome.NoCheckpoint), "but no manifest selects it: the caller must probe");

            backend.Files.Clear();
            Assert.That(store.Promote(Checkpoint(Mid)).Succeeded, Is.True);
            Assert.That(store.ProbeFiles().Value, Is.EqualTo(RunCheckpointPresence.PublishedFiles));
            Assert.That(store.ReadActive().Succeeded, Is.True);

            var bytes = backend.Files[CheckpointStorageFile.DataA];
            bytes[bytes.Length / 2] ^= 0x01;
            var invalid = store.ReadActive();
            Assert.That(invalid.Succeeded, Is.False);
            Assert.That(invalid.Outcome, Is.EqualTo(CommandOutcome.InvalidCheckpoint));
            Assert.That(store.ProbeFiles().Value, Is.EqualTo(RunCheckpointPresence.PublishedFiles));

            backend.Files.Clear();
            backend.Files[CheckpointStorageFile.ManifestA] = new byte[] { 1, 2, 3 };
            Assert.That(store.ReadActive().Outcome, Is.EqualTo(CommandOutcome.InvalidCheckpoint), "a manifest that does not parse is invalid, not absent");
        }

        [Test]
        public void Rejections_CarryOnlyStaticCodesAndCopy_NeverTextFromTheFile()
        {
            var codec = new SaveV2CheckpointCodec(Mid.Catalog);
            string secret = "secret.file-text";
            string text = new UTF8Encoding(false).GetString(Mid.Bytes);
            byte[] unresolved = new UTF8Encoding(false).GetBytes(text.Replace("machine.miner", secret));
            var decoded = codec.Deserialize(unresolved);
            Assert.That(decoded.Succeeded, Is.False);
            Assert.That(decoded.Outcome, Is.EqualTo(CommandOutcome.UnresolvedContent));
            Assert.That(decoded.Message, Does.StartWith("save.v2."));
            Assert.That(decoded.Message, Does.Not.Contain(secret));

            var corrupt = codec.Deserialize(SaveV2Scenarios.All().Single(f => f.FileName == "corrupt-07-run-number-mismatch.json").Bytes);
            Assert.That(corrupt.Message, Is.EqualTo(SaveV2Codes.CrossField + ": " + SaveV2Codes.Message(SaveV2Codes.CrossField)));

            var version = codec.Deserialize(SaveV2Scenarios.Mutate(Mid.Bytes, "\"schemaVersion\":2", "\"schemaVersion\":3"));
            Assert.That(version.Outcome, Is.EqualTo(CommandOutcome.UnsupportedSaveVersion));
            Assert.That(version.Message, Does.StartWith(SaveV2Codes.Version + ":"));

            var backend = new ScriptedBackend();
            var store = Store(backend);
            Assert.That(store.Promote(Checkpoint(Mid)).Succeeded, Is.True);
            backend.Files[CheckpointStorageFile.DataA] = unresolved;
            var read = store.ReadActive();
            Assert.That(read.Succeeded, Is.False);
            Assert.That(read.Message, Does.Not.Contain(secret));
        }

        // ------------------------------------------------------------------------------------------------------
        // Recovery of the previous generation
        // ------------------------------------------------------------------------------------------------------

        [Test]
        public void RecoverPrevious_PublishesThePreviousStateAsANewActiveGeneration_AndRetainsTheOldActive()
        {
            var store = DiskStore();
            AssertPromoted(store, Mid);
            AssertPromoted(store, Candidate);
            RunCheckpoint previous = store.ReadPrevious().Checkpoint;
            RunCheckpoint active = store.ReadActive().Checkpoint;

            var recovery = new RunCheckpoint(new CheckpointId(SaveV2Scenarios.Uuid(50)), active.Generation + 1, active.DepartureUtcMs + 1,
                previous.OfflinePolicyRevision, previous.Id, previous.ContentRevision, null, previous.Run, previous.Permanent);
            var result = store.RecoverPrevious(recovery);
            Assert.That(result.Succeeded, Is.True, result.Message);

            Assert.That(store.ReadActive().Checkpoint.Id, Is.EqualTo(recovery.Id));
            Assert.That(store.ReadActive().Checkpoint.Generation, Is.EqualTo(active.Generation + 1));
            Assert.That(SaveV2Scenarios.Dump(store.ReadActive().Checkpoint.Run, store.ReadActive().Checkpoint.Permanent),
                Is.EqualTo(SaveV2Scenarios.Dump(Mid.Run, Mid.Permanent)));
            Assert.That(store.ReadPrevious().Checkpoint.Id, Is.EqualTo(previous.Id), "the recovered generation keeps the previous one retained");
            Assert.That(store.RecoverPrevious(recovery).Succeeded, Is.True, "the same recovery is idempotent");
        }

        [Test]
        public void RecoverPrevious_RejectsACandidateThatIsNotThePreviousState()
        {
            var store = DiskStore();
            AssertPromoted(store, Mid);
            AssertPromoted(store, Candidate);
            RunCheckpoint previous = store.ReadPrevious().Checkpoint;
            RunCheckpoint active = store.ReadActive().Checkpoint;

            var wrongState = new RunCheckpoint(new CheckpointId(SaveV2Scenarios.Uuid(51)), active.Generation + 1, active.DepartureUtcMs,
                previous.OfflinePolicyRevision, previous.Id, previous.ContentRevision, null, active.Run, active.Permanent);
            var notLinked = new RunCheckpoint(new CheckpointId(SaveV2Scenarios.Uuid(52)), active.Generation + 1, active.DepartureUtcMs,
                previous.OfflinePolicyRevision, null, previous.ContentRevision, null, previous.Run, previous.Permanent);
            var notAdvanced = new RunCheckpoint(new CheckpointId(SaveV2Scenarios.Uuid(53)), active.Generation, active.DepartureUtcMs,
                previous.OfflinePolicyRevision, previous.Id, previous.ContentRevision, null, previous.Run, previous.Permanent);
            foreach (RunCheckpoint bad in new[] { wrongState, notLinked, notAdvanced })
            {
                var result = store.RecoverPrevious(bad);
                Assert.That(result.Succeeded, Is.False);
                Assert.That(result.Outcome, Is.EqualTo(CommandOutcome.InvalidCheckpoint));
            }

            Assert.That(store.ReadActive().Checkpoint.Id, Is.EqualTo(active.Id));
        }

        [Test]
        public void RecoverPrevious_WithoutAPreviousGeneration_IsNoCheckpoint()
        {
            var store = DiskStore();
            AssertPromoted(store, Mid);
            RunCheckpoint active = store.ReadActive().Checkpoint;
            var candidate = new RunCheckpoint(new CheckpointId(SaveV2Scenarios.Uuid(54)), active.Generation + 1, active.DepartureUtcMs,
                active.OfflinePolicyRevision, active.Id, active.ContentRevision, null, active.Run, active.Permanent);
            Assert.That(store.RecoverPrevious(candidate).Outcome, Is.EqualTo(CommandOutcome.NoCheckpoint));
        }

        // ------------------------------------------------------------------------------------------------------
        // V1 isolation
        // ------------------------------------------------------------------------------------------------------

        [Test]
        public void V2Writes_LeaveTheV1DirectoryByteIdentical()
        {
            string v1 = SaveV1CheckpointFiles.Layout.DirectoryFor(_root);
            Directory.CreateDirectory(v1);
            var names = new[]
            {
                SaveV1CheckpointFiles.DataAFileName, SaveV1CheckpointFiles.ManifestAFileName, SaveV1CheckpointFiles.PendingFileName,
            };
            for (int i = 0; i < names.Length; i++) File.WriteAllBytes(Path.Combine(v1, names[i]), new byte[] { (byte)(i + 1), 9, 9, 9 });
            string Hash() => string.Join("|", Directory.GetFiles(v1).OrderBy(f => f, StringComparer.Ordinal)
                .Select(f => Path.GetFileName(f) + ":" + SaveV2Scenarios.Sha256(File.ReadAllBytes(f))));
            string before = Hash();

            var store = DiskStore();
            AssertPromoted(store, Mid);
            AssertPromoted(store, Candidate);
            AssertPromoted(store, Post);
            Assert.That(store.RecoverPrevious(new RunCheckpoint(new CheckpointId(SaveV2Scenarios.Uuid(55)), Post.Meta.Generation + 1,
                Post.Meta.DepartureUtcMs, 1, new CheckpointId(Candidate.Meta.CheckpointId), Mid.Catalog.ContentRevision, null,
                store.ReadPrevious().Checkpoint.Run, store.ReadPrevious().Checkpoint.Permanent)).Succeeded, Is.True);

            Assert.That(Hash(), Is.EqualTo(before));
            Assert.That(Directory.GetFiles(v1).Length, Is.EqualTo(names.Length));
        }

        // ------------------------------------------------------------------------------------------------------
        // Committed W003-S device set (T123, PE-6, OPS-5)
        // ------------------------------------------------------------------------------------------------------

        [Test]
        public void DeviceSet_W003S_IsByteIdenticalToTheStoreRegeneration()
        {
            string fixtures = Path.Combine(Application.dataPath, "IdleFactory/Tests/EditMode/Fixtures/SaveV2");
            List<SaveV2DeviceFile> regenerated = SaveV2DeviceSets.BuildW003S(out byte[] pending);
            if (Environment.GetEnvironmentVariable(RegenerateEnvironmentVariable) == "1") SaveV2DeviceSets.WriteW003S(fixtures);

            string hint = " Set " + RegenerateEnvironmentVariable + "=1 and rerun to update the committed device set.";
            string directory = Path.Combine(fixtures, SaveV2DeviceSets.W003SDirectory);
            foreach (SaveV2DeviceFile file in regenerated)
            {
                string path = Path.Combine(directory, file.Name);
                Assert.That(File.Exists(path), Is.True, "Committed device file is missing: " + file.Name + "." + hint);
                Assert.That(File.ReadAllBytes(path), Is.EqualTo(file.Bytes), "Device file drifted: " + file.Name + "." + hint);
            }

            Assert.That(Directory.GetFiles(directory).Select(Path.GetFileName).Where(n => !n.EndsWith(".meta", StringComparison.Ordinal))
                .OrderBy(n => n, StringComparer.Ordinal), Is.EqualTo(SaveV2DeviceSets.W003SFiles.OrderBy(n => n, StringComparer.Ordinal)),
                "no unlisted file in the device set");
            Assert.That(pending, Is.EqualTo(regenerated.Single(f => f.Name == SaveV2CheckpointFiles.DataAFileName).Bytes),
                "the pending slot only holds the candidate that was then published");
        }

        // ------------------------------------------------------------------------------------------------------
        // Supersede of a damaged sole save with an intact manifest (T141, SEC-1)
        // ------------------------------------------------------------------------------------------------------

        private static byte[] FlipMiddle(byte[] bytes)
        {
            var copy = (byte[])bytes.Clone();
            copy[copy.Length / 2] ^= 0x5A;
            return copy;
        }

        /// <summary>One generation published (data A, manifest A, pending), then its data torn: the manifest stays intact.</summary>
        private static ScriptedBackend TornSoleGeneration()
        {
            var backend = new ScriptedBackend();
            Assert.That(Store(backend).Promote(Checkpoint(Mid)).Succeeded, Is.True);
            backend.Files[CheckpointStorageFile.DataA] = FlipMiddle(backend.Files[CheckpointStorageFile.DataA]);
            return backend;
        }

        private static void AssertSameFiles(ScriptedBackend backend, Dictionary<CheckpointStorageFile, byte[]> expected, string because)
        {
            Assert.That(backend.Files.Keys.OrderBy(k => k), Is.EqualTo(expected.Keys.OrderBy(k => k)), because);
            foreach (var entry in expected) Assert.That(backend.Files[entry.Key], Is.EqualTo(entry.Value), because + " " + entry.Key);
        }

        [Test]
        public void Supersede_WithAnIntactManifestAndTornSoleData_PublishesTheCandidateAsTheOnlyGeneration()
        {
            var backend = TornSoleGeneration();
            var store = Store(backend);
            Assert.That(store.ReadActive().Outcome, Is.EqualTo(CommandOutcome.InvalidCheckpoint));
            Assert.That(store.Promote(Checkpoint(Candidate)).Outcome, Is.EqualTo(CommandOutcome.InvalidCheckpoint),
                "the ordinary promotion is unchanged: it still rejects a torn active generation");

            var floor = store.NextSupersedeGeneration();
            Assert.That(floor.Succeeded, Is.True);
            Assert.That(floor.Value, Is.EqualTo(Mid.Meta.Generation + 1));

            var result = store.SupersedeDamaged(Checkpoint(Candidate));

            Assert.That(result.Succeeded, Is.True, result.Message);
            Assert.That(result.Value, Is.EqualTo(Checkpoint(Candidate).Id));
            var restarted = Store(backend);
            Assert.That(restarted.ReadActive().Checkpoint.Id, Is.EqualTo(Checkpoint(Candidate).Id));
            Assert.That(restarted.ReadPrevious().Outcome, Is.EqualTo(CommandOutcome.NoCheckpoint), "the torn generation is not retained");
            Assert.That(backend.Files.ContainsKey(CheckpointStorageFile.ManifestB), Is.True, "the alternate manifest slot carries it");
            Assert.That(backend.Files[CheckpointStorageFile.DataB], Is.EqualTo(Candidate.Bytes));

            // The store keeps working normally afterwards.
            AssertPromoted(restarted, Post);
            Assert.That(restarted.ReadPrevious().Checkpoint.Id, Is.EqualTo(Checkpoint(Candidate).Id));
        }

        [Test]
        public void Supersede_WithATornSoleGenerationInDataB_UsesTheOtherSlotsToo()
        {
            var backend = new ScriptedBackend();
            Assert.That(Store(backend).Promote(Checkpoint(Mid)).Succeeded, Is.True);
            Assert.That(Store(backend).Promote(Checkpoint(Candidate)).Succeeded, Is.True);
            // Active is B, previous A; both torn: the previous generation is unreadable too.
            backend.Files[CheckpointStorageFile.DataA] = FlipMiddle(backend.Files[CheckpointStorageFile.DataA]);
            backend.Files[CheckpointStorageFile.DataB] = FlipMiddle(backend.Files[CheckpointStorageFile.DataB]);
            var store = Store(backend);

            Assert.That(store.NextSupersedeGeneration().Value, Is.EqualTo(Candidate.Meta.Generation + 1));
            var result = store.SupersedeDamaged(Checkpoint(Post));

            Assert.That(result.Succeeded, Is.True, result.Message);
            var restarted = Store(backend);
            Assert.That(restarted.ReadActive().Checkpoint.Id, Is.EqualTo(Checkpoint(Post).Id));
            Assert.That(restarted.ReadPrevious().Outcome, Is.EqualTo(CommandOutcome.NoCheckpoint));
        }

        [Test]
        public void Supersede_FailingAtEveryBackendCall_LeavesTheDamagedStateUntilTheManifestIsWritten_AndRetryAndRestartSucceed()
        {
            RunCheckpoint candidate = Checkpoint(Candidate);
            var reference = TornSoleGeneration();
            var snapshot = reference.Snapshot();
            reference.ResetCounters();
            Assert.That(Store(reference).SupersedeDamaged(candidate).Succeeded, Is.True);
            int total = reference.Calls;
            var log = reference.Log.ToList();
            int manifestWrite = log.IndexOf("Create:" + CheckpointStorageFile.ManifestB);
            Assert.That(manifestWrite, Is.GreaterThan(0));
            Assert.That(log, Does.Contain("Create:" + CheckpointStorageFile.Pending), "the candidate goes to the pending slot first");
            Assert.That(log, Does.Contain("Flush:" + CheckpointStorageFile.DataB));
            Assert.That(log, Does.Contain("Flush:" + CheckpointStorageFile.ManifestB));

            int untouched = 0;
            for (int call = 0; call < total; call++)
            {
                var backend = new ScriptedBackend();
                backend.Restore(snapshot);
                backend.ResetCounters();
                backend.FailAtCall(call);
                var result = Store(backend).SupersedeDamaged(candidate);
                Assert.That(backend.FaultTriggered, Is.True, "call " + call + " " + log[call]);
                Assert.That(result.Succeeded, Is.False, "call " + call + " " + log[call]);
                Assert.That(result.Message, Does.Not.Contain("Injected"), "static copy only");

                backend.ClearFault();
                var restarted = Store(backend);
                var active = restarted.ReadActive();
                if (active.Succeeded)
                {
                    Assert.That(call, Is.GreaterThan(manifestWrite), "published only once the manifest was written: " + log[call]);
                    Assert.That(active.Checkpoint.Id, Is.EqualTo(candidate.Id), "call " + call);
                }
                else
                {
                    Assert.That(active.Outcome, Is.EqualTo(CommandOutcome.InvalidCheckpoint), "call " + call + " " + log[call]);
                    untouched++;
                }

                // Retry in the same session: the supersede again, or the idempotent confirmation when it already landed.
                var retried = active.Succeeded ? restarted.Promote(candidate) : restarted.SupersedeDamaged(candidate);
                Assert.That(retried.Succeeded, Is.True, "retry after call " + call + " " + log[call] + " " + retried.Message);

                // A restart selects the superseding generation.
                var afterRestart = Store(backend);
                Assert.That(afterRestart.ReadActive().Checkpoint.Id, Is.EqualTo(candidate.Id), "call " + call);
                Assert.That(afterRestart.ReadPrevious().Outcome, Is.EqualTo(CommandOutcome.NoCheckpoint), "call " + call);
                AssertPromoted(afterRestart, Post);
            }

            Assert.That(untouched, Is.GreaterThanOrEqualTo(manifestWrite), "every earlier failure left the damaged state selected");
        }

        [Test]
        public void Supersede_IsRefusedWhenAnyGenerationIsValid_AndWritesNothing()
        {
            RunCheckpoint candidate = Checkpoint(Post);

            // One valid generation (active only, intact).
            var onlyValid = new ScriptedBackend();
            Assert.That(Store(onlyValid).Promote(Checkpoint(Mid)).Succeeded, Is.True);
            // Active torn, previous valid.
            var previousValid = new ScriptedBackend();
            Assert.That(Store(previousValid).Promote(Checkpoint(Mid)).Succeeded, Is.True);
            Assert.That(Store(previousValid).Promote(Checkpoint(Candidate)).Succeeded, Is.True);
            previousValid.Files[CheckpointStorageFile.DataB] = FlipMiddle(previousValid.Files[CheckpointStorageFile.DataB]);
            // Active valid, previous torn.
            var activeValid = new ScriptedBackend();
            Assert.That(Store(activeValid).Promote(Checkpoint(Mid)).Succeeded, Is.True);
            Assert.That(Store(activeValid).Promote(Checkpoint(Candidate)).Succeeded, Is.True);
            activeValid.Files[CheckpointStorageFile.DataA] = FlipMiddle(activeValid.Files[CheckpointStorageFile.DataA]);

            foreach (var backend in new[] { onlyValid, previousValid, activeValid })
            {
                var before = backend.Snapshot();
                var result = Store(backend).SupersedeDamaged(candidate);
                Assert.That(result.Succeeded, Is.False);
                Assert.That(result.Outcome, Is.EqualTo(CommandOutcome.InvalidCheckpoint));
                AssertSameFiles(backend, before, "a valid generation is never overwritten");
            }

            Assert.That(Store(previousValid).ReadPrevious().Checkpoint.Id, Is.EqualTo(Checkpoint(Mid).Id), "the valid previous generation survives");
            Assert.That(Store(activeValid).ReadActive().Checkpoint.Id, Is.EqualTo(Checkpoint(Candidate).Id), "the valid active generation survives");
        }

        [Test]
        public void Supersede_IsRefusedOnAStorageFailure_AndWritesNothing()
        {
            RunCheckpoint candidate = Checkpoint(Candidate);

            // A read failure on intact data must never count as damage.
            var backend = new ScriptedBackend();
            Assert.That(Store(backend).Promote(Checkpoint(Mid)).Succeeded, Is.True);
            var before = backend.Snapshot();
            backend.FailOnFirst("ReadExactly", CheckpointStorageFile.DataA);
            var read = Store(backend).SupersedeDamaged(candidate);
            Assert.That(backend.FaultTriggered, Is.True);
            Assert.That(read.Outcome, Is.EqualTo(CommandOutcome.PersistenceFailed));
            backend.ClearFault();
            AssertSameFiles(backend, before, "a transient read failure never allows an overwrite");
            Assert.That(Store(backend).ReadActive().Checkpoint.Id, Is.EqualTo(Checkpoint(Mid).Id));

            // The whole store failing, on torn data.
            var torn = TornSoleGeneration();
            var tornBefore = torn.Snapshot();
            torn.FailAll = true;
            Assert.That(Store(torn).SupersedeDamaged(candidate).Outcome, Is.EqualTo(CommandOutcome.PersistenceFailed));
            Assert.That(Store(torn).NextSupersedeGeneration().Outcome, Is.EqualTo(CommandOutcome.PersistenceFailed));
            torn.ClearFault();
            AssertSameFiles(torn, tornBefore, "nothing was written");

            // A failure reading the previous data of a torn active generation.
            var twoGenerations = new ScriptedBackend();
            Assert.That(Store(twoGenerations).Promote(Checkpoint(Mid)).Succeeded, Is.True);
            Assert.That(Store(twoGenerations).Promote(Checkpoint(Candidate)).Succeeded, Is.True);
            twoGenerations.Files[CheckpointStorageFile.DataB] = FlipMiddle(twoGenerations.Files[CheckpointStorageFile.DataB]);
            var twoBefore = twoGenerations.Snapshot();
            twoGenerations.FailOnFirst("ReadExactly", CheckpointStorageFile.DataA);
            Assert.That(Store(twoGenerations).SupersedeDamaged(Checkpoint(Post)).Outcome, Is.EqualTo(CommandOutcome.PersistenceFailed));
            twoGenerations.ClearFault();
            AssertSameFiles(twoGenerations, twoBefore, "an unreadable previous generation due to a storage failure is not damage");
        }

        [Test]
        public void Supersede_WithoutASelectableManifest_IsRefused_AndTheOrdinaryPromotionApplies()
        {
            var empty = new ScriptedBackend();
            Assert.That(Store(empty).SupersedeDamaged(Checkpoint(Mid)).Outcome, Is.EqualTo(CommandOutcome.NoCheckpoint));
            Assert.That(Store(empty).NextSupersedeGeneration().Outcome, Is.EqualTo(CommandOutcome.NoCheckpoint));
            Assert.That(empty.Files, Is.Empty);

            var tornManifest = TornSoleGeneration();
            tornManifest.Files[CheckpointStorageFile.ManifestA] = FlipMiddle(tornManifest.Files[CheckpointStorageFile.ManifestA]);
            var before = tornManifest.Snapshot();
            Assert.That(Store(tornManifest).SupersedeDamaged(Checkpoint(Candidate)).Succeeded, Is.False);
            AssertSameFiles(tornManifest, before, "nothing was written");
            Assert.That(Store(tornManifest).Promote(Checkpoint(Mid)).Succeeded, Is.True, "the ordinary protocol still repairs this state");
        }

        [Test]
        public void Supersede_RejectsACandidateThatDoesNotAdvanceTheGeneration_OrReusesAnIdentity()
        {
            var backend = TornSoleGeneration();
            var before = backend.Snapshot();
            var store = Store(backend);

            Assert.That(store.SupersedeDamaged(Checkpoint(Mid)).Succeeded, Is.False, "the damaged generation's own identity");
            Assert.That(store.SupersedeDamaged(null).Succeeded, Is.False);
            RunCheckpoint plain = Checkpoint(Candidate);
            var older = new RunCheckpoint(plain.Id, Mid.Meta.Generation, plain.DepartureUtcMs, plain.OfflinePolicyRevision,
                plain.ReconciledFromCheckpointId, plain.ContentRevision, plain.Import, plain.Run, plain.Permanent);
            Assert.That(store.SupersedeDamaged(older).Succeeded, Is.False, "the generation must advance");

            AssertSameFiles(backend, before, "nothing was written");
        }

        [Test]
        public void Supersede_NeverTouchesTheV1Directory()
        {
            string v1Directory = Path.Combine(_root, "IdleFactory", "Checkpoints", "v1");
            Directory.CreateDirectory(v1Directory);
            var v1Files = new Dictionary<string, byte[]>
            {
                { "checkpoint.data-a.json", new byte[] { 1, 2, 3, 4 } },
                { "checkpoint.manifest-a.bin", new byte[] { 9, 8, 7 } },
            };
            foreach (var entry in v1Files) File.WriteAllBytes(Path.Combine(v1Directory, entry.Key), entry.Value);
            var v1Stamps = v1Files.Keys.ToDictionary(name => name, name => File.GetLastWriteTimeUtc(Path.Combine(v1Directory, name)));

            var store = DiskStore();
            AssertPromoted(store, Mid);
            string data = Path.Combine(RunCheckpointTestSupport.V2Directory(_root), SaveV2CheckpointFiles.DataAFileName);
            File.WriteAllBytes(data, FlipMiddle(File.ReadAllBytes(data)));
            Assert.That(DiskStore().ReadActive().Outcome, Is.EqualTo(CommandOutcome.InvalidCheckpoint));

            var result = DiskStore().SupersedeDamaged(Checkpoint(Candidate));

            Assert.That(result.Succeeded, Is.True, result.Message);
            Assert.That(DiskStore().ReadActive().Checkpoint.Id, Is.EqualTo(Checkpoint(Candidate).Id));
            Assert.That(Directory.GetFiles(v1Directory).Length, Is.EqualTo(v1Files.Count), "no file was added to v1");
            foreach (var entry in v1Files)
            {
                string path = Path.Combine(v1Directory, entry.Key);
                Assert.That(File.ReadAllBytes(path), Is.EqualTo(entry.Value), entry.Key);
                Assert.That(File.GetLastWriteTimeUtc(path), Is.EqualTo(v1Stamps[entry.Key]), entry.Key);
            }
        }

        [Test]
        public void DeviceSet_W003S_LoadsAsAValidActiveCheckpointWithAFarPastDeparture()
        {
            string directory = Path.Combine(Application.dataPath, "IdleFactory/Tests/EditMode/Fixtures/SaveV2", SaveV2DeviceSets.W003SDirectory);
            var backend = new ScriptedBackend();
            backend.Files[CheckpointStorageFile.DataA] = File.ReadAllBytes(Path.Combine(directory, SaveV2CheckpointFiles.DataAFileName));
            backend.Files[CheckpointStorageFile.ManifestA] = File.ReadAllBytes(Path.Combine(directory, SaveV2CheckpointFiles.ManifestAFileName));
            SaveV2Fixture w003s = SaveV2Scenarios.All().Single(f => f.FileName == "06-w003-s-stress.json");
            var store = new JsonRunCheckpointStore(Memory, w003s.Catalog, backend);

            var read = store.ReadActive();
            Assert.That(read.Succeeded, Is.True, read.Message);
            Assert.That(RunCheckpointTestSupport.BytesOf(read.Checkpoint, w003s), Is.EqualTo(w003s.Bytes), "the data file is the 06 fixture");
            Assert.That(read.Checkpoint.Run.Machines.Count, Is.EqualTo(12));
            long eightHoursMs = 8L * 3600 * 1000;
            long nowMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            Assert.That(nowMs - read.Checkpoint.DepartureUtcMs, Is.GreaterThan(eightHoursMs * 3),
                "a cold start always reconciles the capped eight hours (T123)");
            Assert.That(store.ProbeFiles().Value, Is.EqualTo(RunCheckpointPresence.PublishedFiles));
        }
    }
}
