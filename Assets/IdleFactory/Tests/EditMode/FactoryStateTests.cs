using System;
using System.Collections.Generic;
using System.Linq;
using IdleFactory.Domain;
using NUnit.Framework;

namespace IdleFactory.Tests.EditMode
{
    public sealed class FactoryStateTests
    {
        private static readonly ResourceId InputId = new ResourceId("dev.input");
        private static readonly ResourceId OutputId = new ResourceId("dev.output");
        private static readonly RecipeId RecipeId = new RecipeId("dev.recipe.basic");
        private static readonly BuildingDefinitionId BuildingId = new BuildingDefinitionId("dev.machine.basic");

        [Test]
        public void ValidD001StateOwnsIndependentMutableStateAndReconstructsOccupancy()
        {
            var catalog = CreateCatalog();
            var firstSource = CreateBuilding(catalog, Id(1), new GridPosition(0, 0), input: 4, output: 0);
            var secondSource = CreateBuilding(catalog, Id(2), new GridPosition(4, 3), input: 2, output: 1);

            var factory = new FactoryState(Bounds(), catalog, new[] { firstSource, secondSource });
            var first = factory.Buildings[Id(1)];
            var second = factory.Buildings[Id(2)];

            Assert.That(factory.Bounds, Is.EqualTo(Bounds()));
            Assert.That(factory.Occupancy.Count, Is.EqualTo(8));
            Assert.That(factory.Occupancy[new GridPosition(1, 1)], Is.EqualTo(Id(1)));
            Assert.That(factory.Occupancy[new GridPosition(5, 4)], Is.EqualTo(Id(2)));
            Assert.That(first, Is.Not.SameAs(firstSource));
            Assert.That(first.InputBuffer, Is.Not.SameAs(firstSource.InputBuffer));
            Assert.That(first.InputBuffer, Is.Not.SameAs(second.InputBuffer));

            Assert.That(first.InputBuffer.TryRemove(InputId, 1), Is.EqualTo(BufferOperationResult.Success));
            Assert.That(first.InputBuffer.Quantity(InputId), Is.EqualTo(3));
            Assert.That(firstSource.InputBuffer.Quantity(InputId), Is.EqualTo(4));
            Assert.That(second.InputBuffer.Quantity(InputId), Is.EqualTo(2));
        }

        [Test]
        public void BufferRejectsInvalidCandidatesAndFailedOperationsWithoutMutation()
        {
            var definition = Buffer(BufferRole.Input, InputId, 4);
            var buffer = new ResourceBufferState(definition, Quantities(InputId, 3));

            Assert.That(() => new ResourceBufferState(definition, null), Throws.TypeOf<ArgumentNullException>());
            Assert.That(() => new ResourceBufferState(definition, Array.Empty<KeyValuePair<ResourceId, long>>()), Throws.InstanceOf<ArgumentException>());
            Assert.That(() => new ResourceBufferState(definition, Quantities(OutputId, 0)), Throws.InstanceOf<ArgumentException>());
            Assert.That(() => new ResourceBufferState(definition, Quantities(InputId, -1)), Throws.TypeOf<ArgumentOutOfRangeException>());
            Assert.That(() => new ResourceBufferState(definition, Quantities(InputId, 5)), Throws.TypeOf<ArgumentOutOfRangeException>());
            Assert.That(
                () => new ResourceBufferState(definition, new[] { Pair(InputId, 0), Pair(InputId, 1) }),
                Throws.InstanceOf<ArgumentException>());

            Assert.That(buffer.TryAdd(InputId, 0), Is.EqualTo(BufferOperationResult.InvalidAmount));
            Assert.That(buffer.TryAdd(OutputId, 1), Is.EqualTo(BufferOperationResult.UnknownResource));
            Assert.That(buffer.TryAdd(InputId, 2), Is.EqualTo(BufferOperationResult.InsufficientCapacity));
            Assert.That(buffer.TryRemove(InputId, -1), Is.EqualTo(BufferOperationResult.InvalidAmount));
            Assert.That(buffer.TryRemove(InputId, 4), Is.EqualTo(BufferOperationResult.InsufficientQuantity));
            Assert.That(buffer.Quantity(InputId), Is.EqualTo(3));
        }

        [Test]
        public void BufferAcceptsExactCapacityAndStockBoundariesAndExposesReadOnlyQuantities()
        {
            var buffer = new ResourceBufferState(Buffer(BufferRole.Input, InputId, 4), Quantities(InputId, 0));

            Assert.That(buffer.TryAdd(InputId, 4), Is.EqualTo(BufferOperationResult.Success));
            Assert.That(buffer.FreeCapacity(InputId), Is.Zero);
            Assert.That(buffer.TryRemove(InputId, 4), Is.EqualTo(BufferOperationResult.Success));
            Assert.That(buffer.Quantity(InputId), Is.Zero);
            Assert.That(
                () => ((IDictionary<ResourceId, long>)buffer.Quantities).Add(OutputId, 1),
                Throws.TypeOf<NotSupportedException>());
        }

