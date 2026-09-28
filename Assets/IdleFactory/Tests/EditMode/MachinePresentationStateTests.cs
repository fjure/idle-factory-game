using System;
using System.Linq;
using System.Reflection;
using IdleFactory.Core.Application;
using IdleFactory.Domain;
using IdleFactory.Tests.EditMode.Fixtures;
using NUnit.Framework;

namespace IdleFactory.Tests.EditMode
{
    /// <summary>T062: the allocation-free presented machine state that keeps world badges and the summary current.</summary>
    public sealed class MachinePresentationStateTests
    {
        private const long ActiveAdvanceTicks = TimeSpan.TicksPerMillisecond * 100L;

        [Test]
        public void W001CopyMatchesInspectionForEveryMachine()
        {
            var session = CreateRunningSession(FoundationFixtures.CreateW001());
            var states = new MachinePresentationState[FoundationFixtures.W001MachineCount];

            Assert.That(session.CopyMachinePresentationStates(states), Is.EqualTo(FoundationFixtures.W001MachineCount));
            AssertMatchesInspections(session, states);
            Assert.That(states.Count(state => state.State == MachinePresentedState.Producing),
                Is.EqualTo(FoundationFixtures.W001ActiveUnblockedCount));
            Assert.That(states.Count(state => state.State == MachinePresentedState.FullOutput),
                Is.EqualTo(FoundationFixtures.W001ActiveBlockedCount));
            Assert.That(states.Count(state => state.State == MachinePresentedState.Inactive),
                Is.EqualTo(FoundationFixtures.W001InactiveCount));
            Assert.That(states.Select(state => state.InstanceId), Is.Ordered, "States follow instance-ID order.");
        }

        [Test]
        public void W001CopyAfterEachActiveTickAllocatesNoManagedMemory()
        {
            var session = CreateRunningSession(FoundationFixtures.CreateW001());
            var commands = new ProductionCommands(session);
            var states = new MachinePresentationState[FoundationFixtures.W001MachineCount];
            for (var warmup = 0; warmup < 10; warmup++)
            {
                Assert.That(commands.AdvanceSimulation(ActiveAdvanceTicks).Succeeded, Is.True);
                session.CopyMachinePresentationStates(states);
            }

            var samples = new long[200];
            for (var sample = 0; sample < samples.Length; sample++)
            {
                Assert.That(commands.AdvanceSimulation(ActiveAdvanceTicks).Succeeded, Is.True);
                samples[sample] = GcAllocationCounter.Count(() => session.CopyMachinePresentationStates(states));
            }

            Assert.That(samples, Has.All.EqualTo(0L));
            AssertMatchesInspections(session, states);
        }

        [Test]
        public void CompletedCycleChangesPresentedStateButProgressAloneDoesNot()
        {
            var session = CreateRunningSession(FoundationFixtures.CreateD001());
            var commands = new ProductionCommands(session);
            var states = new MachinePresentationState[1];
            session.CopyMachinePresentationStates(states);
            var initial = states[0];
            Assert.That(initial.State, Is.EqualTo(MachinePresentedState.Producing));
            Assert.That(initial.BlockerCount, Is.Zero);

            Assert.That(commands.AdvanceSimulation(FoundationFixtures.RecipeDurationTicks / 2).Succeeded, Is.True);
            session.CopyMachinePresentationStates(states);
            Assert.That(states[0], Is.EqualTo(initial), "Progress is not part of the presented state.");

            Assert.That(commands.AdvanceSimulation(FoundationFixtures.RecipeDurationTicks).Succeeded, Is.True);
            session.CopyMachinePresentationStates(states);
            Assert.That(states[0], Is.Not.EqualTo(initial));
            Assert.That(states[0].State, Is.EqualTo(MachinePresentedState.FullOutput));
            Assert.That(states[0].BlockerCount, Is.EqualTo(1));
            AssertMatchesInspections(session, states);

            Assert.That(commands.SetBuildingActive(states[0].InstanceId, false).Succeeded, Is.True);
            session.CopyMachinePresentationStates(states);
            Assert.That(states[0].State, Is.EqualTo(MachinePresentedState.Inactive));
            Assert.That(states[0].BlockerCount, Is.EqualTo(2));
            AssertMatchesInspections(session, states);
        }

        [Test]
        public void UndersizedBufferReportsCountAndNullBufferIsRejected()
        {
            var session = CreateRunningSession(FoundationFixtures.CreateW001());
            var states = new MachinePresentationState[3];

            Assert.That(session.CopyMachinePresentationStates(states), Is.EqualTo(FoundationFixtures.W001MachineCount));
            Assert.That(states.All(state => state.HasRecipe), Is.True, "Only the available entries are written.");
            Assert.That(session.CopyMachinePresentationStates(Array.Empty<MachinePresentationState>()),
                Is.EqualTo(FoundationFixtures.W001MachineCount));
            Assert.That(() => session.CopyMachinePresentationStates(null), Throws.ArgumentNullException);
        }

        private static void AssertMatchesInspections(FactorySession session, MachinePresentationState[] states)
        {
            foreach (var state in states)
            {
                var inspection = session.GetMachineInspection(state.InstanceId);
                Assert.That(inspection.Succeeded, Is.True);
                Assert.That(state, Is.EqualTo(MachinePresentationState.FromInspection(inspection.Value)));
                Assert.That(state.BlockerCount, Is.EqualTo(inspection.Value.Blockers.Count));
            }
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
