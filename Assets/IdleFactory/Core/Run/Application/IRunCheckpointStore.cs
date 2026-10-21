using System;
using IdleFactory.Core.Application;
using IdleFactory.Core.Numerics;
using IdleFactory.Domain;
using IdleFactory.Run.Domain;

namespace IdleFactory.Run.Application
{
    /// <summary>
    /// The record that a checkpoint was written by a V1 import (contracts/save-v2-and-v1-import.md, Envelope). The
    /// source checkpoint ID is kept as the canonical UUID text the codec round-trips byte for byte.
    /// </summary>
    public sealed class RunCheckpointImport
    {
        public RunCheckpointImport(int sourceSchemaVersion, string sourceCheckpointId, long sourceGeneration)
        {
            SourceSchemaVersion = sourceSchemaVersion;
            SourceCheckpointId = sourceCheckpointId ?? throw new ArgumentNullException(nameof(sourceCheckpointId));
            SourceGeneration = sourceGeneration;
        }

        public int SourceSchemaVersion { get; }
        public string SourceCheckpointId { get; }
        public long SourceGeneration { get; }
    }

    /// <summary>
    /// One Save V2 generation in Core terms (T138): the envelope (identity, generation, departure boundary, offline
    /// policy, exactly-once link, diagnostic content revision, optional import record) and the run and permanent state.
    /// Core has no file format: the Unity store owns the codec (<c>SaveV2Mapper</c>) and the file backend, and this type
    /// is what crosses the <see cref="IRunCheckpointStore"/> port. A checkpoint read from the store holds freshly built
    /// state objects that nothing else references; a checkpoint written by <see cref="RunPersistence"/> may reference
    /// the live state, so a store must serialize it before <see cref="IRunCheckpointStore.Promote"/> returns.
    /// </summary>
    public sealed class RunCheckpoint
    {
        public RunCheckpoint(
            CheckpointId id,
            long generation,
            long departureUtcMs,
            int offlinePolicyRevision,
            CheckpointId? reconciledFromCheckpointId,
            int contentRevision,
            RunCheckpointImport import,
            RunState run,
            PermanentState permanent)
            : this(id, generation, departureUtcMs, offlinePolicyRevision, reconciledFromCheckpointId, contentRevision, import, run, permanent, 0)
        {
        }

        /// <summary>
        /// A checkpoint as the store decoded it, with the number of contract-scoped entries the decoder dropped as content drift
        /// (Save V3, T335: entries of offers the loaded content no longer has, or an active contract whose offer changed objective).
        /// </summary>
        public RunCheckpoint(
            CheckpointId id,
            long generation,
            long departureUtcMs,
            int offlinePolicyRevision,
            CheckpointId? reconciledFromCheckpointId,
            int contentRevision,
            RunCheckpointImport import,
            RunState run,
            PermanentState permanent,
            int droppedContractEntries)
        {
            _ = id.Value;
            if (reconciledFromCheckpointId.HasValue) _ = reconciledFromCheckpointId.Value.Value;
            if (generation < 0) throw new ArgumentOutOfRangeException(nameof(generation));
            if (!ExactBound.Contains(departureUtcMs)) throw new ArgumentOutOfRangeException(nameof(departureUtcMs));
            if (offlinePolicyRevision < 1) throw new ArgumentOutOfRangeException(nameof(offlinePolicyRevision));
            if (contentRevision < 1) throw new ArgumentOutOfRangeException(nameof(contentRevision));
            if (droppedContractEntries < 0) throw new ArgumentOutOfRangeException(nameof(droppedContractEntries));
            DroppedContractEntries = droppedContractEntries;
            Id = id;
            Generation = generation;
            DepartureUtcMs = departureUtcMs;
            OfflinePolicyRevision = offlinePolicyRevision;
            ReconciledFromCheckpointId = reconciledFromCheckpointId;
            ContentRevision = contentRevision;
            Import = import;
            Run = run ?? throw new ArgumentNullException(nameof(run));
            Permanent = permanent ?? throw new ArgumentNullException(nameof(permanent));
        }

        public CheckpointId Id { get; }
        public long Generation { get; }

        /// <summary>Departure boundary for the offline return, Unix milliseconds.</summary>
        public long DepartureUtcMs { get; }

        public int OfflinePolicyRevision { get; }
        public CheckpointId? ReconciledFromCheckpointId { get; }

