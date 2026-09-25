using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security;
using System.Security.Cryptography;
using System.Text;
using IdleFactory.Core.Application;
using IdleFactory.Domain;

namespace IdleFactory.Unity.Persistence
{
    public enum CheckpointStorageFile
    {
        DataA = 0,
        DataB = 1,
        ManifestA = 2,
        ManifestB = 3,
        Pending = 4,
    }

    // Infrastructure-only transaction primitives. Gameplay code must use ICheckpointStore.
    public interface ICheckpointStorageBackend
    {
        string StorageIdentity { get; }
        bool Exists(CheckpointStorageFile file);
        long GetLength(CheckpointStorageFile file);
        byte[] ReadExactly(CheckpointStorageFile file, int expectedLength);
        void Create(CheckpointStorageFile file);
        void Write(CheckpointStorageFile file, byte[] bytes);
        void Flush(CheckpointStorageFile file);
        // Flush an existing slot without truncating or rewriting its bytes.
        void FlushExisting(CheckpointStorageFile file);
        void Close(CheckpointStorageFile file);
    }

    public sealed class JsonCheckpointStore : ICheckpointStore
    {
        public const string DataAFileName = "factory-checkpoint.data-a.json";
        public const string DataBFileName = "factory-checkpoint.data-b.json";
        public const string ManifestAFileName = "factory-checkpoint.manifest-a.bin";
        public const string ManifestBFileName = "factory-checkpoint.manifest-b.bin";
        public const string PendingFileName = "factory-checkpoint.pending.json";

        private const int ManifestLength = 163;
        private const int MaximumManifestByteCount = 256;
        private const string ApplicationDirectory = "IdleFactory";
        private const string CheckpointDirectory = "Checkpoints";
        private const string VersionDirectory = "v1";
        private const string NoActiveMessage = "No active checkpoint is available.";
        private const string NoPreviousMessage = "No previous checkpoint is available.";
        private const string NotFoundMessage = "The requested checkpoint is not retained.";
        private const string InvalidMessage = "Checkpoint data is invalid.";
        private const string PersistenceMessage = "Checkpoint storage operation failed.";
        private static readonly byte[] ManifestMagic = Encoding.ASCII.GetBytes("IFCPMF01");

        private static readonly object LockMapGate = new object();
        private static readonly Dictionary<string, object> LockMap =
            new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);

        private readonly DefinitionCatalog catalog;
        private readonly ICheckpointStorageBackend backend;
        private readonly object operationGate;

        public JsonCheckpointStore(string persistentDataRoot, DefinitionCatalog catalog)
            : this(persistentDataRoot, catalog, CreateBackend(persistentDataRoot))
        {
        }

        public JsonCheckpointStore(
            string persistentDataRoot,
            DefinitionCatalog catalog,
            ICheckpointStorageBackend backend)
        {
            if (string.IsNullOrWhiteSpace(persistentDataRoot))
            {
                throw new ArgumentException("A persistence root is required.", nameof(persistentDataRoot));
            }

            this.catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
            this.backend = backend ?? throw new ArgumentNullException(nameof(backend));
            if (string.IsNullOrWhiteSpace(backend.StorageIdentity))
            {
                throw new ArgumentException("The checkpoint backend requires a storage identity.", nameof(backend));
            }

            operationGate = GetOperationGate(backend.StorageIdentity);
        }

        public CheckpointReadResult ReadActive()
        {
            lock (operationGate)
            {
                var manifest = SelectManifest();
                if (!manifest.Succeeded)
                {
                    return manifest.ToReadResult(NoActiveMessage);
                }

                return ReadReference(manifest.Selection.Record.Active);
            }
        }

        public CheckpointReadResult ReadPrevious()
        {
            lock (operationGate)
            {
                var manifest = SelectManifest();
                if (!manifest.Succeeded)
                {
                    return manifest.ToReadResult(NoPreviousMessage);
                }

                return manifest.Selection.Record.Previous == null
                    ? CheckpointReadResult.Rejected(CommandOutcome.NoCheckpoint, NoPreviousMessage)
                    : ReadReference(manifest.Selection.Record.Previous);
            }
        }

