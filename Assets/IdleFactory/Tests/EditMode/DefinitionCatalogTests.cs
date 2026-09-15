using System;
using System.Collections.Generic;
using System.Linq;
using IdleFactory.Domain;
using NUnit.Framework;

namespace IdleFactory.Tests.EditMode
{
    public sealed class DefinitionCatalogTests
    {
        private static readonly ResourceId InputId = new ResourceId("dev.input");
        private static readonly ResourceId OutputId = new ResourceId("dev.output");
        private static readonly RecipeId RecipeId = new RecipeId("dev.recipe.basic");
        private static readonly BuildingDefinitionId BuildingId = new BuildingDefinitionId("dev.machine.basic");

        [Test]
        public void ValidD001GraphResolvesTypedImmutableDefinitions()
        {
            var catalog = CreateCatalog();

            Assert.That(catalog.Resolve(InputId).DevelopmentLabel, Is.EqualTo("Input"));
            Assert.That(catalog.Resolve(InputId).PresentationKey, Is.Null);
            Assert.That(catalog.Resolve(RecipeId).DurationTicks, Is.EqualTo(50000000L));
            Assert.That(catalog.Resolve(BuildingId).AllowedOrientations, Is.EqualTo(new[] { 0 }));
            Assert.That(catalog.Resolve(BuildingId).Footprint, Is.EqualTo(new Footprint(2, 2)));
            Assert.That(catalog.Resolve(new OfflinePolicyRevision(1)).EfficiencyNumerator, Is.EqualTo(4));
            Assert.That(catalog.Resolve(new OfflinePolicyRevision(1)).EfficiencyDenominator, Is.EqualTo(5));
            Assert.That(catalog.Resolve(new OfflinePolicyRevision(1)).MaxCreditedTicks, Is.EqualTo(288000000000L));
        }

        [Test]
        public void GenericDefinitionsDoNotDependOnD001Names()
        {
            var resource = new ResourceDefinition(new ResourceId("alpha.ore"), "Ore", "ore.sprite", false);
            var recipe = new RecipeDefinition(
                new RecipeId("alpha.recipe"),
                new[] { new ResourceAmountDefinition(resource.Id, 1) },
                new[] { new ResourceAmountDefinition(resource.Id, 1) },
                1,
                false);
            var input = Buffer(BufferRole.Input, resource.Id, 0);
            var output = Buffer(BufferRole.Output, resource.Id, long.MaxValue);
            var building = new BuildingDefinition(
                new BuildingDefinitionId("alpha.machine"),
                new Footprint(1, 2),
                new[] { 0, 1, 2, 3 },
                recipe.Id,
                input,
                output,
                "alpha.machine.sprite",
                false);

            var catalog = new DefinitionCatalog(
                new[] { resource },
                new[] { recipe },
                new[] { building },
                new[] { SupportedPolicy() });

            Assert.That(catalog.Resolve(building.Id), Is.SameAs(building));
            Assert.That(catalog.Resolve(recipe.Id), Is.SameAs(recipe));
        }

