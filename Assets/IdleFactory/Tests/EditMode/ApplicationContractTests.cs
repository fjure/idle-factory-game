using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using IdleFactory.Core.Application;
using IdleFactory.Domain;
using NUnit.Framework;

namespace IdleFactory.Tests.EditMode
{
    public sealed class ApplicationContractTests
    {
        private static readonly ResourceId InputId = new ResourceId("dev.input");
        private static readonly ResourceId OutputId = new ResourceId("dev.output");
        private static readonly RecipeId RecipeId = new RecipeId("dev.recipe.basic");
        private static readonly BuildingDefinitionId BuildingId = new BuildingDefinitionId("dev.machine.basic");

        [Test]
        public void SessionOwnsDefensiveStateCopyAndRetainsImmutableCatalogReference()
        {
            var catalog = CreateCatalog();
            var source = CreateFactory(catalog, input: 4);
            var session = CreateSession(source, catalog, LifecycleState.Running);

            Assert.That(session.Catalog, Is.SameAs(catalog));
            Assert.That(RemoveInput(source.Buildings[Id(1)], 3), Is.EqualTo(BufferOperationResult.Success));

            var snapshot = session.GetFactorySnapshot();
            var inspection = session.GetMachineInspection(Id(1)).Value;
            Assert.That(snapshot.Buildings.Single().IsActive, Is.True);
            Assert.That(inspection.Inputs.Single().Quantity, Is.EqualTo(4));
        }

        [Test]
        public void FactoryAndInspectionSnapshotsDefensivelyCopyCollections()
        {
            var catalog = CreateCatalog();
            var sourceBuildings = new List<BuildingSnapshot>
            {
                new BuildingSnapshot(Id(1), BuildingId, new GridPosition(0, 0), 0, true),
            };
            var sourceOccupancy = new Dictionary<GridPosition, BuildingInstanceId>
            {
                { new GridPosition(0, 0), Id(1) },
            };
            var snapshot = new FactorySnapshot(Bounds(), 3, sourceBuildings, sourceOccupancy);

            sourceBuildings.Clear();
            sourceOccupancy.Clear();
            Assert.That(snapshot.Buildings.Count, Is.EqualTo(1));
            Assert.That(snapshot.Occupancy.Count, Is.EqualTo(1));
            Assert.That(
                () => ((IList<BuildingSnapshot>)snapshot.Buildings).Clear(),
                Throws.TypeOf<NotSupportedException>());
            Assert.That(
                () => ((IDictionary<GridPosition, BuildingInstanceId>)snapshot.Occupancy).Clear(),
                Throws.TypeOf<NotSupportedException>());

            var inputs = new List<ResourceQuantitySnapshot> { new ResourceQuantitySnapshot(InputId, 4, 2) };
            var blockers = new List<ProductionBlockerSnapshot>();
            var inspection = new MachineInspectionSnapshot(
                3, Id(1), BuildingId, RecipeId, true, 0, 50, inputs,
                new[] { new ResourceQuantitySnapshot(OutputId, 0, 1) }, blockers);
            inputs.Clear();
            blockers.Add(new ProductionBlockerSnapshot(ProductionBlockerKind.Inactive, null, 0, 0));

            Assert.That(inspection.Inputs.Count, Is.EqualTo(1));
            Assert.That(inspection.Blockers, Is.Empty);
            Assert.That(
                () => ((IList<ResourceQuantitySnapshot>)inspection.Inputs).Clear(),
                Throws.TypeOf<NotSupportedException>());
        }

