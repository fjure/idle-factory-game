using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using IdleFactory.Domain;

namespace IdleFactory.Core.Application
{
    public enum CommandOutcome
    {
        Success = 0,
        InvalidRequest = 1,
        NotFound = 2,
        OutOfBounds = 3,
        Occupied = 4,
        InsufficientResource = 5,
        InsufficientCapacity = 6,
        InvalidDefinition = 7,
        InvalidElapsedTime = 8,
        NoCheckpoint = 9,
        UnsupportedSaveVersion = 10,
        InvalidCheckpoint = 11,
        PersistenceFailed = 12,
        ReturnBlocked = 13,
        NoOfflineCredit = 14,
        LifecycleNoOp = 15,
    }

    public sealed class CommandResult
    {
        private CommandResult(CommandOutcome outcome, string message)
        {
            Outcome = outcome;
            Message = message ?? string.Empty;
        }

        public CommandOutcome Outcome { get; }
        public string Message { get; }
        public bool Succeeded => Outcome == CommandOutcome.Success;

        public static CommandResult Success() => new CommandResult(CommandOutcome.Success, string.Empty);

        public static CommandResult Rejected(CommandOutcome outcome, string message)
        {
            if (outcome == CommandOutcome.Success)
            {
                throw new ArgumentException("A rejected result requires a non-success outcome.", nameof(outcome));
            }

            return new CommandResult(outcome, message);
        }
    }

    public sealed class CommandResult<T>
    {
        private CommandResult(CommandOutcome outcome, T value, string message)
        {
            Outcome = outcome;
            Value = value;
            Message = message ?? string.Empty;
        }

        public CommandOutcome Outcome { get; }
        public T Value { get; }
        public string Message { get; }
        public bool Succeeded => Outcome == CommandOutcome.Success;

        public static CommandResult<T> Success(T value) =>
            new CommandResult<T>(CommandOutcome.Success, value, string.Empty);

        public static CommandResult<T> Rejected(CommandOutcome outcome, string message)
        {
            if (outcome == CommandOutcome.Success)
            {
                throw new ArgumentException("A rejected result requires a non-success outcome.", nameof(outcome));
            }

            return new CommandResult<T>(outcome, default(T), message);
        }
    }

    public enum LifecycleState
    {
        Bootstrapping = 0,
        Loading = 1,
        Running = 2,
        Checkpointing = 3,
        Suspended = 4,
        Reconciling = 5,
        ReturnBlocked = 6,
    }

    public enum ApplicationOperation
    {
        OrdinaryMutation = 0,
        Save = 1,
        LoadDevelopmentCheckpoint = 2,
        RecordSuspension = 3,
        ResumeAndReconcile = 4,
        RetryReturn = 5,
        ReloadAuthoritativeGeneration = 6,
        RecoverPreviousCheckpoint = 7,
        StatusQuery = 8,
    }

    public sealed class BuildingSnapshot
    {
        public BuildingSnapshot(
            BuildingInstanceId instanceId,
            BuildingDefinitionId definitionId,
            GridPosition anchor,
            int orientationQuarterTurns,
            bool isActive)
        {
            _ = instanceId.Value;
            _ = definitionId.Value;
            InstanceId = instanceId;
            DefinitionId = definitionId;
            Anchor = anchor;
            OrientationQuarterTurns = orientationQuarterTurns;
            IsActive = isActive;
        }

        public BuildingInstanceId InstanceId { get; }
        public BuildingDefinitionId DefinitionId { get; }
        public GridPosition Anchor { get; }
        public int OrientationQuarterTurns { get; }
        public bool IsActive { get; }
    }

    public sealed class FactorySnapshot
    {
        public FactorySnapshot(
            FactoryBounds bounds,
            long revision,
            IEnumerable<BuildingSnapshot> buildings,
            IEnumerable<KeyValuePair<GridPosition, BuildingInstanceId>> occupancy)
        {
            _ = bounds.Width;
            if (revision < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(revision));
            }

            Bounds = bounds;
            Revision = revision;
            Buildings = CopyList(buildings, nameof(buildings));
            Occupancy = CopyDictionary(occupancy, nameof(occupancy));
        }

        public FactoryBounds Bounds { get; }
        public long Revision { get; }
        public IReadOnlyList<BuildingSnapshot> Buildings { get; }
        public IReadOnlyDictionary<GridPosition, BuildingInstanceId> Occupancy { get; }

