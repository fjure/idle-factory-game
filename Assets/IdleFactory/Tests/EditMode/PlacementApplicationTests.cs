using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using IdleFactory.Core.Application;
using IdleFactory.Domain;
using IdleFactory.Tests.EditMode.Fixtures;
using NUnit.Framework;

namespace IdleFactory.Tests.EditMode
{
    public sealed class PlacementApplicationTests
    {
        private static readonly BuildingDefinitionId D001Id =
            new BuildingDefinitionId(FoundationFixtures.D001BuildingId);

        [TestCase(LifecycleState.Bootstrapping, CommandOutcome.LifecycleNoOp)]
        [TestCase(LifecycleState.ReturnBlocked, CommandOutcome.ReturnBlocked)]
        public void PlacementObeysOrdinaryMutationAdmissionWithoutConsumingIdentity(
            LifecycleState lifecycle,
            CommandOutcome expected)
        {
            var ids = new TestIdSource(Id(10));
            var commands = CreateCommands(FoundationFixtures.CreateD001(), ids, lifecycle);

            var result = commands.PlaceBuilding(D001Id, new GridPosition(8, 8), 0);

            Assert.That(result.Outcome, Is.EqualTo(expected));
            Assert.That(ids.CallCount, Is.Zero);
            Assert.That(commands.GetFactorySnapshot().Revision, Is.Zero);
        }

        [Test]
        public void ValidPlacementPublishesOnceAndReturnsStableDefensiveResult()
        {
            var expectedId = Id(10);
            var commands = CreateCommands(FoundationFixtures.CreateD001(), new TestIdSource(expectedId));

            var result = commands.PlaceBuilding(D001Id, new GridPosition(8, 9), 0);
            var snapshot = commands.GetFactorySnapshot();

            Assert.That(result.Succeeded, Is.True);
            Assert.That(result.Value.InstanceId, Is.EqualTo(expectedId));
            Assert.That(result.Value.Revision, Is.EqualTo(1));
            Assert.That(result.Value.OccupiedCells, Is.EqualTo(new[]
            {
                new GridPosition(8, 9),
                new GridPosition(9, 9),
                new GridPosition(8, 10),
                new GridPosition(9, 10),
            }));
            Assert.That(snapshot.Revision, Is.EqualTo(1));
            Assert.That(snapshot.Buildings.Count, Is.EqualTo(2));
            Assert.That(snapshot.Occupancy.Where(pair => pair.Value == expectedId).Select(pair => pair.Key),
                Is.EquivalentTo(result.Value.OccupiedCells));
            Assert.That(() => ((IList<GridPosition>)result.Value.OccupiedCells).Clear(),
                Throws.TypeOf<NotSupportedException>());
        }

        [TestCase("invalid-definition", 8, 8, 0, CommandOutcome.NotFound)]
        [TestCase("orientation", 8, 8, 1, CommandOutcome.InvalidDefinition)]
        [TestCase("bounds", 15, 15, 0, CommandOutcome.OutOfBounds)]
        [TestCase("overlap", 1, 1, 0, CommandOutcome.Occupied)]
        public void DomainFailuresMapToTypedOutcomesWithoutPublishing(
            string scenario,
            int x,
            int y,
            int orientation,
            CommandOutcome expected)
        {
            var commands = CreateCommands(FoundationFixtures.CreateD001(), new TestIdSource(Id(10)));
            var definitionId = scenario == "invalid-definition"
                ? new BuildingDefinitionId("missing.machine")
                : D001Id;
            var before = commands.GetFactorySnapshot();

            var result = commands.PlaceBuilding(definitionId, new GridPosition(x, y), orientation);

            Assert.That(result.Outcome, Is.EqualTo(expected));
            AssertUnchanged(before, commands.GetFactorySnapshot());
        }

        [Test]
        public void InvalidDefinitionIdentifierMapsToInvalidRequestWithoutPublishing()
        {
            var commands = CreateCommands(FoundationFixtures.CreateD001(), new TestIdSource(Id(10)));

            var result = commands.PlaceBuilding(default(BuildingDefinitionId), new GridPosition(8, 8), 0);

            Assert.That(result.Outcome, Is.EqualTo(CommandOutcome.InvalidRequest));
            Assert.That(commands.GetFactorySnapshot().Revision, Is.Zero);
        }

