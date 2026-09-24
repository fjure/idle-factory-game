using System;
using System.Collections.Generic;
using System.Linq;
using IdleFactory.Domain;
using IdleFactory.Simulation;

namespace IdleFactory.Core.Application
{
    public enum OfflineReconciliationStatus
    {
        CandidateReady = 0,
        MissingReturnTime = 1,
        InvalidReturnTime = 2,
        InvalidCheckpoint = 3,
        SimulationFailed = 4,
    }

    /// <summary>
    /// An immutable, detached calculation summary. It is provisional until its candidate checkpoint is durable.
    /// </summary>
    public sealed class OfflineReconciliationSummary
    {
        internal OfflineReconciliationSummary(
            bool hasValidInterval,
            bool hasEligibleProduction,
            long measuredTicks,
            long creditedTicks,
            long effectiveTicks,
            CheckpointId sourceCheckpointId,
            CheckpointId candidateCheckpointId,
            IEnumerable<ResourceDeltaSnapshot> resourceDeltas,
            IEnumerable<OfflineLimitationSnapshot> limitations)
        {
            if (measuredTicks < 0 || creditedTicks < 0 || effectiveTicks < 0 || creditedTicks > measuredTicks ||
                effectiveTicks > creditedTicks)
            {
                throw new ArgumentOutOfRangeException(nameof(measuredTicks), "Offline duration values are inconsistent.");
            }

            _ = sourceCheckpointId.Value;
            _ = candidateCheckpointId.Value;
            HasValidInterval = hasValidInterval;
            HasEligibleProduction = hasEligibleProduction;
            MeasuredTicks = measuredTicks;
            CreditedTicks = creditedTicks;
            EffectiveTicks = effectiveTicks;
            SourceCheckpointId = sourceCheckpointId;
            CandidateCheckpointId = candidateCheckpointId;
            ResourceDeltas = CopyList(resourceDeltas, nameof(resourceDeltas));
            Limitations = CopyList(limitations, nameof(limitations));
        }

        public bool HasValidInterval { get; }
        public bool HasEligibleProduction { get; }
        public long MeasuredTicks { get; }
        public long CreditedTicks { get; }
        public long EffectiveTicks { get; }
        public CheckpointId SourceCheckpointId { get; }

        /// <summary>
        /// The ID assigned to the detached candidate. It is not evidence that the checkpoint has been promoted.
        /// </summary>
        public CheckpointId CandidateCheckpointId { get; }

        public IReadOnlyList<ResourceDeltaSnapshot> ResourceDeltas { get; }
        public IReadOnlyList<OfflineLimitationSnapshot> Limitations { get; }

        /// <summary>
        /// Call only after the store confirms promotion of the matching candidate checkpoint.
        /// </summary>
        public OfflineReturnSummary CreateDurableSummary(CheckpointId durableResultCheckpointId)
        {
            _ = durableResultCheckpointId.Value;
            if (durableResultCheckpointId != CandidateCheckpointId)
            {
                throw new ArgumentException(
                    "The durable result ID must match the reconciled candidate checkpoint.",
                    nameof(durableResultCheckpointId));
            }

            var outcome = !HasValidInterval
                ? OfflineReturnOutcome.InvalidTime
                : HasEligibleProduction
                    ? OfflineReturnOutcome.Applied
                    : OfflineReturnOutcome.NoEligibleProduction;

            return new OfflineReturnSummary(
                outcome,
                MeasuredTicks,
                CreditedTicks,
                EffectiveTicks,
                ResourceDeltas,
                Limitations,
                SourceCheckpointId,
                durableResultCheckpointId);
        }

        private static IReadOnlyList<T> CopyList<T>(IEnumerable<T> items, string parameterName) where T : class
        {
            if (items == null)
            {
                throw new ArgumentNullException(parameterName);
            }

            var copy = new List<T>();
            foreach (var item in items)
            {
                if (item == null)
                {
                    throw new ArgumentException("Offline summary collections must not contain null items.", parameterName);
                }

                copy.Add(item);
            }

            return copy.AsReadOnly();
        }
    }

    public sealed class OfflineReconciliationResult
    {
        private OfflineReconciliationResult(
            OfflineReconciliationStatus status,
            Checkpoint candidateCheckpoint,
            OfflineReconciliationSummary summary,
            ProductionAdvanceStatus? simulationFailureStatus)
        {
            Status = status;
            CandidateCheckpoint = candidateCheckpoint;
            Summary = summary;
            SimulationFailureStatus = simulationFailureStatus;
        }

