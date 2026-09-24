using System;
using System.Collections.Generic;
using System.Linq;
using IdleFactory.Core.Application;
using IdleFactory.Domain;
using IdleFactory.Simulation;
using IdleFactory.Tests.EditMode.Fixtures;
using NUnit.Framework;

namespace IdleFactory.Tests.EditMode
{
    public sealed class OfflineReconciliationTests
    {
        private const long HalfCycleTicks = FoundationFixtures.RecipeDurationTicks / 2;
        private static readonly ResourceId InputId = new ResourceId(FoundationFixtures.InputResourceId);
        private static readonly ResourceId OutputId = new ResourceId(FoundationFixtures.OutputResourceId);
        private static readonly DateTimeOffset DepartureUtc =
            new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

        private readonly OfflineReconciler reconciler = new OfflineReconciler();

        [Test]
        public void ZeroDurationReturnsAnIndependentEquivalentCandidate()
        {
            var factory = FoundationFixtures.CreateD001();
            var source = CreateCheckpoint(factory);

            var result = Reconcile(source, DepartureUtc, 2);

            AssertCandidate(result);
            Assert.That(result.Summary.MeasuredTicks, Is.Zero);
            Assert.That(result.Summary.CreditedTicks, Is.Zero);
            Assert.That(result.Summary.EffectiveTicks, Is.Zero);
            Assert.That(result.CandidateCheckpoint, Is.Not.SameAs(source));
            AssertFactoryEquivalent(factory, result.CandidateCheckpoint.CreateFactoryCopy());
            AssertFactoryState(factory, input: 4, output: 0, progress: 0);
            Assert.That(result.Summary.ResourceDeltas, Is.Empty);
        }

        [TestCase(TimeSpan.TicksPerSecond, 8000000L)]
        [TestCase(62500000L, FoundationFixtures.RecipeDurationTicks)]
        public void UsesPolicyRevisionAndFloorsTheRationalEffectiveTime(long measuredTicks, long effectiveTicks)
        {
            var source = CreateCheckpoint(FoundationFixtures.CreateD001());

            var result = Reconcile(source, DepartureUtc.AddTicks(measuredTicks), 2);

            AssertCandidate(result);
            Assert.That(result.Summary.HasValidInterval, Is.True);
            Assert.That(result.Summary.MeasuredTicks, Is.EqualTo(measuredTicks));
            Assert.That(result.Summary.CreditedTicks, Is.EqualTo(measuredTicks));
            Assert.That(result.Summary.EffectiveTicks, Is.EqualTo(effectiveTicks));
            var expected = ProductionSimulator.Advance(source.CreateFactoryCopy(), effectiveTicks);
            Assert.That(expected.IsSuccess, Is.True);
            AssertFactoryEquivalent(expected.Candidate, result.CandidateCheckpoint.CreateFactoryCopy());
        }

        [TestCase(8, 288000000000L)]
        [TestCase(24, 864000000000L)]
        public void CapsCreditAtEightHoursAndProducesAtMostSixPointFourEffectiveHours(
            int absenceHours,
            long expectedMeasuredTicks)
        {
            var source = CreateCheckpoint(FoundationFixtures.CreateD001());

            var result = Reconcile(source, DepartureUtc.AddHours(absenceHours), 2);

            AssertCandidate(result);
            Assert.That(result.Summary.MeasuredTicks, Is.EqualTo(expectedMeasuredTicks));
            Assert.That(result.Summary.CreditedTicks, Is.EqualTo(TimeSpan.FromHours(8).Ticks));
            Assert.That(result.Summary.EffectiveTicks, Is.EqualTo(TimeSpan.FromHours(6.4).Ticks));
            AssertFactoryState(result.CandidateCheckpoint.CreateFactoryCopy(), input: 2, output: 1, progress: 0);
            Assert.That(result.Summary.Limitations.Single().Kind,
                Is.EqualTo(ProductionBlockerKind.InsufficientOutputCapacity));
        }

