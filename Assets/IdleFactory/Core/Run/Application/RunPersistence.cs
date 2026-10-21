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
    /// The persistence commands of the run (T138, contracts/run-application.md): <c>SaveNow</c>,
    /// <c>RecordSuspension</c>, <c>ResumeAndReconcile</c>, <c>RetryReturn</c>, <c>ReloadAuthoritativeGeneration</c> and
    /// <c>RecoverPreviousCheckpoint</c> keep the meanings of the retired Feature 001 persistence commands over Save V2:
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
    /// bootstrap; the V1 import and a Start new run settle in their own path, in <see cref="V1Import.Build"/> and
    /// <c>FreshRunStart</c>, once before the candidate is promoted, so this install settle is a no-op on a fixed point there); (4) while a prestige transition is pending, <see cref="SaveNow"/> is rejected with
    /// <c>transition.pending</c>, <see cref="Autosave"/> skips and nothing else is written: a suspension and a return
    /// first re-read the durable active generation (R09) and publish a verified promotion, otherwise the old run stays
    /// without any credit and the transition remains retryable; <see cref="CommitPendingTransition"/> /
    /// <see cref="RetryPendingTransition"/> write the candidate, verify it and only then publish it (T105); the new run's
    /// departure is the confirmed instant <c>max(ConfirmedUtcMs, active departure)</c> even when the commit succeeds
    /// later, so a crash after a late commit credits the gap to the new run, bounded by the offline cap (T102 handoff); (5) a return
    /// credits offline time through <see cref="OfflineRunReturnReconciler"/> (T105), exactly once by
    /// <c>reconciledFromCheckpointId</c>; (6) "no checkpoint" on a return means no data or manifest file exists. A lone
    /// pending file is an unpromoted candidate that the protocol discards, and the run stays fresh. The V1 import is
    /// decided BEFORE <see cref="ResumeAndReconcile"/>, which never imports: <see cref="ColdStart"/> is the one cold-start
    /// entry point (valid V2 active, else files without a valid active checkpoint recover and NEVER import (SEC-1), else V1
    /// import when <see cref="IRunCheckpointStore.ProbeFiles"/> reports <see cref="RunCheckpointPresence.NoFiles"/> or
    /// <see cref="RunCheckpointPresence.PendingOnly"/> (OPS-2), else a fresh run), and it reuses this class's promotion,
    /// install and lifecycle machinery; (7) an install always uses a fresh read of the durable generation; (8) a failed
    /// import leaves the lifecycle in <see cref="LifecycleState.Loading"/> with <see cref="BlockedImportCode"/> set, publishes
    /// nothing and admits only <see cref="ColdStart"/> (retry: the same candidate bytes after a failed promotion, otherwise
    /// a recomputation with a newly captured return instant) and <see cref="StartNewRunAfterBlockedImport"/> (OD-3).
    /// </summary>
    public sealed partial class RunPersistence
    {
        private const string SaveFailureMessage = "The factory could not be saved.";
        private const string ReturnFailureMessage = "The return could not be completed. Retry or reload the saved factory.";
        private const string LoadFailureMessage = "The saved factory could not be loaded.";
        private const string RecoveryFailureMessage = "The previous factory could not be recovered.";
        private const string StorageMessage = SafeRunStore.StorageMessage;

        /// <summary>
        /// The minimum interval between two <see cref="Autosave"/> writes when the catalog does not set
        /// <c>runStart.liveGapThresholdMs</c> (programmatic test catalogs; the shipped content sets 60,000). FR-033 ties the
        /// interval to the FR-032 live-gap threshold, so a catalog value always wins over this fallback.
        /// </summary>
        internal const long DefaultMinimumAutosaveIntervalMs = 60_000;

        private readonly RunSession session;
        private readonly SafeRunStore store;
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
        private bool pendingRecoveryOverDamagedActive;
        private bool pendingUsedFallback;
        private long capturedReturnUtcMs;
        private bool hasCapturedReturnUtc;
        private RunOfflineSummary pendingSummary;

        // T306 (FR-033, SEC-5): the clock instant, in Unix milliseconds, of the latest durable checkpoint written by a save or a
        // published prestige. Only the throttled Autosave reads it; every other write bypasses the guard and refreshes it.
        private long? lastCheckpointUtcMs;

        // V1 import (T106). importBlocked: the cold start chose the import and it did not complete; the lifecycle stays
        // Loading and the code is the failed rule. A candidate is kept only after a failed promotion, so a retry in the
        // same session re-promotes the same bytes.
        private bool importBlocked;
        private bool coldStartInterrupted;
        private RunCheckpoint pendingDiscardCandidate;

        // True once any run was live in this process (the lifecycle reached Running). The discard of a damaged sole save is
        // only for a ReturnBlocked reached from the cold start: afterwards the in-memory run is newer than any durable state.
        private bool everRunning;
        private string blockedImportCode;
        private RunCheckpoint pendingImportCandidate;
        private RunCheckpoint pendingFreshRunCandidate;

        // The durable prestige candidate: built once per pending transition, so a retry re-promotes the same bytes under
        // the same checkpoint identity.
        private RunCheckpoint transitionCheckpoint;
        private PrestigeCandidate transitionCheckpointFor;

        // Set just before a promotion of the pending candidate is issued; cleared when a read verifies that nothing became
        // durable, or when the transition is published or cancelled. While it is set, the promotion may be durable although
        // the store reported a failure (manifest visible, flush or read back threw), so a cancel must verify first.
        private PrestigeCandidate promotionIssuedFor;

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
            this.store = new SafeRunStore(store ?? throw new ArgumentNullException(nameof(store)));
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

        /// <summary>
        /// The failed rule of a blocked V1 import (one of <see cref="V1ImportCodes"/>), or null when no import is blocked
        /// (T106). While it is set, the player's recovery actions are <see cref="ColdStart"/> (Retry) and
        /// <see cref="StartNewRunAfterBlockedImport"/> (OD-3; the UI is T118).
        /// </summary>
        public string BlockedImportCode => importBlocked ? blockedImportCode : null;

        /// <summary>
        /// True while the cold start was interrupted by a transient V2 read or probe failure (T106): nothing was decided, the
        /// lifecycle is <see cref="LifecycleState.Loading"/> and the only recovery is <see cref="ColdStart"/> again.
        /// </summary>
        public bool ColdStartRetryRequired => coldStartInterrupted;

        /// <summary>
        /// True when <see cref="DiscardDamagedSaveAndRestart"/> is available (owner decision 2026-10-01): the session is
        /// <see cref="LifecycleState.ReturnBlocked"/>, the V2 directory holds data or manifest files, and neither an active
        /// nor a previous generation is valid. Reads the store, so call it when showing the recovery actions, not per frame.
        /// </summary>
        public bool CanDiscardDamagedSave => CanDiscardCore();

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
        /// The periodic write (the desktop focus-loss safety checkpoint): the same as <see cref="SaveNow"/>, except that it
        /// skips silently (a successful no-op) while a prestige transition is pending or the lifecycle is not running, and
        /// while the latest checkpoint is younger than <see cref="MinimumAutosaveIntervalMs"/> (T306, FR-033; the outcome then
        /// reports <see cref="RunPersistenceOutcome.AutosaveThrottled"/>). Only this command is throttled: <see cref="SaveNow"/>,
        /// <see cref="RecordSuspension()"/> (quit) and a published prestige always write and refresh the interval (SEC-5). A clock
        /// that cannot be read or has moved backwards never throttles, so the guard can only reduce writes, never block a save.
        /// </summary>
        public RunCommandResult<RunPersistenceOutcome> Autosave()
        {
            if (session.PendingTransition != null || lifecycle != LifecycleState.Running)
                return Succeeded(null, null, false, true, false, false);
            if (IsWithinAutosaveInterval())
                return RunCommandResult.Success(Succeeded(null, null, false, true, false, false).Value.AsAutosaveThrottled());
            return SaveNow();
        }

        /// <summary>
        /// The one live-gap threshold of the run, in milliseconds (FR-032, FR-033, T337): the catalog's
        /// <c>runStart.liveGapThresholdMs</c>, or 60,000 when the catalog does not set it. The host's live-gap check (a wall delta
        /// strictly above it is an offline return) and the <see cref="Autosave"/> minimum interval both read this accessor, so the two
        /// cannot drift apart.
        /// </summary>
        public long LiveGapThresholdMs => session.Catalog.RunStart.LiveGapThresholdMs ?? DefaultMinimumAutosaveIntervalMs;

        /// <summary>The <see cref="Autosave"/> minimum interval (FR-033): by definition the live-gap threshold, <see cref="LiveGapThresholdMs"/>.</summary>
        internal long MinimumAutosaveIntervalMs => LiveGapThresholdMs;

        private bool IsWithinAutosaveInterval()
        {
            if (!lastCheckpointUtcMs.HasValue || !TryCaptureUtcMs(out long nowMs)) return false;
            long elapsed = nowMs - lastCheckpointUtcMs.Value;
            return elapsed >= 0 && elapsed < MinimumAutosaveIntervalMs;
        }

        /// <summary>
        /// Writes the departure checkpoint and suspends. A reported departure suspends the application even if
        /// storage fails (the return then uses the FR-037 fallback). Repeated while suspended, it is a no-op.
        /// </summary>
        public RunCommandResult<RunPersistenceOutcome> RecordSuspension() => RecordSuspensionCore(null);

        /// <summary>
        /// The departure of a large live gap (T337, FR-032, F5-R10): the same command as <see cref="RecordSuspension()"/> (same admission,
        /// same lifecycle, same pending-prestige handling and same checkpoint write), except that the checkpoint's departure is the
        /// previous frame, <paramref name="atUtcMs"/> (Unix milliseconds), instead of the current clock read. The departure is
        /// <c>max(current, min(atUtcMs, clock))</c>, where <c>current</c> is the floor every departure has: never before the run timeline
        /// (the end of the simulated time, load rule 4) and never before the active generation's departure. So the departure is never
        /// later than the clock read of this call (SEC-10), except where the timeline itself is ahead of the clock (a development
        /// time-scale run, D-I): then it is the timeline and the offline credit of the return is 0. The checkpoint and the autosave
        /// interval are stamped from the real clock read, not from <paramref name="atUtcMs"/> (so a safety checkpoint inside the gap is
        /// throttled). The caller follows it with <see cref="ResumeAndReconcile"/> (the host's gap path); nothing else calls it.
        /// </summary>
        public RunCommandResult<RunPersistenceOutcome> RecordSuspension(long atUtcMs) => RecordSuspensionCore(atUtcMs);

        private RunCommandResult<RunPersistenceOutcome> RecordSuspensionCore(long? departureAtUtcMs)
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
                ? SuspendWithPendingTransition(departureAtUtcMs)
                : SaveCheckpoint(departureAtUtcMs);
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
            // A blocked import must not turn into a fresh run through the "no checkpoint" path (T106).
            if (importBlocked || coldStartInterrupted) return Reject(RunPersistenceReasons.LifecycleNoOp);
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
                sourceRead = store.Read(departureCheckpointId.Value);
                pendingUsedFallback = false;
            }
            else
            {
                sourceRead = store.ReadActive();
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

            if (pendingRecoveryCandidate != null && pendingRecoveryOverDamagedActive) return RetryPendingRecoveryChecked();

            var activeRead = store.ReadActive();
            if (!activeRead.Succeeded)
            {
                SetReadFailure(activeRead, lastActiveCheckpointId);
                return Reject(RunPersistenceReasons.ReturnBlocked);
            }

            RunCheckpoint active = activeRead.Checkpoint;
            if (pendingRecoveryCandidate != null) return RetryPendingRecovery();

            if (pendingCandidate != null && SameIdentity(active, pendingCandidate))
            {
                var confirmation = store.Promote(pendingCandidate);
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
                    var confirmation = store.Promote(rebuilt);
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

            var activeRead = store.ReadActive();
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

            var confirmation = store.Promote(active);
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
            if (pendingRecoveryCandidate != null) return pendingRecoveryOverDamagedActive ? RetryPendingRecoveryChecked() : RetryReturn();

            // Installing the previous generation would drop the pending candidate silently: decide the transition first.
            if (session.PendingTransition != null) return Reject(RunPersistenceReasons.TransitionPending);

            string rejected = Admit(ApplicationOperation.RecoverPreviousCheckpoint);
            if (rejected != null)
            {
                CheckpointId? activeId = status.ActiveCheckpointId ?? lastActiveCheckpointId;
                SetStatus(lifecycle == LifecycleState.ReturnBlocked ? PersistenceStatusKind.Failure : PersistenceStatusKind.Ready,
                    activeId, false, RecoveryFailureMessage);
                return Reject(rejected);
            }

            var previousRead = store.ReadPrevious();
            if (!previousRead.Succeeded)
            {
                CheckpointId? activeId = status.ActiveCheckpointId ?? lastActiveCheckpointId;
                bool none = previousRead.Outcome == CommandOutcome.NoCheckpoint;
                SetStatus(lifecycle == LifecycleState.ReturnBlocked ? PersistenceStatusKind.Failure
                    : none && activeId.HasValue ? PersistenceStatusKind.Ready
                    : none ? PersistenceStatusKind.NoCheckpoint : PersistenceStatusKind.Failure, activeId, false, RecoveryFailureMessage);
                return Reject(RunPersistenceReasons.From(previousRead.Outcome));
            }

            var activeRead = store.ReadActive();
            long activeGeneration = -1;
            CheckpointId? durableId = null;
            bool damagedActive = false;
            if (activeRead.Succeeded)
            {
                activeGeneration = activeRead.Checkpoint.Generation;
                durableId = activeRead.Checkpoint.Id;
            }
            else if (CanRecoverOverDamagedActive(activeRead))
            {
                // Cold-start case 2 (contracts/save-v2-and-v1-import.md): the manifest and the previous generation are intact, only the
                // active data is unreadable. The store's recovery never reads the active data, so the previous generation is promoted
                // over it through the same protocol (data write, alternate manifest, old manifest selected until the new one verifies).
                CommandResult<long> floor = store.NextSupersedeGeneration();
                if (!floor.Succeeded)
                {
                    SetReadFailure(activeRead, lastActiveCheckpointId);
                    return Reject(RunPersistenceReasons.From(floor.Outcome));
                }

                damagedActive = true;
                activeGeneration = floor.Value - 1;
            }
            else
            {
                SetReadFailure(activeRead, lastActiveCheckpointId);
                return Reject(RunPersistenceReasons.From(activeRead.Outcome));
            }

            RunCheckpoint previous = previousRead.Checkpoint;
            if (!ReferenceEquals(previous.Run.Catalog, session.Catalog))
            {
                SetStatus(PersistenceStatusKind.Failure, durableId, true, RecoveryFailureMessage);
                return Reject(RunPersistenceReasons.InvalidCheckpoint);
            }

            if (!TryCaptureUtcMs(out long recoveryUtcMs))
            {
                SetStatus(PersistenceStatusKind.Failure, durableId, true, RecoveryFailureMessage);
                return Reject(RunPersistenceReasons.PersistenceFailed);
            }

            long recoveryGeneration = activeGeneration > previous.Generation ? activeGeneration : previous.Generation;
            if (recoveryGeneration == long.MaxValue)
            {
                SetStatus(PersistenceStatusKind.Failure, durableId, true, RecoveryFailureMessage);
                return Reject(RunPersistenceReasons.InvalidCheckpoint);
            }

            recoveryGeneration++;
            if (!TryNextCheckpointId(durableId, previous.Id, out CheckpointId recoveryId))
            {
                SetStatus(PersistenceStatusKind.Failure, durableId, true, RecoveryFailureMessage);
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
                SetStatus(PersistenceStatusKind.Failure, durableId, true, RecoveryFailureMessage);
                return Reject(RunPersistenceReasons.InvalidCheckpoint);
            }

            pendingRecoveryOverDamagedActive = damagedActive;
            SetStatus(PersistenceStatusKind.Writing, durableId, true, "Recovering the previous factory.");
            var recovered = store.RecoverPrevious(candidate);
            if (!recovered.Succeeded || recovered.Value != candidate.Id) return KeepRecoveryBlocked(candidate, durableId);

            if (!TryInstall(candidate)) return KeepRecoveryBlocked(candidate, durableId);
            if (lifecycle == LifecycleState.ReturnBlocked &&
                (Transition(LifecycleState.Loading) != null || Transition(LifecycleState.Running) != null))
            {
                SetStatus(PersistenceStatusKind.Failure, candidate.Id, true, RecoveryFailureMessage);
                return Reject(RunPersistenceReasons.ReturnBlocked);
            }

            lastActiveCheckpointId = candidate.Id;
            ResetReturnBookkeeping();
            pendingRecoveryCandidate = null;
            pendingRecoveryOverDamagedActive = false;
            SetStatus(PersistenceStatusKind.Recovered, candidate.Id, true, "Previous factory recovered.");
            return Succeeded(candidate.Id, candidate.Generation, false, false, false, true);
        }

        // The recovery over an unreadable active generation is only for a ReturnBlocked reached from the cold start (afterwards the
        // in-memory run is newer than any durable state) and only for plainly damaged active data: a storage failure, another
        // build's save or a missing generation never qualifies. The caller has read the previous generation successfully.
        private bool CanRecoverOverDamagedActive(RunCheckpointReadResult activeRead) =>
            lifecycle == LifecycleState.ReturnBlocked && !everRunning && !activeRead.Succeeded &&
            activeRead.Outcome == CommandOutcome.InvalidCheckpoint;

        // ---------------------------------------------------------------------------------------------------
        // Save
        // ---------------------------------------------------------------------------------------------------

        private RunCommandResult<RunPersistenceOutcome> SaveCheckpoint(long? departureAtUtcMs = null)
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

            // A durable successor of the live run (a promoted prestige the session has not published) must never be written
            // over by the old run: the transition has happened, publish it with RetryPendingTransition.
            if (active != null && IsSuccessorOfLiveRun(active))
            {
                SetStatus(PersistenceStatusKind.Failure, active.Id, previous != null, SaveFailureMessage);
                return Reject(RunPersistenceReasons.TransitionCommitted);
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
                // Never before the timeline (load rule 4) and never before the active generation's departure. A live-gap departure
                // (T337) is the previous frame instead of the clock, bounded by both (SEC-10).
                long departure = departureAtUtcMs.HasValue
                    ? Math.Max(Math.Min(departureAtUtcMs.Value, clockMs), session.TimelineUtcMs)
                    : Math.Max(clockMs, session.TimelineUtcMs);
                if (active != null && active.DepartureUtcMs > departure) departure = active.DepartureUtcMs;
                checkpoint = new RunCheckpoint(checkpointId, generation, departure,
                    session.Catalog.OfflinePolicy.Revision, null, session.Catalog.ContentRevision, null, session.State, session.Permanent);
            }
            catch (ArgumentException)
            {
                SetStatus(PersistenceStatusKind.Failure, active?.Id ?? lastActiveCheckpointId, previous != null, SaveFailureMessage);
                return Reject(RunPersistenceReasons.InvalidCheckpoint);
            }

            var promoted = store.Promote(checkpoint);
            if (!promoted.Succeeded || promoted.Value != checkpoint.Id)
            {
                var activeNow = store.ReadActive();
                CheckpointId? activeId = activeNow.Succeeded ? activeNow.Checkpoint.Id : active?.Id ?? lastActiveCheckpointId;
                SetStatus(PersistenceStatusKind.Failure, activeId, active != null || previous != null, SaveFailureMessage);
                return Reject(promoted.Succeeded ? RunPersistenceReasons.PersistenceFailed : RunPersistenceReasons.From(promoted.Outcome));
            }

            lastActiveCheckpointId = checkpoint.Id;
            lastCheckpointUtcMs = clockMs;
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

            var promoted = store.Promote(candidate);
            if (!promoted.Succeeded || promoted.Value != candidate.Id)
            {
                var activeRead = store.ReadActive();
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
            var fresh = store.Read(source.Id);
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
            bool imported = durable.Import != null;
            SetStatus(PersistenceStatusKind.Ready, durable.Id, true, imported
                ? "Development save imported and saved."
                : summary != null && summary.CreditForfeited
                    ? "Factory return applied and saved; the offline credit was forfeited."
                    : "Factory return applied and saved.");
            return Succeeded(durable.Id, durable.Generation, usedFallback, false, false, true, summary, false, imported);
        }

        private RunCommandResult<RunPersistenceOutcome> CompleteWithoutCheckpointOrBlock()
        {
            var probe = store.Probe();
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

        // A retry of a recovery over a damaged active generation: the gate is evaluated again (still the cold-start block, the
        // same pending candidate) and the store confirms the exact identity, so a promotion that landed is not repeated.
        private RunCommandResult<RunPersistenceOutcome> RetryPendingRecoveryChecked()
        {
            if (lifecycle != LifecycleState.ReturnBlocked || everRunning)
            {
                pendingRecoveryCandidate = null;
                pendingRecoveryOverDamagedActive = false;
                return Reject(RunPersistenceReasons.ReturnBlocked);
            }

            return RetryPendingRecovery();
        }

        private RunCommandResult<RunPersistenceOutcome> RetryPendingRecovery()
        {
            RunCheckpoint candidate = pendingRecoveryCandidate;
            if (candidate == null) return Reject(RunPersistenceReasons.ReturnBlocked);

            var recovered = store.RecoverPrevious(candidate);
            if (!recovered.Succeeded || recovered.Value != candidate.Id) return KeepRecoveryBlocked(candidate, status.ActiveCheckpointId);

            // The candidate is installed only after RecoverPrevious confirmed this exact identity.
            if (!TryInstall(candidate)) return KeepRecoveryBlocked(candidate, status.ActiveCheckpointId);
            if (Transition(LifecycleState.Loading) != null || Transition(LifecycleState.Running) != null)
                return KeepRecoveryBlocked(candidate, status.ActiveCheckpointId);

            lastActiveCheckpointId = candidate.Id;
            ResetReturnBookkeeping();
            pendingRecoveryCandidate = null;
            pendingRecoveryOverDamagedActive = false;
            SetStatus(PersistenceStatusKind.Recovered, candidate.Id, true, "Previous factory recovered.");
            return Succeeded(candidate.Id, candidate.Generation, false, false, false, true);
        }

        private RetrySource ResolveRetrySource(RunCheckpoint active)
        {
            if (pendingSource != null) return RetrySource.Success(pendingSource);
            if (departureCheckpointId.HasValue)
            {
                var departureRead = store.Read(departureCheckpointId.Value);
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
            var read = store.Read(promoted.Id);
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
            if (lifecycle == LifecycleState.ReturnBlocked) pendingDiscardCandidate = null;
            if (next == LifecycleState.Running) everRunning = true;
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

            var activeRead = store.ReadActive();
            if (activeRead.Succeeded) active = activeRead.Checkpoint;
            else if (activeRead.Outcome != CommandOutcome.NoCheckpoint)
            {
                failure = activeRead;
                return false;
            }

            // An unreadable previous generation (a slot torn by an interrupted write) must not block every later save:
            // the manifest guarantees it is older than the active one, so the next generation only needs the active one.
            // A storage failure is still a failure.
            var previousRead = store.ReadPrevious();
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
            catch (Exception exception) when (SafeRunStore.IsExpectedInfrastructureFailure(exception))
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

        private bool ReadPreviousAvailability() => store.ReadPrevious().Succeeded;

        private void InitializeStatus()
        {
            var active = store.ReadActive();
            if (active.Succeeded)
            {
                lastActiveCheckpointId = active.Checkpoint.Id;
                SetStatus(PersistenceStatusKind.Ready, active.Checkpoint.Id, store.ReadPrevious().Succeeded, "Saved factory is ready.");
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
                case CommandOutcome.UnresolvedContent: return "The saved factory comes from a different version of the game.";
                case CommandOutcome.InvalidCheckpoint: return "The saved factory is invalid.";
                case CommandOutcome.PersistenceFailed: return StorageMessage;
                default: return fallback;
            }
        }

        private static RunCommandResult<RunPersistenceOutcome> Succeeded(CheckpointId? checkpointId, long? generation, bool usedFallback,
            bool wasNoOp, bool noCheckpoint, bool stateReplaced, RunOfflineSummary summary = null, bool transitionPending = false,
            bool imported = false) =>
            RunCommandResult.Success(new RunPersistenceOutcome(checkpointId, generation, usedFallback, wasNoOp, noCheckpoint, stateReplaced,
                summary, transitionPending, imported));

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
