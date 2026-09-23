using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using IdleFactory.Domain;
using IdleFactory.Tests.EditMode.Fixtures;
using NUnit.Framework;

namespace IdleFactory.Tests.EditMode
{
    public sealed class PlacementTests
    {
        private static readonly BuildingDefinitionId D001Id =
            new BuildingDefinitionId(FoundationFixtures.D001BuildingId);
        private static readonly ResourceId InputId =
            new ResourceId(FoundationFixtures.InputResourceId);
        private static readonly ResourceId OutputId =
            new ResourceId(FoundationFixtures.OutputResourceId);

        [Test]
        public void D001PlacementOccupiesCompleteTwoByTwoFootprintAndReturnsCandidateDetails()
        {
            var original = EmptyCopy(FoundationFixtures.CreateD001());
            var id = Id(10);

            var result = FactoryPlacement.TryPlace(original, id, D001Id, new GridPosition(3, 4), 0);

            Assert.That(result.Outcome, Is.EqualTo(PlacementOutcome.Success));
            Assert.That(result.IsSuccess, Is.True);
            Assert.That(result.Candidate, Is.Not.Null.And.Not.SameAs(original));
            Assert.That(result.PlacedBuilding, Is.SameAs(result.Candidate.Buildings[id]));
            Assert.That(result.PlacedBuilding.Id, Is.EqualTo(id));
            Assert.That(result.PlacedBuilding.DefinitionId, Is.EqualTo(D001Id));
            Assert.That(result.PlacedBuilding.Anchor, Is.EqualTo(new GridPosition(3, 4)));
            Assert.That(result.PlacedBuilding.OrientationQuarterTurns, Is.Zero);
            Assert.That(result.Candidate.Occupancy, Is.EquivalentTo(new Dictionary<GridPosition, BuildingInstanceId>
            {
                [new GridPosition(3, 4)] = id,
                [new GridPosition(4, 4)] = id,
                [new GridPosition(3, 5)] = id,
                [new GridPosition(4, 5)] = id,
            }));
            Assert.That(original.Buildings, Is.Empty);
            Assert.That(original.Occupancy, Is.Empty);
        }

        [TestCase(-1, 0)]
        [TestCase(0, -1)]
        [TestCase(15, 0)]
        [TestCase(0, 15)]
        [TestCase(16, 16)]
        [TestCase(int.MaxValue, int.MaxValue)]
        public void CompleteFootprintCrossingAnyFactoryEdgeIsRejected(int x, int y)
        {
            var original = FoundationFixtures.CreateD001();

            AssertRejectedUnchanged(
                original,
                PlacementOutcome.OutOfBounds,
                () => FactoryPlacement.TryPlace(original, Id(10), D001Id, new GridPosition(x, y), 0));
        }

        [Test]
        public void OneCellOverlapRejectsEntirePlacement()
        {
            var original = FoundationFixtures.CreateD001();

            AssertRejectedUnchanged(
                original,
                PlacementOutcome.Overlap,
                () => FactoryPlacement.TryPlace(original, Id(10), D001Id, new GridPosition(1, 1), 0));
        }

        [Test]
        public void DuplicateInstanceIdIsRejectedBeforeAnyCandidateIsPublished()
        {
            var original = FoundationFixtures.CreateD001();
            var existingId = original.Buildings.Keys.Single();

            AssertRejectedUnchanged(
                original,
                PlacementOutcome.DuplicateInstanceId,
                () => FactoryPlacement.TryPlace(original, existingId, D001Id, new GridPosition(8, 8), 0));
        }

        [Test]
        public void UnknownDefinitionIsTypedRejectionWithoutMutation()
        {
            var original = FoundationFixtures.CreateD001();

            AssertRejectedUnchanged(
                original,
                PlacementOutcome.UnknownDefinition,
                () => FactoryPlacement.TryPlace(
                    original,
                    Id(10),
                    new BuildingDefinitionId("missing.machine"),
                    new GridPosition(8, 8),
                    0));
        }

