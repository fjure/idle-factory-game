using System;
using System.Collections.Generic;
using System.Linq;
using IdleFactory.Domain;
using NUnit.Framework;

namespace IdleFactory.Tests.EditMode.Fixtures
{
    public sealed class FoundationFixtures
    {
        public const string InputResourceId = "dev.input";
        public const string OutputResourceId = "dev.output";
        public const string BasicRecipeId = "dev.recipe.basic";
        public const string D001BuildingId = "dev.machine.basic";
        public const string AlternateBuildingId = "test.machine.alternate-footprint";
        public const string MultiCycleBuildingId = "test.machine.multi-cycle";
        public const string W001BuildingId = "test.machine.w001";

        public const long RecipeDurationTicks = 50000000L;
        public const int W001MachineCount = 1000;
        public const int W001ActiveUnblockedCount = 500;
        public const int W001ActiveBlockedCount = 250;
        public const int W001InactiveCount = 250;
        public const long W001InputQuantity = 9216L;
        public const long W001OutputCapacity = 4608L;

        private const string InputLabel = "Input";
        private const string OutputLabel = "Output";
        private const string OutputPresentationKey = "dev.output.sprite";
        private const string D001PresentationKey = "dev.machine.sprite";

        private static readonly ResourceId InputId = new ResourceId(InputResourceId);
        private static readonly ResourceId OutputId = new ResourceId(OutputResourceId);
        private static readonly RecipeId RecipeId = new RecipeId(BasicRecipeId);

        public static FactoryState CreateD001()
        {
            return CreateSingleBuildingFixture(
                D001BuildingId,
                new Footprint(2, 2),
                D001PresentationKey,
                new FactoryBounds(new GridPosition(0, 0), 16, 16),
                new GridPosition(0, 0),
                InstanceId(1),
                inputCapacity: 4,
                inputQuantity: 4,
                outputCapacity: 1,
                outputQuantity: 0);
        }

        public static FactoryState CreateAlternateFootprint()
        {
            return CreateSingleBuildingFixture(
                AlternateBuildingId,
                new Footprint(3, 1),
                "test.fixture.alternate-footprint",
                new FactoryBounds(new GridPosition(2, 3), 9, 7),
                new GridPosition(4, 5),
                InstanceId(2),
                inputCapacity: 4,
                inputQuantity: 4,
                outputCapacity: 1,
                outputQuantity: 0);
        }

        public static FactoryState CreateMultiCycle()
        {
            return CreateSingleBuildingFixture(
                MultiCycleBuildingId,
                new Footprint(2, 2),
                "test.fixture.multi-cycle",
                new FactoryBounds(new GridPosition(0, 0), 16, 16),
                new GridPosition(0, 0),
                InstanceId(3),
                inputCapacity: 4,
                inputQuantity: 4,
                outputCapacity: 2,
                outputQuantity: 0);
        }

        public static FactoryState CreateNegativeCoordinate()
        {
            return CreateSingleBuildingFixture(
                D001BuildingId,
                new Footprint(2, 2),
                D001PresentationKey,
                new FactoryBounds(new GridPosition(-8, -6), 10, 9),
                new GridPosition(-7, -5),
                InstanceId(4),
                inputCapacity: 4,
                inputQuantity: 4,
                outputCapacity: 1,
                outputQuantity: 0);
        }

        public static FactoryState CreateW001()
        {
            var catalog = CreateCatalog(
                W001BuildingId,
                new Footprint(2, 2),
                "test.fixture.w001",
                W001InputQuantity,
                W001OutputCapacity);
            var definition = catalog.Resolve(new BuildingDefinitionId(W001BuildingId));
            var recipe = catalog.Resolve(RecipeId);
            var buildings = new BuildingInstanceState[W001MachineCount];

            for (var index = 0; index < buildings.Length; index++)
            {
                var isBlocked = index >= W001ActiveUnblockedCount &&
                                index < W001ActiveUnblockedCount + W001ActiveBlockedCount;
                var isActive = index < W001ActiveUnblockedCount + W001ActiveBlockedCount;
                var anchor = new GridPosition((index % 50) * 2, (index / 50) * 2);
                buildings[index] = CreateBuilding(
                    definition,
                    recipe,
                    InstanceId(index + 1001),
                    anchor,
                    W001InputQuantity,
                    isBlocked ? W001OutputCapacity : 0,
                    isActive);
            }

            return new FactoryState(
                new FactoryBounds(new GridPosition(0, 0), 100, 40),
                catalog,
                buildings);
        }

