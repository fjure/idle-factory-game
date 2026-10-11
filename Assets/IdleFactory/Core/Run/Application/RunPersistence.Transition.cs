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
    public sealed partial class RunPersistence
    {
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

            promotionIssuedFor = candidate;
            var promoted = store.Promote(checkpoint);
            if (!promoted.Succeeded || promoted.Value != checkpoint.Id)
            {
                var activeNow = store.ReadActive();
                if (activeNow.Succeeded && !IsSuccessorOfLiveRun(activeNow.Checkpoint) && activeNow.Checkpoint.Id != checkpoint.Id)
                    promotionIssuedFor = null; // verified: the failed promotion left nothing durable
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
        /// cannot be read the cancellation fails closed (<c>persistence.failed</c>, the retry stays possible) once a promotion
        /// of the candidate was issued, because that promotion may be durable although the store reported a failure; a
        /// cancel of a candidate whose promotion was never issued stays permissive, so a dead store cannot trap the
        /// player behind a transition that never reached it. A cancel also rebinds the session anchor to the clock, so the
        /// old run's timers are measured against real time (the pending state paused the timeline).
        /// </summary>
        public RunCommandResult<bool> CancelPendingTransition()
        {
            PrestigeCandidate candidate = session.PendingTransition;
            if (candidate == null) return RunCommandResult.Success(false);
            var activeRead = store.ReadActive();
            if (activeRead.Succeeded && IsSuccessorOfLiveRun(activeRead.Checkpoint))
                return RunCommandResult.Reject<bool>(RunPersistenceReasons.TransitionCommitted);
            bool verified = activeRead.Succeeded || activeRead.Outcome == CommandOutcome.NoCheckpoint;
            if (!verified && ReferenceEquals(promotionIssuedFor, candidate))
                return RunCommandResult.Reject<bool>(RunPersistenceReasons.PersistenceFailed);

            session.SetPendingTransition(null);
            transitionCheckpoint = null;
            transitionCheckpointFor = null;
            promotionIssuedFor = null;
            if (TryCaptureUtcMs(out long now)) session.RebindAnchor(now);
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
            if (TryCaptureUtcMs(out long publishedAtMs)) lastCheckpointUtcMs = publishedAtMs;
            transitionCheckpoint = null;
            transitionCheckpointFor = null;
            promotionIssuedFor = null;
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
            var activeRead = store.ReadActive();
            if (activeRead.Succeeded && IsSuccessorOfLiveRun(activeRead.Checkpoint))
            {
                lastActiveCheckpointId = activeRead.Checkpoint.Id;
                if (!TryInstall(activeRead.Checkpoint)) return Reject(RunPersistenceReasons.PersistenceFailed);
                transitionCheckpoint = null;
                transitionCheckpointFor = null;
                promotionIssuedFor = null;
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
            var activeRead = store.ReadActive();
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

            // The old run is not credited, but the pending state paused its timeline: rebind the anchor to the captured return
            // instant so its timers (contract deadline, Item effects, Overdrive) are measured against real time and expire at the
            // first advance. Neither the old state nor the candidate is touched.
            if (hasCapturedReturnUtc) session.RebindAnchor(capturedReturnUtcMs);
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
    }
}
