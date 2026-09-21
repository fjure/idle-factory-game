using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using IdleFactory.Core.Application;
using IdleFactory.Domain;
using IdleFactory.Tests.EditMode.Fixtures;
using IdleFactory.Unity.Persistence;
using NUnit.Framework;

namespace IdleFactory.Tests.EditMode
{
    public sealed class CheckpointStoreTests
    {
        private const string Root = "test-persistent-root";

        [Test]
        public void PromotionsAlternateDataAndManifestSlotsWithMonotonicSequences()
        {
            var state = new MemoryState();
            var store = Store(state);
            var first = Checkpoint(1, 1, FoundationFixtures.CreateNegativeCoordinate());
            var second = Checkpoint(2, 2);
            var third = Checkpoint(3, 3, FoundationFixtures.CreateNegativeCoordinate());

            Assert.That(store.Promote(first).Succeeded, Is.True);
            Assert.That(Sequence(state, CheckpointStorageFile.ManifestA), Is.EqualTo(1));
            Assert.That(store.Promote(second).Succeeded, Is.True);
            Assert.That(Sequence(state, CheckpointStorageFile.ManifestB), Is.EqualTo(2));
            Assert.That(store.Promote(third).Succeeded, Is.True);
            Assert.That(Sequence(state, CheckpointStorageFile.ManifestA), Is.EqualTo(3));

            AssertCheckpoint(store.ReadActive(), third);
            AssertCheckpoint(store.ReadPrevious(), second);
            AssertCheckpoint(store.Read(second.Id), second);
            Assert.That(store.Read(first.Id).Outcome, Is.EqualTo(CommandOutcome.NoCheckpoint));
            Assert.That(state.Files.Keys, Is.EquivalentTo(new[]
            {
                CheckpointStorageFile.DataA,
                CheckpointStorageFile.DataB,
                CheckpointStorageFile.ManifestA,
                CheckpointStorageFile.ManifestB,
            }));
        }

        [Test]
        public void SameIdentityIsIdempotentOnlyForExactStrictContent()
        {
            var state = new MemoryState();
            var original = Checkpoint(1, 1);
            var store = Store(state);
            Assert.That(store.Promote(original).Succeeded, Is.True);
            Assert.That(store.Promote(original).Succeeded, Is.True);

            var conflict = Checkpoint(1, 1, FoundationFixtures.CreateNegativeCoordinate());
            Assert.That(store.Promote(conflict).Outcome, Is.EqualTo(CommandOutcome.InvalidCheckpoint));
            AssertCheckpoint(store.ReadActive(), original);
        }

        [Test]
        public void CandidateAndPostPublicationSubstitutionWithMatchingIdentityAreRejected()
        {
            var state = SeedOne();
            var candidate = Checkpoint(2, 2);
            var conflictingBytes = Bytes(System.Text.Encoding.UTF8.GetString(Serialize(candidate))
                .Replace("\"quantity\":4", "\"quantity\":3"));
            var backend = new MemoryBackend(state);
            backend.SubstituteRead = new Substitution(CheckpointStorageFile.DataB, 1, conflictingBytes);
            Assert.That(Store(backend).Promote(candidate).Outcome, Is.EqualTo(CommandOutcome.InvalidCheckpoint));
            AssertCheckpoint(Store(state).ReadActive(), Checkpoint(1, 1));

            state = SeedOne();
            backend = new MemoryBackend(state);
            backend.AfterClose = file =>
            {
                if (file == CheckpointStorageFile.ManifestB)
                    state.Files[CheckpointStorageFile.DataB] = conflictingBytes;
            };
            Assert.That(Store(backend).Promote(candidate).Outcome, Is.EqualTo(CommandOutcome.InvalidCheckpoint));
            var restarted = Store(state);
            Assert.That(restarted.ReadActive().Outcome, Is.EqualTo(CommandOutcome.InvalidCheckpoint));
            AssertCheckpoint(restarted.ReadPrevious(), Checkpoint(1, 1));
        }