        private static FactoryState CreateSingleBuildingFixture(
            string buildingId,
            Footprint footprint,
            string presentationKey,
            FactoryBounds bounds,
            GridPosition anchor,
            BuildingInstanceId instanceId,
            long inputCapacity,
            long inputQuantity,
            long outputCapacity,
            long outputQuantity)
        {
            var catalog = CreateCatalog(buildingId, footprint, presentationKey, inputCapacity, outputCapacity);
            var building = CreateBuilding(
                catalog.Resolve(new BuildingDefinitionId(buildingId)),
                catalog.Resolve(RecipeId),
                instanceId,
                anchor,
                inputQuantity,
                outputQuantity,
                true);
            return new FactoryState(bounds, catalog, new[] { building });
        }

        private static DefinitionCatalog CreateCatalog(
            string buildingId,
            Footprint footprint,
            string presentationKey,
            long inputCapacity,
            long outputCapacity)
        {
            var recipe = new RecipeDefinition(
                RecipeId,
                Amounts(InputId, 2),
                Amounts(OutputId, 1),
                RecipeDurationTicks,
                true);
            var building = new BuildingDefinition(
                new BuildingDefinitionId(buildingId),
                footprint,
                new[] { 0 },
                RecipeId,
                Buffer(BufferRole.Input, InputId, inputCapacity),
                Buffer(BufferRole.Output, OutputId, outputCapacity),
                presentationKey,
                true);

            return new DefinitionCatalog(
                new[]
                {
                    new ResourceDefinition(InputId, InputLabel, null, true),
                    new ResourceDefinition(OutputId, OutputLabel, OutputPresentationKey, true),
                },
                new[] { recipe },
                new[] { building },
                new[]
                {
                    new OfflinePolicyDefinition(
                        new OfflinePolicyRevision(DefinitionCatalog.SupportedOfflinePolicyRevision),
                        DefinitionCatalog.SupportedEfficiencyNumerator,
                        DefinitionCatalog.SupportedEfficiencyDenominator,
                        DefinitionCatalog.SupportedMaxCreditedTicks),
                });
        }

        private static BuildingInstanceState CreateBuilding(
            BuildingDefinition definition,
            RecipeDefinition recipe,
            BuildingInstanceId id,
            GridPosition anchor,
            long inputQuantity,
            long outputQuantity,
            bool isActive)
        {
            return new BuildingInstanceState(
                id,
                definition,
                anchor,
                0,
                isActive,
                BufferState(definition.InputBuffer, InputId, inputQuantity),
                BufferState(definition.OutputBuffer, OutputId, outputQuantity),
                new ProductionState(recipe, 0));
        }

        private static BuildingInstanceId InstanceId(int value)
        {
            return new BuildingInstanceId(new Guid(value, 0, 0, new byte[8]));
        }

        private static BufferDefinition Buffer(BufferRole role, ResourceId resourceId, long capacity)
        {
            return new BufferDefinition(role, Quantities(resourceId, capacity));
        }

        private static ResourceBufferState BufferState(
            BufferDefinition definition,
            ResourceId resourceId,
            long quantity)
        {
            return new ResourceBufferState(definition, Quantities(resourceId, quantity));
        }

        private static ResourceAmountDefinition[] Amounts(ResourceId resourceId, long amount)
        {
            return new[] { new ResourceAmountDefinition(resourceId, amount) };
        }

        private static KeyValuePair<ResourceId, long>[] Quantities(ResourceId resourceId, long quantity)
        {
            return new[] { new KeyValuePair<ResourceId, long>(resourceId, quantity) };
        }

