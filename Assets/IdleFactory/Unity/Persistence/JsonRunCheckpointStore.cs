using System;
using IdleFactory.Core.Application;
using IdleFactory.Domain;
using IdleFactory.Run.Application;
using IdleFactory.Run.Content;

namespace IdleFactory.Unity.Persistence
{
    /// <summary>The Save V2 slot names under <c>IdleFactory/Checkpoints/v2/</c> (contracts/save-v2-and-v1-import.md, Storage layout).</summary>
    public static class SaveV2CheckpointFiles
    {
        public const string VersionDirectory = "v2";
        public const string DataAFileName = "run-checkpoint.data-a.json";
        public const string DataBFileName = "run-checkpoint.data-b.json";
        public const string ManifestAFileName = "run-checkpoint.manifest-a.bin";
        public const string ManifestBFileName = "run-checkpoint.manifest-b.bin";
        public const string PendingFileName = "run-checkpoint.pending.json";

        public static readonly CheckpointFileLayout Layout = new CheckpointFileLayout(
            VersionDirectory,
            DataAFileName,
            DataBFileName,
            ManifestAFileName,
            ManifestBFileName,
            PendingFileName);
    }

    /// <summary>
    /// The Save V2 codec for the generation protocol (R08): a <see cref="RunCheckpoint"/> goes through
    /// <see cref="SaveV2Mapper"/> against one <see cref="RunCatalog"/>. Every rejection is a static
    /// <see cref="SaveV2Codes"/> code with its fixed copy, never text from the file (SEC-5).
    /// </summary>
    public sealed class SaveV2CheckpointCodec : ICheckpointGenerationCodec<RunCheckpoint>
    {
        private readonly RunCatalog catalog;

        public SaveV2CheckpointCodec(RunCatalog catalog)
        {
            this.catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
        }

        public int MaximumByteCount => SaveV2Mapper.MaximumByteCount;

        public CheckpointId GetId(RunCheckpoint checkpoint) => checkpoint.Id;

        public long GetGeneration(RunCheckpoint checkpoint) => checkpoint.Generation;

        public CommandResult<byte[]> Serialize(RunCheckpoint checkpoint)
        {
            var written = SaveV2Mapper.Serialize(ToMeta(checkpoint), checkpoint.Run, checkpoint.Permanent);
            return written.IsSuccess
                ? CommandResult<byte[]>.Success(written.Bytes)
                : CommandResult<byte[]>.Rejected(OutcomeOf(written.Code), Describe(written.Code));
        }

        public CheckpointDecodeResult<RunCheckpoint> Deserialize(byte[] bytes)
        {
            var read = SaveV2Mapper.Deserialize(bytes, catalog);
            if (!read.IsSuccess)
            {
                return CheckpointDecodeResult<RunCheckpoint>.Rejected(OutcomeOf(read.Code), Describe(read.Code));
            }

            try
            {
                return CheckpointDecodeResult<RunCheckpoint>.Success(ToCheckpoint(read.Document));
            }
            catch (ArgumentException)
            {
                return CheckpointDecodeResult<RunCheckpoint>.Rejected(
                    CommandOutcome.InvalidCheckpoint, Describe(SaveV2Codes.Invariant));
            }
        }

        public bool ValidateBytes(RunCheckpoint checkpoint, byte[] bytes)
        {
            var actual = SaveV2Mapper.Serialize(ToMeta(checkpoint), checkpoint.Run, checkpoint.Permanent);
            return actual.IsSuccess && CheckpointGenerationFiles.BytesEqual(actual.Bytes, bytes);
        }

        internal static SaveV2Meta ToMeta(RunCheckpoint checkpoint)
        {
            return new SaveV2Meta(
                checkpoint.Id.Uuid,
                checkpoint.Generation,
                checkpoint.DepartureUtcMs,
                checkpoint.OfflinePolicyRevision,
                checkpoint.ReconciledFromCheckpointId?.Uuid,
                checkpoint.ContentRevision,
                checkpoint.Import == null
                    ? null
                    : new SaveV2Import(
                        checkpoint.Import.SourceSchemaVersion, checkpoint.Import.SourceCheckpointId, checkpoint.Import.SourceGeneration));
        }

        private static RunCheckpoint ToCheckpoint(SaveV2Document document)
        {
            var meta = document.Meta;
            return new RunCheckpoint(
                new CheckpointId(meta.CheckpointId),
                meta.Generation,
                meta.DepartureUtcMs,
                meta.OfflinePolicyRevision,
                meta.ReconciledFromCheckpointId.HasValue ? new CheckpointId(meta.ReconciledFromCheckpointId.Value) : (CheckpointId?)null,
                meta.ContentRevision,
                meta.Import == null
                    ? null
                    : new RunCheckpointImport(meta.Import.SourceSchemaVersion, meta.Import.SourceCheckpointId, meta.Import.SourceGeneration),
                document.Run,
                document.Permanent);
        }

        private static CommandOutcome OutcomeOf(string code) =>
            code == SaveV2Codes.Version ? CommandOutcome.UnsupportedSaveVersion
            : code == SaveV2Codes.Unresolved ? CommandOutcome.UnresolvedContent
            : CommandOutcome.InvalidCheckpoint;

        private static string Describe(string code) => code + ": " + SaveV2Codes.Message(code);
    }