        public CheckpointReadResult Read(CheckpointId checkpointId)
        {
            _ = checkpointId.Value;
            lock (operationGate)
            {
                var manifest = SelectManifest();
                if (!manifest.Succeeded)
                {
                    return manifest.ToReadResult(NotFoundMessage);
                }

                if (manifest.Selection.Record.Active.Id == checkpointId)
                {
                    return ReadReference(manifest.Selection.Record.Active);
                }

                var previous = manifest.Selection.Record.Previous;
                return previous != null && previous.Id == checkpointId
                    ? ReadReference(previous)
                    : CheckpointReadResult.Rejected(CommandOutcome.NoCheckpoint, NotFoundMessage);
            }
        }

        public CommandResult<CheckpointId> Promote(Checkpoint checkpoint)
        {
            if (checkpoint == null)
            {
                return InvalidCommand();
            }

            lock (operationGate)
            {
                return Publish(checkpoint, false);
            }
        }

        public CommandResult<CheckpointId> RecoverPrevious(Checkpoint recoveryCandidate)
        {
            if (recoveryCandidate == null)
            {
                return InvalidCommand();
            }

            lock (operationGate)
            {
                var manifest = SelectManifest();
                if (!manifest.Succeeded)
                {
                    return CommandResult<CheckpointId>.Rejected(manifest.Outcome, manifest.Message);
                }

                var previousReference = manifest.Selection.Record.Previous;
                if (previousReference == null)
                {
                    return CommandResult<CheckpointId>.Rejected(CommandOutcome.NoCheckpoint, NoPreviousMessage);
                }

                var previous = ReadReferenceWithBytes(previousReference);
                if (!previous.Succeeded) return previous.ToCommandResult();

                if (manifest.Selection.Record.Active.Id == recoveryCandidate.Id &&
                    manifest.Selection.Record.Active.Generation == recoveryCandidate.Generation)
                {
                    var alreadyRecovered = ReadReferenceWithBytes(manifest.Selection.Record.Active);
                    var expected = SaveV1Mapper.Serialize(recoveryCandidate);
                    if (!alreadyRecovered.Succeeded) return alreadyRecovered.ToCommandResult();
                    if (!expected.Succeeded || !BytesEqual(alreadyRecovered.Bytes, expected.Value)) return InvalidCommand();
                    return ConfirmDurable(manifest.Selection, manifest.Selection.Record.Active, expected.Value)
                        ?? CommandResult<CheckpointId>.Success(recoveryCandidate.Id);
                }

                if (!IsValidRecoveryCandidate(
                    recoveryCandidate,
                    manifest.Selection.Record.Active,
                    previous.Checkpoint))
                {
                    return InvalidCommand();
                }

                return Publish(recoveryCandidate, true);
            }
        }

