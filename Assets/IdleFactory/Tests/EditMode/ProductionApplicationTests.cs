using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using IdleFactory.Core.Application;
using IdleFactory.Domain;
using IdleFactory.Simulation;
using IdleFactory.Tests.EditMode.Fixtures;
using NUnit.Framework;

namespace IdleFactory.Tests.EditMode
{
    public sealed class ProductionApplicationTests
    {
        private static readonly ResourceId InputResource = new ResourceId(FoundationFixtures.InputResourceId);
        private static readonly ResourceId OutputResource = new ResourceId(FoundationFixtures.OutputResourceId);
        private const long CycleTicks = FoundationFixtures.RecipeDurationTicks;

        [Test]
        public void ResourceCommandsUseExplicitBuffersAndReturnRevisionQuantityAndCapacity()
        {
            var factory = FoundationFixtures.CreateD001();
            var instanceId = factory.Buildings.Keys.Single();
            FactorySession session;
            var commands = CreateCommands(factory, out session);

            var removed = commands.RemoveResource(instanceId, BufferRole.Input, InputResource, 2);
            var added = commands.AddResource(instanceId, BufferRole.Input, InputResource, 2);
            var outputAdded = commands.AddResource(instanceId, BufferRole.Output, OutputResource, 1);

            Assert.That(removed.Succeeded, Is.True);
            Assert.That(removed.Value.Revision, Is.EqualTo(1));
            Assert.That(removed.Value.Quantity, Is.EqualTo(2));
            Assert.That(removed.Value.Capacity, Is.EqualTo(4));
            Assert.That(removed.Value.FreeCapacity, Is.EqualTo(2));
            Assert.That(added.Succeeded, Is.True);
            Assert.That(added.Value.Revision, Is.EqualTo(2));
            Assert.That(added.Value.Quantity, Is.EqualTo(4));
            Assert.That(added.Value.FreeCapacity, Is.Zero);
            Assert.That(outputAdded.Succeeded, Is.True);
            Assert.That(outputAdded.Value.Revision, Is.EqualTo(3));
            Assert.That(outputAdded.Value.Quantity, Is.EqualTo(1));
            Assert.That(outputAdded.Value.FreeCapacity, Is.Zero);
            Assert.That(session.GetFactorySnapshot().Revision, Is.EqualTo(3));
            Assert.That(GetPreparedSession(session), Is.Not.Null);
        }

        [Test]
        public void RejectedResourceCommandsPreserveFactoryRevisionAndPreparedSession()
        {
            var factory = FoundationFixtures.CreateD001();
            var instanceId = factory.Buildings.Keys.Single();
            FactorySession session;
            var commands = CreateCommands(factory, out session);
            var before = session.GetMachineInspection(instanceId).Value;
            var prepared = GetPreparedSession(session);

            AssertResourceOutcome(commands.AddResource(default(BuildingInstanceId), BufferRole.Input, InputResource, 1),
                CommandOutcome.InvalidRequest);
            AssertResourceOutcome(commands.AddResource(Id(99), BufferRole.Input, InputResource, 1),
                CommandOutcome.NotFound);
            AssertResourceOutcome(commands.AddResource(instanceId, BufferRole.Input, default(ResourceId), 1),
                CommandOutcome.InvalidRequest);
            AssertResourceOutcome(commands.AddResource(instanceId, BufferRole.Input, new ResourceId("missing.resource"), 1),
                CommandOutcome.NotFound);
            AssertResourceOutcome(commands.AddResource(instanceId, (BufferRole)99, InputResource, 1),
                CommandOutcome.InvalidRequest);
            AssertResourceOutcome(commands.AddResource(instanceId, BufferRole.Input, InputResource, 0),
                CommandOutcome.InvalidRequest);
            AssertResourceOutcome(commands.RemoveResource(instanceId, BufferRole.Input, InputResource, -1),
                CommandOutcome.InvalidRequest);
            AssertResourceOutcome(commands.AddResource(instanceId, BufferRole.Input, InputResource, 1),
                CommandOutcome.InsufficientCapacity);
            AssertResourceOutcome(commands.RemoveResource(instanceId, BufferRole.Output, OutputResource, 1),
                CommandOutcome.InsufficientResource);
            AssertResourceOutcome(commands.AddResource(instanceId, BufferRole.Output, InputResource, 1),
                CommandOutcome.NotFound);

            Assert.That(session.GetFactorySnapshot().Revision, Is.Zero);
            Assert.That(GetPreparedSession(session), Is.SameAs(prepared));
            AssertInspectionEquivalent(before, session.GetMachineInspection(instanceId).Value);
        }

