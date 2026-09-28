using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using IdleFactory.Domain;
using IdleFactory.Tests.EditMode.Fixtures;
using IdleFactory.Unity.Content;
using NUnit.Framework;

namespace IdleFactory.Tests.EditMode
{
    public sealed class JsonDevelopmentContentTests
    {
        private const string ContentPath = "Assets/IdleFactory/Content/Development/development-catalog.json";
        private const string GuidePath = "docs/content/json-content-pipeline.md";
        private const string CompleteExampleHeading = "## A complete, valid, annotated example";

        [Test]
        public void PackagedDocumentLoadsAndMatchesAcceptedFixtureFieldForField()
        {
            var result = GameplayContentLoader.Load(File.ReadAllBytes(ContentPath));

            Assert.That(result.IsSuccess, Is.True, Describe(result.Diagnostics));
            AssertCatalogEqual(result.Catalog, AcceptedFoundationCatalogFixture.CreateCatalog());
        }

        [Test]
        public void PackagedDocumentEqualsItsCanonicalRendering()
        {
            var bytes = File.ReadAllBytes(ContentPath);
            var strictUtf8 = new UTF8Encoding(false, true);
            var actual = strictUtf8.GetString(bytes);
            var expected = CanonicalCatalogRenderer.Render(AcceptedFoundationCatalogFixture.CreateCatalog());

            Assert.That(bytes.Take(3), Is.Not.EqualTo(new byte[] { 0xEF, 0xBB, 0xBF }), "UTF-8 BOM is forbidden.");
            Assert.That(actual, Is.EqualTo(expected));
            Assert.That(actual, Does.Not.Contain("\r"));
            Assert.That(actual, Does.EndWith("\n"));
        }

        [Test]
        public void PipelineGuideCompleteExampleValidatesThroughContentPipeline()
        {
            var guide = File.ReadAllText(GuidePath);
            var heading = guide.IndexOf(CompleteExampleHeading, StringComparison.Ordinal);
            Assert.That(heading, Is.GreaterThanOrEqualTo(0), "Complete example heading was not found.");

            var fenceStart = guide.IndexOf("```json", heading, StringComparison.Ordinal);
            Assert.That(fenceStart, Is.GreaterThanOrEqualTo(0), "Complete example JSON fence was not found.");
            var jsonStart = guide.IndexOf('\n', fenceStart) + 1;
            var fenceEnd = guide.IndexOf("```", jsonStart, StringComparison.Ordinal);
            Assert.That(fenceEnd, Is.GreaterThan(jsonStart), "Complete example closing fence was not found.");

            var json = guide.Substring(jsonStart, fenceEnd - jsonStart);
            var result = GameplayContentLoader.Load(new UTF8Encoding(false, true).GetBytes(json));

            Assert.That(result.IsSuccess, Is.True, Describe(result.Diagnostics));
            AssertCatalogEqual(result.Catalog, AcceptedFoundationCatalogFixture.CreateCatalog());
        }

        private static void AssertCatalogEqual(DefinitionCatalog actual, DefinitionCatalog expected)
        {
            Assert.That(actual.Resources.Count, Is.EqualTo(expected.Resources.Count));
            Assert.That(actual.Recipes.Count, Is.EqualTo(expected.Recipes.Count));
            Assert.That(actual.Buildings.Count, Is.EqualTo(expected.Buildings.Count));
            Assert.That(actual.OfflinePolicies.Count, Is.EqualTo(expected.OfflinePolicies.Count));

            AssertResourceEqual(actual.Resolve(AcceptedFoundationCatalogFixture.Input), expected.Resolve(AcceptedFoundationCatalogFixture.Input));
            AssertResourceEqual(actual.Resolve(AcceptedFoundationCatalogFixture.Output), expected.Resolve(AcceptedFoundationCatalogFixture.Output));
            AssertRecipeEqual(actual.Resolve(AcceptedFoundationCatalogFixture.Recipe), expected.Resolve(AcceptedFoundationCatalogFixture.Recipe));
            AssertBuildingEqual(actual.Resolve(AcceptedFoundationCatalogFixture.Building), expected.Resolve(AcceptedFoundationCatalogFixture.Building));
            AssertPolicyEqual(actual.Resolve(AcceptedFoundationCatalogFixture.OfflineRevision), expected.Resolve(AcceptedFoundationCatalogFixture.OfflineRevision));
        }

