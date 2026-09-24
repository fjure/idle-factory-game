using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using IdleFactory.Core.Application;
using IdleFactory.Domain;
using IdleFactory.Simulation;
using IdleFactory.Tests.EditMode.Fixtures;
using NUnit.Framework;

namespace IdleFactory.Tests.EditMode
{
    public sealed class SimulationEquivalenceTests
    {
        private const long CycleTicks = FoundationFixtures.RecipeDurationTicks;
        private const long HalfCycleTicks = CycleTicks / 2;

        private static readonly ResourceId InputResource = new ResourceId(FoundationFixtures.InputResourceId);
        private static readonly ResourceId OutputResource = new ResourceId(FoundationFixtures.OutputResourceId);

        [TestCase(0L, 4L, 0L, 0L, 0)]
        [TestCase(1L, 4L, 0L, 1L, 0)]
        [TestCase(HalfCycleTicks, 4L, 0L, HalfCycleTicks, 0)]
        [TestCase(CycleTicks - 1, 4L, 0L, CycleTicks - 1, 0)]
        [TestCase(CycleTicks, 2L, 1L, 0L, 0)]
        [TestCase(CycleTicks + 1, 2L, 1L, 1L, 0)]
        [TestCase(2 * CycleTicks, 0L, 2L, 0L, 2)]
        [TestCase(2 * CycleTicks + HalfCycleTicks, 0L, 2L, 0L, 2)]
        public void WholeAndPartitionedAdvancesMatchAtCycleBoundaries(
            long elapsedTicks,
            long expectedInput,
            long expectedOutput,
            long expectedProgress,
            int expectedBlockerCount)
        {
            var initial = FoundationFixtures.CreateMultiCycle();
            var oneInterval = ProductionSimulator.Advance(initial, elapsedTicks);
            Assert.That(oneInterval.Status, Is.EqualTo(ProductionAdvanceStatus.Success));

            var partitionedState = AdvancePrepared(initial, SplitIntoThree(elapsedTicks));
            var expected = SemanticFactorySnapshot.Capture(oneInterval.Candidate);
            var actual = SemanticFactorySnapshot.Capture(partitionedState);

            Assert.That(actual.Equals(expected), Is.True, "Partitioning must preserve complete semantic state.");
            Assert.That(actual.Checksum, Is.EqualTo(expected.Checksum));
            AssertBuildingState(actual.Buildings.Single(), expectedInput, expectedOutput, expectedProgress);
            Assert.That(actual.Buildings.Single().Blockers.Count, Is.EqualTo(expectedBlockerCount));
        }

        [TestCase(ProductionBlockerKind.Inactive, 36000000000L)]
        [TestCase(ProductionBlockerKind.Inactive, 864000000000L)]
        [TestCase(ProductionBlockerKind.Inactive, 6048000000000L)]
        [TestCase(ProductionBlockerKind.MissingInput, 36000000000L)]
        [TestCase(ProductionBlockerKind.MissingInput, 864000000000L)]
        [TestCase(ProductionBlockerKind.MissingInput, 6048000000000L)]
        [TestCase(ProductionBlockerKind.InsufficientOutputCapacity, 36000000000L)]
        [TestCase(ProductionBlockerKind.InsufficientOutputCapacity, 864000000000L)]
        [TestCase(ProductionBlockerKind.InsufficientOutputCapacity, 6048000000000L)]
        public void StoppedIntervalsPreserveResourcesProgressAndBlockerDetails(
            ProductionBlockerKind blockerKind,
            long elapsedTicks)
        {
            var factory = FoundationFixtures.CreateD001();
            var instanceId = factory.Buildings.Keys.Single();
            var commands = CreateCommands(factory, out var session);
            Assert.That(commands.AdvanceSimulation(HalfCycleTicks).Succeeded, Is.True);

            switch (blockerKind)
            {
                case ProductionBlockerKind.Inactive:
                    Assert.That(commands.SetBuildingActive(instanceId, false).Succeeded, Is.True);
                    break;
                case ProductionBlockerKind.MissingInput:
                    Assert.That(commands.RemoveResource(instanceId, BufferRole.Input, InputResource, 4).Succeeded, Is.True);
                    break;
                case ProductionBlockerKind.InsufficientOutputCapacity:
                    Assert.That(commands.AddResource(instanceId, BufferRole.Output, OutputResource, 1).Succeeded, Is.True);
                    break;
                default:
                    Assert.Fail("Unexpected blocker kind.");
                    break;
            }

            var before = SemanticFactorySnapshot.Capture(CaptureState(session));
            var advance = commands.AdvanceSimulation(elapsedTicks);

            Assert.That(advance.Succeeded, Is.True);
            var after = SemanticFactorySnapshot.Capture(CaptureState(session));
            Assert.That(after.Equals(before), Is.True);
            Assert.That(after.Checksum, Is.EqualTo(before.Checksum));
            Assert.That(after.Buildings.Single().ProgressTicks, Is.EqualTo(HalfCycleTicks));
            Assert.That(after.Buildings.Single().Blockers.Select(blocker => blocker.Kind),
                Does.Contain(blockerKind));
        }

