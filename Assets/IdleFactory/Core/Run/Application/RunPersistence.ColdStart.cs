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
        // Cold start and V1 import (T106, FR-028, R10)
        // ---------------------------------------------------------------------------------------------------

        private const string ImportFailureMessage = "The saved development factory could not be imported.";
        private const string ImportPersistenceMessage = "The imported factory could not be saved.";

        /// <summary>
        /// The one cold-start entry point (contracts/save-v2-and-v1-import.md, Cold-start source selection), to be called
        /// instead of <see cref="ResumeAndReconcile"/> at start-up and again as the Retry of a blocked import:
        /// (1) a valid V2 active checkpoint is loaded with its offline return; (2) V2 checkpoint files without a valid active
        /// checkpoint, or a V2 store that cannot be read, take the recovery path and NEVER import (SEC-1); (3) with
        /// <see cref="RunCheckpointPresence.NoFiles"/> or <see cref="RunCheckpointPresence.PendingOnly"/> (a lone pending
        /// file is no checkpoint file, OPS-2) and a V1 active checkpoint, the V1 is imported; (4) otherwise a fresh run
        /// stays. <paramref name="legacySource"/> may be null (no V1 access wired): the import is then never offered.
        ///
        /// Import: the return instant <c>r</c> is captured once per attempt, <see cref="V1Import.Build"/> builds and settles
        /// the complete candidate, which is promoted through the shared protocol and only then installed (a fresh read, so
        /// the candidate's objects are never the live ones). A failure publishes nothing, leaves V1 untouched, blocks the
        /// start with the static code of the failed rule (the rejection's reason code and <see cref="BlockedImportCode"/>)
        /// and keeps the lifecycle in <see cref="LifecycleState.Loading"/>. A failed promotion keeps the candidate: the
        /// retry re-promotes the same bytes (same checkpoint ID, same <c>r</c>). Every other failure builds nothing durable,
        /// so the retry recomputes with a newly captured <c>r</c> (exactly-once: nothing was published). After a restart
        /// the promoted V2 is selected by rule 1 and V1 is never credited twice.
        /// </summary>
        public RunCommandResult<RunPersistenceOutcome> ColdStart(ILegacyCheckpointSource legacySource)
        {
            bool retrying = importBlocked || coldStartInterrupted;
            if (!retrying && lifecycle != LifecycleState.Bootstrapping) return Reject(RunPersistenceReasons.LifecycleNoOp);
            if (retrying && lifecycle != LifecycleState.Loading) return Reject(RunPersistenceReasons.LifecycleNoOp);

            // The same session retries a failed promotion with the same candidate bytes.
            if (pendingImportCandidate != null) return RetryImportPromotion();

            var activeRead = store.ReadActive();
            // A transient read or probe failure must not be re-decided by the generic return, which would see "no checkpoint"
            // and start an unpersisted fresh run, silently skipping V1. The start blocks and ColdStart is retried in full.
            if (!activeRead.Succeeded && activeRead.Outcome == CommandOutcome.PersistenceFailed) return BlockColdStart();
            if (activeRead.Succeeded || activeRead.Outcome != CommandOutcome.NoCheckpoint) return ResumeFromColdStart();

            var probe = store.Probe();
            if (!probe.Succeeded) return BlockColdStart();
            if (probe.Value == RunCheckpointPresence.PublishedFiles || legacySource == null) return ResumeFromColdStart();

            var import = new V1Import(session.Catalog, legacySource);
            V1SourceRead v1 = import.ReadSource();
            if (v1.State == V1SourceState.Absent) return ResumeFromColdStart();

            if (lifecycle == LifecycleState.Bootstrapping)
            {
                string loading = Transition(LifecycleState.Loading);
                if (loading != null) return Reject(loading);
            }

            if (v1.State == V1SourceState.Unusable) return BlockImport(V1ImportCodes.Invalid, ImportFailureMessage);

            // r is captured once per attempt. A failed clock is a code of its own, decided by Build after the cheap rules.
            long? returnUtcMs = TryCaptureUtcMs(out long captured) ? captured : (long?)null;
            if (!TryReadHistory(out RunCheckpoint active, out RunCheckpoint previous, out _) || active != null || previous != null)
            {
                return BlockImport(V1ImportCodes.Persistence, ImportPersistenceMessage);
            }

            if (!TryNextGeneration(null, null, out long generation) || !TryNextCheckpointId(null, null, out CheckpointId candidateId) ||
                !TryNextCheckpointId(candidateId, null, out CheckpointId runId))
            {
                return BlockImport(V1ImportCodes.Persistence, ImportPersistenceMessage);
            }

            V1ImportResult built = import.Build(v1.Checkpoint, returnUtcMs, candidateId, generation, runId.Uuid);
            if (!built.IsSuccess) return BlockImport(built.Code, ImportFailureMessage);

            pendingImportCandidate = built.Candidate;
            return PromoteImportCandidate(pendingImportCandidate);
        }

        /// <summary>
        /// The confirmed Start new run after a blocked import (OD-3, QA-8): builds a fresh-run Save V2 candidate from the
        /// content, promotes it before it is published, and installs it. The V1 files stay untouched. Because a promoted V2
        /// wins the next cold start (rule 1), V1 is not offered again, with no extra marker. Admitted only while an import
        /// is blocked; the confirmation dialog is the UI's (T118). A failed promotion keeps the candidate, so the retry
        /// re-promotes the same bytes. When a durable V2 checkpoint turns out to exist already (a promotion that was
        /// reported as failed but landed), it is selected instead, which never credits the V1 interval twice.
        /// </summary>
        public RunCommandResult<RunPersistenceOutcome> StartNewRunAfterBlockedImport()
        {
            if (!importBlocked || lifecycle != LifecycleState.Loading) return Reject(RunPersistenceReasons.LifecycleNoOp);

            var activeRead = store.ReadActive();
            if (activeRead.Succeeded) return ResumeFromColdStart(true);
            if (activeRead.Outcome == CommandOutcome.PersistenceFailed) return Reject(RunPersistenceReasons.PersistenceFailed);
            if (activeRead.Outcome != CommandOutcome.NoCheckpoint) return ResumeFromColdStart(); // damaged V2 files: recovery, never import (SEC-1)

            if (pendingFreshRunCandidate == null)
            {
                if (!TryCaptureUtcMs(out long now)) return Reject(RunPersistenceReasons.PersistenceFailed);
                if (!TryReadHistory(out RunCheckpoint active, out RunCheckpoint previous, out _) || active != null || previous != null)
                    return Reject(RunPersistenceReasons.PersistenceFailed);
                if (!TryNextGeneration(null, null, out long generation) || !TryNextCheckpointId(null, null, out CheckpointId checkpointId) ||
                    !TryNextCheckpointId(checkpointId, null, out CheckpointId runId))
                    return Reject(RunPersistenceReasons.PersistenceFailed);

                string rejected = FreshRunStart.TryBuild(session.Catalog, runId.Uuid, now, out RunState run, out PermanentState permanent);
                if (rejected != null) return Reject(rejected);
                try
                {
                    pendingFreshRunCandidate = new RunCheckpoint(checkpointId, generation, now, session.Catalog.OfflinePolicy.Revision, null,
                        session.Catalog.ContentRevision, null, run, permanent);
                }
                catch (ArgumentException)
                {
                    return Reject(RunPersistenceReasons.InvalidCheckpoint);
                }
            }

            pendingImportCandidate = null;
            RunCheckpoint candidate = pendingFreshRunCandidate;
            SetStatus(PersistenceStatusKind.Writing, null, false, "Saving the new run.");
            var promoted = store.Promote(candidate);
            if (!promoted.Succeeded || promoted.Value != candidate.Id)
            {
                SetStatus(PersistenceStatusKind.Failure, null, false, SaveFailureMessage);
                return Reject(promoted.Succeeded ? RunPersistenceReasons.PersistenceFailed : RunPersistenceReasons.From(promoted.Outcome));
            }

            pendingFreshRunCandidate = null;
            return PublishPromotedColdStart(candidate);
        }

        private bool CanDiscardCore()
        {
            if (lifecycle != LifecycleState.ReturnBlocked || session.PendingTransition != null || everRunning) return false;
            var probe = store.Probe();
            if (!probe.Succeeded || probe.Value != RunCheckpointPresence.PublishedFiles) return false;

            // Only plainly damaged files qualify. An unsupported version (another build's save), a storage failure or anything
            // else may hide a valid save and is never overwritten. "No checkpoint" with published files is a data slot that
            // was written before any manifest existed: the manifest is never deleted, so it was never published (same as OPS-2).
            var active = store.ReadActive();
            if (active.Succeeded ||
                (active.Outcome != CommandOutcome.InvalidCheckpoint && active.Outcome != CommandOutcome.NoCheckpoint)) return false;
            var previous = store.ReadPrevious();
            return !previous.Succeeded &&
                   (previous.Outcome == CommandOutcome.NoCheckpoint || previous.Outcome == CommandOutcome.InvalidCheckpoint);
        }

        /// <summary>
        /// The confirmed recovery of a damaged sole save (owner decision 2026-10-01). Available only when the session is
        /// <see cref="LifecycleState.ReturnBlocked"/> because the V2 directory holds files but no valid active AND no valid
        /// previous generation exists (<see cref="CanDiscardDamagedSave"/>): then no V2 progress can be lost or duplicated, and
        /// SEC-1 still holds whenever any valid V2 generation exists (any other state is rejected with
        /// <see cref="RunPersistenceReasons.DiscardUnavailable"/>). With a V1 source it re-imports V1 (the same import, once,
        /// with a newly captured return instant); with a null source, or when no V1 save is present, it promotes a fresh run.
        /// An import that fails a rule is rejected with that rule code and the state stays as it was, so the player can call
        /// this again with a null source to start fresh. V1 is never written. The candidate is promoted over the damaged
        /// files through the shared protocol (generation 1, which the protocol accepts when no manifest selects a
        /// generation); a failed promotion keeps the candidate, so a retry re-promotes the same bytes, and a restart after
        /// success selects the new V2.
        /// </summary>
        public RunCommandResult<RunPersistenceOutcome> DiscardDamagedSaveAndRestart(ILegacyCheckpointSource legacySource)
        {
            // The gate is evaluated on every call, retries included.
            if (lifecycle != LifecycleState.ReturnBlocked || session.PendingTransition != null || everRunning)
            {
                pendingDiscardCandidate = null;
                return Reject(RunPersistenceReasons.DiscardUnavailable);
            }

            bool landed = false;
            if (pendingDiscardCandidate != null)
            {
                // A promotion that was reported as failed may have landed: the same identity is then confirmed idempotently.
                var again = store.ReadActive();
                landed = again.Succeeded && SameIdentity(again.Checkpoint, pendingDiscardCandidate);
            }

            if (!landed && !CanDiscardCore())
            {
                pendingDiscardCandidate = null;
                return Reject(RunPersistenceReasons.DiscardUnavailable);
            }

            // An intact manifest over unreadable data is superseded (T141); no selectable manifest takes the ordinary promotion
            // at generation 1. The store re-checks every precondition itself (SEC-1), so this only picks the generation.
            bool supersede = false;
            long generation = 1;
            if (!landed)
            {
                CommandResult<long> floor = store.NextSupersedeGeneration();
                if (floor.Succeeded)
                {
                    supersede = true;
                    generation = floor.Value;
                }
                else if (floor.Outcome != CommandOutcome.NoCheckpoint && floor.Outcome != CommandOutcome.InvalidCheckpoint)
                {
                    return Reject(RunPersistenceReasons.From(floor.Outcome));
                }

                if (pendingDiscardCandidate != null && pendingDiscardCandidate.Generation != generation) pendingDiscardCandidate = null;
            }

            if (pendingDiscardCandidate == null)
            {
                if (!TryNextCheckpointId(null, null, out CheckpointId candidateId) || !TryNextCheckpointId(candidateId, null, out CheckpointId runId))
                    return Reject(RunPersistenceReasons.PersistenceFailed);
                bool hasClock = TryCaptureUtcMs(out long now);

                var import = legacySource == null ? null : new V1Import(session.Catalog, legacySource);
                V1SourceRead v1 = import?.ReadSource();
                if (v1 != null && v1.State == V1SourceState.Unusable) return Reject(V1ImportCodes.Invalid);
                if (v1 != null && v1.State == V1SourceState.Available)
                {
                    V1ImportResult built = import.Build(v1.Checkpoint, hasClock ? now : (long?)null, candidateId, generation, runId.Uuid);
                    if (!built.IsSuccess) return Reject(built.Code);
                    pendingDiscardCandidate = built.Candidate;
                }
                else
                {
                    if (!hasClock) return Reject(RunPersistenceReasons.PersistenceFailed);
                    string rejected = FreshRunStart.TryBuild(session.Catalog, runId.Uuid, now, out RunState run, out PermanentState permanent);
                    if (rejected != null) return Reject(rejected);
                    try
                    {
                        pendingDiscardCandidate = new RunCheckpoint(candidateId, generation, now, session.Catalog.OfflinePolicy.Revision, null,
                            session.Catalog.ContentRevision, null, run, permanent);
                    }
                    catch (ArgumentException)
                    {
                        return Reject(RunPersistenceReasons.InvalidCheckpoint);
                    }
                }
            }

            RunCheckpoint candidate = pendingDiscardCandidate;
            var promoted = supersede ? store.SupersedeDamaged(candidate) : store.Promote(candidate);
            if (!promoted.Succeeded || promoted.Value != candidate.Id)
            {
                SetStatus(PersistenceStatusKind.Failure, null, false, SaveFailureMessage);
                return Reject(promoted.Succeeded ? RunPersistenceReasons.PersistenceFailed : RunPersistenceReasons.From(promoted.Outcome));
            }

            return PublishPromotedColdStart(candidate);
        }

        private RunCommandResult<RunPersistenceOutcome> ResumeFromColdStart(bool overridden = false)
        {
            ClearColdStartState();
            RunCommandResult<RunPersistenceOutcome> resumed = ResumeAndReconcile();
            return overridden && resumed.IsSuccess ? RunCommandResult.Success(resumed.Value.AsSelectedDurable()) : resumed;
        }

        private RunCommandResult<RunPersistenceOutcome> BlockColdStart()
        {
            if (lifecycle == LifecycleState.Bootstrapping)
            {
                string loading = Transition(LifecycleState.Loading);
                if (loading != null) return Reject(loading);
            }

            coldStartInterrupted = true;
            SetStatus(PersistenceStatusKind.Failure, null, false, StorageMessage);
            return Reject(RunPersistenceReasons.PersistenceFailed);
        }

        private void ClearColdStartState()
        {
            importBlocked = false;
            coldStartInterrupted = false;
            blockedImportCode = null;
            pendingImportCandidate = null;
            pendingFreshRunCandidate = null;
            pendingDiscardCandidate = null;
        }

        private RunCommandResult<RunPersistenceOutcome> BlockImport(string code, string message)
        {
            coldStartInterrupted = false;
            importBlocked = true;
            blockedImportCode = code;
            SetStatus(PersistenceStatusKind.Failure, null, false, message);
            return Reject(code);
        }

        /// <summary>Re-promotes the SAME candidate bytes after a failed promotion; a durable different V2 generation wins instead.</summary>
        private RunCommandResult<RunPersistenceOutcome> RetryImportPromotion()
        {
            RunCheckpoint candidate = pendingImportCandidate;
            var activeRead = store.ReadActive();
            if (activeRead.Succeeded && !SameIdentity(activeRead.Checkpoint, candidate)) return ResumeFromColdStart(true);
            if (!activeRead.Succeeded && activeRead.Outcome == CommandOutcome.PersistenceFailed)
                return BlockImport(V1ImportCodes.Persistence, ImportPersistenceMessage);
            if (!activeRead.Succeeded && activeRead.Outcome != CommandOutcome.NoCheckpoint) return ResumeFromColdStart(); // torn files: SEC-1
            return PromoteImportCandidate(candidate);
        }

        private RunCommandResult<RunPersistenceOutcome> PromoteImportCandidate(RunCheckpoint candidate)
        {
            SetStatus(PersistenceStatusKind.Writing, null, false, "Importing the saved development factory.");
            var promoted = store.Promote(candidate);
            if (!promoted.Succeeded || promoted.Value != candidate.Id)
                return BlockImport(V1ImportCodes.Persistence, ImportPersistenceMessage);

            pendingImportCandidate = null;
            return PublishPromotedColdStart(candidate);
        }

        /// <summary>
        /// Publishes a promoted cold-start candidate (the import or a new run): the import is no longer blocked, the lifecycle
        /// follows the return path (Reconciling, then Running), and a failed install leaves <c>return.blocked</c> with the
        /// Reload action, which installs the durable generation (no second credit).
        /// </summary>
        private RunCommandResult<RunPersistenceOutcome> PublishPromotedColdStart(RunCheckpoint durable)
        {
            ClearColdStartState();
            ClearPendingReturn();
            pendingCandidate = durable;
            string reconciling = Transition(LifecycleState.Reconciling);
            if (reconciling != null) return Reject(reconciling);
            return PublishDurableReturn(durable, false);
        }
    }
}