        [Test]
        public void UnsupportedOrientationIsTypedRejectionWithoutMutation()
        {
            var original = FoundationFixtures.CreateD001();

            AssertRejectedUnchanged(
                original,
                PlacementOutcome.UnsupportedOrientation,
                () => FactoryPlacement.TryPlace(original, Id(10), D001Id, new GridPosition(8, 8), 1));
        }

        [Test]
        public void InvalidStateOrUninitializedIdentifiersAreTypedRejections()
        {
            var original = FoundationFixtures.CreateD001();

            AssertRejectedUnchanged(
                original,
                PlacementOutcome.InvalidRequest,
                () => FactoryPlacement.TryPlace(
                    original,
                    default(BuildingInstanceId),
                    D001Id,
                    new GridPosition(8, 8),
                    0));
            AssertRejectedUnchanged(
                original,
                PlacementOutcome.InvalidRequest,
                () => FactoryPlacement.TryPlace(
                    original,
                    Id(10),
                    default(BuildingDefinitionId),
                    new GridPosition(8, 8),
                    0));

            var nullState = FactoryPlacement.TryPlace(null, Id(10), D001Id, new GridPosition(8, 8), 0);
            Assert.That(nullState.Outcome, Is.EqualTo(PlacementOutcome.InvalidRequest));
            Assert.That(nullState.Candidate, Is.Null);
            Assert.That(nullState.PlacedBuilding, Is.Null);
        }

        [Test]
        public void SameDefinitionPlacementsUseIndependentIdsAndZeroInitializedMutableGraphs()
        {
            var original = EmptyCopy(FoundationFixtures.CreateD001());
            var firstResult = FactoryPlacement.TryPlace(original, Id(10), D001Id, new GridPosition(0, 0), 0);
            var secondResult = FactoryPlacement.TryPlace(firstResult.Candidate, Id(11), D001Id, new GridPosition(4, 4), 0);
            var first = secondResult.Candidate.Buildings[Id(10)];
            var second = secondResult.Candidate.Buildings[Id(11)];

            Assert.That(secondResult.Outcome, Is.EqualTo(PlacementOutcome.Success));
            Assert.That(first.Id, Is.Not.EqualTo(second.Id));
            Assert.That(first.Definition, Is.SameAs(second.Definition));
            Assert.That(first.InputBuffer, Is.Not.SameAs(second.InputBuffer));
            Assert.That(first.OutputBuffer, Is.Not.SameAs(second.OutputBuffer));
            Assert.That(first.Production, Is.Not.SameAs(second.Production));
            Assert.That(first.IsActive, Is.True);
            Assert.That(second.IsActive, Is.True);
            Assert.That(first.InputBuffer.Quantity(InputId), Is.Zero);
            Assert.That(first.OutputBuffer.Quantity(OutputId), Is.Zero);
            Assert.That(first.Production.ProgressTicks, Is.Zero);
            Assert.That(second.InputBuffer.Quantity(InputId), Is.Zero);
            Assert.That(second.OutputBuffer.Quantity(OutputId), Is.Zero);
            Assert.That(second.Production.ProgressTicks, Is.Zero);

            Assert.That(MutateBuffer(first.InputBuffer, InputId, 1), Is.EqualTo(BufferOperationResult.Success));
            Assert.That(first.InputBuffer.Quantity(InputId), Is.EqualTo(1));
            Assert.That(second.InputBuffer.Quantity(InputId), Is.Zero);
        }

        [Test]
        public void PlacementUsesNegativeBoundsWithoutAssumingZeroOrigin()
        {
            var original = EmptyCopy(FoundationFixtures.CreateNegativeCoordinate());

            var result = FactoryPlacement.TryPlace(original, Id(10), D001Id, new GridPosition(-8, -6), 0);

            Assert.That(result.Outcome, Is.EqualTo(PlacementOutcome.Success));
            Assert.That(result.Candidate.Occupancy.Keys, Is.EquivalentTo(new[]
            {
                new GridPosition(-8, -6),
                new GridPosition(-7, -6),
                new GridPosition(-8, -5),
                new GridPosition(-7, -5),
            }));
        }

