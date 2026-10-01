using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using IdleFactory.Core.Application;
using IdleFactory.Domain;
using IdleFactory.Run.Application;
using IdleFactory.Tests.EditMode.Fixtures;
using IdleFactory.Unity.Persistence;

namespace IdleFactory.Tests.EditMode
{
    /// <summary>
    /// The deterministic in-memory sources of the four V1 import fixtures (T106, QA-9): <c>07-over-capacity</c>,
    /// <c>08-over-slot</c>, <c>09-unsupported-id</c> and <c>10-corrupt</c>. Each is built from the same accepted
    /// <c>dev.*</c> definitions as the committed fixtures 01-05, except where a scenario needs a different catalog; the
    /// README names the catalog of every fixture. Used by <c>GenerateSaveV1FixturesTests</c> (byte compare against the
    /// committed files) and by <c>V1ImportTests</c>.
    /// </summary>
    internal static class V1ImportFixtures
    {
        public static readonly DateTimeOffset DepartureUtc = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

        public const string OverCapacityFileName = "07-over-capacity.json";
        public const string OverSlotFileName = "08-over-slot.json";
        public const string UnsupportedIdFileName = "09-unsupported-id.json";
        public const string CorruptFileName = "10-corrupt.json";

        /// <summary>The id of a building that the import does not map.</summary>
        public const string UnsupportedBuildingId = "dev.machine.unsupported";

        /// <summary>Input buffer capacity of the over-capacity catalog; two machines at 1500 give 3000 stock units.</summary>
        public const long OverCapacityInputCapacity = 2000;

        public const long OverCapacityInputPerMachine = 1500;

        public static BuildingInstanceId InstanceId(int n) =>
            new BuildingInstanceId(new Guid("22222222-2222-2222-2222-" + n.ToString("D12")));

        /// <summary>The accepted <c>dev.*</c> catalog with one building definition of the given ID and buffer capacities.</summary>
        public static DefinitionCatalog Catalog(string buildingId, long inputCapacity, long outputCapacity)
        {
            var input = AcceptedFoundationCatalogFixture.Input;
            var output = AcceptedFoundationCatalogFixture.Output;
            var recipe = new RecipeDefinition(
                AcceptedFoundationCatalogFixture.Recipe,
                new[] { new ResourceAmountDefinition(input, AcceptedFoundationCatalogFixture.RecipeInputAmount) },
                new[] { new ResourceAmountDefinition(output, AcceptedFoundationCatalogFixture.RecipeOutputAmount) },
                AcceptedFoundationCatalogFixture.RecipeDurationTicks,
                true);
            var building = new BuildingDefinition(
                new BuildingDefinitionId(buildingId),
                new Footprint(2, 2),
                new[] { 0 },
                AcceptedFoundationCatalogFixture.Recipe,
                new BufferDefinition(BufferRole.Input, new[] { new KeyValuePair<ResourceId, long>(input, inputCapacity) }),
                new BufferDefinition(BufferRole.Output, new[] { new KeyValuePair<ResourceId, long>(output, outputCapacity) }),
                AcceptedFoundationCatalogFixture.BuildingPresentationKey,
                true);
            return new DefinitionCatalog(
                new[]
                {
                    new ResourceDefinition(input, AcceptedFoundationCatalogFixture.InputDevelopmentLabel,
                        AcceptedFoundationCatalogFixture.InputPresentationKey, true),
                    new ResourceDefinition(output, AcceptedFoundationCatalogFixture.OutputDevelopmentLabel,
                        AcceptedFoundationCatalogFixture.OutputPresentationKey, true),
                },
                new[] { recipe },
                new[] { building },
                new[]
                {
                    new OfflinePolicyDefinition(
                        AcceptedFoundationCatalogFixture.OfflineRevision,
                        AcceptedFoundationCatalogFixture.OfflineEfficiencyNumerator,
                        AcceptedFoundationCatalogFixture.OfflineEfficiencyDenominator,
                        AcceptedFoundationCatalogFixture.OfflineMaxCreditedTicks),
                });
        }

        public static DefinitionCatalog OverCapacityCatalog() =>
            Catalog(AcceptedFoundationCatalogFixture.BuildingId, OverCapacityInputCapacity, AcceptedFoundationCatalogFixture.OutputBufferCapacity);

