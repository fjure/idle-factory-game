using System;
using System.Threading;
using IdleFactory.Core.Application;
using IdleFactory.Domain;

namespace IdleFactory.Run.Application
{
    /// <summary>
    /// The timer-driven autosave of the live run (T559, FR-003, owner decision 2026-11-18): a force-stop without a pause callback
    /// used to lose everything since the last departure. The cadence is <see cref="AutosavePolicy"/>; the write is the existing
    /// generation protocol of the store (atomic replace, same format and version), so a failed write keeps the previous checkpoint and
    /// reports through <see cref="Status"/>.
    /// <para>
    /// Offline accounting is unchanged: a checkpoint's departure is <c>max(clock, timeline)</c> at the instant the run was captured, and the
    /// run state it holds is the state at that instant, so a later return credits only the time since that checkpoint, once (the
    /// exactly-once link <c>reconciledFromCheckpointId</c> is untouched). A periodic checkpoint only moves the kill point closer to the present.
    /// </para>
    /// <para>
    /// Threading (SC-008, no main-thread hitch): one write costs about 30 ms of file IO (five flushed files and their read-backs), which is
    /// two frames. When the store can (<see cref="IRunCheckpointBackgroundPromoter"/>) the main thread only captures the run and serializes it
    /// (the live run may only be read there); the generation protocol then runs on a pool thread over those bytes. At most one write is in
    /// flight, its result is applied on the main thread at a later tick, and every other persistence access of the owning thread joins it
    /// first (<see cref="SafeRunStore"/> calls <see cref="JoinAutosave"/> before each store access), so ordering, the generation numbers and the
    /// status are exactly those of sequential writes. The store serializes the operations of both threads with its operation gate, and a
    /// failed or interrupted write leaves the previous generation active, as any failed promote does. Without the capability, or without a
    /// known durable generation to continue from, the write is the synchronous <see cref="SaveNow"/>.
    /// </para>
    /// </summary>
    public sealed partial class RunPersistence
    {
        // The run revisions of the latest durable state (a save, an installed generation). A different value is a change that is not
        // durable yet. Without a baseline (nothing installed or saved this process) the run counts as changed.
        private bool revisionsKnown;
        private long durableQuantityRevision;
        private long durableStructureRevision;

        // The instant of the latest autosave attempt and whether it failed. Null until the first tick of a live run anchors the cadence.
        private long? autosaveAttemptUtcMs;
        private bool autosaveAttemptFailed;
        private long lastAutosaveTickUtcMs;
        private bool hasAutosaveTick;

        // The latest durable generation this process knows (a save it made, a generation it installed): enough to number the next one without
        // reading the store on the main thread. Used only while it is the generation <see cref="lastActiveCheckpointId"/> names; the store's
        // own generation check rejects a stale number, and a rejected write drops the cache so the next write reads the store.
        private bool durableKnown;
        private CheckpointId durableId;
        private long durableGeneration;
        private long durableDepartureUtcMs;

        private AutosaveJob inFlight;

        internal AutosavePolicy AutosaveCadence { get; set; } = new AutosavePolicy();

        /// <summary>Runs the file IO of a background write; the default is the thread pool. A test replaces it to control completion.</summary>
        internal Action<Action> AutosaveExecutor { get; set; } = work => ThreadPool.QueueUserWorkItem(_ => work());

        /// <summary>True while a background autosave write has been started and its result has not been applied yet.</summary>
        internal bool AutosaveInFlight => inFlight != null;