        [Test]
        public void AlternateNineBySevenFactoryAcceptsItsDeclaredThreeByOneFootprint()
        {
            var fixture = FoundationFixtures.CreateAlternateFootprint();
            var original = EmptyCopy(fixture);
            var definitionId = new BuildingDefinitionId(FoundationFixtures.AlternateBuildingId);

            var result = FactoryPlacement.TryPlace(original, Id(10), definitionId, new GridPosition(8, 9), 0);

            Assert.That(original.Bounds, Is.EqualTo(new FactoryBounds(new GridPosition(2, 3), 9, 7)));
            Assert.That(result.Outcome, Is.EqualTo(PlacementOutcome.Success));
            Assert.That(result.Candidate.Occupancy.Keys, Is.EquivalentTo(new[]
            {
                new GridPosition(8, 9),
                new GridPosition(9, 9),
                new GridPosition(10, 9),
            }));
        }

        [Test]
        public void AlternateThreeByOneFootprintRejectsPartialOverlapWithoutMutation()
        {
            var original = FoundationFixtures.CreateAlternateFootprint();
            var definitionId = new BuildingDefinitionId(FoundationFixtures.AlternateBuildingId);

            AssertRejectedUnchanged(
                original,
                PlacementOutcome.Overlap,
                () => FactoryPlacement.TryPlace(original, Id(10), definitionId, new GridPosition(6, 5), 0));
        }

        [Test]
        public void AlternateThreeByOneFootprintRejectsCrossingBoundsWithoutMutation()
        {
            var original = FoundationFixtures.CreateAlternateFootprint();
            var definitionId = new BuildingDefinitionId(FoundationFixtures.AlternateBuildingId);

            AssertRejectedUnchanged(
                original,
                PlacementOutcome.OutOfBounds,
                () => FactoryPlacement.TryPlace(original, Id(10), definitionId, new GridPosition(9, 9), 0));
        }

        [Test]
        public void EvaluationReturnsCompleteImmutableCellsAndAllApplicableFailuresInOrder()
        {
            var original = FoundationFixtures.CreateD001();
            var buildings = original.Buildings.ToArray();
            var occupancy = original.Occupancy.ToArray();

            var evaluation = FactoryPlacement.Evaluate(
                original,
                D001Id,
                new GridPosition(-1, 0),
                1);

            Assert.That(evaluation.IsValid, Is.False);
            Assert.That(evaluation.OccupiedCells, Is.EqualTo(new[]
            {
                new GridPosition(-1, 0),
                new GridPosition(0, 0),
                new GridPosition(-1, 1),
                new GridPosition(0, 1),
            }));
            Assert.That(evaluation.Failures, Is.EqualTo(new[]
            {
                PlacementOutcome.UnsupportedOrientation,
                PlacementOutcome.OutOfBounds,
                PlacementOutcome.Overlap,
            }));
            Assert.That(() => ((IList<GridPosition>)evaluation.OccupiedCells).Clear(),
                Throws.TypeOf<NotSupportedException>());
            Assert.That(() => ((IList<PlacementOutcome>)evaluation.Failures).Clear(),
                Throws.TypeOf<NotSupportedException>());
            Assert.That(original.Buildings, Is.EqualTo(buildings));
            Assert.That(original.Occupancy, Is.EqualTo(occupancy));
        }

        [Test]
        public void EvaluationCoversInvalidUnknownAndOverflowWithoutPartialCells()
        {
            var original = FoundationFixtures.CreateD001();

            Assert.That(
                FactoryPlacement.Evaluate(null, D001Id, new GridPosition(0, 0), 0).Failures,
                Is.EqualTo(new[] { PlacementOutcome.InvalidRequest }));
            Assert.That(
                FactoryPlacement.Evaluate(
                    original,
                    default(BuildingDefinitionId),
                    new GridPosition(0, 0),
                    0).Failures,
                Is.EqualTo(new[] { PlacementOutcome.InvalidRequest }));
            Assert.That(
                FactoryPlacement.Evaluate(
                    original,
                    new BuildingDefinitionId("missing.machine"),
                    new GridPosition(0, 0),
                    0).Failures,
                Is.EqualTo(new[] { PlacementOutcome.UnknownDefinition }));

            var overflow = FactoryPlacement.Evaluate(
                original,
                D001Id,
                new GridPosition(int.MaxValue, int.MaxValue),
                0);
            Assert.That(overflow.OccupiedCells, Is.Empty);
            Assert.That(overflow.Failures, Is.EqualTo(new[] { PlacementOutcome.OutOfBounds }));
        }