        [Test]
        public void IdenticalOrderedActionsAtIdenticalTimesReplayToTheSameState()
        {
            var firstCommands = CreateCommands(FoundationFixtures.CreateD001(), out var firstSession);
            var secondCommands = CreateCommands(FoundationFixtures.CreateD001(), out var secondSession);
            var firstId = firstSession.GetFactorySnapshot().Buildings.Single().InstanceId;
            var secondId = secondSession.GetFactorySnapshot().Buildings.Single().InstanceId;

            ApplyReplay(firstCommands, firstId);
            ApplyReplay(secondCommands, secondId);

            var first = SemanticFactorySnapshot.Capture(CaptureState(firstSession));
            var second = SemanticFactorySnapshot.Capture(CaptureState(secondSession));
            Assert.That(first.Equals(second), Is.True);
            Assert.That(first.Checksum, Is.EqualTo(second.Checksum));
            Assert.That(firstSession.GetFactorySnapshot().Revision, Is.EqualTo(secondSession.GetFactorySnapshot().Revision));
        }

        [Test]
        public void ReorderingExternalActionAndAdvanceChangesTheSemanticResult()
        {
            var beforeAdvanceCommands = CreateCommands(FoundationFixtures.CreateD001(), out var beforeAdvanceSession);
            var beforeAdvanceId = beforeAdvanceSession.GetFactorySnapshot().Buildings.Single().InstanceId;
            Assert.That(beforeAdvanceCommands.SetBuildingActive(beforeAdvanceId, false).Succeeded, Is.True);
            Assert.That(beforeAdvanceCommands.AdvanceSimulation(CycleTicks).Succeeded, Is.True);
            Assert.That(beforeAdvanceCommands.SetBuildingActive(beforeAdvanceId, true).Succeeded, Is.True);

            var afterAdvanceCommands = CreateCommands(FoundationFixtures.CreateD001(), out var afterAdvanceSession);
            var afterAdvanceId = afterAdvanceSession.GetFactorySnapshot().Buildings.Single().InstanceId;
            Assert.That(afterAdvanceCommands.AdvanceSimulation(CycleTicks).Succeeded, Is.True);
            Assert.That(afterAdvanceCommands.SetBuildingActive(afterAdvanceId, false).Succeeded, Is.True);
            Assert.That(afterAdvanceCommands.SetBuildingActive(afterAdvanceId, true).Succeeded, Is.True);

            var beforeAdvance = SemanticFactorySnapshot.Capture(CaptureState(beforeAdvanceSession));
            var afterAdvance = SemanticFactorySnapshot.Capture(CaptureState(afterAdvanceSession));
            Assert.That(beforeAdvance.Equals(afterAdvance), Is.False);
            Assert.That(beforeAdvance.Checksum, Is.Not.EqualTo(afterAdvance.Checksum));
            Assert.That(beforeAdvance.Buildings.Single().Inputs.Single().Quantity, Is.EqualTo(4));
            Assert.That(afterAdvance.Buildings.Single().Inputs.Single().Quantity, Is.EqualTo(2));
            Assert.That(beforeAdvance.Buildings.Single().Outputs.Single().Quantity, Is.Zero);
            Assert.That(afterAdvance.Buildings.Single().Outputs.Single().Quantity, Is.EqualTo(1));
        }