        public static DefinitionCatalog UnsupportedIdCatalog() =>
            Catalog(UnsupportedBuildingId, AcceptedFoundationCatalogFixture.InputBufferCapacity,
                AcceptedFoundationCatalogFixture.OutputBufferCapacity);

        public static BuildingInstanceState Building(DefinitionCatalog catalog, string buildingId, BuildingInstanceId id, int x, int y,
            long input, long output, long progressTicks, bool isActive)
        {
            var definition = catalog.Resolve(new BuildingDefinitionId(buildingId));
            return new BuildingInstanceState(
                id,
                definition,
                new GridPosition(x, y),
                0,
                isActive,
                new ResourceBufferState(
                    definition.InputBuffer, new[] { new KeyValuePair<ResourceId, long>(AcceptedFoundationCatalogFixture.Input, input) }),
                new ResourceBufferState(
                    definition.OutputBuffer, new[] { new KeyValuePair<ResourceId, long>(AcceptedFoundationCatalogFixture.Output, output) }),
                new ProductionState(catalog.Resolve(AcceptedFoundationCatalogFixture.Recipe), progressTicks));
        }

        public static Checkpoint CheckpointOf(string checkpointId, long generation, DefinitionCatalog catalog, params BuildingInstanceState[] buildings)
        {
            var factory = new FactoryState(new FactoryBounds(new GridPosition(0, 0), 16, 16), catalog, buildings);
            return new Checkpoint(
                new CheckpointId(checkpointId),
                generation,
                DepartureUtc,
                new OfflinePolicyRevision(DefinitionCatalog.SupportedOfflinePolicyRevision),
                null,
                factory);
        }

        /// <summary>Two machines holding 1500 input each: 3000 stock units, above every level-1 capacity.</summary>
        public static Checkpoint OverCapacity()
        {
            var catalog = OverCapacityCatalog();
            return CheckpointOf(
                "11111111-1111-1111-1111-111111110007", 0, catalog,
                Building(catalog, AcceptedFoundationCatalogFixture.BuildingId, InstanceId(7001), 0, 0, OverCapacityInputPerMachine, 0, 0, true),
                Building(catalog, AcceptedFoundationCatalogFixture.BuildingId, InstanceId(7002), 2, 0, OverCapacityInputPerMachine, 0, 0, true));
        }

        /// <summary>Five D001 machines in a row: one more than the four level-1 slots.</summary>
        public static Checkpoint OverSlot()
        {
            var catalog = AcceptedFoundationCatalogFixture.CreateCatalog();
            var buildings = Enumerable.Range(0, 5)
                .Select(i => Building(catalog, AcceptedFoundationCatalogFixture.BuildingId, InstanceId(8001 + i), i * 2, 0, 4, 0, 0, true))
                .ToArray();
            return CheckpointOf("11111111-1111-1111-1111-111111110008", 0, catalog, buildings);
        }

        /// <summary>One machine whose building definition ID is not <c>dev.machine.basic</c>.</summary>
        public static Checkpoint UnsupportedId()
        {
            var catalog = UnsupportedIdCatalog();
            return CheckpointOf(
                "11111111-1111-1111-1111-111111110009", 0, catalog,
                Building(catalog, UnsupportedBuildingId, InstanceId(9001), 0, 0, 4, 0, 0, true));
        }

        /// <summary>The bytes of fixture 02 without their last byte: not valid JSON, so the Save V1 mapper rejects them.</summary>
        public static byte[] Corrupt(byte[] fixture02Bytes)
        {
            var truncated = new byte[fixture02Bytes.Length - 1];
            Array.Copy(fixture02Bytes, truncated, truncated.Length);
            return truncated;
        }
    }

    /// <summary>
    /// A V1 directory behind a file-system fake that denies every write: the legacy source only reads through it, and a
    /// byte hash of the whole directory (every slot in a fixed order, with its name and length) proves that nothing changed
    /// (SC-005, QA-1).
    /// </summary>
    internal sealed class WriteDenyingV1Directory : ICheckpointStorageBackend
    {
        private readonly Dictionary<CheckpointStorageFile, byte[]> files;