        private CommandResult<CheckpointId> Publish(Checkpoint checkpoint, bool recovery)
        {
            var serialized = SaveV1Mapper.Serialize(checkpoint);
            if (!serialized.Succeeded)
            {
                return CommandResult<CheckpointId>.Rejected(serialized.Outcome, serialized.Message);
            }

            var candidateHash = Hash(serialized.Value);
            var current = SelectManifest();
            if (!current.Succeeded && current.Outcome != CommandOutcome.NoCheckpoint &&
                current.Outcome != CommandOutcome.InvalidCheckpoint)
            {
                return CommandResult<CheckpointId>.Rejected(current.Outcome, current.Message);
            }

            DataReference retainedReference = null;
            DataRead retainedData = null;
            if (current.Succeeded)
            {
                if (!recovery)
                {
                    var active = ReadReferenceWithBytes(current.Selection.Record.Active);
                    if (!active.Succeeded)
                    {
                        return active.ToCommandResult();
                    }

                    if (active.Checkpoint.Id == checkpoint.Id && active.Checkpoint.Generation == checkpoint.Generation)
                    {
                        if (!BytesEqual(active.Bytes, serialized.Value) ||
                            !BytesEqual(current.Selection.Record.Active.ContentHash, candidateHash)) return InvalidCommand();
                        return ConfirmDurable(current.Selection, current.Selection.Record.Active, serialized.Value)
                            ?? CommandResult<CheckpointId>.Success(checkpoint.Id);
                    }

                    var previousGeneration = current.Selection.Record.Previous?.Generation ?? -1;
                    if (checkpoint.Id == active.Checkpoint.Id ||
                        (current.Selection.Record.Previous != null && checkpoint.Id == current.Selection.Record.Previous.Id) ||
                        checkpoint.Generation <= active.Checkpoint.Generation ||
                        checkpoint.Generation <= previousGeneration)
                    {
                        return InvalidCommand();
                    }
                }

                // A prior failed flush may leave a readable but volatile selector. Stabilize
                // the retained generation before overwriting either of the other fixed slots.
                retainedReference = recovery
                    ? current.Selection.Record.Previous
                    : current.Selection.Record.Active;
                retainedData = ReadReferenceWithBytes(retainedReference);
                if (!retainedData.Succeeded) return retainedData.ToCommandResult();
            }

            // Keep the candidate separate from both published generations until its flushed
            // bytes have passed the same strict parser and semantic validation used on load.
            var pendingWrite = WriteAndClose(CheckpointStorageFile.Pending, serialized.Value);
            if (pendingWrite != null) return pendingWrite;
            var pendingReadback = ReadDataFile(CheckpointStorageFile.Pending);
            if (!pendingReadback.Succeeded) return pendingReadback.ToCommandResult();
            if (!BytesEqual(pendingReadback.Bytes, serialized.Value) ||
                !BytesEqual(Hash(pendingReadback.Bytes), candidateHash) ||
                !CheckpointMatches(pendingReadback.Checkpoint, checkpoint, serialized.Value))
            {
                return InvalidCommand();
            }

            if (current.Succeeded)
            {
                // Stabilize the retained generation only after pending bytes are valid, and
                // before overwriting a data slot that may hold the prior Previous generation.
                var durable = ConfirmDurable(current.Selection, retainedReference, retainedData.Bytes);
                if (durable != null) return durable;
            }

            var dataFile = current.Succeeded && recovery
                ? current.Selection.Record.Active.File
                : !current.Succeeded || current.Selection.Record.Active.File == CheckpointStorageFile.DataB
                    ? CheckpointStorageFile.DataA
                    : CheckpointStorageFile.DataB;
            var manifestFile = !current.Succeeded || current.Selection.File == CheckpointStorageFile.ManifestB
                ? CheckpointStorageFile.ManifestA
                : CheckpointStorageFile.ManifestB;

            var writeData = WriteAndClose(dataFile, pendingReadback.Bytes);
            if (writeData != null) return writeData;
            var dataReadback = ReadDataFile(dataFile);
            if (!dataReadback.Succeeded) return dataReadback.ToCommandResult();
            if (!BytesEqual(dataReadback.Bytes, pendingReadback.Bytes) ||
                !BytesEqual(Hash(dataReadback.Bytes), candidateHash) ||
                !CheckpointMatches(dataReadback.Checkpoint, checkpoint, serialized.Value))
            {
                return InvalidCommand();
            }

            long nextSequence;
            try
            {
                nextSequence = checked((current.Succeeded ? current.Selection.Record.Sequence : 0L) + 1L);
            }
            catch (OverflowException)
            {
                return InvalidCommand();
            }

            var nextRecord = new ManifestRecord(
                nextSequence,
                new DataReference(dataFile, checkpoint.Id, checkpoint.Generation, candidateHash),
                current.Succeeded
                    ? recovery ? current.Selection.Record.Previous : current.Selection.Record.Active
                    : null);
            var manifestBytes = SerializeManifest(nextRecord);
            var writeManifest = WriteAndClose(manifestFile, manifestBytes);
            if (writeManifest != null) return writeManifest;
            var manifestReadback = ReadManifestFile(manifestFile);
            if (!manifestReadback.Valid || !BytesEqual(manifestReadback.Bytes, manifestBytes) ||
                !ManifestEquals(manifestReadback.Record, nextRecord))
            {
                return manifestReadback.Outcome == CommandOutcome.PersistenceFailed
                    ? PersistenceCommand()
                    : InvalidCommand();
            }

            var published = SelectManifest();
            if (!published.Succeeded || published.Selection.Record.Sequence != nextSequence ||
                published.Selection.File != manifestFile)
            {
                return published.Outcome == CommandOutcome.PersistenceFailed
                    ? PersistenceCommand()
                    : InvalidCommand();
            }

            var activeReadback = ReadReferenceWithBytes(published.Selection.Record.Active);
            if (!activeReadback.Succeeded) return activeReadback.ToCommandResult();
            return BytesEqual(activeReadback.Bytes, serialized.Value) &&
                   CheckpointMatches(activeReadback.Checkpoint, checkpoint, serialized.Value)
                ? CommandResult<CheckpointId>.Success(checkpoint.Id)
                : InvalidCommand();
        }

