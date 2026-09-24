using System;
using System.IO;
using System.Linq;
using System.Security;
using IdleFactory.Domain;
using IdleFactory.Simulation;

namespace IdleFactory.Core.Application
{
    public sealed class FactorySession
    {
        private ProductionSimulationSession productionSession;
        private long revision;

        public FactorySession(
            FactoryState initialState,
            DefinitionCatalog catalog,
            IUtcClock utcClock,
            ICheckpointStore checkpointStore)
        {
            if (initialState == null)
            {
                throw new ArgumentNullException(nameof(initialState));
            }

            Catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
            if (!ReferenceEquals(initialState.Catalog, Catalog))
            {
                throw new ArgumentException(
                    "The session state must reference the session's immutable definition catalog.",
                    nameof(initialState));
            }

            UtcClock = utcClock ?? throw new ArgumentNullException(nameof(utcClock));
            CheckpointStore = checkpointStore ?? throw new ArgumentNullException(nameof(checkpointStore));
            ProductionSimulationSession preparedSession;
            var preparation = ProductionSimulator.TryPrepare(initialState, out preparedSession);
            if (preparation != ProductionAdvanceStatus.Success)
            {
                throw new ArgumentException(
                    "The initial factory state cannot be prepared for production.",
                    nameof(initialState));
            }

            productionSession = preparedSession;
            LifecycleState = LifecycleState.Bootstrapping;
        }

        public DefinitionCatalog Catalog { get; }
        public LifecycleState LifecycleState { get; private set; }
        internal long Revision => revision;

        internal IUtcClock UtcClock { get; }
        internal ICheckpointStore CheckpointStore { get; }

        internal FactoryState CreateStateCopy() => productionSession.CaptureFactory();

        public FactorySnapshot GetFactorySnapshot()
        {
            return CreateFactorySnapshot(CreateStateCopy(), revision);
        }

        public CommandResult<MachineInspectionSnapshot> GetMachineInspection(BuildingInstanceId instanceId)
        {
            try
            {
                _ = instanceId.Value;
            }
            catch (InvalidOperationException)
            {
                return CommandResult<MachineInspectionSnapshot>.Rejected(
                    CommandOutcome.InvalidRequest,
                    "Building instance ID is required.");
            }

            var currentState = CreateStateCopy();
            BuildingInstanceState building;
            if (!currentState.Buildings.TryGetValue(instanceId, out building))
            {
                return CommandResult<MachineInspectionSnapshot>.Rejected(
                    CommandOutcome.NotFound,
                    "Building instance was not found.");
            }

            return CommandResult<MachineInspectionSnapshot>.Success(
                CreateMachineInspectionSnapshot(currentState, revision, building));
        }

        internal CommandResult<ProductionAdvanceResult> AdvanceProduction(long elapsedTicks)
        {
            var admission = Admit(ApplicationOperation.OrdinaryMutation);
            if (!admission.Succeeded)
            {
                return CommandResult<ProductionAdvanceResult>.Rejected(admission.Outcome, admission.Message);
            }

            if (elapsedTicks < 0)
            {
                return CommandResult<ProductionAdvanceResult>.Rejected(
                    CommandOutcome.InvalidElapsedTime,
                    "Elapsed simulation time must be nonnegative.");
            }

            long nextRevision;
            try
            {
                nextRevision = checked(revision + 1);
            }
            catch (OverflowException)
            {
                return CommandResult<ProductionAdvanceResult>.Rejected(
                    CommandOutcome.InvalidRequest,
                    "The factory revision cannot be advanced.");
            }

            var status = productionSession.Advance(elapsedTicks);
            if (status != ProductionAdvanceStatus.Success)
            {
                return CommandResult<ProductionAdvanceResult>.Rejected(
                    MapAdvanceStatus(status),
                    AdvanceStatusMessage(status));
            }

            var captured = productionSession.CaptureResult();
            revision = nextRevision;
            return CommandResult<ProductionAdvanceResult>.Success(captured);
        }

        internal FactorySnapshot CreateFactorySnapshot(FactoryState factory, long snapshotRevision)
        {
            if (factory == null)
            {
                throw new ArgumentNullException(nameof(factory));
            }

            var buildings = factory.Buildings.Values
                .OrderBy(building => building.Id)
                .Select(building => new BuildingSnapshot(
                    building.Id,
                    building.DefinitionId,
                    building.Anchor,
                    building.OrientationQuarterTurns,
                    building.IsActive));

            return new FactorySnapshot(factory.Bounds, snapshotRevision, buildings, factory.Occupancy);
        }