        [Test]
        public void IdSourceEmptyDuplicateAndFailureCannotPublish()
        {
            var fixture = FoundationFixtures.CreateD001();
            var existingId = fixture.Buildings.Keys.Single();
            var cases = new[]
            {
                new { Source = new TestIdSource(default(BuildingInstanceId)), Expected = CommandOutcome.InvalidRequest },
                new { Source = new TestIdSource(existingId), Expected = CommandOutcome.InvalidRequest },
                new { Source = new TestIdSource(CommandOutcome.PersistenceFailed), Expected = CommandOutcome.PersistenceFailed },
            };

            foreach (var testCase in cases)
            {
                var commands = CreateCommands(fixture, testCase.Source);
                var result = commands.PlaceBuilding(D001Id, new GridPosition(8, 8), 0);

                Assert.That(result.Outcome, Is.EqualTo(testCase.Expected));
                Assert.That(testCase.Source.CallCount, Is.EqualTo(1));
                Assert.That(commands.GetFactorySnapshot().Revision, Is.Zero);
                Assert.That(commands.GetFactorySnapshot().Buildings.Count, Is.EqualTo(1));
            }
        }

        [Test]
        public void PreviewReportsValidOverlapAndOutOfBoundsWithoutMutationOrIdentity()
        {
            var ids = new TestIdSource(Id(10));
            var commands = CreateCommands(FoundationFixtures.CreateD001(), ids);
            var before = commands.GetFactorySnapshot();

            var valid = commands.GetPlacementPreview(D001Id, new GridPosition(8, 8), 0);
            var overlap = commands.GetPlacementPreview(D001Id, new GridPosition(1, 1), 0);
            var outOfBounds = commands.GetPlacementPreview(D001Id, new GridPosition(15, 15), 0);

            Assert.That(valid.IsValid, Is.True);
            Assert.That(valid.Cells, Has.Count.EqualTo(4));
            Assert.That(overlap.Rejections, Is.EqualTo(new[] { PlacementPreviewRejection.Occupied }));
            Assert.That(outOfBounds.Cells, Has.Count.EqualTo(4));
            Assert.That(outOfBounds.Rejections, Is.EqualTo(new[] { PlacementPreviewRejection.OutOfBounds }));
            Assert.That(ids.CallCount, Is.Zero);
            AssertUnchanged(before, commands.GetFactorySnapshot());
        }

        [Test]
        public void PreviewUsesAlternateAndNegativeFixtureDefinitionsRatherThanD001Dimensions()
        {
            var alternate = CreateCommands(
                FoundationFixtures.CreateAlternateFootprint(),
                new TestIdSource(Id(10)));
            var negativeFixture = FoundationFixtures.CreateNegativeCoordinate();
            var negative = CreateCommands(
                new FactoryState(
                    negativeFixture.Bounds,
                    negativeFixture.Catalog,
                    Array.Empty<BuildingInstanceState>()),
                new TestIdSource(Id(10)));

            var alternatePreview = alternate.GetPlacementPreview(
                new BuildingDefinitionId(FoundationFixtures.AlternateBuildingId),
                new GridPosition(8, 9),
                0);
            var negativePreview = negative.GetPlacementPreview(D001Id, new GridPosition(-8, -6), 0);

            Assert.That(alternatePreview.IsValid, Is.True);
            Assert.That(alternatePreview.Cells, Is.EqualTo(new[]
            {
                new GridPosition(8, 9),
                new GridPosition(9, 9),
                new GridPosition(10, 9),
            }));
            Assert.That(negativePreview.IsValid, Is.True);
            Assert.That(negativePreview.Cells, Has.Member(new GridPosition(-8, -6)));
            Assert.That(negativePreview.Cells, Has.Count.EqualTo(4));
        }