        [Test]
        public void OfflineSummaryDefensivelyCopiesResultsAndCarriesDurableTrace()
        {
            var sourceId = new CheckpointId(new Guid(10, 0, 0, new byte[8]));
            var resultId = new CheckpointId(new Guid(11, 0, 0, new byte[8]));
            var deltas = new List<ResourceDeltaSnapshot>
            {
                new ResourceDeltaSnapshot(Id(1), OutputId, 2),
            };
            var limitations = new List<OfflineLimitationSnapshot>
            {
                new OfflineLimitationSnapshot(Id(1), ProductionBlockerKind.MissingInput, InputId),
            };
            var summary = new OfflineReturnSummary(
                OfflineReturnOutcome.Applied,
                100,
                100,
                80,
                deltas,
                limitations,
                sourceId,
                resultId);

            deltas.Clear();
            limitations.Clear();
            Assert.That(summary.ResourceDeltas.Single().Delta, Is.EqualTo(2));
            Assert.That(summary.Limitations.Single().ResourceId, Is.EqualTo(InputId));
            Assert.That(summary.SourceCheckpointId, Is.EqualTo(sourceId));
            Assert.That(summary.ResultCheckpointId, Is.EqualTo(resultId));
            Assert.That(
                () => ((IList<ResourceDeltaSnapshot>)summary.ResourceDeltas).Clear(),
                Throws.TypeOf<NotSupportedException>());
        }

        [Test]
        public void OfflineSummaryRejectsInvalidDurationRelationsAndAppliedWithoutDurableTrace()
        {
            var sourceId = new CheckpointId(new Guid(10, 0, 0, new byte[8]));
            var resultId = new CheckpointId(new Guid(11, 0, 0, new byte[8]));

            Assert.That(
                () => CreateOfflineSummary(OfflineReturnOutcome.NoEligibleProduction, 10, 11, 8, null, null),
                Throws.TypeOf<ArgumentOutOfRangeException>());
            Assert.That(
                () => CreateOfflineSummary(OfflineReturnOutcome.NoEligibleProduction, 10, 10, 11, null, null),
                Throws.TypeOf<ArgumentOutOfRangeException>());
            Assert.That(
                () => CreateOfflineSummary(OfflineReturnOutcome.Applied, 10, 10, 8, null, resultId),
                Throws.InstanceOf<ArgumentException>());
            Assert.That(
                () => CreateOfflineSummary(OfflineReturnOutcome.Applied, 10, 10, 8, sourceId, null),
                Throws.InstanceOf<ArgumentException>());
            Assert.That(
                () => CreateOfflineSummary(
                    OfflineReturnOutcome.Applied,
                    10,
                    10,
                    8,
                    default(CheckpointId),
                    resultId),
                Throws.InstanceOf<ArgumentException>());
            Assert.That(
                () => CreateOfflineSummary(
                    OfflineReturnOutcome.Applied,
                    10,
                    10,
                    8,
                    sourceId,
                    default(CheckpointId)),
                Throws.InstanceOf<ArgumentException>());

            var valid = CreateOfflineSummary(OfflineReturnOutcome.Applied, 10, 10, 8, sourceId, resultId);
            Assert.That(valid.SourceCheckpointId, Is.EqualTo(sourceId));
            Assert.That(valid.ResultCheckpointId, Is.EqualTo(resultId));
        }

        [Test]
        public void ExpectedRejectionsUseTypedOutcomesWithoutExceptions()
        {
            var rejected = CommandResult<int>.Rejected(CommandOutcome.NotFound, "Unknown building.");

            Assert.That(rejected.Succeeded, Is.False);
            Assert.That(rejected.Outcome, Is.EqualTo(CommandOutcome.NotFound));
            Assert.That(rejected.Value, Is.Zero);
            Assert.That(rejected.Message, Is.EqualTo("Unknown building."));
            Assert.That(() => CommandResult.Rejected(CommandOutcome.Success, string.Empty), Throws.TypeOf<ArgumentException>());

            var session = CreateSession(lifecycleState: LifecycleState.Running);
            Assert.That(session.GetMachineInspection(default(BuildingInstanceId)).Outcome, Is.EqualTo(CommandOutcome.InvalidRequest));
            Assert.That(session.GetMachineInspection(Id(99)).Outcome, Is.EqualTo(CommandOutcome.NotFound));
        }

