using System;
using System.Collections.Generic;
using IdleFactory.Run.Domain;

namespace IdleFactory.Unity.Persistence
{
    /// <summary>
    /// Static rejection codes of the Save V2 mapper (contracts/save-v2-and-v1-import.md, load rule 5, SEC-5). A code
    /// and its copy are constants; neither is ever built from text that came from a file.
    /// </summary>
    public static class SaveV2Codes
    {
        /// <summary>The input is null, empty or larger than 1 MiB.</summary>
        public const string Size = "save.v2.size";

        /// <summary>The input starts with a BOM or is not strict UTF-8.</summary>
        public const string Encoding = "save.v2.encoding";

        /// <summary>Malformed JSON, a float, an unknown, duplicate or missing member, or a value of the wrong type.</summary>
        public const string Structure = "save.v2.structure";

        /// <summary>Depth above 16, more than 64 machines, more than 256 entries in a list, or an ID above 128 code units.</summary>
        public const string Limit = "save.v2.limit";

        /// <summary>An integer outside its documented range (including any integer above 10^18).</summary>
        public const string Bounds = "save.v2.bounds";

        /// <summary><c>schemaVersion</c> is not 2 or 3 (Save V3, T334), or <c>offlinePolicyRevision</c> is not one the catalog has.</summary>
        public const string Version = "save.v2.version";

        /// <summary>An ID that does not resolve in the current catalog.</summary>
        public const string Unresolved = "save.v2.id.unresolved";

        /// <summary>A machine or resource is present that the <c>unlocked</c> set does not list (load rule 1).</summary>
        public const string Locked = "save.v2.locked";

        /// <summary>A domain invariant of data-model.md is violated.</summary>
        public const string Invariant = "save.v2.invariant";

        /// <summary>A cross-field bound of load rule 4 (SEC-2) is violated.</summary>
        public const string CrossField = "save.v2.crossfield";

        /// <summary>The state to serialize is not representable (serialization only).</summary>
        public const string Write = "save.v2.write";

        /// <summary>The fixed copy for a code; an unknown code maps to the generic copy.</summary>
        public static string Message(string code)
        {
            switch (code)
            {
                case Size: return "The save file is empty or too large.";
                case Encoding: return "The save file is not valid UTF-8 without a byte order mark.";
                case Structure: return "The save file structure is not valid.";
                case Limit: return "The save file exceeds a structural limit.";
                case Bounds: return "The save file contains a number outside its allowed range.";
                case Version: return "The save file version is not supported.";
                case Unresolved: return "The save file refers to content that does not exist.";
                case Locked: return "The save file contains content that is not unlocked.";
                case Invariant: return "The save file contradicts the game rules.";
                case CrossField: return "The save file contains times or identities that do not match the content.";
                case Write: return "The state cannot be written as a save file.";
                default: return "The save file cannot be used.";
            }
        }
    }

    /// <summary>The optional record that a checkpoint was written by a V1 import.</summary>
    public sealed class SaveV2Import
    {
        public SaveV2Import(int sourceSchemaVersion, string sourceCheckpointId, long sourceGeneration)
        {
            SourceSchemaVersion = sourceSchemaVersion;
            SourceCheckpointId = sourceCheckpointId;
            SourceGeneration = sourceGeneration;
        }

        public int SourceSchemaVersion { get; }
        public string SourceCheckpointId { get; }
        public long SourceGeneration { get; }
    }

    /// <summary>The checkpoint envelope: identity, offline-return anchor and diagnostics of one V2 file.</summary>
    public sealed class SaveV2Meta
    {
        public SaveV2Meta(Guid checkpointId, long generation, long departureUtcMs, int offlinePolicyRevision,
            Guid? reconciledFromCheckpointId, int contentRevision, SaveV2Import import)
        {
            CheckpointId = checkpointId;
            Generation = generation;
            DepartureUtcMs = departureUtcMs;
            OfflinePolicyRevision = offlinePolicyRevision;
            ReconciledFromCheckpointId = reconciledFromCheckpointId;
            ContentRevision = contentRevision;
            Import = import;
        }

        public Guid CheckpointId { get; }
        public long Generation { get; }
        public long DepartureUtcMs { get; }
        public int OfflinePolicyRevision { get; }
        public Guid? ReconciledFromCheckpointId { get; }

        /// <summary>Diagnostic only; ID resolution is authoritative.</summary>
        public int ContentRevision { get; }

        public SaveV2Import Import { get; }
    }

    /// <summary>A loaded checkpoint: its envelope and the domain state built through the constructors and restore paths.</summary>
    public sealed class SaveV2Document
    {
        public SaveV2Document(SaveV2Meta meta, RunState run, PermanentState permanent)
            : this(meta, run, permanent, SaveV2Mapper.SchemaVersion)
        {
        }

        public SaveV2Document(SaveV2Meta meta, RunState run, PermanentState permanent, int schemaVersion)
        {
            Meta = meta;
            Run = run;
            Permanent = permanent;
            SchemaVersion = schemaVersion;
        }

        /// <summary>The <c>schemaVersion</c> the file was read with (2 or 3); diagnostic only, the state is the same either way (T334).</summary>
        public int SchemaVersion { get; }