        /// <summary>
        /// One tick of the timer-driven autosave: starts a write when <see cref="AutosavePolicy"/> says one is due, otherwise does
        /// nothing and allocates nothing. Never writes while the lifecycle is not <see cref="LifecycleState.Running"/> (the cold start,
        /// a return, a recovery or a suspension is in progress, so no half-initialised session is saved) or while a prestige transition
        /// is pending (its durable commit owns the checkpoint). A tick that follows a wall-clock jump above the live-gap threshold writes
        /// nothing, so the host's live-gap departure (FR-032) always decides first. When a write is due,
        /// <paramref name="beforeWrite"/> runs first (the host brings the run up to the current time) and the decision is re-checked,
        /// because that call may have reconciled a gap or left the running state.
        /// <para>
        /// Returns <see cref="AutosaveTickOutcome.Written"/> or <see cref="AutosaveTickOutcome.Failed"/> on the tick that learns the result of a
        /// write (the synchronous write: the same tick; the background write: the first tick after it finished), otherwise
        /// <see cref="AutosaveTickOutcome.NotDue"/>.
        /// </para>
        /// </summary>
        public AutosaveTickOutcome AutosaveWhenDue(Action beforeWrite = null)
        {
            if (inFlight != null && inFlight.IsDone) return ApplyAutosave();
            if (EvaluateAutosave(true) == AutosaveReason.None) return AutosaveTickOutcome.NotDue;

            try
            {
                beforeWrite?.Invoke();
            }
            catch
            {
                // An exception of the preparation backs the cadence off by a full interval instead of repeating every frame.
                StampAutosaveAttempt(true);
                throw;
            }

            if (EvaluateAutosave(false) == AutosaveReason.None) return AutosaveTickOutcome.NotDue;

            StampAutosaveAttempt(true);
            if (TryStartBackgroundWrite()) return AutosaveTickOutcome.NotDue;

            bool written = false;
            try
            {
                written = SaveNow().IsSuccess;
                return written ? AutosaveTickOutcome.Written : AutosaveTickOutcome.Failed;
            }
            finally
            {
                autosaveAttemptFailed = !written;
            }
        }

        private AutosaveReason EvaluateAutosave(bool observeTick)
        {
            if (lifecycle != LifecycleState.Running || session.PendingTransition != null) return AutosaveReason.None;
            if (!TryCaptureUtcMs(out long nowMs)) return AutosaveReason.None;

            // A jump of the wall clock above the live-gap threshold between two ticks is the host's live-gap path to handle (FR-032).
            if (observeTick)
            {
                bool jumped = hasAutosaveTick && nowMs - lastAutosaveTickUtcMs > LiveGapThresholdMs;
                lastAutosaveTickUtcMs = nowMs;
                hasAutosaveTick = true;
                if (jumped) return AutosaveReason.None;
            }

            if (inFlight != null) return AutosaveReason.None;

            long? reference = LatestWriteUtcMs();
            if (!reference.HasValue)
            {
                // The first tick of a live run starts the cadence; nothing is due before one interval has passed.
                if (observeTick) autosaveAttemptUtcMs = nowMs;
                return AutosaveReason.None;
            }

            bool structural = !revisionsKnown || session.StructureRevision != durableStructureRevision;
            bool any = structural || session.QuantityRevision != durableQuantityRevision;
            return AutosaveCadence.Decide(nowMs - reference.Value, structural, any, autosaveAttemptFailed);
        }

        private long? LatestWriteUtcMs()
        {
            if (!autosaveAttemptUtcMs.HasValue) return lastCheckpointUtcMs;
            if (!lastCheckpointUtcMs.HasValue) return autosaveAttemptUtcMs;
            return Math.Max(autosaveAttemptUtcMs.Value, lastCheckpointUtcMs.Value);
        }

        private void StampAutosaveAttempt(bool failed)
        {
            if (TryCaptureUtcMs(out long nowMs)) autosaveAttemptUtcMs = nowMs;
            autosaveAttemptFailed = failed;
        }

        // A durable generation now holds the live run (a successful save, an install): the run is clean again.
        private void NoteDurable(RunCheckpoint checkpoint) =>
            NoteDurable(checkpoint.Id, checkpoint.Generation, checkpoint.DepartureUtcMs, session.QuantityRevision, session.StructureRevision);

        private void NoteDurable(CheckpointId id, long generation, long departureUtcMs, long quantityRevision, long structureRevision)
        {
            durableKnown = true;
            durableId = id;
            durableGeneration = generation;
            durableDepartureUtcMs = departureUtcMs;
            revisionsKnown = true;
            durableQuantityRevision = quantityRevision;
            durableStructureRevision = structureRevision;
            autosaveAttemptFailed = false;
        }

        // ---------------------------------------------------------------------------------------------------
        // The background write
        // ---------------------------------------------------------------------------------------------------

        private sealed class AutosaveJob
        {
            private readonly ManualResetEventSlim done = new ManualResetEventSlim(false);
            private CommandResult<CheckpointId> result;

            public RunCheckpoint Checkpoint;
            public long ClockMs;
            public long QuantityRevision;
            public long StructureRevision;

            public bool IsDone => done.IsSet;

            public CommandResult<CheckpointId> Result => result;