        [TestCase(ProductionBlockerKind.Inactive)]
        [TestCase(ProductionBlockerKind.MissingInput)]
        [TestCase(ProductionBlockerKind.InsufficientOutputCapacity)]
        public void BlockedAndInactiveMachinesKeepProgressAndReportTheirLimitation(
            ProductionBlockerKind blockerKind)
        {
            var factory = CreateD001State(
                input: blockerKind == ProductionBlockerKind.MissingInput ? 0 : 4,
                output: blockerKind == ProductionBlockerKind.InsufficientOutputCapacity ? 1 : 0,
                progress: HalfCycleTicks,
                active: blockerKind != ProductionBlockerKind.Inactive);
            var source = CreateCheckpoint(factory);

            var result = Reconcile(source, DepartureUtc.AddSeconds(6.25), 2);

            AssertCandidate(result);
            AssertFactoryState(result.CandidateCheckpoint.CreateFactoryCopy(),
                input: blockerKind == ProductionBlockerKind.MissingInput ? 0 : 4,
                output: blockerKind == ProductionBlockerKind.InsufficientOutputCapacity ? 1 : 0,
                progress: HalfCycleTicks);
            Assert.That(result.Summary.ResourceDeltas, Is.Empty);
            Assert.That(result.Summary.Limitations.Select(item => item.Kind), Does.Contain(blockerKind));
            AssertFactoryState(source.CreateFactoryCopy(),
                input: blockerKind == ProductionBlockerKind.MissingInput ? 0 : 4,
                output: blockerKind == ProductionBlockerKind.InsufficientOutputCapacity ? 1 : 0,
                progress: HalfCycleTicks);
        }

        [Test]
        public void ExistingPartialProgressCanCompleteAndSummaryContainsActualResourceDeltas()
        {
            var factory = CreateD001State(input: 4, output: 0, progress: HalfCycleTicks, active: true);
            var source = CreateCheckpoint(factory);

            var result = Reconcile(source, DepartureUtc.AddTicks(31250000L), 2);

            AssertCandidate(result);
            Assert.That(result.Summary.EffectiveTicks, Is.EqualTo(HalfCycleTicks));
            AssertFactoryState(result.CandidateCheckpoint.CreateFactoryCopy(), input: 2, output: 1, progress: 0);
            Assert.That(result.Summary.ResourceDeltas.Count, Is.EqualTo(2));
            AssertDelta(result, InputId, -2);
            AssertDelta(result, OutputId, 1);
            Assert.That(result.Summary.HasEligibleProduction, Is.True);
        }

        [Test]
        public void OfflineResultMatchesPartitionedSimulationAndEachReturnFloorsIndependently()
        {
            var multiCycle = FoundationFixtures.CreateMultiCycle();
            var source = CreateCheckpoint(multiCycle);
            const long effectiveTicks = 80000000L;
            const long firstPartition = 11234567L;
            const long secondPartition = 68765433L;

            var reconciled = Reconcile(source, DepartureUtc.AddSeconds(10), 2);
            var direct = ProductionSimulator.Advance(multiCycle, effectiveTicks);
            var first = ProductionSimulator.Advance(multiCycle, firstPartition);
            var second = ProductionSimulator.Advance(first.Candidate, secondPartition);

            AssertCandidate(reconciled);
            Assert.That(first.IsSuccess && second.IsSuccess && direct.IsSuccess, Is.True);
            Assert.That(reconciled.Summary.EffectiveTicks, Is.EqualTo(effectiveTicks));
            AssertFactoryEquivalent(direct.Candidate, second.Candidate);
            AssertFactoryEquivalent(direct.Candidate, reconciled.CandidateCheckpoint.CreateFactoryCopy());

            var oneTickFirst = Reconcile(source, DepartureUtc.AddTicks(1), 2);
            var oneTickSecond = Reconcile(
                oneTickFirst.CandidateCheckpoint,
                DepartureUtc.AddTicks(2),
                3);
            var twoTicksTogether = Reconcile(source, DepartureUtc.AddTicks(2), 4);

            AssertCandidate(oneTickFirst);
            AssertCandidate(oneTickSecond);
            AssertCandidate(twoTicksTogether);
            Assert.That(oneTickFirst.Summary.EffectiveTicks, Is.Zero);
            Assert.That(oneTickSecond.Summary.EffectiveTicks, Is.Zero);
            Assert.That(twoTicksTogether.Summary.EffectiveTicks, Is.EqualTo(1));
            AssertFactoryState(oneTickSecond.CandidateCheckpoint.CreateFactoryCopy(), input: 4, output: 0, progress: 0);
            AssertFactoryState(twoTicksTogether.CandidateCheckpoint.CreateFactoryCopy(), input: 4, output: 0, progress: 1);
        }