        public SaveV2Meta Meta { get; }
        public RunState Run { get; }
        public PermanentState Permanent { get; }
    }

    /// <summary>The outcome of <see cref="SaveV2Mapper.Deserialize"/>: a document, or a static code and copy.</summary>
    public sealed class SaveV2ReadResult
    {
        private SaveV2ReadResult(SaveV2Document document, string code)
        {
            Document = document;
            Code = code;
        }

        public bool IsSuccess => Document != null;
        public SaveV2Document Document { get; }

        /// <summary>The static rejection code (a <see cref="SaveV2Codes"/> constant), or null on success.</summary>
        public string Code { get; }

        public string Message => Code == null ? null : SaveV2Codes.Message(Code);

        internal static SaveV2ReadResult Success(SaveV2Document document) => new SaveV2ReadResult(document, null);
        internal static SaveV2ReadResult Rejected(string code) => new SaveV2ReadResult(null, code);
    }

    /// <summary>The outcome of <see cref="SaveV2Mapper.Serialize"/>: the bytes, or a static code and copy.</summary>
    public sealed class SaveV2WriteResult
    {
        private SaveV2WriteResult(byte[] bytes, string code)
        {
            Bytes = bytes;
            Code = code;
        }

        public bool IsSuccess => Bytes != null;
        public byte[] Bytes { get; }
        public string Code { get; }
        public string Message => Code == null ? null : SaveV2Codes.Message(Code);

        internal static SaveV2WriteResult Success(byte[] bytes) => new SaveV2WriteResult(bytes, null);
        internal static SaveV2WriteResult Rejected(string code) => new SaveV2WriteResult(null, code);
    }

    // The DTOs below are the raw, structurally validated content of one file. They hold no domain state and are
    // turned into domain objects only by SaveV2Mapper's build step, through the constructors and restore paths.

    internal sealed class SaveV2Dto
    {
        public int SchemaVersion;
        public long Generation;
        public long DepartureUtcMs;
        public int OfflinePolicyRevision;
        public string CheckpointId;
        public string ReconciledFromCheckpointId;
        public int ContentRevision;
        public ImportV2Dto Import;
        public PermanentV2Dto Permanent;
        public RunV2Dto Run;
    }

    internal sealed class ImportV2Dto
    {
        public int SourceSchemaVersion;
        public string SourceCheckpointId;
        public long SourceGeneration;
    }

    internal sealed class IdValueV2Dto
    {
        public IdValueV2Dto(string id, long value)
        {
            Id = id;
            Value = value;
        }

        public string Id { get; }
        public long Value { get; }
    }

    internal sealed class PermanentV2Dto
    {
        public long Ip;
        public long PrestigeCount;
        public List<IdValueV2Dto> Industry;
        public List<IdValueV2Dto> Items;
    }

    internal sealed class ExpansionV2Dto
    {
        public long Level;
        public long InvestedCash;
    }

    internal sealed class MachineV2Dto
    {
        public string InstanceId;
        public string MachineId;
        public long Slot;
        public long Level;
        public bool IsActive;
        public long ProgressTicks;
        public long CycleDurationTicks;
        public long InvestedCash;
    }

    internal sealed class ActiveContractV2Dto
    {
        public string OfferId;
        public long AcceptedUtcMs;
        public long? DeadlineUtcMs;
        public long RequiredUnits;

        // Save V3 members (T334); a V2 file leaves the V2 defaults: auto-deliver off, nothing produced.
        public bool AutoDeliver;
        public long Produced;
    }

    /// <summary>One <c>bonusOffers[]</c> entry of a Save V3 file (T334).</summary>
    internal sealed class BonusOfferV3Dto
    {
        public string OfferId;
        public bool Consumed;
        public long WindowEndUtcMs;
    }

    internal sealed class ContractsV2Dto
    {
        public List<IdValueV2Dto> Completions;
        public ActiveContractV2Dto Active;
    }

    internal sealed class EffectV2Dto
    {
        public string ItemId;
        public string EffectKind;
        public string TargetKey;
        public long StartUtcMs;
        public long EndUtcMs;
    }

    internal sealed class OverdriveV2Dto
    {
        public long? ActiveUntilUtcMs;
        public long? CooldownUntilUtcMs;
    }

    internal sealed class RunV2Dto
    {
        public string RunId;
        public long RunNumber;
        public string PreviousRunId;
        public long Cash;
        public long Rp;
        public ExpansionV2Dto Storage;
        public ExpansionV2Dto Factory;
        public List<IdValueV2Dto> Inventory;
        public List<MachineV2Dto> Machines;
        public List<string> Priority;
        public List<string> AutoSell;
        public List<string> Unlocked;
        public ContractsV2Dto Contracts;
        public List<IdValueV2Dto> Research;
        public List<EffectV2Dto> Effects;
        public OverdriveV2Dto Overdrive;

        // Save V3 members (T334); null in a V2 file.
        public long? RunCompletionTotal;
        public List<IdValueV2Dto> ContractCooldowns;
        public List<BonusOfferV3Dto> BonusOffers;
    }
}