        private static void AssertResourceEqual(ResourceDefinition actual, ResourceDefinition expected)
        {
            Assert.That(actual.Id, Is.EqualTo(expected.Id));
            Assert.That(actual.DevelopmentLabel, Is.EqualTo(expected.DevelopmentLabel));
            Assert.That(actual.PresentationKey, Is.EqualTo(expected.PresentationKey));
            Assert.That(actual.IsDevelopmentOnly, Is.EqualTo(expected.IsDevelopmentOnly));
        }

        private static void AssertRecipeEqual(RecipeDefinition actual, RecipeDefinition expected)
        {
            Assert.That(actual.Id, Is.EqualTo(expected.Id));
            Assert.That(actual.DurationTicks, Is.EqualTo(expected.DurationTicks));
            Assert.That(actual.IsDevelopmentOnly, Is.EqualTo(expected.IsDevelopmentOnly));
            AssertAmountsEqual(actual.Inputs, expected.Inputs);
            AssertAmountsEqual(actual.Outputs, expected.Outputs);
        }

        private static void AssertAmountsEqual(
            IReadOnlyList<ResourceAmountDefinition> actual,
            IReadOnlyList<ResourceAmountDefinition> expected)
        {
            Assert.That(actual, Has.Count.EqualTo(expected.Count));
            for (var index = 0; index < expected.Count; index++)
            {
                Assert.That(actual[index].ResourceId, Is.EqualTo(expected[index].ResourceId));
                Assert.That(actual[index].Amount, Is.EqualTo(expected[index].Amount));
            }
        }

        private static void AssertBuildingEqual(BuildingDefinition actual, BuildingDefinition expected)
        {
            Assert.That(actual.Id, Is.EqualTo(expected.Id));
            Assert.That(actual.Footprint, Is.EqualTo(expected.Footprint));
            Assert.That(actual.AllowedOrientations, Is.EqualTo(expected.AllowedOrientations));
            Assert.That(actual.RecipeId, Is.EqualTo(expected.RecipeId));
            Assert.That(actual.PresentationKey, Is.EqualTo(expected.PresentationKey));
            Assert.That(actual.IsDevelopmentOnly, Is.EqualTo(expected.IsDevelopmentOnly));
            AssertBufferEqual(actual.InputBuffer, expected.InputBuffer);
            AssertBufferEqual(actual.OutputBuffer, expected.OutputBuffer);
        }

        private static void AssertBufferEqual(BufferDefinition actual, BufferDefinition expected)
        {
            if (expected == null)
            {
                Assert.That(actual, Is.Null);
                return;
            }

            Assert.That(actual, Is.Not.Null);
            Assert.That(actual.Role, Is.EqualTo(expected.Role));
            Assert.That(actual.Capacities.Count, Is.EqualTo(expected.Capacities.Count));
            foreach (var pair in expected.Capacities)
            {
                Assert.That(actual.Capacities[pair.Key], Is.EqualTo(pair.Value));
            }
        }

        private static void AssertPolicyEqual(OfflinePolicyDefinition actual, OfflinePolicyDefinition expected)
        {
            Assert.That(actual.Revision, Is.EqualTo(expected.Revision));
            Assert.That(actual.EfficiencyNumerator, Is.EqualTo(expected.EfficiencyNumerator));
            Assert.That(actual.EfficiencyDenominator, Is.EqualTo(expected.EfficiencyDenominator));
            Assert.That(actual.MaxCreditedTicks, Is.EqualTo(expected.MaxCreditedTicks));
        }