        [Test]
        public void LifecycleTransitionsFollowDocumentedGraphWithoutThrowingForRejection()
        {
            var session = CreateSession();

            Assert.That(Transition(session, LifecycleState.Running).Outcome, Is.EqualTo(CommandOutcome.InvalidRequest));
            Assert.That(session.LifecycleState, Is.EqualTo(LifecycleState.Bootstrapping));
            Assert.That(Transition(session, LifecycleState.Loading).Succeeded, Is.True);
            Assert.That(Transition(session, LifecycleState.Running).Succeeded, Is.True);
            Assert.That(Transition(session, LifecycleState.Checkpointing).Succeeded, Is.True);
            Assert.That(Transition(session, LifecycleState.Suspended).Succeeded, Is.True);
            Assert.That(Transition(session, LifecycleState.Reconciling).Succeeded, Is.True);
            Assert.That(Transition(session, LifecycleState.ReturnBlocked).Succeeded, Is.True);
            Assert.That(Transition(session, LifecycleState.Loading).Succeeded, Is.True);
            Assert.That(Transition(session, LifecycleState.Running).Succeeded, Is.True);
            Assert.That(Transition(session, LifecycleState.Running).Outcome, Is.EqualTo(CommandOutcome.LifecycleNoOp));
        }

        [Test]
        public void PublicSessionApiCannotInitializeOrTransitionLifecycleArbitrarily()
        {
            var constructor = typeof(FactorySession).GetConstructors().Single();

            Assert.That(constructor.GetParameters().Select(parameter => parameter.ParameterType),
                Has.None.EqualTo(typeof(LifecycleState)));
            Assert.That(typeof(FactorySession).GetMethod("TransitionTo", BindingFlags.Instance | BindingFlags.Public), Is.Null);
            Assert.That(CreateSession().LifecycleState, Is.EqualTo(LifecycleState.Bootstrapping));
        }

        [Test]
        public void AdmissionCentralizesRunningSuspendedAndReturnBlockedRules()
        {
            var runningStore = new TestCheckpointStore();
            var running = CreateSession(lifecycleState: LifecycleState.Running, store: runningStore);
            Assert.That(running.Admit(ApplicationOperation.OrdinaryMutation).Succeeded, Is.True);
            Assert.That(running.Admit(ApplicationOperation.Save).Succeeded, Is.True);
            Assert.That(running.Admit(ApplicationOperation.RetryReturn).Outcome, Is.EqualTo(CommandOutcome.LifecycleNoOp));
            Assert.That(running.Admit(ApplicationOperation.RecoverPreviousCheckpoint).Outcome, Is.EqualTo(CommandOutcome.NoCheckpoint));

            var suspended = CreateSession(lifecycleState: LifecycleState.Suspended);
            Assert.That(suspended.Admit(ApplicationOperation.ResumeAndReconcile).Succeeded, Is.True);
            Assert.That(suspended.Admit(ApplicationOperation.OrdinaryMutation).Outcome, Is.EqualTo(CommandOutcome.LifecycleNoOp));

            var blockedStore = new TestCheckpointStore();
            var blocked = CreateSession(lifecycleState: LifecycleState.ReturnBlocked, store: blockedStore);
            Assert.That(blocked.Admit(ApplicationOperation.RetryReturn).Succeeded, Is.True);
            Assert.That(blocked.Admit(ApplicationOperation.ReloadAuthoritativeGeneration).Succeeded, Is.True);
            Assert.That(blocked.Admit(ApplicationOperation.RecoverPreviousCheckpoint).Outcome, Is.EqualTo(CommandOutcome.NoCheckpoint));
            Assert.That(blocked.Admit(ApplicationOperation.StatusQuery).Succeeded, Is.True);
            Assert.That(blocked.Admit(ApplicationOperation.OrdinaryMutation).Outcome, Is.EqualTo(CommandOutcome.ReturnBlocked));
            Assert.That(blocked.Admit(ApplicationOperation.Save).Outcome, Is.EqualTo(CommandOutcome.ReturnBlocked));

            var catalog = CreateCatalog();
            var previous = new Checkpoint(
                new CheckpointId(new Guid(30, 0, 0, new byte[8])),
                1,
                new DateTimeOffset(2030, 1, 1, 0, 0, 0, TimeSpan.Zero),
                new OfflinePolicyRevision(1),
                null,
                CreateFactory(catalog));
            runningStore.Previous = CheckpointReadResult.Success(previous);
            blockedStore.Previous = CheckpointReadResult.Success(previous);
            Assert.That(running.Admit(ApplicationOperation.RecoverPreviousCheckpoint).Succeeded, Is.True);
            Assert.That(blocked.Admit(ApplicationOperation.RecoverPreviousCheckpoint).Succeeded, Is.True);
        }

