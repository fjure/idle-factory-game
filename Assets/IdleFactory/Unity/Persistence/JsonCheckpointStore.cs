using System;
using System.Collections.Generic;
using System.IO;
using System.Security;
using IdleFactory.Core.Application;
using IdleFactory.Domain;

namespace IdleFactory.Unity.Persistence
{
    public enum CheckpointFile
    {
        Pending = 0,
        Active = 1,
        Previous = 2,
    }

    // Fixed slots keep save content and checkpoint IDs out of path construction.
    public interface ICheckpointFileBackend
    {
        string StorageIdentity { get; }
        bool SupportsDirectoryMetadataFlush { get; }
        bool Exists(CheckpointFile file);
        long GetLength(CheckpointFile file);
        byte[] ReadExactly(CheckpointFile file, int expectedLength);
        void CreatePending();
        void WritePending(byte[] bytes);
        void FlushPending();
        void ClosePending();
        void PromotePending(bool replaceActive);
        void FlushDirectory();
        void DeletePending();
    }

    public sealed class JsonCheckpointStore : ICheckpointStore
    {
        public const string ActiveFileName = "factory-checkpoint.json";
        public const string PreviousFileName = "factory-checkpoint.previous.json";
        public const string PendingFileName = "factory-checkpoint.pending.json";

        private const string ApplicationDirectory = "IdleFactory";
        private const string CheckpointDirectory = "Checkpoints";
        private const string VersionDirectory = "v1";
        private const string NoActiveMessage = "No active checkpoint is available.";
        private const string NoPreviousMessage = "No previous checkpoint is available.";
        private const string NotFoundMessage = "The requested checkpoint is not retained.";
        private const string InvalidMessage = "Checkpoint data is invalid.";
        private const string PersistenceMessage = "Checkpoint storage operation failed.";

        private static readonly object LockMapGate = new object();
        private static readonly Dictionary<string, object> LockMap =
            new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);

        private readonly DefinitionCatalog catalog;
        private readonly ICheckpointFileBackend backend;
        private readonly object operationGate;
        private Checkpoint pendingRecovery;

        public JsonCheckpointStore(string persistentDataRoot, DefinitionCatalog catalog)
            : this(persistentDataRoot, catalog, CreateBackend(persistentDataRoot))
        {
        }

        public JsonCheckpointStore(
            string persistentDataRoot,
            DefinitionCatalog catalog,
            ICheckpointFileBackend backend)
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

        public bool SupportsDirectoryMetadataFlush => backend.SupportsDirectoryMetadataFlush;

        public CheckpointReadResult ReadActive()
        {
            lock (operationGate)
            {
                return ReadSlot(CheckpointFile.Active, NoActiveMessage);
            }
        }

        public CheckpointReadResult ReadPrevious()
        {
            lock (operationGate)
            {
                return ReadSlot(CheckpointFile.Previous, NoPreviousMessage);
            }
        }

        public CheckpointReadResult Read(CheckpointId checkpointId)
        {
            _ = checkpointId.Value;
            lock (operationGate)
            {
                var active = ReadSlot(CheckpointFile.Active, NoActiveMessage);
                if (active.Succeeded && active.Checkpoint.Id == checkpointId)
                {
                    return active;
                }

                if (!active.Succeeded && active.Outcome != CommandOutcome.NoCheckpoint)
                {
                    return active;
                }

                var previous = ReadSlot(CheckpointFile.Previous, NoPreviousMessage);
                if (previous.Succeeded && previous.Checkpoint.Id == checkpointId)
                {
                    return previous;
                }

                if (!previous.Succeeded && previous.Outcome != CommandOutcome.NoCheckpoint)
                {
                    return previous;
                }

                return CheckpointReadResult.Rejected(CommandOutcome.NoCheckpoint, NotFoundMessage);
            }
        }

        public CommandResult<CheckpointId> Promote(Checkpoint checkpoint)
        {
            if (checkpoint == null)
            {
                return CommandResult<CheckpointId>.Rejected(CommandOutcome.InvalidCheckpoint, InvalidMessage);
            }

            lock (operationGate)
            {
                return PromoteCore(checkpoint, false, false);
            }
        }

        // ICheckpointStore intentionally remains unchanged. The later application command must depend on
        // this concrete capability (or an application adapter) to make rollback an explicit player action.
        public CommandResult<CheckpointId> RecoverPrevious()
        {
            lock (operationGate)
            {
                if (pendingRecovery != null)
                {
                    var active = ReadSlot(CheckpointFile.Active, NoActiveMessage);
                    if (active.Succeeded && SameGeneration(active.Checkpoint, pendingRecovery))
                    {
                        var verified = VerifyPublished(pendingRecovery);
                        if (verified.Succeeded)
                        {
                            pendingRecovery = null;
                        }

                        return verified;
                    }
                }

                var previous = ReadSlot(CheckpointFile.Previous, NoPreviousMessage);
                if (!previous.Succeeded)
                {
                    return CommandResult<CheckpointId>.Rejected(previous.Outcome, previous.Message);
                }

                pendingRecovery = previous.Checkpoint;
                var result = PromoteCore(previous.Checkpoint, true, true);
                if (result.Succeeded)
                {
                    pendingRecovery = null;
                }

                return result;
            }
        }