        [Test]
        public void CorruptHighestActiveNeverFallsBackButCorruptIncompleteManifestDoes()
        {
            var state = SeedTwo();
            state.Files[CheckpointStorageFile.DataB] = Bytes("corrupt active secret");
            var store = Store(state);
            Assert.That(store.ReadActive().Outcome, Is.EqualTo(CommandOutcome.InvalidCheckpoint));
            Assert.That(store.Read(Checkpoint(1, 1).Id).Succeeded, Is.True);
            AssertCheckpoint(store.ReadPrevious(), Checkpoint(1, 1));

            state = SeedOne();
            state.Files[CheckpointStorageFile.ManifestB] = Bytes("incomplete new manifest");
            AssertCheckpoint(Store(state).ReadActive(), Checkpoint(1, 1));
        }

        [TestCaseSource(nameof(UpdateFailureCases))]
        public void UpdateFailureAtEveryBoundarySurvivesRestartAndRetry(object value)
        {
            var failure = (BoundaryFailure)value;
            var state = SeedOne();
            var candidate = Checkpoint(2, 2, FoundationFixtures.CreateNegativeCoordinate());
            var backend = new MemoryBackend(state) { Failure = failure };

            var failed = Store(backend).Promote(candidate);

            Assert.That(failed.Succeeded, Is.False, failure.ToString());
            backend.Failure = null;
            var restarted = Store(state);
            var active = restarted.ReadActive();
            Assert.That(active.Succeeded, Is.True, failure.ToString());
            Assert.That(active.Checkpoint.Id == Checkpoint(1, 1).Id || active.Checkpoint.Id == candidate.Id,
                Is.True, failure.ToString());
            if (active.Checkpoint.Id == candidate.Id) AssertCheckpoint(restarted.ReadPrevious(), Checkpoint(1, 1));
            Assert.That(restarted.Promote(candidate).Succeeded, Is.True, failure.ToString());
            AssertCheckpoint(restarted.ReadActive(), candidate);
        }

        [TestCaseSource(nameof(FirstSaveFailureCases))]
        public void FirstSaveFailureNeverReturnsFalseSuccessAndRestartConverges(object value)
        {
            var failure = (BoundaryFailure)value;
            var state = new MemoryState();
            var candidate = Checkpoint(1, 1);
            var backend = new MemoryBackend(state) { Failure = failure };

            Assert.That(Store(backend).Promote(candidate).Succeeded, Is.False, failure.ToString());
            backend.Failure = null;
            var restarted = Store(state);
            var read = restarted.ReadActive();
            Assert.That(read.Outcome == CommandOutcome.NoCheckpoint ||
                        read.Outcome == CommandOutcome.InvalidCheckpoint || read.Succeeded,
                Is.True, failure.ToString());
            if (read.Succeeded) AssertCheckpoint(read, candidate);
            Assert.That(restarted.Promote(candidate).Succeeded, Is.True, failure.ToString());
        }

        [Test]
        public void ExplicitRecoveryRequiresNewHigherLinkedCandidateWithIdenticalPreviousFactory()
        {
            var state = SeedTwo();
            var store = Store(state);
            var previous = Checkpoint(1, 1);
            var recovery = new Checkpoint(
                new CheckpointId(new Guid(3, 0, 0, new byte[8])),
                3,
                DateTimeOffset.FromUnixTimeMilliseconds(9000),
                previous.OfflinePolicyRevision,
                previous.Id,
                previous.CreateFactoryCopy());

            Assert.That(store.RecoverPrevious(recovery).Succeeded, Is.True);
            AssertCheckpoint(store.ReadActive(), recovery);
            AssertCheckpoint(store.ReadPrevious(), previous);

            state = SeedTwo();
            store = Store(state);
            Assert.That(store.RecoverPrevious(Checkpoint(1, 3)).Outcome, Is.EqualTo(CommandOutcome.InvalidCheckpoint));
            Assert.That(store.RecoverPrevious(new Checkpoint(
                new CheckpointId(new Guid(4, 0, 0, new byte[8])), 2,
                DateTimeOffset.UnixEpoch, previous.OfflinePolicyRevision, previous.Id,
                previous.CreateFactoryCopy())).Outcome, Is.EqualTo(CommandOutcome.InvalidCheckpoint));
            Assert.That(store.RecoverPrevious(new Checkpoint(
                new CheckpointId(new Guid(4, 0, 0, new byte[8])), 3,
                DateTimeOffset.UnixEpoch, previous.OfflinePolicyRevision, null,
                previous.CreateFactoryCopy())).Outcome, Is.EqualTo(CommandOutcome.InvalidCheckpoint));
            Assert.That(store.RecoverPrevious(new Checkpoint(
                new CheckpointId(new Guid(4, 0, 0, new byte[8])), 3,
                DateTimeOffset.UnixEpoch, previous.OfflinePolicyRevision, previous.Id,
                FoundationFixtures.CreateNegativeCoordinate())).Outcome, Is.EqualTo(CommandOutcome.InvalidCheckpoint));
        }

