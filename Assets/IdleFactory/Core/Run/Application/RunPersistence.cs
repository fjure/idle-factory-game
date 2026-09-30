using System;
using System.IO;
using System.Security;
using IdleFactory.Core.Application;
using IdleFactory.Core.Numerics;
using IdleFactory.Domain;

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
        public const string CommandInvalid = RunReasons.CommandInvalid;
        public const string ReturnBlocked = "return.blocked";
        public const string LifecycleNoOp = "lifecycle.noop";
        public const string PersistenceFailed = "persistence.failed";
        public const string NoCheckpoint = "checkpoint.none";
        public const string InvalidCheckpoint = "checkpoint.invalid";
        public const string UnsupportedVersion = "checkpoint.version";

        /// <summary>Maps a Feature 001 <see cref="CommandOutcome"/> to its code; anything else is <see cref="CommandInvalid"/>.</summary>
        public static string From(CommandOutcome outcome)
        {
            switch (outcome)
            {
                case CommandOutcome.NoCheckpoint: return NoCheckpoint;
                case CommandOutcome.UnsupportedSaveVersion: return UnsupportedVersion;
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
            bool noCheckpoint, bool stateReplaced)
        {
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

        /// <summary>True when a durable generation replaced the live run (return, reload or recovery).</summary>
        public bool StateReplaced { get; }
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
    /// The seam T105 fills with the offline simulation. <see cref="Reconcile"/> turns the source checkpoint of a return
    /// into the candidate checkpoint that is promoted and only then published (R09, exactly once by
    /// <c>reconciledFromCheckpointId</c>). A reconciler must be a pure function of its arguments, so that a retry with
    /// the same captured return instant builds the same candidate.
    /// </summary>
    public interface IRunReturnReconciler
    {
        RunCommandResult<RunCheckpoint> Reconcile(RunCheckpoint source, long returnUtcMs, CheckpointId resultId, long resultGeneration);
    }

    /// <summary>
    /// The T138 reconciler: it credits no offline time. The candidate is the source state under a new identity,
    /// <c>reconciledFromCheckpointId</c> = the source, and the departure moved forward to the return instant (never
    /// back). Offline production is wired in T105 by replacing this seam.
    /// </summary>
    public sealed class NoCreditRunReturnReconciler : IRunReturnReconciler
    {
        public RunCommandResult<RunCheckpoint> Reconcile(RunCheckpoint source, long returnUtcMs, CheckpointId resultId, long resultGeneration)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            try
            {
                long departure = returnUtcMs > source.DepartureUtcMs ? returnUtcMs : source.DepartureUtcMs;
                return RunCommandResult.Success(new RunCheckpoint(resultId, resultGeneration, departure, source.OfflinePolicyRevision,
                    source.Id, source.ContentRevision, null, source.Run, source.Permanent));
            }
            catch (ArgumentException)
            {
                return RunCommandResult.Reject<RunCheckpoint>(RunPersistenceReasons.InvalidCheckpoint);
            }
        }
    }

    /// <summary>
    /// The persistence commands of the run (T138, contracts/run-application.md): <c>SaveNow</c>,
    /// <c>RecordSuspension</c>, <c>ResumeAndReconcile</c>, <c>RetryReturn</c>, <c>ReloadAuthoritativeGeneration</c> and
    /// <c>RecoverPreviousCheckpoint</c> keep their Feature 001 meanings (<c>PersistenceCommands</c>) over Save V2:
    /// the lifecycle table and admission rules, generation and checkpoint-ID advance, a departure checkpoint per
    /// suspension with the FR-037 fallback to the active checkpoint, a captured return instant reused by retries, a
    /// candidate that is promoted and read back before it is published, and recovery of the retained previous
    /// generation as a new active one. Core owns the rules only; the file format and the files are behind
    /// <see cref="IRunCheckpointStore"/>.
    ///
    /// Decisions where the contract is silent: (1) departure = <c>max(clock, run timeline)</c> in Unix milliseconds,
    /// so every cross-field bound of load rule 4 holds for any clock; (2) outside <see cref="LifecycleState.Running"/>
    /// the session is lifecycle-blocked (<c>lifecycle.blocked</c>), so a command cannot change the run under a
    /// checkpoint; (3) after every install of a durable generation the session settles once, which is load rule 2
    /// (the R03 rescale of progress when content durations were retuned); (4) while a prestige transition is pending,
    /// <see cref="SaveNow"/> is rejected with <c>transition.pending</c>, <see cref="Autosave"/> skips and a suspension
    /// writes no checkpoint; a return installs the durable generation and thereby drops the in-memory candidate (the
    /// durable transition itself is T105); (5) a return credits no offline time until T105 replaces
    /// <see cref="IRunReturnReconciler"/>; (6) "no checkpoint" on a return means no data or manifest file exists. A lone
    /// pending file is an unpromoted candidate that the protocol discards.
    /// </summary>
    public sealed class RunPersistence
    {
        private const string SaveFailureMessage = "The factory could not be saved.";
        private const string ReturnFailureMessage = "The return could not be completed. Retry or reload the saved factory.";
        private const string LoadFailureMessage = "The saved factory could not be loaded.";
        private const string RecoveryFailureMessage = "The previous factory could not be recovered.";
        private const string StorageMessage = "Checkpoint storage is unavailable.";

        private readonly RunSession session;
        private readonly IRunCheckpointStore store;
        private readonly IUtcClock clock;
        private readonly ICheckpointIdSource checkpointIds;
        private readonly IRunReturnReconciler reconciler;

        private LifecycleState lifecycle = LifecycleState.Bootstrapping;
        private RunPersistenceStatus status;
        private CheckpointId? lastActiveCheckpointId;
        private CheckpointId? departureCheckpointId;
        private RunPersistenceOutcome departureOutcome;
        private bool suspensionAttempted;
        private RunCheckpoint pendingSource;
        private RunCheckpoint pendingCandidate;
        private RunCheckpoint pendingRecoveryCandidate;
        private bool pendingUsedFallback;
        private long capturedReturnUtcMs;
        private bool hasCapturedReturnUtc;

        public RunPersistence(RunSession session, IRunCheckpointStore store, IUtcClock clock)
            : this(session, store, clock, new GuidCheckpointIdSource(), new NoCreditRunReturnReconciler())
        {
        }

        public RunPersistence(RunSession session, IRunCheckpointStore store, IUtcClock clock, ICheckpointIdSource checkpointIds,
            IRunReturnReconciler reconciler)
        {
            this.session = session ?? throw new ArgumentNullException(nameof(session));
            this.store = store ?? throw new ArgumentNullException(nameof(store));
            this.clock = clock ?? throw new ArgumentNullException(nameof(clock));
            this.checkpointIds = checkpointIds ?? throw new ArgumentNullException(nameof(checkpointIds));
            this.reconciler = reconciler ?? throw new ArgumentNullException(nameof(reconciler));
            session.SetLifecycleBlocked(true);
            InitializeStatus();
        }

        public LifecycleState Lifecycle => lifecycle;

        public RunPersistenceStatus Status => status;

        // ---------------------------------------------------------------------------------------------------
        // Commands
        // ---------------------------------------------------------------------------------------------------

        /// <summary>Saves the live run as a new generation. Rejected with <c>transition.pending</c> while a prestige is pending.</summary>
        public RunCommandResult<RunPersistenceOutcome> SaveNow()
        {
            string rejected = Admit(ApplicationOperation.Save);
            if (rejected != null) return Reject(rejected);
            if (session.PendingTransition != null) return Reject(RunPersistenceReasons.TransitionPending);
            return SaveCheckpoint();
        }

        /// <summary>
        /// The periodic write: the same as <see cref="SaveNow"/>, except that it skips silently (a successful no-op)
        /// while a prestige transition is pending or the lifecycle is not running, so no checkpoint can race the candidate.
        /// </summary>
        public RunCommandResult<RunPersistenceOutcome> Autosave()
        {
            if (session.PendingTransition != null || lifecycle != LifecycleState.Running)
                return Succeeded(null, null, false, true, false, false);
            return SaveNow();
        }

        /// <summary>
        /// Writes the departure checkpoint and suspends. A reported departure suspends the application even if
        /// storage fails (the return then uses the FR-037 fallback). Repeated while suspended, it is a no-op.
        /// </summary>
        public RunCommandResult<RunPersistenceOutcome> RecordSuspension()
        {
            if (lifecycle == LifecycleState.Suspended && suspensionAttempted)
                return Succeeded(departureOutcome?.CheckpointId, departureOutcome?.Generation, false, true, false, false);

            string rejected = Admit(ApplicationOperation.RecordSuspension);
            if (rejected != null) return Reject(rejected);
            string checkpointing = Transition(LifecycleState.Checkpointing);
            if (checkpointing != null) return Reject(checkpointing);

            suspensionAttempted = true;
            departureCheckpointId = null;
            departureOutcome = null;
            var saved = session.PendingTransition != null
                ? Reject(RunPersistenceReasons.TransitionPending)
                : SaveCheckpoint();
            if (saved.IsSuccess)
            {
                departureCheckpointId = saved.Value.CheckpointId;
                departureOutcome = saved.Value;
            }

            string suspended = Transition(LifecycleState.Suspended);
            if (suspended != null)
            {
                SetStatus(PersistenceStatusKind.Failure, lastActiveCheckpointId, status.PreviousCheckpointAvailable,
                    "The application could not enter its suspended state.");
                return Reject(suspended);
            }

            return saved;
        }

        /// <summary>
        /// The cold start and every return: reads the departure checkpoint (or the active one), builds the return
        /// candidate with the captured return instant, promotes it and only then installs it as the live run.
        /// </summary>
        public RunCommandResult<RunPersistenceOutcome> ResumeAndReconcile()
        {
            LifecycleState startingState = lifecycle;
            string rejected = Admit(ApplicationOperation.ResumeAndReconcile);
            if (rejected != null) return Reject(rejected);
            if (startingState == LifecycleState.Bootstrapping)
            {
                string loading = Transition(LifecycleState.Loading);
                if (loading != null) return Reject(loading);
            }

            string reconciling = Transition(LifecycleState.Reconciling);
            if (reconciling != null) return Reject(reconciling);

            ClearPendingReturn();
            hasCapturedReturnUtc = TryCaptureUtcMs(out capturedReturnUtcMs);

            RunCheckpointReadResult sourceRead;
            if (departureCheckpointId.HasValue)
            {
                sourceRead = SafeRead(departureCheckpointId.Value);
                pendingUsedFallback = false;
            }
            else
            {
                sourceRead = SafeReadActive();
                pendingUsedFallback = startingState == LifecycleState.Suspended;
            }

            if (!sourceRead.Succeeded)
            {
                if (!departureCheckpointId.HasValue && sourceRead.Outcome == CommandOutcome.NoCheckpoint)
                {
                    return CompleteWithoutCheckpointOrBlock();
                }

                return BlockReturn(sourceRead.Outcome, null);
            }

            pendingSource = sourceRead.Checkpoint;

            // A checkpoint of another catalog instance can never be installed, so nothing may be promoted for it.
            if (!ReferenceEquals(pendingSource.Run.Catalog, session.Catalog)) return BlockReturn(CommandOutcome.InvalidCheckpoint, null);
            if (!hasCapturedReturnUtc) return BlockReturn(CommandOutcome.PersistenceFailed, null);
            return BuildPromoteAndPublish(pendingSource, capturedReturnUtcMs, pendingUsedFallback);
        }

        /// <summary>
        /// Retries a blocked return. It always re-reads the durable active generation first and treats a readable
        /// manifest as provisional: a candidate is published only after the store confirms that exact checkpoint.
        /// </summary>
        public RunCommandResult<RunPersistenceOutcome> RetryReturn()
        {
            string rejected = Admit(ApplicationOperation.RetryReturn);
            if (rejected != null) return Reject(rejected);

            var activeRead = SafeReadActive();
            if (!activeRead.Succeeded)
            {
                SetReadFailure(activeRead, lastActiveCheckpointId);
                return Reject(RunPersistenceReasons.ReturnBlocked);
            }

            RunCheckpoint active = activeRead.Checkpoint;
            if (pendingRecoveryCandidate != null) return RetryPendingRecovery();

            if (pendingCandidate != null && SameIdentity(active, pendingCandidate))
            {
                var confirmation = SafePromote(pendingCandidate);
                if (!confirmation.Succeeded || confirmation.Value != active.Id)
                {
                    return KeepReturnBlocked(pendingSource?.Id ?? status.ActiveCheckpointId ?? lastActiveCheckpointId);
                }

                lastActiveCheckpointId = active.Id;
                string retryTransition = Transition(LifecycleState.Reconciling);
                if (retryTransition != null) return KeepReturnBlocked(active.Id);
                return PublishDurableReturn(pendingCandidate, pendingUsedFallback);
            }

            lastActiveCheckpointId = active.Id;
            var sourceResult = ResolveRetrySource(active);
            if (!sourceResult.Succeeded) return KeepReturnBlocked(active.Id);

            RunCheckpoint source = sourceResult.Checkpoint;
            if (SameIdentity(active, source))
            {
                if (!hasCapturedReturnUtc)
                {
                    hasCapturedReturnUtc = TryCaptureUtcMs(out capturedReturnUtcMs);
                    if (!hasCapturedReturnUtc) return KeepReturnBlocked(active.Id);
                }

                string retryTransition = Transition(LifecycleState.Reconciling);
                if (retryTransition != null) return KeepReturnBlocked(active.Id);
                return BuildPromoteAndPublish(source, capturedReturnUtcMs, pendingUsedFallback);
            }

            if (hasCapturedReturnUtc && active.ReconciledFromCheckpointId.HasValue && active.ReconciledFromCheckpointId.Value == source.Id)
            {
                // The promotion of a candidate may be visible although its confirmation failed and the in-memory
                // candidate is gone: rebuild it with the identity of the active generation and confirm it exactly.
                var rebuilt = reconciler.Reconcile(source, capturedReturnUtcMs, active.Id, active.Generation);
                if (rebuilt.IsSuccess && SameIdentity(active, rebuilt.Value))
                {
                    pendingCandidate = rebuilt.Value;
                    var confirmation = SafePromote(rebuilt.Value);
                    if (!confirmation.Succeeded || confirmation.Value != active.Id) return KeepReturnBlocked(source.Id);
                    lastActiveCheckpointId = active.Id;
                    string retryTransition = Transition(LifecycleState.Reconciling);
                    if (retryTransition != null) return KeepReturnBlocked(active.Id);
                    return PublishDurableReturn(rebuilt.Value, pendingUsedFallback);
                }
            }

            return KeepReturnBlocked(active.Id);
        }

        /// <summary>Discards the in-memory return and installs the durable active generation, confirmed durable first.</summary>
        public RunCommandResult<RunPersistenceOutcome> ReloadAuthoritativeGeneration()
        {
            string rejected = Admit(ApplicationOperation.ReloadAuthoritativeGeneration);
            if (rejected != null) return Reject(rejected);

            var activeRead = SafeReadActive();
            if (!activeRead.Succeeded)
            {
                SetReadFailure(activeRead, lastActiveCheckpointId);
                return Reject(RunPersistenceReasons.ReturnBlocked);
            }

            RunCheckpoint active = activeRead.Checkpoint;
            if (!ReferenceEquals(active.Run.Catalog, session.Catalog))
            {
                SetStatus(PersistenceStatusKind.Failure, active.Id, status.PreviousCheckpointAvailable, LoadFailureMessage);
                return Reject(RunPersistenceReasons.ReturnBlocked);
            }

            var confirmation = SafePromote(active);
            if (!confirmation.Succeeded || confirmation.Value != active.Id)
            {
                SetStatus(PersistenceStatusKind.Failure, status.ActiveCheckpointId ?? lastActiveCheckpointId,
                    status.PreviousCheckpointAvailable, LoadFailureMessage);
                return Reject(RunPersistenceReasons.ReturnBlocked);
            }

            lastActiveCheckpointId = active.Id;
            if (!TryInstall(active))
            {
                SetStatus(PersistenceStatusKind.Failure, active.Id, status.PreviousCheckpointAvailable, ReturnFailureMessage);
                return Reject(RunPersistenceReasons.ReturnBlocked);
            }

            // Publication succeeded before leaving ReturnBlocked; a failed install stays blocked.
            if (Transition(LifecycleState.Loading) != null || Transition(LifecycleState.Running) != null)
            {
                SetStatus(PersistenceStatusKind.Failure, active.Id, status.PreviousCheckpointAvailable, ReturnFailureMessage);
                return Reject(RunPersistenceReasons.ReturnBlocked);
            }

            ResetReturnBookkeeping();
            pendingRecoveryCandidate = null;
            SetStatus(PersistenceStatusKind.Ready, active.Id, ReadPreviousAvailability(), "Saved factory reloaded.");
            return Succeeded(active.Id, active.Generation, false, false, false, true);
        }

        /// <summary>
        /// Publishes the retained previous generation as a new active one (new identity, generation after both) and
        /// installs it. The active generation stays retained as the new previous one.
        /// </summary>
        public RunCommandResult<RunPersistenceOutcome> RecoverPreviousCheckpoint()
        {
            if (pendingRecoveryCandidate != null) return RetryReturn();

            string rejected = Admit(ApplicationOperation.RecoverPreviousCheckpoint);
            if (rejected != null)
            {
                CheckpointId? activeId = status.ActiveCheckpointId ?? lastActiveCheckpointId;
                SetStatus(lifecycle == LifecycleState.ReturnBlocked ? PersistenceStatusKind.Failure : PersistenceStatusKind.Ready,
                    activeId, false, RecoveryFailureMessage);
                return Reject(rejected);
            }

            var previousRead = SafeReadPrevious();
            if (!previousRead.Succeeded)
            {
                CheckpointId? activeId = status.ActiveCheckpointId ?? lastActiveCheckpointId;
                bool none = previousRead.Outcome == CommandOutcome.NoCheckpoint;
                SetStatus(lifecycle == LifecycleState.ReturnBlocked ? PersistenceStatusKind.Failure
                    : none && activeId.HasValue ? PersistenceStatusKind.Ready
                    : none ? PersistenceStatusKind.NoCheckpoint : PersistenceStatusKind.Failure, activeId, false, RecoveryFailureMessage);
                return Reject(RunPersistenceReasons.From(previousRead.Outcome));
            }

            var activeRead = SafeReadActive();
            if (!activeRead.Succeeded)
            {
                SetReadFailure(activeRead, lastActiveCheckpointId);
                return Reject(RunPersistenceReasons.From(activeRead.Outcome));
            }

            RunCheckpoint active = activeRead.Checkpoint;
            RunCheckpoint previous = previousRead.Checkpoint;
            if (!ReferenceEquals(previous.Run.Catalog, session.Catalog))
            {
                SetStatus(PersistenceStatusKind.Failure, active.Id, true, RecoveryFailureMessage);
                return Reject(RunPersistenceReasons.InvalidCheckpoint);
            }

            if (!TryCaptureUtcMs(out long recoveryUtcMs))
            {
                SetStatus(PersistenceStatusKind.Failure, active.Id, true, RecoveryFailureMessage);
                return Reject(RunPersistenceReasons.PersistenceFailed);
            }

            long recoveryGeneration = active.Generation > previous.Generation ? active.Generation : previous.Generation;
            if (recoveryGeneration == long.MaxValue)
            {
                SetStatus(PersistenceStatusKind.Failure, active.Id, true, RecoveryFailureMessage);
                return Reject(RunPersistenceReasons.InvalidCheckpoint);
            }

            recoveryGeneration++;
            if (!TryNextCheckpointId(active.Id, previous.Id, out CheckpointId recoveryId))
            {
                SetStatus(PersistenceStatusKind.Failure, active.Id, true, RecoveryFailureMessage);
                return Reject(RunPersistenceReasons.PersistenceFailed);
            }

            RunCheckpoint candidate;
            try
            {
                long departure = recoveryUtcMs > previous.DepartureUtcMs ? recoveryUtcMs : previous.DepartureUtcMs;
                candidate = new RunCheckpoint(recoveryId, recoveryGeneration, departure, previous.OfflinePolicyRevision, previous.Id,
                    previous.ContentRevision, null, previous.Run, previous.Permanent);
            }
            catch (ArgumentException)
            {
                SetStatus(PersistenceStatusKind.Failure, active.Id, true, RecoveryFailureMessage);
                return Reject(RunPersistenceReasons.InvalidCheckpoint);
            }

            SetStatus(PersistenceStatusKind.Writing, active.Id, true, "Recovering the previous factory.");
            var recovered = SafeRecoverPrevious(candidate);
            if (!recovered.Succeeded || recovered.Value != candidate.Id) return KeepRecoveryBlocked(candidate, active.Id);

            if (!TryInstall(candidate)) return KeepRecoveryBlocked(candidate, active.Id);
            if (lifecycle == LifecycleState.ReturnBlocked &&
                (Transition(LifecycleState.Loading) != null || Transition(LifecycleState.Running) != null))
            {
                SetStatus(PersistenceStatusKind.Failure, candidate.Id, true, RecoveryFailureMessage);
                return Reject(RunPersistenceReasons.ReturnBlocked);
            }

            ResetReturnBookkeeping();
            pendingRecoveryCandidate = null;
            SetStatus(PersistenceStatusKind.Recovered, candidate.Id, true, "Previous factory recovered.");
            return Succeeded(candidate.Id, candidate.Generation, false, false, false, true);
        }

        // ---------------------------------------------------------------------------------------------------
        // Save
        // ---------------------------------------------------------------------------------------------------

        private RunCommandResult<RunPersistenceOutcome> SaveCheckpoint()
        {
            SetStatus(PersistenceStatusKind.Writing, lastActiveCheckpointId, status.PreviousCheckpointAvailable, "Saving the factory.");

            if (session.Permanent == null)
            {
                SetStatus(PersistenceStatusKind.Failure, lastActiveCheckpointId, status.PreviousCheckpointAvailable, SaveFailureMessage);
                return Reject(RunPersistenceReasons.CommandInvalid);
            }

            if (!TryCaptureUtcMs(out long clockMs))
            {
                SetStatus(PersistenceStatusKind.Failure, lastActiveCheckpointId, status.PreviousCheckpointAvailable, SaveFailureMessage);
                return Reject(RunPersistenceReasons.PersistenceFailed);
            }

            if (!TryReadHistory(out RunCheckpoint active, out RunCheckpoint previous, out RunCheckpointReadResult historyFailure))
            {
                SetReadFailure(historyFailure, lastActiveCheckpointId);
                return Reject(RunPersistenceReasons.From(historyFailure.Outcome));
            }

            if (!TryNextGeneration(active, previous, out long generation) ||
                !TryNextCheckpointId(active?.Id, previous?.Id, out CheckpointId checkpointId))
            {
                SetStatus(PersistenceStatusKind.Failure, active?.Id ?? lastActiveCheckpointId, previous != null, SaveFailureMessage);
                return Reject(RunPersistenceReasons.InvalidCheckpoint);
            }

            RunCheckpoint checkpoint;
            try
            {
                long timeline = session.TimelineUtcMs;
                checkpoint = new RunCheckpoint(checkpointId, generation, clockMs > timeline ? clockMs : timeline,
                    session.Catalog.OfflinePolicy.Revision, null, session.Catalog.ContentRevision, null, session.State, session.Permanent);
            }
            catch (ArgumentException)
            {
                SetStatus(PersistenceStatusKind.Failure, active?.Id ?? lastActiveCheckpointId, previous != null, SaveFailureMessage);
                return Reject(RunPersistenceReasons.InvalidCheckpoint);
            }

            var promoted = SafePromote(checkpoint);
            if (!promoted.Succeeded || promoted.Value != checkpoint.Id)
            {
                var activeNow = SafeReadActive();
                CheckpointId? activeId = activeNow.Succeeded ? activeNow.Checkpoint.Id : active?.Id ?? lastActiveCheckpointId;
                SetStatus(PersistenceStatusKind.Failure, activeId, active != null || previous != null, SaveFailureMessage);
                return Reject(promoted.Succeeded ? RunPersistenceReasons.PersistenceFailed : RunPersistenceReasons.From(promoted.Outcome));
            }

            lastActiveCheckpointId = checkpoint.Id;
            SetStatus(PersistenceStatusKind.Ready, checkpoint.Id, active != null || previous != null, "Factory saved.");
            return Succeeded(checkpoint.Id, checkpoint.Generation, false, false, false, false);
        }

        // ---------------------------------------------------------------------------------------------------
        // Return
        // ---------------------------------------------------------------------------------------------------

        private RunCommandResult<RunPersistenceOutcome> BuildPromoteAndPublish(RunCheckpoint source, long returnUtcMs, bool usedFallback)
        {
            if (!TryReadHistory(out RunCheckpoint active, out RunCheckpoint previous, out RunCheckpointReadResult historyFailure))
            {
                return BlockReturn(historyFailure.Outcome, null);
            }

            if (!TryNextGeneration(active, previous, out long generation) ||
                !TryNextCheckpointId(source.Id, lastActiveCheckpointId, out CheckpointId resultId))
            {
                return BlockReturn(CommandOutcome.PersistenceFailed, null);
            }

            var calculated = reconciler.Reconcile(source, returnUtcMs, resultId, generation);
            if (!calculated.IsSuccess) return BlockReturn(CommandOutcome.InvalidCheckpoint, null);

            pendingSource = source;
            pendingCandidate = calculated.Value;
            pendingUsedFallback = usedFallback;
            SetStatus(PersistenceStatusKind.Writing, source.Id, status.PreviousCheckpointAvailable, "Applying the saved return state.");

            var promoted = SafePromote(calculated.Value);
            if (!promoted.Succeeded || promoted.Value != calculated.Value.Id)
            {
                var activeRead = SafeReadActive();
                return BlockReturn(promoted.Succeeded ? CommandOutcome.PersistenceFailed : promoted.Outcome,
                    activeRead.Succeeded ? activeRead.Checkpoint.Id : lastActiveCheckpointId);
            }

            return PublishDurableReturn(calculated.Value, usedFallback);
        }

        private RunCommandResult<RunPersistenceOutcome> PublishDurableReturn(RunCheckpoint durable, bool usedFallback)
        {
            if (!TryInstall(durable)) return BlockReturn(CommandOutcome.InvalidCheckpoint, durable.Id);
            if (Transition(LifecycleState.Running) != null) return BlockReturn(CommandOutcome.InvalidRequest, durable.Id);

            lastActiveCheckpointId = durable.Id;
            ResetReturnBookkeeping();
            SetStatus(PersistenceStatusKind.Ready, durable.Id, true, "Factory return applied and saved.");
            return Succeeded(durable.Id, durable.Generation, usedFallback, false, false, true);
        }

        private RunCommandResult<RunPersistenceOutcome> CompleteWithoutCheckpointOrBlock()
        {
            var probe = SafeProbe();
            if (!probe.Succeeded) return BlockReturn(CommandOutcome.PersistenceFailed, null);

            // Data or manifest files without a valid active checkpoint are a corrupt store, never a fresh start (SEC-1).
            if (probe.Value == RunCheckpointPresence.PublishedFiles) return BlockReturn(CommandOutcome.InvalidCheckpoint, null);

            if (Transition(LifecycleState.Running) != null) return BlockReturn(CommandOutcome.InvalidRequest, null);
            ResetReturnBookkeeping();
            lastActiveCheckpointId = null;
            SetStatus(PersistenceStatusKind.NoCheckpoint, null, false, "No saved factory was available; the current run was kept.");
            return Succeeded(null, null, false, false, true, false);
        }

        private RunCommandResult<RunPersistenceOutcome> BlockReturn(CommandOutcome outcome, CheckpointId? activeCheckpointId)
        {
            if (lifecycle == LifecycleState.Reconciling) Transition(LifecycleState.ReturnBlocked);
            SetStatus(PersistenceStatusKind.Failure, activeCheckpointId ?? pendingSource?.Id ?? lastActiveCheckpointId,
                status.PreviousCheckpointAvailable, ReturnFailureMessage);
            return Reject(lifecycle == LifecycleState.ReturnBlocked ? RunPersistenceReasons.ReturnBlocked : RunPersistenceReasons.From(outcome));
        }

        private RunCommandResult<RunPersistenceOutcome> KeepReturnBlocked(CheckpointId? activeCheckpointId)
        {
            SetStatus(PersistenceStatusKind.Failure, activeCheckpointId ?? lastActiveCheckpointId, status.PreviousCheckpointAvailable,
                ReturnFailureMessage);
            return Reject(RunPersistenceReasons.ReturnBlocked);
        }

        private RunCommandResult<RunPersistenceOutcome> KeepRecoveryBlocked(RunCheckpoint candidate, CheckpointId? confirmedActiveId)
        {
            pendingRecoveryCandidate = candidate;
            if (lifecycle == LifecycleState.Running) Transition(LifecycleState.ReturnBlocked);
            SetStatus(PersistenceStatusKind.Failure, confirmedActiveId ?? status.ActiveCheckpointId ?? lastActiveCheckpointId, true,
                RecoveryFailureMessage);
            return Reject(lifecycle == LifecycleState.ReturnBlocked ? RunPersistenceReasons.ReturnBlocked : RunPersistenceReasons.PersistenceFailed);
        }

        private RunCommandResult<RunPersistenceOutcome> RetryPendingRecovery()
        {
            RunCheckpoint candidate = pendingRecoveryCandidate;
            if (candidate == null) return Reject(RunPersistenceReasons.ReturnBlocked);

            var recovered = SafeRecoverPrevious(candidate);
            if (!recovered.Succeeded || recovered.Value != candidate.Id) return KeepRecoveryBlocked(candidate, status.ActiveCheckpointId);

            // The candidate is installed only after RecoverPrevious confirmed this exact identity.
            if (!TryInstall(candidate)) return KeepRecoveryBlocked(candidate, status.ActiveCheckpointId);
            if (Transition(LifecycleState.Loading) != null || Transition(LifecycleState.Running) != null)
                return KeepRecoveryBlocked(candidate, status.ActiveCheckpointId);

            lastActiveCheckpointId = candidate.Id;
            ResetReturnBookkeeping();
            pendingRecoveryCandidate = null;
            SetStatus(PersistenceStatusKind.Recovered, candidate.Id, true, "Previous factory recovered.");
            return Succeeded(candidate.Id, candidate.Generation, false, false, false, true);
        }

        private RetrySource ResolveRetrySource(RunCheckpoint active)
        {
            if (pendingSource != null) return RetrySource.Success(pendingSource);
            if (departureCheckpointId.HasValue)
            {
                var departureRead = SafeRead(departureCheckpointId.Value);
                return departureRead.Succeeded ? RetrySource.Success(departureRead.Checkpoint) : RetrySource.Failure();
            }

            pendingUsedFallback = true;
            return RetrySource.Success(active);
        }

        /// <summary>
        /// Installs a durable generation as the live run and settles once (load rule 2): the session is re-anchored at
        /// the checkpoint's departure, and the settle rescales every machine's progress to the content's current cycle.
        /// </summary>
        private bool TryInstall(RunCheckpoint durable)
        {
            if (!ReferenceEquals(durable.Run.Catalog, session.Catalog)) return false;
            try
            {
                session.ReplaceLoaded(durable.Run, durable.Permanent, durable.DepartureUtcMs);
            }
            catch (ArgumentException)
            {
                return false;
            }

            session.BeginCommand();
            return session.EndCommand(true, true).IsSuccess;
        }

        // ---------------------------------------------------------------------------------------------------
        // Lifecycle and bookkeeping
        // ---------------------------------------------------------------------------------------------------

        private string Admit(ApplicationOperation operation)
        {
            if (operation == ApplicationOperation.StatusQuery) return null;
            if (operation == ApplicationOperation.RecoverPreviousCheckpoint)
            {
                return lifecycle == LifecycleState.Running || lifecycle == LifecycleState.ReturnBlocked
                    ? null
                    : RunPersistenceReasons.LifecycleNoOp;
            }

            if (lifecycle == LifecycleState.ReturnBlocked)
            {
                return operation == ApplicationOperation.RetryReturn || operation == ApplicationOperation.ReloadAuthoritativeGeneration
                    ? null
                    : RunPersistenceReasons.ReturnBlocked;
            }

            bool admitted;
            switch (lifecycle)
            {
                case LifecycleState.Running:
                    admitted = operation == ApplicationOperation.Save || operation == ApplicationOperation.RecordSuspension;
                    break;
                case LifecycleState.Bootstrapping:
                case LifecycleState.Loading:
                case LifecycleState.Suspended:
                    admitted = operation == ApplicationOperation.ResumeAndReconcile;
                    break;
                default:
                    admitted = false;
                    break;
            }

            return admitted ? null : RunPersistenceReasons.LifecycleNoOp;
        }

        private string Transition(LifecycleState next)
        {
            if (lifecycle == next) return RunPersistenceReasons.LifecycleNoOp;
            if (!CanTransition(lifecycle, next)) return RunPersistenceReasons.CommandInvalid;
            lifecycle = next;
            session.SetLifecycleBlocked(next != LifecycleState.Running);
            return null;
        }

        private static bool CanTransition(LifecycleState current, LifecycleState next)
        {
            switch (current)
            {
                case LifecycleState.Bootstrapping: return next == LifecycleState.Loading;
                case LifecycleState.Loading: return next == LifecycleState.Running || next == LifecycleState.Reconciling;
                case LifecycleState.Running: return next == LifecycleState.Checkpointing || next == LifecycleState.ReturnBlocked;
                case LifecycleState.Checkpointing: return next == LifecycleState.Running || next == LifecycleState.Suspended;
                case LifecycleState.Suspended: return next == LifecycleState.Reconciling;
                case LifecycleState.Reconciling: return next == LifecycleState.Running || next == LifecycleState.ReturnBlocked;
                case LifecycleState.ReturnBlocked: return next == LifecycleState.Reconciling || next == LifecycleState.Loading;
                default: return false;
            }
        }

        private void ResetReturnBookkeeping()
        {
            departureCheckpointId = null;
            departureOutcome = null;
            suspensionAttempted = false;
            ClearPendingReturn();
        }

        private void ClearPendingReturn()
        {
            pendingSource = null;
            pendingCandidate = null;
            pendingUsedFallback = false;
            capturedReturnUtcMs = 0;
            hasCapturedReturnUtc = false;
        }

        private bool TryCaptureUtcMs(out long utcMs)
        {
            utcMs = 0;
            try
            {
                utcMs = clock.GetUtcNow().ToUniversalTime().ToUnixTimeMilliseconds();
                return ExactBound.Contains(utcMs);
            }
            catch (Exception exception) when (exception is ArgumentException || exception is InvalidOperationException ||
                                               exception is OverflowException)
            {
                return false;
            }
        }

        private bool TryReadHistory(out RunCheckpoint active, out RunCheckpoint previous, out RunCheckpointReadResult failure)
        {
            active = null;
            previous = null;
            failure = null;

            var activeRead = SafeReadActive();
            if (activeRead.Succeeded) active = activeRead.Checkpoint;
            else if (activeRead.Outcome != CommandOutcome.NoCheckpoint)
            {
                failure = activeRead;
                return false;
            }

            // An unreadable previous generation (a slot torn by an interrupted write) must not block every later save:
            // the manifest guarantees it is older than the active one, so the next generation only needs the active one.
            // A storage failure is still a failure.
            var previousRead = SafeReadPrevious();
            if (previousRead.Succeeded) previous = previousRead.Checkpoint;
            else if (previousRead.Outcome != CommandOutcome.NoCheckpoint && previousRead.Outcome != CommandOutcome.InvalidCheckpoint)
            {
                failure = previousRead;
                return false;
            }

            if (active == null && previous != null)
            {
                failure = RunCheckpointReadResult.Rejected(CommandOutcome.InvalidCheckpoint, "Checkpoint history is invalid.");
                return false;
            }

            lastActiveCheckpointId = active?.Id;
            status = new RunPersistenceStatus(active == null ? PersistenceStatusKind.NoCheckpoint : PersistenceStatusKind.Ready,
                active?.Id, previous != null, active == null ? "No saved factory is available." : "Saved factory is ready.");
            return true;
        }

        private static bool TryNextGeneration(RunCheckpoint active, RunCheckpoint previous, out long generation)
        {
            generation = 0;
            if (active == null && previous == null)
            {
                generation = 1;
                return true;
            }

            long highest = Math.Max(active?.Generation ?? -1, previous?.Generation ?? -1);
            if (highest == long.MaxValue) return false;
            generation = highest + 1;
            return true;
        }

        private bool TryNextCheckpointId(CheckpointId? reservedOne, CheckpointId? reservedTwo, out CheckpointId checkpointId)
        {
            checkpointId = default;
            CommandResult<CheckpointId> generated;
            try
            {
                generated = checkpointIds.Next();
            }
            catch (Exception exception) when (IsExpectedInfrastructureFailure(exception))
            {
                return false;
            }

            if (generated == null || !generated.Succeeded) return false;
            try
            {
                _ = generated.Value.Value;
            }
            catch (InvalidOperationException)
            {
                return false;
            }

            if ((reservedOne.HasValue && generated.Value == reservedOne.Value) ||
                (reservedTwo.HasValue && generated.Value == reservedTwo.Value))
            {
                return false;
            }

            checkpointId = generated.Value;
            return true;
        }

        private bool ReadPreviousAvailability() => SafeReadPrevious().Succeeded;

        private void InitializeStatus()
        {
            var active = SafeReadActive();
            if (active.Succeeded)
            {
                lastActiveCheckpointId = active.Checkpoint.Id;
                SetStatus(PersistenceStatusKind.Ready, active.Checkpoint.Id, SafeReadPrevious().Succeeded, "Saved factory is ready.");
                return;
            }

            SetStatus(active.Outcome == CommandOutcome.NoCheckpoint ? PersistenceStatusKind.NoCheckpoint : PersistenceStatusKind.Failure,
                null, false, SafeMessage(active.Outcome, "Checkpoint status is unavailable."));
        }

        private void SetReadFailure(RunCheckpointReadResult failure, CheckpointId? fallbackId)
        {
            SetStatus(failure.Outcome == CommandOutcome.NoCheckpoint ? PersistenceStatusKind.NoCheckpoint : PersistenceStatusKind.Failure,
                fallbackId, status.PreviousCheckpointAvailable, SafeMessage(failure.Outcome, StorageMessage));
        }

        private void SetStatus(PersistenceStatusKind kind, CheckpointId? activeId, bool previousAvailable, string message)
        {
            status = new RunPersistenceStatus(kind, activeId, previousAvailable, message);
            if (activeId.HasValue || kind == PersistenceStatusKind.NoCheckpoint) lastActiveCheckpointId = activeId;
        }

        // Identity only: a durable re-promotion of the same ID and generation makes the store compare the bytes.
        private static bool SameIdentity(RunCheckpoint left, RunCheckpoint right) =>
            left != null && right != null && left.Id == right.Id && left.Generation == right.Generation;

        private static string SafeMessage(CommandOutcome outcome, string fallback)
        {
            switch (outcome)
            {
                case CommandOutcome.NoCheckpoint: return "No saved factory is available.";
                case CommandOutcome.UnsupportedSaveVersion: return "The saved factory uses an unsupported version.";
                case CommandOutcome.InvalidCheckpoint: return "The saved factory is invalid.";
                case CommandOutcome.PersistenceFailed: return StorageMessage;
                default: return fallback;
            }
        }

        // ---------------------------------------------------------------------------------------------------
        // Store access: an infrastructure exception becomes a typed rejection.
        // ---------------------------------------------------------------------------------------------------

        private RunCheckpointReadResult SafeReadActive() => SafeRead(() => store.ReadActive());

        private RunCheckpointReadResult SafeReadPrevious() => SafeRead(() => store.ReadPrevious());

        private RunCheckpointReadResult SafeRead(CheckpointId id) => SafeRead(() => store.Read(id));

        private static RunCheckpointReadResult SafeRead(Func<RunCheckpointReadResult> read)
        {
            try
            {
                return read() ?? RunCheckpointReadResult.Rejected(CommandOutcome.PersistenceFailed, StorageMessage);
            }
            catch (Exception exception) when (IsExpectedInfrastructureFailure(exception))
            {
                return RunCheckpointReadResult.Rejected(CommandOutcome.PersistenceFailed, StorageMessage);
            }
        }

        private CommandResult<CheckpointId> SafePromote(RunCheckpoint checkpoint)
        {
            try
            {
                return store.Promote(checkpoint) ?? CommandResult<CheckpointId>.Rejected(CommandOutcome.PersistenceFailed, StorageMessage);
            }
            catch (Exception exception) when (IsExpectedInfrastructureFailure(exception))
            {
                return CommandResult<CheckpointId>.Rejected(CommandOutcome.PersistenceFailed, StorageMessage);
            }
        }

        private CommandResult<CheckpointId> SafeRecoverPrevious(RunCheckpoint checkpoint)
        {
            try
            {
                return store.RecoverPrevious(checkpoint) ??
                       CommandResult<CheckpointId>.Rejected(CommandOutcome.PersistenceFailed, StorageMessage);
            }
            catch (Exception exception) when (IsExpectedInfrastructureFailure(exception))
            {
                return CommandResult<CheckpointId>.Rejected(CommandOutcome.PersistenceFailed, StorageMessage);
            }
        }

        private CommandResult<RunCheckpointPresence> SafeProbe()
        {
            try
            {
                return store.ProbeFiles() ??
                       CommandResult<RunCheckpointPresence>.Rejected(CommandOutcome.PersistenceFailed, StorageMessage);
            }
            catch (Exception exception) when (IsExpectedInfrastructureFailure(exception))
            {
                return CommandResult<RunCheckpointPresence>.Rejected(CommandOutcome.PersistenceFailed, StorageMessage);
            }
        }

        private static bool IsExpectedInfrastructureFailure(Exception exception)
        {
            return exception is IOException || exception is UnauthorizedAccessException || exception is SecurityException ||
                   exception is InvalidOperationException || exception is NotSupportedException ||
                   exception is PlatformNotSupportedException || exception is ArgumentException || exception is OverflowException;
        }

        private static RunCommandResult<RunPersistenceOutcome> Succeeded(CheckpointId? checkpointId, long? generation, bool usedFallback,
            bool wasNoOp, bool noCheckpoint, bool stateReplaced) =>
            RunCommandResult.Success(new RunPersistenceOutcome(checkpointId, generation, usedFallback, wasNoOp, noCheckpoint, stateReplaced));

        private static RunCommandResult<RunPersistenceOutcome> Reject(string reason) =>
            RunCommandResult.Reject<RunPersistenceOutcome>(reason);

        private sealed class RetrySource
        {
            private RetrySource(RunCheckpoint checkpoint) => Checkpoint = checkpoint;

            public RunCheckpoint Checkpoint { get; }
            public bool Succeeded => Checkpoint != null;

            public static RetrySource Success(RunCheckpoint checkpoint) => new RetrySource(checkpoint);
            public static RetrySource Failure() => new RetrySource(null);
        }
    }
}
