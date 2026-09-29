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
        private const string ContentPath = "Assets/IdleFactory/Content/Development/development-catalog.json";

        [Test]
        public void PackagedJsonAuthorityLoadsAtExactPathWithStableGuidAndMeta()
        {
            var asset = AssetDatabase.LoadAssetAtPath<TextAsset>(ContentPath);
            Assert.That(asset, Is.Not.Null, $"Expected a TextAsset at {ContentPath}");
            Assert.That(AssetDatabase.AssetPathToGUID(ContentPath), Has.Length.EqualTo(32));
            Assert.That(File.Exists(ContentPath + ".meta"), Is.True);
        }

        [Test]
        public void CatalogLoadsThroughGameplayContentLoaderWithoutDiagnostics()
        {
            var result = GameplayContentLoader.Load(File.ReadAllBytes(ContentPath));

            Assert.That(result.IsSuccess, Is.True);
            Assert.That(result.Diagnostics, Is.Empty);
        }

        [Test]
        public void JsonProducesExactCompatibilitySensitiveDevelopmentDefinitions()
        {
            var catalog = GameplayContentLoader.Load(File.ReadAllBytes(ContentPath)).Catalog;

            Assert.That(catalog.Resources.Count, Is.EqualTo(2));
            Assert.That(catalog.Recipes.Count, Is.EqualTo(1));
            Assert.That(catalog.Buildings.Count, Is.EqualTo(1));
            Assert.That(catalog.OfflinePolicies.Count, Is.EqualTo(1));

            var input = catalog.Resolve(new ResourceId("dev.input"));
            Assert.That(input.Id.Value, Is.EqualTo("dev.input"));
            Assert.That(input.DevelopmentLabel, Is.EqualTo("Input"));
            Assert.That(input.PresentationKey, Is.EqualTo("F001-RESOURCE-DEV-INPUT"));
            Assert.That(input.IsDevelopmentOnly, Is.True);

            var output = catalog.Resolve(new ResourceId("dev.output"));
            Assert.That(output.Id.Value, Is.EqualTo("dev.output"));
            Assert.That(output.DevelopmentLabel, Is.EqualTo("Output"));
            Assert.That(output.PresentationKey, Is.EqualTo("F001-RESOURCE-DEV-OUTPUT"));
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
            Assert.That(building.PresentationKey, Is.EqualTo("F001-MACHINE-D001-NORMAL"));
            Assert.That(building.IsDevelopmentOnly, Is.True);

            var policy = catalog.Resolve(new OfflinePolicyRevision(1));
            Assert.That(policy.Revision.Value, Is.EqualTo(1));
            Assert.That(policy.EfficiencyNumerator, Is.EqualTo(4L));
            Assert.That(policy.EfficiencyDenominator, Is.EqualTo(5L));
            Assert.That(policy.MaxCreditedTicks, Is.EqualTo(288000000000L));
        }
    }
}