        [Test]
        public void ControllableClockAndCheckpointStoreRemainEngineFreePorts()
        {
            var now = new DateTimeOffset(2030, 4, 5, 6, 7, 8, TimeSpan.Zero);
            var clock = new TestClock(now);
            var store = new TestCheckpointStore();
            var catalog = CreateCatalog();
            var checkpoint = new Checkpoint(
                new CheckpointId(new Guid(20, 0, 0, new byte[8])),
                7,
                now,
                new OfflinePolicyRevision(1),
                null,
                CreateFactory(catalog));

            store.Active = CheckpointReadResult.Success(checkpoint);
            Assert.That(clock.GetUtcNow(), Is.EqualTo(now));
            clock.UtcNow = now.AddMinutes(2);
            Assert.That(clock.GetUtcNow(), Is.EqualTo(now.AddMinutes(2)));
            Assert.That(store.ReadActive().Checkpoint.Id, Is.EqualTo(checkpoint.Id));
            Assert.That(store.Promote(checkpoint).Value, Is.EqualTo(checkpoint.Id));
            Assert.That(store.Promoted, Is.SameAs(checkpoint));
            Assert.That(
                typeof(FactorySession).Assembly.GetReferencedAssemblies().Select(reference => reference.Name),
                Has.None.StartsWith("UnityEngine"));
        }

        private static FactorySession CreateSession(
            FactoryState state = null,
            DefinitionCatalog catalog = null,
            LifecycleState lifecycleState = LifecycleState.Bootstrapping,
            TestCheckpointStore store = null)
        {
            catalog = catalog ?? CreateCatalog();
            state = state ?? CreateFactory(catalog);
            var session = new FactorySession(
                state,
                catalog,
                new TestClock(new DateTimeOffset(1970, 1, 1, 0, 0, 0, TimeSpan.Zero)),
                store ?? new TestCheckpointStore());
            MoveTo(session, lifecycleState);
            return session;
        }

        private static void MoveTo(FactorySession session, LifecycleState target)
        {
            if (target == LifecycleState.Bootstrapping)
            {
                return;
            }

            Assert.That(Transition(session, LifecycleState.Loading).Succeeded, Is.True);
            if (target == LifecycleState.Loading)
            {
                return;
            }

            Assert.That(Transition(session, LifecycleState.Running).Succeeded, Is.True);
            if (target == LifecycleState.Running)
            {
                return;
            }

            Assert.That(Transition(session, LifecycleState.Checkpointing).Succeeded, Is.True);
            Assert.That(Transition(session, LifecycleState.Suspended).Succeeded, Is.True);
            if (target == LifecycleState.Suspended)
            {
                return;
            }

            Assert.That(Transition(session, LifecycleState.Reconciling).Succeeded, Is.True);
            if (target == LifecycleState.Reconciling)
            {
                return;
            }

            Assert.That(target, Is.EqualTo(LifecycleState.ReturnBlocked));
            Assert.That(Transition(session, LifecycleState.ReturnBlocked).Succeeded, Is.True);
        }

        private static CommandResult Transition(FactorySession session, LifecycleState nextState)
        {
            var method = typeof(FactorySession).GetMethod("TransitionTo", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null);
            return (CommandResult)method.Invoke(session, new object[] { nextState });
        }

