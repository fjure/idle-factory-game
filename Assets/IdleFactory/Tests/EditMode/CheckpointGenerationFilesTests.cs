using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using IdleFactory.Core.Application;
using IdleFactory.Domain;
using IdleFactory.Run.Application;
using IdleFactory.Tests.EditMode.Fixtures;
using IdleFactory.Unity.Persistence;
using NUnit.Framework;
using UnityEngine;

namespace IdleFactory.Tests.EditMode
{
    /// <summary>
    /// T093 (FR-027, FR-028, SC-005, R08): the format-independent generation protocol in
    /// <see cref="CheckpointGenerationFiles{TCheckpoint}"/> with a fake codec and every step failing
    /// individually; the read-only <see cref="SaveV1CheckpointSource"/> behind a write-denying file-system
    /// fake and read-only files on disk; and a golden characterization of the Save V1 read path over the six
    /// committed V1 fixtures (OPS-3). The golden constants were captured on the unmodified base commit
    /// 7855fae, before the extraction, by the same procedure <see cref="PublishTwoGenerations"/> runs.
    /// </summary>
    public sealed class CheckpointGenerationFilesTests
    {
        private const string FixtureDirectoryRelativeToAssets = "IdleFactory/Tests/EditMode/Fixtures/SaveV1";
        private const string Root = "checkpoint-generation-test-root";

        // Any 163-byte buffer: flipped, it can never carry a valid SHA-256 trailer.
        private static readonly byte[] ManifestPlaceholder = new byte[163];

        // ---------------------------------------------------------------------------------------------
        // Golden characterization of the Save V1 read path (OPS-3)
        // ---------------------------------------------------------------------------------------------

        private sealed class GoldenCase
        {
            public GoldenCase(
                int index, string name, string fixtureSha, string manifestASha, string dataBSha, string manifestBSha)
            {
                Index = index;
                Name = name;
                FixtureSha = fixtureSha;
                ManifestASha = manifestASha;
                DataBSha = dataBSha;
                ManifestBSha = manifestBSha;
            }

            public int Index { get; }
            public string Name { get; }
            public string FixtureSha { get; }
            public string ManifestASha { get; }
            public string DataBSha { get; }
            public string ManifestBSha { get; }
            public string FixtureId => "11111111-1111-1111-1111-11111111000" + Index;
            public string SecondId => "7e570000-0000-0000-0000-00000000000" + Index;

            // The selections exactly as the base commit's manifests encode them (magic, sequence, references).
            public string FirstSelection =>
                "ManifestA IFCPMF01 seq=1 active=DataA/" + FixtureId + "/0/" + FixtureSha + " previous=none";

            public string SecondSelection =>
                "ManifestB IFCPMF01 seq=2 active=DataB/" + SecondId + "/1/" + DataBSha +
                " previous=DataA/" + FixtureId + "/0/" + FixtureSha;

            public override string ToString() => Name;
        }

        // Captured on 7855fae (pre-extraction JsonCheckpointStore) on 2026-09-29.
        private static readonly GoldenCase[] GoldenCases =
        {
            new GoldenCase(1, "01-empty-factory.json",
                "b359a6232b95f2af1fd04916362b8ca8714b46f6edff5df51abeca28538f8e6f",
                "15691f6e116be9b5fab79f0818dcd0a50e2900ff28d0d885cd432a06dc0b0dbe",
                "0ac28d5b7e63639ce97f36f9b7d97bb28fa26fbfa3e8e738d23a85a9a1bb641d",
                "4f11fc8c3f42914b7ecb0965f5bb17822a73f607e237f535e6a8342aec32bab7"),
            new GoldenCase(2, "02-idle-placed-d001.json",
                "5e031431eabce7ce58b8e570f1ef635a17b3e08e19ca2fc043a22d76e8f2d706",
                "a6cfb316c848b2394ab50517d23581484495dd34d0599fae26eb7200ca12b454",
                "e390c04b44c7d34bd24b07ac05da79f4178177e5ed66e8634c1470e11dd145ff",
                "f120d2ff996ea8dbedb548345d1bfdb3ab7227c793b5209d3704d8dfa3bae6cf"),
            new GoldenCase(3, "03-d001-mid-cycle-partial-progress.json",
                "525a7eb757a82a2bd2d8e1bbfc603f11674ea88a6a598587a5f4080d7ce1e7cc",
                "b6babbcaf9498155167600c81b86542e2cf70f97ca1ecfa69a4935d28d27dd59",
                "041793661415418baba28f7a76a9b638cef9323647b4158873d2914fb157b57e",
                "65c33523f02a5b0dd69e95213edac94c3f2b1c3cbd44a5e62104f0708fa7a7ac"),
            new GoldenCase(4, "04-d001-blocked-output-full.json",
                "460e1fc054d2aa1360b5b94f0673753a5ea484cf0bd74c6f0095bbbbf0c4f5e9",
                "ee0b56a85c1723037aea317b159a55499fb735c080f4024de013d36dabdf270d",
                "578bb2f92e2948fd5c40672f37737142afeac064c5a8c02a8dd25299334e77d2",
                "1ad7fc5d168c71681fc543a6819f49bc6c959dd9e56783b02d164587c01ef817"),
            new GoldenCase(5, "05-checkpoint-departure-offline-return.json",
                "3954d221debdb7d50a32b7b243b8f1567f2dfd42c9327211dc75be61da7723f2",
                "11e7a3a94164c83574812559862b42530fba60140c02714dd735c173e47d2c90",
                "c0cb90b03ebd4fe6529d998cb6a5d1bb5b26b778488dd51dc3ea02393aa4f0fd",
                "c9c6c03e7c3fe3cdd75f92346fb73d0c2e638401b6cf2dfd94284cf2dc0d99b5"),
            new GoldenCase(6, "06-w001-scale-checkpoint.json",
                "8961f2fdb52394aa10052717a62032650a7a7e8ab4af5c56681468fb789acb5d",
                "996ac39ba0e63fecc28bdfd47cc8f01724c554a261fc572cb0cb9f620adf75a0",
                "e1f584a69d7ae14116113066b8b87f757a769e7fdbbf0086d07b2913329aba10",
                "c7092aaa8c641cdec045c6e0baf960c851bca2f6901c1b5bbfb3bc264429fa3c"),
        };

