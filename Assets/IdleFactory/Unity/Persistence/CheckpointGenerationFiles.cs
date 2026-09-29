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

    /// <summary>
    /// Read-only checkpoint slot primitives. A holder of only this interface cannot create, write,
    /// flush or close a slot; <see cref="SaveV1CheckpointSource"/> depends on nothing wider (FR-028).
    /// </summary>
    public interface ICheckpointStorageReader
    {
        string StorageIdentity { get; }
        bool Exists(CheckpointStorageFile file);
        long GetLength(CheckpointStorageFile file);
        byte[] ReadExactly(CheckpointStorageFile file, int expectedLength);
    }

    // Infrastructure-only transaction primitives. Gameplay code must use ICheckpointStore.
    public interface ICheckpointStorageBackend : ICheckpointStorageReader
    {
        void Create(CheckpointStorageFile file);
        void Write(CheckpointStorageFile file, byte[] bytes);
        void Flush(CheckpointStorageFile file);
        // Flush an existing slot without truncating or rewriting its bytes.
        void FlushExisting(CheckpointStorageFile file);
        void Close(CheckpointStorageFile file);
    }

    /// <summary>
    /// The format of one checkpoint generation's data file. The generation protocol never interprets
    /// data bytes itself; every parse, semantic check and canonical re-encoding goes through the codec.
    /// </summary>
    public interface ICheckpointGenerationCodec<TCheckpoint> where TCheckpoint : class
    {
        /// <summary>Upper bound on a data file's length, checked before any byte is read.</summary>
        int MaximumByteCount { get; }

        CheckpointId GetId(TCheckpoint checkpoint);
        long GetGeneration(TCheckpoint checkpoint);

        CommandResult<byte[]> Serialize(TCheckpoint checkpoint);

        /// <summary>Strict parse plus full semantic validation; must fail closed and never throw on bad input.</summary>
        CheckpointDecodeResult<TCheckpoint> Deserialize(byte[] bytes);

        /// <summary>True only when <paramref name="checkpoint"/> re-encodes to exactly <paramref name="bytes"/>.</summary>
        bool ValidateBytes(TCheckpoint checkpoint, byte[] bytes);
    }

    public sealed class CheckpointDecodeResult<TCheckpoint> where TCheckpoint : class
    {
        private CheckpointDecodeResult(CommandOutcome outcome, string message, TCheckpoint checkpoint)
        {
            Outcome = outcome;
            Message = message ?? string.Empty;
            Checkpoint = checkpoint;
        }

        public bool Succeeded => Outcome == CommandOutcome.Success;
        public CommandOutcome Outcome { get; }
        public string Message { get; }
        public TCheckpoint Checkpoint { get; }

        public static CheckpointDecodeResult<TCheckpoint> Success(TCheckpoint checkpoint) =>
            new CheckpointDecodeResult<TCheckpoint>(
                CommandOutcome.Success,
                string.Empty,
                checkpoint ?? throw new ArgumentNullException(nameof(checkpoint)));

        public static CheckpointDecodeResult<TCheckpoint> Rejected(CommandOutcome outcome, string message)
        {
            if (outcome == CommandOutcome.Success)
            {
                throw new ArgumentException("A rejected decode requires a non-success outcome.", nameof(outcome));
            }

            return new CheckpointDecodeResult<TCheckpoint>(outcome, message, null);
        }
    }

    /// <summary>The fixed slot names of one save version under <c>IdleFactory/Checkpoints/&lt;version&gt;/</c>.</summary>
    public sealed class CheckpointFileLayout
    {
        private const string ApplicationDirectory = "IdleFactory";
        private const string CheckpointDirectory = "Checkpoints";

        public CheckpointFileLayout(
            string versionDirectory,
            string dataAFileName,
            string dataBFileName,
            string manifestAFileName,
            string manifestBFileName,
            string pendingFileName)
        {
            VersionDirectory = RequireName(versionDirectory, nameof(versionDirectory));
            DataAFileName = RequireName(dataAFileName, nameof(dataAFileName));
            DataBFileName = RequireName(dataBFileName, nameof(dataBFileName));
            ManifestAFileName = RequireName(manifestAFileName, nameof(manifestAFileName));
            ManifestBFileName = RequireName(manifestBFileName, nameof(manifestBFileName));
            PendingFileName = RequireName(pendingFileName, nameof(pendingFileName));
            var names = new[] { DataAFileName, DataBFileName, ManifestAFileName, ManifestBFileName, PendingFileName };
            if (names.Distinct(StringComparer.OrdinalIgnoreCase).Count() != names.Length)
            {
                throw new ArgumentException("Checkpoint slot file names must be distinct.");
            }
        }

        public string VersionDirectory { get; }
        public string DataAFileName { get; }
        public string DataBFileName { get; }
        public string ManifestAFileName { get; }
        public string ManifestBFileName { get; }
        public string PendingFileName { get; }

        public string FileName(CheckpointStorageFile file)
        {
            switch (file)
            {
                case CheckpointStorageFile.DataA: return DataAFileName;
                case CheckpointStorageFile.DataB: return DataBFileName;
                case CheckpointStorageFile.ManifestA: return ManifestAFileName;
                case CheckpointStorageFile.ManifestB: return ManifestBFileName;
                case CheckpointStorageFile.Pending: return PendingFileName;
                default: throw new ArgumentOutOfRangeException(nameof(file));
            }
        }

        public string DirectoryFor(string persistentDataRoot)
        {
            if (string.IsNullOrWhiteSpace(persistentDataRoot))
            {
                throw new ArgumentException("A persistence root is required.", nameof(persistentDataRoot));
            }

            var root = Path.GetFullPath(persistentDataRoot);
            return Path.Combine(root, ApplicationDirectory, CheckpointDirectory, VersionDirectory);
        }

        private static string RequireName(string value, string parameterName)
        {
            if (string.IsNullOrWhiteSpace(value) || value.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
            {
                throw new ArgumentException("A plain file or directory name is required.", parameterName);
            }

            return value;
        }
    }

    /// <summary>One manifest entry: the data slot, identity and SHA-256 of a published generation.</summary>
    public sealed class CheckpointGenerationReference
    {
        internal static readonly CheckpointGenerationReference Empty = new CheckpointGenerationReference(
            CheckpointStorageFile.DataA, default(CheckpointId), 0, new byte[32], true);

        internal CheckpointGenerationReference(CheckpointStorageFile file, CheckpointId id, long generation, byte[] contentHash)
            : this(file, id, generation, contentHash, false)
        {
        }

        private CheckpointGenerationReference(
            CheckpointStorageFile file, CheckpointId id, long generation, byte[] contentHash, bool empty)
        {
            File = file;
            Id = id;
            Generation = generation;
            Hash = (byte[])contentHash.Clone();
            IsEmpty = empty;
        }

        public CheckpointStorageFile File { get; }
        public CheckpointId Id { get; }
        public long Generation { get; }
        public byte[] ContentHash => (byte[])Hash.Clone();
        internal byte[] Hash { get; }
        internal bool IsEmpty { get; }
    }

    /// <summary>The manifest chosen by the highest valid sequence, with its active and retained previous generation.</summary>
    public sealed class CheckpointGenerationSelection
    {
        internal CheckpointGenerationSelection(
            CheckpointStorageFile manifestFile,
            long sequence,
            CheckpointGenerationReference active,
            CheckpointGenerationReference previous)
        {
            ManifestFile = manifestFile;
            Sequence = sequence;
            Active = active;
            Previous = previous;
        }

        public CheckpointStorageFile ManifestFile { get; }
        public long Sequence { get; }
        public CheckpointGenerationReference Active { get; }
        public CheckpointGenerationReference Previous { get; }
    }

    public sealed class CheckpointGenerationRead<TCheckpoint> where TCheckpoint : class
    {
        private readonly byte[] bytes;

        private CheckpointGenerationRead(
            CommandOutcome outcome,
            string message,
            TCheckpoint checkpoint,
            byte[] bytes,
            CheckpointGenerationSelection selection)
        {
            Outcome = outcome;
            Message = message ?? string.Empty;
            Checkpoint = checkpoint;
            this.bytes = bytes;
            Selection = selection;
        }

        public bool Succeeded => Outcome == CommandOutcome.Success;
        public CommandOutcome Outcome { get; }
        public string Message { get; }
        public TCheckpoint Checkpoint { get; }

        /// <summary>The manifest selection the read used; null when no manifest could be selected.</summary>
        public CheckpointGenerationSelection Selection { get; }

        public byte[] CopyBytes() => bytes == null ? null : (byte[])bytes.Clone();

        internal static CheckpointGenerationRead<TCheckpoint> Success(
            TCheckpoint checkpoint, byte[] bytes, CheckpointGenerationSelection selection) =>
            new CheckpointGenerationRead<TCheckpoint>(CommandOutcome.Success, string.Empty, checkpoint, bytes, selection);

        internal static CheckpointGenerationRead<TCheckpoint> Failure(
            CommandOutcome outcome, string message, CheckpointGenerationSelection selection) =>
            new CheckpointGenerationRead<TCheckpoint>(outcome, message, null, null, selection);
    }

    /// <summary>
    /// The read half of the Feature 001 generation protocol: manifest selection by sequence and
    /// hash-verified data reads. It depends only on <see cref="ICheckpointStorageReader"/>, so no
    /// code path in it can create, write or flush a file.
    /// </summary>
    public sealed class CheckpointGenerationReader<TCheckpoint> where TCheckpoint : class
    {
        private const int MaximumManifestByteCount = 256;
        private const string NoActiveMessage = CheckpointGenerationFiles.NoActiveMessage;
        private const string NoPreviousMessage = CheckpointGenerationFiles.NoPreviousMessage;
        private const string NotFoundMessage = CheckpointGenerationFiles.NotFoundMessage;
        private const string InvalidMessage = CheckpointGenerationFiles.InvalidMessage;
        private const string PersistenceMessage = CheckpointGenerationFiles.PersistenceMessage;

        private readonly ICheckpointStorageReader storage;
        private readonly ICheckpointGenerationCodec<TCheckpoint> codec;

        public CheckpointGenerationReader(ICheckpointStorageReader storage, ICheckpointGenerationCodec<TCheckpoint> codec)
        {
            this.storage = storage ?? throw new ArgumentNullException(nameof(storage));
            this.codec = codec ?? throw new ArgumentNullException(nameof(codec));
            if (string.IsNullOrWhiteSpace(storage.StorageIdentity))
            {
                throw new ArgumentException("The checkpoint storage requires a storage identity.", nameof(storage));
            }

            OperationGate = CheckpointOperationGates.For(storage.StorageIdentity);
        }

        /// <summary>Shared by every reader and writer of the same storage identity in this process.</summary>
        internal object OperationGate { get; }

        public CheckpointGenerationRead<TCheckpoint> ReadActive()
        {
            lock (OperationGate)
            {
                var manifest = SelectManifest();
                if (!manifest.Succeeded)
                {
                    return CheckpointGenerationRead<TCheckpoint>.Failure(
                        manifest.Outcome, manifest.MessageOr(NoActiveMessage), null);
                }

                return ReadReference(manifest.Selection, manifest.Selection.Active);
            }
        }

        public CheckpointGenerationRead<TCheckpoint> ReadPrevious()
        {
            lock (OperationGate)
            {
                var manifest = SelectManifest();
                if (!manifest.Succeeded)
                {
                    return CheckpointGenerationRead<TCheckpoint>.Failure(
                        manifest.Outcome, manifest.MessageOr(NoPreviousMessage), null);
                }

                return manifest.Selection.Previous == null
                    ? CheckpointGenerationRead<TCheckpoint>.Failure(
                        CommandOutcome.NoCheckpoint, NoPreviousMessage, manifest.Selection)
                    : ReadReference(manifest.Selection, manifest.Selection.Previous);
            }
        }

        public CheckpointGenerationRead<TCheckpoint> Read(CheckpointId checkpointId)
        {
            _ = checkpointId.Value;
            lock (OperationGate)
            {
                var manifest = SelectManifest();
                if (!manifest.Succeeded)
                {
                    return CheckpointGenerationRead<TCheckpoint>.Failure(
                        manifest.Outcome, manifest.MessageOr(NotFoundMessage), null);
                }

                if (manifest.Selection.Active.Id == checkpointId)
                {
                    return ReadReference(manifest.Selection, manifest.Selection.Active);
                }

                var previous = manifest.Selection.Previous;
                return previous != null && previous.Id == checkpointId
                    ? ReadReference(manifest.Selection, previous)
                    : CheckpointGenerationRead<TCheckpoint>.Failure(
                        CommandOutcome.NoCheckpoint, NotFoundMessage, manifest.Selection);
            }
        }

        internal ManifestSelectionResult SelectManifest()
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
                    ? ManifestSelectionResult.Success(first.Record)
                    : ManifestSelectionResult.Success(second.Record);
            }

            if (first.Valid) return ManifestSelectionResult.Success(first.Record);
            if (second.Valid) return ManifestSelectionResult.Success(second.Record);
            if (!first.Exists && !second.Exists)
            {
                return ManifestSelectionResult.Failure(CommandOutcome.NoCheckpoint, NoActiveMessage);
            }

            return ManifestSelectionResult.Failure(CommandOutcome.InvalidCheckpoint, InvalidMessage);
        }

        internal ManifestFileRead ReadManifestFile(CheckpointStorageFile file)
        {
            try
            {
                if (!storage.Exists(file)) return ManifestFileRead.Absent();
                var length = storage.GetLength(file);
                if (length < 0 || length > MaximumManifestByteCount || length != CheckpointGenerationFiles.ManifestLength)
                {
                    return ManifestFileRead.Invalid();
                }

                var bytes = storage.ReadExactly(file, (int)length);
                if (bytes == null || bytes.Length != length || storage.GetLength(file) != length)
                {
                    return ManifestFileRead.PersistenceFailure();
                }

                return CheckpointManifestFormat.TryParse(file, bytes, out var record)
                    ? ManifestFileRead.Success(bytes, record)
                    : ManifestFileRead.Invalid();
            }
            catch (Exception exception) when (CheckpointGenerationFiles.IsPersistenceFailure(exception))
            {
                CheckpointDiagnostics.Report("ReadManifest", file.ToString(), exception);
                return ManifestFileRead.PersistenceFailure();
            }
        }

        internal DataRead ReadReferenceWithBytes(CheckpointGenerationReference reference)
        {
            var read = ReadDataFile(reference.File);
            if (!read.Succeeded) return read;
            if (!CheckpointGenerationFiles.BytesEqual(CheckpointGenerationFiles.Hash(read.Bytes), reference.Hash) ||
                codec.GetId(read.Checkpoint) != reference.Id || codec.GetGeneration(read.Checkpoint) != reference.Generation)
            {
                return DataRead.Failure(CommandOutcome.InvalidCheckpoint, InvalidMessage);
            }

            return read;
        }

        internal DataRead ReadDataFile(CheckpointStorageFile file)
        {
            try
            {
                if (!storage.Exists(file)) return DataRead.Failure(CommandOutcome.InvalidCheckpoint, InvalidMessage);
                var length = storage.GetLength(file);
                if (length < 0 || length > codec.MaximumByteCount)
                {
                    return DataRead.Failure(CommandOutcome.InvalidCheckpoint, InvalidMessage);
                }

                var bytes = storage.ReadExactly(file, (int)length);
                if (bytes == null || bytes.Length != length || storage.GetLength(file) != length)
                {
                    return DataRead.Failure(CommandOutcome.PersistenceFailed, PersistenceMessage);
                }

                var mapped = codec.Deserialize(bytes);
                return mapped.Succeeded
                    ? DataRead.Success(bytes, mapped.Checkpoint)
                    : DataRead.Failure(mapped.Outcome, mapped.Message);
            }
            catch (Exception exception) when (CheckpointGenerationFiles.IsPersistenceFailure(exception))
            {
                CheckpointDiagnostics.Report("ReadData", file.ToString(), exception);
                return DataRead.Failure(CommandOutcome.PersistenceFailed, PersistenceMessage);
            }
        }

        private CheckpointGenerationRead<TCheckpoint> ReadReference(
            CheckpointGenerationSelection selection, CheckpointGenerationReference reference)
        {
            var read = ReadReferenceWithBytes(reference);
            return read.Succeeded
                ? CheckpointGenerationRead<TCheckpoint>.Success(read.Checkpoint, read.Bytes, selection)
                : CheckpointGenerationRead<TCheckpoint>.Failure(read.Outcome, read.Message, selection);
        }

        internal sealed class ManifestSelectionResult
        {
            private ManifestSelectionResult(CommandOutcome outcome, string message, CheckpointGenerationSelection selection)
            {
                Outcome = outcome;
                Message = message;
                Selection = selection;
            }

            public bool Succeeded => Outcome == CommandOutcome.Success;
            public CommandOutcome Outcome { get; }
            public string Message { get; }
            public CheckpointGenerationSelection Selection { get; }
            public static ManifestSelectionResult Success(CheckpointGenerationSelection selection) =>
                new ManifestSelectionResult(CommandOutcome.Success, string.Empty, selection);
            public static ManifestSelectionResult Failure(CommandOutcome outcome, string message) =>
                new ManifestSelectionResult(outcome, message, null);
            public string MessageOr(string absentMessage) =>
                Outcome == CommandOutcome.NoCheckpoint ? absentMessage : Message;
        }

        internal sealed class ManifestFileRead
        {
            private ManifestFileRead(
                bool exists, bool valid, CommandOutcome outcome, byte[] bytes, CheckpointGenerationSelection record)
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
            public CheckpointGenerationSelection Record { get; }
            public static ManifestFileRead Absent() => new ManifestFileRead(false, false, CommandOutcome.NoCheckpoint, null, null);
            public static ManifestFileRead Invalid() => new ManifestFileRead(true, false, CommandOutcome.InvalidCheckpoint, null, null);
            public static ManifestFileRead PersistenceFailure() => new ManifestFileRead(true, false, CommandOutcome.PersistenceFailed, null, null);
            public static ManifestFileRead Success(byte[] bytes, CheckpointGenerationSelection record) =>
                new ManifestFileRead(true, true, CommandOutcome.Success, bytes, record);
        }

        internal sealed class DataRead
        {
            private DataRead(CommandOutcome outcome, string message, byte[] bytes, TCheckpoint checkpoint)
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
            public TCheckpoint Checkpoint { get; }
            public static DataRead Success(byte[] bytes, TCheckpoint checkpoint) =>
                new DataRead(CommandOutcome.Success, string.Empty, bytes, checkpoint);
            public static DataRead Failure(CommandOutcome outcome, string message) =>
                new DataRead(outcome, message, null, null);
            public CommandResult<CheckpointId> ToCommandResult() =>
                CommandResult<CheckpointId>.Rejected(Outcome, Message);
        }
    }

    /// <summary>
    /// The Feature 001 generation protocol, independent of the checkpoint format (R08): the candidate is
    /// written to the pending slot and read back through the codec, the retained generation is stabilized,
    /// the candidate is copied into the free data slot and read back, the next manifest is written into the
    /// free manifest slot and read back, and the published selection and active data are verified. Each
    /// backend call is a fault-injection seam (write, flush, read back, manifest, verify).
    /// </summary>
    public sealed class CheckpointGenerationFiles<TCheckpoint> where TCheckpoint : class
    {
        private readonly ICheckpointStorageBackend backend;
        private readonly ICheckpointGenerationCodec<TCheckpoint> codec;
        private readonly CheckpointGenerationReader<TCheckpoint> reader;

        public CheckpointGenerationFiles(ICheckpointStorageBackend backend, ICheckpointGenerationCodec<TCheckpoint> codec)
        {
            this.backend = backend ?? throw new ArgumentNullException(nameof(backend));
            this.codec = codec ?? throw new ArgumentNullException(nameof(codec));
            reader = new CheckpointGenerationReader<TCheckpoint>(backend, codec);
        }

        public CheckpointGenerationReader<TCheckpoint> Reader => reader;

        public CheckpointGenerationRead<TCheckpoint> ReadActive() => reader.ReadActive();
        public CheckpointGenerationRead<TCheckpoint> ReadPrevious() => reader.ReadPrevious();
        public CheckpointGenerationRead<TCheckpoint> Read(CheckpointId checkpointId) => reader.Read(checkpointId);

        public CommandResult<CheckpointId> Promote(TCheckpoint checkpoint)
        {
            if (checkpoint == null)
            {
                return InvalidCommand();
            }

            lock (reader.OperationGate)
            {
                return Publish(checkpoint, false);
            }
        }

        /// <summary>
        /// Publishes <paramref name="recoveryCandidate"/> as the successor of the retained previous generation,
        /// keeping that previous generation retained. <paramref name="isValidRecoveryCandidate"/> receives the
        /// candidate, the current active reference and the decoded previous checkpoint.
        /// </summary>
        public CommandResult<CheckpointId> RecoverPrevious(
            TCheckpoint recoveryCandidate,
            Func<TCheckpoint, CheckpointGenerationReference, TCheckpoint, bool> isValidRecoveryCandidate)
        {
            if (isValidRecoveryCandidate == null) throw new ArgumentNullException(nameof(isValidRecoveryCandidate));
            if (recoveryCandidate == null)
            {
                return InvalidCommand();
            }

            lock (reader.OperationGate)
            {
                var manifest = reader.SelectManifest();
                if (!manifest.Succeeded)
                {
                    return CommandResult<CheckpointId>.Rejected(manifest.Outcome, manifest.Message);
                }

                var previousReference = manifest.Selection.Previous;
                if (previousReference == null)
                {
                    return CommandResult<CheckpointId>.Rejected(
                        CommandOutcome.NoCheckpoint, CheckpointGenerationFiles.NoPreviousMessage);
                }

                var previous = reader.ReadReferenceWithBytes(previousReference);
                if (!previous.Succeeded) return previous.ToCommandResult();

                if (manifest.Selection.Active.Id == codec.GetId(recoveryCandidate) &&
                    manifest.Selection.Active.Generation == codec.GetGeneration(recoveryCandidate))
                {
                    var alreadyRecovered = reader.ReadReferenceWithBytes(manifest.Selection.Active);
                    var expected = codec.Serialize(recoveryCandidate);
                    if (!alreadyRecovered.Succeeded) return alreadyRecovered.ToCommandResult();
                    if (!expected.Succeeded || !BytesEqual(alreadyRecovered.Bytes, expected.Value)) return InvalidCommand("Recover.AlreadyRecoveredMismatch");
                    return ConfirmDurable(manifest.Selection, manifest.Selection.Active, expected.Value)
                        ?? CommandResult<CheckpointId>.Success(codec.GetId(recoveryCandidate));
                }

                if (!isValidRecoveryCandidate(recoveryCandidate, manifest.Selection.Active, previous.Checkpoint))
                {
                    return InvalidCommand("Recover.InvalidCandidate");
                }

                return Publish(recoveryCandidate, true);
            }
        }

        private CommandResult<CheckpointId> Publish(TCheckpoint checkpoint, bool recovery)
        {
            var checkpointId = codec.GetId(checkpoint);
            var checkpointGeneration = codec.GetGeneration(checkpoint);
            var serialized = codec.Serialize(checkpoint);
            if (!serialized.Succeeded)
            {
                CheckpointDiagnostics.Report("Publish", "Serialize");
                return CommandResult<CheckpointId>.Rejected(serialized.Outcome, serialized.Message);
            }

            var candidateHash = Hash(serialized.Value);
            var current = reader.SelectManifest();
            if (!current.Succeeded && current.Outcome != CommandOutcome.NoCheckpoint &&
                current.Outcome != CommandOutcome.InvalidCheckpoint)
            {
                return CommandResult<CheckpointId>.Rejected(current.Outcome, current.Message);
            }

            CheckpointGenerationReference retainedReference = null;
            CheckpointGenerationReader<TCheckpoint>.DataRead retainedData = null;
            if (current.Succeeded)
            {
                if (!recovery)
                {
                    var active = reader.ReadReferenceWithBytes(current.Selection.Active);
                    if (!active.Succeeded)
                    {
                        return active.ToCommandResult();
                    }

                    var activeId = codec.GetId(active.Checkpoint);
                    var activeGeneration = codec.GetGeneration(active.Checkpoint);
                    if (activeId == checkpointId && activeGeneration == checkpointGeneration)
                    {
                        if (!BytesEqual(active.Bytes, serialized.Value) ||
                            !BytesEqual(current.Selection.Active.Hash, candidateHash)) return InvalidCommand("Publish.SameGenerationMismatch");
                        return ConfirmDurable(current.Selection, current.Selection.Active, serialized.Value)
                            ?? CommandResult<CheckpointId>.Success(checkpointId);
                    }

                    var previousGeneration = current.Selection.Previous?.Generation ?? -1;
                    if (checkpointId == activeId ||
                        (current.Selection.Previous != null && checkpointId == current.Selection.Previous.Id) ||
                        checkpointGeneration <= activeGeneration ||
                        checkpointGeneration <= previousGeneration)
                    {
                        return InvalidCommand("Publish.GenerationNotAdvanced");
                    }
                }

                // A prior failed flush may leave a readable but volatile selector. Stabilize
                // the retained generation before overwriting either of the other fixed slots.
                retainedReference = recovery
                    ? current.Selection.Previous
                    : current.Selection.Active;
                retainedData = reader.ReadReferenceWithBytes(retainedReference);
                if (!retainedData.Succeeded) return retainedData.ToCommandResult();
            }

            // Keep the candidate separate from both published generations until its flushed
            // bytes have passed the same strict parser and semantic validation used on load.
            var pendingWrite = WriteAndClose(CheckpointStorageFile.Pending, serialized.Value);
            if (pendingWrite != null) return pendingWrite;
            var pendingReadback = reader.ReadDataFile(CheckpointStorageFile.Pending);
            if (!pendingReadback.Succeeded) return pendingReadback.ToCommandResult();
            if (!BytesEqual(pendingReadback.Bytes, serialized.Value) ||
                !BytesEqual(Hash(pendingReadback.Bytes), candidateHash) ||
                !CheckpointMatches(pendingReadback.Checkpoint, checkpointId, checkpointGeneration, serialized.Value))
            {
                return InvalidCommand("Publish.PendingReadbackMismatch");
            }

            if (current.Succeeded)
            {
                // Stabilize the retained generation only after pending bytes are valid, and
                // before overwriting a data slot that may hold the prior Previous generation.
                var durable = ConfirmDurable(current.Selection, retainedReference, retainedData.Bytes);
                if (durable != null) return durable;
            }

            var dataFile = current.Succeeded && recovery
                ? current.Selection.Active.File
                : !current.Succeeded || current.Selection.Active.File == CheckpointStorageFile.DataB
                    ? CheckpointStorageFile.DataA
                    : CheckpointStorageFile.DataB;
            var manifestFile = !current.Succeeded || current.Selection.ManifestFile == CheckpointStorageFile.ManifestB
                ? CheckpointStorageFile.ManifestA
                : CheckpointStorageFile.ManifestB;

            var writeData = WriteAndClose(dataFile, pendingReadback.Bytes);
            if (writeData != null) return writeData;
            var dataReadback = reader.ReadDataFile(dataFile);
            if (!dataReadback.Succeeded) return dataReadback.ToCommandResult();
            if (!BytesEqual(dataReadback.Bytes, pendingReadback.Bytes) ||
                !BytesEqual(Hash(dataReadback.Bytes), candidateHash) ||
                !CheckpointMatches(dataReadback.Checkpoint, checkpointId, checkpointGeneration, serialized.Value))
            {
                return InvalidCommand("Publish.DataReadbackMismatch");
            }

            long nextSequence;
            try
            {
                nextSequence = checked((current.Succeeded ? current.Selection.Sequence : 0L) + 1L);
            }
            catch (OverflowException)
            {
                return InvalidCommand("Publish.SequenceOverflow");
            }

            var nextRecord = new CheckpointGenerationSelection(
                manifestFile,
                nextSequence,
                new CheckpointGenerationReference(dataFile, checkpointId, checkpointGeneration, candidateHash),
                current.Succeeded
                    ? recovery ? current.Selection.Previous : current.Selection.Active
                    : null);
            var manifestBytes = CheckpointManifestFormat.Serialize(nextRecord);
            var writeManifest = WriteAndClose(manifestFile, manifestBytes);
            if (writeManifest != null) return writeManifest;
            var manifestReadback = reader.ReadManifestFile(manifestFile);
            if (!manifestReadback.Valid || !BytesEqual(manifestReadback.Bytes, manifestBytes) ||
                !CheckpointManifestFormat.RecordsEqual(manifestReadback.Record, nextRecord))
            {
                return manifestReadback.Outcome == CommandOutcome.PersistenceFailed
                    ? PersistenceCommand("Publish.ManifestReadback")
                    : InvalidCommand("Publish.ManifestReadbackMismatch");
            }

            var published = reader.SelectManifest();
            if (!published.Succeeded || published.Selection.Sequence != nextSequence ||
                published.Selection.ManifestFile != manifestFile)
            {
                return published.Outcome == CommandOutcome.PersistenceFailed
                    ? PersistenceCommand("Publish.ManifestSelection")
                    : InvalidCommand("Publish.ManifestSelectionMismatch");
            }

            var activeReadback = reader.ReadReferenceWithBytes(published.Selection.Active);
            if (!activeReadback.Succeeded) return activeReadback.ToCommandResult();
            return BytesEqual(activeReadback.Bytes, serialized.Value) &&
                   CheckpointMatches(activeReadback.Checkpoint, checkpointId, checkpointGeneration, serialized.Value)
                ? CommandResult<CheckpointId>.Success(checkpointId)
                : InvalidCommand("Publish.ActiveReadbackMismatch");
        }

        private CommandResult<CheckpointId> ConfirmDurable(
            CheckpointGenerationSelection selection, CheckpointGenerationReference reference, byte[] expectedBytes)
        {
            try
            {
                backend.FlushExisting(reference.File);
                backend.FlushExisting(selection.ManifestFile);
            }
            catch (Exception exception) when (CheckpointGenerationFiles.IsPersistenceFailure(exception))
            {
                CheckpointDiagnostics.Report("ConfirmDurable", "Flush", exception);
                return PersistenceCommand();
            }

            var data = reader.ReadReferenceWithBytes(reference);
            if (!data.Succeeded) return data.ToCommandResult();
            var manifest = reader.SelectManifest();
            if (!manifest.Succeeded)
                return CommandResult<CheckpointId>.Rejected(manifest.Outcome, manifest.Message);
            return BytesEqual(data.Bytes, expectedBytes) && manifest.Selection.ManifestFile == selection.ManifestFile &&
                   CheckpointManifestFormat.RecordsEqual(manifest.Selection, selection)
                ? null
                : InvalidCommand("ConfirmDurable.Mismatch");
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
            catch (Exception exception) when (CheckpointGenerationFiles.IsPersistenceFailure(exception))
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
                    catch (Exception exception) when (CheckpointGenerationFiles.IsPersistenceFailure(exception))
                    {
                        if (failure == null) failure = exception;
                    }
                }
            }

            if (failure == null) return null;
            CheckpointDiagnostics.Report("Write", file.ToString(), failure);
            return PersistenceCommand();
        }

        private bool CheckpointMatches(TCheckpoint actual, CheckpointId expectedId, long expectedGeneration, byte[] expectedBytes)
        {
            return codec.ValidateBytes(actual, expectedBytes) &&
                   codec.GetId(actual) == expectedId && codec.GetGeneration(actual) == expectedGeneration;
        }

        private static byte[] Hash(byte[] bytes) => CheckpointGenerationFiles.Hash(bytes);

        private static bool BytesEqual(byte[] left, byte[] right) => CheckpointGenerationFiles.BytesEqual(left, right);

        private static CommandResult<CheckpointId> InvalidCommand() =>
            CommandResult<CheckpointId>.Rejected(
                CommandOutcome.InvalidCheckpoint, CheckpointGenerationFiles.InvalidMessage);

        private static CommandResult<CheckpointId> PersistenceCommand() =>
            CommandResult<CheckpointId>.Rejected(
                CommandOutcome.PersistenceFailed, CheckpointGenerationFiles.PersistenceMessage);

        private static CommandResult<CheckpointId> InvalidCommand(string stage)
        {
            CheckpointDiagnostics.Report("Store", stage);
            return InvalidCommand();
        }

        private static CommandResult<CheckpointId> PersistenceCommand(string stage)
        {
            CheckpointDiagnostics.Report("Store", stage);
            return PersistenceCommand();
        }
    }

    /// <summary>Format-independent helpers shared by every generation reader and writer.</summary>
    public static class CheckpointGenerationFiles
    {
        internal const int ManifestLength = 163;
        internal const string NoActiveMessage = "No active checkpoint is available.";
        internal const string NoPreviousMessage = "No previous checkpoint is available.";
        internal const string NotFoundMessage = "The requested checkpoint is not retained.";
        internal const string InvalidMessage = "Checkpoint data is invalid.";
        internal const string PersistenceMessage = "Checkpoint storage operation failed.";
        internal static readonly byte[] ManifestMagic = Encoding.ASCII.GetBytes("IFCPMF01");

        public static bool IsPersistenceFailure(Exception exception)
        {
            return exception is IOException || exception is UnauthorizedAccessException ||
                   exception is SecurityException || exception is InvalidOperationException ||
                   exception is NotSupportedException || exception is PlatformNotSupportedException;
        }

        internal static byte[] Hash(byte[] bytes)
        {
            using (var sha = SHA256.Create()) return sha.ComputeHash(bytes);
        }

        internal static bool BytesEqual(byte[] left, byte[] right)
        {
            if (left == null || right == null || left.Length != right.Length) return false;
            var difference = 0;
            for (var index = 0; index < left.Length; index++) difference |= left[index] ^ right[index];
            return difference == 0;
        }
    }

    /// <summary>The 163-byte manifest: magic, sequence, active and optional previous reference, SHA-256 trailer.</summary>
    internal static class CheckpointManifestFormat
    {
        public static byte[] Serialize(CheckpointGenerationSelection record)
        {
            byte[] payload;
            using (var stream = new MemoryStream())
            using (var writer = new BinaryWriter(stream, Encoding.UTF8, true))
            {
                writer.Write(ManifestMagic);
                writer.Write(record.Sequence);
                WriteReference(writer, record.Active);
                writer.Write(record.Previous != null);
                WriteReference(writer, record.Previous ?? CheckpointGenerationReference.Empty);
                writer.Flush();
                payload = stream.ToArray();
            }

            var result = new byte[payload.Length + 32];
            Buffer.BlockCopy(payload, 0, result, 0, payload.Length);
            Buffer.BlockCopy(CheckpointGenerationFiles.Hash(payload), 0, result, payload.Length, 32);
            return result;
        }

        public static bool TryParse(CheckpointStorageFile file, byte[] bytes, out CheckpointGenerationSelection record)
        {
            record = null;
            if (bytes == null || bytes.Length != ManifestLength) return false;
            var payloadLength = bytes.Length - 32;
            var payload = new byte[payloadLength];
            var checksum = new byte[32];
            Buffer.BlockCopy(bytes, 0, payload, 0, payloadLength);
            Buffer.BlockCopy(bytes, payloadLength, checksum, 0, checksum.Length);
            if (!CheckpointGenerationFiles.BytesEqual(CheckpointGenerationFiles.Hash(payload), checksum)) return false;

            try
            {
                using (var stream = new MemoryStream(payload, false))
                using (var reader = new BinaryReader(stream, Encoding.UTF8, false))
                {
                    if (!CheckpointGenerationFiles.BytesEqual(reader.ReadBytes(ManifestMagic.Length), ManifestMagic)) return false;
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

                    record = new CheckpointGenerationSelection(file, sequence, active, previous);
                    return true;
                }
            }
            catch (Exception exception) when (exception is IOException || exception is ArgumentException ||
                                               exception is EndOfStreamException || exception is FormatException)
            {
                return false;
            }
        }

        public static bool RecordsEqual(CheckpointGenerationSelection left, CheckpointGenerationSelection right)
        {
            return left != null && right != null && left.Sequence == right.Sequence &&
                   ReferencesEqual(left.Active, right.Active) && ReferencesEqual(left.Previous, right.Previous);
        }

        private const int ManifestLength = CheckpointGenerationFiles.ManifestLength;

        private static byte[] ManifestMagic => CheckpointGenerationFiles.ManifestMagic;

        private static bool ReferencesEqual(CheckpointGenerationReference left, CheckpointGenerationReference right)
        {
            if (left == null || right == null) return left == right;
            return left.File == right.File && left.Id == right.Id && left.Generation == right.Generation &&
                   CheckpointGenerationFiles.BytesEqual(left.Hash, right.Hash);
        }

        private static void WriteReference(BinaryWriter writer, CheckpointGenerationReference reference)
        {
            writer.Write(reference.File == CheckpointStorageFile.DataB ? (byte)1 : (byte)0);
            writer.Write(reference.IsEmpty ? Guid.Empty.ToByteArray() : new Guid(reference.Id.Value).ToByteArray());
            writer.Write(reference.Generation);
            writer.Write(reference.Hash);
        }

        private static CheckpointGenerationReference ReadReference(BinaryReader reader)
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
                    ? CheckpointGenerationReference.Empty
                    : null;
            }

            return new CheckpointGenerationReference(
                slot == 0 ? CheckpointStorageFile.DataA : CheckpointStorageFile.DataB,
                new CheckpointId(id),
                generation,
                hash);
        }
    }

    internal static class CheckpointOperationGates
    {
        private static readonly object LockMapGate = new object();
        private static readonly Dictionary<string, object> LockMap =
            new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);

        public static object For(string storageIdentity)
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

    public sealed class SystemIoCheckpointStorageBackend : ICheckpointStorageBackend
    {
        // Redundant fixed slots avoid rename/replace atomicity assumptions. Portable Unity System.IO
        // cannot fsync directory entries, so first-file power loss and device interruption remain T038 gates.
        private readonly string directory;
        private readonly CheckpointFileLayout layout;
        private FileStream openStream;
        private CheckpointStorageFile? openFile;

        public SystemIoCheckpointStorageBackend(string directory)
            : this(directory, SaveV1CheckpointFiles.Layout)
        {
        }

        public SystemIoCheckpointStorageBackend(string directory, CheckpointFileLayout layout)
        {
            if (string.IsNullOrWhiteSpace(directory))
            {
                throw new ArgumentException("A checkpoint directory is required.", nameof(directory));
            }

            this.layout = layout ?? throw new ArgumentNullException(nameof(layout));
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

        public byte[] ReadExactly(CheckpointStorageFile file, int expectedLength) =>
            SystemIoCheckpointStorageReader.ReadExactly(PathFor(file), expectedLength);

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

        private string PathFor(CheckpointStorageFile file) => Path.Combine(directory, layout.FileName(file));
    }

    /// <summary>
    /// Read-only System.IO slots. Unlike <see cref="SystemIoCheckpointStorageBackend"/> it never creates the
    /// directory, and every file is opened with <see cref="FileAccess.Read"/> only.
    /// </summary>
    public sealed class SystemIoCheckpointStorageReader : ICheckpointStorageReader
    {
        private readonly string directory;
        private readonly CheckpointFileLayout layout;
        private readonly Func<string, FileAttributes> getAttributes;

        public SystemIoCheckpointStorageReader(string directory, CheckpointFileLayout layout)
            : this(directory, layout, File.GetAttributes)
        {
        }

        public SystemIoCheckpointStorageReader(
            string directory, CheckpointFileLayout layout, Func<string, FileAttributes> getAttributes)
        {
            if (string.IsNullOrWhiteSpace(directory))
            {
                throw new ArgumentException("A checkpoint directory is required.", nameof(directory));
            }

            this.layout = layout ?? throw new ArgumentNullException(nameof(layout));
            this.getAttributes = getAttributes ?? throw new ArgumentNullException(nameof(getAttributes));
            this.directory = Path.GetFullPath(directory);
        }

        public string StorageIdentity => directory;

        public bool Exists(CheckpointStorageFile file)
        {
            try
            {
                // A missing file or directory is absent; access and query failures must reach the
                // protocol's typed persistence-failure boundary instead of becoming "absent".
                getAttributes(PathFor(file));
                return true;
            }
            catch (FileNotFoundException)
            {
                return false;
            }
            catch (DirectoryNotFoundException)
            {
                return false;
            }
        }

        public long GetLength(CheckpointStorageFile file) => new FileInfo(PathFor(file)).Length;

        public byte[] ReadExactly(CheckpointStorageFile file, int expectedLength) =>
            ReadExactly(PathFor(file), expectedLength);

        internal static byte[] ReadExactly(string path, int expectedLength)
        {
            var bytes = new byte[expectedLength];
            using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
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

        private string PathFor(CheckpointStorageFile file) => Path.Combine(directory, layout.FileName(file));
    }
}