        [Test]
        public void ProductionRequiresValidProgressAndMatchingImmutableReferences()
        {
            var catalog = CreateCatalog();
            var recipe = catalog.Resolve(RecipeId);
            var building = catalog.Resolve(BuildingId);
            var otherRecipe = new RecipeDefinition(
                RecipeId,
                Amounts(InputId, 2),
                Amounts(OutputId, 1),
                50000000L,
                true);

            Assert.That(new ProductionState(recipe, 49999999L).ProgressTicks, Is.EqualTo(49999999L));
            Assert.That(() => new ProductionState(recipe, -1), Throws.TypeOf<ArgumentOutOfRangeException>());
            Assert.That(() => new ProductionState(recipe, recipe.DurationTicks), Throws.TypeOf<ArgumentOutOfRangeException>());
            Assert.That(
                () => new BuildingInstanceState(
                    Id(1), building, new GridPosition(0, 0), 0, true,
                    new ResourceBufferState(building.InputBuffer, Quantities(InputId, 4)),
                    new ResourceBufferState(building.OutputBuffer, Quantities(OutputId, 0)),
                    new ProductionState(otherRecipe, 0)),
                Throws.Nothing);

            var staleDefinitionBuilding = new BuildingInstanceState(
                Id(1), building, new GridPosition(0, 0), 0, true,
                new ResourceBufferState(building.InputBuffer, Quantities(InputId, 4)),
                new ResourceBufferState(building.OutputBuffer, Quantities(OutputId, 0)),
                new ProductionState(otherRecipe, 0));
            Assert.That(
                () => new FactoryState(Bounds(), catalog, new[] { staleDefinitionBuilding }),
                Throws.InstanceOf<ArgumentException>());
        }

        [Test]
        public void DerivedBlockersExposeEveryCurrentConditionWithoutStoredAlternateState()
        {
            var catalog = CreateCatalog();
            var building = CreateBuilding(catalog, Id(1), new GridPosition(0, 0), input: 1, output: 1, active: false, progress: 25);
            var factory = new FactoryState(Bounds(), catalog, new[] { building });
            var owned = factory.Buildings[Id(1)];

            var blockers = owned.GetProductionBlockers();
            Assert.That(
                blockers.Select(blocker => blocker.Kind),
                Is.EqualTo(new[]
                {
                    ProductionBlockerKind.Inactive,
                    ProductionBlockerKind.MissingInput,
                    ProductionBlockerKind.InsufficientOutputCapacity,
                }));
            Assert.That(blockers[1].ResourceId, Is.EqualTo(InputId));
            Assert.That(blockers[1].Required, Is.EqualTo(2));
            Assert.That(blockers[1].Available, Is.EqualTo(1));

            owned.SetActive(true);
            Assert.That(owned.InputBuffer.TryAdd(InputId, 1), Is.EqualTo(BufferOperationResult.Success));
            Assert.That(owned.OutputBuffer.TryRemove(OutputId, 1), Is.EqualTo(BufferOperationResult.Success));
            Assert.That(owned.GetProductionBlockers(), Is.Empty);
            Assert.That(owned.Production.ProgressTicks, Is.EqualTo(25));
        }

        [Test]
        public void FactoryRejectsDuplicateOverlapAndOutOfBoundsCandidatesWithoutChangingLiveState()
        {
            var catalog = CreateCatalog();
            var live = new FactoryState(Bounds(), catalog, new[] { CreateBuilding(catalog, Id(1), new GridPosition(0, 0)) });
            var liveBuilding = live.Buildings[Id(1)];
            var originalQuantity = liveBuilding.InputBuffer.Quantity(InputId);

            Assert.That(
                () => new FactoryState(Bounds(), catalog, new[]
                {
                    CreateBuilding(catalog, Id(2), new GridPosition(4, 4)),
                    CreateBuilding(catalog, Id(2), new GridPosition(8, 8)),
                }),
                Throws.InstanceOf<ArgumentException>());
            Assert.That(
                () => new FactoryState(Bounds(), catalog, new[]
                {
                    CreateBuilding(catalog, Id(2), new GridPosition(4, 4)),
                    CreateBuilding(catalog, Id(3), new GridPosition(5, 5)),
                }),
                Throws.InstanceOf<ArgumentException>());
            Assert.That(
                () => new FactoryState(Bounds(), catalog, new[]
                {
                    CreateBuilding(catalog, Id(2), new GridPosition(15, 15)),
                }),
                Throws.InstanceOf<ArgumentException>());

            Assert.That(live.Buildings.Count, Is.EqualTo(1));
            Assert.That(live.Occupancy.Count, Is.EqualTo(4));
            Assert.That(liveBuilding.InputBuffer.Quantity(InputId), Is.EqualTo(originalQuantity));
        }