        private static IEnumerable<object> Golden() => GoldenCases;

        [TestCaseSource(nameof(Golden))]
        public void SaveV1ReadPathSelectionAndBytesMatchThePreExtractionGolden(object value)
        {
            // NUnit case sources pass object so the case type can stay private (focused-runner rule).
            var golden = (GoldenCase)value;
            var fixtureBytes = File.ReadAllBytes(FixturePath(golden.Name));
            Assert.That(Sha(fixtureBytes), Is.EqualTo(golden.FixtureSha), "The committed fixture changed.");
            var catalog = CatalogFor(golden);

            var root = TempRoot("golden");
            try
            {
                var directory = SaveV1CheckpointFiles.Layout.DirectoryFor(root);
                var legacy = new JsonCheckpointStore(root, catalog);
                var fixture = SaveV1Mapper.Deserialize(fixtureBytes, catalog);
                Assert.That(fixture.Succeeded, Is.True);

                Assert.That(legacy.Promote(fixture.Checkpoint).Succeeded, Is.True);
                Assert.That(DiskSelection(directory), Is.EqualTo(golden.FirstSelection));
                Assert.That(FileHashes(directory), Is.EqualTo(new Dictionary<string, string>
                {
                    [JsonCheckpointStore.DataAFileName] = golden.FixtureSha,
                    [JsonCheckpointStore.ManifestAFileName] = golden.ManifestASha,
                    [JsonCheckpointStore.PendingFileName] = golden.FixtureSha,
                }));
                AssertSourceRead(root, catalog, golden.FirstSelection, golden.FixtureSha);

                Assert.That(legacy.Promote(SecondGeneration(fixture.Checkpoint, golden)).Succeeded, Is.True);
                Assert.That(DiskSelection(directory), Is.EqualTo(golden.SecondSelection));
                var afterSecond = FileHashes(directory);
                Assert.That(afterSecond, Is.EqualTo(new Dictionary<string, string>
                {
                    [JsonCheckpointStore.DataAFileName] = golden.FixtureSha,
                    [JsonCheckpointStore.DataBFileName] = golden.DataBSha,
                    [JsonCheckpointStore.ManifestAFileName] = golden.ManifestASha,
                    [JsonCheckpointStore.ManifestBFileName] = golden.ManifestBSha,
                    [JsonCheckpointStore.PendingFileName] = golden.DataBSha,
                }));
                AssertSourceRead(root, catalog, golden.SecondSelection, golden.DataBSha);

                var previous = legacy.ReadPrevious();
                Assert.That(previous.Succeeded, Is.True);
                Assert.That(Sha(SaveV1Mapper.Serialize(previous.Checkpoint).Value), Is.EqualTo(golden.FixtureSha));
                Assert.That(FileHashes(directory), Is.EqualTo(afterSecond), "Reading must not change a V1 byte.");
            }
            finally
            {
                DeleteRoot(root);
            }
        }

        [Test]
        public void GoldenProcedureIsTheSameForTheLegacyStoreAndTheReadOnlySource()
        {
            // The same two-generation publication through an in-memory backend yields the same bytes and
            // selection as the real file system, so the golden is a property of the protocol, not of System.IO.
            var golden = GoldenCases[2];
            var catalog = CatalogFor(golden);
            var state = new FaultBackend("memory-golden");
            var legacy = new JsonCheckpointStore(Root, catalog, state);
            PublishTwoGenerations(legacy, golden, catalog);

            Assert.That(Sha(state.Files[CheckpointStorageFile.ManifestA]), Is.EqualTo(golden.ManifestASha));
            Assert.That(Sha(state.Files[CheckpointStorageFile.ManifestB]), Is.EqualTo(golden.ManifestBSha));
            Assert.That(Sha(state.Files[CheckpointStorageFile.DataB]), Is.EqualTo(golden.DataBSha));
            var read = new SaveV1CheckpointSource(state, catalog).ReadActive();
            Assert.That(read.Succeeded, Is.True);
            Assert.That(ApiSelection(read.Selection), Is.EqualTo(WithoutMagic(golden.SecondSelection)));
        }

        // ---------------------------------------------------------------------------------------------
        // Read-only Save V1 source (FR-028, SC-005)
        // ---------------------------------------------------------------------------------------------

        [TestCaseSource(nameof(Golden))]
        public void SaveV1SourceNeverWritesThroughAWriteDenyingFileSystem(object value)
        {
            var golden = (GoldenCase)value;
            var catalog = CatalogFor(golden);
            var seed = new FaultBackend("memory-seed");
            PublishTwoGenerations(new JsonCheckpointStore(Root, catalog, seed), golden, catalog);

            var denying = new WriteDenyingFileSystem("write-denying-v1", seed.Files);
            var before = denying.Snapshot();
            ILegacyCheckpointSource source = new SaveV1CheckpointSource(denying, catalog);

            for (var attempt = 0; attempt < 3; attempt++)
            {
                Assert.That(source.TryReadActive(out var checkpoint, out var outcome), Is.True);
                Assert.That(outcome, Is.EqualTo(CommandOutcome.Success));
                Assert.That(Sha(SaveV1Mapper.Serialize(checkpoint).Value), Is.EqualTo(golden.DataBSha));
            }

            Assert.That(denying.WriteAttempts, Is.Zero);
            Assert.That(denying.Snapshot(), Is.EqualTo(before));
        }

        private static IEnumerable<TestCaseData> DamagedV1States()
        {
            yield return new TestCaseData("absent", CommandOutcome.NoCheckpoint).SetName("V1SourceAbsent");
            yield return new TestCaseData("pending-only", CommandOutcome.NoCheckpoint).SetName("V1SourcePendingOnly");
            yield return new TestCaseData("corrupt-manifests", CommandOutcome.InvalidCheckpoint).SetName("V1SourceCorruptManifests");
            yield return new TestCaseData("corrupt-active", CommandOutcome.InvalidCheckpoint).SetName("V1SourceCorruptActive");
            yield return new TestCaseData("equal-sequences", CommandOutcome.InvalidCheckpoint).SetName("V1SourceEqualSequences");
            yield return new TestCaseData("unreadable", CommandOutcome.PersistenceFailed).SetName("V1SourceUnreadable");
        }

