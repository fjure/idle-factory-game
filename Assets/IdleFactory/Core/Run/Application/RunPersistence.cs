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
            bool noCheckpoint, bool stateReplaced, RunOfflineSummary offlineSummary = null, bool transitionPending = false)
        {
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
    /// the same captured return instant builds the same candidate, and it must return state that is not shared with
    /// the live session. <see cref="RunPersistence"/> never installs a candidate's own objects: it installs a fresh
    /// read of the durable generation, so a failed install cannot mutate a candidate that a retry re-promotes.
    /// </summary>
    public interface IRunReturnReconciler
    {
        RunCommandResult<RunCheckpoint> Reconcile(RunCheckpoint source, long returnUtcMs, CheckpointId resultId, long resultGeneration);
    }

    /// <summary>
    /// Optional companion of an <see cref="IRunReturnReconciler"/> (T105): the summary of the last reconcile and the last
    /// completions its simulation recorded, which <see cref="RunPersistence"/> hands to the session after the install.
    /// </summary>
    public interface IRunReturnSummarySource
    {
        /// <summary>The summary of the last successful reconcile, or null.</summary>
        RunOfflineSummary LastSummary { get; }

        /// <summary>The last completion the offline advance saw of a machine (RatesSnapshot, OD-9).</summary>
        bool TryGetLastCompletion(Guid instanceId, out long units, out long batchFactor, out int limit);
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

    /// <summary>
    /// The offline return of the run (T105, R05, R09, FR-025): the candidate checkpoint of a return is the source state
    /// advanced by <see cref="OfflineSegmenter"/> from the PERSISTED departure <c>d</c> to the captured return instant
    /// <c>r</c>, under a new identity with <c>reconciledFromCheckpointId</c> = the source (the exactly-once key, so
    /// it is never applied to one absence twice) and the departure moved to <c>r</c>.
    ///
    /// Rules: (1) the credit is <c>max(0, r - d)</c>: a departure in the future (a forged or stale value) credits
    /// nothing, leaves the state as it was, and the departure is never rewritten earlier; (2) a simulator rejection
    /// (<c>value.limit</c>) forfeits the credit, the state stays as before the absence and the departure advances to
    /// <c>r</c>, so a retry or restart never attempts the credit again (owner decision 2026-10-01; the summary
    /// carries <see cref="RunOfflineSummary.ValueLimitCode"/>); (3) the reconcile mutates the source state, so
    /// <see cref="RunPersistence"/> hands it a private fresh read of the source on every call; with the same source
    /// bytes and the same <c>r</c> the candidate bytes are identical, which the store's idempotent promotion relies on;
    /// (4) the segmenter's simulator rescales every machine to its effective cycle before it produces anything
    /// (load rule 2), and a return that credits nothing is rescaled by the settle of the install.
    /// </summary>
    public sealed class OfflineRunReturnReconciler : IRunReturnReconciler, IRunReturnSummarySource
    {
        private readonly RunCatalog catalog;
        private readonly OfflineSegmenter segmenter;

        public OfflineRunReturnReconciler(RunCatalog catalog)
        {
            this.catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
            segmenter = new OfflineSegmenter(catalog);
        }

        public RunOfflineSummary LastSummary { get; private set; }

        public bool TryGetLastCompletion(Guid instanceId, out long units, out long batchFactor, out int limit) =>
            segmenter.Simulator.TryGetLastCompletion(instanceId, out units, out batchFactor, out limit);

        public RunCommandResult<RunCheckpoint> Reconcile(RunCheckpoint source, long returnUtcMs, CheckpointId resultId, long resultGeneration)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            LastSummary = null;
            if (!ReferenceEquals(source.Run.Catalog, catalog)) return RunCommandResult.Reject<RunCheckpoint>(RunPersistenceReasons.InvalidCheckpoint);

            RunOfflineSummary summary = segmenter.Reconcile(source.Run, source.Permanent, source.DepartureUtcMs, returnUtcMs);
            long departure;
            switch (summary.Outcome)
            {
                case RunOfflineOutcome.Applied:
                    departure = returnUtcMs;
                    break;
                case RunOfflineOutcome.BackwardClock:
                    // Zero credit, nothing processed; the departure is never rewritten earlier.
                    departure = source.DepartureUtcMs;
                    break;
                case RunOfflineOutcome.SimulationRejected when summary.CreditForfeited:
                    departure = returnUtcMs > source.DepartureUtcMs ? returnUtcMs : source.DepartureUtcMs;
                    break;
                case RunOfflineOutcome.SimulationRejected:
                    return RunCommandResult.Reject<RunCheckpoint>(RunPersistenceReasons.InvalidCheckpoint);
                default:
                    return RunCommandResult.Reject<RunCheckpoint>(RunPersistenceReasons.PersistenceFailed);
            }

            try
            {
                var candidate = new RunCheckpoint(resultId, resultGeneration, departure, source.OfflinePolicyRevision, source.Id,
                    source.ContentRevision, null, source.Run, source.Permanent);
                LastSummary = summary;
                return RunCommandResult.Success(candidate);
            }
            catch (ArgumentException)
            {
                return RunCommandResult.Reject<RunCheckpoint>(RunPersistenceReasons.InvalidCheckpoint);
            }
        }
    }

    /// <summary>
    /// A reconciler that credits no offline time (T138): the candidate is the source state under a new identity,
    /// <c>reconciledFromCheckpointId</c> = the source, and the departure moved forward to the return instant (never
    /// back). T105 replaced it as the default by <see cref="OfflineRunReturnReconciler"/>; it stays for tests and
    /// callers that explicitly want no credit.
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
    /// (the R03 rescale of progress when content durations were retuned) and the owner of the settle after a load, a
    /// return and a prestige promotion (run creation settles in <see cref="PrestigeTransition.TryBuild"/> and the
    /// bootstrap, import in T106); (4) while a prestige transition is pending, <see cref="SaveNow"/> is rejected with
    /// <c>transition.pending</c>, <see cref="Autosave"/> skips and nothing else is written: a suspension and a return
    /// first re-read the durable active generation (R09) and publish a verified promotion, otherwise the old run stays
    /// without any credit and the transition remains retryable; <see cref="CommitPendingTransition"/> /
    /// <see cref="RetryPendingTransition"/> write the candidate, verify it and only then publish it (T105); (5) a return
    /// credits offline time through <see cref="OfflineRunReturnReconciler"/> (T105), exactly once by
    /// <c>reconciledFromCheckpointId</c>; (6) "no checkpoint" on a return means no data or manifest file exists. A lone
    /// pending file is an unpromoted candidate that the protocol discards, and the run stays fresh. V1 import must be
    /// decided by the bootstrap from <see cref="IRunCheckpointStore.ProbeFiles"/> BEFORE <see cref="ResumeAndReconcile"/>
    /// is called (T106); this class never imports; (7) an install always uses a fresh read of the durable generation.
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
        private readonly IRunReturnSummarySource summarySource;

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
        private RunOfflineSummary pendingSummary;

        // The durable prestige candidate: built once per pending transition, so a retry re-promotes the same bytes under
        // the same checkpoint identity.
        private RunCheckpoint transitionCheckpoint;
        private PrestigeCandidate transitionCheckpointFor;

        public RunPersistence(RunSession session, IRunCheckpointStore store, IUtcClock clock)
            : this(session, store, clock, new GuidCheckpointIdSource(), DefaultReconciler(session))
        {
        }

        private static IRunReturnReconciler DefaultReconciler(RunSession session) =>
            new OfflineRunReturnReconciler((session ?? throw new ArgumentNullException(nameof(session))).Catalog);

        public RunPersistence(RunSession session, IRunCheckpointStore store, IUtcClock clock, ICheckpointIdSource checkpointIds,
            IRunReturnReconciler reconciler)
        {
            this.session = session ?? throw new ArgumentNullException(nameof(session));
            this.store = store ?? throw new ArgumentNullException(nameof(store));
            this.clock = clock ?? throw new ArgumentNullException(nameof(clock));
            this.checkpointIds = checkpointIds ?? throw new ArgumentNullException(nameof(checkpointIds));
            this.reconciler = reconciler ?? throw new ArgumentNullException(nameof(reconciler));
            summarySource = reconciler as IRunReturnSummarySource;
            session.TransitionCoordinator = new TransitionCoordinator(this);
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
                ? SuspendWithPendingTransition()
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

            if (session.PendingTransition != null) return ReturnWithPendingTransition();

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

            if (session.PendingTransition != null && pendingRecoveryCandidate == null)
            {
                if (!hasCapturedReturnUtc) hasCapturedReturnUtc = TryCaptureUtcMs(out capturedReturnUtcMs);
                string reconcilingAgain = Transition(LifecycleState.Reconciling);
                if (reconcilingAgain != null) return KeepReturnBlocked(lastActiveCheckpointId);
                return ReturnWithPendingTransition();
            }

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
                if (TryReconcile(source, capturedReturnUtcMs, active.Id, active.Generation, out RunCheckpoint rebuilt, out _) &&
                    SameIdentity(active, rebuilt))
                {
                    pendingCandidate = rebuilt;
                    var confirmation = SafePromote(rebuilt);
                    if (!confirmation.Succeeded || confirmation.Value != active.Id) return KeepReturnBlocked(source.Id);
                    lastActiveCheckpointId = active.Id;
                    string retryTransition = Transition(LifecycleState.Reconciling);
                    if (retryTransition != null) return KeepReturnBlocked(active.Id);
                    return PublishDurableReturn(rebuilt, pendingUsedFallback);
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
                // Never before the timeline (load rule 4) and never before the active generation's departure.
                long departure = Math.Max(clockMs, session.TimelineUtcMs);
                if (active != null && active.DepartureUtcMs > departure) departure = active.DepartureUtcMs;
                checkpoint = new RunCheckpoint(checkpointId, generation, departure,
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

            if (!TryReconcile(source, returnUtcMs, resultId, generation, out RunCheckpoint candidate, out CommandOutcome failure))
                return BlockReturn(failure, null);

            pendingSource = source;
            pendingCandidate = candidate;
            pendingUsedFallback = usedFallback;
            SetStatus(PersistenceStatusKind.Writing, source.Id, status.PreviousCheckpointAvailable, "Applying the saved return state.");

            var promoted = SafePromote(candidate);
            if (!promoted.Succeeded || promoted.Value != candidate.Id)
            {
                var activeRead = SafeReadActive();
                return BlockReturn(promoted.Succeeded ? CommandOutcome.PersistenceFailed : promoted.Outcome,
                    activeRead.Succeeded ? activeRead.Checkpoint.Id : lastActiveCheckpointId);
            }

            return PublishDurableReturn(candidate, usedFallback);
        }

        /// <summary>
        /// Builds the return candidate from a PRIVATE fresh read of the source: the reconcile mutates the state it is
        /// given and is not idempotent, so a retry must never run on objects a previous attempt already advanced
        /// (T103 handoff 1). With the same source bytes and the same captured return instant the candidate is identical.
        /// </summary>
        private bool TryReconcile(RunCheckpoint source, long returnUtcMs, CheckpointId resultId, long resultGeneration,
            out RunCheckpoint candidate, out CommandOutcome failure)
        {
            candidate = null;
            failure = CommandOutcome.InvalidCheckpoint;
            var fresh = SafeRead(source.Id);
            if (!fresh.Succeeded)
            {
                failure = fresh.Outcome;
                return false;
            }

            if (!ReferenceEquals(fresh.Checkpoint.Run.Catalog, session.Catalog) || fresh.Checkpoint.Generation != source.Generation)
                return false;
            RunCommandResult<RunCheckpoint> calculated = reconciler.Reconcile(fresh.Checkpoint, returnUtcMs, resultId, resultGeneration);
            if (!calculated.IsSuccess) return false;
            candidate = calculated.Value;
            pendingSummary = summarySource?.LastSummary;
            return true;
        }

        private RunCommandResult<RunPersistenceOutcome> PublishDurableReturn(RunCheckpoint durable, bool usedFallback)
        {
            RunOfflineSummary summary = pendingSummary;
            if (!TryInstall(durable)) return BlockReturn(CommandOutcome.InvalidCheckpoint, durable.Id);
            if (Transition(LifecycleState.Running) != null) return BlockReturn(CommandOutcome.InvalidRequest, durable.Id);

            if (summary != null) session.AdoptLastCompletions(summarySource);
            lastActiveCheckpointId = durable.Id;
            ResetReturnBookkeeping();
            SetStatus(PersistenceStatusKind.Ready, durable.Id, true, summary != null && summary.CreditForfeited
                ? "Factory return applied and saved; the offline credit was forfeited."
                : "Factory return applied and saved.");
            return Succeeded(durable.Id, durable.Generation, usedFallback, false, false, true, summary);
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
        private bool TryInstall(RunCheckpoint promoted)
        {
            // The live session must never own (and a failed settle never mutate) an object that a retry compares or
            // re-promotes: install a fresh read of the durable generation instead of the candidate's own state.
            var read = SafeRead(promoted.Id);
            if (!read.Succeeded) return false;
            RunCheckpoint durable = read.Checkpoint;
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
        // Prestige transition (R09, T105)
        // ---------------------------------------------------------------------------------------------------

        /// <summary>
        /// The durable promotion of the pending prestige: re-reads the durable active generation; if that is already the
        /// confirmed run's successor (<c>previousRunId</c> equals the live <c>runId</c>) the transition happened and is
        /// published WITHOUT a second award; otherwise the candidate checkpoint (built once, same bytes and identity on
        /// every attempt, departure <c>max(ConfirmedUtcMs, active departure)</c>) is promoted, read back and verified, and
        /// only then is a fresh read of it installed as the live run (the candidate's own objects are never reused). A
        /// failure keeps the old run, the pending candidate and the retry available; nothing is awarded twice however
        /// many times this is repeated, and a restart simply loads whichever generation is durable.
        /// </summary>
        public RunCommandResult<RunPersistenceOutcome> CommitPendingTransition()
        {
            string rejected = Admit(ApplicationOperation.Save);
            if (rejected != null) return Reject(rejected);
            PrestigeCandidate candidate = session.PendingTransition;
            if (candidate == null) return Reject(RunPersistenceReasons.CommandInvalid);

            SetStatus(PersistenceStatusKind.Writing, lastActiveCheckpointId, status.PreviousCheckpointAvailable, "Saving the next run.");
            if (!TryReadHistory(out RunCheckpoint active, out RunCheckpoint previous, out RunCheckpointReadResult historyFailure))
            {
                SetReadFailure(historyFailure, lastActiveCheckpointId);
                return Reject(RunPersistenceReasons.From(historyFailure.Outcome));
            }

            if (active != null && IsSuccessorOfLiveRun(active)) return PublishTransition(active);

            RunCheckpoint checkpoint = transitionCheckpoint;
            bool reusable = checkpoint != null && ReferenceEquals(transitionCheckpointFor, candidate) &&
                            checkpoint.Generation > Math.Max(active?.Generation ?? -1, previous?.Generation ?? -1) &&
                            (active == null || active.Id != checkpoint.Id) && (previous == null || previous.Id != checkpoint.Id);
            if (!reusable)
            {
                if (!TryNextGeneration(active, previous, out long generation) ||
                    !TryNextCheckpointId(active?.Id, previous?.Id, out CheckpointId checkpointId))
                {
                    SetStatus(PersistenceStatusKind.Failure, active?.Id ?? lastActiveCheckpointId, previous != null, SaveFailureMessage);
                    return Reject(RunPersistenceReasons.InvalidCheckpoint);
                }

                try
                {
                    long departure = candidate.ConfirmedUtcMs;
                    if (active != null && active.DepartureUtcMs > departure) departure = active.DepartureUtcMs;
                    checkpoint = new RunCheckpoint(checkpointId, generation, departure, session.Catalog.OfflinePolicy.Revision, null,
                        session.Catalog.ContentRevision, null, candidate.Run, candidate.Permanent);
                }
                catch (ArgumentException)
                {
                    SetStatus(PersistenceStatusKind.Failure, active?.Id ?? lastActiveCheckpointId, previous != null, SaveFailureMessage);
                    return Reject(RunPersistenceReasons.InvalidCheckpoint);
                }

                transitionCheckpoint = checkpoint;
                transitionCheckpointFor = candidate;
            }

            var promoted = SafePromote(checkpoint);
            if (!promoted.Succeeded || promoted.Value != checkpoint.Id)
            {
                var activeNow = SafeReadActive();
                CheckpointId? activeId = activeNow.Succeeded ? activeNow.Checkpoint.Id : active?.Id ?? lastActiveCheckpointId;
                SetStatus(PersistenceStatusKind.Failure, activeId, active != null || previous != null, SaveFailureMessage);
                return Reject(promoted.Succeeded ? RunPersistenceReasons.PersistenceFailed : RunPersistenceReasons.From(promoted.Outcome));
            }

            return PublishTransition(checkpoint);
        }

        /// <summary>The same as <see cref="CommitPendingTransition"/>: an idempotent re-check and re-promotion (R09).</summary>
        public RunCommandResult<RunPersistenceOutcome> RetryPendingTransition() => CommitPendingTransition();

        /// <summary>
        /// Drops the pending prestige and keeps the old run. A promotion that is already durable cannot be taken back
        /// (<c>transition.committed</c>: retry it to publish it). A candidate checkpoint that was written but never
        /// promoted is an unpublished pending file; the generation protocol discards it at the next promotion, so
        /// nothing has to be deleted and the next save writes the old run under a new identity. When the durable state
        /// cannot be read the cancellation still succeeds: either outcome (old run, no award, or new run, one award) is
        /// consistent, and a player must not be stuck behind a dead store.
        /// </summary>
        public RunCommandResult<bool> CancelPendingTransition()
        {
            if (session.PendingTransition == null) return RunCommandResult.Success(false);
            var activeRead = SafeReadActive();
            if (activeRead.Succeeded && IsSuccessorOfLiveRun(activeRead.Checkpoint))
                return RunCommandResult.Reject<bool>(RunPersistenceReasons.TransitionCommitted);

            session.SetPendingTransition(null);
            transitionCheckpoint = null;
            transitionCheckpointFor = null;
            return RunCommandResult.Success(true);
        }

        private bool IsSuccessorOfLiveRun(RunCheckpoint active) =>
            active.Run.PreviousRunId.HasValue && active.Run.PreviousRunId.Value == session.State.RunId &&
            ReferenceEquals(active.Run.Catalog, session.Catalog);

        /// <summary>Installs the verified durable next run (a fresh read of it, then one settle) and releases the transition.</summary>
        private RunCommandResult<RunPersistenceOutcome> PublishTransition(RunCheckpoint durable)
        {
            bool installed = TryInstall(durable);
            if (!installed)
            {
                SetStatus(PersistenceStatusKind.Failure, durable.Id, status.PreviousCheckpointAvailable, SaveFailureMessage);
                if (session.PendingTransition != null) return Reject(RunPersistenceReasons.PersistenceFailed);

                // The install replaced the run and then failed its settle: only a reload of the durable generation can recover.
                Transition(LifecycleState.ReturnBlocked);
                return Reject(RunPersistenceReasons.ReturnBlocked);
            }

            lastActiveCheckpointId = durable.Id;
            transitionCheckpoint = null;
            transitionCheckpointFor = null;
            SetStatus(PersistenceStatusKind.Ready, durable.Id, ReadPreviousAvailability(), "Next run saved.");
            return Succeeded(durable.Id, durable.Generation, false, false, false, true);
        }

        /// <summary>
        /// A suspension with a pending prestige re-reads the durable active generation first: a verified promotion is
        /// published and the departure checkpoint of the new run is written; otherwise nothing is written (the old run
        /// stays and the transition remains retryable).
        /// </summary>
        private RunCommandResult<RunPersistenceOutcome> SuspendWithPendingTransition()
        {
            var activeRead = SafeReadActive();
            if (activeRead.Succeeded && IsSuccessorOfLiveRun(activeRead.Checkpoint))
            {
                lastActiveCheckpointId = activeRead.Checkpoint.Id;
                if (!TryInstall(activeRead.Checkpoint)) return Reject(RunPersistenceReasons.PersistenceFailed);
                transitionCheckpoint = null;
                transitionCheckpointFor = null;
                return SaveCheckpoint();
            }

            return Reject(RunPersistenceReasons.TransitionPending);
        }

        /// <summary>
        /// A return (cold or retried) with a pending prestige. The durable active generation is read first: when it is the
        /// promotion of the pending candidate the durable new run is the source of the normal return, credited from ITS
        /// departure (never the old run's), exactly once by <c>reconciledFromCheckpointId</c>; otherwise the old run
        /// stays, no offline time is credited, nothing is written and the transition remains retryable.
        /// </summary>
        private RunCommandResult<RunPersistenceOutcome> ReturnWithPendingTransition()
        {
            var activeRead = SafeReadActive();
            if (!activeRead.Succeeded && activeRead.Outcome != CommandOutcome.NoCheckpoint)
            {
                SetReadFailure(activeRead, lastActiveCheckpointId);
                return BlockReturn(activeRead.Outcome, null);
            }

            RunCheckpoint active = activeRead.Succeeded ? activeRead.Checkpoint : null;
            if (active != null && IsSuccessorOfLiveRun(active))
            {
                if (!hasCapturedReturnUtc) hasCapturedReturnUtc = TryCaptureUtcMs(out capturedReturnUtcMs);
                if (!hasCapturedReturnUtc) return BlockReturn(CommandOutcome.PersistenceFailed, active.Id);
                lastActiveCheckpointId = active.Id;
                pendingUsedFallback = true;
                return BuildPromoteAndPublish(active, capturedReturnUtcMs, true);
            }

            if (Transition(LifecycleState.Running) != null) return BlockReturn(CommandOutcome.InvalidRequest, active?.Id);
            ResetReturnBookkeeping();
            SetStatus(active == null ? PersistenceStatusKind.NoCheckpoint : PersistenceStatusKind.Ready, active?.Id,
                status.PreviousCheckpointAvailable, "The return kept the current run; the next run is still waiting to be saved.");
            return Succeeded(active?.Id, active?.Generation, false, true, false, false, null, true);
        }

        private sealed class TransitionCoordinator : IPendingTransitionCoordinator
        {
            private readonly RunPersistence owner;

            public TransitionCoordinator(RunPersistence owner) => this.owner = owner;

            public RunCommandResult<Guid> Promote()
            {
                RunCommandResult<RunPersistenceOutcome> committed = owner.CommitPendingTransition();
                return committed.IsSuccess
                    ? RunCommandResult.Success(owner.session.State.RunId)
                    : RunCommandResult.Reject<Guid>(committed.ReasonCode);
            }

            public RunCommandResult<bool> Cancel() => owner.CancelPendingTransition();
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
            pendingSummary = null;
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
            bool wasNoOp, bool noCheckpoint, bool stateReplaced, RunOfflineSummary summary = null, bool transitionPending = false) =>
            RunCommandResult.Success(new RunPersistenceOutcome(checkpointId, generation, usedFallback, wasNoOp, noCheckpoint, stateReplaced,
                summary, transitionPending));

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