        /// <summary>Diagnostic only; ID resolution is authoritative.</summary>
        public int ContentRevision { get; }

        /// <summary>Non-null only on the checkpoint written by a V1 import.</summary>
        public RunCheckpointImport Import { get; }

        public RunState Run { get; }
        public PermanentState Permanent { get; }

        /// <summary>
        /// How many contract-scoped entries the store dropped as content drift when it decoded this checkpoint (Save V3, T335); 0 for a
        /// checkpoint that was not read from a store or lost nothing. Not persisted: the next write carries the state without them. The
        /// offline return reports a non-zero count as <see cref="RunPersistenceReasons.ContractDropped"/> (<see cref="RunOfflineSummary.ContractDropCode"/>).
        /// </summary>
        public int DroppedContractEntries { get; }
    }

    /// <summary>The outcome of a store read: a checkpoint, or a <see cref="CommandOutcome"/> with static copy.</summary>
    public sealed class RunCheckpointReadResult
    {
        private RunCheckpointReadResult(CommandOutcome outcome, RunCheckpoint checkpoint, string message)
        {
            Outcome = outcome;
            Checkpoint = checkpoint;
            Message = message ?? string.Empty;
        }

        public CommandOutcome Outcome { get; }
        public RunCheckpoint Checkpoint { get; }

        /// <summary>Static copy that never contains text taken from a file (SEC-5).</summary>
        public string Message { get; }

        public bool Succeeded => Outcome == CommandOutcome.Success;

        public static RunCheckpointReadResult Success(RunCheckpoint checkpoint) =>
            new RunCheckpointReadResult(
                CommandOutcome.Success,
                checkpoint ?? throw new ArgumentNullException(nameof(checkpoint)),
                string.Empty);

        public static RunCheckpointReadResult Rejected(CommandOutcome outcome, string message)
        {
            if (outcome == CommandOutcome.Success)
            {
                throw new ArgumentException("A rejected read requires a non-success outcome.", nameof(outcome));
            }

            return new RunCheckpointReadResult(outcome, null, message);
        }
    }

    /// <summary>What a V2 checkpoint directory holds, for the cold-start source selection (contracts, Cold-start source selection).</summary>
    public enum RunCheckpointPresence
    {
        /// <summary>No checkpoint file at all.</summary>
        NoFiles = 0,

        /// <summary>
        /// A pending file exists but no data or manifest slot was ever written (the first promote was interrupted
        /// before the data slot). The generation protocol never treats a pending file as authoritative. The Save V2
        /// contract (cold-start rule 3, OPS-2) counts this case as no checkpoint file, so a cold start may import V1.
        /// </summary>
        PendingOnly = 1,

        /// <summary>A data or manifest file exists, whether or not it forms a valid active checkpoint.</summary>
        PublishedFiles = 2,
    }

    /// <summary>
    /// The Core port over the Save V2 generation protocol (T138). Same meanings as the Feature 001
    /// <c>ICheckpointStore</c>: reads are strict and fail closed, <see cref="Promote"/> publishes a candidate only after
    /// it has been written, read back and verified, and is idempotent for the exact same checkpoint.
    /// </summary>
    public interface IRunCheckpointStore
    {
        RunCheckpointReadResult ReadActive();
        RunCheckpointReadResult ReadPrevious();
        RunCheckpointReadResult Read(CheckpointId checkpointId);
        CommandResult<CheckpointId> Promote(RunCheckpoint checkpoint);
        CommandResult<CheckpointId> RecoverPrevious(RunCheckpoint recoveryCandidate);

        /// <summary>
        /// The generation a <see cref="SupersedeDamaged"/> candidate must carry, or the typed rejection of the manifest
        /// selection (no selectable manifest: use <see cref="Promote"/> with generation 1; a storage failure).
        /// </summary>
        CommandResult<long> NextSupersedeGeneration();

        /// <summary>
        /// The confirmed discard of a damaged sole save with an intact manifest (T141, SEC-1). Publishes the candidate only
        /// when no retained generation is readable; refused when any generation is valid, when a read hits a storage
        /// failure, or when a generation is another build's save. Every failure step leaves the damaged state as it was.
        /// </summary>
        CommandResult<CheckpointId> SupersedeDamaged(RunCheckpoint checkpoint);

        /// <summary>Which checkpoint files exist, without reading or validating them. Failure is a typed rejection.</summary>
        CommandResult<RunCheckpointPresence> ProbeFiles();
    }
}