        [TestCaseSource(nameof(DamagedV1States))]
        public void SaveV1SourceFailsClosedWithoutWritingOnDamagedState(string damage, CommandOutcome expected)
        {
            var golden = GoldenCases[1];
            var catalog = CatalogFor(golden);
            var seed = new FaultBackend("memory-seed");
            PublishTwoGenerations(new JsonCheckpointStore(Root, catalog, seed), golden, catalog);
            var files = new Dictionary<CheckpointStorageFile, byte[]>(seed.Files);
            switch (damage)
            {
                case "absent":
                    files.Clear();
                    break;
                case "pending-only":
                    files.Remove(CheckpointStorageFile.DataA);
                    files.Remove(CheckpointStorageFile.DataB);
                    files.Remove(CheckpointStorageFile.ManifestA);
                    files.Remove(CheckpointStorageFile.ManifestB);
                    break;
                case "corrupt-manifests":
                    files[CheckpointStorageFile.ManifestA] = Flip(files[CheckpointStorageFile.ManifestA]);
                    files[CheckpointStorageFile.ManifestB] = Flip(files[CheckpointStorageFile.ManifestB]);
                    break;
                case "corrupt-active":
                    files[CheckpointStorageFile.DataB] = Flip(files[CheckpointStorageFile.DataB]);
                    break;
                case "equal-sequences":
                    files[CheckpointStorageFile.ManifestA] = files[CheckpointStorageFile.ManifestB];
                    break;
            }

            var denying = new WriteDenyingFileSystem("write-denying-damaged", files)
            {
                FailReads = damage == "unreadable",
            };
            var before = denying.Snapshot();
            ILegacyCheckpointSource source = new SaveV1CheckpointSource(denying, catalog);

            Assert.That(source.TryReadActive(out var checkpoint, out var outcome), Is.False);
            Assert.That(checkpoint, Is.Null);
            Assert.That(outcome, Is.EqualTo(expected));
            Assert.That(denying.WriteAttempts, Is.Zero);
            Assert.That(denying.Snapshot(), Is.EqualTo(before));
        }

        [Test]
        public void SaveV1SourceReadsReadOnlyFilesOnDiskAndLeavesEveryByteAndTimestampUnchanged()
        {
            var golden = GoldenCases[4];
            var catalog = CatalogFor(golden);
            var root = TempRoot("readonly");
            var directory = SaveV1CheckpointFiles.Layout.DirectoryFor(root);
            try
            {
                PublishTwoGenerations(new JsonCheckpointStore(root, catalog), golden, catalog);
                foreach (var path in Directory.GetFiles(directory))
                {
                    File.SetAttributes(path, File.GetAttributes(path) | FileAttributes.ReadOnly);
                }

                var before = DirectoryState(directory);
                var source = new SaveV1CheckpointSource(root, catalog);
                for (var attempt = 0; attempt < 3; attempt++)
                {
                    Assert.That(source.TryReadActive(out var checkpoint, out var outcome), Is.True, outcome.ToString());
                    Assert.That(Sha(SaveV1Mapper.Serialize(checkpoint).Value), Is.EqualTo(golden.DataBSha));
                }

                Assert.That(DirectoryState(directory), Is.EqualTo(before));
            }
            finally
            {
                if (Directory.Exists(directory))
                {
                    foreach (var path in Directory.GetFiles(directory)) File.SetAttributes(path, FileAttributes.Normal);
                }

                DeleteRoot(root);
            }
        }

        [Test]
        public void SaveV1SourceDoesNotCreateAMissingV1Directory()
        {
            var root = TempRoot("missing");
            try
            {
                var source = new SaveV1CheckpointSource(root, AcceptedFoundationCatalogFixture.CreateCatalog());
                Assert.That(source.TryReadActive(out var checkpoint, out var outcome), Is.False);
                Assert.That(checkpoint, Is.Null);
                Assert.That(outcome, Is.EqualTo(CommandOutcome.NoCheckpoint));
                Assert.That(Directory.Exists(root), Is.False, "A read-only source must not create directories.");
            }
            finally
            {
                DeleteRoot(root);
            }
        }

        [Test]
        public void SaveV1SourceHoldsNoWriteCapableDependencyAndDescribesItselfWithoutPaths()
        {
            var forbidden = new[] { typeof(ICheckpointStorageBackend), typeof(JsonCheckpointStore), typeof(ICheckpointStore) };
            const BindingFlags all = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;

            foreach (var type in new[] { typeof(SaveV1CheckpointSource), typeof(CheckpointGenerationReader<Checkpoint>) })
            {
                foreach (var field in type.GetFields(all))
                {
                    Assert.That(forbidden.Any(candidate => candidate.IsAssignableFrom(field.FieldType)), Is.False,
                        type.Name + "." + field.Name);
                    Assert.That(field.FieldType.IsGenericType &&
                                field.FieldType.GetGenericTypeDefinition() == typeof(CheckpointGenerationFiles<>), Is.False,
                        type.Name + "." + field.Name);
                }

                foreach (var parameter in type.GetConstructors(all).SelectMany(constructor => constructor.GetParameters()))
                {
                    Assert.That(forbidden.Any(candidate => candidate.IsAssignableFrom(parameter.ParameterType)), Is.False,
                        type.Name + " constructor parameter " + parameter.Name);
                }
            }

            Assert.That(typeof(ILegacyCheckpointSource).GetMethods().Select(method => method.Name),
                Is.EquivalentTo(new[] { "get_Description", "TryReadActive" }));
            var root = TempRoot("describe");
            var source = new SaveV1CheckpointSource(root, AcceptedFoundationCatalogFixture.CreateCatalog());
            Assert.That(source.Description, Is.EqualTo(SaveV1CheckpointSource.SourceDescription));
            Assert.That(source.Description, Does.Not.Contain(root));
            Assert.That(source.Description, Does.Not.Contain(Path.DirectorySeparatorChar.ToString()));
            Assert.That(source.Description, Does.Not.Contain(Path.AltDirectorySeparatorChar.ToString()));
            Assert.That(typeof(ILegacyCheckpointSource).Assembly.GetName().Name, Is.EqualTo("IdleFactory.Core"));
            Assert.That(typeof(ILegacyCheckpointSource).Namespace, Is.EqualTo("IdleFactory.Run.Application"));
        }

