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
        {
            _ = id.Value;
            if (reconciledFromCheckpointId.HasValue) _ = reconciledFromCheckpointId.Value.Value;
            if (generation < 0) throw new ArgumentOutOfRangeException(nameof(generation));
            if (!ExactBound.Contains(departureUtcMs)) throw new ArgumentOutOfRangeException(nameof(departureUtcMs));
            if (offlinePolicyRevision < 1) throw new ArgumentOutOfRangeException(nameof(offlinePolicyRevision));
            if (contentRevision < 1) throw new ArgumentOutOfRangeException(nameof(contentRevision));
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
        /// Only the pending slot exists: a candidate that was never promoted. The generation protocol discards it
        /// (nothing was published), so a caller may treat this like <see cref="NoFiles"/> (OPS-2).
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

        /// <summary>Which checkpoint files exist, without reading or validating them. Failure is a typed rejection.</summary>
        CommandResult<RunCheckpointPresence> ProbeFiles();
    }
}