    /// <summary>
    /// The Save V2 checkpoint store (T138): the Feature 001 generation protocol over
    /// <c>IdleFactory/Checkpoints/v2/</c>, implemented once in <see cref="CheckpointGenerationFiles{TCheckpoint}"/> (R08)
    /// and parameterized by <see cref="SaveV2CheckpointCodec"/>. It never touches <c>v1/</c>. The backend is a seam, so a
    /// test can fail any protocol step (write, flush, read back, manifest, verify).
    /// </summary>
    public sealed class JsonRunCheckpointStore : IRunCheckpointStore
    {
        public const string DataAFileName = SaveV2CheckpointFiles.DataAFileName;
        public const string DataBFileName = SaveV2CheckpointFiles.DataBFileName;
        public const string ManifestAFileName = SaveV2CheckpointFiles.ManifestAFileName;
        public const string ManifestBFileName = SaveV2CheckpointFiles.ManifestBFileName;
        public const string PendingFileName = SaveV2CheckpointFiles.PendingFileName;

        private readonly ICheckpointStorageBackend backend;
        private readonly CheckpointGenerationFiles<RunCheckpoint> files;

        public JsonRunCheckpointStore(string persistentDataRoot, RunCatalog catalog)
            : this(persistentDataRoot, catalog, CreateBackend(persistentDataRoot))
        {
        }

        public JsonRunCheckpointStore(string persistentDataRoot, RunCatalog catalog, ICheckpointStorageBackend backend)
        {
            if (string.IsNullOrWhiteSpace(persistentDataRoot))
            {
                throw new ArgumentException("A persistence root is required.", nameof(persistentDataRoot));
            }

            if (catalog == null) throw new ArgumentNullException(nameof(catalog));
            this.backend = backend ?? throw new ArgumentNullException(nameof(backend));
            if (string.IsNullOrWhiteSpace(backend.StorageIdentity))
            {
                throw new ArgumentException("The checkpoint backend requires a storage identity.", nameof(backend));
            }

            files = new CheckpointGenerationFiles<RunCheckpoint>(backend, new SaveV2CheckpointCodec(catalog));
        }

        public RunCheckpointReadResult ReadActive() => ToReadResult(files.ReadActive());

        public RunCheckpointReadResult ReadPrevious() => ToReadResult(files.ReadPrevious());

        public RunCheckpointReadResult Read(CheckpointId checkpointId) => ToReadResult(files.Read(checkpointId));

        public CommandResult<CheckpointId> Promote(RunCheckpoint checkpoint) => files.Promote(checkpoint);

        public CommandResult<CheckpointId> RecoverPrevious(RunCheckpoint recoveryCandidate) =>
            files.RecoverPrevious(recoveryCandidate, IsValidRecoveryCandidate);

        public CommandResult<CheckpointId> SupersedeDamaged(RunCheckpoint checkpoint) => files.SupersedeDamaged(checkpoint);

        public CommandResult<long> NextSupersedeGeneration() => files.NextSupersedeGeneration();

        public CommandResult<RunCheckpointPresence> ProbeFiles()
        {
            try
            {
                lock (files.Reader.OperationGate)
                {
                    var published = backend.Exists(CheckpointStorageFile.DataA) || backend.Exists(CheckpointStorageFile.DataB) ||
                                    backend.Exists(CheckpointStorageFile.ManifestA) || backend.Exists(CheckpointStorageFile.ManifestB);
                    var presence = published
                        ? RunCheckpointPresence.PublishedFiles
                        : backend.Exists(CheckpointStorageFile.Pending)
                            ? RunCheckpointPresence.PendingOnly
                            : RunCheckpointPresence.NoFiles;
                    return CommandResult<RunCheckpointPresence>.Success(presence);
                }
            }
            catch (Exception exception) when (CheckpointGenerationFiles.IsPersistenceFailure(exception))
            {
                CheckpointDiagnostics.Report("ProbeFiles", "Exists", exception);
                return CommandResult<RunCheckpointPresence>.Rejected(
                    CommandOutcome.PersistenceFailed, CheckpointGenerationFiles.PersistenceMessage);
            }
        }

        private static RunCheckpointReadResult ToReadResult(CheckpointGenerationRead<RunCheckpoint> read) =>
            read.Succeeded
                ? RunCheckpointReadResult.Success(read.Checkpoint)
                : RunCheckpointReadResult.Rejected(read.Outcome, read.Message);

        // The recovery candidate must be the previous generation's state under a new identity that is newer than both
        // retained generations, linked to the previous checkpoint (Feature 001 rules).
        private static bool IsValidRecoveryCandidate(
            RunCheckpoint candidate,
            CheckpointGenerationReference active,
            RunCheckpoint previous)
        {
            if (candidate.Id == active.Id || candidate.Id == previous.Id ||
                candidate.Generation <= active.Generation || candidate.Generation <= previous.Generation ||
                candidate.OfflinePolicyRevision != previous.OfflinePolicyRevision ||
                !candidate.ReconciledFromCheckpointId.HasValue ||
                candidate.ReconciledFromCheckpointId.Value != previous.Id)
            {
                return false;
            }

            var meta = SaveV2CheckpointCodec.ToMeta(previous);
            var normalized = SaveV2Mapper.Serialize(meta, candidate.Run, candidate.Permanent);
            var original = SaveV2Mapper.Serialize(meta, previous.Run, previous.Permanent);
            return normalized.IsSuccess && original.IsSuccess &&
                   CheckpointGenerationFiles.BytesEqual(normalized.Bytes, original.Bytes);
        }

        private static ICheckpointStorageBackend CreateBackend(string persistentDataRoot)
        {
            if (string.IsNullOrWhiteSpace(persistentDataRoot))
            {
                throw new ArgumentException("A persistence root is required.", nameof(persistentDataRoot));
            }

            return new SystemIoCheckpointStorageBackend(
                SaveV2CheckpointFiles.Layout.DirectoryFor(persistentDataRoot),
                SaveV2CheckpointFiles.Layout);
        }
    }
}