        [Test]
        public void SaveV1SlotNamesAndDirectoryAreUnchanged()
        {
            Assert.That(JsonCheckpointStore.DataAFileName, Is.EqualTo("factory-checkpoint.data-a.json"));
            Assert.That(JsonCheckpointStore.DataBFileName, Is.EqualTo("factory-checkpoint.data-b.json"));
            Assert.That(JsonCheckpointStore.ManifestAFileName, Is.EqualTo("factory-checkpoint.manifest-a.bin"));
            Assert.That(JsonCheckpointStore.ManifestBFileName, Is.EqualTo("factory-checkpoint.manifest-b.bin"));
            Assert.That(JsonCheckpointStore.PendingFileName, Is.EqualTo("factory-checkpoint.pending.json"));
            var root = Path.GetFullPath(TempRoot("layout"));
            Assert.That(SaveV1CheckpointFiles.Layout.DirectoryFor(root),
                Is.EqualTo(Path.Combine(root, "IdleFactory", "Checkpoints", "v1")));
        }

        // ---------------------------------------------------------------------------------------------
        // Shared protocol with a fake codec (R08)
        // ---------------------------------------------------------------------------------------------

        [Test]
        public void ProtocolAlternatesSlotsAndRetainsThePreviousGeneration()
        {
            var backend = new FaultBackend("fake-alternation");
            var files = Files(backend);

            Assert.That(files.ReadActive().Outcome, Is.EqualTo(CommandOutcome.NoCheckpoint));
            Assert.That(files.Promote(Fake(1, "one")).Succeeded, Is.True);
            AssertSelection(files, CheckpointStorageFile.ManifestA, 1, CheckpointStorageFile.DataA, 1, null);
            Assert.That(files.ReadPrevious().Outcome, Is.EqualTo(CommandOutcome.NoCheckpoint));

            Assert.That(files.Promote(Fake(2, "two")).Succeeded, Is.True);
            AssertSelection(files, CheckpointStorageFile.ManifestB, 2, CheckpointStorageFile.DataB, 2, 1L);

            Assert.That(files.Promote(Fake(3, "three")).Succeeded, Is.True);
            AssertSelection(files, CheckpointStorageFile.ManifestA, 3, CheckpointStorageFile.DataA, 3, 2L);
            Assert.That(files.ReadPrevious().Checkpoint.Payload, Is.EqualTo("two"));
            Assert.That(files.Read(FakeId(2)).Checkpoint.Payload, Is.EqualTo("two"));
            Assert.That(files.Read(FakeId(1)).Outcome, Is.EqualTo(CommandOutcome.NoCheckpoint), "Only two generations are retained.");
            Assert.That(backend.Files[CheckpointStorageFile.Pending], Is.EqualTo(FakeCodec.Encode(Fake(3, "three"))));
        }

        [Test]
        public void ProtocolRunsPendingReadbackRetainPromoteVerifyInOrder()
        {
            var backend = new FaultBackend("fake-order");
            var files = Files(backend);
            Assert.That(files.Promote(Fake(1, "one")).Succeeded, Is.True);
            backend.Log.Clear();

            Assert.That(files.Promote(Fake(2, "two")).Succeeded, Is.True);
            var writes = backend.Log.Where(entry => !entry.StartsWith("Exists", StringComparison.Ordinal) &&
                                                     !entry.StartsWith("GetLength", StringComparison.Ordinal)).ToArray();
            Assert.That(writes, Is.EqualTo(new[]
            {
                // select current, read active, read retained generation
                "ReadExactly:ManifestA", "ReadExactly:DataA", "ReadExactly:DataA",
                // pending, then read back through the codec
                "Create:Pending", "Write:Pending", "Flush:Pending", "Close:Pending", "ReadExactly:Pending",
                // retain previous: stabilize the retained data and manifest, then re-verify them
                "FlushExisting:DataA", "FlushExisting:ManifestA", "ReadExactly:DataA", "ReadExactly:ManifestA",
                // promote into the free data slot and read back
                "Create:DataB", "Write:DataB", "Flush:DataB", "Close:DataB", "ReadExactly:DataB",
                // next manifest into the free manifest slot and read back
                "Create:ManifestB", "Write:ManifestB", "Flush:ManifestB", "Close:ManifestB", "ReadExactly:ManifestB",
                // verify the published selection and active data
                "ReadExactly:ManifestA", "ReadExactly:ManifestB", "ReadExactly:DataB",
            }));
        }

        private static IEnumerable<TestCaseData> ProtocolStepFailures()
        {
            // write
            yield return Step("write.pending.create", "Create", CheckpointStorageFile.Pending, 1, false);
            yield return Step("write.pending.write", "Write", CheckpointStorageFile.Pending, 1, false);
            yield return Step("write.data.write", "Write", CheckpointStorageFile.DataB, 1, false);
            yield return Step("write.close", "Close", CheckpointStorageFile.DataB, 1, false);
            // flush
            yield return Step("flush.pending", "Flush", CheckpointStorageFile.Pending, 1, false);
            yield return Step("flush.data", "Flush", CheckpointStorageFile.DataB, 1, false);
            yield return Step("flush.retained-data", "FlushExisting", CheckpointStorageFile.DataA, 1, false);
            yield return Step("flush.retained-manifest", "FlushExisting", CheckpointStorageFile.ManifestA, 1, false);
            // read back
            yield return Step("readback.pending", "ReadExactly", CheckpointStorageFile.Pending, 1, false);
            yield return Step("readback.data", "ReadExactly", CheckpointStorageFile.DataB, 1, false);
            // manifest
            yield return Step("manifest.create", "Create", CheckpointStorageFile.ManifestB, 1, false);
            yield return Step("manifest.write", "Write", CheckpointStorageFile.ManifestB, 1, false);
            // The manifest bytes are complete when its flush fails, so the new generation may already be selected.
            yield return Step("manifest.flush", "Flush", CheckpointStorageFile.ManifestB, 1, true);
            yield return Step("manifest.readback", "ReadExactly", CheckpointStorageFile.ManifestB, 1, true);
            // verify
            yield return Step("verify.selection", "ReadExactly", CheckpointStorageFile.ManifestB, 2, true);
            yield return Step("verify.active", "ReadExactly", CheckpointStorageFile.DataB, 2, true);
        }