            public void Complete(CommandResult<CheckpointId> outcome)
            {
                result = outcome;
                done.Set(); // the release makes the result visible to the thread that sees IsDone
            }

            public void Wait() => done.Wait();
        }

        // Captures the run on this thread, serializes it, and hands the file IO to the executor. False when the write must be synchronous.
        private bool TryStartBackgroundWrite()
        {
            if (!store.SupportsBackgroundPromotion || !durableKnown || !lastActiveCheckpointId.HasValue ||
                lastActiveCheckpointId.Value != durableId || session.Permanent == null) return false;
            if (!TryCaptureUtcMs(out long clockMs)) return false;
            if (durableGeneration == long.MaxValue || !TryNextCheckpointId(durableId, null, out CheckpointId checkpointId)) return false;

            RunCheckpoint checkpoint;
            try
            {
                long departure = Math.Max(clockMs, session.TimelineUtcMs);
                if (durableDepartureUtcMs > departure) departure = durableDepartureUtcMs;
                checkpoint = new RunCheckpoint(checkpointId, durableGeneration + 1, departure,
                    session.Catalog.OfflinePolicy.Revision, null, session.Catalog.ContentRevision, null, session.State, session.Permanent);
            }
            catch (ArgumentException)
            {
                return false;
            }

            IPreparedPromotion prepared = store.TryPreparePromotion(checkpoint);
            if (prepared == null) return false;

            var job = new AutosaveJob
            {
                Checkpoint = checkpoint,
                ClockMs = clockMs,
                QuantityRevision = session.QuantityRevision,
                StructureRevision = session.StructureRevision,
            };
            inFlight = job;
            try
            {
                AutosaveExecutor(() => RunBackgroundWrite(prepared, job));
            }
            catch (Exception exception) when (SafeRunStore.IsExpectedInfrastructureFailure(exception) || exception is OutOfMemoryException)
            {
                // The pool refused the work: nothing was started, so write synchronously.
                inFlight = null;
                return false;
            }

            return true;
        }

        // On the worker: only the prepared bytes and the store's own operation gate are touched, never the session or this object's state.
        private static void RunBackgroundWrite(IPreparedPromotion prepared, AutosaveJob job)
        {
            CommandResult<CheckpointId> result;
            try
            {
                result = prepared.Publish() ?? CommandResult<CheckpointId>.Rejected(CommandOutcome.PersistenceFailed, SafeRunStore.StorageMessage);
            }
            catch (Exception)
            {
                // Nothing may escape a pool thread: an uncaught exception there ends the process.
                result = CommandResult<CheckpointId>.Rejected(CommandOutcome.PersistenceFailed, SafeRunStore.StorageMessage);
            }

            job.Complete(result);
        }

        /// <summary>
        /// Waits for the autosave in flight, if any, and applies its result (the owning thread only). Every store access of this class calls it
        /// first, so a command never runs with a background write of this process outstanding.
        /// </summary>
        private void JoinAutosave()
        {
            AutosaveJob job = inFlight;
            if (job == null) return;
            job.Wait();
            ApplyAutosave();
        }

        private AutosaveTickOutcome ApplyAutosave()
        {
            AutosaveJob job = inFlight;
            if (job == null) return AutosaveTickOutcome.NotDue;
            inFlight = null;
            CommandResult<CheckpointId> result = job.Result;
            RunCheckpoint checkpoint = job.Checkpoint;
            if (result != null && result.Succeeded && result.Value == checkpoint.Id)
            {
                lastActiveCheckpointId = checkpoint.Id;
                lastCheckpointUtcMs = job.ClockMs;
                NoteDurable(checkpoint.Id, checkpoint.Generation, checkpoint.DepartureUtcMs, job.QuantityRevision, job.StructureRevision);
                SetStatus(PersistenceStatusKind.Ready, checkpoint.Id, true, "Factory saved.");
                return AutosaveTickOutcome.Written;
            }

            // The previous generation is still the active one (the protocol never publishes a partial write). The cached generation may
            // be what was rejected, so the next write reads the store.
            durableKnown = false;
            autosaveAttemptFailed = true;
            SetStatus(PersistenceStatusKind.Failure, status.ActiveCheckpointId ?? lastActiveCheckpointId, status.PreviousCheckpointAvailable, SaveFailureMessage);
            return AutosaveTickOutcome.Failed;
        }
    }
}
