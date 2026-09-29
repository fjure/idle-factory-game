using System;
using IdleFactory.Core.Application;
using IdleFactory.Domain;

namespace IdleFactory.Unity.Persistence
{
    /// <summary>
    /// The Feature 001 Save V1 store, kept with its write API as a thin delegate over
    /// <see cref="CheckpointGenerationFiles{TCheckpoint}"/> for the legacy Save V1 suites (GD-1). The
    /// Feature 003 runtime reads V1 only through the read-only <see cref="SaveV1CheckpointSource"/>.
    /// </summary>
    public sealed class JsonCheckpointStore : ICheckpointStore
    {
        public const string DataAFileName = SaveV1CheckpointFiles.DataAFileName;
        public const string DataBFileName = SaveV1CheckpointFiles.DataBFileName;
        public const string ManifestAFileName = SaveV1CheckpointFiles.ManifestAFileName;
        public const string ManifestBFileName = SaveV1CheckpointFiles.ManifestBFileName;
        public const string PendingFileName = SaveV1CheckpointFiles.PendingFileName;

        private readonly CheckpointGenerationFiles<Checkpoint> files;

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

            if (catalog == null) throw new ArgumentNullException(nameof(catalog));
            if (backend == null) throw new ArgumentNullException(nameof(backend));
            if (string.IsNullOrWhiteSpace(backend.StorageIdentity))
            {
                throw new ArgumentException("The checkpoint backend requires a storage identity.", nameof(backend));
            }

            files = new CheckpointGenerationFiles<Checkpoint>(backend, new SaveV1CheckpointCodec(catalog));
        }

        public CheckpointReadResult ReadActive() => ToReadResult(files.ReadActive());

        public CheckpointReadResult ReadPrevious() => ToReadResult(files.ReadPrevious());

        public CheckpointReadResult Read(CheckpointId checkpointId) => ToReadResult(files.Read(checkpointId));

        public CommandResult<CheckpointId> Promote(Checkpoint checkpoint) => files.Promote(checkpoint);

        public CommandResult<CheckpointId> RecoverPrevious(Checkpoint recoveryCandidate) =>
            files.RecoverPrevious(recoveryCandidate, IsValidRecoveryCandidate);

        private static CheckpointReadResult ToReadResult(CheckpointGenerationRead<Checkpoint> read) =>
            read.Succeeded
                ? CheckpointReadResult.Success(read.Checkpoint)
                : CheckpointReadResult.Rejected(read.Outcome, read.Message);

        private static bool IsValidRecoveryCandidate(
            Checkpoint candidate,
            CheckpointGenerationReference active,
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
                   CheckpointGenerationFiles.BytesEqual(normalizedBytes.Value, previousBytes.Value);
        }

        private static ICheckpointStorageBackend CreateBackend(string persistentDataRoot)
        {
            if (string.IsNullOrWhiteSpace(persistentDataRoot))
            {
                throw new ArgumentException("A persistence root is required.", nameof(persistentDataRoot));
            }

            return new SystemIoCheckpointStorageBackend(
                SaveV1CheckpointFiles.Layout.DirectoryFor(persistentDataRoot),
                SaveV1CheckpointFiles.Layout);
        }
    }
}