        private static TestCaseData Step(string name, string operation, CheckpointStorageFile file, int occurrence, bool afterManifest) =>
            new TestCaseData(operation, file, occurrence, afterManifest).SetName("ProtocolStepFails_" + name);

        [TestCaseSource(nameof(ProtocolStepFailures))]
        public void EachProtocolStepFailingIndividuallyReportsFailureKeepsGenerationsAndRetries(
            string operation, CheckpointStorageFile file, int occurrence, bool afterManifest)
        {
            var backend = new FaultBackend("fake-step-" + operation + file + occurrence);
            var files = Files(backend);
            Assert.That(files.Promote(Fake(1, "one")).Succeeded, Is.True);
            var retainedBytes = (byte[])backend.Files[CheckpointStorageFile.DataA].Clone();

            backend.FailAt(operation, file, occurrence);
            var failed = files.Promote(Fake(2, "two"));
            Assert.That(backend.FaultTriggered, Is.True, "The injected step was not reached.");
            Assert.That(failed.Succeeded, Is.False);
            Assert.That(failed.Outcome, Is.EqualTo(CommandOutcome.PersistenceFailed));
            Assert.That(failed.Message, Does.Not.Contain("two"));
            backend.ClearFault();

            var reopened = Files(backend);
            var active = reopened.ReadActive();
            Assert.That(active.Succeeded, Is.True);
            if (afterManifest)
            {
                Assert.That(active.Checkpoint.Generation, Is.EqualTo(1L).Or.EqualTo(2L));
            }
            else
            {
                Assert.That(active.Checkpoint.Generation, Is.EqualTo(1L), "No generation is published before its manifest.");
                Assert.That(active.Selection.ManifestFile, Is.EqualTo(CheckpointStorageFile.ManifestA));
            }

            Assert.That(backend.Files[CheckpointStorageFile.DataA], Is.EqualTo(retainedBytes), "The retained generation was overwritten.");

            var retried = reopened.Promote(Fake(2, "two"));
            Assert.That(retried.Succeeded, Is.True, retried.Outcome.ToString());
            AssertSelection(reopened, CheckpointStorageFile.ManifestB, 2, CheckpointStorageFile.DataB, 2, 1L);
        }

        private static IEnumerable<TestCaseData> ReadbackMismatches()
        {
            yield return new TestCaseData(CheckpointStorageFile.Pending, 1).SetName("ReadbackMismatch_pending");
            yield return new TestCaseData(CheckpointStorageFile.DataB, 1).SetName("ReadbackMismatch_data");
            yield return new TestCaseData(CheckpointStorageFile.ManifestB, 1).SetName("ReadbackMismatch_manifest");
            yield return new TestCaseData(CheckpointStorageFile.DataB, 2).SetName("ReadbackMismatch_verify");
        }

        [TestCaseSource(nameof(ReadbackMismatches))]
        public void ReadBackBytesThatDifferFromTheCandidateAreRejectedAsInvalid(CheckpointStorageFile file, int occurrence)
        {
            var backend = new FaultBackend("fake-mismatch-" + file + occurrence);
            var files = Files(backend);
            Assert.That(files.Promote(Fake(1, "one")).Succeeded, Is.True);

            // A valid, same-length encoding of a different checkpoint, or damaged manifest bytes of the right length.
            backend.SubstituteRead(file, occurrence, file == CheckpointStorageFile.ManifestB
                ? Flip(ManifestPlaceholder)
                : FakeCodec.Encode(Fake(2, "owt")));
            var result = files.Promote(Fake(2, "two"));

            Assert.That(backend.FaultTriggered, Is.True);
            Assert.That(result.Outcome, Is.EqualTo(CommandOutcome.InvalidCheckpoint));
            backend.ClearFault();
            var active = Files(backend).ReadActive();
            Assert.That(active.Succeeded, Is.True);
            Assert.That(active.Checkpoint.Generation, Is.EqualTo(1L).Or.EqualTo(2L));
            Assert.That(active.Checkpoint.Payload, Is.Not.EqualTo("owt"), "Substituted bytes are never published.");
        }

        [Test]
        public void CodecSerializeFailureWritesNothing()
        {
            var backend = new FaultBackend("fake-serialize");
            var codec = new FakeCodec();
            var files = new CheckpointGenerationFiles<FakeCheckpoint>(backend, codec);
            Assert.That(files.Promote(Fake(1, "one")).Succeeded, Is.True);
            backend.Log.Clear();

            codec.FailSerialize = true;
            var result = files.Promote(Fake(2, "two"));

            Assert.That(result.Outcome, Is.EqualTo(CommandOutcome.InvalidCheckpoint));
            Assert.That(backend.Log, Is.Empty, "A candidate that cannot be encoded never touches storage.");
        }

        [Test]
        public void CodecValidationRejectingThePendingCandidateNeverPublishes()
        {
            var backend = new FaultBackend("fake-validate");
            var codec = new FakeCodec();
            var files = new CheckpointGenerationFiles<FakeCheckpoint>(backend, codec);
            Assert.That(files.Promote(Fake(1, "one")).Succeeded, Is.True);
            backend.Log.Clear();

            var result = files.Promote(Fake(2, FakeCodec.RejectedPayload));

            Assert.That(result.Outcome, Is.EqualTo(CommandOutcome.InvalidCheckpoint));
            Assert.That(backend.Log, Does.Contain("Create:Pending"));
            Assert.That(backend.Log.Where(entry => entry.StartsWith("Create:", StringComparison.Ordinal)),
                Is.EqualTo(new[] { "Create:Pending" }), "No data or manifest slot is written after a failed validation.");
            Assert.That(files.ReadActive().Checkpoint.Payload, Is.EqualTo("one"));
        }