        public OfflineReconciliationStatus Status { get; }
        public bool HasCandidate => CandidateCheckpoint != null;
        public Checkpoint CandidateCheckpoint { get; }
        public OfflineReconciliationSummary Summary { get; }
        public ProductionAdvanceStatus? SimulationFailureStatus { get; }

        internal static OfflineReconciliationResult Candidate(
            Checkpoint checkpoint,
            OfflineReconciliationSummary summary) =>
            new OfflineReconciliationResult(
                OfflineReconciliationStatus.CandidateReady,
                checkpoint ?? throw new ArgumentNullException(nameof(checkpoint)),
                summary ?? throw new ArgumentNullException(nameof(summary)),
                null);

        internal static OfflineReconciliationResult Rejected(OfflineReconciliationStatus status) =>
            new OfflineReconciliationResult(status, null, null, null);

        internal static OfflineReconciliationResult SimulationFailed(ProductionAdvanceStatus status) =>
            new OfflineReconciliationResult(
                OfflineReconciliationStatus.SimulationFailed,
                null,
                null,
                status == ProductionAdvanceStatus.Success
                    ? throw new ArgumentException("A failed simulation must have a failure status.", nameof(status))
                    : status);
    }

    /// <summary>
    /// Calculates a detached offline checkpoint candidate without publishing it or changing its source checkpoint.
    /// </summary>
    public sealed class OfflineReconciler
    {
        public OfflineReconciliationResult Reconcile(
            Checkpoint sourceCheckpoint,
            DateTimeOffset? returnUtc,
            CheckpointId candidateCheckpointId)
        {
            if (sourceCheckpoint == null)
            {
                return OfflineReconciliationResult.Rejected(OfflineReconciliationStatus.InvalidCheckpoint);
            }

            FactoryState sourceFactory;
            OfflinePolicyDefinition policy;
            try
            {
                _ = candidateCheckpointId.Value;
                if (candidateCheckpointId == sourceCheckpoint.Id)
                {
                    return OfflineReconciliationResult.Rejected(OfflineReconciliationStatus.InvalidCheckpoint);
                }

                sourceFactory = sourceCheckpoint.CreateFactoryCopy();
                policy = sourceFactory.Catalog.Resolve(sourceCheckpoint.OfflinePolicyRevision);
            }
            catch (Exception exception) when (IsInvalidCheckpointException(exception))
            {
                return OfflineReconciliationResult.Rejected(OfflineReconciliationStatus.InvalidCheckpoint);
            }

            if (!returnUtc.HasValue)
            {
                return OfflineReconciliationResult.Rejected(OfflineReconciliationStatus.MissingReturnTime);
            }

            DateTimeOffset normalizedReturnUtc;
            try
            {
                normalizedReturnUtc = returnUtc.Value.ToUniversalTime();
            }
            catch (Exception exception) when (exception is ArgumentException || exception is OverflowException)
            {
                return OfflineReconciliationResult.Rejected(OfflineReconciliationStatus.InvalidReturnTime);
            }

            long candidateGeneration;
            try
            {
                candidateGeneration = checked(sourceCheckpoint.Generation + 1);
            }
            catch (OverflowException)
            {
                return OfflineReconciliationResult.Rejected(OfflineReconciliationStatus.InvalidCheckpoint);
            }

            var measuredInterval = normalizedReturnUtc - sourceCheckpoint.DepartureUtc;
            var hasValidInterval = measuredInterval.Ticks >= 0;
            var measuredTicks = hasValidInterval ? measuredInterval.Ticks : 0;
            var creditedTicks = hasValidInterval
                ? Math.Min(measuredTicks, policy.MaxCreditedTicks)
                : 0;

            long effectiveTicks;
            try
            {
                effectiveTicks = checked(creditedTicks * policy.EfficiencyNumerator) /
                                 policy.EfficiencyDenominator;
            }
            catch (OverflowException)
            {
                return OfflineReconciliationResult.SimulationFailed(ProductionAdvanceStatus.ArithmeticOverflow);
            }

            ProductionSimulationSession session;
            var preparationStatus = ProductionSimulator.TryPrepare(sourceFactory, out session);
            if (preparationStatus != ProductionAdvanceStatus.Success)
            {
                return OfflineReconciliationResult.SimulationFailed(preparationStatus);
            }

            var advanceStatus = session.Advance(effectiveTicks);
            if (advanceStatus != ProductionAdvanceStatus.Success)
            {
                return OfflineReconciliationResult.SimulationFailed(advanceStatus);
            }

            ProductionAdvanceResult captured;
            try
            {
                captured = session.CaptureResult();
            }
            catch (OverflowException)
            {
                return OfflineReconciliationResult.SimulationFailed(ProductionAdvanceStatus.ArithmeticOverflow);
            }

            if (!captured.IsSuccess)
            {
                return OfflineReconciliationResult.SimulationFailed(captured.Status);
            }

            var candidateDepartureUtc = normalizedReturnUtc > sourceCheckpoint.DepartureUtc
                ? normalizedReturnUtc
                : sourceCheckpoint.DepartureUtc;

            Checkpoint candidateCheckpoint;
            OfflineReconciliationSummary summary;
            try
            {
                candidateCheckpoint = new Checkpoint(
                    candidateCheckpointId,
                    candidateGeneration,
                    candidateDepartureUtc,
                    sourceCheckpoint.OfflinePolicyRevision,
                    sourceCheckpoint.Id,
                    captured.Candidate);
                summary = CreateSummary(
                    captured.Summary,
                    hasValidInterval,
                    measuredTicks,
                    creditedTicks,
                    effectiveTicks,
                    sourceCheckpoint.Id,
                    candidateCheckpointId);
            }
            catch (OverflowException)
            {
                return OfflineReconciliationResult.SimulationFailed(ProductionAdvanceStatus.ArithmeticOverflow);
            }
            catch (Exception exception) when (IsInvalidCheckpointException(exception))
            {
                return OfflineReconciliationResult.SimulationFailed(ProductionAdvanceStatus.InvalidFactoryState);
            }

            return OfflineReconciliationResult.Candidate(candidateCheckpoint, summary);
        }