        [Test]
        public void D001HasExactCanonicalDefinitionsAndInitializedState()
        {
            var fixture = FoundationFixtures.CreateD001();
            var input = fixture.Catalog.Resolve(InputId);
            var output = fixture.Catalog.Resolve(OutputId);
            var recipe = fixture.Catalog.Resolve(RecipeId);
            var definition = fixture.Catalog.Resolve(new BuildingDefinitionId(FoundationFixtures.D001BuildingId));
            var building = fixture.Buildings.Values.Single();
            var policy = fixture.Catalog.Resolve(new OfflinePolicyRevision(1));

            Assert.That(fixture.Catalog.Resources.Count, Is.EqualTo(2));
            Assert.That(fixture.Catalog.Recipes.Count, Is.EqualTo(1));
            Assert.That(fixture.Catalog.Buildings.Count, Is.EqualTo(1));
            Assert.That(fixture.Catalog.OfflinePolicies.Count, Is.EqualTo(1));
            Assert.That(input.DevelopmentLabel, Is.EqualTo("Input"));
            Assert.That(input.PresentationKey, Is.Null);
            Assert.That(input.IsDevelopmentOnly, Is.True);
            Assert.That(output.DevelopmentLabel, Is.EqualTo("Output"));
            Assert.That(output.PresentationKey, Is.EqualTo("dev.output.sprite"));
            Assert.That(output.IsDevelopmentOnly, Is.True);
            Assert.That(recipe.IsDevelopmentOnly, Is.True);
            Assert.That(recipe.Inputs.Single().ResourceId, Is.EqualTo(InputId));
            Assert.That(recipe.Inputs.Single().Amount, Is.EqualTo(2));
            Assert.That(recipe.Outputs.Single().ResourceId, Is.EqualTo(OutputId));
            Assert.That(recipe.Outputs.Single().Amount, Is.EqualTo(1));
            Assert.That(recipe.DurationTicks, Is.EqualTo(FoundationFixtures.RecipeDurationTicks));
            Assert.That(definition.Footprint, Is.EqualTo(new Footprint(2, 2)));
            Assert.That(definition.AllowedOrientations, Is.EqualTo(new[] { 0 }));
            Assert.That(definition.PresentationKey, Is.EqualTo("dev.machine.sprite"));
            Assert.That(definition.IsDevelopmentOnly, Is.True);
            Assert.That(policy.Revision.Value, Is.EqualTo(1));
            Assert.That(policy.EfficiencyNumerator, Is.EqualTo(4));
            Assert.That(policy.EfficiencyDenominator, Is.EqualTo(5));
            Assert.That(policy.MaxCreditedTicks, Is.EqualTo(288000000000L));
            Assert.That(fixture.Bounds, Is.EqualTo(new FactoryBounds(new GridPosition(0, 0), 16, 16)));
            Assert.That(building.Anchor, Is.EqualTo(new GridPosition(0, 0)));
            Assert.That(building.OrientationQuarterTurns, Is.Zero);
            Assert.That(building.IsActive, Is.True);
            Assert.That(building.InputBuffer.Definition.Capacities[InputId], Is.EqualTo(4));
            Assert.That(building.InputBuffer.Quantity(InputId), Is.EqualTo(4));
            Assert.That(building.OutputBuffer.Definition.Capacities[OutputId], Is.EqualTo(1));
            Assert.That(building.OutputBuffer.Quantity(OutputId), Is.Zero);
            Assert.That(building.Production.ProgressTicks, Is.Zero);
            Assert.That(fixture.Occupancy.Count, Is.EqualTo(4));
        }

        [Test]
        public void AlternateFixtureUsesDeclaredNonCanonicalBoundsAndFootprint()
        {
            var fixture = FoundationFixtures.CreateAlternateFootprint();
            var definition = fixture.Catalog.Resolve(
                new BuildingDefinitionId(FoundationFixtures.AlternateBuildingId));
            var building = fixture.Buildings.Values.Single();

            Assert.That(definition.Id.Value, Is.EqualTo(FoundationFixtures.AlternateBuildingId));
            Assert.That(definition.Footprint, Is.EqualTo(new Footprint(3, 1)));
            Assert.That(definition.Footprint, Is.Not.EqualTo(new Footprint(2, 2)));
            Assert.That(definition.IsDevelopmentOnly, Is.True);
            Assert.That(fixture.Bounds, Is.EqualTo(new FactoryBounds(new GridPosition(2, 3), 9, 7)));
            Assert.That(fixture.Bounds.Width, Is.Not.EqualTo(16));
            Assert.That(fixture.Bounds.Height, Is.Not.EqualTo(16));
            Assert.That(building.Anchor, Is.EqualTo(new GridPosition(4, 5)));
            Assert.That(fixture.Occupancy.Keys, Is.EquivalentTo(new[]
            {
                new GridPosition(4, 5),
                new GridPosition(5, 5),
                new GridPosition(6, 5),
            }));
        }

