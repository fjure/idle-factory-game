using System;
using System.Linq;
using IdleFactory.Domain;

namespace IdleFactory.Core.Application
{
    public sealed class FactorySession
    {
        private FactoryState currentState;
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
            currentState = initialState.Copy();
            LifecycleState = LifecycleState.Bootstrapping;
        }

        public DefinitionCatalog Catalog { get; }
        public LifecycleState LifecycleState { get; private set; }

        internal IUtcClock UtcClock { get; }
        internal ICheckpointStore CheckpointStore { get; }

        internal FactoryState CreateStateCopy() => currentState.Copy();

        public FactorySnapshot GetFactorySnapshot()
        {
            var buildings = currentState.Buildings.Values
                .OrderBy(building => building.Id)
                .Select(building => new BuildingSnapshot(
                    building.Id,
                    building.DefinitionId,
                    building.Anchor,
                    building.OrientationQuarterTurns,
                    building.IsActive));

            return new FactorySnapshot(currentState.Bounds, revision, buildings, currentState.Occupancy);
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

            BuildingInstanceState building;
            if (!currentState.Buildings.TryGetValue(instanceId, out building))
            {
                return CommandResult<MachineInspectionSnapshot>.Rejected(
                    CommandOutcome.NotFound,
                    "Building instance was not found.");
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

            return CommandResult<MachineInspectionSnapshot>.Success(new MachineInspectionSnapshot(
                revision,
                building.Id,
                building.DefinitionId,
                building.Production?.RecipeId,
                building.IsActive,
                building.Production?.ProgressTicks ?? 0,
                building.Production?.Definition.DurationTicks ?? 0,
                inputs,
                outputs,
                blockers));
        }

        public CommandResult Admit(ApplicationOperation operation)
        {
            if (operation == ApplicationOperation.StatusQuery)
            {
                return CommandResult.Success();
            }

            if (operation == ApplicationOperation.RecoverPreviousCheckpoint &&
                (LifecycleState == LifecycleState.Running || LifecycleState == LifecycleState.ReturnBlocked))
            {
                var previous = CheckpointStore.ReadPrevious();
                return previous.Succeeded
                    ? CommandResult.Success()
                    : CommandResult.Rejected(previous.Outcome, previous.Message);
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

        internal void ReplaceState(FactoryState candidate)
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

            currentState = candidate.Copy();
            revision = checked(revision + 1);
        }

        private static bool CanTransition(LifecycleState current, LifecycleState next)
        {
            switch (current)
            {
                case LifecycleState.Bootstrapping:
                    return next == LifecycleState.Loading;
                case LifecycleState.Loading:
                    return next == LifecycleState.Running;
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