        private CommandResult<CheckpointId> PromoteCore(
            Checkpoint checkpoint,
            bool allowNonIncreasingGeneration,
            bool allowInvalidActive)
        {
            var serialized = SaveV1Mapper.Serialize(checkpoint);
            if (!serialized.Succeeded)
            {
                return CommandResult<CheckpointId>.Rejected(serialized.Outcome, serialized.Message);
            }

            bool activeExists;
            try
            {
                activeExists = backend.Exists(CheckpointFile.Active);
            }
            catch (Exception exception) when (IsPersistenceFailure(exception))
            {
                return PersistenceFailure();
            }

            if (activeExists)
            {
                var active = ReadSlot(CheckpointFile.Active, NoActiveMessage);
                if (active.Succeeded && SameGeneration(active.Checkpoint, checkpoint))
                {
                    return VerifyPublished(checkpoint);
                }

                if (!active.Succeeded && !allowInvalidActive)
                {
                    return CommandResult<CheckpointId>.Rejected(active.Outcome, active.Message);
                }

                if (active.Succeeded && !allowNonIncreasingGeneration &&
                    checkpoint.Generation <= active.Checkpoint.Generation)
                {
                    return CommandResult<CheckpointId>.Rejected(CommandOutcome.InvalidCheckpoint, InvalidMessage);
                }
            }

            try
            {
                if (backend.Exists(CheckpointFile.Pending))
                {
                    backend.DeletePending();
                }
            }
            catch (Exception exception) when (IsPersistenceFailure(exception))
            {
                return PersistenceFailure();
            }

            var writeFailure = WritePending(serialized.Value);
            if (writeFailure != null)
            {
                return writeFailure;
            }

            var candidate = ReadSlot(CheckpointFile.Pending, NoActiveMessage);
            if (!candidate.Succeeded)
            {
                return CommandResult<CheckpointId>.Rejected(candidate.Outcome, candidate.Message);
            }

            if (!SameGeneration(candidate.Checkpoint, checkpoint))
            {
                return CommandResult<CheckpointId>.Rejected(CommandOutcome.InvalidCheckpoint, InvalidMessage);
            }

            try
            {
                backend.PromotePending(activeExists);
            }
            catch (Exception exception) when (IsPersistenceFailure(exception))
            {
                return PersistenceFailure();
            }

            return VerifyPublished(checkpoint);
        }

        private CommandResult<CheckpointId> WritePending(byte[] bytes)
        {
            Exception failure = null;
            var opened = false;
            try
            {
                opened = true;
                backend.CreatePending();
                backend.WritePending(bytes);
                backend.FlushPending();
            }
            catch (Exception exception) when (IsPersistenceFailure(exception))
            {
                failure = exception;
            }
            finally
            {
                if (opened)
                {
                    try
                    {
                        backend.ClosePending();
                    }
                    catch (Exception exception) when (IsPersistenceFailure(exception))
                    {
                        if (failure == null)
                        {
                            failure = exception;
                        }
                    }
                }
            }

            return failure == null ? null : PersistenceFailure();
        }

        private CommandResult<CheckpointId> VerifyPublished(Checkpoint expected)
        {
            try
            {
                // System.IO has no portable directory fsync. Unsupported production platforms execute
                // this modeled boundary as a documented no-op; Success means flushed file data plus a
                // validated observable generation, not an unsupported power-loss guarantee.
                backend.FlushDirectory();
            }
            catch (Exception exception) when (IsPersistenceFailure(exception))
            {
                return PersistenceFailure();
            }

            var active = ReadSlot(CheckpointFile.Active, NoActiveMessage);
            if (!active.Succeeded)
            {
                return CommandResult<CheckpointId>.Rejected(active.Outcome, active.Message);
            }

            return SameGeneration(active.Checkpoint, expected)
                ? CommandResult<CheckpointId>.Success(active.Checkpoint.Id)
                : CommandResult<CheckpointId>.Rejected(CommandOutcome.InvalidCheckpoint, InvalidMessage);
        }

        private CheckpointReadResult ReadSlot(CheckpointFile file, string absentMessage)
        {
            try
            {
                if (!backend.Exists(file))
                {
                    return CheckpointReadResult.Rejected(CommandOutcome.NoCheckpoint, absentMessage);
                }

                var length = backend.GetLength(file);
                if (length < 0 || length > SaveV1Mapper.MaximumByteCount)
                {
                    return CheckpointReadResult.Rejected(CommandOutcome.InvalidCheckpoint, InvalidMessage);
                }

                var bytes = backend.ReadExactly(file, (int)length);
                if (bytes == null || bytes.Length != length || backend.GetLength(file) != length)
                {
                    return CheckpointReadResult.Rejected(CommandOutcome.PersistenceFailed, PersistenceMessage);
                }

                return SaveV1Mapper.Deserialize(bytes, catalog);
            }
            catch (Exception exception) when (IsPersistenceFailure(exception))
            {
                return CheckpointReadResult.Rejected(CommandOutcome.PersistenceFailed, PersistenceMessage);
            }
        }