        [TestCase(-1L)]
        [TestCase(long.MinValue)]
        public void InvalidElapsedTimeFailsWithoutChangingCommittedOrCapturedState(long elapsedTicks)
        {
            var factory = FoundationFixtures.CreateD001();
            ProductionSimulationSession simulation;
            Assert.That(ProductionSimulator.TryPrepare(factory, out simulation), Is.EqualTo(ProductionAdvanceStatus.Success));
            Assert.That(simulation.Advance(123L), Is.EqualTo(ProductionAdvanceStatus.Success));
            var captured = simulation.CaptureResult().Candidate;
            var before = SemanticFactorySnapshot.Capture(captured);

            Assert.That(simulation.Advance(elapsedTicks), Is.EqualTo(ProductionAdvanceStatus.InvalidElapsedTicks));
            Assert.That(simulation.CaptureResult().Candidate, Is.Null);
            Assert.That(SemanticFactorySnapshot.Capture(captured).Equals(before), Is.True);

            var staticFailure = ProductionSimulator.Advance(factory, elapsedTicks);
            Assert.That(staticFailure.Status, Is.EqualTo(ProductionAdvanceStatus.InvalidElapsedTicks));
            Assert.That(staticFailure.Candidate, Is.Null);
            Assert.That(staticFailure.Summary, Is.Null);
            Assert.That(SemanticFactorySnapshot.Capture(factory).Buildings.Single().ProgressTicks, Is.Zero);

            Assert.That(simulation.Advance(1L), Is.EqualTo(ProductionAdvanceStatus.Success),
                "A valid retry must overwrite failed staging and advance from committed state.");
            var retried = SemanticFactorySnapshot.Capture(simulation.CaptureResult().Candidate);
            Assert.That(retried.Buildings.Single().ProgressTicks, Is.EqualTo(124L));
            Assert.That(before.Buildings.Single().ProgressTicks, Is.EqualTo(123L));
        }

        [Test]
        public void ApplicationRevisionIsComparedSeparatelyFromSemanticState()
        {
            var commands = CreateCommands(FoundationFixtures.CreateD001(), out var session);
            var beforeView = session.GetFactorySnapshot();
            var beforeState = SemanticFactorySnapshot.Capture(CaptureState(session));

            Assert.That(commands.AdvanceSimulation(0).Succeeded, Is.True);

            var afterView = session.GetFactorySnapshot();
            var afterState = SemanticFactorySnapshot.Capture(CaptureState(session));
            Assert.That(beforeView.Revision, Is.EqualTo(0));
            Assert.That(afterView.Revision, Is.EqualTo(1));
            Assert.That(beforeState.Equals(afterState), Is.True,
                "Application revision is metadata and is deliberately outside semantic equality.");
            Assert.That(beforeState.Checksum, Is.EqualTo(afterState.Checksum));
        }

        [Test]
        public void SnapshotCapturesCompleteStateInCanonicalOrderAndReturnsDetachedReadOnlyData()
        {
            var factory = CreateTwoMachineFactory(reverseEnumeration: false);
            var reverse = CreateTwoMachineFactory(reverseEnumeration: true);
            var snapshot = SemanticFactorySnapshot.Capture(factory);
            var reversedSnapshot = SemanticFactorySnapshot.Capture(reverse);

            Assert.That(snapshot.Equals(reversedSnapshot), Is.True);
            Assert.That(snapshot.Checksum, Is.EqualTo(reversedSnapshot.Checksum));
            Assert.That(snapshot.Bounds, Is.EqualTo(factory.Bounds));
            Assert.That(snapshot.OccupiedCells.Count, Is.EqualTo(8));
            Assert.That(
                snapshot.Buildings.Select(building => building.InstanceId).ToArray(),
                Is.EqualTo(snapshot.Buildings.Select(building => building.InstanceId)
                    .OrderBy(instanceId => instanceId).ToArray()));

            var first = snapshot.Buildings[0];
            Assert.That(first.DefinitionId, Is.EqualTo(new BuildingDefinitionId(FoundationFixtures.D001BuildingId)));
            Assert.That(first.Anchor, Is.EqualTo(new GridPosition(0, 0)));
            Assert.That(first.OrientationQuarterTurns, Is.Zero);
            Assert.That(first.Footprint, Is.EqualTo(new Footprint(2, 2)));
            Assert.That(first.IsActive, Is.True);
            Assert.That(first.RecipeId, Is.EqualTo(new RecipeId(FoundationFixtures.BasicRecipeId)));
            Assert.That(first.RecipeDurationTicks, Is.EqualTo(CycleTicks));
            Assert.That(first.ProgressTicks, Is.Zero);
            Assert.That(first.RecipeInputs.Single().Amount, Is.EqualTo(2));
            Assert.That(first.RecipeOutputs.Single().Amount, Is.EqualTo(1));
            AssertStorage(first.Inputs.Single(), BufferRole.Input, InputResource, 4, 4);
            AssertStorage(first.Outputs.Single(), BufferRole.Output, OutputResource, 0, 1);
            Assert.That(first.Blockers, Is.Empty);

            var checksum = snapshot.Checksum;
            ProductionSimulationSession simulation;
            Assert.That(ProductionSimulator.TryPrepare(factory, out simulation), Is.EqualTo(ProductionAdvanceStatus.Success));
            Assert.That(simulation.Advance(CycleTicks), Is.EqualTo(ProductionAdvanceStatus.Success));
            Assert.That(simulation.Advance(1L), Is.EqualTo(ProductionAdvanceStatus.Success));
            Assert.That(simulation.Advance(-1L), Is.EqualTo(ProductionAdvanceStatus.InvalidElapsedTicks));

            Assert.That(snapshot.Checksum, Is.EqualTo(checksum));
            Assert.That(SemanticFactorySnapshot.Capture(factory).Equals(snapshot), Is.True);
            Assert.That(() => ((IList<SemanticOccupiedCell>)snapshot.OccupiedCells).Clear(),
                Throws.TypeOf<NotSupportedException>());
            Assert.That(() => ((IList<SemanticBuildingSnapshot>)snapshot.Buildings).Clear(),
                Throws.TypeOf<NotSupportedException>());
            Assert.That(() => ((IList<SemanticResourceStorage>)first.Inputs).Clear(),
                Throws.TypeOf<NotSupportedException>());
            Assert.That(() => ((IList<SemanticBlockerSnapshot>)first.Blockers).Clear(),
                Throws.TypeOf<NotSupportedException>());
        }