        private static OfflineReturnSummary CreateOfflineSummary(
            OfflineReturnOutcome outcome,
            long measuredTicks,
            long creditedTicks,
            long effectiveTicks,
            CheckpointId? sourceId,
            CheckpointId? resultId)
        {
            return new OfflineReturnSummary(
                outcome,
                measuredTicks,
                creditedTicks,
                effectiveTicks,
                Array.Empty<ResourceDeltaSnapshot>(),
                Array.Empty<OfflineLimitationSnapshot>(),
                sourceId,
                resultId);
        }

        private static BufferOperationResult RemoveInput(BuildingInstanceState building, long amount)
        {
            var method = typeof(ResourceBufferState).GetMethod("TryRemove", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null);
            return (BufferOperationResult)method.Invoke(building.InputBuffer, new object[] { InputId, amount });
        }

        private static FactoryState CreateFactory(DefinitionCatalog catalog, long input = 4)
        {
            var definition = catalog.Resolve(BuildingId);
            var building = new BuildingInstanceState(
                Id(1),
                definition,
                new GridPosition(0, 0),
                0,
                true,
                new ResourceBufferState(definition.InputBuffer, Pair(InputId, input)),
                new ResourceBufferState(definition.OutputBuffer, Pair(OutputId, 0)),
                new ProductionState(catalog.Resolve(RecipeId), 0));
            return new FactoryState(Bounds(), catalog, new[] { building });
        }

        private static DefinitionCatalog CreateCatalog()
        {
            var recipe = new RecipeDefinition(
                RecipeId,
                new[] { new ResourceAmountDefinition(InputId, 2) },
                new[] { new ResourceAmountDefinition(OutputId, 1) },
                50,
                true);
            var building = new BuildingDefinition(
                BuildingId,
                new Footprint(1, 1),
                new[] { 0 },
                RecipeId,
                new BufferDefinition(BufferRole.Input, Pair(InputId, 4)),
                new BufferDefinition(BufferRole.Output, Pair(OutputId, 2)),
                "dev.machine.basic.sprite",
                true);
            return new DefinitionCatalog(
                new[]
                {
                    new ResourceDefinition(InputId, "Input", null, true),
                    new ResourceDefinition(OutputId, "Output", null, true),
                },
                new[] { recipe },
                new[] { building },
                new[] { new OfflinePolicyDefinition(new OfflinePolicyRevision(1), 4, 5, 288000000000L) });
        }

        private static FactoryBounds Bounds() => new FactoryBounds(new GridPosition(0, 0), 8, 8);

        private static BuildingInstanceId Id(int value) =>
            new BuildingInstanceId(new Guid(value, 0, 0, new byte[8]));

        private static KeyValuePair<ResourceId, long>[] Pair(ResourceId id, long value) =>
            new[] { new KeyValuePair<ResourceId, long>(id, value) };

        private sealed class TestClock : IUtcClock
        {
            public TestClock(DateTimeOffset utcNow)
            {
                UtcNow = utcNow;
            }

            public DateTimeOffset UtcNow { get; set; }
            public DateTimeOffset GetUtcNow() => UtcNow;
        }

        private sealed class TestCheckpointStore : ICheckpointStore
        {
            public CheckpointReadResult Active { get; set; } =
                CheckpointReadResult.Rejected(CommandOutcome.NoCheckpoint, "No active checkpoint.");

            public CheckpointReadResult Previous { get; set; } =
                CheckpointReadResult.Rejected(CommandOutcome.NoCheckpoint, "No previous checkpoint.");

            public Checkpoint Promoted { get; private set; }

            public CheckpointReadResult ReadActive() => Active;

            public CheckpointReadResult ReadPrevious() => Previous;

            public CheckpointReadResult Read(CheckpointId checkpointId) =>
                Active.Succeeded && Active.Checkpoint.Id == checkpointId
                    ? Active
                    : CheckpointReadResult.Rejected(CommandOutcome.NoCheckpoint, "Checkpoint not found.");

            public CommandResult<CheckpointId> Promote(Checkpoint checkpoint)
            {
                Promoted = checkpoint;
                return CommandResult<CheckpointId>.Success(checkpoint.Id);
            }
        }
    }
}