        [Test]
        public void StaleGenerationsAreRejectedAndTheSameGenerationIsIdempotent()
        {
            var backend = new FaultBackend("fake-generations");
            var files = Files(backend);
            Assert.That(files.Promote(Fake(1, "one")).Succeeded, Is.True);
            Assert.That(files.Promote(Fake(2, "two")).Succeeded, Is.True);
            backend.Log.Clear();

            Assert.That(files.Promote(new FakeCheckpoint(FakeId(9), 2, "other")).Outcome, Is.EqualTo(CommandOutcome.InvalidCheckpoint));
            Assert.That(files.Promote(new FakeCheckpoint(FakeId(1), 5, "reused-id")).Outcome, Is.EqualTo(CommandOutcome.InvalidCheckpoint));
            Assert.That(files.Promote(new FakeCheckpoint(FakeId(2), 2, "changed")).Outcome, Is.EqualTo(CommandOutcome.InvalidCheckpoint));
            Assert.That(backend.Log.Any(entry => entry.StartsWith("Create:", StringComparison.Ordinal)), Is.False);

            Assert.That(files.Promote(Fake(2, "two")).Succeeded, Is.True, "Re-promoting the durable active bytes confirms them.");
            Assert.That(backend.Log.Any(entry => entry.StartsWith("Create:", StringComparison.Ordinal)), Is.False);
            Assert.That(files.Promote(null).Outcome, Is.EqualTo(CommandOutcome.InvalidCheckpoint));
        }

        [Test]
        public void RecoverPreviousPublishesAValidatedSuccessorOfThePreviousGeneration()
        {
            var backend = new FaultBackend("fake-recover");
            var files = Files(backend);
            Assert.That(files.Promote(Fake(1, "one")).Succeeded, Is.True);
            Assert.That(files.Promote(Fake(2, "two")).Succeeded, Is.True);

            FakeCheckpoint seenPrevious = null;
            Func<FakeCheckpoint, CheckpointGenerationReference, FakeCheckpoint, bool> accept = (candidate, active, previous) =>
            {
                seenPrevious = previous;
                return candidate.Generation > active.Generation;
            };

            Assert.That(files.RecoverPrevious(Fake(3, "rejected"), (candidate, active, previous) => false).Outcome,
                Is.EqualTo(CommandOutcome.InvalidCheckpoint));
            var recovered = files.RecoverPrevious(Fake(3, "recovered"), accept);

            Assert.That(recovered.Succeeded, Is.True, recovered.Outcome.ToString());
            Assert.That(seenPrevious.Payload, Is.EqualTo("one"));
            // The recovery overwrites the damaged active slot and keeps generation 1 as previous.
            AssertSelection(files, CheckpointStorageFile.ManifestA, 3, CheckpointStorageFile.DataB, 3, 1L);
            Assert.That(files.RecoverPrevious(Fake(3, "recovered"), accept).Succeeded, Is.True, "Recovery is idempotent.");
        }

        [Test]
        public void TheLayoutParameterizesDirectoryAndSlotNamesOnDisk()
        {
            var layout = new CheckpointFileLayout(
                "v-test", "run.data-a.json", "run.data-b.json", "run.manifest-a.bin", "run.manifest-b.bin", "run.pending.json");
            var root = TempRoot("layout-disk");
            try
            {
                var directory = layout.DirectoryFor(root);
                var files = new CheckpointGenerationFiles<FakeCheckpoint>(
                    new SystemIoCheckpointStorageBackend(directory, layout), new FakeCodec());
                Assert.That(files.Promote(Fake(1, "one")).Succeeded, Is.True);
                Assert.That(files.Promote(Fake(2, "two")).Succeeded, Is.True);

                Assert.That(Directory.GetFiles(directory).Select(Path.GetFileName), Is.EquivalentTo(new[]
                {
                    "run.data-a.json", "run.data-b.json", "run.manifest-a.bin", "run.manifest-b.bin", "run.pending.json",
                }));
                Assert.That(Directory.Exists(SaveV1CheckpointFiles.Layout.DirectoryFor(root)), Is.False);
                var reader = new CheckpointGenerationReader<FakeCheckpoint>(
                    new SystemIoCheckpointStorageReader(directory, layout), new FakeCodec());
                Assert.That(reader.ReadActive().Checkpoint.Payload, Is.EqualTo("two"));
            }
            finally
            {
                DeleteRoot(root);
            }

            Assert.That(() => new CheckpointFileLayout("v", "a", "a", "b", "c", "d"), Throws.ArgumentException);
            Assert.That(() => new CheckpointFileLayout("v/x", "a", "b", "c", "d", "e"), Throws.ArgumentException);
        }

        // ---------------------------------------------------------------------------------------------
        // Helpers
        // ---------------------------------------------------------------------------------------------

        private static void AssertSourceRead(string root, DefinitionCatalog catalog, string expectedSelection, string expectedActiveSha)
        {
            var source = new SaveV1CheckpointSource(root, catalog);
            var read = source.ReadActive();
            Assert.That(read.Succeeded, Is.True, read.Outcome.ToString());
            Assert.That(ApiSelection(read.Selection), Is.EqualTo(WithoutMagic(expectedSelection)));
            Assert.That(Sha(read.CopyBytes()), Is.EqualTo(expectedActiveSha));
            Assert.That(Sha(SaveV1Mapper.Serialize(read.Checkpoint).Value), Is.EqualTo(expectedActiveSha));
            Assert.That(source.TryReadActive(out var checkpoint, out var outcome), Is.True);
            Assert.That(outcome, Is.EqualTo(CommandOutcome.Success));
            Assert.That(Sha(SaveV1Mapper.Serialize(checkpoint).Value), Is.EqualTo(expectedActiveSha));
        }

        private static void PublishTwoGenerations(JsonCheckpointStore store, GoldenCase golden, DefinitionCatalog catalog)
        {
            var fixture = SaveV1Mapper.Deserialize(File.ReadAllBytes(FixturePath(golden.Name)), catalog);
            Assert.That(fixture.Succeeded, Is.True);
            Assert.That(store.Promote(fixture.Checkpoint).Succeeded, Is.True);
            Assert.That(store.Promote(SecondGeneration(fixture.Checkpoint, golden)).Succeeded, Is.True);
        }

        private static Checkpoint SecondGeneration(Checkpoint first, GoldenCase golden) => new Checkpoint(
            new CheckpointId(new Guid(golden.SecondId)),
            first.Generation + 1,
            first.DepartureUtc,
            first.OfflinePolicyRevision,
            null,
            first.CreateFactoryCopy());