        [Test]
        public void DefinitionsDefensivelyCopyCollectionsAndExposeReadOnlyViews()
        {
            var inputs = new List<ResourceAmountDefinition> { new ResourceAmountDefinition(InputId, 2) };
            var outputs = new List<ResourceAmountDefinition> { new ResourceAmountDefinition(OutputId, 1) };
            var capacities = new List<KeyValuePair<ResourceId, long>> { Pair(InputId, 4) };
            var orientations = new List<int> { 0 };
            var resources = CreateResources().ToList();
            var recipes = new List<RecipeDefinition>();
            var buildings = new List<BuildingDefinition>();
            var policies = new List<OfflinePolicyDefinition> { SupportedPolicy() };

            var recipe = new RecipeDefinition(RecipeId, inputs, outputs, 5, true);
            var buffer = new BufferDefinition(BufferRole.Input, capacities);
            var building = new BuildingDefinition(
                BuildingId,
                new Footprint(2, 2),
                orientations,
                null,
                buffer,
                null,
                "machine",
                true);
            recipes.Add(recipe);
            buildings.Add(building);
            var catalog = new DefinitionCatalog(resources, recipes, buildings, policies);

            inputs.Clear();
            outputs.Clear();
            capacities.Clear();
            orientations.Clear();
            resources.Clear();
            recipes.Clear();
            buildings.Clear();
            policies.Clear();

            Assert.That(recipe.Inputs.Count, Is.EqualTo(1));
            Assert.That(recipe.Outputs.Count, Is.EqualTo(1));
            Assert.That(buffer.Capacities.Count, Is.EqualTo(1));
            Assert.That(building.AllowedOrientations, Is.EqualTo(new[] { 0 }));
            Assert.That(catalog.Resources.Count, Is.EqualTo(2));
            Assert.That(catalog.Recipes.Count, Is.EqualTo(1));
            Assert.That(catalog.Buildings.Count, Is.EqualTo(1));
            Assert.That(catalog.OfflinePolicies.Count, Is.EqualTo(1));

            Assert.That(
                () => ((IList<ResourceAmountDefinition>)recipe.Inputs).Add(new ResourceAmountDefinition(InputId, 1)),
                Throws.TypeOf<NotSupportedException>());
            Assert.That(
                () => ((IDictionary<ResourceId, long>)buffer.Capacities).Add(OutputId, 1),
                Throws.TypeOf<NotSupportedException>());
            Assert.That(
                () => ((IList<int>)building.AllowedOrientations).Add(1),
                Throws.TypeOf<NotSupportedException>());
            Assert.That(
                () => ((IDictionary<ResourceId, ResourceDefinition>)catalog.Resources).Clear(),
                Throws.TypeOf<NotSupportedException>());
        }

        [Test]
        public void RequiredStringsAreTrimmedAndOptionalResourcePresentationKeyMayBeAbsent()
        {
            var resource = new ResourceDefinition(InputId, "  Input  ", null, true);
            var building = StorageBuilding("  machine.sprite  ");

            Assert.That(resource.DevelopmentLabel, Is.EqualTo("Input"));
            Assert.That(resource.PresentationKey, Is.Null);
            Assert.That(building.PresentationKey, Is.EqualTo("machine.sprite"));
            Assert.That(() => new ResourceDefinition(InputId, " ", null, true), Throws.InstanceOf<ArgumentException>());
            Assert.That(() => new ResourceDefinition(InputId, "Input", " ", true), Throws.InstanceOf<ArgumentException>());
            Assert.That(() => StorageBuilding(" "), Throws.InstanceOf<ArgumentException>());
        }

        [Test]
        public void EveryDefinitionRejectsDefaultIdentifierValues()
        {
            Assert.That(
                () => new ResourceDefinition(default(ResourceId), "Input", null, true),
                Throws.TypeOf<InvalidOperationException>());
            Assert.That(
                () => new ResourceAmountDefinition(default(ResourceId), 1),
                Throws.TypeOf<InvalidOperationException>());
            Assert.That(
                () => new RecipeDefinition(default(RecipeId), Amounts(InputId, 1), Amounts(OutputId, 1), 1, true),
                Throws.TypeOf<InvalidOperationException>());
            Assert.That(
                () => new BufferDefinition(BufferRole.Input, new[] { Pair(default(ResourceId), 1) }),
                Throws.TypeOf<InvalidOperationException>());
            Assert.That(
                () => new BuildingDefinition(default(BuildingDefinitionId), new Footprint(1, 1), new[] { 0 }, null, null, null, "building", true),
                Throws.TypeOf<InvalidOperationException>());
            Assert.That(
                () => new BuildingDefinition(BuildingId, new Footprint(1, 1), new[] { 0 }, default(RecipeId), null, null, "building", true),
                Throws.TypeOf<InvalidOperationException>());
            Assert.That(
                () => new OfflinePolicyDefinition(default(OfflinePolicyRevision), 4, 5, 1),
                Throws.TypeOf<InvalidOperationException>());
        }

