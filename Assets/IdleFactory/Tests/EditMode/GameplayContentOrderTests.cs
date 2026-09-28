using System.Collections.Generic;
using System.Linq;
using System.Text;
using IdleFactory.Domain;
using IdleFactory.Unity.Content;
using NUnit.Framework;

namespace IdleFactory.Tests.EditMode
{
    public sealed class GameplayContentOrderTests
    {
        private const string ValidJson = @"{
  ""contentSchemaVersion"": 1,
  ""resources"": [
    { ""id"": ""dev.input"", ""developmentLabel"": ""Input"", ""presentationKey"": ""F001-RESOURCE-DEV-INPUT"", ""isDevelopmentOnly"": true },
    { ""id"": ""dev.output"", ""developmentLabel"": ""Output"", ""isDevelopmentOnly"": true },
    { ""id"": ""dev.aux"", ""developmentLabel"": ""Auxiliary"", ""isDevelopmentOnly"": true },
    { ""id"": ""dev.extra"", ""developmentLabel"": ""Extra"", ""isDevelopmentOnly"": true }
  ],
  ""recipes"": [
    {
      ""id"": ""dev.recipe.basic"",
      ""inputs"": [ { ""resourceId"": ""dev.input"", ""amount"": 2 }, { ""resourceId"": ""dev.aux"", ""amount"": 5 } ],
      ""outputs"": [ { ""resourceId"": ""dev.output"", ""amount"": 1 }, { ""resourceId"": ""dev.extra"", ""amount"": 2 } ],
      ""durationTicks"": 50000000,
      ""isDevelopmentOnly"": true
    }
  ],
  ""buildings"": [
    {
      ""id"": ""dev.machine.basic"",
      ""footprintWidth"": 2,
      ""footprintHeight"": 2,
      ""allowedOrientations"": [2, 0],
      ""recipeId"": ""dev.recipe.basic"",
      ""inputBuffer"": [ { ""resourceId"": ""dev.input"", ""capacity"": 4 }, { ""resourceId"": ""dev.aux"", ""capacity"": 6 } ],
      ""outputBuffer"": [ { ""resourceId"": ""dev.output"", ""capacity"": 1 }, { ""resourceId"": ""dev.extra"", ""capacity"": 2 } ],
      ""presentationKey"": ""F001-MACHINE-D001-NORMAL"",
      ""isDevelopmentOnly"": true
    }
  ],
  ""offlinePolicies"": [
    { ""revision"": 1, ""efficiencyNumerator"": 4, ""efficiencyDenominator"": 5, ""maxCreditedTicks"": 288000000000 }
  ]
}";

        [Test]
        public void ReorderingDefinitionFamiliesDoesNotChangeCatalogMeaning()
        {
            var document = ReadValidDocument();
            AddSecondDefinitionSet(document);
            var reordered = ReadValidDocument();
            AddSecondDefinitionSet(reordered);
            reordered.Resources.Reverse();
            reordered.Recipes.Reverse();
            reordered.Buildings.Reverse();
            foreach (var recipe in reordered.Recipes)
            {
                recipe.Inputs.Reverse();
                recipe.Outputs.Reverse();
            }

            foreach (var building in reordered.Buildings)
            {
                building.AllowedOrientations.Reverse();
                building.InputBuffer?.Reverse();
                building.OutputBuffer?.Reverse();
            }

            var first = JsonDefinitionCatalogBuilder.Build(document);
            var second = JsonDefinitionCatalogBuilder.Build(reordered);

            Assert.That(first.IsSuccess, Is.True, Describe(first.Diagnostics));
            Assert.That(second.IsSuccess, Is.True, Describe(second.Diagnostics));
            AssertCatalogEquivalent(first.Catalog, second.Catalog);
        }

        private static void AddSecondDefinitionSet(GameplayContentDocumentDto document)
        {
            document.Resources.Add(new ResourceDto
            {
                Id = "dev.tertiary",
                DevelopmentLabel = "Tertiary",
                IsDevelopmentOnly = true,
            });
            document.Recipes.Add(new RecipeDto
            {
                Id = "dev.recipe.secondary",
                Inputs = new List<ResourceAmountDto>
                {
                    new ResourceAmountDto { ResourceId = "dev.output", Amount = 3 },
                    new ResourceAmountDto { ResourceId = "dev.input", Amount = 2 },
                },
                Outputs = new List<ResourceAmountDto>
                {
                    new ResourceAmountDto { ResourceId = "dev.tertiary", Amount = 1 },
                    new ResourceAmountDto { ResourceId = "dev.extra", Amount = 2 },
                },
                DurationTicks = 100000000,
                IsDevelopmentOnly = true,
            });
            document.Buildings.Add(new BuildingDto
            {
                Id = "dev.machine.secondary",
                FootprintWidth = 1,
                FootprintHeight = 2,
                AllowedOrientations = new List<int> { 0, 2 },
                RecipeId = "dev.recipe.secondary",
                InputBuffer = new List<ResourceCapacityDto>
                {
                    new ResourceCapacityDto { ResourceId = "dev.output", Capacity = 5 },
                    new ResourceCapacityDto { ResourceId = "dev.input", Capacity = 4 },
                },
                OutputBuffer = new List<ResourceCapacityDto>
                {
                    new ResourceCapacityDto { ResourceId = "dev.tertiary", Capacity = 2 },
                    new ResourceCapacityDto { ResourceId = "dev.extra", Capacity = 3 },
                },
                PresentationKey = "F001-MACHINE-D002-NORMAL",
                IsDevelopmentOnly = true,
            });
        }