        private CommandResult<CheckpointId> ConfirmDurable(
            ManifestSelection selection, DataReference reference, byte[] expectedBytes)
        {
            try
            {
                backend.FlushExisting(reference.File);
                backend.FlushExisting(selection.File);
            }
            catch (Exception exception) when (IsPersistenceFailure(exception))
            {
                return PersistenceCommand();
            }

            var data = ReadReferenceWithBytes(reference);
            if (!data.Succeeded) return data.ToCommandResult();
            var manifest = SelectManifest();
            if (!manifest.Succeeded)
                return CommandResult<CheckpointId>.Rejected(manifest.Outcome, manifest.Message);
            return BytesEqual(data.Bytes, expectedBytes) && manifest.Selection.File == selection.File &&
                   ManifestEquals(manifest.Selection.Record, selection.Record)
                ? null
                : InvalidCommand();
        }

        private CommandResult<CheckpointId> WriteAndClose(CheckpointStorageFile file, byte[] bytes)
        {
            Exception failure = null;
            var closeRequired = false;
            try
            {
                closeRequired = true;
                backend.Create(file);
                backend.Write(file, bytes);
                backend.Flush(file);
            }
            catch (Exception exception) when (IsPersistenceFailure(exception))
            {
                failure = exception;
            }
            finally
            {
                if (closeRequired)
                {
                    try
                    {
                        backend.Close(file);
                    }
                    catch (Exception exception) when (IsPersistenceFailure(exception))
                    {
                        if (failure == null) failure = exception;
                    }
                }
            }

            return failure == null ? null : PersistenceCommand();
        }

        private ManifestSelectionResult SelectManifest()
        {
            var first = ReadManifestFile(CheckpointStorageFile.ManifestA);
            var second = ReadManifestFile(CheckpointStorageFile.ManifestB);
            if (first.Outcome == CommandOutcome.PersistenceFailed || second.Outcome == CommandOutcome.PersistenceFailed)
            {
                return ManifestSelectionResult.Failure(CommandOutcome.PersistenceFailed, PersistenceMessage);
            }

            if (first.Valid && second.Valid)
            {
                if (first.Record.Sequence == second.Record.Sequence)
                {
                    return ManifestSelectionResult.Failure(CommandOutcome.InvalidCheckpoint, InvalidMessage);
                }

                return first.Record.Sequence > second.Record.Sequence
                    ? ManifestSelectionResult.Success(new ManifestSelection(CheckpointStorageFile.ManifestA, first.Record))
                    : ManifestSelectionResult.Success(new ManifestSelection(CheckpointStorageFile.ManifestB, second.Record));
            }

            if (first.Valid) return ManifestSelectionResult.Success(new ManifestSelection(CheckpointStorageFile.ManifestA, first.Record));
            if (second.Valid) return ManifestSelectionResult.Success(new ManifestSelection(CheckpointStorageFile.ManifestB, second.Record));
            if (!first.Exists && !second.Exists)
            {
                return ManifestSelectionResult.Failure(CommandOutcome.NoCheckpoint, NoActiveMessage);
            }

            return ManifestSelectionResult.Failure(CommandOutcome.InvalidCheckpoint, InvalidMessage);
        }

        private ManifestFileRead ReadManifestFile(CheckpointStorageFile file)
        {
            try
            {
                if (!backend.Exists(file)) return ManifestFileRead.Absent();
                var length = backend.GetLength(file);
                if (length < 0 || length > MaximumManifestByteCount || length != ManifestLength)
                {
                    return ManifestFileRead.Invalid();
                }

                var bytes = backend.ReadExactly(file, (int)length);
                if (bytes == null || bytes.Length != length || backend.GetLength(file) != length)
                {
                    return ManifestFileRead.PersistenceFailure();
                }

                return TryParseManifest(bytes, out var record)
                    ? ManifestFileRead.Success(bytes, record)
                    : ManifestFileRead.Invalid();
            }
            catch (Exception exception) when (IsPersistenceFailure(exception))
            {
                return ManifestFileRead.PersistenceFailure();
            }
        }