        [Test]
        public void PreviewReportsUnknownOrientationOverflowAndAllApplicableReasonsInOrder()
        {
            var commands = CreateCommands(FoundationFixtures.CreateD001(), new TestIdSource(Id(10)));

            var unknown = commands.GetPlacementPreview(
                new BuildingDefinitionId("missing.machine"),
                new GridPosition(0, 0),
                0);
            var invalidOrientation = commands.GetPlacementPreview(D001Id, new GridPosition(8, 8), 4);
            var overflow = commands.GetPlacementPreview(
                D001Id,
                new GridPosition(int.MaxValue, int.MaxValue),
                0);
            var all = commands.GetPlacementPreview(D001Id, new GridPosition(-1, 0), 1);

            Assert.That(unknown.Cells, Is.Empty);
            Assert.That(unknown.Rejections, Is.EqualTo(new[] { PlacementPreviewRejection.UnknownDefinition }));
            Assert.That(invalidOrientation.Cells, Is.Empty);
            Assert.That(invalidOrientation.Rejections,
                Is.EqualTo(new[] { PlacementPreviewRejection.UnsupportedOrientation }));
            Assert.That(overflow.Cells, Is.Empty);
            Assert.That(overflow.Rejections, Is.EqualTo(new[] { PlacementPreviewRejection.OutOfBounds }));
            Assert.That(all.Cells, Has.Count.EqualTo(4));
            Assert.That(all.Rejections, Is.EqualTo(new[]
            {
                PlacementPreviewRejection.UnsupportedOrientation,
                PlacementPreviewRejection.OutOfBounds,
                PlacementPreviewRejection.Occupied,
            }));
        }

        [Test]
        public void SnapshotQueryDelegatesCoherentRevisionAndReturnsDefensiveCollections()
        {
            var commands = CreateCommands(FoundationFixtures.CreateD001(), new TestIdSource(Id(10)));
            var before = commands.GetFactorySnapshot();

            var placed = commands.PlaceBuilding(D001Id, new GridPosition(8, 8), 0);
            var after = commands.GetFactorySnapshot();

            Assert.That(before.Revision, Is.Zero);
            Assert.That(before.Buildings, Has.Count.EqualTo(1));
            Assert.That(after.Revision, Is.EqualTo(placed.Value.Revision));
            Assert.That(after.Buildings, Has.Count.EqualTo(2));
            Assert.That(() => ((IList<BuildingSnapshot>)after.Buildings).Clear(),
                Throws.TypeOf<NotSupportedException>());
            Assert.That(() => ((IDictionary<GridPosition, BuildingInstanceId>)after.Occupancy).Clear(),
                Throws.TypeOf<NotSupportedException>());
        }

        [Test]
        public void SelectionResolutionIsEmptySelectedOrMissingWithoutOwningSelection()
        {
            var fixture = FoundationFixtures.CreateD001();
            var existing = fixture.Buildings.Keys.Single();
            var commands = CreateCommands(fixture, new TestIdSource(Id(10)));

            var empty = commands.ResolveSelection(null);
            var selected = commands.ResolveSelection(existing);
            var missing = commands.ResolveSelection(Id(99));

            Assert.That(empty.Kind, Is.EqualTo(SelectionResolutionKind.Empty));
            Assert.That(empty.Inspection, Is.Null);
            Assert.That(selected.Kind, Is.EqualTo(SelectionResolutionKind.Selected));
            Assert.That(selected.RequestedInstanceId, Is.EqualTo(existing));
            Assert.That(selected.Inspection.InstanceId, Is.EqualTo(existing));
            Assert.That(missing.Kind, Is.EqualTo(SelectionResolutionKind.Missing));
            Assert.That(missing.RequestedInstanceId, Is.EqualTo(Id(99)));
            Assert.That(missing.Inspection, Is.Null);
            Assert.That(commands.GetFactorySnapshot().Revision, Is.Zero);
        }

        [Test]
        public void SelectionReResolvesByPersistentIdAfterStateReplacement()
        {
            var original = FoundationFixtures.CreateD001();
            var selectedId = original.Buildings.Keys.Single();
            var session = CreateRunningSession(original);
            var commands = new PlacementCommands(session, new TestIdSource(Id(10)));

            ReplaceState(session, new FactoryState(original.Bounds, original.Catalog, Array.Empty<BuildingInstanceState>()));
            Assert.That(commands.ResolveSelection(selectedId).Kind, Is.EqualTo(SelectionResolutionKind.Missing));

            ReplaceState(session, original.Copy());
            var resolved = commands.ResolveSelection(selectedId);
            Assert.That(resolved.Kind, Is.EqualTo(SelectionResolutionKind.Selected));
            Assert.That(resolved.Inspection.InstanceId, Is.EqualTo(selectedId));
            Assert.That(resolved.Inspection.Revision, Is.EqualTo(2));
        }

