using System;
using System.IO;
using System.Text;
using IdleFactory.Core.Application;
using IdleFactory.Domain;
using IdleFactory.Run.Application;
using IdleFactory.Run.Content;
using IdleFactory.Unity.Persistence;
using NUnit.Framework;
using UnityEngine;

namespace IdleFactory.Tests.PlayMode
{
    /// <summary>
    /// T348 (issue #413, plan step 17b): Save V3 in the built Windows player. (1) A real Save V2 generation, published through the production
    /// generation protocol onto the file system, is read by the shipped <see cref="JsonRunCheckpointStore"/>, upgraded by its first write to V3 and
    /// read back equal, with the V2 generation retained as the previous one. (2) The packaged fixtures carry the SHA-256 their READMEs list: the
    /// W005-L V3 fixture of content revision 3 and the V2 fixtures it supersedes. The Edit Mode <c>SaveV3PlayerFixtureParityTests</c> ties each packaged
    /// copy to its committed original; here the player proves that the bytes that actually shipped in the build are those bytes.
    /// </summary>
    public sealed class SaveV3PlayerRoundTripTests
    {
        /// <summary>The SHA-256 of <c>01-w005-l.json</c> (content revision 3); <c>W005PlayerTimingTests</c> and the Edit Mode parity class pin the same value.</summary>
        private const string W005LSha256 = "7066eed15513d2bdeb2c349bfa98c2a3fdc500d6b179b997ed8fe92b954a70fd";

        private string root;

        [SetUp]
        public void CreateRoot() => root = RunPlayerFixtures.NewRoot();

        [TearDown]
        public void DeleteRoot() => RunPlayerFixtures.DeleteRoot(root);

        /// <summary>
        /// A codec that reads like the production one and writes one fixed byte string for the fixture's own checkpoint, so a Save V2 generation can be
        /// published through the real protocol: the player's codec always writes V3 and its V2 writer is an Edit Mode-only seam.
        /// </summary>
        private sealed class FixedV2Codec : ICheckpointGenerationCodec<RunCheckpoint>
        {
            private readonly SaveV2CheckpointCodec real;
            private readonly CheckpointId fixtureId;
            private readonly byte[] fixtureBytes;

            public FixedV2Codec(RunCatalog catalog, CheckpointId fixtureId, byte[] fixtureBytes)
            {
                real = new SaveV2CheckpointCodec(catalog);
                this.fixtureId = fixtureId;
                this.fixtureBytes = fixtureBytes;
            }

            public int MaximumByteCount => real.MaximumByteCount;
            public CheckpointId GetId(RunCheckpoint checkpoint) => real.GetId(checkpoint);
            public long GetGeneration(RunCheckpoint checkpoint) => real.GetGeneration(checkpoint);

            public CommandResult<byte[]> Serialize(RunCheckpoint checkpoint) =>
                checkpoint.Id == fixtureId ? CommandResult<byte[]>.Success((byte[])fixtureBytes.Clone()) : real.Serialize(checkpoint);

            public CheckpointDecodeResult<RunCheckpoint> Deserialize(byte[] bytes) => real.Deserialize(bytes);
            public bool ValidateBytes(RunCheckpoint checkpoint, byte[] bytes) => real.ValidateBytes(checkpoint, bytes);
        }

        private static string Text(byte[] bytes) => Encoding.UTF8.GetString(bytes);