        public WriteDenyingV1Directory(IDictionary<CheckpointStorageFile, byte[]> files)
        {
            StorageIdentity = "write-denying-v1-" + Guid.NewGuid().ToString("N");
            this.files = files.ToDictionary(entry => entry.Key, entry => (byte[])entry.Value.Clone());
        }

        public string StorageIdentity { get; }
        public int WriteAttempts { get; private set; }

        public bool Exists(CheckpointStorageFile file) => files.ContainsKey(file);
        public long GetLength(CheckpointStorageFile file) => files[file].Length;
        public byte[] ReadExactly(CheckpointStorageFile file, int expectedLength) => (byte[])files[file].Clone();
        public void Create(CheckpointStorageFile file) => Deny();
        public void Write(CheckpointStorageFile file, byte[] bytes) => Deny();
        public void Flush(CheckpointStorageFile file) => Deny();
        public void FlushExisting(CheckpointStorageFile file) => Deny();
        public void Close(CheckpointStorageFile file) => Deny();

        /// <summary>SHA-256 over every slot of the directory in enum order: name, length and bytes.</summary>
        public string DirectoryHash()
        {
            using (var sha = SHA256.Create())
            {
                var all = new List<byte>();
                foreach (var file in files.Keys.OrderBy(key => (int)key))
                {
                    all.AddRange(System.Text.Encoding.ASCII.GetBytes(file + ":" + files[file].Length + ":"));
                    all.AddRange(files[file]);
                }

                return string.Concat(sha.ComputeHash(all.ToArray()).Select(value => value.ToString("x2")));
            }
        }

        private void Deny()
        {
            WriteAttempts++;
            throw new UnauthorizedAccessException("The Save V1 directory is read-only.");
        }
    }

    /// <summary>A legacy source port fake that counts reads and can return any checkpoint or outcome.</summary>
    internal sealed class FakeLegacySource : ILegacyCheckpointSource
    {
        private readonly ILegacyCheckpointSource inner;
        private readonly Checkpoint checkpoint;
        private readonly CommandOutcome outcome;

        public FakeLegacySource(ILegacyCheckpointSource inner)
        {
            this.inner = inner;
        }

        public FakeLegacySource(Checkpoint checkpoint, CommandOutcome outcome = CommandOutcome.Success)
        {
            this.checkpoint = checkpoint;
            this.outcome = outcome;
        }

        public int Reads { get; private set; }
        public string Description => "fake legacy source";

        public bool TryReadActive(out Checkpoint result, out CommandOutcome readOutcome)
        {
            Reads++;
            if (inner != null) return inner.TryReadActive(out result, out readOutcome);
            result = outcome == CommandOutcome.Success ? checkpoint : null;
            readOutcome = outcome;
            return outcome == CommandOutcome.Success;
        }
    }

    /// <summary>The Save V1 data file as opaque bytes, so a test can publish a hash-valid generation whose content is corrupt.</summary>
    internal sealed class RawV1Checkpoint
    {
        public RawV1Checkpoint(CheckpointId id, long generation, byte[] bytes)
        {
            Id = id;
            Generation = generation;
            Bytes = bytes;
        }

        public CheckpointId Id { get; }
        public long Generation { get; }
        public byte[] Bytes { get; }
    }

    internal sealed class RawV1Codec : ICheckpointGenerationCodec<RawV1Checkpoint>
    {
        private readonly CheckpointId id;
        private readonly long generation;

        public RawV1Codec(CheckpointId id, long generation)
        {
            this.id = id;
            this.generation = generation;
        }

        public int MaximumByteCount => SaveV1Mapper.MaximumByteCount;
        public CheckpointId GetId(RawV1Checkpoint checkpoint) => checkpoint.Id;
        public long GetGeneration(RawV1Checkpoint checkpoint) => checkpoint.Generation;
        public CommandResult<byte[]> Serialize(RawV1Checkpoint checkpoint) => CommandResult<byte[]>.Success(checkpoint.Bytes);

        public CheckpointDecodeResult<RawV1Checkpoint> Deserialize(byte[] bytes) =>
            CheckpointDecodeResult<RawV1Checkpoint>.Success(new RawV1Checkpoint(id, generation, bytes));

        public bool ValidateBytes(RawV1Checkpoint checkpoint, byte[] bytes) => checkpoint.Bytes.SequenceEqual(bytes);
    }
}