        [Test]
        public void MissingTimeCannotPromoteAndBackwardsTimeKeepsTheTimestampNondecreasing()
        {
            var source = CreateCheckpoint(FoundationFixtures.CreateD001());

            var missing = reconciler.Reconcile(source, null, CheckpointId(2));
            var backwards = Reconcile(source, DepartureUtc.AddTicks(-1), 3);

            Assert.That(missing.Status, Is.EqualTo(OfflineReconciliationStatus.MissingReturnTime));
            Assert.That(missing.HasCandidate, Is.False);
            Assert.That(missing.Summary, Is.Null);
            AssertCandidate(backwards);
            Assert.That(backwards.Summary.HasValidInterval, Is.False);
            Assert.That(backwards.Summary.MeasuredTicks, Is.Zero);
            Assert.That(backwards.Summary.CreditedTicks, Is.Zero);
            Assert.That(backwards.Summary.EffectiveTicks, Is.Zero);
            Assert.That(backwards.CandidateCheckpoint.DepartureUtc, Is.EqualTo(source.DepartureUtc));
            Assert.That(backwards.CandidateCheckpoint.ReconciledFromCheckpointId, Is.EqualTo(source.Id));
            var durableSummary = backwards.Summary.CreateDurableSummary(backwards.CandidateCheckpoint.Id);
            Assert.That(durableSummary.Outcome, Is.EqualTo(OfflineReturnOutcome.InvalidTime));
            Assert.That(durableSummary.SourceCheckpointId, Is.EqualTo(source.Id));
            Assert.That(durableSummary.ResultCheckpointId, Is.EqualTo(backwards.CandidateCheckpoint.Id));
        }

        [Test]
        public void UnknownPolicyRevisionAndMissingCheckpointAreRejectedWithoutCandidates()
        {
            var factory = FoundationFixtures.CreateD001();
            var unknownPolicy = new Checkpoint(
                CheckpointId(1),
                5,
                DepartureUtc,
                new OfflinePolicyRevision(2),
                null,
                factory);

            var unknown = Reconcile(unknownPolicy, DepartureUtc.AddSeconds(1), 2);
            var missing = reconciler.Reconcile(null, DepartureUtc.AddSeconds(1), CheckpointId(2));

            Assert.That(unknown.Status, Is.EqualTo(OfflineReconciliationStatus.InvalidCheckpoint));
            Assert.That(unknown.HasCandidate, Is.False);
            Assert.That(missing.Status, Is.EqualTo(OfflineReconciliationStatus.InvalidCheckpoint));
            Assert.That(missing.HasCandidate, Is.False);
            AssertFactoryState(factory, input: 4, output: 0, progress: 0);
        }

