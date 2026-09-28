using System;
using System.Linq;
using System.Reflection;
using IdleFactory.Core.Application;
using IdleFactory.Domain;
using IdleFactory.Tests.EditMode.Fixtures;
using NUnit.Framework;

namespace IdleFactory.Tests.EditMode
{
    /// <summary>
    /// T063: the application queries behind the machine inspector's poll and the runtime host's per-frame advance.
    /// Before T063 the poll copied the whole factory once per snapshot and once per inspected machine, and every
    /// advance captured a factory snapshot plus one inspection per machine (T040-F1).
    /// </summary>
    public sealed class InspectorPollingQueryTests
    {
        private const long TickTicks = TimeSpan.TicksPerMillisecond * 100L;

        [Test]
        public void W001AnyMachineCanProduceMatchesInspectionsAndAllocatesNothing()
        {
            var session = CreateRunningSession(FoundationFixtures.CreateW001());
            var commands = new ProductionCommands(session);
            Assert.That(commands.AnyMachineCanProduce(), Is.True);
            Assert.That(commands.AnyMachineCanProduce(), Is.EqualTo(InspectionsSayAnyMachineCanProduce(session)));
            AssertPollQueriesAllocateNothing(commands);

            // No machine can produce: the pre-T063 poll scanned every machine here, which made it O(N^2).
            foreach (var building in session.GetFactorySnapshot().Buildings.Where(building => building.IsActive).ToArray())
            {
                Assert.That(commands.SetBuildingActive(building.InstanceId, false).Succeeded, Is.True);
            }

            Assert.That(commands.AnyMachineCanProduce(), Is.False);
            Assert.That(InspectionsSayAnyMachineCanProduce(session), Is.False);
            AssertPollQueriesAllocateNothing(commands);
        }

        [Test]
        public void AnyMachineCanProduceFollowsEveryBlockerAndRecipeCase()
        {
            var session = CreateRunningSession(FoundationFixtures.CreateD001());
            var commands = new ProductionCommands(session);
            var id = session.GetFactorySnapshot().Buildings.Single().InstanceId;
            Assert.That(commands.AnyMachineCanProduce(), Is.True, "Input present, output free and active.");

            Assert.That(commands.AdvanceActiveTime(FoundationFixtures.RecipeDurationTicks).Succeeded, Is.True);
            Assert.That(commands.AnyMachineCanProduce(), Is.False, "Output full.");
            Assert.That(InspectionsSayAnyMachineCanProduce(session), Is.False);

            Assert.That(commands.RemoveResource(id, BufferRole.Output, new ResourceId(FoundationFixtures.OutputResourceId), 1L).Succeeded, Is.True);
            Assert.That(commands.AnyMachineCanProduce(), Is.True, "Output freed and two inputs remain.");

            Assert.That(commands.SetBuildingActive(id, false).Succeeded, Is.True);
            Assert.That(commands.AnyMachineCanProduce(), Is.False, "Inactive.");
            Assert.That(commands.SetBuildingActive(id, true).Succeeded, Is.True);

            Assert.That(commands.RemoveResource(id, BufferRole.Input, new ResourceId(FoundationFixtures.InputResourceId), 1L).Succeeded, Is.True);
            Assert.That(commands.AnyMachineCanProduce(), Is.False, "Missing input.");
            Assert.That(InspectionsSayAnyMachineCanProduce(session), Is.False);
        }