        [Category("StandalonePlayer")]
        [Test]
        public void ARealV2GenerationIsReadByTheStoreUpgradedToV3ByTheFirstWriteAndReadBackEqual()
        {
            W003PlayerFixture fixture = W003PlayerFixtures.Reference;
            RunCatalog catalog = W003PlayerFixtures.Catalog(fixture);
            byte[] v2Bytes = W003PlayerFixtures.Bytes(fixture);
            Assert.That(W003PlayerFixtures.Sha256Of(v2Bytes), Is.EqualTo(fixture.Sha256), "packaged V2 bytes");
            Assert.That(Text(v2Bytes), Does.StartWith("{\"schemaVersion\":2,"), "the fixture is a Save V2 file");

            var codec = new SaveV2CheckpointCodec(catalog);
            var decoded = codec.Deserialize(v2Bytes);
            Assert.That(decoded.Succeeded, Is.True, decoded.Message);
            RunCheckpoint v2 = decoded.Checkpoint;

            // Publish the V2 generation through the production protocol on the real file system.
            string directory = SaveV2CheckpointFiles.Layout.DirectoryFor(root);
            var backend = new SystemIoCheckpointStorageBackend(directory, SaveV2CheckpointFiles.Layout);
            var seeded = new CheckpointGenerationFiles<RunCheckpoint>(backend, new FixedV2Codec(catalog, v2.Id, v2Bytes)).Promote(v2);
            Assert.That(seeded.Succeeded, Is.True, seeded.Message);
            string dataA = Path.Combine(directory, JsonRunCheckpointStore.DataAFileName);
            string dataB = Path.Combine(directory, JsonRunCheckpointStore.DataBFileName);
            Assert.That(File.ReadAllBytes(dataA), Is.EqualTo(v2Bytes), "the V2 generation is on disk byte for byte");

            // The shipped store reads it.
            var store = new JsonRunCheckpointStore(root, catalog);
            var loaded = store.ReadActive();
            Assert.That(loaded.Succeeded, Is.True, loaded.Message);
            RunCheckpoint first = loaded.Checkpoint;
            Assert.That(first.Id, Is.EqualTo(v2.Id));
            string v3OfOriginal = Text(codec.Serialize(first).Value);
            Assert.That(v3OfOriginal, Does.StartWith("{\"schemaVersion\":3,"), "the codec writes V3");

            // Its first write is V3: the loaded state under a new identity and the next generation.
            var upgrade = new RunCheckpoint(new CheckpointId(Guid.Parse("33333333-3333-3333-3333-000000000348")), first.Generation + 1,
                first.DepartureUtcMs, first.OfflinePolicyRevision, first.Id, first.ContentRevision, null, first.Run, first.Permanent);
            var promoted = store.Promote(upgrade);
            Assert.That(promoted.Succeeded, Is.True, promoted.Message);
            Assert.That(File.Exists(dataB), Is.True);
            Assert.That(Text(File.ReadAllBytes(dataB)), Does.StartWith("{\"schemaVersion\":3,"), "the first write after a V2 load is V3");
            Assert.That(File.ReadAllBytes(dataA), Is.EqualTo(v2Bytes), "the retained previous generation stays the V2 file");

            // A fresh store (a restart) reads the V3 generation equal to the V2 state, and still reads the V2 generation as the previous one.
            var restartedStore = new JsonRunCheckpointStore(root, catalog);
            var active = restartedStore.ReadActive();
            Assert.That(active.Succeeded, Is.True, active.Message);
            Assert.That(active.Checkpoint.Id, Is.EqualTo(upgrade.Id));
            Assert.That(active.Checkpoint.Generation, Is.EqualTo(first.Generation + 1));
            var sameIdentity = new RunCheckpoint(first.Id, first.Generation, first.DepartureUtcMs, first.OfflinePolicyRevision, first.ReconciledFromCheckpointId,
                first.ContentRevision, null, active.Checkpoint.Run, active.Checkpoint.Permanent);
            Assert.That(Text(codec.Serialize(sameIdentity).Value), Is.EqualTo(v3OfOriginal), "the V3 state is the V2 state, nothing dropped");
            var previous = restartedStore.ReadPrevious();
            Assert.That(previous.Succeeded, Is.True, previous.Message);
            Assert.That(previous.Checkpoint.Id, Is.EqualTo(v2.Id), "the V2 generation is the previous one");
        }

        [Category("StandalonePlayer")]
        [Test]
        public void ThePackagedW005LFixtureHasTheListedHashIsAV3FileOfRevision3AndIsAFixedPointOfTheMapper()
        {
            var catalog = RunPlayerFixtures.IronCatalog();
            var asset = Resources.Load<TextAsset>("W005PlayerFixtures/01-w005-l");
            Assert.That(asset, Is.Not.Null, "The packaged W005-L fixture is missing from the build.");
            byte[] bytes = asset.bytes;
            Assert.That(W003PlayerFixtures.Sha256Of(bytes), Is.EqualTo(W005LSha256), "packaged W005-L bytes");

            var read = SaveV2Mapper.Deserialize(bytes, catalog);
            Assert.That(read.IsSuccess, Is.True, read.Code);
            Assert.That(read.Document.SchemaVersion, Is.EqualTo(SaveV2Mapper.SchemaVersion), "a Save V3 file");
            Assert.That(read.Document.Meta.ContentRevision, Is.EqualTo(catalog.ContentRevision), "recorded under the shipped content revision");
            var written = SaveV2Mapper.Serialize(read.Document.Meta, read.Document.Run, read.Document.Permanent);
            Assert.That(written.IsSuccess, Is.True, written.Code);
            Assert.That(W003PlayerFixtures.Sha256Of(written.Bytes), Is.EqualTo(W005LSha256), "read then write reproduces the listed hash");
        }

        [Category("StandalonePlayer")]
        [Test]
        public void EveryPackagedV2FixtureHasItsListedHashAndLoadsUnderTheV3Reader()
        {
            foreach (W003PlayerFixture fixture in new[] { W003PlayerFixtures.Bound, W003PlayerFixtures.Stress, W003PlayerFixtures.Reference })
            {
                var document = W003PlayerFixtures.Load(fixture, W003PlayerFixtures.Catalog(fixture)); // asserts the hash and the read
                Assert.That(document.SchemaVersion, Is.EqualTo(SaveV2Mapper.LegacySchemaVersion), fixture.Scenario + " is a Save V2 file the V3 reader accepts");
            }
        }
    }
}
