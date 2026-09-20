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
        private static readonly FailurePoint[] WriteAndPromotionFailures =
        {
            FailurePoint.DeletePendingBefore,
            FailurePoint.DeletePendingAfter,
            FailurePoint.CreateBefore,
            FailurePoint.CreateAfter,
            FailurePoint.WriteBefore,
            FailurePoint.WriteAfter,
            FailurePoint.FlushBefore,
            FailurePoint.FlushAfter,
            FailurePoint.CloseBefore,
            FailurePoint.CloseAfter,
            FailurePoint.PendingLength,
            FailurePoint.PendingRead,
            FailurePoint.PromoteBefore,
            FailurePoint.PromoteAfter,
            FailurePoint.DirectoryFlushBefore,
            FailurePoint.DirectoryFlushAfter,
            FailurePoint.PostReadLength,
            FailurePoint.PostRead,
        };

        [Test]
        public void FirstAndSubsequentPromotionsRetainStableGenerationsAndReadByIdentity()
        {
            var backend = new MemoryBackend();
            var store = Store(backend);
            var first = Checkpoint(1, 1, FoundationFixtures.CreateNegativeCoordinate());
            var second = Checkpoint(2, 2, FoundationFixtures.CreateD001());

            Assert.That(store.Promote(first).Value, Is.EqualTo(first.Id));
            AssertCheckpoint(store.ReadActive(), first);
            Assert.That(store.ReadPrevious().Outcome, Is.EqualTo(CommandOutcome.NoCheckpoint));
            Assert.That(store.Promote(second).Value, Is.EqualTo(second.Id));
            AssertCheckpoint(store.ReadActive(), second);
            AssertCheckpoint(store.ReadPrevious(), first);
            AssertCheckpoint(store.Read(first.Id), first);
            AssertCheckpoint(store.Read(second.Id), second);
            Assert.That(store.Read(new CheckpointId(Guid.NewGuid())).Outcome, Is.EqualTo(CommandOutcome.NoCheckpoint));
        }

        [Test]
        public void ReadsIgnoreAbandonedPendingAndReportAbsentCorruptOversizeAndUnsupportedPrecisely()
        {
            var backend = new MemoryBackend();
            var store = Store(backend);
            backend.Seed(CheckpointFile.Pending, Bytes("not json"));

            Assert.That(store.ReadActive().Outcome, Is.EqualTo(CommandOutcome.NoCheckpoint));
            backend.Seed(CheckpointFile.Active, Bytes("not json"));
            Assert.That(store.ReadActive().Outcome, Is.EqualTo(CommandOutcome.InvalidCheckpoint));
            backend.SetReportedLength(CheckpointFile.Active, SaveV1Mapper.MaximumByteCount + 1L);
            Assert.That(store.ReadActive().Outcome, Is.EqualTo(CommandOutcome.InvalidCheckpoint));
            backend.Seed(CheckpointFile.Active, Bytes("{\"schemaVersion\":2}"));
            Assert.That(store.ReadActive().Outcome, Is.EqualTo(CommandOutcome.UnsupportedSaveVersion));
            Assert.That(store.ReadActive().Message, Is.EqualTo("Save V1 schema version is unsupported."));
        }

        [Test]
        public void CorruptActiveNeverFallsBackOrOverwritesKnownValidPrevious()
        {
            var backend = new MemoryBackend();
            var previous = Checkpoint(1, 1);
            backend.Seed(CheckpointFile.Previous, Serialize(previous));
            backend.Seed(CheckpointFile.Active, Bytes("corrupt"));
            var store = Store(backend);

            Assert.That(store.ReadActive().Outcome, Is.EqualTo(CommandOutcome.InvalidCheckpoint));
            Assert.That(store.Read(previous.Id).Outcome, Is.EqualTo(CommandOutcome.InvalidCheckpoint));
            Assert.That(store.Promote(Checkpoint(2, 2)).Outcome, Is.EqualTo(CommandOutcome.InvalidCheckpoint));
            AssertCheckpoint(store.ReadPrevious(), previous);
        }

        [TestCaseSource(nameof(WriteAndPromotionFailures))]
        public void EveryInjectedBoundaryFailureReturnsNoFalseSuccessAndPreservesAValidGeneration(FailurePoint point)
        {
            var backend = new MemoryBackend();
            var old = Checkpoint(1, 1);
            var candidate = Checkpoint(2, 2, FoundationFixtures.CreateNegativeCoordinate());
            backend.Seed(CheckpointFile.Active, Serialize(old));
            backend.Seed(CheckpointFile.Pending, Bytes("abandoned"));
            var store = Store(backend);
            backend.FailAt = point;

            var result = store.Promote(candidate);

            Assert.That(result.Succeeded, Is.False, point.ToString());
            Assert.That(result.Outcome, Is.EqualTo(CommandOutcome.PersistenceFailed), point.ToString());
            Assert.That(IsValid(backend, CheckpointFile.Active) || IsValid(backend, CheckpointFile.Previous), Is.True, point.ToString());
            backend.FailAt = FailurePoint.None;
            Assert.That(store.Promote(candidate).Succeeded, Is.True, point.ToString());
            AssertCheckpoint(store.ReadActive(), candidate);
        }

        [Test]
        public void RetryReplacesAbandonedPendingAndConvergesIdempotently()
        {
            var backend = new MemoryBackend();
            var store = Store(backend);
            var candidate = Checkpoint(3, 3);
            backend.Seed(CheckpointFile.Pending, Bytes("partial secret json"));
            backend.FailAt = FailurePoint.PromoteBefore;
            Assert.That(store.Promote(candidate).Outcome, Is.EqualTo(CommandOutcome.PersistenceFailed));

            backend.FailAt = FailurePoint.None;
            Assert.That(store.Promote(candidate).Succeeded, Is.True);
            Assert.That(store.Promote(candidate).Succeeded, Is.True);
            AssertCheckpoint(store.ReadActive(), candidate);
            Assert.That(backend.Exists(CheckpointFile.Pending), Is.False);
        }

        [Test]
        public void ExplicitRecoveryPromotesExactPreviousAndRetainsOldActiveAsPrevious()
        {
            var backend = new MemoryBackend();
            var first = Checkpoint(1, 4);
            var second = Checkpoint(2, 5);
            backend.Seed(CheckpointFile.Active, Serialize(second));
            backend.Seed(CheckpointFile.Previous, Serialize(first));
            var store = Store(backend);

            var recovered = store.RecoverPrevious();

            Assert.That(recovered.Value, Is.EqualTo(first.Id));
            AssertCheckpoint(store.ReadActive(), first);
            AssertCheckpoint(store.ReadPrevious(), second);
        }

        [TestCase(FailurePoint.WriteAfter)]
        [TestCase(FailurePoint.FlushAfter)]
        [TestCase(FailurePoint.CloseAfter)]
        [TestCase(FailurePoint.PendingRead)]
        [TestCase(FailurePoint.PromoteBefore)]
        [TestCase(FailurePoint.PromoteAfter)]
        [TestCase(FailurePoint.DirectoryFlushAfter)]
        [TestCase(FailurePoint.PostRead)]
        public void FailedExplicitRecoveryNeverDestroysItsOnlyValidSource(FailurePoint point)
        {
            var backend = new MemoryBackend();
            var previous = Checkpoint(1, 1);
            backend.Seed(CheckpointFile.Active, Bytes("corrupt"));
            backend.Seed(CheckpointFile.Previous, Serialize(previous));
            var store = Store(backend);
            backend.FailAt = point;

            Assert.That(store.RecoverPrevious().Succeeded, Is.False);
            Assert.That(IsValid(backend, CheckpointFile.Active) || IsValid(backend, CheckpointFile.Previous), Is.True);
            backend.FailAt = FailurePoint.None;
            Assert.That(store.RecoverPrevious().Succeeded, Is.True);
            AssertCheckpoint(store.ReadActive(), previous);
        }

        [Test]
        public void LengthRaceIsBoundedAndReportedAsPersistenceFailure()
        {
            var backend = new MemoryBackend();
            backend.Seed(CheckpointFile.Active, Serialize(Checkpoint(1, 1)));
            backend.ChangeLengthAfterRead = CheckpointFile.Active;

            var result = Store(backend).ReadActive();

            Assert.That(result.Outcome, Is.EqualTo(CommandOutcome.PersistenceFailed));
            Assert.That(backend.LastReadAllocation, Is.LessThanOrEqualTo(SaveV1Mapper.MaximumByteCount));
        }

        [Test]
        public void MessagesNeverExposeStorageIdentityOrRawData()
        {
            var backend = new MemoryBackend("C:\\Users\\secret\\save-root");
            backend.Seed(CheckpointFile.Active, Bytes("{raw-secret-json}"));
            var invalid = Store(backend).ReadActive();
            backend.FailAt = FailurePoint.ActiveExists;
            var failed = Store(backend).ReadActive();

            Assert.That(invalid.Message, Does.Not.Contain("secret"));
            Assert.That(invalid.Message, Does.Not.Contain("{"));
            Assert.That(failed.Message, Is.EqualTo("Checkpoint storage operation failed."));
            Assert.That(failed.Message, Does.Not.Contain(backend.StorageIdentity));
        }

        [Test]
        public void ReadbackCreatesIsolatedStateAndPreservesNegativeCoordinates()
        {
            var backend = new MemoryBackend();
            var source = Checkpoint(1, 1, FoundationFixtures.CreateNegativeCoordinate());
            var store = Store(backend);
            Assert.That(store.Promote(source).Succeeded, Is.True);

            var first = store.ReadActive().Checkpoint;
            var second = store.ReadActive().Checkpoint;

            Assert.That(first, Is.Not.SameAs(source));
            Assert.That(first.CreateFactoryCopy(), Is.Not.SameAs(second.CreateFactoryCopy()));
            Assert.That(first.CreateFactoryCopy().Bounds.Origin, Is.EqualTo(new GridPosition(-8, -6)));
            Assert.That(first.CreateFactoryCopy().Buildings.Values.Single().Anchor, Is.EqualTo(new GridPosition(-7, -5)));
        }

        [Test]
        public void RealBackendUsesOnlyStableApplicationOwnedFileNames()
        {
            var root = Path.Combine(Path.GetTempPath(), "idle-factory-checkpoint-test-" + Guid.NewGuid().ToString("N"));
            try
            {
                var store = new JsonCheckpointStore(root, FoundationFixtures.CreateD001().Catalog);
                Assert.That(store.Promote(Checkpoint(1, 1)).Succeeded, Is.True);
                Assert.That(store.Promote(Checkpoint(2, 2)).Succeeded, Is.True);
                var directory = Path.Combine(root, "IdleFactory", "Checkpoints", "v1");
                Assert.That(Directory.GetFiles(directory).Select(Path.GetFileName),
                    Is.EquivalentTo(new[]
                    {
                        JsonCheckpointStore.ActiveFileName,
                        JsonCheckpointStore.PreviousFileName,
                    }));
                Assert.That(store.SupportsDirectoryMetadataFlush, Is.False);
            }
            finally
            {
                if (Directory.Exists(root)) Directory.Delete(root, true);
            }
        }

        [Test]
        public void ConstructorAndGenerationOrderRejectInvalidInputsWithoutStorageMutation()
        {
            var backend = new MemoryBackend();
            var catalog = FoundationFixtures.CreateD001().Catalog;
            Assert.That(() => new JsonCheckpointStore("", catalog, backend), Throws.InstanceOf<ArgumentException>());
            Assert.That(() => new JsonCheckpointStore(Root, null, backend), Throws.InstanceOf<ArgumentNullException>());
            Assert.That(() => new JsonCheckpointStore(Root, catalog, null), Throws.InstanceOf<ArgumentNullException>());

            var store = new JsonCheckpointStore(Root, catalog, backend);
            Assert.That(store.Promote(Checkpoint(2, 2)).Succeeded, Is.True);
            Assert.That(store.Promote(Checkpoint(1, 1)).Outcome, Is.EqualTo(CommandOutcome.InvalidCheckpoint));
            AssertCheckpoint(store.ReadActive(), Checkpoint(2, 2));
        }

        private static JsonCheckpointStore Store(MemoryBackend backend)
        {
            return new JsonCheckpointStore(Root, FoundationFixtures.CreateD001().Catalog, backend);
        }

        private static Checkpoint Checkpoint(int id, long generation, FactoryState factory = null)
        {
            return new Checkpoint(
                new CheckpointId(new Guid(id, 0, 0, new byte[8])),
                generation,
                DateTimeOffset.FromUnixTimeMilliseconds(1000 + generation),
                new OfflinePolicyRevision(1),
                null,
                factory ?? FoundationFixtures.CreateD001());
        }

        private static byte[] Serialize(Checkpoint checkpoint) => SaveV1Mapper.Serialize(checkpoint).Value;
        private static byte[] Bytes(string value) => System.Text.Encoding.UTF8.GetBytes(value);

        private static bool IsValid(MemoryBackend backend, CheckpointFile file)
        {
            if (!backend.TryGet(file, out var bytes)) return false;
            return SaveV1Mapper.Deserialize(bytes, FoundationFixtures.CreateD001().Catalog).Succeeded;
        }

        private static void AssertCheckpoint(CheckpointReadResult result, Checkpoint expected)
        {
            Assert.That(result.Outcome, Is.EqualTo(CommandOutcome.Success));
            Assert.That(result.Checkpoint.Id, Is.EqualTo(expected.Id));
            Assert.That(result.Checkpoint.Generation, Is.EqualTo(expected.Generation));
            Assert.That(result.Checkpoint.DepartureUtc, Is.EqualTo(expected.DepartureUtc));
            Assert.That(result.Checkpoint.CreateFactoryCopy().Bounds, Is.EqualTo(expected.CreateFactoryCopy().Bounds));
        }

        public enum FailurePoint
        {
            None,
            ActiveExists,
            DeletePendingBefore,
            DeletePendingAfter,
            CreateBefore,
            CreateAfter,
            WriteBefore,
            WriteAfter,
            FlushBefore,
            FlushAfter,
            CloseBefore,
            CloseAfter,
            PendingLength,
            PendingRead,
            PromoteBefore,
            PromoteAfter,
            DirectoryFlushBefore,
            DirectoryFlushAfter,
            PostReadLength,
            PostRead,
        }

        private sealed class MemoryBackend : ICheckpointFileBackend
        {
            private readonly Dictionary<CheckpointFile, byte[]> files = new Dictionary<CheckpointFile, byte[]>();
            private readonly Dictionary<CheckpointFile, long> reportedLengths = new Dictionary<CheckpointFile, long>();
            private byte[] openPending;
            private int activeLengthCalls;
            private int activeReadCalls;

            public MemoryBackend(string identity = "memory-checkpoint-store")
            {
                StorageIdentity = identity;
            }

            public string StorageIdentity { get; }
            public bool SupportsDirectoryMetadataFlush => true;
            public FailurePoint FailAt { get; set; }
            public CheckpointFile? ChangeLengthAfterRead { get; set; }
            public int LastReadAllocation { get; private set; }
            public List<CheckpointFile> RequestedFiles { get; } = new List<CheckpointFile>();
            public bool Exists(CheckpointFile file)
            {
                RequestedFiles.Add(file);
                if (file == CheckpointFile.Active) Throw(FailurePoint.ActiveExists);
                return files.ContainsKey(file);
            }

            public long GetLength(CheckpointFile file)
            {
                RequestedFiles.Add(file);
                if (file == CheckpointFile.Pending) Throw(FailurePoint.PendingLength);
                if (file == CheckpointFile.Active && ++activeLengthCalls > 1) Throw(FailurePoint.PostReadLength);
                return reportedLengths.TryGetValue(file, out var length) ? length : files[file].LongLength;
            }

            public byte[] ReadExactly(CheckpointFile file, int expectedLength)
            {
                RequestedFiles.Add(file);
                LastReadAllocation = expectedLength;
                if (file == CheckpointFile.Pending) Throw(FailurePoint.PendingRead);
                if (file == CheckpointFile.Active && ++activeReadCalls > 1) Throw(FailurePoint.PostRead);
                var source = files[file];
                if (source.Length != expectedLength) throw new IOException("length race");
                var result = (byte[])source.Clone();
                if (ChangeLengthAfterRead == file) reportedLengths[file] = expectedLength + 1L;
                return result;
            }

            public void CreatePending()
            {
                Throw(FailurePoint.CreateBefore);
                openPending = Array.Empty<byte>();
                Throw(FailurePoint.CreateAfter);
            }

            public void WritePending(byte[] bytes)
            {
                Throw(FailurePoint.WriteBefore);
                openPending = (byte[])bytes.Clone();
                Throw(FailurePoint.WriteAfter);
            }

            public void FlushPending()
            {
                Throw(FailurePoint.FlushBefore);
                Throw(FailurePoint.FlushAfter);
            }

            public void ClosePending()
            {
                Throw(FailurePoint.CloseBefore);
                if (openPending != null) files[CheckpointFile.Pending] = openPending;
                openPending = null;
                Throw(FailurePoint.CloseAfter);
            }

            public void PromotePending(bool replaceActive)
            {
                Throw(FailurePoint.PromoteBefore);
                if (replaceActive) files[CheckpointFile.Previous] = files[CheckpointFile.Active];
                files[CheckpointFile.Active] = files[CheckpointFile.Pending];
                files.Remove(CheckpointFile.Pending);
                Throw(FailurePoint.PromoteAfter);
            }

            public void FlushDirectory()
            {
                Throw(FailurePoint.DirectoryFlushBefore);
                Throw(FailurePoint.DirectoryFlushAfter);
            }

            public void DeletePending()
            {
                Throw(FailurePoint.DeletePendingBefore);
                files.Remove(CheckpointFile.Pending);
                Throw(FailurePoint.DeletePendingAfter);
            }

            public void Seed(CheckpointFile file, byte[] bytes)
            {
                files[file] = (byte[])bytes.Clone();
                reportedLengths.Remove(file);
            }

            public void SetReportedLength(CheckpointFile file, long length) => reportedLengths[file] = length;
            public bool TryGet(CheckpointFile file, out byte[] bytes) => files.TryGetValue(file, out bytes);

            private void Throw(FailurePoint point)
            {
                if (FailAt == point) throw new IOException("injected path and raw data must remain private");
            }
        }
    }
}