        private static string Describe(IEnumerable<ContentDiagnostic> diagnostics) =>
            string.Join("; ", diagnostics.Select(diagnostic =>
                $"{diagnostic.Code} {diagnostic.Path}: {diagnostic.Message}"));

        private static class CanonicalCatalogRenderer
        {
            public static string Render(DefinitionCatalog catalog)
            {
                var text = new StringBuilder();
                Line(text, 0, "{");
                Line(text, 1, "\"contentSchemaVersion\": 1,");
                RenderResources(text, catalog);
                RenderRecipes(text, catalog);
                RenderBuildings(text, catalog);
                RenderPolicies(text, catalog);
                Line(text, 0, "}");
                return text.ToString();
            }

            private static void RenderResources(StringBuilder text, DefinitionCatalog catalog)
            {
                Line(text, 1, "\"resources\": [");
                var resources = catalog.Resources.Values.OrderBy(resource => resource.Id.Value, StringComparer.Ordinal).ToArray();
                for (var index = 0; index < resources.Length; index++)
                {
                    var resource = resources[index];
                    Line(text, 2, "{");
                    Line(text, 3, $"\"id\": {Quote(resource.Id.Value)},");
                    Line(text, 3, $"\"developmentLabel\": {Quote(resource.DevelopmentLabel)},");
                    if (resource.PresentationKey != null)
                    {
                        Line(text, 3, $"\"presentationKey\": {Quote(resource.PresentationKey)},");
                    }

                    Line(text, 3, $"\"isDevelopmentOnly\": {Boolean(resource.IsDevelopmentOnly)}");
                    Line(text, 2, index + 1 == resources.Length ? "}" : "},");
                }

                Line(text, 1, "],");
            }

            private static void RenderRecipes(StringBuilder text, DefinitionCatalog catalog)
            {
                Line(text, 1, "\"recipes\": [");
                var recipes = catalog.Recipes.Values.OrderBy(recipe => recipe.Id.Value, StringComparer.Ordinal).ToArray();
                for (var index = 0; index < recipes.Length; index++)
                {
                    var recipe = recipes[index];
                    Line(text, 2, "{");
                    Line(text, 3, $"\"id\": {Quote(recipe.Id.Value)},");
                    RenderAmounts(text, "inputs", recipe.Inputs);
                    RenderAmounts(text, "outputs", recipe.Outputs);
                    Line(text, 3, $"\"durationTicks\": {Number(recipe.DurationTicks)},");
                    Line(text, 3, $"\"isDevelopmentOnly\": {Boolean(recipe.IsDevelopmentOnly)}");
                    Line(text, 2, index + 1 == recipes.Length ? "}" : "},");
                }

                Line(text, 1, "],");
            }

            private static void RenderAmounts(
                StringBuilder text,
                string memberName,
                IEnumerable<ResourceAmountDefinition> source)
            {
                Line(text, 3, $"\"{memberName}\": [");
                var amounts = source.OrderBy(amount => amount.ResourceId.Value, StringComparer.Ordinal).ToArray();
                for (var index = 0; index < amounts.Length; index++)
                {
                    var amount = amounts[index];
                    var suffix = index + 1 == amounts.Length ? string.Empty : ",";
                    Line(text, 4, $"{{ \"resourceId\": {Quote(amount.ResourceId.Value)}, \"amount\": {Number(amount.Amount)} }}{suffix}");
                }

                Line(text, 3, "],");
            }