        private static DefinitionCatalog CatalogFor(GoldenCase golden) => golden.Index == 6
            ? FoundationFixtures.CreateW001().Catalog
            : AcceptedFoundationCatalogFixture.CreateCatalog();

        private static string FixturePath(string name) =>
            Path.Combine(Application.dataPath, FixtureDirectoryRelativeToAssets, name);

        private static string ApiSelection(CheckpointGenerationSelection selection) =>
            selection.ManifestFile + " seq=" + selection.Sequence + " active=" + ApiReference(selection.Active) +
            " previous=" + (selection.Previous == null ? "none" : ApiReference(selection.Previous));

        private static string ApiReference(CheckpointGenerationReference reference) =>
            reference.File + "/" + reference.Id.Value + "/" + reference.Generation + "/" + Hex(reference.ContentHash);

        private static string WithoutMagic(string selection) => selection.Replace(" IFCPMF01", string.Empty);

        // Independent decoder of the Feature 001 manifest layout, used to pin the on-disk format.
        private static string DiskSelection(string directory)
        {
            var a = ParseManifest(Path.Combine(directory, JsonCheckpointStore.ManifestAFileName));
            var b = ParseManifest(Path.Combine(directory, JsonCheckpointStore.ManifestBFileName));
            if (a == null) return b == null ? "none" : "ManifestB " + b.Item2;
            if (b == null || a.Item1 > b.Item1) return "ManifestA " + a.Item2;
            return "ManifestB " + b.Item2;
        }

        private static Tuple<long, string> ParseManifest(string path)
        {
            if (!File.Exists(path)) return null;
            using (var reader = new BinaryReader(new MemoryStream(File.ReadAllBytes(path))))
            {
                var magic = Encoding.ASCII.GetString(reader.ReadBytes(8));
                var sequence = reader.ReadInt64();
                var active = DiskReference(reader);
                var hasPrevious = reader.ReadBoolean();
                var previous = DiskReference(reader);
                return Tuple.Create(sequence,
                    magic + " seq=" + sequence + " active=" + active + " previous=" + (hasPrevious ? previous : "none"));
            }
        }

        private static string DiskReference(BinaryReader reader)
        {
            var slot = reader.ReadByte() == 0 ? "DataA" : "DataB";
            var id = new Guid(reader.ReadBytes(16));
            var generation = reader.ReadInt64();
            var hash = reader.ReadBytes(32);
            return slot + "/" + id.ToString("D") + "/" + generation + "/" + Hex(hash);
        }

        private static Dictionary<string, string> FileHashes(string directory) => Directory.GetFiles(directory)
            .ToDictionary(path => Path.GetFileName(path), path => Sha(File.ReadAllBytes(path)));

        private static Dictionary<string, string> DirectoryState(string directory) => Directory.GetFiles(directory)
            .ToDictionary(
                path => Path.GetFileName(path),
                path => Sha(File.ReadAllBytes(path)) + "@" + File.GetLastWriteTimeUtc(path).Ticks + "/" +
                        File.GetAttributes(path));

        private static CheckpointGenerationFiles<FakeCheckpoint> Files(FaultBackend backend) =>
            new CheckpointGenerationFiles<FakeCheckpoint>(backend, new FakeCodec());

        private static void AssertSelection(
            CheckpointGenerationFiles<FakeCheckpoint> files,
            CheckpointStorageFile manifest,
            long sequence,
            CheckpointStorageFile activeFile,
            long activeGeneration,
            long? previousGeneration)
        {
            var read = files.ReadActive();
            Assert.That(read.Succeeded, Is.True, read.Outcome.ToString());
            Assert.That(read.Selection.ManifestFile, Is.EqualTo(manifest));
            Assert.That(read.Selection.Sequence, Is.EqualTo(sequence));
            Assert.That(read.Selection.Active.File, Is.EqualTo(activeFile));
            Assert.That(read.Selection.Active.Generation, Is.EqualTo(activeGeneration));
            Assert.That(read.Checkpoint.Generation, Is.EqualTo(activeGeneration));
            Assert.That(read.Selection.Previous?.Generation, Is.EqualTo(previousGeneration));
        }

        private static FakeCheckpoint Fake(long generation, string payload) =>
            new FakeCheckpoint(FakeId(generation), generation, payload);

        private static CheckpointId FakeId(long generation) =>
            new CheckpointId(new Guid("f0000000-0000-0000-0000-" + generation.ToString("D12")));

        private static string TempRoot(string purpose) =>
            Path.Combine(Path.GetTempPath(), "idle-factory-t093-" + purpose + "-" + Guid.NewGuid().ToString("N"));

        private static void DeleteRoot(string root)
        {
            if (Directory.Exists(root)) Directory.Delete(root, true);
        }

        private static byte[] Flip(byte[] bytes)
        {
            var copy = (byte[])bytes.Clone();
            copy[copy.Length / 2] ^= 0x5A;
            return copy;
        }

        private static string Sha(byte[] bytes)
        {
            using (var sha = SHA256.Create()) return Hex(sha.ComputeHash(bytes));
        }

        private static string Hex(byte[] bytes) => string.Concat(bytes.Select(value => value.ToString("x2")));

        private sealed class FakeCheckpoint
        {
            public FakeCheckpoint(CheckpointId id, long generation, string payload)
            {
                Id = id;
                Generation = generation;
                Payload = payload;
            }

            public CheckpointId Id { get; }
            public long Generation { get; }
            public string Payload { get; }
        }

        private sealed class FakeCodec : ICheckpointGenerationCodec<FakeCheckpoint>
        {
            public const string RejectedPayload = "semantically-invalid";
            private const string Prefix = "FAKE1|";

            public bool FailSerialize { get; set; }
            public int MaximumByteCount => 4096;
            public CheckpointId GetId(FakeCheckpoint checkpoint) => checkpoint.Id;
            public long GetGeneration(FakeCheckpoint checkpoint) => checkpoint.Generation;

            public static byte[] Encode(FakeCheckpoint checkpoint) => Encoding.UTF8.GetBytes(
                Prefix + checkpoint.Id.Value + "|" + checkpoint.Generation + "|" + checkpoint.Payload);