        [TestCaseSource(nameof(RecoveryFailureCases))]
        public void RecoveryFailureRestartNeverReversesOrDestroysSource(object value)
        {
            var failure = (BoundaryFailure)value;
            var state = SeedTwo();
            var previous = Checkpoint(1, 1);
            var recovery = new Checkpoint(
                new CheckpointId(new Guid(3, 0, 0, new byte[8])), 3,
                DateTimeOffset.FromUnixTimeMilliseconds(3000), previous.OfflinePolicyRevision,
                previous.Id, previous.CreateFactoryCopy());
            var backend = new MemoryBackend(state) { Failure = failure };

            Assert.That(Store(backend).RecoverPrevious(recovery).Succeeded, Is.False, failure.ToString());
            backend.Failure = null;
            var restarted = Store(state);
            Assert.That(restarted.Read(previous.Id).Succeeded, Is.True, failure.ToString());
            Assert.That(restarted.RecoverPrevious(recovery).Succeeded, Is.True, failure.ToString());
            AssertCheckpoint(restarted.ReadActive(), recovery);
        }

        [Test]
        public void BoundedLengthsRacesUnsupportedVersionAndMessagesRemainStable()
        {
            var state = SeedOne();
            var backend = new MemoryBackend(state);
            backend.ReportedLengths[CheckpointStorageFile.DataA] = SaveV1Mapper.MaximumByteCount + 1L;
            Assert.That(Store(backend).ReadActive().Outcome, Is.EqualTo(CommandOutcome.InvalidCheckpoint));

            backend.ReportedLengths.Clear();
            backend.ChangeLengthAfterRead = CheckpointStorageFile.DataA;
            Assert.That(Store(backend).ReadActive().Outcome, Is.EqualTo(CommandOutcome.PersistenceFailed));
            Assert.That(backend.LastAllocation, Is.LessThanOrEqualTo(SaveV1Mapper.MaximumByteCount));

            state = SeedOne();
            state.Files[CheckpointStorageFile.DataA] = Bytes("{\"schemaVersion\":2}");
            Assert.That(Store(state).ReadActive().Outcome, Is.EqualTo(CommandOutcome.UnsupportedSaveVersion));

            state = new MemoryState();
            state.Files[CheckpointStorageFile.ManifestA] = Bytes("{raw-secret-manifest}");
            var invalid = Store(new MemoryBackend(state, "C:\\Users\\secret\\root")).ReadActive();
            Assert.That(invalid.Message, Is.EqualTo("Checkpoint data is invalid."));
            Assert.That(invalid.Message, Does.Not.Contain("secret"));
            Assert.That(invalid.Message, Does.Not.Contain("{"));
        }

        [Test]
        public void ReadbackIsIsolatedAndFullSemanticStateMatchesIncludingNegativeCoordinates()
        {
            var source = Checkpoint(1, 1, FoundationFixtures.CreateNegativeCoordinate());
            var state = new MemoryState();
            var store = Store(state);
            Assert.That(store.Promote(source).Succeeded, Is.True);
            var first = store.ReadActive().Checkpoint;
            var second = Store(state).ReadActive().Checkpoint;
            Assert.That(first, Is.Not.SameAs(source));
            Assert.That(first.CreateFactoryCopy(), Is.Not.SameAs(second.CreateFactoryCopy()));
            AssertCheckpoint(CheckpointReadResult.Success(first), source);
        }