        [Test]
        public void SetBuildingActiveAndInspectionExposeEveryCurrentBlockerImmutably()
        {
            var factory = FoundationFixtures.CreateD001();
            var instanceId = factory.Buildings.Keys.Single();
            FactorySession session;
            var commands = CreateCommands(factory, out session);

            Assert.That(commands.AddResource(instanceId, BufferRole.Output, OutputResource, 1).Succeeded, Is.True);
            Assert.That(commands.RemoveResource(instanceId, BufferRole.Input, InputResource, 4).Succeeded, Is.True);
            var deactivated = commands.SetBuildingActive(instanceId, false);

            Assert.That(deactivated.Succeeded, Is.True);
            Assert.That(deactivated.Value.Revision, Is.EqualTo(3));
            Assert.That(deactivated.Value.IsActive, Is.False);
            Assert.That(deactivated.Value.Inputs.Single().Quantity, Is.Zero);
            Assert.That(deactivated.Value.Outputs.Single().Quantity, Is.EqualTo(1));
            Assert.That(deactivated.Value.Outputs.Single().RequiredOrFreeCapacity, Is.Zero);
            Assert.That(deactivated.Value.Blockers.Select(blocker => blocker.Kind), Is.EquivalentTo(new[]
            {
                ProductionBlockerKind.Inactive,
                ProductionBlockerKind.MissingInput,
                ProductionBlockerKind.InsufficientOutputCapacity,
            }));
            Assert.That(() => ((IList<ProductionBlockerSnapshot>)deactivated.Value.Blockers).Clear(),
                Throws.TypeOf<NotSupportedException>());
            Assert.That(() => ((IList<ResourceQuantitySnapshot>)deactivated.Value.Inputs).Clear(),
                Throws.TypeOf<NotSupportedException>());

            var reactivated = commands.SetBuildingActive(instanceId, true);
            Assert.That(reactivated.Succeeded, Is.True);
            Assert.That(reactivated.Value.IsActive, Is.True);
            Assert.That(reactivated.Value.Revision, Is.EqualTo(4));
            Assert.That(reactivated.Value.Blockers.Select(blocker => blocker.Kind), Is.EquivalentTo(new[]
            {
                ProductionBlockerKind.MissingInput,
                ProductionBlockerKind.InsufficientOutputCapacity,
            }));
            Assert.That(session.GetMachineInspection(instanceId).Value.Revision, Is.EqualTo(4));
        }

        [Test]
        public void InvalidBuildingIdentifiersAreTypedAndDoNotReplacePreparedSession()
        {
            var factory = FoundationFixtures.CreateD001();
            var instanceId = factory.Buildings.Keys.Single();
            FactorySession session;
            var commands = CreateCommands(factory, out session);
            var prepared = GetPreparedSession(session);

            var invalid = commands.SetBuildingActive(default(BuildingInstanceId), false);
            var missing = commands.SetBuildingActive(Id(99), false);

            Assert.That(invalid.Outcome, Is.EqualTo(CommandOutcome.InvalidRequest));
            Assert.That(missing.Outcome, Is.EqualTo(CommandOutcome.NotFound));
            Assert.That(commands.GetMachineInspection(default(BuildingInstanceId)).Outcome,
                Is.EqualTo(CommandOutcome.InvalidRequest));
            Assert.That(commands.GetMachineInspection(Id(99)).Outcome, Is.EqualTo(CommandOutcome.NotFound));
            Assert.That(session.GetFactorySnapshot().Revision, Is.Zero);
            Assert.That(GetPreparedSession(session), Is.SameAs(prepared));
            Assert.That(session.GetMachineInspection(instanceId).Value.IsActive, Is.True);
        }