        [Test]
        public void PositiveAmountsAndDurationsPreserveLongMaxValueWithoutArithmetic()
        {
            var amount = new ResourceAmountDefinition(InputId, long.MaxValue);
            var recipe = new RecipeDefinition(
                RecipeId,
                new[] { amount },
                Amounts(OutputId, long.MaxValue),
                long.MaxValue,
                true);

            Assert.That(amount.Amount, Is.EqualTo(long.MaxValue));
            Assert.That(recipe.DurationTicks, Is.EqualTo(long.MaxValue));
            Assert.That(() => new ResourceAmountDefinition(InputId, 0), Throws.TypeOf<ArgumentOutOfRangeException>());
            Assert.That(() => new ResourceAmountDefinition(InputId, -1), Throws.TypeOf<ArgumentOutOfRangeException>());
            Assert.That(
                () => new RecipeDefinition(RecipeId, Amounts(InputId, 1), Amounts(OutputId, 1), 0, true),
                Throws.TypeOf<ArgumentOutOfRangeException>());
        }

        [Test]
        public void RecipesRequireNonemptyUniqueResourceSides()
        {
            Assert.That(
                () => new RecipeDefinition(RecipeId, null, Amounts(OutputId, 1), 1, true),
                Throws.TypeOf<ArgumentNullException>());
            Assert.That(
                () => new RecipeDefinition(RecipeId, Amounts(InputId, 1), null, 1, true),
                Throws.TypeOf<ArgumentNullException>());
            Assert.That(
                () => new RecipeDefinition(RecipeId, Array.Empty<ResourceAmountDefinition>(), Amounts(OutputId, 1), 1, true),
                Throws.InstanceOf<ArgumentException>());
            Assert.That(
                () => new RecipeDefinition(RecipeId, Amounts(InputId, 1), Array.Empty<ResourceAmountDefinition>(), 1, true),
                Throws.InstanceOf<ArgumentException>());
            Assert.That(
                () => new RecipeDefinition(RecipeId, new ResourceAmountDefinition[] { null }, Amounts(OutputId, 1), 1, true),
                Throws.InstanceOf<ArgumentException>());
            Assert.That(
                () => new RecipeDefinition(
                    RecipeId,
                    new[]
                    {
                        new ResourceAmountDefinition(new ResourceId(" dev.input "), 1),
                        new ResourceAmountDefinition(new ResourceId("dev.input"), 2),
                    },
                    Amounts(OutputId, 1),
                    1,
                    true),
                Throws.InstanceOf<ArgumentException>());
            Assert.That(
                () => new RecipeDefinition(
                    RecipeId,
                    Amounts(InputId, 1),
                    new[]
                    {
                        new ResourceAmountDefinition(new ResourceId(" dev.output "), 1),
                        new ResourceAmountDefinition(OutputId, 2),
                    },
                    1,
                    true),
                Throws.InstanceOf<ArgumentException>());
        }

        [Test]
        public void BuffersRequireValidRoleNonemptyUniqueNonnegativeCapacities()
        {
            var zero = Buffer(BufferRole.Input, InputId, 0);
            var maximum = Buffer(BufferRole.Output, OutputId, long.MaxValue);

            Assert.That(zero.Capacities[InputId], Is.Zero);
            Assert.That(maximum.Capacities[OutputId], Is.EqualTo(long.MaxValue));
            Assert.That(
                () => new BufferDefinition(BufferRole.Input, null),
                Throws.TypeOf<ArgumentNullException>());
            Assert.That(
                () => new BufferDefinition((BufferRole)2, new[] { Pair(InputId, 1) }),
                Throws.TypeOf<ArgumentOutOfRangeException>());
            Assert.That(
                () => new BufferDefinition(BufferRole.Input, Array.Empty<KeyValuePair<ResourceId, long>>()),
                Throws.InstanceOf<ArgumentException>());
            Assert.That(
                () => new BufferDefinition(BufferRole.Input, new[] { Pair(InputId, -1) }),
                Throws.TypeOf<ArgumentOutOfRangeException>());
            Assert.That(
                () => new BufferDefinition(
                    BufferRole.Input,
                    new[] { Pair(new ResourceId(" dev.input "), 1), Pair(InputId, 2) }),
                Throws.InstanceOf<ArgumentException>());
        }