        [Test]
        public void RealBackendCreatesOnlyFourFixedSlotsAndNoPendingArtifacts()
        {
            var root = Path.Combine(Path.GetTempPath(), "idle-factory-manifest-test-" + Guid.NewGuid().ToString("N"));
            try
            {
                var store = new JsonCheckpointStore(root, FoundationFixtures.CreateD001().Catalog);
                Assert.That(store.Promote(Checkpoint(1, 1)).Succeeded, Is.True);
                Assert.That(store.Promote(Checkpoint(2, 2)).Succeeded, Is.True);
                var directory = Path.Combine(root, "IdleFactory", "Checkpoints", "v1");
                Assert.That(Directory.GetFiles(directory).Select(Path.GetFileName), Is.EquivalentTo(new[]
                {
                    JsonCheckpointStore.DataAFileName,
                    JsonCheckpointStore.DataBFileName,
                    JsonCheckpointStore.ManifestAFileName,
                    JsonCheckpointStore.ManifestBFileName,
                }));
                Assert.That(Directory.GetFiles(directory).Any(path => path.IndexOf("pending", StringComparison.OrdinalIgnoreCase) >= 0), Is.False);
            }
            finally
            {
                if (Directory.Exists(root)) Directory.Delete(root, true);
            }
        }

        [Test]
        public void ConstructorRejectsInvalidDependencies()
        {
            var catalog = FoundationFixtures.CreateD001().Catalog;
            Assert.That(() => new JsonCheckpointStore("", catalog, new MemoryBackend(new MemoryState())), Throws.InstanceOf<ArgumentException>());
            Assert.That(() => new JsonCheckpointStore(Root, null, new MemoryBackend(new MemoryState())), Throws.InstanceOf<ArgumentNullException>());
            Assert.That(() => new JsonCheckpointStore(Root, catalog, null), Throws.InstanceOf<ArgumentNullException>());
        }

        private static IEnumerable<BoundaryFailure> UpdateFailureCases() => Cases(CheckpointStorageFile.DataB, CheckpointStorageFile.ManifestB);
        private static IEnumerable<BoundaryFailure> FirstSaveFailureCases() => Cases(CheckpointStorageFile.DataA, CheckpointStorageFile.ManifestA);
        private static IEnumerable<BoundaryFailure> RecoveryFailureCases() => Cases(CheckpointStorageFile.DataB, CheckpointStorageFile.ManifestA);

        private static IEnumerable<BoundaryFailure> Cases(CheckpointStorageFile data, CheckpointStorageFile manifest)
        {
            foreach (var item in BasicCases(data)) yield return item;
            foreach (var item in BasicCases(manifest)) yield return item;
            yield return new BoundaryFailure(data, Operation.Length, 1, false);
            yield return new BoundaryFailure(data, Operation.Length, 1, true);
            yield return new BoundaryFailure(data, Operation.Length, 2, false);
            yield return new BoundaryFailure(data, Operation.Length, 2, true);
            yield return new BoundaryFailure(data, Operation.Length, 3, false);
            yield return new BoundaryFailure(data, Operation.Length, 3, true);
            yield return new BoundaryFailure(data, Operation.Length, 4, false);
            yield return new BoundaryFailure(data, Operation.Length, 4, true);
            yield return new BoundaryFailure(data, Operation.Read, 1, false);
            yield return new BoundaryFailure(data, Operation.Read, 1, true);
            yield return new BoundaryFailure(data, Operation.Read, 2, false);
            yield return new BoundaryFailure(data, Operation.Read, 2, true);
            yield return new BoundaryFailure(manifest, Operation.Length, 1, false);
            yield return new BoundaryFailure(manifest, Operation.Length, 1, true);
            yield return new BoundaryFailure(manifest, Operation.Length, 2, false);
            yield return new BoundaryFailure(manifest, Operation.Length, 2, true);
            yield return new BoundaryFailure(manifest, Operation.Length, 3, false);
            yield return new BoundaryFailure(manifest, Operation.Length, 3, true);
            yield return new BoundaryFailure(manifest, Operation.Length, 4, false);
            yield return new BoundaryFailure(manifest, Operation.Length, 4, true);
            yield return new BoundaryFailure(manifest, Operation.Read, 1, false);
            yield return new BoundaryFailure(manifest, Operation.Read, 1, true);
            yield return new BoundaryFailure(manifest, Operation.Read, 2, false);
            yield return new BoundaryFailure(manifest, Operation.Read, 2, true);
            yield return new BoundaryFailure(data, Operation.Exists, 2, false);
            yield return new BoundaryFailure(data, Operation.Exists, 2, true);
            yield return new BoundaryFailure(manifest, Operation.Exists, 2, false);
            yield return new BoundaryFailure(manifest, Operation.Exists, 2, true);
            yield return new BoundaryFailure(manifest, Operation.Exists, 3, false);
            yield return new BoundaryFailure(manifest, Operation.Exists, 3, true);
        }