        private static IReadOnlyList<BuildingSnapshot> CopyList(
            IEnumerable<BuildingSnapshot> source,
            string parameterName)
        {
            if (source == null)
            {
                throw new ArgumentNullException(parameterName);
            }

            var copy = new List<BuildingSnapshot>();
            foreach (var item in source)
            {
                if (item == null)
                {
                    throw new ArgumentException("Snapshot collections must not contain null.", parameterName);
                }

                copy.Add(item);
            }

            return new ReadOnlyCollection<BuildingSnapshot>(copy);
        }

        private static IReadOnlyDictionary<GridPosition, BuildingInstanceId> CopyDictionary(
            IEnumerable<KeyValuePair<GridPosition, BuildingInstanceId>> source,
            string parameterName)
        {
            if (source == null)
            {
                throw new ArgumentNullException(parameterName);
            }

            var copy = new Dictionary<GridPosition, BuildingInstanceId>();
            foreach (var item in source)
            {
                _ = item.Value.Value;
                copy.Add(item.Key, item.Value);
            }

            return new ReadOnlyDictionary<GridPosition, BuildingInstanceId>(copy);
        }
    }

    public sealed class ResourceQuantitySnapshot
    {
        public ResourceQuantitySnapshot(ResourceId resourceId, long quantity, long requiredOrFreeCapacity)
        {
            _ = resourceId.Value;
            if (quantity < 0 || requiredOrFreeCapacity < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(quantity), "Resource projection values must be nonnegative.");
            }

            ResourceId = resourceId;
            Quantity = quantity;
            RequiredOrFreeCapacity = requiredOrFreeCapacity;
        }

        public ResourceId ResourceId { get; }
        public long Quantity { get; }
        public long RequiredOrFreeCapacity { get; }
    }

    public sealed class ProductionBlockerSnapshot
    {
        public ProductionBlockerSnapshot(
            ProductionBlockerKind kind,
            ResourceId? resourceId,
            long required,
            long available)
        {
            if (required < 0 || available < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(required), "Blocker quantities must be nonnegative.");
            }

            Kind = kind;
            ResourceId = resourceId;
            Required = required;
            Available = available;
        }

        public ProductionBlockerKind Kind { get; }
        public ResourceId? ResourceId { get; }
        public long Required { get; }
        public long Available { get; }
    }

    public sealed class MachineInspectionSnapshot
    {
        public MachineInspectionSnapshot(
            long revision,
            BuildingInstanceId instanceId,
            BuildingDefinitionId definitionId,
            RecipeId? recipeId,
            bool isActive,
            long progressTicks,
            long durationTicks,
            IEnumerable<ResourceQuantitySnapshot> inputs,
            IEnumerable<ResourceQuantitySnapshot> outputs,
            IEnumerable<ProductionBlockerSnapshot> blockers)
        {
            if (revision < 0 || progressTicks < 0 || durationTicks < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(revision), "Inspection timing and revision values must be nonnegative.");
            }

            _ = instanceId.Value;
            _ = definitionId.Value;
            Revision = revision;
            InstanceId = instanceId;
            DefinitionId = definitionId;
            RecipeId = recipeId;
            IsActive = isActive;
            ProgressTicks = progressTicks;
            DurationTicks = durationTicks;
            Inputs = CopyList(inputs, nameof(inputs));
            Outputs = CopyList(outputs, nameof(outputs));
            Blockers = CopyList(blockers, nameof(blockers));
        }

        public long Revision { get; }
        public BuildingInstanceId InstanceId { get; }
        public BuildingDefinitionId DefinitionId { get; }
        public RecipeId? RecipeId { get; }
        public bool IsActive { get; }
        public long ProgressTicks { get; }
        public long DurationTicks { get; }
        public IReadOnlyList<ResourceQuantitySnapshot> Inputs { get; }
        public IReadOnlyList<ResourceQuantitySnapshot> Outputs { get; }
        public IReadOnlyList<ProductionBlockerSnapshot> Blockers { get; }

        private static IReadOnlyList<T> CopyList<T>(IEnumerable<T> source, string parameterName) where T : class
        {
            if (source == null)
            {
                throw new ArgumentNullException(parameterName);
            }

            var copy = new List<T>();
            foreach (var item in source)
            {
                if (item == null)
                {
                    throw new ArgumentException("Snapshot collections must not contain null.", parameterName);
                }

                copy.Add(item);
            }

            return new ReadOnlyCollection<T>(copy);
        }
    }

    public enum OfflineReturnOutcome
    {
        Applied = 0,
        NoCheckpoint = 1,
        InvalidTime = 2,
        PersistenceFailed = 3,
        NoEligibleProduction = 4,
    }

    public sealed class ResourceDeltaSnapshot
    {
        public ResourceDeltaSnapshot(BuildingInstanceId ownerId, ResourceId resourceId, long delta)
        {
            _ = ownerId.Value;
            _ = resourceId.Value;
            OwnerId = ownerId;
            ResourceId = resourceId;
            Delta = delta;
        }

        public BuildingInstanceId OwnerId { get; }
        public ResourceId ResourceId { get; }
        public long Delta { get; }
    }

    public sealed class OfflineLimitationSnapshot
    {
        public OfflineLimitationSnapshot(BuildingInstanceId ownerId, ProductionBlockerKind kind, ResourceId? resourceId)
        {
            _ = ownerId.Value;
            OwnerId = ownerId;
            Kind = kind;
            ResourceId = resourceId;
        }

        public BuildingInstanceId OwnerId { get; }
        public ProductionBlockerKind Kind { get; }
        public ResourceId? ResourceId { get; }
    }

    public sealed class OfflineReturnSummary
    {
        public OfflineReturnSummary(
            OfflineReturnOutcome outcome,
            long measuredTicks,
            long creditedTicks,
            long effectiveTicks,
            IEnumerable<ResourceDeltaSnapshot> resourceDeltas,
            IEnumerable<OfflineLimitationSnapshot> limitations,
            CheckpointId? sourceCheckpointId,
            CheckpointId? resultCheckpointId)
        {
            if (measuredTicks < 0 || creditedTicks < 0 || effectiveTicks < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(measuredTicks), "Offline durations must be nonnegative.");
            }

            Outcome = outcome;
            MeasuredTicks = measuredTicks;
            CreditedTicks = creditedTicks;
            EffectiveTicks = effectiveTicks;
            ResourceDeltas = CopyList(resourceDeltas, nameof(resourceDeltas));
            Limitations = CopyList(limitations, nameof(limitations));
            SourceCheckpointId = sourceCheckpointId;
            ResultCheckpointId = resultCheckpointId;
        }

        public OfflineReturnOutcome Outcome { get; }
        public long MeasuredTicks { get; }
        public long CreditedTicks { get; }
        public long EffectiveTicks { get; }
        public IReadOnlyList<ResourceDeltaSnapshot> ResourceDeltas { get; }
        public IReadOnlyList<OfflineLimitationSnapshot> Limitations { get; }
        public CheckpointId? SourceCheckpointId { get; }
        public CheckpointId? ResultCheckpointId { get; }

        private static IReadOnlyList<T> CopyList<T>(IEnumerable<T> source, string parameterName) where T : class
        {
            if (source == null)
            {
                throw new ArgumentNullException(parameterName);
            }

            var copy = new List<T>();
            foreach (var item in source)
            {
                if (item == null)
                {
                    throw new ArgumentException("Snapshot collections must not contain null.", parameterName);
                }

                copy.Add(item);
            }

            return new ReadOnlyCollection<T>(copy);
        }
    }

    public enum PersistenceStatusKind
    {
        NoCheckpoint = 0,
        Ready = 1,
        Writing = 2,
        Failure = 3,
        Recovered = 4,
    }

    public sealed class PersistenceStatusSnapshot
    {
        public PersistenceStatusSnapshot(
            PersistenceStatusKind status,
            CheckpointId? activeCheckpointId,
            bool previousCheckpointAvailable,
            string message)
        {
            Status = status;
            ActiveCheckpointId = activeCheckpointId;
            PreviousCheckpointAvailable = previousCheckpointAvailable;
            Message = message ?? string.Empty;
        }

        public PersistenceStatusKind Status { get; }
        public CheckpointId? ActiveCheckpointId { get; }
        public bool PreviousCheckpointAvailable { get; }
        public string Message { get; }
    }
}