        [Test]
        public void BuildingsRequirePositiveFootprintValidUniqueOrientationsAndCorrectRoles()
        {
            Assert.That(
                () => new BuildingDefinition(BuildingId, default(Footprint), new[] { 0 }, null, null, null, "building", true),
                Throws.TypeOf<InvalidOperationException>());
            Assert.That(
                () => new BuildingDefinition(BuildingId, new Footprint(1, 1), Array.Empty<int>(), null, null, null, "building", true),
                Throws.InstanceOf<ArgumentException>());
            Assert.That(
                () => new BuildingDefinition(BuildingId, new Footprint(1, 1), null, null, null, null, "building", true),
                Throws.TypeOf<ArgumentNullException>());
            Assert.That(
                () => new BuildingDefinition(BuildingId, new Footprint(1, 1), new[] { 0, 0 }, null, null, null, "building", true),
                Throws.InstanceOf<ArgumentException>());
            Assert.That(
                () => new BuildingDefinition(BuildingId, new Footprint(1, 1), new[] { -1 }, null, null, null, "building", true),
                Throws.TypeOf<ArgumentOutOfRangeException>());
            Assert.That(
                () => new BuildingDefinition(BuildingId, new Footprint(1, 1), new[] { 4 }, null, null, null, "building", true),
                Throws.TypeOf<ArgumentOutOfRangeException>());
            Assert.That(
                () => new BuildingDefinition(BuildingId, new Footprint(1, 1), new[] { 0 }, null, Buffer(BufferRole.Output, InputId, 1), null, "building", true),
                Throws.InstanceOf<ArgumentException>());
            Assert.That(
                () => new BuildingDefinition(BuildingId, new Footprint(1, 1), new[] { 0 }, null, null, Buffer(BufferRole.Input, OutputId, 1), "building", true),
                Throws.InstanceOf<ArgumentException>());

            var ordered = new BuildingDefinition(
                BuildingId,
                new Footprint(1, 1),
                new[] { 3, 1, 2, 0 },
                null,
                null,
                null,
                "building",
                true);
            Assert.That(ordered.AllowedOrientations, Is.EqualTo(new[] { 0, 1, 2, 3 }));
        }

        [Test]
        public void OfflinePolicyValidatesMathBeforeCatalogCompatibility()
        {
            Assert.That(
                () => new OfflinePolicyDefinition(new OfflinePolicyRevision(1), 0, 5, 1),
                Throws.TypeOf<ArgumentOutOfRangeException>());
            Assert.That(
                () => new OfflinePolicyDefinition(new OfflinePolicyRevision(1), 4, 0, 1),
                Throws.TypeOf<ArgumentOutOfRangeException>());
            Assert.That(
                () => new OfflinePolicyDefinition(new OfflinePolicyRevision(1), 5, 5, 1),
                Throws.InstanceOf<ArgumentException>());
            Assert.That(
                () => new OfflinePolicyDefinition(new OfflinePolicyRevision(1), 6, 5, 1),
                Throws.InstanceOf<ArgumentException>());
            Assert.That(
                () => new OfflinePolicyDefinition(new OfflinePolicyRevision(1), 4, 5, 0),
                Throws.TypeOf<ArgumentOutOfRangeException>());
        }