        [Test]
        public void AdvanceUsesOneRetainedSessionAndReturnsDetachedCompleteSnapshots()
        {
            var factory = FoundationFixtures.CreateMultiCycle();
            var instanceId = factory.Buildings.Keys.Single();
            FactorySession session;
            var commands = CreateCommands(factory, out session);
            var prepared = GetPreparedSession(session);

            var first = commands.AdvanceSimulation(CycleTicks);
            var second = commands.AdvanceSimulation(CycleTicks);

            Assert.That(first.Succeeded, Is.True);
            Assert.That(first.Value.Revision, Is.EqualTo(1));
            Assert.That(first.Value.Factory.Revision, Is.EqualTo(1));
            Assert.That(first.Value.Summary.ElapsedTicks, Is.EqualTo(CycleTicks));
            Assert.That(first.Value.Summary.Buildings.Single().CompletedCycles, Is.EqualTo(1));
            Assert.That(first.Value.Machines.Single().InstanceId, Is.EqualTo(instanceId));
            Assert.That(first.Value.Machines.Single().Inputs.Single().Quantity, Is.EqualTo(2));
            Assert.That(first.Value.Machines.Single().Outputs.Single().Quantity, Is.EqualTo(1));
            Assert.That(first.Value.Machines.Single().ProgressTicks, Is.Zero);
            Assert.That(first.Value.Machines.Single().Blockers, Is.Empty);

            Assert.That(second.Succeeded, Is.True);
            Assert.That(second.Value.Revision, Is.EqualTo(2));
            Assert.That(second.Value.Factory.Revision, Is.EqualTo(2));
            Assert.That(second.Value.Summary.Buildings.Single().CompletedCycles, Is.EqualTo(1));
            Assert.That(second.Value.Machines.Single().Inputs.Single().Quantity, Is.Zero);
            Assert.That(second.Value.Machines.Single().Outputs.Single().Quantity, Is.EqualTo(2));
            Assert.That(second.Value.Machines.Single().Blockers.Select(blocker => blocker.Kind), Is.EquivalentTo(new[]
            {
                ProductionBlockerKind.MissingInput,
                ProductionBlockerKind.InsufficientOutputCapacity,
            }));

            Assert.That(GetPreparedSession(session), Is.SameAs(prepared));
            Assert.That(() => ((IList<MachineInspectionSnapshot>)first.Value.Machines).Clear(),
                Throws.TypeOf<NotSupportedException>());
            AssertInspectionState(first.Value.Machines.Single(), input: 2, output: 1, progress: 0);
            AssertInspectionState(second.Value.Machines.Single(), input: 0, output: 2, progress: 0);
            Assert.That(session.GetFactorySnapshot().Revision, Is.EqualTo(2));
        }

        [Test]
        public void ExternalPlacementReplacesPreparedSessionAndTheNextAdvanceUsesCurrentFactory()
        {
            var factory = FoundationFixtures.CreateD001();
            var firstId = factory.Buildings.Keys.Single();
            var session = CreateRunningSession(factory);
            var commands = new ProductionCommands(session);
            var prepared = GetPreparedSession(session);
            var placement = new PlacementCommands(session, new TestIdSource(Id(2)));

            var placed = placement.PlaceBuilding(
                new BuildingDefinitionId(FoundationFixtures.D001BuildingId),
                new GridPosition(8, 8),
                0);

            Assert.That(placed.Succeeded, Is.True);
            Assert.That(placed.Value.InstanceId, Is.EqualTo(Id(2)));
            var afterPlacement = GetPreparedSession(session);
            Assert.That(afterPlacement, Is.Not.SameAs(prepared));
            var supplied = commands.AddResource(Id(2), BufferRole.Input, InputResource, 4);
            Assert.That(supplied.Succeeded, Is.True);
            Assert.That(GetPreparedSession(session), Is.Not.SameAs(afterPlacement));

            var advanced = commands.AdvanceSimulation(CycleTicks);

            Assert.That(advanced.Succeeded, Is.True);
            Assert.That(advanced.Value.Revision, Is.EqualTo(3));
            Assert.That(advanced.Value.Machines.Select(machine => machine.InstanceId),
                Is.EqualTo(new[] { firstId, Id(2) }));
            var advancedById = advanced.Value.Machines.ToDictionary(machine => machine.InstanceId);
            AssertInspectionState(advancedById[firstId], input: 2, output: 1, progress: 0);
            AssertInspectionState(advancedById[Id(2)], input: 2, output: 1, progress: 0);
        }

