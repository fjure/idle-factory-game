using System;
using IdleFactory.Core.Application;
using IdleFactory.Domain;
using IdleFactory.Run.Application;

namespace IdleFactory.Unity.Persistence
{
    /// <summary>The unchanged Feature 001 Save V1 slot names under <c>IdleFactory/Checkpoints/v1/</c> (SC-005).</summary>
    public static class SaveV1CheckpointFiles
    {
        public const string VersionDirectory = "v1";
        public const string DataAFileName = "factory-checkpoint.data-a.json";
        public const string DataBFileName = "factory-checkpoint.data-b.json";
        public const string ManifestAFileName = "factory-checkpoint.manifest-a.bin";
        public const string ManifestBFileName = "factory-checkpoint.manifest-b.bin";
        public const string PendingFileName = "factory-checkpoint.pending.json";

        public static readonly CheckpointFileLayout Layout = new CheckpointFileLayout(
            VersionDirectory,
            DataAFileName,
            DataBFileName,
            ManifestAFileName,
            ManifestBFileName,
            PendingFileName);
    }

    /// <summary>The Save V1 codec: the unchanged <see cref="SaveV1Mapper"/> against one definition catalog.</summary>
    public sealed class SaveV1CheckpointCodec : ICheckpointGenerationCodec<Checkpoint>
    {
        private readonly DefinitionCatalog catalog;

        public SaveV1CheckpointCodec(DefinitionCatalog catalog)
        {
            this.catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
        }

        public int MaximumByteCount => SaveV1Mapper.MaximumByteCount;

        public CheckpointId GetId(Checkpoint checkpoint) => checkpoint.Id;

        public long GetGeneration(Checkpoint checkpoint) => checkpoint.Generation;

        public CommandResult<byte[]> Serialize(Checkpoint checkpoint) => SaveV1Mapper.Serialize(checkpoint);

        public CheckpointDecodeResult<Checkpoint> Deserialize(byte[] bytes)
        {
            var mapped = SaveV1Mapper.Deserialize(bytes, catalog);
            return mapped.Succeeded
                ? CheckpointDecodeResult<Checkpoint>.Success(mapped.Checkpoint)
                : CheckpointDecodeResult<Checkpoint>.Rejected(mapped.Outcome, mapped.Message);
        }

        public bool ValidateBytes(Checkpoint checkpoint, byte[] bytes)
        {
            var actualBytes = SaveV1Mapper.Serialize(checkpoint);
            return actualBytes.Succeeded && CheckpointGenerationFiles.BytesEqual(actualBytes.Value, bytes);
        }
    }

    /// <summary>
    /// The only Save V1 access the Feature 003 runtime wires (GD-1, FR-028). It holds nothing wider than an
    /// <see cref="ICheckpointStorageReader"/>, so no member can create, write, flush or close a V1 file, and
    /// the System.IO reader it builds by default does not even create the <c>v1/</c> directory.
    /// </summary>
    public sealed class SaveV1CheckpointSource : ILegacyCheckpointSource
    {
        public const string SourceDescription =
            "Save V1 checkpoint source (read-only, schemaVersion 1 slots)";

        private readonly CheckpointGenerationReader<Checkpoint> reader;

        public SaveV1CheckpointSource(string persistentDataRoot, DefinitionCatalog legacyCatalog)
            : this(
                new SystemIoCheckpointStorageReader(
                    SaveV1CheckpointFiles.Layout.DirectoryFor(persistentDataRoot),
                    SaveV1CheckpointFiles.Layout),
                legacyCatalog)
        {
        }

        public SaveV1CheckpointSource(ICheckpointStorageReader storage, DefinitionCatalog legacyCatalog)
        {
            if (storage == null) throw new ArgumentNullException(nameof(storage));
            if (legacyCatalog == null) throw new ArgumentNullException(nameof(legacyCatalog));
            reader = new CheckpointGenerationReader<Checkpoint>(storage, new SaveV1CheckpointCodec(legacyCatalog));
        }

        public string Description => SourceDescription;

        public bool TryReadActive(out Checkpoint checkpoint, out CommandOutcome outcome)
        {
            var read = ReadActive();
            checkpoint = read.Checkpoint;
            outcome = read.Outcome;
            return read.Succeeded;
        }

        /// <summary>The active read with its manifest selection, for diagnostics and characterization tests.</summary>
        public CheckpointGenerationRead<Checkpoint> ReadActive() => reader.ReadActive();
    }
}