        [Test]
        public void FallbackCheckpointIsTheOnlySourceForReconciliation()
        {
            var fallbackState = FoundationFixtures.CreateD001();
            var fallback = CreateCheckpoint(fallbackState, id: 7, generation: 11);
            var laterUnsavedState = CreateD001State(input: 2, output: 1, progress: 0, active: true);

            var result = Reconcile(fallback, fallback.DepartureUtc.AddSeconds(2), 8);

            AssertCandidate(result);
            Assert.That(result.CandidateCheckpoint.Generation, Is.EqualTo(12));
            Assert.That(result.CandidateCheckpoint.ReconciledFromCheckpointId, Is.EqualTo(fallback.Id));
            Assert.That(result.Summary.SourceCheckpointId, Is.EqualTo(fallback.Id));
            Assert.That(result.CandidateCheckpoint.DepartureUtc, Is.EqualTo(fallback.DepartureUtc.AddSeconds(2)));
            AssertFactoryState(result.CandidateCheckpoint.CreateFactoryCopy(), input: 4, output: 0, progress: 16000000L);
            AssertFactoryState(laterUnsavedState, input: 2, output: 1, progress: 0);
            AssertFactoryState(fallback.CreateFactoryCopy(), input: 4, output: 0, progress: 0);
        }

        [Test]
        public void RepeatingTheSameReturnIsIdempotentAndDoesNotChangeRetainedCandidates()
        {
            var source = CreateCheckpoint(FoundationFixtures.CreateD001());
            var returnUtc = DepartureUtc.AddSeconds(6.25);

            var first = Reconcile(source, returnUtc, 2);
            var retry = Reconcile(source, returnUtc, 2);
            var afterDurableResult = Reconcile(first.CandidateCheckpoint, returnUtc, 3);

            AssertCandidate(first);
            AssertCandidate(retry);
            AssertCandidate(afterDurableResult);
            Assert.That(first.CandidateCheckpoint.Id, Is.EqualTo(retry.CandidateCheckpoint.Id));
            Assert.That(first.CandidateCheckpoint.Generation, Is.EqualTo(retry.CandidateCheckpoint.Generation));
            AssertFactoryEquivalent(
                first.CandidateCheckpoint.CreateFactoryCopy(),
                retry.CandidateCheckpoint.CreateFactoryCopy());
            Assert.That(first.Summary.EffectiveTicks, Is.EqualTo(retry.Summary.EffectiveTicks));
            Assert.That(first.Summary.ResourceDeltas.Select(delta => delta.Delta),
                Is.EqualTo(retry.Summary.ResourceDeltas.Select(delta => delta.Delta)));
            Assert.That(afterDurableResult.Summary.MeasuredTicks, Is.Zero);
            Assert.That(afterDurableResult.Summary.EffectiveTicks, Is.Zero);
            AssertFactoryEquivalent(
                first.CandidateCheckpoint.CreateFactoryCopy(),
                afterDurableResult.CandidateCheckpoint.CreateFactoryCopy());
            AssertFactoryState(first.CandidateCheckpoint.CreateFactoryCopy(), input: 2, output: 1, progress: 0);
        }

        [Test]
        public void CandidateGenerationOverflowReturnsNoCandidateAndLeavesSourceUnchanged()
        {
            var source = CreateCheckpoint(FoundationFixtures.CreateD001(), generation: long.MaxValue);

            var result = Reconcile(source, DepartureUtc.AddSeconds(6.25), 2);

            Assert.That(result.Status, Is.EqualTo(OfflineReconciliationStatus.InvalidCheckpoint));
            Assert.That(result.HasCandidate, Is.False);
            Assert.That(result.Summary, Is.Null);
            AssertFactoryState(source.CreateFactoryCopy(), input: 4, output: 0, progress: 0);
        }

        [Test]
        public void AFailedDurableIdMatchCannotProduceAPublishedSummary()
        {
            var source = CreateCheckpoint(FoundationFixtures.CreateD001());
            var result = Reconcile(source, DepartureUtc.AddSeconds(1), 2);

            AssertCandidate(result);
            Assert.That(
                () => result.Summary.CreateDurableSummary(CheckpointId(3)),
                Throws.ArgumentException);
        }

        private static OfflineReconciliationResult Reconcile(
            Checkpoint source,
            DateTimeOffset returnUtc,
            int candidateId)
        {
            return new OfflineReconciler().Reconcile(source, returnUtc, CheckpointId(candidateId));
        }