        internal MachineInspectionSnapshot CreateMachineInspectionSnapshot(
            FactoryState factory,
            long snapshotRevision,
            BuildingInstanceState building)
        {
            if (factory == null)
            {
                throw new ArgumentNullException(nameof(factory));
            }

            if (building == null)
            {
                throw new ArgumentNullException(nameof(building));
            }

            BuildingInstanceState ownedBuilding;
            if (!factory.Buildings.TryGetValue(building.Id, out ownedBuilding) ||
                !ReferenceEquals(ownedBuilding, building))
            {
                throw new ArgumentException("The inspected building must belong to the captured factory.", nameof(building));
            }

            var inputs = building.InputBuffer == null
                ? Enumerable.Empty<ResourceQuantitySnapshot>()
                : building.InputBuffer.Quantities.Select(quantity => new ResourceQuantitySnapshot(
                    quantity.Key,
                    quantity.Value,
                    RequiredInput(building, quantity.Key)));
            var outputs = building.OutputBuffer == null
                ? Enumerable.Empty<ResourceQuantitySnapshot>()
                : building.OutputBuffer.Quantities.Select(quantity => new ResourceQuantitySnapshot(
                    quantity.Key,
                    quantity.Value,
                    building.OutputBuffer.FreeCapacity(quantity.Key)));
            var blockers = building.GetProductionBlockers().Select(blocker => new ProductionBlockerSnapshot(
                blocker.Kind,
                blocker.ResourceId,
                blocker.Required,
                blocker.Available));

            return new MachineInspectionSnapshot(
                snapshotRevision,
                building.Id,
                building.DefinitionId,
                building.Production?.RecipeId,
                building.IsActive,
                building.Production?.ProgressTicks ?? 0,
                building.Production?.Definition.DurationTicks ?? 0,
                inputs,
                outputs,
                blockers);
        }