        private DataRead ReadReferenceWithBytes(DataReference reference)
        {
            var read = ReadDataFile(reference.File);
            if (!read.Succeeded) return read;
            if (!BytesEqual(Hash(read.Bytes), reference.ContentHash) ||
                read.Checkpoint.Id != reference.Id || read.Checkpoint.Generation != reference.Generation)
            {
                return DataRead.Failure(CommandOutcome.InvalidCheckpoint, InvalidMessage);
            }

            return read;
        }

        private CheckpointReadResult ReadReference(DataReference reference)
        {
            var read = ReadReferenceWithBytes(reference);
            return read.Succeeded
                ? CheckpointReadResult.Success(read.Checkpoint)
                : CheckpointReadResult.Rejected(read.Outcome, read.Message);
        }

        private DataRead ReadDataFile(CheckpointStorageFile file)
        {
            try
            {
                if (!backend.Exists(file)) return DataRead.Failure(CommandOutcome.InvalidCheckpoint, InvalidMessage);
                var length = backend.GetLength(file);
                if (length < 0 || length > SaveV1Mapper.MaximumByteCount)
                {
                    return DataRead.Failure(CommandOutcome.InvalidCheckpoint, InvalidMessage);
                }

                var bytes = backend.ReadExactly(file, (int)length);
                if (bytes == null || bytes.Length != length || backend.GetLength(file) != length)
                {
                    return DataRead.Failure(CommandOutcome.PersistenceFailed, PersistenceMessage);
                }

                var mapped = SaveV1Mapper.Deserialize(bytes, catalog);
                return mapped.Succeeded
                    ? DataRead.Success(bytes, mapped.Checkpoint)
                    : DataRead.Failure(mapped.Outcome, mapped.Message);
            }
            catch (Exception exception) when (IsPersistenceFailure(exception))
            {
                return DataRead.Failure(CommandOutcome.PersistenceFailed, PersistenceMessage);
            }
        }

        private static bool IsValidRecoveryCandidate(
            Checkpoint candidate,
            DataReference active,
            Checkpoint previous)
        {
            if (candidate.Id == active.Id || candidate.Id == previous.Id ||
                candidate.Generation <= active.Generation || candidate.Generation <= previous.Generation ||
                candidate.OfflinePolicyRevision != previous.OfflinePolicyRevision ||
                !candidate.ReconciledFromCheckpointId.HasValue ||
                candidate.ReconciledFromCheckpointId.Value != previous.Id)
            {
                return false;
            }

            var normalized = new Checkpoint(
                previous.Id,
                previous.Generation,
                previous.DepartureUtc,
                previous.OfflinePolicyRevision,
                previous.ReconciledFromCheckpointId,
                candidate.CreateFactoryCopy());
            var normalizedBytes = SaveV1Mapper.Serialize(normalized);
            var previousBytes = SaveV1Mapper.Serialize(previous);
            return normalizedBytes.Succeeded && previousBytes.Succeeded &&
                   BytesEqual(normalizedBytes.Value, previousBytes.Value);
        }

        private static bool CheckpointMatches(Checkpoint actual, Checkpoint expected, byte[] expectedBytes)
        {
            var actualBytes = SaveV1Mapper.Serialize(actual);
            return actualBytes.Succeeded && BytesEqual(actualBytes.Value, expectedBytes) &&
                   actual.Id == expected.Id && actual.Generation == expected.Generation;
        }

        private static byte[] SerializeManifest(ManifestRecord record)
        {
            byte[] payload;
            using (var stream = new MemoryStream())
            using (var writer = new BinaryWriter(stream, Encoding.UTF8, true))
            {
                writer.Write(ManifestMagic);
                writer.Write(record.Sequence);
                WriteReference(writer, record.Active);
                writer.Write(record.Previous != null);
                WriteReference(writer, record.Previous ?? DataReference.Empty);
                writer.Flush();
                payload = stream.ToArray();
            }

            var result = new byte[payload.Length + 32];
            Buffer.BlockCopy(payload, 0, result, 0, payload.Length);
            Buffer.BlockCopy(Hash(payload), 0, result, payload.Length, 32);
            return result;
        }