        private static GameplayContentDocumentDto ReadValidDocument()
        {
            var result = GameplayContentReader.Read(Encoding.UTF8.GetBytes(ValidJson));
            Assert.That(result.IsSuccess, Is.True, result.Diagnostic == null ? string.Empty : result.Diagnostic.Message);
            return result.Document;
        }

        private static void AssertCatalogEquivalent(DefinitionCatalog expected, DefinitionCatalog actual)
        {
            Assert.That(expected.Resources.Keys.Select(id => id.Value).OrderBy(id => id, System.StringComparer.Ordinal),
                Is.EqualTo(actual.Resources.Keys.Select(id => id.Value).OrderBy(id => id, System.StringComparer.Ordinal)));
            Assert.That(expected.Recipes.Keys.Select(id => id.Value).OrderBy(id => id, System.StringComparer.Ordinal),
                Is.EqualTo(actual.Recipes.Keys.Select(id => id.Value).OrderBy(id => id, System.StringComparer.Ordinal)));
            Assert.That(expected.Buildings.Keys.Select(id => id.Value).OrderBy(id => id, System.StringComparer.Ordinal),
                Is.EqualTo(actual.Buildings.Keys.Select(id => id.Value).OrderBy(id => id, System.StringComparer.Ordinal)));

            foreach (var pair in expected.Resources)
            {
                var right = actual.Resolve(pair.Key);
                Assert.That(right.DevelopmentLabel, Is.EqualTo(pair.Value.DevelopmentLabel));
                Assert.That(right.PresentationKey, Is.EqualTo(pair.Value.PresentationKey));
                Assert.That(right.IsDevelopmentOnly, Is.EqualTo(pair.Value.IsDevelopmentOnly));
            }

            foreach (var pair in expected.Recipes)
            {
                var right = actual.Resolve(pair.Key);
                Assert.That(right.DurationTicks, Is.EqualTo(pair.Value.DurationTicks));
                Assert.That(right.IsDevelopmentOnly, Is.EqualTo(pair.Value.IsDevelopmentOnly));
                AssertAmounts(right.Inputs, pair.Value.Inputs);
                AssertAmounts(right.Outputs, pair.Value.Outputs);
            }

            foreach (var pair in expected.Buildings)
            {
                var right = actual.Resolve(pair.Key);
                Assert.That(right.Footprint, Is.EqualTo(pair.Value.Footprint));
                Assert.That(right.AllowedOrientations, Is.EqualTo(pair.Value.AllowedOrientations));
                Assert.That(right.RecipeId, Is.EqualTo(pair.Value.RecipeId));
                Assert.That(right.PresentationKey, Is.EqualTo(pair.Value.PresentationKey));
                Assert.That(right.IsDevelopmentOnly, Is.EqualTo(pair.Value.IsDevelopmentOnly));
                AssertBuffer(right.InputBuffer, pair.Value.InputBuffer);
                AssertBuffer(right.OutputBuffer, pair.Value.OutputBuffer);
            }

            var expectedPolicy = expected.OfflinePolicies.Values.Single();
            var actualPolicy = actual.OfflinePolicies.Values.Single();
            Assert.That(actualPolicy.Revision, Is.EqualTo(expectedPolicy.Revision));
            Assert.That(actualPolicy.EfficiencyNumerator, Is.EqualTo(expectedPolicy.EfficiencyNumerator));
            Assert.That(actualPolicy.EfficiencyDenominator, Is.EqualTo(expectedPolicy.EfficiencyDenominator));
            Assert.That(actualPolicy.MaxCreditedTicks, Is.EqualTo(expectedPolicy.MaxCreditedTicks));
        }

        private static void AssertAmounts(
            IReadOnlyList<ResourceAmountDefinition> actual,
            IReadOnlyList<ResourceAmountDefinition> expected)
        {
            Assert.That(actual.Select(amount => new { amount.ResourceId, amount.Amount }),
                Is.EqualTo(expected.Select(amount => new { amount.ResourceId, amount.Amount })));
        }

        private static void AssertBuffer(BufferDefinition actual, BufferDefinition expected)
        {
            if (expected == null)
            {
                Assert.That(actual, Is.Null);
                return;
            }

            Assert.That(actual, Is.Not.Null);
            Assert.That(actual.Role, Is.EqualTo(expected.Role));
            Assert.That(actual.Capacities.Count, Is.EqualTo(expected.Capacities.Count));
            Assert.That(actual.Capacities.Keys.Select(id => id.Value),
                Is.EqualTo(expected.Capacities.Keys.Select(id => id.Value)));
            foreach (var pair in expected.Capacities)
            {
                Assert.That(actual.Capacities[pair.Key], Is.EqualTo(pair.Value));
            }
        }

        private static string Describe(IEnumerable<ContentDiagnostic> diagnostics) =>
            string.Join("; ", diagnostics.Select(diagnostic => $"{diagnostic.Code} {diagnostic.Path}: {diagnostic.Message}"));
    }
}