        public CommandResult Admit(ApplicationOperation operation)
        {
            if (operation == ApplicationOperation.StatusQuery)
            {
                return CommandResult.Success();
            }

            if (operation == ApplicationOperation.RecoverPreviousCheckpoint)
            {
                var recovery = AdmitRecoveryPrevious();
                return recovery.Succeeded
                    ? CommandResult.Success()
                    : CommandResult.Rejected(recovery.Outcome, recovery.Message);
            }

            if (LifecycleState == LifecycleState.ReturnBlocked)
            {
                return operation == ApplicationOperation.RetryReturn ||
                       operation == ApplicationOperation.ReloadAuthoritativeGeneration
                    ? CommandResult.Success()
                    : CommandResult.Rejected(
                        CommandOutcome.ReturnBlocked,
                        "Resolve the blocked return before issuing this operation.");
            }

            bool admitted;
            switch (LifecycleState)
            {
                case LifecycleState.Running:
                    admitted = operation == ApplicationOperation.OrdinaryMutation ||
                               operation == ApplicationOperation.Save ||
                               operation == ApplicationOperation.LoadDevelopmentCheckpoint ||
                               operation == ApplicationOperation.RecordSuspension;
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

            return admitted
                ? CommandResult.Success()
                : CommandResult.Rejected(
                    CommandOutcome.LifecycleNoOp,
                $"Operation '{operation}' is not admitted while lifecycle state is '{LifecycleState}'.");
        }

        internal CommandResult<Checkpoint> AdmitRecoveryPrevious()
        {
            if (LifecycleState != LifecycleState.Running && LifecycleState != LifecycleState.ReturnBlocked)
            {
                return CommandResult<Checkpoint>.Rejected(
                    LifecycleState == LifecycleState.ReturnBlocked
                        ? CommandOutcome.ReturnBlocked
                        : CommandOutcome.LifecycleNoOp,
                    "Previous checkpoint recovery is not admitted in the current lifecycle state.");
            }

            CheckpointReadResult previous;
            try
            {
                previous = CheckpointStore.ReadPrevious();
            }
            catch (Exception exception) when (IsExpectedPersistenceFailure(exception))
            {
                return CommandResult<Checkpoint>.Rejected(
                    CommandOutcome.PersistenceFailed,
                    "Checkpoint storage is unavailable.");
            }

            if (previous == null)
            {
                return CommandResult<Checkpoint>.Rejected(
                    CommandOutcome.PersistenceFailed,
                    "Checkpoint storage is unavailable.");
            }

            return previous.Succeeded
                ? CommandResult<Checkpoint>.Success(previous.Checkpoint)
                : CommandResult<Checkpoint>.Rejected(
                    previous.Outcome,
                    previous.Outcome == CommandOutcome.NoCheckpoint
                        ? "No previous checkpoint is available."
                        : "Checkpoint storage is unavailable.");
        }

        internal CommandResult TransitionTo(LifecycleState nextState)
        {
            if (LifecycleState == nextState)
            {
                return CommandResult.Rejected(CommandOutcome.LifecycleNoOp, "Lifecycle state is already current.");
            }

            if (!CanTransition(LifecycleState, nextState))
            {
                return CommandResult.Rejected(
                    CommandOutcome.InvalidRequest,
                    $"Lifecycle transition from '{LifecycleState}' to '{nextState}' is not valid.");
            }

            LifecycleState = nextState;
            return CommandResult.Success();
        }

        internal CommandResult ReplaceState(FactoryState candidate)
        {
            if (candidate == null)
            {
                throw new ArgumentNullException(nameof(candidate));
            }

            if (!ReferenceEquals(candidate.Catalog, Catalog))
            {
                throw new ArgumentException(
                    "Published state must reference the session's immutable definition catalog.",
                    nameof(candidate));
            }

            long nextRevision;
            try
            {
                nextRevision = checked(revision + 1);
            }
            catch (OverflowException)
            {
                return CommandResult.Rejected(
                    CommandOutcome.InvalidRequest,
                    "The factory revision cannot be advanced.");
            }

            ProductionSimulationSession replacementSession;
            var preparation = ProductionSimulator.TryPrepare(candidate, out replacementSession);
            if (preparation != ProductionAdvanceStatus.Success)
            {
                return CommandResult.Rejected(
                    MapAdvanceStatus(preparation),
                    AdvanceStatusMessage(preparation));
            }

            productionSession = replacementSession;
            revision = nextRevision;
            return CommandResult.Success();
        }

        private static CommandOutcome MapAdvanceStatus(ProductionAdvanceStatus status)
        {
            switch (status)
            {
                case ProductionAdvanceStatus.InvalidElapsedTicks:
                    return CommandOutcome.InvalidElapsedTime;
                case ProductionAdvanceStatus.ArithmeticOverflow:
                case ProductionAdvanceStatus.InvalidFactoryState:
                    return CommandOutcome.InvalidRequest;
                default:
                    throw new ArgumentOutOfRangeException(nameof(status), status, "Unsupported simulation status.");
            }
        }

        private static string AdvanceStatusMessage(ProductionAdvanceStatus status)
        {
            switch (status)
            {
                case ProductionAdvanceStatus.InvalidElapsedTicks:
                    return "Elapsed simulation time must be nonnegative.";
                case ProductionAdvanceStatus.ArithmeticOverflow:
                    return "The production calculation exceeded its supported numeric range.";
                case ProductionAdvanceStatus.InvalidFactoryState:
                    return "The factory state is not valid for production.";
                default:
                    throw new ArgumentOutOfRangeException(nameof(status), status, "Unsupported simulation status.");
            }
        }

        private static bool CanTransition(LifecycleState current, LifecycleState next)
        {
            switch (current)
            {
                case LifecycleState.Bootstrapping:
                    return next == LifecycleState.Loading;
                case LifecycleState.Loading:
                    return next == LifecycleState.Running || next == LifecycleState.Reconciling;
                case LifecycleState.Running:
                    return next == LifecycleState.Checkpointing;
                case LifecycleState.Checkpointing:
                    return next == LifecycleState.Running || next == LifecycleState.Suspended;
                case LifecycleState.Suspended:
                    return next == LifecycleState.Reconciling;
                case LifecycleState.Reconciling:
                    return next == LifecycleState.Running || next == LifecycleState.ReturnBlocked;
                case LifecycleState.ReturnBlocked:
                    return next == LifecycleState.Reconciling || next == LifecycleState.Loading;
                default:
                    return false;
            }
        }

        private static bool IsExpectedPersistenceFailure(Exception exception)
        {
            return exception is IOException || exception is UnauthorizedAccessException ||
                   exception is SecurityException || exception is InvalidOperationException ||
                   exception is NotSupportedException || exception is ArgumentException ||
                   exception is OverflowException;
        }

        private static long RequiredInput(BuildingInstanceState building, ResourceId resourceId)
        {
            if (building.Production == null)
            {
                return 0;
            }

            foreach (var input in building.Production.Definition.Inputs)
            {
                if (input.ResourceId == resourceId)
                {
                    return input.Amount;
                }
            }

            return 0;
        }
    }
}