        [Test]
        public void CatalogRejectsMissingFutureAdditionalOrIncorrectPolicy()
        {
            Assert.That(() => CreateCatalog(policies: Array.Empty<OfflinePolicyDefinition>()), Throws.InstanceOf<ArgumentException>());
            Assert.That(
                () => CreateCatalog(policies: new[] { new OfflinePolicyDefinition(new OfflinePolicyRevision(2), 4, 5, 288000000000L) }),
                Throws.InstanceOf<ArgumentException>());
            Assert.That(
                () => CreateCatalog(policies: new[] { SupportedPolicy(), new OfflinePolicyDefinition(new OfflinePolicyRevision(2), 4, 5, 288000000000L) }),
                Throws.InstanceOf<ArgumentException>());
            Assert.That(
                () => CreateCatalog(policies: new[] { new OfflinePolicyDefinition(new OfflinePolicyRevision(1), 3, 5, 288000000000L) }),
                Throws.InstanceOf<ArgumentException>());
            Assert.That(
                () => CreateCatalog(policies: new[] { new OfflinePolicyDefinition(new OfflinePolicyRevision(1), 4, 6, 288000000000L) }),
                Throws.InstanceOf<ArgumentException>());
            Assert.That(
                () => CreateCatalog(policies: new[] { new OfflinePolicyDefinition(new OfflinePolicyRevision(1), 4, 5, 1) }),
                Throws.InstanceOf<ArgumentException>());
        }

        [Test]
        public void CatalogRejectsNullCollectionsAndEntries()
        {
            Assert.That(
                () => new DefinitionCatalog(null, Array.Empty<RecipeDefinition>(), Array.Empty<BuildingDefinition>(), new[] { SupportedPolicy() }),
                Throws.TypeOf<ArgumentNullException>());
            Assert.That(
                () => new DefinitionCatalog(CreateResources(), null, Array.Empty<BuildingDefinition>(), new[] { SupportedPolicy() }),
                Throws.TypeOf<ArgumentNullException>());
            Assert.That(
                () => new DefinitionCatalog(CreateResources(), Array.Empty<RecipeDefinition>(), null, new[] { SupportedPolicy() }),
                Throws.TypeOf<ArgumentNullException>());
            Assert.That(
                () => new DefinitionCatalog(CreateResources(), Array.Empty<RecipeDefinition>(), Array.Empty<BuildingDefinition>(), null),
                Throws.TypeOf<ArgumentNullException>());
            Assert.That(
                () => new DefinitionCatalog(new ResourceDefinition[] { null }, Array.Empty<RecipeDefinition>(), Array.Empty<BuildingDefinition>(), new[] { SupportedPolicy() }),
                Throws.InstanceOf<ArgumentException>());
            Assert.That(
                () => new DefinitionCatalog(CreateResources(), new RecipeDefinition[] { null }, Array.Empty<BuildingDefinition>(), new[] { SupportedPolicy() }),
                Throws.InstanceOf<ArgumentException>());
            Assert.That(
                () => new DefinitionCatalog(CreateResources(), Array.Empty<RecipeDefinition>(), new BuildingDefinition[] { null }, new[] { SupportedPolicy() }),
                Throws.InstanceOf<ArgumentException>());
            Assert.That(
                () => new DefinitionCatalog(CreateResources(), Array.Empty<RecipeDefinition>(), Array.Empty<BuildingDefinition>(), new OfflinePolicyDefinition[] { null }),
                Throws.InstanceOf<ArgumentException>());
        }

        [Test]
        public void CatalogRejectsDuplicateIdsInEveryCategory()
        {
            var resources = CreateResources();
            var recipe = CreateRecipe();
            var building = CreateBuilding();
            var policy = SupportedPolicy();

            Assert.That(
                () => new DefinitionCatalog(resources.Concat(new[] { new ResourceDefinition(new ResourceId(" dev.input "), "Duplicate", null, true) }), new[] { recipe }, new[] { building }, new[] { policy }),
                Throws.InstanceOf<ArgumentException>());
            Assert.That(
                () => new DefinitionCatalog(resources, new[] { recipe, CreateRecipe() }, new[] { building }, new[] { policy }),
                Throws.InstanceOf<ArgumentException>());
            Assert.That(
                () => new DefinitionCatalog(resources, new[] { recipe }, new[] { building, CreateBuilding() }, new[] { policy }),
                Throws.InstanceOf<ArgumentException>());
            Assert.That(
                () => new DefinitionCatalog(resources, new[] { recipe }, new[] { building }, new[] { policy, SupportedPolicy() }),
                Throws.InstanceOf<ArgumentException>());
        }

