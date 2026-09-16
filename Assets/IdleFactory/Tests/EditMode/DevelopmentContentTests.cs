using System.Collections.Generic;
using System.IO;
using System.Linq;
using IdleFactory.Domain;
using IdleFactory.Unity.Content;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace IdleFactory.Tests.EditMode
{
    public sealed class DevelopmentContentTests
    {
        private const string Root = "Assets/IdleFactory/Content/Development";
        private const string InputPath = Root + "/Resources/dev.input.asset";
        private const string OutputPath = Root + "/Resources/dev.output.asset";
        private const string RecipePath = Root + "/Recipes/D001.asset";
        private const string BuildingPath = Root + "/Buildings/D001.asset";
        private const string PolicyPath = Root + "/OfflinePolicies/revision-1.asset";
        private const string CatalogPath = Root + "/DevelopmentCatalog.asset";

        private static readonly string[] AssetPaths =
        {
            InputPath,
            OutputPath,
            RecipePath,
            BuildingPath,
            PolicyPath,
            CatalogPath,
        };

        [Test]
        public void AllAuthoredAssetsLoadAtExactPathsWithStableUniqueGuidsAndMetas()
        {
            AssertAsset<ResourceDefinitionAsset>(InputPath);
            AssertAsset<ResourceDefinitionAsset>(OutputPath);
            AssertAsset<RecipeDefinitionAsset>(RecipePath);
            AssertAsset<BuildingDefinitionAsset>(BuildingPath);
            AssertAsset<OfflinePolicyAsset>(PolicyPath);
            AssertAsset<DefinitionCatalogAsset>(CatalogPath);

            var guids = AssetPaths.Select(AssetDatabase.AssetPathToGUID).ToArray();
            Assert.That(guids, Has.All.Not.Empty);
            Assert.That(guids.Distinct().Count(), Is.EqualTo(AssetPaths.Length));
            Assert.That(AssetPaths.Select(path => File.Exists(path + ".meta")), Has.All.True);
        }

        [Test]
        public void CatalogContainsExactlyTheAuthoredRegisteredGraphWithoutMissingReferences()
        {
            var input = Load<ResourceDefinitionAsset>(InputPath);
            var output = Load<ResourceDefinitionAsset>(OutputPath);
            var recipe = Load<RecipeDefinitionAsset>(RecipePath);
            var building = Load<BuildingDefinitionAsset>(BuildingPath);
            var policy = Load<OfflinePolicyAsset>(PolicyPath);
            var catalogAsset = Load<DefinitionCatalogAsset>(CatalogPath);
            var serialized = new SerializedObject(catalogAsset);

            Assert.That(
                new SerializedObject(input).FindProperty("presentationKey").stringValue,
                Is.EqualTo(string.Empty));

            AssertReferences(serialized.FindProperty("resources"), input, output);
            AssertReferences(serialized.FindProperty("recipes"), recipe);
            AssertReferences(serialized.FindProperty("buildings"), building);
            AssertReferences(serialized.FindProperty("offlinePolicies"), policy);

            Assert.That(() => DefinitionCatalogBuilder.Build(catalogAsset), Throws.Nothing);
        }

        [Test]
        public void BuilderProducesExactCompatibilitySensitiveDevelopmentDefinitions()
        {
            var catalog = DefinitionCatalogBuilder.Build(Load<DefinitionCatalogAsset>(CatalogPath));

            Assert.That(catalog.Resources.Count, Is.EqualTo(2));
            Assert.That(catalog.Recipes.Count, Is.EqualTo(1));
            Assert.That(catalog.Buildings.Count, Is.EqualTo(1));
            Assert.That(catalog.OfflinePolicies.Count, Is.EqualTo(1));

            var input = catalog.Resolve(new ResourceId("dev.input"));
            Assert.That(input.Id.Value, Is.EqualTo("dev.input"));
            Assert.That(input.DevelopmentLabel, Is.EqualTo("Input"));
            Assert.That(input.PresentationKey, Is.Null);
            Assert.That(input.IsDevelopmentOnly, Is.True);

            var output = catalog.Resolve(new ResourceId("dev.output"));
            Assert.That(output.Id.Value, Is.EqualTo("dev.output"));
            Assert.That(output.DevelopmentLabel, Is.EqualTo("Output"));
            Assert.That(output.PresentationKey, Is.EqualTo("dev.output.sprite"));
            Assert.That(output.IsDevelopmentOnly, Is.True);

            var recipe = catalog.Resolve(new RecipeId("dev.recipe.basic"));
            Assert.That(recipe.Id.Value, Is.EqualTo("dev.recipe.basic"));
            Assert.That(recipe.Inputs, Has.Count.EqualTo(1));
            Assert.That(recipe.Inputs.Single().ResourceId, Is.EqualTo(input.Id));
            Assert.That(recipe.Inputs.Single().Amount, Is.EqualTo(2L));
            Assert.That(recipe.Outputs, Has.Count.EqualTo(1));
            Assert.That(recipe.Outputs.Single().ResourceId, Is.EqualTo(output.Id));
            Assert.That(recipe.Outputs.Single().Amount, Is.EqualTo(1L));
            Assert.That(recipe.DurationTicks, Is.EqualTo(50000000L));
            Assert.That(recipe.IsDevelopmentOnly, Is.True);

            var building = catalog.Resolve(new BuildingDefinitionId("dev.machine.basic"));
            Assert.That(building.Id.Value, Is.EqualTo("dev.machine.basic"));
            Assert.That(building.Footprint, Is.EqualTo(new Footprint(2, 2)));
            Assert.That(building.AllowedOrientations, Is.EqualTo(new[] { 0 }));
            Assert.That(building.RecipeId, Is.EqualTo(recipe.Id));
            Assert.That(building.InputBuffer, Is.Not.Null);
            Assert.That(building.InputBuffer.Role, Is.EqualTo(BufferRole.Input));
            Assert.That(building.InputBuffer.Capacities, Has.Count.EqualTo(1));
            Assert.That(building.InputBuffer.Capacities[input.Id], Is.EqualTo(4L));
            Assert.That(building.OutputBuffer, Is.Not.Null);
            Assert.That(building.OutputBuffer.Role, Is.EqualTo(BufferRole.Output));
            Assert.That(building.OutputBuffer.Capacities, Has.Count.EqualTo(1));
            Assert.That(building.OutputBuffer.Capacities[output.Id], Is.EqualTo(1L));
            Assert.That(building.PresentationKey, Is.EqualTo("dev.machine.sprite"));
            Assert.That(building.IsDevelopmentOnly, Is.True);

            var policy = catalog.Resolve(new OfflinePolicyRevision(1));
            Assert.That(policy.Revision.Value, Is.EqualTo(1));
            Assert.That(policy.EfficiencyNumerator, Is.EqualTo(4L));
            Assert.That(policy.EfficiencyDenominator, Is.EqualTo(5L));
            Assert.That(policy.MaxCreditedTicks, Is.EqualTo(288000000000L));
        }

        private static void AssertAsset<T>(string path) where T : Object
        {
            Assert.That(Load<T>(path), Is.Not.Null, $"Expected {typeof(T).Name} at {path}");
        }

        private static T Load<T>(string path) where T : Object
        {
            return AssetDatabase.LoadAssetAtPath<T>(path);
        }

        private static void AssertReferences(SerializedProperty collection, params Object[] expected)
        {
            Assert.That(collection, Is.Not.Null);
            Assert.That(collection.isArray, Is.True);
            Assert.That(collection.arraySize, Is.EqualTo(expected.Length));

            var actual = new List<Object>(collection.arraySize);
            for (var index = 0; index < collection.arraySize; index++)
            {
                var reference = collection.GetArrayElementAtIndex(index).objectReferenceValue;
                Assert.That(reference, Is.Not.Null, $"Missing reference at index {index} in {collection.propertyPath}");
                actual.Add(reference);
            }

            Assert.That(actual, Is.EqualTo(expected));
            Assert.That(actual.Distinct().Count(), Is.EqualTo(actual.Count));
        }
    }
}