        [Test]
        public void CoreStateDoesNotAcquirePresentationOwnedFieldsOrPlatformDependencies()
        {
            var forbidden = new[] { "selection", "selected", "camera", "preview", "pointer" };
            var fields = typeof(FactorySession).GetFields(
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

            Assert.That(fields.Select(field => field.Name.ToLowerInvariant()),
                Has.None.Matches<string>(name => forbidden.Any(name.Contains)));
            Assert.That(typeof(FactorySession).Assembly.GetReferencedAssemblies().Select(reference => reference.Name),
                Has.None.StartsWith("UnityEngine"));
        }

        private static PlacementCommands CreateCommands(
            FactoryState state,
            TestIdSource ids,
            LifecycleState lifecycle = LifecycleState.Running)
        {
            var session = new FactorySession(state, state.Catalog, new TestClock(), new TestCheckpointStore());
            MoveTo(session, lifecycle);
            return new PlacementCommands(session, ids);
        }

        private static FactorySession CreateRunningSession(FactoryState state)
        {
            var session = new FactorySession(state, state.Catalog, new TestClock(), new TestCheckpointStore());
            MoveTo(session, LifecycleState.Running);
            return session;
        }

        private static void MoveTo(FactorySession session, LifecycleState target)
        {
            if (target == LifecycleState.Bootstrapping)
            {
                return;
            }

            Transition(session, LifecycleState.Loading);
            Transition(session, LifecycleState.Running);
            if (target == LifecycleState.ReturnBlocked)
            {
                Transition(session, LifecycleState.Checkpointing);
                Transition(session, LifecycleState.Suspended);
                Transition(session, LifecycleState.Reconciling);
                Transition(session, LifecycleState.ReturnBlocked);
            }
        }

        private static void Transition(FactorySession session, LifecycleState target)
        {
            var method = typeof(FactorySession).GetMethod("TransitionTo", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null);
            var result = (CommandResult)method.Invoke(session, new object[] { target });
            Assert.That(result.Succeeded, Is.True);
        }

        private static void ReplaceState(FactorySession session, FactoryState state)
        {
            var method = typeof(FactorySession).GetMethod("ReplaceState", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null);
            method.Invoke(session, new object[] { state });
        }

        private static void AssertUnchanged(FactorySnapshot before, FactorySnapshot after)
        {
            Assert.That(after.Revision, Is.EqualTo(before.Revision));
            Assert.That(after.Buildings.Select(building => building.InstanceId),
                Is.EqualTo(before.Buildings.Select(building => building.InstanceId)));
            Assert.That(after.Occupancy, Is.EqualTo(before.Occupancy));
        }

        private static BuildingInstanceId Id(int value) =>
            new BuildingInstanceId(new Guid(value, 0, 0, new byte[8]));

        private sealed class TestIdSource : IBuildingInstanceIdSource
        {
            private readonly BuildingInstanceId id;
            private readonly CommandOutcome? failure;

            public TestIdSource(BuildingInstanceId id)
            {
                this.id = id;
            }

            public TestIdSource(CommandOutcome failure)
            {
                this.failure = failure;
            }

            public int CallCount { get; private set; }

            public CommandResult<BuildingInstanceId> Next()
            {
                CallCount++;
                return failure.HasValue
                    ? CommandResult<BuildingInstanceId>.Rejected(failure.Value, "ID generation failed.")
                    : CommandResult<BuildingInstanceId>.Success(id);
            }
        }

        private sealed class TestClock : IUtcClock
        {
            public DateTimeOffset GetUtcNow() => DateTimeOffset.UnixEpoch;
        }

        private sealed class TestCheckpointStore : ICheckpointStore
        {
            public CheckpointReadResult ReadActive() => Missing();
            public CheckpointReadResult ReadPrevious() => Missing();
            public CheckpointReadResult Read(CheckpointId checkpointId) => Missing();
            public CommandResult<CheckpointId> Promote(Checkpoint checkpoint) =>
                CommandResult<CheckpointId>.Rejected(CommandOutcome.PersistenceFailed, "Not configured.");

            private static CheckpointReadResult Missing() =>
                CheckpointReadResult.Rejected(CommandOutcome.NoCheckpoint, "No checkpoint.");
        }
    }
}