        [Test]
        public void CatalogRejectsUnknownRecipeAndBufferResources()
        {
            var unknown = new ResourceId("unknown.resource");
            var unknownInputRecipe = new RecipeDefinition(RecipeId, Amounts(unknown, 1), Amounts(OutputId, 1), 1, true);
            var unknownOutputRecipe = new RecipeDefinition(RecipeId, Amounts(InputId, 1), Amounts(unknown, 1), 1, true);
            var unknownBufferBuilding = new BuildingDefinition(
                BuildingId,
                new Footprint(1, 1),
                new[] { 0 },
                null,
                Buffer(BufferRole.Input, unknown, 1),
                null,
                "building",
                true);

            Assert.That(() => CreateCatalog(recipes: new[] { unknownInputRecipe }, buildings: Array.Empty<BuildingDefinition>()), Throws.InstanceOf<ArgumentException>());
            Assert.That(() => CreateCatalog(recipes: new[] { unknownOutputRecipe }, buildings: Array.Empty<BuildingDefinition>()), Throws.InstanceOf<ArgumentException>());
            Assert.That(() => CreateCatalog(recipes: Array.Empty<RecipeDefinition>(), buildings: new[] { unknownBufferBuilding }), Throws.InstanceOf<ArgumentException>());
        }

        [Test]
        public void CatalogRejectsUnknownRecipeMissingBuffersAndMissingRecipeKeys()
        {
            var unknownRecipeBuilding = new BuildingDefinition(
                BuildingId,
                new Footprint(1, 1),
                new[] { 0 },
                new RecipeId("unknown.recipe"),
                Buffer(BufferRole.Input, InputId, 1),
                Buffer(BufferRole.Output, OutputId, 1),
                "building",
                true);
            var missingInput = new BuildingDefinition(
                BuildingId,
                new Footprint(2, 2),
                new[] { 0 },
                RecipeId,
                null,
                Buffer(BufferRole.Output, OutputId, 1),
                "building",
                true);
            var missingOutput = new BuildingDefinition(
                BuildingId,
                new Footprint(2, 2),
                new[] { 0 },
                RecipeId,
                Buffer(BufferRole.Input, InputId, 4),
                null,
                "building",
                true);
            var wrongInput = CreateBuilding(input: Buffer(BufferRole.Input, OutputId, 1));
            var wrongOutput = CreateBuilding(output: Buffer(BufferRole.Output, InputId, 1));

            Assert.That(() => CreateCatalog(buildings: new[] { unknownRecipeBuilding }), Throws.InstanceOf<ArgumentException>());
            Assert.That(() => CreateCatalog(buildings: new[] { missingInput }), Throws.InstanceOf<ArgumentException>());
            Assert.That(() => CreateCatalog(buildings: new[] { missingOutput }), Throws.InstanceOf<ArgumentException>());
            Assert.That(() => CreateCatalog(buildings: new[] { wrongInput }), Throws.InstanceOf<ArgumentException>());
            Assert.That(() => CreateCatalog(buildings: new[] { wrongOutput }), Throws.InstanceOf<ArgumentException>());
        }

        [Test]
        public void BufferCoverageDoesNotInventCapacityComparedWithRecipeAmount()
        {
            var recipe = new RecipeDefinition(RecipeId, Amounts(InputId, long.MaxValue), Amounts(OutputId, long.MaxValue), 1, true);
            var building = CreateBuilding(input: Buffer(BufferRole.Input, InputId, 0), output: Buffer(BufferRole.Output, OutputId, 0));

            Assert.That(() => CreateCatalog(recipes: new[] { recipe }, buildings: new[] { building }), Throws.Nothing);
        }