        private static IEnumerable<BoundaryFailure> BasicCases(CheckpointStorageFile file)
        {
            foreach (var operation in new[] { Operation.Exists, Operation.Create, Operation.Write, Operation.Flush, Operation.Close })
            {
                yield return new BoundaryFailure(file, operation, 1, false);
                yield return new BoundaryFailure(file, operation, 1, true);
            }
        }

        private static MemoryState SeedOne()
        {
            var state = new MemoryState();
            Assert.That(Store(state).Promote(Checkpoint(1, 1)).Succeeded, Is.True);
            return state;
        }

        private static MemoryState SeedTwo()
        {
            var state = SeedOne();
            Assert.That(Store(state).Promote(Checkpoint(2, 2)).Succeeded, Is.True);
            return state;
        }

        private static JsonCheckpointStore Store(MemoryState state) => Store(new MemoryBackend(state));
        private static JsonCheckpointStore Store(MemoryBackend backend) =>
            new JsonCheckpointStore(Root, FoundationFixtures.CreateD001().Catalog, backend);

        private static Checkpoint Checkpoint(int id, long generation, FactoryState factory = null)
        {
            return new Checkpoint(
                new CheckpointId(new Guid(id, 0, 0, new byte[8])), generation,
                DateTimeOffset.FromUnixTimeMilliseconds(1000 + generation),
                new OfflinePolicyRevision(1), null, factory ?? FoundationFixtures.CreateD001());
        }

        private static byte[] Serialize(Checkpoint checkpoint) => SaveV1Mapper.Serialize(checkpoint).Value;
        private static byte[] Bytes(string value) => System.Text.Encoding.UTF8.GetBytes(value);
        private static long Sequence(MemoryState state, CheckpointStorageFile file) => BitConverter.ToInt64(state.Files[file], 8);

        private static void AssertCheckpoint(CheckpointReadResult result, Checkpoint expected)
        {
            Assert.That(result.Outcome, Is.EqualTo(CommandOutcome.Success));
            Assert.That(result.Checkpoint.Id, Is.EqualTo(expected.Id));
            Assert.That(result.Checkpoint.Generation, Is.EqualTo(expected.Generation));
            Assert.That(result.Checkpoint.DepartureUtc, Is.EqualTo(expected.DepartureUtc));
            Assert.That(result.Checkpoint.OfflinePolicyRevision, Is.EqualTo(expected.OfflinePolicyRevision));
            Assert.That(result.Checkpoint.ReconciledFromCheckpointId, Is.EqualTo(expected.ReconciledFromCheckpointId));
            Assert.That(Serialize(result.Checkpoint), Is.EqualTo(Serialize(expected)));
        }

        public enum Operation { Exists, Length, Read, Create, Write, Flush, Close }

        private sealed class BoundaryFailure
        {
            public BoundaryFailure(CheckpointStorageFile file, Operation operation, int occurrence, bool after)
            {
                File = file; Operation = operation; Occurrence = occurrence; After = after;
            }
            public CheckpointStorageFile File { get; }
            public Operation Operation { get; }
            public int Occurrence { get; }
            public bool After { get; }
            public override string ToString() => File + "." + Operation + "#" + Occurrence + (After ? ".After" : ".Before");
        }