        [Test]
        public void TryPlaceUsesEvaluationCellsAndFirstFailureWhilePreservingIdPrecedence()
        {
            var original = FoundationFixtures.CreateD001();
            var requestedId = Id(10);
            var anchor = new GridPosition(8, 8);
            var evaluation = FactoryPlacement.Evaluate(original, D001Id, anchor, 0);
            var placed = FactoryPlacement.TryPlace(original, requestedId, D001Id, anchor, 0);

            Assert.That(placed.Outcome, Is.EqualTo(PlacementOutcome.Success));
            Assert.That(placed.OccupiedCells, Is.EqualTo(evaluation.OccupiedCells));
            Assert.That(placed.Candidate.Occupancy.Where(pair => pair.Value == requestedId).Select(pair => pair.Key),
                Is.EquivalentTo(evaluation.OccupiedCells));

            var failedEvaluation = FactoryPlacement.Evaluate(original, D001Id, new GridPosition(-1, 0), 1);
            var failed = FactoryPlacement.TryPlace(original, requestedId, D001Id, new GridPosition(-1, 0), 1);
            Assert.That(failed.Outcome, Is.EqualTo(failedEvaluation.Failures[0]));

            var existingId = original.Buildings.Keys.Single();
            var duplicate = FactoryPlacement.TryPlace(
                original,
                existingId,
                new BuildingDefinitionId("missing.machine"),
                anchor,
                0);
            Assert.That(duplicate.Outcome, Is.EqualTo(PlacementOutcome.DuplicateInstanceId));
            Assert.That(duplicate.OccupiedCells, Is.Empty);

            var invalidBeforeDuplicate = FactoryPlacement.TryPlace(
                original,
                existingId,
                default(BuildingDefinitionId),
                anchor,
                0);
            Assert.That(invalidBeforeDuplicate.Outcome, Is.EqualTo(PlacementOutcome.InvalidRequest));
        }

        private static FactoryState EmptyCopy(FactoryState fixture)
        {
            return new FactoryState(fixture.Bounds, fixture.Catalog, Array.Empty<BuildingInstanceState>());
        }

        private static void AssertRejectedUnchanged(
            FactoryState original,
            PlacementOutcome expected,
            Func<PlacementResult> place)
        {
            var buildings = original.Buildings.ToArray();
            var occupancy = original.Occupancy.ToArray();
            var building = original.Buildings.Values.SingleOrDefault();
            var input = building?.InputBuffer?.Quantities.ToArray();
            var output = building?.OutputBuffer?.Quantities.ToArray();
            var active = building?.IsActive;
            var progress = building?.Production?.ProgressTicks;
            var result = place();

            Assert.That(result.Outcome, Is.EqualTo(expected));
            Assert.That(result.IsSuccess, Is.False);
            Assert.That(result.Candidate, Is.Null);
            Assert.That(result.PlacedBuilding, Is.Null);
            Assert.That(original.Buildings, Is.EqualTo(buildings));
            Assert.That(original.Occupancy, Is.EqualTo(occupancy));
            if (building != null)
            {
                Assert.That(original.Buildings[building.Id], Is.SameAs(building));
                Assert.That(building.InputBuffer.Quantities, Is.EqualTo(input));
                Assert.That(building.OutputBuffer.Quantities, Is.EqualTo(output));
                Assert.That(building.IsActive, Is.EqualTo(active));
                Assert.That(building.Production.ProgressTicks, Is.EqualTo(progress));
            }
        }

        private static BuildingInstanceId Id(int value)
        {
            return new BuildingInstanceId(new Guid(value, 0, 0, new byte[8]));
        }

        private static BufferOperationResult MutateBuffer(
            ResourceBufferState buffer,
            ResourceId resourceId,
            long amount)
        {
            var method = typeof(ResourceBufferState).GetMethod("TryAdd", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null);
            return (BufferOperationResult)method.Invoke(buffer, new object[] { resourceId, amount });
        }
    }
}