        [Test]
        public void MultiCycleUsesDistinctDefinitionAndExactCapacityState()
        {
            var fixture = FoundationFixtures.CreateMultiCycle();
            var definition = fixture.Catalog.Resolve(
                new BuildingDefinitionId(FoundationFixtures.MultiCycleBuildingId));
            var building = fixture.Buildings.Values.Single();

            Assert.That(definition.Id.Value, Is.EqualTo(FoundationFixtures.MultiCycleBuildingId));
            Assert.That(definition.Id.Value, Is.Not.EqualTo(FoundationFixtures.D001BuildingId));
            Assert.That(definition.RecipeId, Is.EqualTo(RecipeId));
            Assert.That(definition.OutputBuffer.Capacities[OutputId], Is.EqualTo(2));
            Assert.That(building.InputBuffer.Quantity(InputId), Is.EqualTo(4));
            Assert.That(building.OutputBuffer.Quantity(OutputId), Is.Zero);
            Assert.That(building.IsActive, Is.True);
            Assert.That(building.Production.ProgressTicks, Is.Zero);
        }

        [Test]
        public void NegativeCoordinateFixturePreservesSignedPositionsAndOccupancy()
        {
            var fixture = FoundationFixtures.CreateNegativeCoordinate();
            var building = fixture.Buildings.Values.Single();

            Assert.That(fixture.Bounds.Origin, Is.EqualTo(new GridPosition(-8, -6)));
            Assert.That(building.Anchor, Is.EqualTo(new GridPosition(-7, -5)));
            Assert.That(fixture.Occupancy.Keys, Is.EquivalentTo(new[]
            {
                new GridPosition(-7, -5),
                new GridPosition(-6, -5),
                new GridPosition(-7, -4),
                new GridPosition(-6, -4),
            }));
        }

        [Test]
        public void W001HasExactDeterministicWorkloadShapeAndClassifications()
        {
            var fixture = FoundationFixtures.CreateW001();
            var buildings = fixture.Buildings.Values.ToArray();
            var definition = fixture.Catalog.Resolve(new BuildingDefinitionId(FoundationFixtures.W001BuildingId));
            var recipe = fixture.Catalog.Resolve(RecipeId);

            Assert.That(buildings, Has.Length.EqualTo(FoundationFixtures.W001MachineCount));
            Assert.That(fixture.Occupancy.Count, Is.EqualTo(4000));
            Assert.That(fixture.Bounds, Is.EqualTo(new FactoryBounds(new GridPosition(0, 0), 100, 40)));
            Assert.That(definition.Footprint, Is.EqualTo(new Footprint(2, 2)));
            Assert.That(definition.InputBuffer.Capacities[InputId], Is.EqualTo(FoundationFixtures.W001InputQuantity));
            Assert.That(definition.OutputBuffer.Capacities[OutputId], Is.EqualTo(FoundationFixtures.W001OutputCapacity));
            Assert.That(recipe.Inputs.Single().Amount, Is.EqualTo(2));
            Assert.That(recipe.Outputs.Single().Amount, Is.EqualTo(1));
            Assert.That(recipe.DurationTicks, Is.EqualTo(FoundationFixtures.RecipeDurationTicks));
            Assert.That(buildings.Select(building => building.Id).Distinct().Count(), Is.EqualTo(buildings.Length));
            Assert.That(
                buildings.Take(FoundationFixtures.W001ActiveUnblockedCount).Count(IsUnblockedActive),
                Is.EqualTo(FoundationFixtures.W001ActiveUnblockedCount));
            Assert.That(
                buildings.Skip(FoundationFixtures.W001ActiveUnblockedCount)
                    .Take(FoundationFixtures.W001ActiveBlockedCount)
                    .Count(IsOutputBlockedActive),
                Is.EqualTo(FoundationFixtures.W001ActiveBlockedCount));
            Assert.That(
                buildings.Skip(FoundationFixtures.W001ActiveUnblockedCount + FoundationFixtures.W001ActiveBlockedCount)
                    .Count(IsInactiveOnly),
                Is.EqualTo(FoundationFixtures.W001InactiveCount));
            Assert.That(buildings, Has.All.Matches<BuildingInstanceState>(building =>
                building.InputBuffer.Quantity(InputId) == FoundationFixtures.W001InputQuantity &&
                building.Production.ProgressTicks == 0));
            Assert.That(buildings[0].Anchor, Is.EqualTo(new GridPosition(0, 0)));
            Assert.That(buildings[49].Anchor, Is.EqualTo(new GridPosition(98, 0)));
            Assert.That(buildings[50].Anchor, Is.EqualTo(new GridPosition(0, 2)));
            Assert.That(buildings[999].Anchor, Is.EqualTo(new GridPosition(98, 38)));
        }