        [Test]
        public void ResolveRejectsUnknownAndDefaultTypedIds()
        {
            var catalog = CreateCatalog();

            Assert.That(() => catalog.Resolve(new ResourceId("unknown")), Throws.TypeOf<KeyNotFoundException>());
            Assert.That(() => catalog.Resolve(new RecipeId("unknown")), Throws.TypeOf<KeyNotFoundException>());
            Assert.That(() => catalog.Resolve(new BuildingDefinitionId("unknown")), Throws.TypeOf<KeyNotFoundException>());
            Assert.That(() => catalog.Resolve(new OfflinePolicyRevision(2)), Throws.TypeOf<KeyNotFoundException>());
            Assert.That(() => catalog.Resolve(default(ResourceId)), Throws.TypeOf<InvalidOperationException>());
            Assert.That(() => catalog.Resolve(default(RecipeId)), Throws.TypeOf<InvalidOperationException>());
            Assert.That(() => catalog.Resolve(default(BuildingDefinitionId)), Throws.TypeOf<InvalidOperationException>());
            Assert.That(() => catalog.Resolve(default(OfflinePolicyRevision)), Throws.TypeOf<InvalidOperationException>());
        }

        [Test]
        public void CoreDefinitionAssemblyHasNoUnityDependency()
        {
            var references = typeof(DefinitionCatalog).Assembly.GetReferencedAssemblies().Select(reference => reference.Name);

            Assert.That(references, Has.None.StartsWith("UnityEngine"));
        }

        private static DefinitionCatalog CreateCatalog(
            IEnumerable<ResourceDefinition> resources = null,
            IEnumerable<RecipeDefinition> recipes = null,
            IEnumerable<BuildingDefinition> buildings = null,
            IEnumerable<OfflinePolicyDefinition> policies = null)
        {
            return new DefinitionCatalog(
                resources ?? CreateResources(),
                recipes ?? new[] { CreateRecipe() },
                buildings ?? new[] { CreateBuilding() },
                policies ?? new[] { SupportedPolicy() });
        }

        private static ResourceDefinition[] CreateResources()
        {
            return new[]
            {
                new ResourceDefinition(InputId, "Input", null, true),
                new ResourceDefinition(OutputId, "Output", "dev.output.sprite", true),
            };
        }

        private static RecipeDefinition CreateRecipe()
        {
            return new RecipeDefinition(RecipeId, Amounts(InputId, 2), Amounts(OutputId, 1), 50000000L, true);
        }

        private static BuildingDefinition CreateBuilding(
            BufferDefinition input = null,
            BufferDefinition output = null)
        {
            return new BuildingDefinition(
                BuildingId,
                new Footprint(2, 2),
                new[] { 0 },
                RecipeId,
                input ?? Buffer(BufferRole.Input, InputId, 4),
                output ?? Buffer(BufferRole.Output, OutputId, 1),
                "dev.machine.basic.sprite",
                true);
        }

        private static BuildingDefinition StorageBuilding(string presentationKey)
        {
            return new BuildingDefinition(
                BuildingId,
                new Footprint(1, 1),
                new[] { 0 },
                null,
                null,
                null,
                presentationKey,
                true);
        }

        private static OfflinePolicyDefinition SupportedPolicy()
        {
            return new OfflinePolicyDefinition(new OfflinePolicyRevision(1), 4, 5, 288000000000L);
        }

        private static ResourceAmountDefinition[] Amounts(ResourceId id, long amount)
        {
            return new[] { new ResourceAmountDefinition(id, amount) };
        }

        private static BufferDefinition Buffer(BufferRole role, ResourceId id, long capacity)
        {
            return new BufferDefinition(role, new[] { Pair(id, capacity) });
        }

        private static KeyValuePair<ResourceId, long> Pair(ResourceId id, long value)
        {
            return new KeyValuePair<ResourceId, long>(id, value);
        }
    }
}