        private static bool SameGeneration(Checkpoint left, Checkpoint right)
        {
            return left.Id == right.Id && left.Generation == right.Generation;
        }

        private static CommandResult<CheckpointId> PersistenceFailure()
        {
            return CommandResult<CheckpointId>.Rejected(CommandOutcome.PersistenceFailed, PersistenceMessage);
        }

        private static bool IsPersistenceFailure(Exception exception)
        {
            return exception is IOException ||
                   exception is UnauthorizedAccessException ||
                   exception is SecurityException ||
                   exception is InvalidOperationException;
        }

        private static ICheckpointFileBackend CreateBackend(string persistentDataRoot)
        {
            if (string.IsNullOrWhiteSpace(persistentDataRoot))
            {
                throw new ArgumentException("A persistence root is required.", nameof(persistentDataRoot));
            }

            var root = Path.GetFullPath(persistentDataRoot);
            var directory = Path.Combine(root, ApplicationDirectory, CheckpointDirectory, VersionDirectory);
            return new SystemIoCheckpointFileBackend(directory);
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
    }

    public sealed class SystemIoCheckpointFileBackend : ICheckpointFileBackend
    {
        private readonly string directory;
        private readonly string pendingPath;
        private readonly string activePath;
        private readonly string previousPath;
        private FileStream pendingStream;

        public SystemIoCheckpointFileBackend(string directory)
        {
            if (string.IsNullOrWhiteSpace(directory))
            {
                throw new ArgumentException("A checkpoint directory is required.", nameof(directory));
            }

            this.directory = Path.GetFullPath(directory);
            Directory.CreateDirectory(this.directory);
            pendingPath = Path.Combine(this.directory, JsonCheckpointStore.PendingFileName);
            activePath = Path.Combine(this.directory, JsonCheckpointStore.ActiveFileName);
            previousPath = Path.Combine(this.directory, JsonCheckpointStore.PreviousFileName);
        }

        public string StorageIdentity => directory;

        public bool SupportsDirectoryMetadataFlush => false;

        public bool Exists(CheckpointFile file) => File.Exists(PathFor(file));

        public long GetLength(CheckpointFile file) => new FileInfo(PathFor(file)).Length;

        public byte[] ReadExactly(CheckpointFile file, int expectedLength)
        {
            var bytes = new byte[expectedLength];
            using (var stream = new FileStream(PathFor(file), FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                var offset = 0;
                while (offset < bytes.Length)
                {
                    var count = stream.Read(bytes, offset, bytes.Length - offset);
                    if (count == 0)
                    {
                        throw new EndOfStreamException();
                    }

                    offset += count;
                }

                if (stream.ReadByte() != -1)
                {
                    throw new IOException("Checkpoint length changed while reading.");
                }
            }

            return bytes;
        }

        public void CreatePending()
        {
            if (pendingStream != null)
            {
                throw new InvalidOperationException("A pending checkpoint stream is already open.");
            }

            pendingStream = new FileStream(
                pendingPath,
                FileMode.Create,
                FileAccess.Write,
                FileShare.None,
                4096,
                FileOptions.WriteThrough);
        }

        public void WritePending(byte[] bytes)
        {
            RequirePendingStream().Write(bytes, 0, bytes.Length);
        }

        public void FlushPending()
        {
            RequirePendingStream().Flush(true);
        }

        public void ClosePending()
        {
            var stream = pendingStream;
            if (stream == null)
            {
                return;
            }

            pendingStream = null;
            stream.Dispose();
        }

        public void PromotePending(bool replaceActive)
        {
            if (pendingStream != null)
            {
                throw new InvalidOperationException("The pending checkpoint must be closed before promotion.");
            }

            if (replaceActive)
            {
                File.Replace(pendingPath, activePath, previousPath, true);
            }
            else
            {
                File.Move(pendingPath, activePath);
            }
        }

        public void FlushDirectory()
        {
            // Deliberate modeled boundary: portable managed directory fsync is unavailable in Unity's
            // System.IO surface. Target-platform interruption testing remains required by the contract.
        }

        public void DeletePending()
        {
            if (pendingStream != null)
            {
                throw new InvalidOperationException("Cannot delete an open pending checkpoint.");
            }

            File.Delete(pendingPath);
        }

        private FileStream RequirePendingStream()
        {
            return pendingStream ?? throw new InvalidOperationException("No pending checkpoint stream is open.");
        }

        private string PathFor(CheckpointFile file)
        {
            switch (file)
            {
                case CheckpointFile.Pending: return pendingPath;
                case CheckpointFile.Active: return activePath;
                case CheckpointFile.Previous: return previousPath;
                default: throw new ArgumentOutOfRangeException(nameof(file));
            }
        }
    }
}