        private static Checkpoint CreateCheckpoint(
            FactoryState factory,
            int id = 1,
            long generation = 5,
            DateTimeOffset? departureUtc = null)
        {
            return new Checkpoint(
                CheckpointId(id),
                generation,
                departureUtc ?? DepartureUtc,
                new OfflinePolicyRevision(DefinitionCatalog.SupportedOfflinePolicyRevision),
                null,
                factory);
        }

        private static CheckpointId CheckpointId(int value) =>
            new CheckpointId(new Guid(value, 0, 0, new byte[8]));

        private static FactoryState CreateD001State(long input, long output, long progress, bool active)
        {
            var source = FoundationFixtures.CreateD001();
            var original = source.Buildings.Values.Single();
            var changed = new BuildingInstanceState(
                original.Id,
                original.Definition,
                original.Anchor,
                original.OrientationQuarterTurns,
                active,
                Buffer(original.InputBuffer.Definition, InputId, input),
                Buffer(original.OutputBuffer.Definition, OutputId, output),
                new ProductionState(original.Production.Definition, progress));
            return new FactoryState(source.Bounds, source.Catalog, new[] { changed });
        }

        private static ResourceBufferState Buffer(BufferDefinition definition, ResourceId resourceId, long quantity)
        {
            return new ResourceBufferState(
                definition,
                new[] { new KeyValuePair<ResourceId, long>(resourceId, quantity) });
        }

        private static void AssertCandidate(OfflineReconciliationResult result)
        {
            Assert.That(result.Status, Is.EqualTo(OfflineReconciliationStatus.CandidateReady));
            Assert.That(result.HasCandidate, Is.True);
            Assert.That(result.CandidateCheckpoint, Is.Not.Null);
            Assert.That(result.Summary, Is.Not.Null);
            Assert.That(result.Summary.SourceCheckpointId, Is.Not.EqualTo(result.Summary.CandidateCheckpointId));
            Assert.That(result.CandidateCheckpoint.Id, Is.EqualTo(result.Summary.CandidateCheckpointId));
        }

        private static void AssertDelta(OfflineReconciliationResult result, ResourceId id, long expected)
        {
            var delta = result.Summary.ResourceDeltas.Single(item => item.ResourceId == id);
            Assert.That(delta.Delta, Is.EqualTo(expected));
        }

        private static void AssertFactoryState(FactoryState factory, long input, long output, long progress)
        {
            var building = factory.Buildings.Values.Single();
            Assert.That(building.InputBuffer.Quantity(InputId), Is.EqualTo(input));
            Assert.That(building.OutputBuffer.Quantity(OutputId), Is.EqualTo(output));
            Assert.That(building.Production.ProgressTicks, Is.EqualTo(progress));
        }

        private static void AssertFactoryEquivalent(FactoryState expected, FactoryState actual)
        {
            Assert.That(actual.Bounds, Is.EqualTo(expected.Bounds));
            var expectedBuildings = expected.Buildings.Values.OrderBy(building => building.Id).ToArray();
            var actualBuildings = actual.Buildings.Values.OrderBy(building => building.Id).ToArray();
            Assert.That(actualBuildings.Select(building => building.Id),
                Is.EqualTo(expectedBuildings.Select(building => building.Id)));
            for (var index = 0; index < expectedBuildings.Length; index++)
            {
                var expectedBuilding = expectedBuildings[index];
                var actualBuilding = actualBuildings[index];
                Assert.That(actualBuilding.InputBuffer.Quantities, Is.EqualTo(expectedBuilding.InputBuffer.Quantities));
                Assert.That(actualBuilding.OutputBuffer.Quantities, Is.EqualTo(expectedBuilding.OutputBuffer.Quantities));
                Assert.That(actualBuilding.Production.ProgressTicks, Is.EqualTo(expectedBuilding.Production.ProgressTicks));
                Assert.That(actualBuilding.IsActive, Is.EqualTo(expectedBuilding.IsActive));
            }
        }
    }
}