            public CommandResult<byte[]> Serialize(FakeCheckpoint checkpoint) => FailSerialize
                ? CommandResult<byte[]>.Rejected(CommandOutcome.InvalidCheckpoint, "Fake serialize failure.")
                : CommandResult<byte[]>.Success(Encode(checkpoint));

            public CheckpointDecodeResult<FakeCheckpoint> Deserialize(byte[] bytes)
            {
                var text = Encoding.UTF8.GetString(bytes);
                var parts = text.Split('|');
                if (!text.StartsWith(Prefix, StringComparison.Ordinal) || parts.Length != 4 ||
                    !Guid.TryParse(parts[1], out var id) || !long.TryParse(parts[2], out var generation) ||
                    parts[3] == RejectedPayload)
                {
                    return CheckpointDecodeResult<FakeCheckpoint>.Rejected(CommandOutcome.InvalidCheckpoint, "Fake invalid.");
                }

                return CheckpointDecodeResult<FakeCheckpoint>.Success(new FakeCheckpoint(new CheckpointId(id), generation, parts[3]));
            }

            public bool ValidateBytes(FakeCheckpoint checkpoint, byte[] bytes) => Encode(checkpoint).SequenceEqual(bytes);
        }

        /// <summary>An in-memory slot store whose Nth call of one operation on one slot can throw or return other bytes.</summary>
        private sealed class FaultBackend : ICheckpointStorageBackend
        {
            private readonly Dictionary<string, int> counts = new Dictionary<string, int>();
            private string faultKey;
            private int faultOccurrence;
            private byte[] substitute;
            private CheckpointStorageFile? open;

            public FaultBackend(string identity)
            {
                StorageIdentity = identity + "-" + Guid.NewGuid().ToString("N");
            }

            public string StorageIdentity { get; }
            public Dictionary<CheckpointStorageFile, byte[]> Files { get; } = new Dictionary<CheckpointStorageFile, byte[]>();
            public List<string> Log { get; } = new List<string>();
            public bool FaultTriggered { get; private set; }

            public void FailAt(string operation, CheckpointStorageFile file, int occurrence)
            {
                counts.Clear();
                faultKey = operation + ":" + file;
                faultOccurrence = occurrence;
                substitute = null;
                FaultTriggered = false;
            }

            public void SubstituteRead(CheckpointStorageFile file, int occurrence, byte[] bytes)
            {
                FailAt("ReadExactly", file, occurrence);
                substitute = bytes;
            }

            public void ClearFault()
            {
                faultKey = null;
                substitute = null;
            }

            public bool Exists(CheckpointStorageFile file)
            {
                Enter("Exists", file);
                return Files.ContainsKey(file);
            }

            public long GetLength(CheckpointStorageFile file)
            {
                Enter("GetLength", file);
                return Files[file].Length;
            }

            public byte[] ReadExactly(CheckpointStorageFile file, int expectedLength)
            {
                if (Enter("ReadExactly", file) && substitute != null)
                {
                    var bytes = substitute;
                    substitute = null;
                    faultKey = null;
                    return (byte[])bytes.Clone();
                }

                return (byte[])Files[file].Clone();
            }

            public void Create(CheckpointStorageFile file)
            {
                Enter("Create", file);
                open = file;
                Files[file] = new byte[0];
            }

            public void Write(CheckpointStorageFile file, byte[] bytes)
            {
                Enter("Write", file);
                if (open != file) throw new InvalidOperationException("Slot not open.");
                Files[file] = Files[file].Concat(bytes).ToArray();
            }

            public void Flush(CheckpointStorageFile file) => Enter("Flush", file);

            public void FlushExisting(CheckpointStorageFile file) => Enter("FlushExisting", file);

            public void Close(CheckpointStorageFile file)
            {
                Enter("Close", file);
                open = null;
            }

            private int Count(string key) => counts.TryGetValue(key, out var value) ? value : 0;

            // Returns true when this call is the injected substitution; throws when it is the injected failure.
            private bool Enter(string operation, CheckpointStorageFile file)
            {
                var key = operation + ":" + file;
                Log.Add(key);
                var count = Count(key) + 1;
                counts[key] = count;
                if (faultKey != key || count != faultOccurrence) return false;
                FaultTriggered = true;
                if (substitute != null) return true;
                faultKey = null;
                throw new IOException("Injected " + key + " failure.");
            }
        }

        /// <summary>
        /// A file system that serves reads and denies every write-capable call. It implements the full
        /// backend interface so that any write attempt, if one were possible, would be counted and refused.
        /// </summary>
        private sealed class WriteDenyingFileSystem : ICheckpointStorageBackend
        {
            private readonly Dictionary<CheckpointStorageFile, byte[]> files;

            public WriteDenyingFileSystem(string identity, IDictionary<CheckpointStorageFile, byte[]> files)
            {
                StorageIdentity = identity + "-" + Guid.NewGuid().ToString("N");
                this.files = files.ToDictionary(entry => entry.Key, entry => (byte[])entry.Value.Clone());
            }

            public string StorageIdentity { get; }
            public int WriteAttempts { get; private set; }
            public bool FailReads { get; set; }

            public Dictionary<CheckpointStorageFile, string> Snapshot() =>
                files.ToDictionary(entry => entry.Key, entry => Sha(entry.Value));

            public bool Exists(CheckpointStorageFile file)
            {
                if (FailReads) throw new UnauthorizedAccessException("Injected read denial.");
                return files.ContainsKey(file);
            }

            public long GetLength(CheckpointStorageFile file) => files[file].Length;
            public byte[] ReadExactly(CheckpointStorageFile file, int expectedLength) => (byte[])files[file].Clone();
            public void Create(CheckpointStorageFile file) => Deny();
            public void Write(CheckpointStorageFile file, byte[] bytes) => Deny();
            public void Flush(CheckpointStorageFile file) => Deny();
            public void FlushExisting(CheckpointStorageFile file) => Deny();
            public void Close(CheckpointStorageFile file) => Deny();

            private void Deny()
            {
                WriteAttempts++;
                throw new UnauthorizedAccessException("The Save V1 directory is read-only.");
            }
        }
    }

}
