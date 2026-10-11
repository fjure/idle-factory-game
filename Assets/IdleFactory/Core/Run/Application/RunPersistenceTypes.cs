using System;
using System.IO;
using System.Security;
using IdleFactory.Core.Application;
using IdleFactory.Core.Numerics;
using IdleFactory.Domain;
using IdleFactory.Run.Content;
using IdleFactory.Run.Domain;
using IdleFactory.Run.Simulation;

namespace IdleFactory.Run.Application
{
    /// <summary>
    /// The stable, static reason codes of the persistence commands (SEC-5: never text taken from a file). The prestige
    /// and lifecycle codes are the ones of <see cref="RunReasons"/>; the others are the Feature 001 persistence
    /// outcomes in code form. UI copy keys off these strings, so they never change.
    /// </summary>
    public static class RunPersistenceReasons
    {
        public const string TransitionPending = RunReasons.TransitionPending;

        /// <summary>A pending prestige whose promotion is already durable cannot be cancelled; retry it to publish it (T105).</summary>
        public const string TransitionCommitted = "transition.committed";
        public const string CommandInvalid = RunReasons.CommandInvalid;
        public const string ReturnBlocked = "return.blocked";
        public const string LifecycleNoOp = "lifecycle.noop";

        /// <summary>The discard of a damaged sole save is not available: a valid V2 generation exists or the store is not in that state (T106).</summary>
        public const string DiscardUnavailable = "discard.unavailable";
        public const string PersistenceFailed = "persistence.failed";
        public const string NoCheckpoint = "checkpoint.none";
        public const string InvalidCheckpoint = "checkpoint.invalid";
        public const string UnsupportedVersion = "checkpoint.version";

        /// <summary>A content ID of the save is missing from this build: a save of another build, never discardable (T141).</summary>
        public const string UnresolvedContent = "checkpoint.content";

        /// <summary>Maps a Feature 001 <see cref="CommandOutcome"/> to its code; anything else is <see cref="CommandInvalid"/>.</summary>
        public static string From(CommandOutcome outcome)
        {
            switch (outcome)
            {
                case CommandOutcome.NoCheckpoint: return NoCheckpoint;
                case CommandOutcome.UnsupportedSaveVersion: return UnsupportedVersion;
                case CommandOutcome.UnresolvedContent: return UnresolvedContent;
                case CommandOutcome.InvalidCheckpoint: return InvalidCheckpoint;
                case CommandOutcome.PersistenceFailed: return PersistenceFailed;
                case CommandOutcome.ReturnBlocked: return ReturnBlocked;
                case CommandOutcome.LifecycleNoOp: return LifecycleNoOp;
                default: return CommandInvalid;
            }
        }
    }

    /// <summary>What a successful persistence command did.</summary>
    public sealed class RunPersistenceOutcome
    {
        internal RunPersistenceOutcome(CheckpointId? checkpointId, long? generation, bool usedFallback, bool wasNoOp,
            bool noCheckpoint, bool stateReplaced, RunOfflineSummary offlineSummary = null, bool transitionPending = false,
            bool imported = false)
        {
            Imported = imported;
            OfflineSummary = offlineSummary;
            TransitionPending = transitionPending;
            CheckpointId = checkpointId;
            Generation = generation;
            UsedFallback = usedFallback;
            WasNoOp = wasNoOp;
            NoCheckpoint = noCheckpoint;
            StateReplaced = stateReplaced;
        }

        /// <summary>The durable checkpoint the command saved, loaded or returned from; null when none was involved.</summary>
        public CheckpointId? CheckpointId { get; }

        public long? Generation { get; }

        /// <summary>True only for the in-process failed-suspension fallback (FR-037): the last durable active checkpoint was used.</summary>
        public bool UsedFallback { get; }

        /// <summary>True when nothing was written or changed (a repeated suspension, or an autosave that skipped).</summary>
        public bool WasNoOp { get; }

        /// <summary>True when a return found no checkpoint at all and kept the run the session already holds.</summary>
        public bool NoCheckpoint { get; }

        /// <summary>True when a durable generation replaced the live run (return, reload, recovery or a published prestige).</summary>
        public bool StateReplaced { get; }

        /// <summary>
        /// What the offline return credited (T105), derived once by the return that was just published, including the
        /// forfeited credit (<see cref="RunOfflineSummary.Code"/>). Null when no offline return was derived in this
        /// process: saves, reloads, recoveries, a return that only found an already durable result, or a return with no
        /// departure (the summary needs one, so the layer above shows none).
        /// </summary>
        public RunOfflineSummary OfflineSummary { get; }

        /// <summary>
        /// True when a return left a prestige transition pending: the durable active generation is not its promotion, so
        /// the old run stays, nothing was credited and <see cref="RunPersistence.RetryPendingTransition"/> remains available.
        /// </summary>
        public bool TransitionPending { get; }

        /// <summary>
        /// True when the live run is the durable result of a V1 import (T106): the cold start found no V2 checkpoint file and
        /// promoted the imported run. A start that loads a V2 checkpoint or begins a fresh run reports false.
        /// </summary>
        public bool Imported { get; }

        /// <summary>
        /// True when a Retry or a confirmed Start new run of a blocked import found a durable V2 checkpoint (a promotion that
        /// was reported as failed but landed) and selected it instead of doing what the player chose (T106). The UI can say so.
        /// </summary>
        public bool SelectedDurableCheckpoint { get; private set; }

        /// <summary>
        /// True when <see cref="RunPersistence.Autosave"/> skipped because the latest checkpoint is younger than the minimum
        /// interval (T306, FR-033); <see cref="WasNoOp"/> is then also true and nothing was written.
        /// </summary>
        public bool AutosaveThrottled { get; private set; }

        internal RunPersistenceOutcome AsAutosaveThrottled()
        {
            var copy = new RunPersistenceOutcome(CheckpointId, Generation, UsedFallback, WasNoOp, NoCheckpoint, StateReplaced,
                OfflineSummary, TransitionPending, Imported);
            copy.AutosaveThrottled = true;
            return copy;
        }

        internal RunPersistenceOutcome AsSelectedDurable()
        {
            var copy = new RunPersistenceOutcome(CheckpointId, Generation, UsedFallback, WasNoOp, NoCheckpoint, StateReplaced,
                OfflineSummary, TransitionPending, Imported);
            copy.SelectedDurableCheckpoint = true;
            return copy;
        }
    }

    /// <summary>The persistence status read model: the last known durable state, never text taken from a file.</summary>
    public sealed class RunPersistenceStatus
    {
        public RunPersistenceStatus(PersistenceStatusKind kind, CheckpointId? activeCheckpointId, bool previousCheckpointAvailable,
            string message)
        {
            Kind = kind;
            ActiveCheckpointId = activeCheckpointId;
            PreviousCheckpointAvailable = previousCheckpointAvailable;
            Message = message ?? string.Empty;
        }

        public PersistenceStatusKind Kind { get; }
        public CheckpointId? ActiveCheckpointId { get; }
        public bool PreviousCheckpointAvailable { get; }
        public string Message { get; }
    }

    /// <summary>
    /// The durable coordinator of a pending prestige transition (T105), attached to the <see cref="RunSession"/> by
    /// <see cref="RunPersistence"/> so that <see cref="RunCommands.PromotePrestige"/> and
    /// <see cref="RunCommands.CancelPendingTransition"/> cannot bypass the write.
    /// </summary>
    internal interface IPendingTransitionCoordinator
    {
        RunCommandResult<Guid> Promote();
        RunCommandResult<bool> Cancel();
    }
}
