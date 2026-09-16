using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using IdleFactory.Domain;
using IdleFactory.Unity.Content;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace IdleFactory.Tests.EditMode
{
    public sealed class DefinitionCatalogBuilderTests
    {
        private readonly List<ScriptableObject> createdAssets = new List<ScriptableObject>();

        [TearDown]
        public void TearDown()
        {
            foreach (var asset in createdAssets)
            {
                if (asset)
                {
                    UnityEngine.Object.DestroyImmediate(asset);
                }
            }

            createdAssets.Clear();
        }

        [Test]
        public void BuildsCompleteTransientGraphWithExactMappedValues()
        {
            var graph = CreateValidGraph();

            var catalog = DefinitionCatalogBuilder.Build(graph.Root);
            var input = catalog.Resolve(new ResourceId("dev.input"));
            var output = catalog.Resolve(new ResourceId("dev.output"));
            var recipe = catalog.Resolve(new RecipeId("dev.recipe.basic"));
            var building = catalog.Resolve(new BuildingDefinitionId("dev.machine.basic"));
            var policy = catalog.Resolve(new OfflinePolicyRevision(1));

            Assert.That(catalog.Resources.Count, Is.EqualTo(2));
            Assert.That(input.PresentationKey, Is.Null);
            Assert.That(input.IsDevelopmentOnly, Is.True);
            Assert.That(output.PresentationKey, Is.EqualTo("dev.output.sprite"));
            Assert.That(output.IsDevelopmentOnly, Is.True);
            Assert.That(recipe.Inputs.Single().ResourceId, Is.EqualTo(new ResourceId("dev.input")));
            Assert.That(recipe.Inputs.Single().Amount, Is.EqualTo(2));
            Assert.That(recipe.Outputs.Single().ResourceId, Is.EqualTo(new ResourceId("dev.output")));
            Assert.That(recipe.Outputs.Single().Amount, Is.EqualTo(1));
            Assert.That(recipe.DurationTicks, Is.EqualTo(50000000L));
            Assert.That(recipe.IsDevelopmentOnly, Is.True);
            Assert.That(building.Footprint, Is.EqualTo(new Footprint(2, 2)));
            Assert.That(building.AllowedOrientations, Is.EqualTo(new[] { 0 }));
            Assert.That(building.RecipeId, Is.EqualTo(recipe.Id));
            Assert.That(building.InputBuffer.Role, Is.EqualTo(BufferRole.Input));
            Assert.That(building.InputBuffer.Capacities[new ResourceId("dev.input")], Is.EqualTo(4));
            Assert.That(building.OutputBuffer.Role, Is.EqualTo(BufferRole.Output));
            Assert.That(building.OutputBuffer.Capacities[new ResourceId("dev.output")], Is.EqualTo(1));
            Assert.That(building.PresentationKey, Is.EqualTo("dev.machine.sprite"));
            Assert.That(building.IsDevelopmentOnly, Is.True);
            Assert.That(policy.EfficiencyNumerator, Is.EqualTo(4));
            Assert.That(policy.EfficiencyDenominator, Is.EqualTo(5));
            Assert.That(policy.MaxCreditedTicks, Is.EqualTo(288000000000L));
        }

        [Test]
        public void MapsAuthoredPresentationKeysAndDevelopmentFlagsWithoutConstants()
        {
            var graph = CreateValidGraph();
            SetField(graph.Input, "presentationKey", "alternate.input.icon");
            SetField(graph.Input, "isDevelopmentOnly", false);
            SetField(graph.Output, "presentationKey", "alternate.output.icon");
            SetField(graph.Output, "isDevelopmentOnly", true);
            SetField(graph.Recipe, "isDevelopmentOnly", false);
            SetField(graph.Building, "presentationKey", "alternate.machine.prefab");
            SetField(graph.Building, "isDevelopmentOnly", true);

            var first = DefinitionCatalogBuilder.Build(graph.Root);
            Assert.That(first.Resolve(new ResourceId("dev.input")).PresentationKey, Is.EqualTo("alternate.input.icon"));
            Assert.That(first.Resolve(new ResourceId("dev.input")).IsDevelopmentOnly, Is.False);
            Assert.That(first.Resolve(new ResourceId("dev.output")).PresentationKey, Is.EqualTo("alternate.output.icon"));
            Assert.That(first.Resolve(new ResourceId("dev.output")).IsDevelopmentOnly, Is.True);
            Assert.That(first.Resolve(new RecipeId("dev.recipe.basic")).IsDevelopmentOnly, Is.False);
            Assert.That(first.Resolve(new BuildingDefinitionId("dev.machine.basic")).PresentationKey, Is.EqualTo("alternate.machine.prefab"));
            Assert.That(first.Resolve(new BuildingDefinitionId("dev.machine.basic")).IsDevelopmentOnly, Is.True);

            SetField(graph.Input, "presentationKey", "alternate.input.secondary");
            SetField(graph.Input, "isDevelopmentOnly", true);
            SetField(graph.Output, "presentationKey", "alternate.output.secondary");
            SetField(graph.Output, "isDevelopmentOnly", false);
            SetField(graph.Recipe, "isDevelopmentOnly", true);
            SetField(graph.Building, "presentationKey", "alternate.machine.secondary");
            SetField(graph.Building, "isDevelopmentOnly", false);

            var second = DefinitionCatalogBuilder.Build(graph.Root);
            Assert.That(second.Resolve(new ResourceId("dev.input")).PresentationKey, Is.EqualTo("alternate.input.secondary"));
            Assert.That(second.Resolve(new ResourceId("dev.input")).IsDevelopmentOnly, Is.True);
            Assert.That(second.Resolve(new ResourceId("dev.output")).PresentationKey, Is.EqualTo("alternate.output.secondary"));
            Assert.That(second.Resolve(new ResourceId("dev.output")).IsDevelopmentOnly, Is.False);
            Assert.That(second.Resolve(new RecipeId("dev.recipe.basic")).IsDevelopmentOnly, Is.True);
            Assert.That(second.Resolve(new BuildingDefinitionId("dev.machine.basic")).PresentationKey, Is.EqualTo("alternate.machine.secondary"));
            Assert.That(second.Resolve(new BuildingDefinitionId("dev.machine.basic")).IsDevelopmentOnly, Is.False);
        }

        [Test]
        public void NullDestroyedRootAndTopLevelReferencesFailDeliberately()
        {
            Assert.That(
                () => DefinitionCatalogBuilder.Build(null),
                Throws.TypeOf<ArgumentNullException>());

            var destroyedRoot = New<DefinitionCatalogAsset>();
            UnityEngine.Object.DestroyImmediate(destroyedRoot);
            Assert.That(
                () => DefinitionCatalogBuilder.Build(destroyedRoot),
                Throws.TypeOf<ArgumentException>());

            var graph = CreateValidGraph();
            SetField(graph.Root, "resources", new List<ResourceDefinitionAsset> { null });
            Assert.That(
                () => DefinitionCatalogBuilder.Build(graph.Root),
                Throws.TypeOf<ArgumentNullException>());

            var destroyedResource = New<ResourceDefinitionAsset>();
            UnityEngine.Object.DestroyImmediate(destroyedResource);
            SetField(graph.Root, "resources", new List<ResourceDefinitionAsset> { destroyedResource });
            Assert.That(
                () => DefinitionCatalogBuilder.Build(graph.Root),
                Throws.TypeOf<ArgumentException>());
        }

        [Test]
        public void NullAndDestroyedNestedReferencesFailWithoutNullReferenceExceptions()
        {
            var graph = CreateValidGraph();
            SetField(
                graph.Recipe,
                "inputs",
                new List<RecipeDefinitionAsset.ResourceAmountRecord> { null });
            Assert.That(
                () => DefinitionCatalogBuilder.Build(graph.Root),
                Throws.InstanceOf<ArgumentException>().And.Not.TypeOf<NullReferenceException>());

            SetField(
                graph.Recipe,
                "inputs",
                new List<RecipeDefinitionAsset.ResourceAmountRecord>
                {
                    new RecipeDefinitionAsset.ResourceAmountRecord(null, 2),
                });
            Assert.That(
                () => DefinitionCatalogBuilder.Build(graph.Root),
                Throws.TypeOf<ArgumentNullException>());

            var destroyed = New<ResourceDefinitionAsset>();
            var record = new RecipeDefinitionAsset.ResourceAmountRecord(destroyed, 2);
            UnityEngine.Object.DestroyImmediate(destroyed);
            SetField(graph.Recipe, "inputs", new List<RecipeDefinitionAsset.ResourceAmountRecord> { record });
            Assert.That(
                () => DefinitionCatalogBuilder.Build(graph.Root),
                Throws.TypeOf<ArgumentException>());

            graph = CreateValidGraph();
            var destroyedRecipe = New<RecipeDefinitionAsset>();
            SetField(graph.Building, "recipe", destroyedRecipe);
            UnityEngine.Object.DestroyImmediate(destroyedRecipe);
            Assert.That(
                () => DefinitionCatalogBuilder.Build(graph.Root),
                Throws.TypeOf<ArgumentException>());
        }

        [Test]
        public void NullRootAndNestedCollectionsFailDeliberately()
        {
            var graph = CreateValidGraph();
            SetField(graph.Root, "recipes", null);
            Assert.That(() => DefinitionCatalogBuilder.Build(graph.Root), Throws.InstanceOf<ArgumentException>());

            graph = CreateValidGraph();
            SetField(graph.Recipe, "inputs", null);
            Assert.That(() => DefinitionCatalogBuilder.Build(graph.Root), Throws.InstanceOf<ArgumentException>());

            graph = CreateValidGraph();
            SetField(graph.Building, "allowedOrientations", null);
            Assert.That(() => DefinitionCatalogBuilder.Build(graph.Root), Throws.InstanceOf<ArgumentException>());

            graph = CreateValidGraph();
            SetField(graph.Building, "inputCapacities", null);
            Assert.That(() => DefinitionCatalogBuilder.Build(graph.Root), Throws.InstanceOf<ArgumentException>());
        }

        [Test]
        public void WhitespaceAndDefaultSerializedFieldsFailThroughDomainValidation()
        {
            var graph = CreateValidGraph();
            SetField(graph.Input, "id", "   ");
            Assert.That(() => DefinitionCatalogBuilder.Build(graph.Root), Throws.InstanceOf<ArgumentException>());

            graph = CreateValidGraph();
            SetField(graph.Input, "developmentLabel", null);
            Assert.That(() => DefinitionCatalogBuilder.Build(graph.Root), Throws.InstanceOf<ArgumentException>());

            graph = CreateValidGraph();
            SetField(graph.Recipe, "id", null);
            Assert.That(() => DefinitionCatalogBuilder.Build(graph.Root), Throws.InstanceOf<ArgumentException>());

            graph = CreateValidGraph();
            SetField(graph.Building, "presentationKey", " ");
            Assert.That(() => DefinitionCatalogBuilder.Build(graph.Root), Throws.InstanceOf<ArgumentException>());
        }

        [Test]
        public void InvalidNumbersFootprintsOrientationsAndPolicyFailWithoutRepair()
        {
            var graph = CreateValidGraph();
            SetField(graph.InputAmount, "amount", 0L);
            Assert.That(() => DefinitionCatalogBuilder.Build(graph.Root), Throws.InstanceOf<ArgumentException>());

            graph = CreateValidGraph();
            SetField(graph.Recipe, "durationTicks", -1L);
            Assert.That(() => DefinitionCatalogBuilder.Build(graph.Root), Throws.InstanceOf<ArgumentException>());

            graph = CreateValidGraph();
            SetField(graph.InputCapacity, "capacity", -1L);
            Assert.That(() => DefinitionCatalogBuilder.Build(graph.Root), Throws.InstanceOf<ArgumentException>());

            graph = CreateValidGraph();
            SetField(graph.Building, "footprintWidth", 0);
            Assert.That(() => DefinitionCatalogBuilder.Build(graph.Root), Throws.InstanceOf<ArgumentException>());

            graph = CreateValidGraph();
            SetField(graph.Building, "allowedOrientations", new List<int>());
            Assert.That(() => DefinitionCatalogBuilder.Build(graph.Root), Throws.InstanceOf<ArgumentException>());

            graph = CreateValidGraph();
            SetField(graph.Building, "allowedOrientations", new List<int> { 0, 0 });
            Assert.That(() => DefinitionCatalogBuilder.Build(graph.Root), Throws.InstanceOf<ArgumentException>());

            graph = CreateValidGraph();
            SetField(graph.Building, "allowedOrientations", new List<int> { 4 });
            Assert.That(() => DefinitionCatalogBuilder.Build(graph.Root), Throws.InstanceOf<ArgumentException>());

            graph = CreateValidGraph();
            SetField(graph.Policy, "efficiencyNumerator", 3L);
            Assert.That(() => DefinitionCatalogBuilder.Build(graph.Root), Throws.InstanceOf<ArgumentException>());

            graph = CreateValidGraph();
            SetField(graph.Policy, "revision", 2);
            Assert.That(() => DefinitionCatalogBuilder.Build(graph.Root), Throws.InstanceOf<ArgumentException>());
        }

        [Test]
        public void DuplicateNestedRowsAndNormalizedTopLevelIdsAreRejected()
        {
            var graph = CreateValidGraph();
            var duplicateInput = new RecipeDefinitionAsset.ResourceAmountRecord(graph.Input, 1);
            SetField(
                graph.Recipe,
                "inputs",
                new List<RecipeDefinitionAsset.ResourceAmountRecord> { graph.InputAmount, duplicateInput });
            Assert.That(() => DefinitionCatalogBuilder.Build(graph.Root), Throws.InstanceOf<ArgumentException>());

            graph = CreateValidGraph();
            var duplicateCapacity = new BuildingDefinitionAsset.ResourceCapacityRecord(graph.Input, 7);
            SetField(
                graph.Building,
                "inputCapacities",
                new List<BuildingDefinitionAsset.ResourceCapacityRecord> { graph.InputCapacity, duplicateCapacity });
            Assert.That(() => DefinitionCatalogBuilder.Build(graph.Root), Throws.InstanceOf<ArgumentException>());

            graph = CreateValidGraph();
            var duplicateResource = New<ResourceDefinitionAsset>();
            ConfigureResource(duplicateResource, " dev.input ", "Duplicate", null, true);
            SetField(
                graph.Root,
                "resources",
                new List<ResourceDefinitionAsset> { graph.Input, graph.Output, duplicateResource });
            Assert.That(() => DefinitionCatalogBuilder.Build(graph.Root), Throws.InstanceOf<ArgumentException>());
        }

        [Test]
        public void EmptyRecipeRowsAreRejectedAndValidRowOrderIsPreserved()
        {
            var graph = CreateValidGraph();
            SetField(graph.Recipe, "inputs", new List<RecipeDefinitionAsset.ResourceAmountRecord>());
            Assert.That(() => DefinitionCatalogBuilder.Build(graph.Root), Throws.InstanceOf<ArgumentException>());

            graph = CreateValidGraph();
            SetField(graph.Recipe, "outputs", new List<RecipeDefinitionAsset.ResourceAmountRecord>());
            Assert.That(() => DefinitionCatalogBuilder.Build(graph.Root), Throws.InstanceOf<ArgumentException>());

            graph = CreateValidGraph();
            SetField(graph.Recipe, "inputs", new List<RecipeDefinitionAsset.ResourceAmountRecord>
            {
                new RecipeDefinitionAsset.ResourceAmountRecord(graph.Output, 3),
                new RecipeDefinitionAsset.ResourceAmountRecord(graph.Input, 2),
            });
            SetField(graph.Building, "inputCapacities", new List<BuildingDefinitionAsset.ResourceCapacityRecord>
            {
                new BuildingDefinitionAsset.ResourceCapacityRecord(graph.Output, 3),
                graph.InputCapacity,
            });

            var recipe = DefinitionCatalogBuilder.Build(graph.Root).Resolve(new RecipeId("dev.recipe.basic"));
            Assert.That(
                recipe.Inputs.Select(amount => amount.ResourceId.Value),
                Is.EqualTo(new[] { "dev.output", "dev.input" }));
        }

        [Test]
        public void ReferencesMustBelongToTheRootCatalog()
        {
            var graph = CreateValidGraph();
            var unregisteredResource = New<ResourceDefinitionAsset>();
            ConfigureResource(unregisteredResource, "other.resource", "Other", null, false);
            SetField(
                graph.Recipe,
                "inputs",
                new List<RecipeDefinitionAsset.ResourceAmountRecord>
                {
                    new RecipeDefinitionAsset.ResourceAmountRecord(unregisteredResource, 1),
                });
            Assert.That(() => DefinitionCatalogBuilder.Build(graph.Root), Throws.InstanceOf<ArgumentException>());

            graph = CreateValidGraph();
            var unregisteredRecipe = New<RecipeDefinitionAsset>();
            ConfigureRecipe(unregisteredRecipe, "other.recipe", graph.Input, graph.Output);
            SetField(graph.Building, "recipe", unregisteredRecipe);
            Assert.That(() => DefinitionCatalogBuilder.Build(graph.Root), Throws.InstanceOf<ArgumentException>());

            graph = CreateValidGraph();
            unregisteredResource = New<ResourceDefinitionAsset>();
            ConfigureResource(unregisteredResource, "other.resource", "Other", null, false);
            SetField(
                graph.Building,
                "inputCapacities",
                new List<BuildingDefinitionAsset.ResourceCapacityRecord>
                {
                    new BuildingDefinitionAsset.ResourceCapacityRecord(unregisteredResource, 1),
                });
            Assert.That(() => DefinitionCatalogBuilder.Build(graph.Root), Throws.InstanceOf<ArgumentException>());
        }

        [Test]
        public void BufferPresenceFlagsAndRecipeCoverageAreStrict()
        {
            var graph = CreateValidGraph();
            SetField(graph.Building, "hasInputBuffer", false);
            Assert.That(() => DefinitionCatalogBuilder.Build(graph.Root), Throws.InstanceOf<ArgumentException>());

            graph = CreateValidGraph();
            SetField(graph.Building, "inputCapacities", new List<BuildingDefinitionAsset.ResourceCapacityRecord>());
            Assert.That(() => DefinitionCatalogBuilder.Build(graph.Root), Throws.InstanceOf<ArgumentException>());

            graph = CreateValidGraph();
            SetField(graph.Building, "inputCapacities", new List<BuildingDefinitionAsset.ResourceCapacityRecord>
            {
                new BuildingDefinitionAsset.ResourceCapacityRecord(graph.Output, 4),
            });
            Assert.That(() => DefinitionCatalogBuilder.Build(graph.Root), Throws.InstanceOf<ArgumentException>());

            graph = CreateValidGraph();
            SetField(graph.Building, "hasInputBuffer", false);
            SetField(graph.Building, "inputCapacities", new List<BuildingDefinitionAsset.ResourceCapacityRecord>());
            Assert.That(() => DefinitionCatalogBuilder.Build(graph.Root), Throws.InstanceOf<ArgumentException>());

            graph = CreateValidGraph();
            SetField(graph.Building, "recipe", null);
            SetField(graph.Building, "hasInputBuffer", false);
            SetField(graph.Building, "inputCapacities", new List<BuildingDefinitionAsset.ResourceCapacityRecord>());
            SetField(graph.Building, "hasOutputBuffer", false);
            SetField(graph.Building, "outputCapacities", new List<BuildingDefinitionAsset.ResourceCapacityRecord>());

            var storage = DefinitionCatalogBuilder.Build(graph.Root)
                .Resolve(new BuildingDefinitionId("dev.machine.basic"));
            Assert.That(storage.RecipeId, Is.Null);
            Assert.That(storage.InputBuffer, Is.Null);
            Assert.That(storage.OutputBuffer, Is.Null);
        }

        [Test]
        public void GenericNonD001GraphBuildsWithoutHardCodedContentIds()
        {
            var graph = CreateValidGraph("alpha");

            var catalog = DefinitionCatalogBuilder.Build(graph.Root);

            Assert.That(catalog.Resolve(new ResourceId("alpha.input")).DevelopmentLabel, Is.EqualTo("Input"));
            Assert.That(catalog.Resolve(new RecipeId("alpha.recipe")).DurationTicks, Is.EqualTo(50000000L));
            Assert.That(catalog.Resolve(new BuildingDefinitionId("alpha.machine")).RecipeId.Value, Is.EqualTo(new RecipeId("alpha.recipe")));
        }

        [Test]
        public void BuiltCoreRemainsUnchangedAfterEverySourceKindMutatesAndIsDestroyed()
        {
            var graph = CreateValidGraph();
            var catalog = DefinitionCatalogBuilder.Build(graph.Root);
            var recipe = catalog.Resolve(new RecipeId("dev.recipe.basic"));
            var building = catalog.Resolve(new BuildingDefinitionId("dev.machine.basic"));

            SetField(graph.Input, "id", "mutated.input");
            SetField(graph.Recipe, "durationTicks", 1L);
            SetField(graph.InputAmount, "amount", 99L);
            SetField(graph.InputCapacity, "capacity", 99L);
            GetField<List<ResourceDefinitionAsset>>(graph.Root, "resources").Clear();
            GetField<List<RecipeDefinitionAsset.ResourceAmountRecord>>(graph.Recipe, "inputs").Clear();
            GetField<List<int>>(graph.Building, "allowedOrientations").Add(1);
            foreach (var asset in createdAssets.ToArray())
            {
                if (asset)
                {
                    UnityEngine.Object.DestroyImmediate(asset);
                }
            }

            Assert.That(catalog.Resolve(new ResourceId("dev.input")).DevelopmentLabel, Is.EqualTo("Input"));
            Assert.That(recipe.DurationTicks, Is.EqualTo(50000000L));
            Assert.That(recipe.Inputs.Single().Amount, Is.EqualTo(2));
            Assert.That(building.InputBuffer.Capacities[new ResourceId("dev.input")], Is.EqualTo(4));
            Assert.That(building.AllowedOrientations, Is.EqualTo(new[] { 0 }));
        }

        [Test]
        public void BuilderDoesNotModifySerializedSourcesOnSuccessOrFailure()
        {
            var graph = CreateValidGraph();
            var beforeSuccess = SnapshotSerializedSources(graph);

            DefinitionCatalogBuilder.Build(graph.Root);

            Assert.That(SnapshotSerializedSources(graph), Is.EqualTo(beforeSuccess));

            SetField(graph.Recipe, "durationTicks", 0L);
            var beforeFailure = SnapshotSerializedSources(graph);
            Assert.That(() => DefinitionCatalogBuilder.Build(graph.Root), Throws.InstanceOf<ArgumentException>());
            Assert.That(SnapshotSerializedSources(graph), Is.EqualTo(beforeFailure));
        }

        [Test]
        public void RepeatedBuildsAreIndependentAndDeterministic()
        {
            var graph = CreateValidGraph();

            var first = DefinitionCatalogBuilder.Build(graph.Root);
            var second = DefinitionCatalogBuilder.Build(graph.Root);

            Assert.That(second, Is.Not.SameAs(first));
            Assert.That(second.Resolve(new ResourceId("dev.input")), Is.Not.SameAs(first.Resolve(new ResourceId("dev.input"))));
            Assert.That(second.Resolve(new RecipeId("dev.recipe.basic")).Inputs.Select(x => x.ResourceId.Value),
                Is.EqualTo(first.Resolve(new RecipeId("dev.recipe.basic")).Inputs.Select(x => x.ResourceId.Value)));
            Assert.That(second.Resolve(new BuildingDefinitionId("dev.machine.basic")).AllowedOrientations,
                Is.EqualTo(first.Resolve(new BuildingDefinitionId("dev.machine.basic")).AllowedOrientations));
        }

        [Test]
        public void FailedBuildCannotAlterPreviouslyPublishedCoreResult()
        {
            var graph = CreateValidGraph();
            var published = DefinitionCatalogBuilder.Build(graph.Root);
            var originalDuration = published.Resolve(new RecipeId("dev.recipe.basic")).DurationTicks;
            SetField(graph.Recipe, "durationTicks", 0L);

            Assert.That(() => DefinitionCatalogBuilder.Build(graph.Root), Throws.InstanceOf<ArgumentException>());
            Assert.That(published.Resolve(new RecipeId("dev.recipe.basic")).DurationTicks, Is.EqualTo(originalDuration));
            Assert.That(published.Resources.Count, Is.EqualTo(2));
        }

        [Test]
        public void PublishedCoreGraphContainsNoUnityObjects()
        {
            var catalog = DefinitionCatalogBuilder.Build(CreateValidGraph().Root);
            var values = new object[]
            {
                catalog,
                catalog.Resources,
                catalog.Recipes,
                catalog.Buildings,
                catalog.OfflinePolicies,
                catalog.Resolve(new ResourceId("dev.input")),
                catalog.Resolve(new RecipeId("dev.recipe.basic")),
                catalog.Resolve(new BuildingDefinitionId("dev.machine.basic")),
                catalog.Resolve(new OfflinePolicyRevision(1)),
            };

            Assert.That(values, Has.None.InstanceOf<UnityEngine.Object>());
            Assert.That(typeof(DefinitionCatalog).Assembly.GetReferencedAssemblies().Select(reference => reference.Name),
                Has.None.StartsWith("UnityEngine"));
        }

        private Graph CreateValidGraph(string prefix = "dev")
        {
            var input = New<ResourceDefinitionAsset>();
            var output = New<ResourceDefinitionAsset>();
            ConfigureResource(input, prefix + ".input", "Input", null, true);
            ConfigureResource(output, prefix + ".output", "Output", prefix + ".output.sprite", true);

            var recipe = New<RecipeDefinitionAsset>();
            ConfigureRecipe(recipe, prefix + ".recipe" + (prefix == "dev" ? ".basic" : string.Empty), input, output);
            var inputAmount = GetField<List<RecipeDefinitionAsset.ResourceAmountRecord>>(recipe, "inputs").Single();
            var outputAmount = GetField<List<RecipeDefinitionAsset.ResourceAmountRecord>>(recipe, "outputs").Single();

            var building = New<BuildingDefinitionAsset>();
            var inputCapacity = new BuildingDefinitionAsset.ResourceCapacityRecord(input, 4);
            var outputCapacity = new BuildingDefinitionAsset.ResourceCapacityRecord(output, 1);
            SetField(building, "id", prefix + ".machine" + (prefix == "dev" ? ".basic" : string.Empty));
            SetField(building, "footprintWidth", 2);
            SetField(building, "footprintHeight", 2);
            SetField(building, "allowedOrientations", new List<int> { 0 });
            SetField(building, "recipe", recipe);
            SetField(building, "hasInputBuffer", true);
            SetField(building, "inputCapacities", new List<BuildingDefinitionAsset.ResourceCapacityRecord> { inputCapacity });
            SetField(building, "hasOutputBuffer", true);
            SetField(building, "outputCapacities", new List<BuildingDefinitionAsset.ResourceCapacityRecord> { outputCapacity });
            SetField(building, "presentationKey", prefix + ".machine.sprite");
            SetField(building, "isDevelopmentOnly", true);

            var policy = New<OfflinePolicyAsset>();
            SetField(policy, "revision", 1);
            SetField(policy, "efficiencyNumerator", 4L);
            SetField(policy, "efficiencyDenominator", 5L);
            SetField(policy, "maxCreditedTicks", 288000000000L);

            var root = New<DefinitionCatalogAsset>();
            SetField(root, "resources", new List<ResourceDefinitionAsset> { input, output });
            SetField(root, "recipes", new List<RecipeDefinitionAsset> { recipe });
            SetField(root, "buildings", new List<BuildingDefinitionAsset> { building });
            SetField(root, "offlinePolicies", new List<OfflinePolicyAsset> { policy });

            return new Graph(root, input, output, recipe, building, policy, inputAmount, outputAmount, inputCapacity, outputCapacity);
        }

        private static void ConfigureResource(ResourceDefinitionAsset asset, string id, string label, string key, bool developmentOnly)
        {
            SetField(asset, "id", id);
            SetField(asset, "developmentLabel", label);
            SetField(asset, "presentationKey", key);
            SetField(asset, "isDevelopmentOnly", developmentOnly);
        }

        private static void ConfigureRecipe(RecipeDefinitionAsset asset, string id, ResourceDefinitionAsset input, ResourceDefinitionAsset output)
        {
            SetField(asset, "id", id);
            SetField(asset, "inputs", new List<RecipeDefinitionAsset.ResourceAmountRecord>
            {
                new RecipeDefinitionAsset.ResourceAmountRecord(input, 2),
            });
            SetField(asset, "outputs", new List<RecipeDefinitionAsset.ResourceAmountRecord>
            {
                new RecipeDefinitionAsset.ResourceAmountRecord(output, 1),
            });
            SetField(asset, "durationTicks", 50000000L);
            SetField(asset, "isDevelopmentOnly", true);
        }

        private T New<T>() where T : ScriptableObject
        {
            var asset = ScriptableObject.CreateInstance<T>();
            createdAssets.Add(asset);
            return asset;
        }

        private static void SetField(object target, string name, object value)
        {
            var field = target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, $"Missing serialized field {target.GetType().Name}.{name}");
            field.SetValue(target, value);
        }

        private static T GetField<T>(object target, string name)
        {
            var field = target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, $"Missing serialized field {target.GetType().Name}.{name}");
            return (T)field.GetValue(target);
        }

        private static string SnapshotSerializedSources(Graph graph)
        {
            return string.Join("\n", new UnityEngine.Object[]
            {
                graph.Root,
                graph.Input,
                graph.Output,
                graph.Recipe,
                graph.Building,
                graph.Policy,
            }.Select(EditorJsonUtility.ToJson));
        }

        private sealed class Graph
        {
            public Graph(
                DefinitionCatalogAsset root,
                ResourceDefinitionAsset input,
                ResourceDefinitionAsset output,
                RecipeDefinitionAsset recipe,
                BuildingDefinitionAsset building,
                OfflinePolicyAsset policy,
                RecipeDefinitionAsset.ResourceAmountRecord inputAmount,
                RecipeDefinitionAsset.ResourceAmountRecord outputAmount,
                BuildingDefinitionAsset.ResourceCapacityRecord inputCapacity,
                BuildingDefinitionAsset.ResourceCapacityRecord outputCapacity)
            {
                Root = root;
                Input = input;
                Output = output;
                Recipe = recipe;
                Building = building;
                Policy = policy;
                InputAmount = inputAmount;
                OutputAmount = outputAmount;
                InputCapacity = inputCapacity;
                OutputCapacity = outputCapacity;
            }

            public DefinitionCatalogAsset Root { get; }
            public ResourceDefinitionAsset Input { get; }
            public ResourceDefinitionAsset Output { get; }
            public RecipeDefinitionAsset Recipe { get; }
            public BuildingDefinitionAsset Building { get; }
            public OfflinePolicyAsset Policy { get; }
            public RecipeDefinitionAsset.ResourceAmountRecord InputAmount { get; }
            public RecipeDefinitionAsset.ResourceAmountRecord OutputAmount { get; }
            public BuildingDefinitionAsset.ResourceCapacityRecord InputCapacity { get; }
            public BuildingDefinitionAsset.ResourceCapacityRecord OutputCapacity { get; }
        }
    }
}