        [Test]
        public void W001SingleMachineInspectionMatchesFullCopyAndDoesNotGrowWithMachineCount()
        {
            var w001 = CreateRunningSession(FoundationFixtures.CreateW001());
            var reference = new ProductionCommands(w001).AdvanceSimulation(0L);
            Assert.That(reference.Succeeded, Is.True);
            foreach (var expected in reference.Value.Machines)
            {
                var actual = w001.GetMachineInspection(expected.InstanceId);
                Assert.That(actual.Succeeded, Is.True);
                AssertSameInspection(actual.Value, expected);
            }

            Assert.That(w001.GetMachineInspection(new BuildingInstanceId(Guid.NewGuid())).Outcome,
                Is.EqualTo(CommandOutcome.NotFound));
            Assert.That(w001.GetMachineInspection(default(BuildingInstanceId)).Outcome,
                Is.EqualTo(CommandOutcome.InvalidRequest));

            var d001 = CreateRunningSession(FoundationFixtures.CreateD001());
            var singleId = d001.GetFactorySnapshot().Buildings.Single().InstanceId;
            var w001Id = reference.Value.Machines[0].InstanceId;
            Action inspectSingle = () => d001.GetMachineInspection(singleId);
            Action inspectW001 = () => w001.GetMachineInspection(w001Id);
            GcAllocationCounter.Count(inspectSingle);
            GcAllocationCounter.Count(inspectW001);
            var singleMachineAllocations = GcAllocationCounter.Count(inspectSingle);
            var w001Allocations = GcAllocationCounter.Count(inspectW001);

            Assert.That(singleMachineAllocations, Is.GreaterThan(0), "The recorder must observe the inspection's own objects.");
            Assert.That(w001Allocations, Is.EqualTo(singleMachineAllocations),
                "Inspecting one machine allocates the same at 1 and 1000 machines: it no longer copies the factory.");
        }

        [Test]
        public void W001AdvanceActiveTimeMatchesAdvanceSimulationAndAllocatesNothing()
        {
            var viaSnapshot = CreateRunningSession(FoundationFixtures.CreateW001());
            var inPlace = CreateRunningSession(FoundationFixtures.CreateW001());
            var snapshotCommands = new ProductionCommands(viaSnapshot);
            var inPlaceCommands = new ProductionCommands(inPlace);

            for (var tick = 0; tick < 20; tick++)
            {
                var expected = snapshotCommands.AdvanceSimulation(TickTicks);
                var actual = inPlaceCommands.AdvanceActiveTime(TickTicks);
                Assert.That(actual.Succeeded, Is.EqualTo(expected.Succeeded));
                Assert.That(inPlaceCommands.CurrentRevision, Is.EqualTo(expected.Value.Revision));
            }

            var expectedMachines = snapshotCommands.AdvanceSimulation(0L).Value.Machines;
            var actualMachines = inPlaceCommands.AdvanceSimulation(0L).Value.Machines;
            Assert.That(actualMachines.Count, Is.EqualTo(expectedMachines.Count));
            for (var index = 0; index < expectedMachines.Count; index++)
            {
                AssertSameInspection(actualMachines[index], expectedMachines[index]);
            }

            Action advance = () => inPlaceCommands.AdvanceActiveTime(TickTicks);
            GcAllocationCounter.Count(advance);
            var samples = new int[50];
            for (var sample = 0; sample < samples.Length; sample++)
            {
                samples[sample] = GcAllocationCounter.Count(advance);
            }

            Assert.That(samples, Has.All.EqualTo(0), "The per-frame advance allocates nothing at W001 scale.");
            Assert.That(GcAllocationCounter.Count(() => snapshotCommands.AdvanceSimulation(TickTicks)),
                Is.GreaterThan(FoundationFixtures.W001MachineCount),
                "Control: the snapshot-returning advance still allocates per machine, so the recorder is live.");
        }