        [Test]
        public void ChecksumDoesNotDependOnCurrentCulture()
        {
            var factory = FoundationFixtures.CreateNegativeCoordinate();
            var originalCulture = CultureInfo.CurrentCulture;
            string defaultChecksum;
            try
            {
                CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
                defaultChecksum = SemanticFactorySnapshot.Capture(factory).Checksum;
                CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("fr-FR");
                Assert.That(SemanticFactorySnapshot.Capture(factory).Checksum, Is.EqualTo(defaultChecksum));
            }
            finally
            {
                CultureInfo.CurrentCulture = originalCulture;
            }
        }

        [Test]
        public void ReadingDifferentViewRefreshSchedulesDoesNotChangeFactoryState()
        {
            var firstCommands = CreateCommands(FoundationFixtures.CreateMultiCycle(), out var firstSession);
            var secondCommands = CreateCommands(FoundationFixtures.CreateMultiCycle(), out var secondSession);

            Assert.That(firstCommands.AdvanceSimulation(HalfCycleTicks).Succeeded, Is.True);
            Assert.That(secondCommands.AdvanceSimulation(HalfCycleTicks).Succeeded, Is.True);
            for (var refresh = 0; refresh < 20; refresh++)
            {
                Assert.That(firstSession.GetFactorySnapshot().Revision, Is.EqualTo(1));
            }

            Assert.That(firstCommands.AdvanceSimulation(HalfCycleTicks).Succeeded, Is.True);
            Assert.That(secondCommands.AdvanceSimulation(HalfCycleTicks).Succeeded, Is.True);
            _ = secondSession.GetFactorySnapshot();

            var firstState = SemanticFactorySnapshot.Capture(CaptureState(firstSession));
            var secondState = SemanticFactorySnapshot.Capture(CaptureState(secondSession));
            Assert.That(firstState.Equals(secondState), Is.True);
            Assert.That(firstState.Checksum, Is.EqualTo(secondState.Checksum));
            Assert.That(firstSession.GetFactorySnapshot().Revision, Is.EqualTo(2));
            Assert.That(secondSession.GetFactorySnapshot().Revision, Is.EqualTo(2));
        }

        private static void ApplyReplay(ProductionCommands commands, BuildingInstanceId instanceId)
        {
            Assert.That(commands.RemoveResource(instanceId, BufferRole.Input, InputResource, 2).Succeeded, Is.True);
            Assert.That(commands.AdvanceSimulation(HalfCycleTicks).Succeeded, Is.True);
            Assert.That(commands.AddResource(instanceId, BufferRole.Input, InputResource, 2).Succeeded, Is.True);
            Assert.That(commands.AdvanceSimulation(HalfCycleTicks).Succeeded, Is.True);
            Assert.That(commands.RemoveResource(instanceId, BufferRole.Output, OutputResource, 1).Succeeded, Is.True);
            Assert.That(commands.AdvanceSimulation(1L).Succeeded, Is.True);
        }