        [Test]
        public void RejectedExternalCommandKeepsPreparedSessionAndPriorSnapshot()
        {
            var factory = FoundationFixtures.CreateD001();
            var instanceId = factory.Buildings.Keys.Single();
            var session = CreateRunningSession(factory);
            var commands = new ProductionCommands(session);
            var prepared = GetPreparedSession(session);
            var before = commands.AdvanceSimulation(0).Value;
            var afterZeroAdvanceSession = GetPreparedSession(session);
            var rejected = commands.RemoveResource(instanceId, BufferRole.Input, InputResource, 5);

            Assert.That(rejected.Outcome, Is.EqualTo(CommandOutcome.InsufficientResource));
            Assert.That(session.GetFactorySnapshot().Revision, Is.EqualTo(1));
            Assert.That(GetPreparedSession(session), Is.SameAs(afterZeroAdvanceSession));
            Assert.That(GetPreparedSession(session), Is.SameAs(prepared));
            AssertInspectionState(before.Machines.Single(), input: 4, output: 0, progress: 0);
            AssertInspectionState(commands.GetMachineInspection(instanceId).Value, input: 4, output: 0, progress: 0);
        }

        [Test]
        public void NegativeElapsedTimeIsRejectedBeforeAdvancingOrCapturing()
        {
            var factory = FoundationFixtures.CreateD001();
            var instanceId = factory.Buildings.Keys.Single();
            FactorySession session;
            var commands = CreateCommands(factory, out session);
            var prepared = GetPreparedSession(session);

            var result = commands.AdvanceSimulation(-1);

            Assert.That(result.Outcome, Is.EqualTo(CommandOutcome.InvalidElapsedTime));
            Assert.That(result.Value, Is.Null);
            Assert.That(session.GetFactorySnapshot().Revision, Is.Zero);
            Assert.That(GetPreparedSession(session), Is.SameAs(prepared));
            AssertInspectionState(commands.GetMachineInspection(instanceId).Value, input: 4, output: 0, progress: 0);
        }

        [Test]
        public void RevisionOverflowRejectsExternalAndSimulationPublicationWithoutChangingSession()
        {
            var factory = FoundationFixtures.CreateD001();
            var instanceId = factory.Buildings.Keys.Single();
            FactorySession session;
            var commands = CreateCommands(factory, out session);
            var prepared = GetPreparedSession(session);
            SetRevision(session, long.MaxValue);
            var before = commands.GetMachineInspection(instanceId).Value;

            var external = commands.SetBuildingActive(instanceId, false);
            var advance = commands.AdvanceSimulation(0);

            Assert.That(external.Outcome, Is.EqualTo(CommandOutcome.InvalidRequest));
            Assert.That(advance.Outcome, Is.EqualTo(CommandOutcome.InvalidRequest));
            Assert.That(advance.Value, Is.Null);
            Assert.That(session.GetFactorySnapshot().Revision, Is.EqualTo(long.MaxValue));
            Assert.That(GetPreparedSession(session), Is.SameAs(prepared));
            Assert.That(commands.GetMachineInspection(instanceId).Value.IsActive, Is.True);
            AssertInspectionEquivalent(before, commands.GetMachineInspection(instanceId).Value);
        }