        [Test]
        public void AdvanceActiveTimeRejectsLikeAdvanceSimulation()
        {
            var session = CreateRunningSession(FoundationFixtures.CreateD001());
            var commands = new ProductionCommands(session);
            var revision = commands.CurrentRevision;

            var negative = commands.AdvanceActiveTime(-1L);
            var negativeReference = commands.AdvanceSimulation(-1L);
            Assert.That(negative.Outcome, Is.EqualTo(CommandOutcome.InvalidElapsedTime));
            Assert.That(negative.Message, Is.EqualTo(negativeReference.Message));
            Assert.That(commands.CurrentRevision, Is.EqualTo(revision), "A rejected advance keeps the revision.");

            var unstarted = FoundationFixtures.CreateD001();
            var bootstrapping = new FactorySession(unstarted, unstarted.Catalog, new TestClock(), new TestCheckpointStore());
            var lifecycle = new ProductionCommands(bootstrapping).AdvanceActiveTime(TickTicks);
            var lifecycleReference = new ProductionCommands(bootstrapping).AdvanceSimulation(TickTicks);
            Assert.That(lifecycle.Succeeded, Is.False);
            Assert.That(lifecycle.Outcome, Is.EqualTo(lifecycleReference.Outcome));
            Assert.That(lifecycle.Message, Is.EqualTo(lifecycleReference.Message));
        }

        private static void AssertPollQueriesAllocateNothing(ProductionCommands commands)
        {
            // Each delegate is recorded once unmeasured: its first invocation may allocate runtime state.
            Action anyCanProduce = () => commands.AnyMachineCanProduce();
            Action readRevision = () => _ = commands.CurrentRevision;
            var firstAnyCanProduce = GcAllocationCounter.Count(anyCanProduce);
            GcAllocationCounter.Count(readRevision);
            Assert.That(GcAllocationCounter.Count(anyCanProduce), Is.Zero,
                $"AnyMachineCanProduce allocated (first recorded call: {firstAnyCanProduce}).");
            Assert.That(GcAllocationCounter.Count(readRevision), Is.Zero);
        }

        /// <summary>The pre-T063 inspector predicate, evaluated through full inspections.</summary>
        private static bool InspectionsSayAnyMachineCanProduce(FactorySession session) =>
            session.GetFactorySnapshot().Buildings.Any(building =>
            {
                var result = session.GetMachineInspection(building.InstanceId);
                return result.Succeeded && result.Value.IsActive && result.Value.RecipeId.HasValue &&
                       result.Value.Blockers.Count == 0;
            });

        private static void AssertSameInspection(MachineInspectionSnapshot actual, MachineInspectionSnapshot expected)
        {
            Assert.That(actual.Revision, Is.EqualTo(expected.Revision));
            Assert.That(actual.InstanceId, Is.EqualTo(expected.InstanceId));
            Assert.That(actual.DefinitionId, Is.EqualTo(expected.DefinitionId));
            Assert.That(actual.RecipeId, Is.EqualTo(expected.RecipeId));
            Assert.That(actual.IsActive, Is.EqualTo(expected.IsActive));
            Assert.That(actual.ProgressTicks, Is.EqualTo(expected.ProgressTicks));
            Assert.That(actual.DurationTicks, Is.EqualTo(expected.DurationTicks));
            Assert.That(Describe(actual), Is.EqualTo(Describe(expected)));
        }

        private static string Describe(MachineInspectionSnapshot inspection) =>
            string.Join(";", inspection.Inputs.Select(input => $"in:{input.ResourceId.Value}={input.Quantity}/{input.RequiredOrFreeCapacity}")) + "|" +
            string.Join(";", inspection.Outputs.Select(output => $"out:{output.ResourceId.Value}={output.Quantity}/{output.RequiredOrFreeCapacity}")) + "|" +
            string.Join(";", inspection.Blockers.Select(blocker => $"{blocker.Kind}:{blocker.ResourceId?.Value}:{blocker.Required}:{blocker.Available}"));

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
            Assert.That(result.Succeeded, Is.True, result.Message);
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

            public CommandResult<CheckpointId> RecoverPrevious(Checkpoint recoveryCandidate) =>
                CommandResult<CheckpointId>.Rejected(CommandOutcome.PersistenceFailed, "Not configured.");

            private static CheckpointReadResult Missing() =>
                CheckpointReadResult.Rejected(CommandOutcome.NoCheckpoint, "No checkpoint.");
        }
    }
}