        private static OfflineReconciliationSummary CreateSummary(
            ProductionAdvanceSummary simulationSummary,
            bool hasValidInterval,
            long measuredTicks,
            long creditedTicks,
            long effectiveTicks,
            CheckpointId sourceCheckpointId,
            CheckpointId candidateCheckpointId)
        {
            var deltas = new Dictionary<ResourceDeltaKey, long>();
            var limitations = new List<OfflineLimitationSnapshot>();
            var hasEligibleProduction = false;

            foreach (var change in simulationSummary.Buildings)
            {
                if (change.CompletedCycles > 0 || change.InitialProgressTicks != change.FinalProgressTicks)
                {
                    hasEligibleProduction = true;
                }

                foreach (var consumed in change.ConsumedInputs)
                {
                    AddDelta(deltas, change.BuildingId, consumed.ResourceId, -consumed.Amount);
                }

                foreach (var produced in change.ProducedOutputs)
                {
                    AddDelta(deltas, change.BuildingId, produced.ResourceId, produced.Amount);
                }

                foreach (var blocker in change.Blockers)
                {
                    limitations.Add(new OfflineLimitationSnapshot(
                        change.BuildingId,
                        blocker.Kind,
                        blocker.ResourceId));
                }
            }

            var resourceDeltas = deltas
                .Where(pair => pair.Value != 0)
                .OrderBy(pair => pair.Key.BuildingId)
                .ThenBy(pair => pair.Key.ResourceId)
                .Select(pair => new ResourceDeltaSnapshot(
                    pair.Key.BuildingId,
                    pair.Key.ResourceId,
                    pair.Value))
                .ToArray();

            return new OfflineReconciliationSummary(
                hasValidInterval,
                hasEligibleProduction,
                measuredTicks,
                creditedTicks,
                effectiveTicks,
                sourceCheckpointId,
                candidateCheckpointId,
                resourceDeltas,
                limitations);
        }

        private static void AddDelta(
            IDictionary<ResourceDeltaKey, long> deltas,
            BuildingInstanceId buildingId,
            ResourceId resourceId,
            long delta)
        {
            if (delta == 0)
            {
                return;
            }

            var key = new ResourceDeltaKey(buildingId, resourceId);
            long existing;
            deltas.TryGetValue(key, out existing);
            deltas[key] = checked(existing + delta);
        }

        private static bool IsInvalidCheckpointException(Exception exception)
        {
            return exception is ArgumentException || exception is InvalidOperationException ||
                   exception is KeyNotFoundException || exception is OverflowException;
        }

        private readonly struct ResourceDeltaKey : IEquatable<ResourceDeltaKey>
        {
            public ResourceDeltaKey(BuildingInstanceId buildingId, ResourceId resourceId)
            {
                BuildingId = buildingId;
                ResourceId = resourceId;
            }

            public BuildingInstanceId BuildingId { get; }
            public ResourceId ResourceId { get; }

            public bool Equals(ResourceDeltaKey other) =>
                BuildingId == other.BuildingId && ResourceId == other.ResourceId;

            public override bool Equals(object obj) => obj is ResourceDeltaKey other && Equals(other);

            public override int GetHashCode()
            {
                unchecked
                {
                    return (BuildingId.GetHashCode() * 397) ^ ResourceId.GetHashCode();
                }
            }
        }
    }
}