        private static FactoryState AdvancePrepared(FactoryState initial, IReadOnlyList<long> intervals)
        {
            ProductionSimulationSession simulation;
            Assert.That(ProductionSimulator.TryPrepare(initial, out simulation), Is.EqualTo(ProductionAdvanceStatus.Success));
            foreach (var interval in intervals)
            {
                Assert.That(simulation.Advance(interval), Is.EqualTo(ProductionAdvanceStatus.Success));
            }

            return simulation.CaptureResult().Candidate;
        }

        private static IReadOnlyList<long> SplitIntoThree(long elapsedTicks)
        {
            var first = elapsedTicks / 3;
            var second = elapsedTicks / 3;
            return new[] { first, second, checked(elapsedTicks - first - second) };
        }

        private static ProductionCommands CreateCommands(FactoryState factory, out FactorySession session)
        {
            session = new FactorySession(factory, factory.Catalog, new TestClock(), new TestCheckpointStore());
            Transition(session, LifecycleState.Loading);
            Transition(session, LifecycleState.Running);
            return new ProductionCommands(session);
        }

        private static FactoryState CaptureState(FactorySession session)
        {
            var capture = typeof(FactorySession).GetMethod("CreateStateCopy", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(capture, Is.Not.Null);
            return (FactoryState)capture.Invoke(session, null);
        }

        private static void Transition(FactorySession session, LifecycleState nextState)
        {
            var transition = typeof(FactorySession).GetMethod("TransitionTo", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(transition, Is.Not.Null);
            var result = (CommandResult)transition.Invoke(session, new object[] { nextState });
            Assert.That(result.Succeeded, Is.True);
        }

        private static FactoryState CreateTwoMachineFactory(bool reverseEnumeration)
        {
            var fixture = FoundationFixtures.CreateD001();
            var definition = fixture.Catalog.Buildings.Values.Single();
            var recipe = fixture.Catalog.Recipes.Values.Single();
            var buildings = new[]
            {
                CreateBuilding(definition, recipe, Id(1), new GridPosition(0, 0)),
                CreateBuilding(definition, recipe, Id(2), new GridPosition(4, 0)),
            };

            if (reverseEnumeration)
            {
                Array.Reverse(buildings);
            }

            return new FactoryState(fixture.Bounds, fixture.Catalog, buildings);
        }

        private static BuildingInstanceState CreateBuilding(
            BuildingDefinition definition,
            RecipeDefinition recipe,
            BuildingInstanceId instanceId,
            GridPosition anchor)
        {
            return new BuildingInstanceState(
                instanceId,
                definition,
                anchor,
                0,
                true,
                new ResourceBufferState(definition.InputBuffer,
                    new[] { new KeyValuePair<ResourceId, long>(InputResource, 4) }),
                new ResourceBufferState(definition.OutputBuffer,
                    new[] { new KeyValuePair<ResourceId, long>(OutputResource, 0) }),
                new ProductionState(recipe, 0));
        }

        private static void AssertBuildingState(
            SemanticBuildingSnapshot building,
            long expectedInput,
            long expectedOutput,
            long expectedProgress)
        {
            Assert.That(building.Inputs.Single().Quantity, Is.EqualTo(expectedInput));
            Assert.That(building.Outputs.Single().Quantity, Is.EqualTo(expectedOutput));
            Assert.That(building.ProgressTicks, Is.EqualTo(expectedProgress));
        }

        private static void AssertStorage(
            SemanticResourceStorage actual,
            BufferRole role,
            ResourceId resourceId,
            long quantity,
            long capacity)
        {
            Assert.That(actual.Role, Is.EqualTo(role));
            Assert.That(actual.ResourceId, Is.EqualTo(resourceId));
            Assert.That(actual.Quantity, Is.EqualTo(quantity));
            Assert.That(actual.Capacity, Is.EqualTo(capacity));
        }

        private static BuildingInstanceId Id(int value) =>
            new BuildingInstanceId(new Guid(value, 0, 0, new byte[8]));

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

            public CommandResult<CheckpointId> RecoverPrevious(Checkpoint recoveryCandidate) =>
                CommandResult<CheckpointId>.Rejected(CommandOutcome.PersistenceFailed, "Not configured.");

            private static CheckpointReadResult Missing() =>
                CheckpointReadResult.Rejected(CommandOutcome.NoCheckpoint, "No checkpoint.");
        }
    }
}