        [Test]
        public void CallsAreEquivalentButOwnIndependentMutableStateGraphs()
        {
            var first = FoundationFixtures.CreateW001();
            var second = FoundationFixtures.CreateW001();
            var firstBuildings = first.Buildings.Values.ToArray();
            var secondBuildings = second.Buildings.Values.ToArray();

            Assert.That(first.Catalog, Is.Not.SameAs(second.Catalog));
            Assert.That(first, Is.Not.SameAs(second));
            Assert.That(firstBuildings.Select(building => building.Id), Is.EqualTo(secondBuildings.Select(building => building.Id)));
            Assert.That(firstBuildings.Select(building => building.Anchor), Is.EqualTo(secondBuildings.Select(building => building.Anchor)));
            Assert.That(firstBuildings[0], Is.Not.SameAs(secondBuildings[0]));
            Assert.That(firstBuildings[0].InputBuffer, Is.Not.SameAs(secondBuildings[0].InputBuffer));
            Assert.That(firstBuildings[0].OutputBuffer, Is.Not.SameAs(secondBuildings[0].OutputBuffer));
            Assert.That(firstBuildings[0].Production, Is.Not.SameAs(secondBuildings[0].Production));
            Assert.That(firstBuildings[0].InputBuffer, Is.Not.SameAs(firstBuildings[1].InputBuffer));
            Assert.That(firstBuildings[0].OutputBuffer, Is.Not.SameAs(firstBuildings[1].OutputBuffer));
            Assert.That(firstBuildings[0].Production, Is.Not.SameAs(firstBuildings[1].Production));
            Assert.That(
                () => ((IDictionary<BuildingDefinitionId, BuildingDefinition>)first.Catalog.Buildings).Clear(),
                Throws.TypeOf<NotSupportedException>());
        }

        [Test]
        public void FixturePublicApiExposesOnlyPlainCoreAndSystemTypes()
        {
            var exposedTypes = typeof(FoundationFixtures)
                .GetMethods(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)
                .SelectMany(method => method.GetParameters().Select(parameter => parameter.ParameterType)
                    .Concat(new[] { method.ReturnType }));

            Assert.That(exposedTypes.Select(type => type.Namespace), Has.None.StartsWith("UnityEngine"));
        }

        private static bool IsUnblockedActive(BuildingInstanceState building)
        {
            return building.IsActive &&
                   building.OutputBuffer.Quantity(OutputId) == 0 &&
                   building.GetProductionBlockers().Count == 0;
        }

        private static bool IsOutputBlockedActive(BuildingInstanceState building)
        {
            return building.IsActive &&
                   building.OutputBuffer.Quantity(OutputId) == FoundationFixtures.W001OutputCapacity &&
                   building.GetProductionBlockers().Count == 1 &&
                   building.GetProductionBlockers()[0].Kind == ProductionBlockerKind.InsufficientOutputCapacity;
        }

        private static bool IsInactiveOnly(BuildingInstanceState building)
        {
            return !building.IsActive &&
                   building.OutputBuffer.Quantity(OutputId) == 0 &&
                   building.GetProductionBlockers().Count == 1 &&
                   building.GetProductionBlockers()[0].Kind == ProductionBlockerKind.Inactive;
        }
    }
}