        [Test]
        public void LateMachineArithmeticFailureRollsBackFactorySessionAndRevisionThenRetries()
        {
            var factory = CreateFactoryWithTwoMachines();
            var instanceIds = factory.Buildings.Keys.OrderBy(id => id).ToArray();
            var session = CreateRunningSession(factory);
            var commands = new ProductionCommands(session);
            var prepared = GetPreparedSession(session);
            var before = instanceIds.ToDictionary(id => id, id => commands.GetMachineInspection(id).Value);
            CorruptLaterMachineForArithmeticOverflow(prepared);

            var failed = commands.AdvanceSimulation(CycleTicks);

            Assert.That(failed.Outcome, Is.EqualTo(CommandOutcome.InvalidRequest));
            Assert.That(failed.Value, Is.Null);
            Assert.That(session.GetFactorySnapshot().Revision, Is.Zero);
            Assert.That(GetPreparedSession(session), Is.SameAs(prepared));
            foreach (var instanceId in instanceIds)
            {
                AssertInspectionEquivalent(before[instanceId], commands.GetMachineInspection(instanceId).Value);
            }

            RestoreLaterMachineAfterArithmeticOverflow(prepared);
            var retried = commands.AdvanceSimulation(CycleTicks);

            Assert.That(retried.Succeeded, Is.True);
            Assert.That(retried.Value.Revision, Is.EqualTo(1));
            Assert.That(GetPreparedSession(session), Is.SameAs(prepared));
            Assert.That(retried.Value.Machines.All(machine => machine.Inputs.Single().Quantity == 2), Is.True);
            Assert.That(retried.Value.Machines.All(machine => machine.Outputs.Single().Quantity == 1), Is.True);
            Assert.That(retried.Value.Machines.All(machine => machine.ProgressTicks == 0), Is.True);
        }

        [Test]
        public void CommandsObeyRunningLifecycleAdmission()
        {
            var factory = FoundationFixtures.CreateD001();
            var instanceId = factory.Buildings.Keys.Single();
            var session = new FactorySession(factory, factory.Catalog, new TestClock(), new TestCheckpointStore());
            var commands = new ProductionCommands(session);
            var prepared = GetPreparedSession(session);

            Assert.That(commands.SetBuildingActive(instanceId, false).Outcome, Is.EqualTo(CommandOutcome.LifecycleNoOp));
            Assert.That(commands.AddResource(instanceId, BufferRole.Input, InputResource, 1).Outcome,
                Is.EqualTo(CommandOutcome.LifecycleNoOp));
            Assert.That(commands.AdvanceSimulation(CycleTicks).Outcome, Is.EqualTo(CommandOutcome.LifecycleNoOp));
            Assert.That(session.GetFactorySnapshot().Revision, Is.Zero);
            Assert.That(GetPreparedSession(session), Is.SameAs(prepared));
            Assert.That(commands.GetMachineInspection(instanceId).Value.IsActive, Is.True);
        }

        private static ProductionCommands CreateCommands(FactoryState factory, out FactorySession session)
        {
            session = CreateRunningSession(factory);
            return new ProductionCommands(session);
        }

        private static FactorySession CreateRunningSession(FactoryState factory)
        {
            var session = new FactorySession(factory, factory.Catalog, new TestClock(), new TestCheckpointStore());
            Transition(session, LifecycleState.Loading);
            Transition(session, LifecycleState.Running);
            return session;
        }

        private static void Transition(FactorySession session, LifecycleState state)
        {
            var method = typeof(FactorySession).GetMethod("TransitionTo", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null);
            var result = (CommandResult)method.Invoke(session, new object[] { state });
            Assert.That(result.Succeeded, Is.True);
        }

        private static ProductionSimulationSession GetPreparedSession(FactorySession session)
        {
            var field = typeof(FactorySession).GetField("productionSession", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null);
            Assert.That(field.FieldType, Is.EqualTo(typeof(ProductionSimulationSession)));
            return (ProductionSimulationSession)field.GetValue(session);
        }

        private static void SetRevision(FactorySession session, long value)
        {
            var field = typeof(FactorySession).GetField("revision", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null);
            field.SetValue(session, value);
        }

        private static void AssertResourceOutcome(
            CommandResult<ResourceCommandResult> result,
            CommandOutcome expected)
        {
            Assert.That(result.Outcome, Is.EqualTo(expected));
            Assert.That(result.Value, Is.Null);
        }

        private static void AssertInspectionEquivalent(
            MachineInspectionSnapshot expected,
            MachineInspectionSnapshot actual)
        {
            Assert.That(actual.Revision, Is.EqualTo(expected.Revision));
            Assert.That(actual.IsActive, Is.EqualTo(expected.IsActive));
            Assert.That(actual.ProgressTicks, Is.EqualTo(expected.ProgressTicks));
            Assert.That(actual.Inputs.Select(item => new { item.ResourceId, item.Quantity, item.RequiredOrFreeCapacity }),
                Is.EqualTo(expected.Inputs.Select(item => new { item.ResourceId, item.Quantity, item.RequiredOrFreeCapacity })));
            Assert.That(actual.Outputs.Select(item => new { item.ResourceId, item.Quantity, item.RequiredOrFreeCapacity }),
                Is.EqualTo(expected.Outputs.Select(item => new { item.ResourceId, item.Quantity, item.RequiredOrFreeCapacity })));
            Assert.That(actual.Blockers.Select(item => new { item.Kind, item.ResourceId, item.Required, item.Available }),
                Is.EqualTo(expected.Blockers.Select(item => new { item.Kind, item.ResourceId, item.Required, item.Available })));
        }