        private static bool TryParseManifest(byte[] bytes, out ManifestRecord record)
        {
            record = null;
            if (bytes == null || bytes.Length != ManifestLength) return false;
            var payloadLength = bytes.Length - 32;
            var payload = new byte[payloadLength];
            var checksum = new byte[32];
            Buffer.BlockCopy(bytes, 0, payload, 0, payloadLength);
            Buffer.BlockCopy(bytes, payloadLength, checksum, 0, checksum.Length);
            if (!BytesEqual(Hash(payload), checksum)) return false;

            try
            {
                using (var stream = new MemoryStream(payload, false))
                using (var reader = new BinaryReader(stream, Encoding.UTF8, false))
                {
                    if (!BytesEqual(reader.ReadBytes(ManifestMagic.Length), ManifestMagic)) return false;
                    var sequence = reader.ReadInt64();
                    var active = ReadReference(reader);
                    var hasPrevious = reader.ReadBoolean();
                    var previous = ReadReference(reader);
                    if (stream.Position != stream.Length || sequence <= 0 || active == null ||
                        active.IsEmpty || previous == null)
                    {
                        return false;
                    }
                    if (!hasPrevious)
                    {
                        if (!previous.IsEmpty) return false;
                        previous = null;
                    }
                    else if (previous.IsEmpty || previous.Id == active.Id || previous.Generation >= active.Generation)
                    {
                        return false;
                    }

                    record = new ManifestRecord(sequence, active, previous);
                    return true;
                }
            }
            catch (Exception exception) when (exception is IOException || exception is ArgumentException ||
                                               exception is EndOfStreamException || exception is FormatException)
            {
                return false;
            }
        }

        private static void WriteReference(BinaryWriter writer, DataReference reference)
        {
            writer.Write(reference.File == CheckpointStorageFile.DataB ? (byte)1 : (byte)0);
            writer.Write(reference.IsEmpty ? Guid.Empty.ToByteArray() : new Guid(reference.Id.Value).ToByteArray());
            writer.Write(reference.Generation);
            writer.Write(reference.ContentHash);
        }

        private static DataReference ReadReference(BinaryReader reader)
        {
            var slot = reader.ReadByte();
            if (slot > 1) return null;
            var id = new Guid(reader.ReadBytes(16));
            var generation = reader.ReadInt64();
            var hash = reader.ReadBytes(32);
            if (hash.Length != 32 || generation < 0) return null;
            if (id == Guid.Empty)
            {
                return generation == 0 && hash.All(value => value == 0)
                    ? DataReference.Empty
                    : null;
            }

            return new DataReference(
                slot == 0 ? CheckpointStorageFile.DataA : CheckpointStorageFile.DataB,
                new CheckpointId(id),
                generation,
                hash);
        }

        private static bool ManifestEquals(ManifestRecord left, ManifestRecord right)
        {
            return left != null && right != null && left.Sequence == right.Sequence &&
                   ReferenceEquals(left.Active, right.Active) && ReferenceEquals(left.Previous, right.Previous);
        }

        private static bool ReferenceEquals(DataReference left, DataReference right)
        {
            if (left == null || right == null) return left == right;
            return left.File == right.File && left.Id == right.Id && left.Generation == right.Generation &&
                   BytesEqual(left.ContentHash, right.ContentHash);
        }

        private static byte[] Hash(byte[] bytes)
        {
            using (var sha = SHA256.Create()) return sha.ComputeHash(bytes);
        }

        private static bool BytesEqual(byte[] left, byte[] right)
        {
            if (left == null || right == null || left.Length != right.Length) return false;
            var difference = 0;
            for (var index = 0; index < left.Length; index++) difference |= left[index] ^ right[index];
            return difference == 0;
        }

        private static CommandResult<CheckpointId> InvalidCommand() =>
            CommandResult<CheckpointId>.Rejected(CommandOutcome.InvalidCheckpoint, InvalidMessage);

        private static CommandResult<CheckpointId> PersistenceCommand() =>
            CommandResult<CheckpointId>.Rejected(CommandOutcome.PersistenceFailed, PersistenceMessage);

        private static bool IsPersistenceFailure(Exception exception)
        {
            return exception is IOException || exception is UnauthorizedAccessException ||
                   exception is SecurityException || exception is InvalidOperationException ||
                   exception is NotSupportedException || exception is PlatformNotSupportedException;
        }

