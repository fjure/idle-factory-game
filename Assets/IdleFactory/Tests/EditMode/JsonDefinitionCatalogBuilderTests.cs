using System.Linq;
using System.Text;
using IdleFactory.Domain;
using IdleFactory.Unity.Content;
using NUnit.Framework;

namespace IdleFactory.Tests.EditMode
{
    /// <summary>
    /// Builder pass-through behavior on the real JSON path: isDevelopmentOnly is copied as authored
    /// (not hard-coded) and a storage building without recipe or buffers keeps null recipe and buffers.
    /// </summary>
    public sealed class JsonDefinitionCatalogBuilderTests
    {
        private const string Json = @"{
  ""contentSchemaVersion"": 1,
  ""resources"": [
    { ""id"": ""dev.input"", ""developmentLabel"": ""Input"", ""presentationKey"": ""F001-RESOURCE-DEV-INPUT"", ""isDevelopmentOnly"": false },
    { ""id"": ""dev.output"", ""developmentLabel"": ""Output"", ""isDevelopmentOnly"": true }
  ],
  ""recipes"": [
    {
      ""id"": ""dev.recipe.basic"",
      ""inputs"": [ { ""resourceId"": ""dev.input"", ""amount"": 2 } ],
      ""outputs"": [ { ""resourceId"": ""dev.output"", ""amount"": 1 } ],
      ""durationTicks"": 50000000,
      ""isDevelopmentOnly"": false
    }
  ],
  ""buildings"": [
    {
      ""id"": ""dev.machine.basic"",
      ""footprintWidth"": 2,
      ""footprintHeight"": 2,
      ""allowedOrientations"": [0],
      ""recipeId"": ""dev.recipe.basic"",
      ""inputBuffer"": [ { ""resourceId"": ""dev.input"", ""capacity"": 4 } ],
      ""outputBuffer"": [ { ""resourceId"": ""dev.output"", ""capacity"": 1 } ],
      ""presentationKey"": ""F001-MACHINE-D001-NORMAL"",
      ""isDevelopmentOnly"": false
    },
    {
      ""id"": ""dev.storage.basic"",
      ""footprintWidth"": 1,
      ""footprintHeight"": 1,
      ""allowedOrientations"": [0],
      ""presentationKey"": ""F001-MACHINE-ALT-3X1-NORMAL"",
      ""isDevelopmentOnly"": true
    }
  ],
  ""offlinePolicies"": [
    { ""revision"": 1, ""efficiencyNumerator"": 4, ""efficiencyDenominator"": 5, ""maxCreditedTicks"": 288000000000 }
  ]
}";

        private static DefinitionCatalog Load()
        {
            var result = GameplayContentLoader.Load(new UTF8Encoding(false, true).GetBytes(Json));
            Assert.That(result.IsSuccess, Is.True,
                string.Join("; ", result.Diagnostics.Select(diagnostic => diagnostic.Code + " " + diagnostic.Message)));
            return result.Catalog;
        }

        [Test]
        public void IsDevelopmentOnlyIsPassedThroughAsAuthoredForResourcesRecipesAndBuildings()
        {
            var catalog = Load();

            Assert.That(catalog.Resolve(new ResourceId("dev.input")).IsDevelopmentOnly, Is.False);
            Assert.That(catalog.Resolve(new ResourceId("dev.output")).IsDevelopmentOnly, Is.True);
            Assert.That(catalog.Resolve(new RecipeId("dev.recipe.basic")).IsDevelopmentOnly, Is.False);
            Assert.That(catalog.Resolve(new BuildingDefinitionId("dev.machine.basic")).IsDevelopmentOnly, Is.False);
            Assert.That(catalog.Resolve(new BuildingDefinitionId("dev.storage.basic")).IsDevelopmentOnly, Is.True);
        }

        [Test]
        public void StorageBuildingWithoutRecipeOrBuffersBuildsWithNullRecipeAndBuffers()
        {
            var catalog = Load();

            var storage = catalog.Resolve(new BuildingDefinitionId("dev.storage.basic"));
            Assert.That(storage.RecipeId, Is.Null);
            Assert.That(storage.InputBuffer, Is.Null);
            Assert.That(storage.OutputBuffer, Is.Null);
            Assert.That(storage.PresentationKey, Is.EqualTo("F001-MACHINE-ALT-3X1-NORMAL"));
            Assert.That(storage.Footprint, Is.EqualTo(new Footprint(1, 1)));

            var machine = catalog.Resolve(new BuildingDefinitionId("dev.machine.basic"));
            Assert.That(machine.RecipeId, Is.Not.Null);
            Assert.That(machine.InputBuffer, Is.Not.Null);
            Assert.That(machine.OutputBuffer, Is.Not.Null);
            Assert.That(machine.PresentationKey, Is.EqualTo("F001-MACHINE-D001-NORMAL"));
            Assert.That(catalog.Resolve(new ResourceId("dev.input")).PresentationKey, Is.EqualTo("F001-RESOURCE-DEV-INPUT"));
            Assert.That(catalog.Resolve(new ResourceId("dev.output")).PresentationKey, Is.Null);
        }
    }
}