            private static void RenderBuildings(StringBuilder text, DefinitionCatalog catalog)
            {
                Line(text, 1, "\"buildings\": [");
                var buildings = catalog.Buildings.Values.OrderBy(building => building.Id.Value, StringComparer.Ordinal).ToArray();
                for (var index = 0; index < buildings.Length; index++)
                {
                    var building = buildings[index];
                    Line(text, 2, "{");
                    Line(text, 3, $"\"id\": {Quote(building.Id.Value)},");
                    Line(text, 3, $"\"footprintWidth\": {building.Footprint.Width},");
                    Line(text, 3, $"\"footprintHeight\": {building.Footprint.Height},");
                    Line(text, 3, $"\"allowedOrientations\": [{string.Join(", ", building.AllowedOrientations.OrderBy(value => value))}],");
                    if (building.RecipeId.HasValue)
                    {
                        Line(text, 3, $"\"recipeId\": {Quote(building.RecipeId.Value.Value)},");
                    }

                    if (building.InputBuffer != null)
                    {
                        RenderBuffer(text, "inputBuffer", building.InputBuffer);
                    }

                    if (building.OutputBuffer != null)
                    {
                        RenderBuffer(text, "outputBuffer", building.OutputBuffer);
                    }

                    Line(text, 3, $"\"presentationKey\": {Quote(building.PresentationKey)},");
                    Line(text, 3, $"\"isDevelopmentOnly\": {Boolean(building.IsDevelopmentOnly)}");
                    Line(text, 2, index + 1 == buildings.Length ? "}" : "},");
                }

                Line(text, 1, "],");
            }

            private static void RenderBuffer(StringBuilder text, string memberName, BufferDefinition buffer)
            {
                Line(text, 3, $"\"{memberName}\": [");
                var capacities = buffer.Capacities.OrderBy(pair => pair.Key.Value, StringComparer.Ordinal).ToArray();
                for (var index = 0; index < capacities.Length; index++)
                {
                    var capacity = capacities[index];
                    var suffix = index + 1 == capacities.Length ? string.Empty : ",";
                    Line(text, 4, $"{{ \"resourceId\": {Quote(capacity.Key.Value)}, \"capacity\": {Number(capacity.Value)} }}{suffix}");
                }

                Line(text, 3, "],");
            }

            private static void RenderPolicies(StringBuilder text, DefinitionCatalog catalog)
            {
                Line(text, 1, "\"offlinePolicies\": [");
                var policies = catalog.OfflinePolicies.Values.OrderBy(policy => policy.Revision.Value).ToArray();
                for (var index = 0; index < policies.Length; index++)
                {
                    var policy = policies[index];
                    Line(text, 2, "{");
                    Line(text, 3, $"\"revision\": {policy.Revision.Value},");
                    Line(text, 3, $"\"efficiencyNumerator\": {Number(policy.EfficiencyNumerator)},");
                    Line(text, 3, $"\"efficiencyDenominator\": {Number(policy.EfficiencyDenominator)},");
                    Line(text, 3, $"\"maxCreditedTicks\": {Number(policy.MaxCreditedTicks)}");
                    Line(text, 2, index + 1 == policies.Length ? "}" : "},");
                }

                Line(text, 1, "]");
            }

            private static void Line(StringBuilder text, int depth, string value)
            {
                text.Append(' ', depth * 2);
                text.Append(value);
                text.Append('\n');
            }

            private static string Boolean(bool value) => value ? "true" : "false";

            private static string Number(long value) => value.ToString(CultureInfo.InvariantCulture);

            private static string Quote(string value)
            {
                var escaped = new StringBuilder(value.Length + 2);
                escaped.Append('"');
                foreach (var character in value)
                {
                    switch (character)
                    {
                        case '"': escaped.Append("\\\""); break;
                        case '\\': escaped.Append("\\\\"); break;
                        case '\b': escaped.Append("\\b"); break;
                        case '\f': escaped.Append("\\f"); break;
                        case '\n': escaped.Append("\\n"); break;
                        case '\r': escaped.Append("\\r"); break;
                        case '\t': escaped.Append("\\t"); break;
                        default:
                            if (character < 0x20)
                            {
                                escaped.Append("\\u");
                                escaped.Append(((int)character).ToString("x4", CultureInfo.InvariantCulture));
                            }
                            else
                            {
                                escaped.Append(character);
                            }

                            break;
                    }
                }

                escaped.Append('"');
                return escaped.ToString();
            }
        }
    }
}