        [Test]
        public void FactoryRejectsUnknownOrStaleDefinitionReferencesAndInvalidOrientation()
        {
            var catalog = CreateCatalog();
            var separateCatalog = CreateCatalog();
            var staleBuilding = CreateBuilding(separateCatalog, Id(1), new GridPosition(0, 0));

            Assert.That(() => new FactoryState(Bounds(), catalog, new[] { staleBuilding }), Throws.InstanceOf<ArgumentException>());
            Assert.That(
                () => new BuildingInstanceState(
                    Id(1), catalog.Resolve(BuildingId), new GridPosition(0, 0), 1, true,
                    BufferState(catalog.Resolve(BuildingId).InputBuffer, InputId, 4),
                    BufferState(catalog.Resolve(BuildingId).OutputBuffer, OutputId, 0),
                    new ProductionState(catalog.Resolve(RecipeId), 0)),
                Throws.TypeOf<ArgumentOutOfRangeException>());
        }

        [Test]
        public void FactoryBoundsSupportNegativeOriginsAndRejectInvalidOrOverflowingDimensions()
        {
            var bounds = new FactoryBounds(new GridPosition(-4, -3), 8, 6);

            Assert.That(bounds.Contains(new GridPosition(-4, -3)), Is.True);
            Assert.That(bounds.Contains(new GridPosition(3, 2)), Is.True);
            Assert.That(bounds.Contains(new GridPosition(4, 2)), Is.False);
            Assert.That(() => new FactoryBounds(new GridPosition(0, 0), 0, 1), Throws.TypeOf<ArgumentOutOfRangeException>());
            Assert.That(() => new FactoryBounds(new GridPosition(int.MaxValue, 0), 2, 1), Throws.TypeOf<OverflowException>());
            Assert.That(() => default(FactoryBounds).Contains(new GridPosition(0, 0)), Throws.TypeOf<InvalidOperationException>());
            Assert.That(
                () => new FactoryState(default(FactoryBounds), CreateCatalog(), Array.Empty<BuildingInstanceState>()),
                Throws.TypeOf<InvalidOperationException>());
        }

        [Test]
        public void StateCollectionsAreReadOnlyAndCoreHasNoUnityDependency()
        {
            var catalog = CreateCatalog();
            var factory = new FactoryState(Bounds(), catalog, new[] { CreateBuilding(catalog, Id(1), new GridPosition(0, 0)) });

            Assert.That(
                () => ((IDictionary<BuildingInstanceId, BuildingInstanceState>)factory.Buildings).Clear(),
                Throws.TypeOf<NotSupportedException>());
            Assert.That(
                () => ((IDictionary<GridPosition, BuildingInstanceId>)factory.Occupancy).Clear(),
                Throws.TypeOf<NotSupportedException>());
            Assert.That(
                typeof(FactoryState).Assembly.GetReferencedAssemblies().Select(reference => reference.Name),
                Has.None.StartsWith("UnityEngine"));
        }

        private static FactoryBounds Bounds() => new FactoryBounds(new GridPosition(0, 0), 16, 16);

        private static BuildingInstanceState CreateBuilding(
            DefinitionCatalog catalog,
            BuildingInstanceId id,
            GridPosition anchor,
            long input = 4,
            long output = 0,
            bool active = true,
            long progress = 0)
        {
            var definition = catalog.Resolve(BuildingId);
            return new BuildingInstanceState(
                id,
                definition,
                anchor,
                0,
                active,
                BufferState(definition.InputBuffer, InputId, input),
                BufferState(definition.OutputBuffer, OutputId, output),
                new ProductionState(catalog.Resolve(RecipeId), progress));
        }

        private static DefinitionCatalog CreateCatalog()
        {
            var recipe = new RecipeDefinition(RecipeId, Amounts(InputId, 2), Amounts(OutputId, 1), 50000000L, true);
            var building = new BuildingDefinition(
                BuildingId,
                new Footprint(2, 2),
                new[] { 0 },
                RecipeId,
                Buffer(BufferRole.Input, InputId, 4),
                Buffer(BufferRole.Output, OutputId, 1),
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

        private static BuildingInstanceId Id(int value)
        {
            return new BuildingInstanceId(new Guid(value, 0, 0, new byte[8]));
        }

        private static BufferDefinition Buffer(BufferRole role, ResourceId id, long capacity)
        {
            return new BufferDefinition(role, new[] { Pair(id, capacity) });
        }

        private static ResourceBufferState BufferState(BufferDefinition definition, ResourceId id, long quantity)
        {
            return new ResourceBufferState(definition, Quantities(id, quantity));
        }

        private static ResourceAmountDefinition[] Amounts(ResourceId id, long amount)
        {
            return new[] { new ResourceAmountDefinition(id, amount) };
        }

        private static KeyValuePair<ResourceId, long>[] Quantities(ResourceId id, long quantity)
        {
            return new[] { Pair(id, quantity) };
        }

        private static KeyValuePair<ResourceId, long> Pair(ResourceId id, long value)
        {
            return new KeyValuePair<ResourceId, long>(id, value);
        }
    }
}