        private static void AssertInspectionState(
            MachineInspectionSnapshot inspection,
            long input,
            long output,
            long progress)
        {
            Assert.That(inspection.Inputs.Single().Quantity, Is.EqualTo(input));
            Assert.That(inspection.Outputs.Single().Quantity, Is.EqualTo(output));
            Assert.That(inspection.ProgressTicks, Is.EqualTo(progress));
        }

        private static FactoryState CreateFactoryWithTwoMachines()
        {
            var template = FoundationFixtures.CreateD001();
            var buildingDefinition = template.Catalog.Buildings.Values.Single();
            var recipe = template.Catalog.Recipes.Values.Single();
            var first = CreateBuilding(buildingDefinition, recipe, Id(1), new GridPosition(0, 0));
            var second = CreateBuilding(buildingDefinition, recipe, Id(2), new GridPosition(4, 0));
            return new FactoryState(template.Bounds, template.Catalog, new[] { first, second });
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
                new ResourceBufferState(definition.InputBuffer, Quantities(InputResource, 4)),
                new ResourceBufferState(definition.OutputBuffer, Quantities(OutputResource, 0)),
                new ProductionState(recipe, 0));
        }

        private static KeyValuePair<ResourceId, long>[] Quantities(ResourceId resourceId, long amount) =>
            new[] { new KeyValuePair<ResourceId, long>(resourceId, amount) };

        private static void CorruptLaterMachineForArithmeticOverflow(ProductionSimulationSession session)
        {
            var buildingsField = typeof(ProductionSimulationSession).GetField("buildings", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(buildingsField, Is.Not.Null);
            var buildings = (Array)buildingsField.GetValue(session);
            var later = buildings.GetValue(1);
            var outputResources = (Array)GetFieldValue(later, "<OutputResources>k__BackingField");
            var outputResource = outputResources.GetValue(0);

            SetBackingField(later, "<OutputAmounts>k__BackingField", new[]
            {
                new ResourceAmountDefinition(OutputResource, long.MaxValue),
                new ResourceAmountDefinition(OutputResource, long.MaxValue),
            });
            SetBackingField(later, "<OutputIndices>k__BackingField", new[] { 0, 0 });
            SetBackingField(outputResource, "<Capacity>k__BackingField", long.MaxValue);
        }

        private static void RestoreLaterMachineAfterArithmeticOverflow(ProductionSimulationSession session)
        {
            var buildingsField = typeof(ProductionSimulationSession).GetField("buildings", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(buildingsField, Is.Not.Null);
            var buildings = (Array)buildingsField.GetValue(session);
            var later = buildings.GetValue(1);
            var outputResources = (Array)GetFieldValue(later, "<OutputResources>k__BackingField");
            var outputResource = outputResources.GetValue(0);

            SetBackingField(later, "<OutputAmounts>k__BackingField", new[]
            {
                new ResourceAmountDefinition(OutputResource, 1),
            });
            SetBackingField(later, "<OutputIndices>k__BackingField", new[] { 0 });
            SetBackingField(outputResource, "<Capacity>k__BackingField", 1L);
        }

        private static object GetFieldValue(object target, string name)
        {
            var field = target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null);
            return field.GetValue(target);
        }

        private static void SetBackingField(object target, string name, object value)
        {
            var field = target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null);
            field.SetValue(target, value);
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

        private sealed class TestIdSource : IBuildingInstanceIdSource
        {
            private readonly BuildingInstanceId id;

            public TestIdSource(BuildingInstanceId id)
            {
                this.id = id;
            }

            public CommandResult<BuildingInstanceId> Next() => CommandResult<BuildingInstanceId>.Success(id);
        }
    }
}