        private static ICheckpointStorageBackend CreateBackend(string persistentDataRoot)
        {
            if (string.IsNullOrWhiteSpace(persistentDataRoot))
            {
                throw new ArgumentException("A persistence root is required.", nameof(persistentDataRoot));
            }

            var root = Path.GetFullPath(persistentDataRoot);
            return new SystemIoCheckpointStorageBackend(
                Path.Combine(root, ApplicationDirectory, CheckpointDirectory, VersionDirectory));
        }

        private static object GetOperationGate(string storageIdentity)
        {
            lock (LockMapGate)
            {
                if (!LockMap.TryGetValue(storageIdentity, out var gate))
                {
                    gate = new object();
                    LockMap.Add(storageIdentity, gate);
                }

                return gate;
            }
        }

        private sealed class DataReference
        {
            public static readonly DataReference Empty = new DataReference(
                CheckpointStorageFile.DataA, default(CheckpointId), 0, new byte[32], true);

            public DataReference(CheckpointStorageFile file, CheckpointId id, long generation, byte[] contentHash)
                : this(file, id, generation, contentHash, false)
            {
            }

            private DataReference(CheckpointStorageFile file, CheckpointId id, long generation, byte[] contentHash, bool empty)
            {
                File = file;
                Id = id;
                Generation = generation;
                ContentHash = (byte[])contentHash.Clone();
                IsEmpty = empty;
            }

            public CheckpointStorageFile File { get; }
            public CheckpointId Id { get; }
            public long Generation { get; }
            public byte[] ContentHash { get; }
            public bool IsEmpty { get; }
        }

        private sealed class ManifestRecord
        {
            public ManifestRecord(long sequence, DataReference active, DataReference previous)
            {
                Sequence = sequence;
                Active = active;
                Previous = previous;
            }

            public long Sequence { get; }
            public DataReference Active { get; }
            public DataReference Previous { get; }
        }

        private sealed class ManifestSelection
        {
            public ManifestSelection(CheckpointStorageFile file, ManifestRecord record)
            {
                File = file;
                Record = record;
            }

            public CheckpointStorageFile File { get; }
            public ManifestRecord Record { get; }
        }

        private sealed class ManifestSelectionResult
        {
            private ManifestSelectionResult(CommandOutcome outcome, string message, ManifestSelection selection)
            {
                Outcome = outcome;
                Message = message;
                Selection = selection;
            }

            public bool Succeeded => Outcome == CommandOutcome.Success;
            public CommandOutcome Outcome { get; }
            public string Message { get; }
            public ManifestSelection Selection { get; }
            public static ManifestSelectionResult Success(ManifestSelection selection) =>
                new ManifestSelectionResult(CommandOutcome.Success, string.Empty, selection);
            public static ManifestSelectionResult Failure(CommandOutcome outcome, string message) =>
                new ManifestSelectionResult(outcome, message, null);
            public CheckpointReadResult ToReadResult(string absentMessage) =>
                CheckpointReadResult.Rejected(Outcome, Outcome == CommandOutcome.NoCheckpoint ? absentMessage : Message);
        }

        private sealed class ManifestFileRead
        {
            private ManifestFileRead(bool exists, bool valid, CommandOutcome outcome, byte[] bytes, ManifestRecord record)
            {
                Exists = exists;
                Valid = valid;
                Outcome = outcome;
                Bytes = bytes;
                Record = record;
            }

            public bool Exists { get; }
            public bool Valid { get; }
            public CommandOutcome Outcome { get; }
            public byte[] Bytes { get; }
            public ManifestRecord Record { get; }
            public static ManifestFileRead Absent() => new ManifestFileRead(false, false, CommandOutcome.NoCheckpoint, null, null);
            public static ManifestFileRead Invalid() => new ManifestFileRead(true, false, CommandOutcome.InvalidCheckpoint, null, null);
            public static ManifestFileRead PersistenceFailure() => new ManifestFileRead(true, false, CommandOutcome.PersistenceFailed, null, null);
            public static ManifestFileRead Success(byte[] bytes, ManifestRecord record) =>
                new ManifestFileRead(true, true, CommandOutcome.Success, bytes, record);
        }

        private sealed class DataRead
        {
            private DataRead(CommandOutcome outcome, string message, byte[] bytes, Checkpoint checkpoint)
            {
                Outcome = outcome;
                Message = message;
                Bytes = bytes;
                Checkpoint = checkpoint;
            }

