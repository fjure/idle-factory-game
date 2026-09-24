using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security;
using IdleFactory.Domain;
using IdleFactory.Simulation;

namespace IdleFactory.Core.Application
{
    public interface ICheckpointIdSource
    {
        CommandResult<CheckpointId> Next();
    }

    public sealed class GuidCheckpointIdSource : ICheckpointIdSource
    {
        public CommandResult<CheckpointId> Next() =>
            CommandResult<CheckpointId>.Success(new CheckpointId(Guid.NewGuid()));
    }

    /// <summary>An immutable persistence command result; it never exposes a checkpoint or save DTO.</summary>
    public sealed class PersistenceOperationResult
    {
        internal PersistenceOperationResult(
            CheckpointId? checkpointId,
            long? generation,
            FactorySnapshot factory,
            OfflineReturnSummary offlineSummary,
            bool usedFallback,
            bool wasNoOp)
        {
            if (generation.HasValue && generation.Value < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(generation));
            }

            if (checkpointId.HasValue)
            {
                _ = checkpointId.Value.Value;
            }

            CheckpointId = checkpointId;
            Generation = generation;
            Factory = factory;
            OfflineSummary = offlineSummary;
            UsedFallback = usedFallback;
            WasNoOp = wasNoOp;
        }

        public CheckpointId? CheckpointId { get; }
        public long? Generation { get; }
        public FactorySnapshot Factory { get; }
        public OfflineReturnSummary OfflineSummary { get; }
        public bool UsedFallback { get; }
        public bool WasNoOp { get; }
    }

    /// <summary>Serializes persistence and lifecycle commands for one application-owned factory session.</summary>
    public sealed class PersistenceCommands
    {
        private const string SaveFailureMessage = "The factory could not be saved.";
        private const string LoadFailureMessage = "The saved factory could not be loaded.";
        private const string ReturnFailureMessage = "The return could not be completed. Retry or reload the saved factory.";
        private const string RecoveryFailureMessage = "The previous factory could not be recovered.";

        private readonly FactorySession session;
        private readonly ICheckpointIdSource checkpointIds;
        private readonly OfflineReconciler reconciler;
        private PersistenceStatusSnapshot persistenceStatus;
        private OfflineReturnSummary lastOfflineSummary;
        private CheckpointId? lastActiveCheckpointId;
        private CheckpointId? departureCheckpointId;
        private PersistenceOperationResult departureResult;
        private bool suspensionAttempted;
        private Checkpoint pendingSource;
        private OfflineReconciliationResult pendingCandidate;
        private Checkpoint pendingRecoveryCandidate;
        private bool pendingUsedFallback;
        private DateTimeOffset capturedReturnUtc;
        private bool hasCapturedReturnUtc;

        public PersistenceCommands(FactorySession session)
            : this(session, new GuidCheckpointIdSource())
        {
        }

        public PersistenceCommands(FactorySession session, ICheckpointIdSource checkpointIds)
        {
            this.session = session ?? throw new ArgumentNullException(nameof(session));
            this.checkpointIds = checkpointIds ?? throw new ArgumentNullException(nameof(checkpointIds));
            reconciler = new OfflineReconciler();
            InitializeStatus();
        }

        public PersistenceStatusSnapshot GetPersistenceStatus() => persistenceStatus;

        public OfflineReturnSummary GetLastOfflineSummary() => lastOfflineSummary;

        public CommandResult<PersistenceOperationResult> SaveNow()
        {
            var admission = session.Admit(ApplicationOperation.Save);
            if (!admission.Succeeded)
            {
                return Rejected(admission.Outcome, admission.Message);
            }

            return SaveCheckpoint();
        }

        public CommandResult<PersistenceOperationResult> LoadDevelopmentCheckpoint()
        {
            var admission = session.Admit(ApplicationOperation.LoadDevelopmentCheckpoint);
            if (!admission.Succeeded)
            {
                return Rejected(admission.Outcome, admission.Message);
            }

            var activeRead = SafeReadActive();
            if (!activeRead.Succeeded)
            {
                SetReadFailure(activeRead, lastActiveCheckpointId);
                return Rejected(activeRead.Outcome, SafeReadMessage(activeRead.Outcome, LoadFailureMessage));
            }

            var candidate = activeRead.Checkpoint.CreateFactoryCopy();
            if (!ReferenceEquals(candidate.Catalog, session.Catalog))
            {
                SetStatus(PersistenceStatusKind.Failure, activeRead.Checkpoint.Id, true, LoadFailureMessage);
                return Rejected(CommandOutcome.InvalidCheckpoint, LoadFailureMessage);
            }

            var revisionCheck = CanReplaceState();
            if (!revisionCheck.Succeeded)
            {
                SetStatus(PersistenceStatusKind.Failure, activeRead.Checkpoint.Id, true, LoadFailureMessage);
                return Rejected(revisionCheck.Outcome, revisionCheck.Message);
            }

            var publication = session.ReplaceState(candidate);
            if (!publication.Succeeded)
            {
                SetStatus(PersistenceStatusKind.Failure, activeRead.Checkpoint.Id, true, LoadFailureMessage);
                return Rejected(publication.Outcome, LoadFailureMessage);
            }

            lastActiveCheckpointId = activeRead.Checkpoint.Id;
            departureCheckpointId = null;
            departureResult = null;
            suspensionAttempted = false;
            ClearPendingReturn();
            lastOfflineSummary = null;
            SetStatus(PersistenceStatusKind.Ready, activeRead.Checkpoint.Id, ReadPreviousAvailability(), "Saved factory loaded.");
            return Succeeded(
                activeRead.Checkpoint.Id,
                activeRead.Checkpoint.Generation,
                session.GetFactorySnapshot(),
                null,
                false,
                false);
        }

        public CommandResult<PersistenceOperationResult> RecordSuspension()
        {
            if (session.LifecycleState == LifecycleState.Suspended && suspensionAttempted)
            {
                return Succeeded(
                    departureResult?.CheckpointId,
                    departureResult?.Generation,
                    null,
                    null,
                    false,
                    true);
            }

            var admission = session.Admit(ApplicationOperation.RecordSuspension);
            if (!admission.Succeeded)
            {
                return Rejected(admission.Outcome, admission.Message);
            }

            var checkpointing = session.TransitionTo(LifecycleState.Checkpointing);
            if (!checkpointing.Succeeded)
            {
                return Rejected(checkpointing.Outcome, checkpointing.Message);
            }

            suspensionAttempted = true;
            departureCheckpointId = null;
            departureResult = null;
            var saved = SaveCheckpoint();
            if (saved.Succeeded)
            {
                departureCheckpointId = saved.Value.CheckpointId;
                departureResult = saved.Value;
            }

            // A reported departure suspends the application even if checkpoint storage fails.
            var suspended = session.TransitionTo(LifecycleState.Suspended);
            if (!suspended.Succeeded)
            {
                SetStatus(PersistenceStatusKind.Failure, lastActiveCheckpointId, persistenceStatus.PreviousCheckpointAvailable,
                    "The application could not enter its suspended state.");
                return Rejected(suspended.Outcome, suspended.Message);
            }

            return saved;
        }

        public CommandResult<PersistenceOperationResult> ResumeAndReconcile()
        {
            var startingState = session.LifecycleState;
            var admission = session.Admit(ApplicationOperation.ResumeAndReconcile);
            if (!admission.Succeeded)
            {
                return Rejected(admission.Outcome, admission.Message);
            }

            if (startingState == LifecycleState.Bootstrapping)
            {
                var loading = session.TransitionTo(LifecycleState.Loading);
                if (!loading.Succeeded)
                {
                    return Rejected(loading.Outcome, loading.Message);
                }
            }

            var reconciling = session.TransitionTo(LifecycleState.Reconciling);
            if (!reconciling.Succeeded)
            {
                return Rejected(reconciling.Outcome, reconciling.Message);
            }

            lastOfflineSummary = null;
            ClearPendingReturn();
            pendingUsedFallback = startingState == LifecycleState.Suspended && !departureCheckpointId.HasValue;
            hasCapturedReturnUtc = TryCaptureUtc(out capturedReturnUtc);

            CheckpointReadResult sourceRead;
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
                    return CompleteWithoutCheckpoint();
                }

                return BlockReturn(SafeReadMessage(sourceRead.Outcome, ReturnFailureMessage), sourceRead.Outcome);
            }

            pendingSource = sourceRead.Checkpoint;
            if (!hasCapturedReturnUtc)
            {
                return BlockReturn(ReturnFailureMessage, CommandOutcome.PersistenceFailed);
            }

            return BuildPromoteAndPublish(pendingSource, capturedReturnUtc, pendingUsedFallback);
        }

        public CommandResult<PersistenceOperationResult> RetryReturn()
        {
            var admission = session.Admit(ApplicationOperation.RetryReturn);
            if (!admission.Succeeded)
            {
                return Rejected(admission.Outcome, admission.Message);
            }

            // Always inspect the authoritative active generation before reusing an in-memory candidate.
            var activeRead = SafeReadActive();
            if (!activeRead.Succeeded)
            {
                SetReadFailure(activeRead, lastActiveCheckpointId);
                return Rejected(CommandOutcome.ReturnBlocked, ReturnFailureMessage);
            }

            var active = activeRead.Checkpoint;
            if (pendingRecoveryCandidate != null)
            {
                // A recovery retry must ask the store to confirm the exact candidate again;
                // reading a visible manifest alone does not prove durable promotion.
                return RetryPendingRecovery();
            }

            lastActiveCheckpointId = active.Id;
            if (pendingCandidate != null && CheckpointsEquivalent(active, pendingCandidate.CandidateCheckpoint))
            {
                var retryTransition = session.TransitionTo(LifecycleState.Reconciling);
                if (!retryTransition.Succeeded)
                {
                    return KeepReturnBlocked(retryTransition.Message, retryTransition.Outcome, active.Id);
                }

                return PublishDurableReturn(active, pendingCandidate, pendingUsedFallback);
            }

            var sourceResult = ResolveRetrySource(active);
            if (!sourceResult.Succeeded)
            {
                return KeepReturnBlocked(sourceResult.Message, sourceResult.Outcome, active.Id);
            }

            var source = sourceResult.Checkpoint;
            if (CheckpointsEquivalent(active, source))
            {
                if (!hasCapturedReturnUtc)
                {
                    hasCapturedReturnUtc = TryCaptureUtc(out capturedReturnUtc);
                    if (!hasCapturedReturnUtc)
                    {
                        return KeepReturnBlocked(ReturnFailureMessage, CommandOutcome.PersistenceFailed, active.Id);
                    }
                }

                var retryTransition = session.TransitionTo(LifecycleState.Reconciling);
                if (!retryTransition.Succeeded)
                {
                    return KeepReturnBlocked(retryTransition.Message, retryTransition.Outcome, active.Id);
                }

                return BuildPromoteAndPublish(source, capturedReturnUtc, pendingUsedFallback);
            }

            if (hasCapturedReturnUtc && active.ReconciledFromCheckpointId.HasValue &&
                active.ReconciledFromCheckpointId.Value == source.Id)
            {
                var recoveredCandidate = reconciler.Reconcile(source, capturedReturnUtc, active.Id);
                if (recoveredCandidate.Status == OfflineReconciliationStatus.CandidateReady &&
                    CheckpointsEquivalent(active, recoveredCandidate.CandidateCheckpoint))
                {
                    var retryTransition = session.TransitionTo(LifecycleState.Reconciling);
                    if (!retryTransition.Succeeded)
                    {
                        return KeepReturnBlocked(retryTransition.Message, retryTransition.Outcome, active.Id);
                    }

                    pendingCandidate = recoveredCandidate;
                    return PublishDurableReturn(active, recoveredCandidate, pendingUsedFallback);
                }
            }

            return KeepReturnBlocked(ReturnFailureMessage, CommandOutcome.ReturnBlocked, active.Id);
        }

        public CommandResult<PersistenceOperationResult> ReloadAuthoritativeGeneration()
        {
            var admission = session.Admit(ApplicationOperation.ReloadAuthoritativeGeneration);
            if (!admission.Succeeded)
            {
                return Rejected(admission.Outcome, admission.Message);
            }

            var activeRead = SafeReadActive();
            if (!activeRead.Succeeded)
            {
                SetReadFailure(activeRead, lastActiveCheckpointId);
                return Rejected(CommandOutcome.ReturnBlocked, SafeReadMessage(activeRead.Outcome, ReturnFailureMessage));
            }

            var candidate = activeRead.Checkpoint.CreateFactoryCopy();
            if (!ReferenceEquals(candidate.Catalog, session.Catalog))
            {
                SetStatus(PersistenceStatusKind.Failure, activeRead.Checkpoint.Id, persistenceStatus.PreviousCheckpointAvailable,
                    LoadFailureMessage);
                return Rejected(CommandOutcome.ReturnBlocked, LoadFailureMessage);
            }

            var revisionCheck = CanReplaceState();
            if (!revisionCheck.Succeeded)
            {
                SetStatus(PersistenceStatusKind.Failure, activeRead.Checkpoint.Id, persistenceStatus.PreviousCheckpointAvailable,
                    ReturnFailureMessage);
                return Rejected(CommandOutcome.ReturnBlocked, ReturnFailureMessage);
            }

            var durableConfirmation = SafePromote(activeRead.Checkpoint);
            if (!durableConfirmation.Succeeded || durableConfirmation.Value != activeRead.Checkpoint.Id)
            {
                SetStatus(
                    PersistenceStatusKind.Failure,
                    persistenceStatus.ActiveCheckpointId ?? lastActiveCheckpointId,
                    persistenceStatus.PreviousCheckpointAvailable,
                    LoadFailureMessage);
                return Rejected(CommandOutcome.ReturnBlocked, LoadFailureMessage);
            }

            lastActiveCheckpointId = activeRead.Checkpoint.Id;

            var publication = session.ReplaceState(candidate);
            if (!publication.Succeeded)
            {
                SetStatus(PersistenceStatusKind.Failure, activeRead.Checkpoint.Id, persistenceStatus.PreviousCheckpointAvailable,
                    ReturnFailureMessage);
                return Rejected(CommandOutcome.ReturnBlocked, ReturnFailureMessage);
            }

            // Publication succeeds before leaving ReturnBlocked; failed replacement remains blocked.
            var loading = session.TransitionTo(LifecycleState.Loading);
            if (!loading.Succeeded)
            {
                SetStatus(PersistenceStatusKind.Failure, activeRead.Checkpoint.Id, persistenceStatus.PreviousCheckpointAvailable,
                    ReturnFailureMessage);
                return Rejected(CommandOutcome.ReturnBlocked, ReturnFailureMessage);
            }

            var running = session.TransitionTo(LifecycleState.Running);
            if (!running.Succeeded)
            {
                SetStatus(PersistenceStatusKind.Failure, activeRead.Checkpoint.Id, persistenceStatus.PreviousCheckpointAvailable,
                    ReturnFailureMessage);
                return Rejected(CommandOutcome.ReturnBlocked, ReturnFailureMessage);
            }

            lastActiveCheckpointId = activeRead.Checkpoint.Id;
            lastOfflineSummary = null;
            departureCheckpointId = null;
            departureResult = null;
            suspensionAttempted = false;
            ClearPendingReturn();
            pendingRecoveryCandidate = null;
            SetStatus(PersistenceStatusKind.Ready, activeRead.Checkpoint.Id, ReadPreviousAvailability(), "Saved factory reloaded.");
            return Succeeded(
                activeRead.Checkpoint.Id,
                activeRead.Checkpoint.Generation,
                session.GetFactorySnapshot(),
                null,
                false,
                false);
        }

        public CommandResult<PersistenceOperationResult> RecoverPreviousCheckpoint()
        {
            if (pendingRecoveryCandidate != null)
            {
                return RetryReturn();
            }

            var admission = session.AdmitRecoveryPrevious();
            if (!admission.Succeeded)
            {
                var activeId = persistenceStatus.ActiveCheckpointId ?? lastActiveCheckpointId;
                var noPrevious = admission.Outcome == CommandOutcome.NoCheckpoint;
                SetStatus(
                    session.LifecycleState == LifecycleState.ReturnBlocked
                        ? PersistenceStatusKind.Failure
                        : noPrevious && activeId.HasValue
                            ? PersistenceStatusKind.Ready
                    : noPrevious
                                ? PersistenceStatusKind.NoCheckpoint
                                : PersistenceStatusKind.Failure,
                    activeId,
                    false,
                    RecoveryFailureMessage);
                return Rejected(admission.Outcome, SafeReadMessage(admission.Outcome, RecoveryFailureMessage));
            }

            var activeRead = SafeReadActive();
            if (!activeRead.Succeeded)
            {
                SetReadFailure(activeRead, lastActiveCheckpointId);
                return Rejected(activeRead.Outcome, SafeReadMessage(activeRead.Outcome, RecoveryFailureMessage));
            }

            var active = activeRead.Checkpoint;
            var previous = admission.Value;
            if (active == null || previous == null)
            {
                SetStatus(
                    session.LifecycleState == LifecycleState.ReturnBlocked ? PersistenceStatusKind.Failure : PersistenceStatusKind.NoCheckpoint,
                    active?.Id ?? lastActiveCheckpointId,
                    false,
                    RecoveryFailureMessage);
                return Rejected(CommandOutcome.NoCheckpoint, RecoveryFailureMessage);
            }

            if (!ReferenceEquals(previous.CreateFactoryCopy().Catalog, session.Catalog))
            {
                SetStatus(PersistenceStatusKind.Failure, active.Id, true, RecoveryFailureMessage);
                return Rejected(CommandOutcome.InvalidCheckpoint, RecoveryFailureMessage);
            }

            if (!TryCaptureUtc(out var recoveryUtc))
            {
                SetStatus(PersistenceStatusKind.Failure, active.Id, true, RecoveryFailureMessage);
                return Rejected(CommandOutcome.PersistenceFailed, RecoveryFailureMessage);
            }

            long recoveryGeneration;
            try
            {
                recoveryGeneration = checked(Math.Max(active.Generation, previous.Generation) + 1);
            }
            catch (OverflowException)
            {
                SetStatus(PersistenceStatusKind.Failure, active.Id, true, RecoveryFailureMessage);
                return Rejected(CommandOutcome.InvalidCheckpoint, RecoveryFailureMessage);
            }

            if (!TryNextCheckpointId(active.Id, previous.Id, out var recoveryId))
            {
                SetStatus(PersistenceStatusKind.Failure, active.Id, true, RecoveryFailureMessage);
                return Rejected(CommandOutcome.PersistenceFailed, RecoveryFailureMessage);
            }

            Checkpoint recoveryCandidate;
            try
            {
                recoveryCandidate = new Checkpoint(
                    recoveryId,
                    recoveryGeneration,
                    recoveryUtc,
                    previous.OfflinePolicyRevision,
                    previous.Id,
                    previous.CreateFactoryCopy());
            }
            catch (Exception exception) when (IsExpectedDomainFailure(exception))
            {
                SetStatus(PersistenceStatusKind.Failure, active.Id, true, RecoveryFailureMessage);
                return Rejected(CommandOutcome.InvalidCheckpoint, RecoveryFailureMessage);
            }

            var revisionCheck = CanReplaceState();
            if (!revisionCheck.Succeeded)
            {
                SetStatus(PersistenceStatusKind.Failure, active.Id, true, RecoveryFailureMessage);
                return Rejected(revisionCheck.Outcome, RecoveryFailureMessage);
            }

            SetStatus(PersistenceStatusKind.Writing, active.Id, true, "Recovering the previous factory.");
            var recovered = SafeRecoverPrevious(recoveryCandidate);
            if (!recovered.Succeeded || recovered.Value != recoveryCandidate.Id)
            {
                var outcome = recovered.Succeeded ? CommandOutcome.PersistenceFailed : recovered.Outcome;
                return KeepRecoveryBlocked(recoveryCandidate, active.Id, outcome);
            }

            var replacement = session.ReplaceState(recoveryCandidate.CreateFactoryCopy());
            if (!replacement.Succeeded)
            {
                return KeepRecoveryBlocked(recoveryCandidate, active.Id, replacement.Outcome);
            }

            if (session.LifecycleState == LifecycleState.ReturnBlocked)
            {
                var loading = session.TransitionTo(LifecycleState.Loading);
                if (!loading.Succeeded)
                {
                    SetStatus(PersistenceStatusKind.Failure, recoveryCandidate.Id, true, RecoveryFailureMessage);
                    return Rejected(CommandOutcome.ReturnBlocked, RecoveryFailureMessage);
                }

                var running = session.TransitionTo(LifecycleState.Running);
                if (!running.Succeeded)
                {
                    SetStatus(PersistenceStatusKind.Failure, recoveryCandidate.Id, true, RecoveryFailureMessage);
                    return Rejected(CommandOutcome.ReturnBlocked, RecoveryFailureMessage);
                }
            }

            lastActiveCheckpointId = recoveryCandidate.Id;
            departureCheckpointId = null;
            departureResult = null;
            suspensionAttempted = false;
            ClearPendingReturn();
            pendingRecoveryCandidate = null;
            lastOfflineSummary = null;
            SetStatus(PersistenceStatusKind.Recovered, recoveryCandidate.Id, true, "Previous factory recovered.");
            return Succeeded(
                recoveryCandidate.Id,
                recoveryCandidate.Generation,
                session.GetFactorySnapshot(),
                null,
                false,
                false);
        }

        public CommandResult<PersistenceOperationResult> ResetDevelopmentScenario()
        {
            var admission = session.Admit(ApplicationOperation.OrdinaryMutation);
            if (!admission.Succeeded)
            {
                return Rejected(admission.Outcome, admission.Message);
            }

            FactoryState scenario;
            try
            {
                scenario = CreateCanonicalD001(session.Catalog);
            }
            catch (Exception exception) when (IsExpectedDomainFailure(exception))
            {
                return Rejected(CommandOutcome.InvalidDefinition, "The D001 development scenario is unavailable.");
            }

            var publication = session.ReplaceState(scenario);
            if (!publication.Succeeded)
            {
                return Rejected(publication.Outcome, "The development scenario could not be reset.");
            }

            lastOfflineSummary = null;
            return Succeeded(null, null, session.GetFactorySnapshot(), null, false, false);
        }

        private static FactoryState CreateCanonicalD001(DefinitionCatalog catalog)
        {
            var buildingId = new BuildingDefinitionId("dev.machine.basic");
            var recipeId = new RecipeId("dev.recipe.basic");
            var inputId = new ResourceId("dev.input");
            var outputId = new ResourceId("dev.output");
            var definition = catalog.Resolve(buildingId);
            var recipe = catalog.Resolve(recipeId);
            _ = catalog.Resolve(inputId);
            _ = catalog.Resolve(outputId);

            if (definition.Footprint.Width != 2 || definition.Footprint.Height != 2 ||
                definition.AllowedOrientations.Count != 1 || definition.AllowedOrientations[0] != 0 ||
                !definition.IsDevelopmentOnly || !definition.RecipeId.HasValue || definition.RecipeId.Value != recipeId ||
                !HasCanonicalCapacity(definition.InputBuffer, BufferRole.Input, inputId, 4) ||
                !HasCanonicalCapacity(definition.OutputBuffer, BufferRole.Output, outputId, 1) ||
                !recipe.IsDevelopmentOnly || recipe.DurationTicks != 50000000L ||
                recipe.Inputs.Count != 1 || recipe.Inputs[0].ResourceId != inputId || recipe.Inputs[0].Amount != 2 ||
                recipe.Outputs.Count != 1 || recipe.Outputs[0].ResourceId != outputId || recipe.Outputs[0].Amount != 1)
            {
                throw new InvalidOperationException("The session catalog does not contain the canonical D001 definitions.");
            }

            var input = new ResourceBufferState(
                definition.InputBuffer,
                new[] { new KeyValuePair<ResourceId, long>(inputId, 4) });
            var output = new ResourceBufferState(
                definition.OutputBuffer,
                new[] { new KeyValuePair<ResourceId, long>(outputId, 0) });
            var machine = new BuildingInstanceState(
                new BuildingInstanceId(Guid.NewGuid()),
                definition,
                new GridPosition(0, 0),
                0,
                true,
                input,
                output,
                new ProductionState(recipe, 0));

            return new FactoryState(
                new FactoryBounds(new GridPosition(0, 0), 16, 16),
                catalog,
                new[] { machine });
        }

        private static bool HasCanonicalCapacity(
            BufferDefinition buffer,
            BufferRole role,
            ResourceId resourceId,
            long expectedCapacity)
        {
            long capacity;
            return buffer != null && buffer.Role == role && buffer.Capacities.Count == 1 &&
                   buffer.Capacities.TryGetValue(resourceId, out capacity) && capacity == expectedCapacity;
        }

        private CommandResult<PersistenceOperationResult> SaveCheckpoint()
        {
            SetStatus(PersistenceStatusKind.Writing, lastActiveCheckpointId, persistenceStatus.PreviousCheckpointAvailable,
                "Saving the factory.");

            if (!TryCaptureUtc(out var departureUtc))
            {
                SetStatus(PersistenceStatusKind.Failure, lastActiveCheckpointId, persistenceStatus.PreviousCheckpointAvailable,
                    SaveFailureMessage);
                return Rejected(CommandOutcome.PersistenceFailed, SaveFailureMessage);
            }

            if (!TryReadHistory(out var active, out var previous, out var historyFailure))
            {
                SetReadFailure(historyFailure, lastActiveCheckpointId);
                return Rejected(historyFailure.Outcome, SafeReadMessage(historyFailure.Outcome, SaveFailureMessage));
            }

            if (!TryNextGeneration(active, previous, out var generation))
            {
                SetStatus(PersistenceStatusKind.Failure, active?.Id ?? lastActiveCheckpointId, previous != null, SaveFailureMessage);
                return Rejected(CommandOutcome.InvalidCheckpoint, SaveFailureMessage);
            }

            if (!TryNextCheckpointId(active?.Id, previous?.Id, out var checkpointId))
            {
                SetStatus(PersistenceStatusKind.Failure, active?.Id ?? lastActiveCheckpointId, previous != null, SaveFailureMessage);
                return Rejected(CommandOutcome.PersistenceFailed, SaveFailureMessage);
            }

            Checkpoint checkpoint;
            try
            {
                checkpoint = new Checkpoint(
                    checkpointId,
                    generation,
                    departureUtc,
                    new OfflinePolicyRevision(DefinitionCatalog.SupportedOfflinePolicyRevision),
                    null,
                    session.CreateStateCopy());
            }
            catch (Exception exception) when (IsExpectedDomainFailure(exception))
            {
                SetStatus(PersistenceStatusKind.Failure, active?.Id ?? lastActiveCheckpointId, previous != null, SaveFailureMessage);
                return Rejected(CommandOutcome.InvalidCheckpoint, SaveFailureMessage);
            }

            var promoted = SafePromote(checkpoint);
            if (!promoted.Succeeded || promoted.Value != checkpoint.Id)
            {
                var outcome = promoted.Succeeded ? CommandOutcome.PersistenceFailed : promoted.Outcome;
                var activeNow = SafeReadActive();
                var activeId = activeNow.Succeeded ? activeNow.Checkpoint.Id : active?.Id ?? lastActiveCheckpointId;
                SetStatus(PersistenceStatusKind.Failure, activeId, active != null || previous != null, SaveFailureMessage);
                return Rejected(outcome, SaveFailureMessage);
            }

            lastActiveCheckpointId = checkpoint.Id;
            SetStatus(PersistenceStatusKind.Ready, checkpoint.Id, active != null || previous != null, "Factory saved.");
            return Succeeded(checkpoint.Id, checkpoint.Generation, null, null, false, false);
        }

        private CommandResult<PersistenceOperationResult> BuildPromoteAndPublish(
            Checkpoint source,
            DateTimeOffset returnUtc,
            bool usedFallback)
        {
            if (!CanReplaceState().Succeeded)
            {
                return BlockReturn(ReturnFailureMessage, CommandOutcome.InvalidRequest);
            }

            if (!TryNextCheckpointId(source.Id, lastActiveCheckpointId, out var resultId))
            {
                return BlockReturn(ReturnFailureMessage, CommandOutcome.PersistenceFailed);
            }

            var calculated = reconciler.Reconcile(source, returnUtc, resultId);
            if (calculated.Status != OfflineReconciliationStatus.CandidateReady)
            {
                return BlockReturn(DescribeReconciliationFailure(calculated.Status), MapReconciliationOutcome(calculated.Status));
            }

            pendingSource = source;
            pendingCandidate = calculated;
            pendingUsedFallback = usedFallback;
            SetStatus(PersistenceStatusKind.Writing, source.Id, persistenceStatus.PreviousCheckpointAvailable,
                "Applying the saved return state.");

            var promoted = SafePromote(calculated.CandidateCheckpoint);
            if (!promoted.Succeeded || promoted.Value != calculated.CandidateCheckpoint.Id)
            {
                var outcome = promoted.Succeeded ? CommandOutcome.PersistenceFailed : promoted.Outcome;
                var activeRead = SafeReadActive();
                var activeId = activeRead.Succeeded ? activeRead.Checkpoint.Id : lastActiveCheckpointId;
                return BlockReturn(ReturnFailureMessage, outcome, activeId);
            }

            return PublishDurableReturn(
                calculated.CandidateCheckpoint,
                calculated,
                usedFallback);
        }

        private CommandResult<PersistenceOperationResult> PublishDurableReturn(
            Checkpoint durableCheckpoint,
            OfflineReconciliationResult reconciliation,
            bool usedFallback)
        {
            if (reconciliation == null || reconciliation.Status != OfflineReconciliationStatus.CandidateReady ||
                !CheckpointsEquivalent(durableCheckpoint, reconciliation.CandidateCheckpoint))
            {
                return BlockReturn(ReturnFailureMessage, CommandOutcome.InvalidCheckpoint, durableCheckpoint?.Id);
            }

            var revisionCheck = CanReplaceState();
            if (!revisionCheck.Succeeded)
            {
                return BlockReturn(ReturnFailureMessage, revisionCheck.Outcome, durableCheckpoint.Id);
            }

            var publication = session.ReplaceState(durableCheckpoint.CreateFactoryCopy());
            if (!publication.Succeeded)
            {
                return BlockReturn(ReturnFailureMessage, publication.Outcome, durableCheckpoint.Id);
            }

            // Create a success summary only after the store confirms this exact candidate is durable.
            OfflineReturnSummary durableSummary;
            try
            {
                durableSummary = reconciliation.Summary.CreateDurableSummary(durableCheckpoint.Id);
            }
            catch (ArgumentException)
            {
                return BlockReturn(ReturnFailureMessage, CommandOutcome.InvalidCheckpoint, durableCheckpoint.Id);
            }

            var running = session.TransitionTo(LifecycleState.Running);
            if (!running.Succeeded)
            {
                return BlockReturn(ReturnFailureMessage, running.Outcome, durableCheckpoint.Id);
            }

            lastOfflineSummary = durableSummary;
            lastActiveCheckpointId = durableCheckpoint.Id;
            departureCheckpointId = null;
            departureResult = null;
            suspensionAttempted = false;
            var generation = durableCheckpoint.Generation;
            ClearPendingReturn();
            SetStatus(PersistenceStatusKind.Ready, durableCheckpoint.Id, true, "Factory return applied and saved.");
            return Succeeded(
                durableCheckpoint.Id,
                generation,
                session.GetFactorySnapshot(),
                durableSummary,
                usedFallback,
                false);
        }

        private CommandResult<PersistenceOperationResult> CompleteWithoutCheckpoint()
        {
            var noCheckpointSummary = new OfflineReturnSummary(
                OfflineReturnOutcome.NoCheckpoint,
                0,
                0,
                0,
                Array.Empty<ResourceDeltaSnapshot>(),
                Array.Empty<OfflineLimitationSnapshot>(),
                null,
                null);

            var running = session.TransitionTo(LifecycleState.Running);
            if (!running.Succeeded)
            {
                return BlockReturn(ReturnFailureMessage, running.Outcome);
            }

            lastOfflineSummary = noCheckpointSummary;
            departureCheckpointId = null;
            departureResult = null;
            suspensionAttempted = false;
            ClearPendingReturn();
            lastActiveCheckpointId = null;
            SetStatus(PersistenceStatusKind.NoCheckpoint, null, false, "No saved factory was available; the current development scenario was kept.");
            return Succeeded(null, null, session.GetFactorySnapshot(), noCheckpointSummary, false, false);
        }

        private CommandResult<PersistenceOperationResult> BlockReturn(
            string message,
            CommandOutcome outcome,
            CheckpointId? activeCheckpointId = null)
        {
            if (session.LifecycleState == LifecycleState.Reconciling)
            {
                var blocked = session.TransitionTo(LifecycleState.ReturnBlocked);
                if (!blocked.Succeeded)
                {
                    outcome = blocked.Outcome;
                }
            }

            SetStatus(
                PersistenceStatusKind.Failure,
                activeCheckpointId ?? pendingSource?.Id ?? lastActiveCheckpointId,
                persistenceStatus.PreviousCheckpointAvailable,
                string.IsNullOrWhiteSpace(message) ? ReturnFailureMessage : message);
            return Rejected(
                session.LifecycleState == LifecycleState.ReturnBlocked ? CommandOutcome.ReturnBlocked : outcome,
                ReturnFailureMessage);
        }

        private CommandResult<PersistenceOperationResult> KeepReturnBlocked(
            string message,
            CommandOutcome outcome,
            CheckpointId? activeCheckpointId)
        {
            SetStatus(PersistenceStatusKind.Failure, activeCheckpointId ?? lastActiveCheckpointId,
                persistenceStatus.PreviousCheckpointAvailable,
                string.IsNullOrWhiteSpace(message) ? ReturnFailureMessage : message);
            return Rejected(CommandOutcome.ReturnBlocked, ReturnFailureMessage);
        }

        private CommandResult<PersistenceOperationResult> KeepRecoveryBlocked(
            Checkpoint candidate,
            CheckpointId? confirmedActiveCheckpointId,
            CommandOutcome outcome)
        {
            pendingRecoveryCandidate = candidate;
            if (session.LifecycleState == LifecycleState.Running)
            {
                var blocked = session.TransitionTo(LifecycleState.ReturnBlocked);
                if (!blocked.Succeeded)
                {
                    outcome = blocked.Outcome;
                }
            }

            SetStatus(
                PersistenceStatusKind.Failure,
                confirmedActiveCheckpointId ?? persistenceStatus.ActiveCheckpointId ?? lastActiveCheckpointId,
                true,
                RecoveryFailureMessage);
            return Rejected(
                session.LifecycleState == LifecycleState.ReturnBlocked ? CommandOutcome.ReturnBlocked : outcome,
                RecoveryFailureMessage);
        }

        private CommandResult<PersistenceOperationResult> RetryPendingRecovery()
        {
            var candidate = pendingRecoveryCandidate;
            if (candidate == null)
            {
                return Rejected(CommandOutcome.ReturnBlocked, RecoveryFailureMessage);
            }

            var revisionCheck = CanReplaceState();
            if (!revisionCheck.Succeeded)
            {
                return KeepRecoveryBlocked(candidate, persistenceStatus.ActiveCheckpointId, revisionCheck.Outcome);
            }

            var recovered = SafeRecoverPrevious(candidate);
            if (!recovered.Succeeded || recovered.Value != candidate.Id)
            {
                var outcome = recovered.Succeeded ? CommandOutcome.PersistenceFailed : recovered.Outcome;
                return KeepRecoveryBlocked(candidate, persistenceStatus.ActiveCheckpointId, outcome);
            }

            // The candidate is published only after RecoverPrevious confirms this exact identity.
            var replacement = session.ReplaceState(candidate.CreateFactoryCopy());
            if (!replacement.Succeeded)
            {
                return KeepRecoveryBlocked(candidate, persistenceStatus.ActiveCheckpointId, replacement.Outcome);
            }

            var loading = session.TransitionTo(LifecycleState.Loading);
            if (!loading.Succeeded)
            {
                return KeepRecoveryBlocked(candidate, persistenceStatus.ActiveCheckpointId, loading.Outcome);
            }

            var running = session.TransitionTo(LifecycleState.Running);
            if (!running.Succeeded)
            {
                return KeepRecoveryBlocked(candidate, persistenceStatus.ActiveCheckpointId, running.Outcome);
            }

            lastActiveCheckpointId = candidate.Id;
            departureCheckpointId = null;
            departureResult = null;
            suspensionAttempted = false;
            ClearPendingReturn();
            pendingRecoveryCandidate = null;
            lastOfflineSummary = null;
            SetStatus(PersistenceStatusKind.Recovered, candidate.Id, true, "Previous factory recovered.");
            return Succeeded(
                candidate.Id,
                candidate.Generation,
                session.GetFactorySnapshot(),
                null,
                false,
                false);
        }

        private RetrySource ResolveRetrySource(Checkpoint active)
        {
            if (pendingSource != null)
            {
                return RetrySource.Success(pendingSource);
            }

            if (departureCheckpointId.HasValue)
            {
                var departureRead = SafeRead(departureCheckpointId.Value);
                return departureRead.Succeeded
                    ? RetrySource.Success(departureRead.Checkpoint)
                    : RetrySource.Failure(departureRead.Outcome, SafeReadMessage(departureRead.Outcome, ReturnFailureMessage));
            }

            pendingUsedFallback = true;
            return RetrySource.Success(active);
        }

        private CommandResult<CheckpointId> TryNextCheckpointId(
            CheckpointId? reservedOne,
            CheckpointId? reservedTwo,
            CheckpointId? reservedThree,
            out CheckpointId checkpointId)
        {
            checkpointId = default(CheckpointId);
            CommandResult<CheckpointId> generated;
            try
            {
                generated = checkpointIds.Next();
            }
            catch (Exception exception) when (IsExpectedInfrastructureFailure(exception))
            {
                return CommandResult<CheckpointId>.Rejected(CommandOutcome.PersistenceFailed, "Checkpoint identity generation failed.");
            }

            if (generated == null || !generated.Succeeded)
            {
                return generated ?? CommandResult<CheckpointId>.Rejected(
                    CommandOutcome.PersistenceFailed,
                    "Checkpoint identity generation failed.");
            }

            try
            {
                _ = generated.Value.Value;
            }
            catch (InvalidOperationException)
            {
                return CommandResult<CheckpointId>.Rejected(CommandOutcome.PersistenceFailed, "Checkpoint identity generation failed.");
            }

            if ((reservedOne.HasValue && generated.Value == reservedOne.Value) ||
                (reservedTwo.HasValue && generated.Value == reservedTwo.Value) ||
                (reservedThree.HasValue && generated.Value == reservedThree.Value))
            {
                return CommandResult<CheckpointId>.Rejected(CommandOutcome.PersistenceFailed, "Checkpoint identity generation failed.");
            }

            checkpointId = generated.Value;
            return CommandResult<CheckpointId>.Success(checkpointId);
        }

        private bool TryNextCheckpointId(
            CheckpointId? reservedOne,
            CheckpointId? reservedTwo,
            out CheckpointId checkpointId) =>
            TryNextCheckpointId(reservedOne, reservedTwo, null, out checkpointId).Succeeded;

        private bool TryReadHistory(
            out Checkpoint active,
            out Checkpoint previous,
            out CheckpointReadResult failure)
        {
            active = null;
            previous = null;
            failure = null;

            var activeRead = SafeReadActive();
            if (activeRead.Succeeded)
            {
                active = activeRead.Checkpoint;
            }
            else if (activeRead.Outcome != CommandOutcome.NoCheckpoint)
            {
                failure = activeRead;
                return false;
            }

            var previousRead = SafeReadPrevious();
            if (previousRead.Succeeded)
            {
                previous = previousRead.Checkpoint;
            }
            else if (previousRead.Outcome != CommandOutcome.NoCheckpoint)
            {
                failure = previousRead;
                return false;
            }

            if (active == null && previous != null)
            {
                failure = CheckpointReadResult.Rejected(CommandOutcome.InvalidCheckpoint, "Checkpoint history is invalid.");
                return false;
            }

            lastActiveCheckpointId = active?.Id;
            persistenceStatus = new PersistenceStatusSnapshot(
                active == null ? PersistenceStatusKind.NoCheckpoint : PersistenceStatusKind.Ready,
                active?.Id,
                previous != null,
                active == null ? "No saved factory is available." : "Saved factory is ready.");
            return true;
        }

        private static bool TryNextGeneration(Checkpoint active, Checkpoint previous, out long generation)
        {
            generation = 0;
            if (active == null && previous == null)
            {
                generation = 1;
                return true;
            }

            try
            {
                var highest = Math.Max(active?.Generation ?? -1, previous?.Generation ?? -1);
                generation = checked(highest + 1);
                return generation >= 0;
            }
            catch (OverflowException)
            {
                return false;
            }
        }

        private CheckpointReadResult SafeReadActive() => SafeRead(() => session.CheckpointStore.ReadActive());
        private CheckpointReadResult SafeReadPrevious() => SafeRead(() => session.CheckpointStore.ReadPrevious());
        private CheckpointReadResult SafeRead(CheckpointId id) => SafeRead(() => session.CheckpointStore.Read(id));

        private static CheckpointReadResult SafeRead(Func<CheckpointReadResult> read)
        {
            try
            {
                var result = read();
                return result ?? CheckpointReadResult.Rejected(CommandOutcome.PersistenceFailed, "Checkpoint storage is unavailable.");
            }
            catch (Exception exception) when (IsExpectedInfrastructureFailure(exception))
            {
                return CheckpointReadResult.Rejected(CommandOutcome.PersistenceFailed, "Checkpoint storage is unavailable.");
            }
        }

        private CommandResult<CheckpointId> SafePromote(Checkpoint checkpoint)
        {
            try
            {
                return session.CheckpointStore.Promote(checkpoint) ??
                       CommandResult<CheckpointId>.Rejected(CommandOutcome.PersistenceFailed, "Checkpoint storage is unavailable.");
            }
            catch (Exception exception) when (IsExpectedInfrastructureFailure(exception))
            {
                return CommandResult<CheckpointId>.Rejected(CommandOutcome.PersistenceFailed, "Checkpoint storage is unavailable.");
            }
        }

        private CommandResult<CheckpointId> SafeRecoverPrevious(Checkpoint checkpoint)
        {
            try
            {
                return session.CheckpointStore.RecoverPrevious(checkpoint) ??
                       CommandResult<CheckpointId>.Rejected(CommandOutcome.PersistenceFailed, "Checkpoint storage is unavailable.");
            }
            catch (Exception exception) when (IsExpectedInfrastructureFailure(exception))
            {
                return CommandResult<CheckpointId>.Rejected(CommandOutcome.PersistenceFailed, "Checkpoint storage is unavailable.");
            }
        }

        private bool TryCaptureUtc(out DateTimeOffset utc)
        {
            utc = default(DateTimeOffset);
            try
            {
                utc = session.UtcClock.GetUtcNow().ToUniversalTime();
                return true;
            }
            catch (Exception exception) when (exception is ArgumentException || exception is InvalidOperationException || exception is OverflowException)
            {
                return false;
            }
        }

        private CommandResult CanReplaceState()
        {
            return session.Revision == long.MaxValue
                ? CommandResult.Rejected(CommandOutcome.InvalidRequest, "The factory revision cannot be advanced.")
                : CommandResult.Success();
        }

        private bool ReadPreviousAvailability()
        {
            var previous = SafeReadPrevious();
            return previous.Succeeded;
        }

        private void InitializeStatus()
        {
            var active = SafeReadActive();
            if (active.Succeeded)
            {
                lastActiveCheckpointId = active.Checkpoint.Id;
                var previous = SafeReadPrevious();
                SetStatus(PersistenceStatusKind.Ready, active.Checkpoint.Id, previous.Succeeded, "Saved factory is ready.");
                return;
            }

            var kind = active.Outcome == CommandOutcome.NoCheckpoint
                ? PersistenceStatusKind.NoCheckpoint
                : PersistenceStatusKind.Failure;
            SetStatus(kind, null, false, SafeReadMessage(active.Outcome, "Checkpoint status is unavailable."));
        }

        private void SetReadFailure(CheckpointReadResult failure, CheckpointId? fallbackId)
        {
            var kind = failure.Outcome == CommandOutcome.NoCheckpoint
                ? PersistenceStatusKind.NoCheckpoint
                : PersistenceStatusKind.Failure;
            SetStatus(kind, fallbackId, persistenceStatus.PreviousCheckpointAvailable,
                SafeReadMessage(failure.Outcome, "Checkpoint storage is unavailable."));
        }

        private void SetStatus(
            PersistenceStatusKind kind,
            CheckpointId? activeId,
            bool previousAvailable,
            string message)
        {
            persistenceStatus = new PersistenceStatusSnapshot(kind, activeId, previousAvailable, message);
            if (activeId.HasValue || kind == PersistenceStatusKind.NoCheckpoint)
            {
                lastActiveCheckpointId = activeId;
            }
        }

        private void ClearPendingReturn()
        {
            pendingSource = null;
            pendingCandidate = null;
            pendingUsedFallback = false;
            capturedReturnUtc = default(DateTimeOffset);
            hasCapturedReturnUtc = false;
        }

        private static bool CheckpointsEquivalent(Checkpoint left, Checkpoint right)
        {
            if (left == null || right == null || left.Id != right.Id || left.Generation != right.Generation ||
                left.DepartureUtc != right.DepartureUtc || left.OfflinePolicyRevision != right.OfflinePolicyRevision ||
                left.ReconciledFromCheckpointId != right.ReconciledFromCheckpointId)
            {
                return false;
            }

            return FactoriesEquivalent(left.CreateFactoryCopy(), right.CreateFactoryCopy());
        }

        private static bool FactoriesEquivalent(FactoryState left, FactoryState right)
        {
            if (left == null || right == null || left.Bounds != right.Bounds || left.Buildings.Count != right.Buildings.Count ||
                !ReferenceEquals(left.Catalog, right.Catalog))
            {
                return false;
            }

            foreach (var pair in left.Buildings)
            {
                BuildingInstanceState other;
                if (!right.Buildings.TryGetValue(pair.Key, out other) || !BuildingsEquivalent(pair.Value, other))
                {
                    return false;
                }
            }

            return left.Occupancy.Count == right.Occupancy.Count && left.Occupancy.All(pair =>
            {
                BuildingInstanceId owner;
                return right.Occupancy.TryGetValue(pair.Key, out owner) && owner == pair.Value;
            });
        }

        private static bool BuildingsEquivalent(BuildingInstanceState left, BuildingInstanceState right)
        {
            if (left.DefinitionId != right.DefinitionId || left.Anchor != right.Anchor ||
                left.OrientationQuarterTurns != right.OrientationQuarterTurns || left.IsActive != right.IsActive ||
                !BufferEquivalent(left.InputBuffer, right.InputBuffer) || !BufferEquivalent(left.OutputBuffer, right.OutputBuffer))
            {
                return false;
            }

            if (left.Production == null || right.Production == null)
            {
                return left.Production == null && right.Production == null;
            }

            return left.Production.RecipeId == right.Production.RecipeId &&
                   left.Production.ProgressTicks == right.Production.ProgressTicks;
        }

        private static bool BufferEquivalent(ResourceBufferState left, ResourceBufferState right)
        {
            if (left == null || right == null)
            {
                return left == null && right == null;
            }

            return left.Quantities.Count == right.Quantities.Count && left.Quantities.All(pair =>
            {
                long quantity;
                long capacity;
                return right.Quantities.TryGetValue(pair.Key, out quantity) && quantity == pair.Value &&
                       left.Definition.Capacities.TryGetValue(pair.Key, out capacity) &&
                       right.Definition.Capacities.TryGetValue(pair.Key, out var rightCapacity) &&
                       capacity == rightCapacity;
            });
        }

        private static string SafeReadMessage(CommandOutcome outcome, string fallback)
        {
            switch (outcome)
            {
                case CommandOutcome.NoCheckpoint:
                    return "No saved factory is available.";
                case CommandOutcome.UnsupportedSaveVersion:
                    return "The saved factory uses an unsupported version.";
                case CommandOutcome.InvalidCheckpoint:
                    return "The saved factory is invalid.";
                case CommandOutcome.PersistenceFailed:
                    return "Checkpoint storage is unavailable.";
                default:
                    return fallback;
            }
        }

        private static string DescribeReconciliationFailure(OfflineReconciliationStatus status)
        {
            switch (status)
            {
                case OfflineReconciliationStatus.MissingReturnTime:
                case OfflineReconciliationStatus.InvalidReturnTime:
                    return "The return time could not be validated.";
                case OfflineReconciliationStatus.InvalidCheckpoint:
                    return "The saved factory could not be validated.";
                case OfflineReconciliationStatus.SimulationFailed:
                    return "The saved factory could not be reconciled.";
                default:
                    return ReturnFailureMessage;
            }
        }

        private static CommandOutcome MapReconciliationOutcome(OfflineReconciliationStatus status)
        {
            switch (status)
            {
                case OfflineReconciliationStatus.MissingReturnTime:
                case OfflineReconciliationStatus.InvalidReturnTime:
                    return CommandOutcome.PersistenceFailed;
                case OfflineReconciliationStatus.InvalidCheckpoint:
                case OfflineReconciliationStatus.SimulationFailed:
                    return CommandOutcome.InvalidCheckpoint;
                default:
                    return CommandOutcome.ReturnBlocked;
            }
        }

        private static bool IsExpectedDomainFailure(Exception exception)
        {
            return exception is ArgumentException || exception is InvalidOperationException ||
                   exception is KeyNotFoundException || exception is OverflowException;
        }

        private static bool IsExpectedInfrastructureFailure(Exception exception)
        {
            return exception is IOException || exception is UnauthorizedAccessException ||
                   exception is SecurityException || exception is InvalidOperationException ||
                   exception is NotSupportedException || exception is PlatformNotSupportedException ||
                   exception is ArgumentException || exception is OverflowException;
        }

        private static CommandResult<PersistenceOperationResult> Succeeded(
            CheckpointId? checkpointId,
            long? generation,
            FactorySnapshot factory,
            OfflineReturnSummary offlineSummary,
            bool usedFallback,
            bool wasNoOp) =>
            CommandResult<PersistenceOperationResult>.Success(new PersistenceOperationResult(
                checkpointId,
                generation,
                factory,
                offlineSummary,
                usedFallback,
                wasNoOp));

        private static CommandResult<PersistenceOperationResult> Rejected(CommandOutcome outcome, string message) =>
            CommandResult<PersistenceOperationResult>.Rejected(outcome, message);

        private sealed class RetrySource
        {
            private RetrySource(Checkpoint checkpoint, CommandOutcome outcome, string message)
            {
                Checkpoint = checkpoint;
                Outcome = outcome;
                Message = message;
            }

            public Checkpoint Checkpoint { get; }
            public CommandOutcome Outcome { get; }
            public string Message { get; }
            public bool Succeeded => Checkpoint != null;

            public static RetrySource Success(Checkpoint checkpoint) => new RetrySource(checkpoint, CommandOutcome.Success, string.Empty);
            public static RetrySource Failure(CommandOutcome outcome, string message) => new RetrySource(null, outcome, message);
        }
    }
}