        private sealed class Substitution
        {
            public Substitution(CheckpointStorageFile file, int occurrence, byte[] bytes)
            { File = file; Occurrence = occurrence; Bytes = bytes; }
            public CheckpointStorageFile File { get; }
            public int Occurrence { get; }
            public byte[] Bytes { get; }
        }

        private sealed class MemoryState
        {
            public Dictionary<CheckpointStorageFile, byte[]> Files { get; } = new Dictionary<CheckpointStorageFile, byte[]>();
        }

        private sealed class MemoryBackend : ICheckpointStorageBackend
        {
            private readonly MemoryState state;
            private readonly Dictionary<string, int> calls = new Dictionary<string, int>();
            private CheckpointStorageFile? openFile;
            private byte[] openBytes;

            public MemoryBackend(MemoryState state, string identity = "memory-manifest-store")
            { this.state = state; StorageIdentity = identity; }

            public string StorageIdentity { get; }
            public BoundaryFailure Failure { get; set; }
            public Substitution SubstituteRead { get; set; }
            public Action<CheckpointStorageFile> AfterClose { get; set; }
            public Dictionary<CheckpointStorageFile, long> ReportedLengths { get; } = new Dictionary<CheckpointStorageFile, long>();
            public CheckpointStorageFile? ChangeLengthAfterRead { get; set; }
            public int LastAllocation { get; private set; }

            public bool Exists(CheckpointStorageFile file)
            {
                var call = Before(file, Operation.Exists);
                var result = state.Files.ContainsKey(file);
                After(file, Operation.Exists, call);
                return result;
            }

            public long GetLength(CheckpointStorageFile file)
            {
                var call = Before(file, Operation.Length);
                var result = ReportedLengths.TryGetValue(file, out var length) ? length : state.Files[file].LongLength;
                After(file, Operation.Length, call);
                return result;
            }

            public byte[] ReadExactly(CheckpointStorageFile file, int expectedLength)
            {
                var call = Before(file, Operation.Read);
                LastAllocation = expectedLength;
                var source = SubstituteRead != null && SubstituteRead.File == file && SubstituteRead.Occurrence == call
                    ? SubstituteRead.Bytes : state.Files[file];
                if (source.Length != expectedLength) throw new IOException("injected length race");
                var result = (byte[])source.Clone();
                if (ChangeLengthAfterRead == file) ReportedLengths[file] = expectedLength + 1L;
                After(file, Operation.Read, call);
                return result;
            }

            public void Create(CheckpointStorageFile file)
            {
                var call = Before(file, Operation.Create);
                openFile = file;
                openBytes = Array.Empty<byte>();
                After(file, Operation.Create, call);
            }

            public void Write(CheckpointStorageFile file, byte[] bytes)
            {
                var call = Before(file, Operation.Write);
                Require(file);
                openBytes = (byte[])bytes.Clone();
                After(file, Operation.Write, call);
            }

            public void Flush(CheckpointStorageFile file)
            {
                var call = Before(file, Operation.Flush);
                Require(file);
                After(file, Operation.Flush, call);
            }

            public void Close(CheckpointStorageFile file)
            {
                var call = Before(file, Operation.Close);
                if (openFile == file)
                {
                    state.Files[file] = openBytes ?? Array.Empty<byte>();
                    openFile = null;
                    openBytes = null;
                }
                AfterClose?.Invoke(file);
                After(file, Operation.Close, call);
            }

            private void Require(CheckpointStorageFile file)
            {
                if (openFile != file) throw new InvalidOperationException("wrong fixed slot");
            }

            private int Before(CheckpointStorageFile file, Operation operation)
            {
                var key = file + ":" + operation;
                calls.TryGetValue(key, out var count);
                count++;
                calls[key] = count;
                if (Matches(file, operation, count, false)) throw FailureException();
                return count;
            }

            private void After(CheckpointStorageFile file, Operation operation, int count)
            {
                if (Matches(file, operation, count, true)) throw FailureException();
            }

            private bool Matches(CheckpointStorageFile file, Operation operation, int occurrence, bool after) =>
                Failure != null && Failure.File == file && Failure.Operation == operation &&
                Failure.Occurrence == occurrence && Failure.After == after;

            private static Exception FailureException() => new IOException("private injected failure");
        }
    }
}