            public bool Succeeded => Outcome == CommandOutcome.Success;
            public CommandOutcome Outcome { get; }
            public string Message { get; }
            public byte[] Bytes { get; }
            public Checkpoint Checkpoint { get; }
            public static DataRead Success(byte[] bytes, Checkpoint checkpoint) =>
                new DataRead(CommandOutcome.Success, string.Empty, bytes, checkpoint);
            public static DataRead Failure(CommandOutcome outcome, string message) =>
                new DataRead(outcome, message, null, null);
            public CommandResult<CheckpointId> ToCommandResult() =>
                CommandResult<CheckpointId>.Rejected(Outcome, Message);
        }
    }

    public sealed class SystemIoCheckpointStorageBackend : ICheckpointStorageBackend
    {
        // Redundant fixed slots avoid rename/replace atomicity assumptions. Portable Unity System.IO
        // cannot fsync directory entries, so first-file power loss and device interruption remain T038 gates.
        private readonly string directory;
        private FileStream openStream;
        private CheckpointStorageFile? openFile;

        public SystemIoCheckpointStorageBackend(string directory)
        {
            if (string.IsNullOrWhiteSpace(directory))
            {
                throw new ArgumentException("A checkpoint directory is required.", nameof(directory));
            }

            this.directory = Path.GetFullPath(directory);
        }

        public string StorageIdentity => directory;
        public bool Exists(CheckpointStorageFile file)
        {
            // Directory/access errors must enter the store's typed operation boundary.
            Directory.CreateDirectory(directory);
            return File.Exists(PathFor(file));
        }
        public long GetLength(CheckpointStorageFile file) => new FileInfo(PathFor(file)).Length;

        public byte[] ReadExactly(CheckpointStorageFile file, int expectedLength)
        {
            var bytes = new byte[expectedLength];
            using (var stream = new FileStream(PathFor(file), FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                var offset = 0;
                while (offset < bytes.Length)
                {
                    var count = stream.Read(bytes, offset, bytes.Length - offset);
                    if (count == 0) throw new EndOfStreamException();
                    offset += count;
                }

                if (stream.ReadByte() != -1) throw new IOException("Checkpoint length changed while reading.");
            }

            return bytes;
        }

        public void Create(CheckpointStorageFile file)
        {
            if (openStream != null) throw new InvalidOperationException("A checkpoint stream is already open.");
            Directory.CreateDirectory(directory);
            openFile = file;
            openStream = new FileStream(
                PathFor(file), FileMode.Create, FileAccess.Write, FileShare.None, 4096, FileOptions.WriteThrough);
        }

        public void Write(CheckpointStorageFile file, byte[] bytes)
        {
            Require(file).Write(bytes, 0, bytes.Length);
        }

        public void Flush(CheckpointStorageFile file)
        {
            Require(file).Flush(true);
        }

        public void FlushExisting(CheckpointStorageFile file)
        {
            using (var stream = new FileStream(PathFor(file), FileMode.Open, FileAccess.Write, FileShare.None))
                stream.Flush(true);
        }

        public void Close(CheckpointStorageFile file)
        {
            if (openStream == null) return;
            if (openFile != file) throw new InvalidOperationException("The wrong checkpoint slot was closed.");
            var stream = openStream;
            openStream = null;
            openFile = null;
            stream.Dispose();
        }

        private FileStream Require(CheckpointStorageFile file)
        {
            if (openStream == null || openFile != file)
            {
                throw new InvalidOperationException("The checkpoint slot is not open.");
            }

            return openStream;
        }

        private string PathFor(CheckpointStorageFile file)
        {
            switch (file)
            {
                case CheckpointStorageFile.DataA: return Path.Combine(directory, JsonCheckpointStore.DataAFileName);
                case CheckpointStorageFile.DataB: return Path.Combine(directory, JsonCheckpointStore.DataBFileName);
                case CheckpointStorageFile.ManifestA: return Path.Combine(directory, JsonCheckpointStore.ManifestAFileName);
                case CheckpointStorageFile.ManifestB: return Path.Combine(directory, JsonCheckpointStore.ManifestBFileName);
                case CheckpointStorageFile.Pending: return Path.Combine(directory, JsonCheckpointStore.PendingFileName);
                default: throw new ArgumentOutOfRangeException(nameof(file));
            }
        }
    }
}
