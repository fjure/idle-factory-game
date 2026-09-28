using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using IdleFactory.Domain;
using IdleFactory.Unity.Content;
using NUnit.Framework;

namespace IdleFactory.Tests.EditMode
{
    public sealed class GameplayContentValidatorTests
    {
        private const string ValidJson = @"{
  ""contentSchemaVersion"": 1,
  ""resources"": [
    { ""id"": ""dev.input"", ""developmentLabel"": ""Input"", ""presentationKey"": ""F001-RESOURCE-DEV-INPUT"", ""isDevelopmentOnly"": true },
    { ""id"": ""dev.output"", ""developmentLabel"": ""Output"", ""isDevelopmentOnly"": true }
  ],
  ""recipes"": [
    {
      ""id"": ""dev.recipe.basic"",
      ""inputs"": [ { ""resourceId"": ""dev.input"", ""amount"": 2 } ],
      ""outputs"": [ { ""resourceId"": ""dev.output"", ""amount"": 1 } ],
      ""durationTicks"": 50000000,
      ""isDevelopmentOnly"": true
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
      ""isDevelopmentOnly"": true
    }
  ],
  ""offlinePolicies"": [
    { ""revision"": 1, ""efficiencyNumerator"": 4, ""efficiencyDenominator"": 5, ""maxCreditedTicks"": 288000000000 }
  ]
}";

        private static readonly string[] SemanticCodes =
        {
            "id.empty",
            "id.too-long",
            "id.not-canonical",
            "id.duplicate",
            "ref.resource.unknown",
            "ref.recipe.unknown",
            "recipe.amount.non-positive",
            "recipe.duration.non-positive",
            "recipe.resource.duplicate",
            "recipe.inputs.empty",
            "recipe.outputs.empty",
            "footprint.non-positive",
            "orientation.invalid",
            "orientation.duplicate",
            "orientation.empty",
            "buffer.capacity.negative",
            "buffer.resource.duplicate",
            "buffer.empty",
            "buffer.missing-recipe-resource",
            "building.producing-requires-buffers",
            "policy.revision.non-positive",
            "policy.revision.duplicate",
            "policy.ratio.invalid",
            "policy.cap.non-positive",
            "policy.revision-1.required",
        };

        private static readonly string[] DomainParityCodes =
        {
            "id.empty",
            "id.duplicate",
            "ref.resource.unknown",
            "ref.recipe.unknown",
            "recipe.amount.non-positive",
            "recipe.duration.non-positive",
            "recipe.resource.duplicate",
            "recipe.inputs.empty",
            "recipe.outputs.empty",
            "footprint.non-positive",
            "orientation.invalid",
            "orientation.duplicate",
            "orientation.empty",
            "buffer.capacity.negative",
            "buffer.resource.duplicate",
            "buffer.empty",
            "buffer.missing-recipe-resource",
            "building.producing-requires-buffers",
            "policy.revision.non-positive",
            "policy.revision.duplicate",
            "policy.ratio.invalid",
            "policy.cap.non-positive",
            "policy.revision-1.required",
        };

        [TestCaseSource(nameof(SemanticCodeCases))]
        public void ReportsEveryContractSemanticCode(string code)
        {
            var diagnostics = GameplayContentValidator.Validate(CreateInvalidDocument(code));

            Assert.That(diagnostics.Any(diagnostic => diagnostic.Code == code), Is.True,
                $"Expected semantic diagnostic '{code}', got: {Describe(diagnostics)}");
        }

        [TestCaseSource(nameof(DomainParityCodeCases))]
        public void DomainConstructorRejectsTheSameInvalidGraph(string code)
        {
            var document = CreateInvalidDocument(code);
            var diagnostics = GameplayContentValidator.Validate(document);

            Assert.That(diagnostics.Any(diagnostic => diagnostic.Code == code), Is.True,
                $"Validator did not report '{code}': {Describe(diagnostics)}");
            Assert.That(() => BuildDomainCatalog(document), Throws.Exception,
                $"Core domain unexpectedly accepted the graph for '{code}'.");
        }

        [Test]
        public void DiagnosticsFollowDocumentFamilyAndEntryOrder()
        {
            var document = CreateValidDocument();
            document.Resources[1].Id = document.Resources[0].Id;
            document.Recipes[0].Outputs[0].ResourceId = "dev.input";
            document.Buildings[0].OutputBuffer[0].ResourceId = "dev.input";
            document.Recipes[0].Inputs[0].Amount = 0;
            document.Buildings[0].FootprintWidth = 0;
            document.OfflinePolicies[0].MaxCreditedTicks = 0;

            var diagnostics = GameplayContentValidator.Validate(document);

            Assert.That(
                diagnostics.Select(diagnostic => diagnostic.Code),
                Is.EqualTo(new[]
                {
                    "id.duplicate",
                    "recipe.amount.non-positive",
                    "footprint.non-positive",
                    "policy.cap.non-positive",
                }));
            Assert.That(
                diagnostics.Select(diagnostic => diagnostic.Path),
                Is.EqualTo(new[]
                {
                    "/resources/1/id",
                    "/recipes/0/inputs/0/amount",
                    "/buildings/0/footprintWidth",
                    "/offlinePolicies/0/maxCreditedTicks",
                }));
        }

        [Test]
        public void CapsDiagnosticsAtOneHundredAndMarksTruncationInFinalMessage()
        {
            var document = CreateValidDocument();
            for (var index = 0; index < GameplayContentLimits.MaximumReportedDiagnostics + 5; index++)
            {
                document.Resources.Add(new ResourceDto
                {
                    Id = " ",
                    DevelopmentLabel = "Extra",
                    IsDevelopmentOnly = true,
                });
            }

            var diagnostics = GameplayContentValidator.Validate(document);

            Assert.That(diagnostics.Count, Is.EqualTo(GameplayContentLimits.MaximumReportedDiagnostics));
            Assert.That(diagnostics.All(diagnostic => diagnostic.Code == "id.empty"), Is.True);
            Assert.That(diagnostics[diagnostics.Count - 1].Message, Does.Contain("Additional semantic diagnostics were omitted"));
            Assert.That(diagnostics[diagnostics.Count - 1].Code, Is.EqualTo("id.empty"));
        }

        [Test]
        public void DomainConstructorFailureAfterValidationBecomesInternalDiagnostic()
        {
            var document = CreateValidDocument();
            document.Resources[0].DevelopmentLabel = " ";

            var result = JsonDefinitionCatalogBuilder.Build(document);

            Assert.That(result.IsSuccess, Is.False);
            Assert.That(result.Catalog, Is.Null);
            Assert.That(result.Diagnostics.Single().Code, Is.EqualTo("content.internal"));
        }

        [Test]
        public void BuiltCatalogDoesNotRetainMutableDtoCollections()
        {
            var document = CreateValidDocument();
            var result = JsonDefinitionCatalogBuilder.Build(document);

            Assert.That(result.IsSuccess, Is.True, Describe(result.Diagnostics));
            document.Resources.Clear();
            document.Recipes[0].Inputs.Clear();
            document.Buildings[0].AllowedOrientations.Clear();

            Assert.That(result.Catalog.Resources.Count, Is.EqualTo(2));
            Assert.That(result.Catalog.Resolve(new RecipeId("dev.recipe.basic")).Inputs.Count, Is.EqualTo(1));
            Assert.That(result.Catalog.Resolve(new BuildingDefinitionId("dev.machine.basic")).AllowedOrientations, Is.EqualTo(new[] { 0 }));
        }

        [Test]
        public void LoaderReturnsOnlyLoadedCatalogOrRejectedDiagnostics()
        {
            var loaded = GameplayContentLoader.Load(Bytes(ValidJson));
            var rejected = GameplayContentLoader.Load(new byte[0]);

            Assert.That(loaded.IsSuccess, Is.True, Describe(loaded.Diagnostics));
            Assert.That(loaded.Catalog, Is.Not.Null);
            Assert.That(loaded.Diagnostics, Is.Empty);
            Assert.That(rejected.IsSuccess, Is.False);
            Assert.That(rejected.Catalog, Is.Null);
            Assert.That(rejected.Diagnostics.Single().Code, Is.EqualTo("content.empty"));
        }

        private static IEnumerable<string> SemanticCodeCases() => SemanticCodes;

        private static IEnumerable<string> DomainParityCodeCases() => DomainParityCodes;

        private static GameplayContentDocumentDto CreateInvalidDocument(string code)
        {
            var document = CreateValidDocument();
            switch (code)
            {
                case "id.empty":
                    document.Resources[0].Id = " ";
                    document.Recipes[0].Inputs[0].ResourceId = " ";
                    document.Buildings[0].InputBuffer[0].ResourceId = " ";
                    break;
                case "id.too-long":
                    var longId = new string('r', GameplayContentLimits.MaximumIdentifierLength + 1);
                    document.Resources[0].Id = longId;
                    document.Recipes[0].Inputs[0].ResourceId = longId;
                    document.Buildings[0].InputBuffer[0].ResourceId = longId;
                    break;
                case "id.not-canonical":
                    document.Resources[0].Id = " dev.input ";
                    document.Recipes[0].Inputs[0].ResourceId = " dev.input ";
                    document.Buildings[0].InputBuffer[0].ResourceId = " dev.input ";
                    break;
                case "id.duplicate":
                    document.Resources[1].Id = "dev.input";
                    document.Recipes[0].Outputs[0].ResourceId = "dev.input";
                    document.Buildings[0].OutputBuffer[0].ResourceId = "dev.input";
                    break;
                case "ref.resource.unknown":
                    document.Recipes[0].Inputs[0].ResourceId = "dev.missing";
                    break;
                case "ref.recipe.unknown":
                    document.Buildings[0].RecipeId = "dev.missing";
                    break;
                case "recipe.amount.non-positive":
                    document.Recipes[0].Inputs[0].Amount = 0;
                    break;
                case "recipe.duration.non-positive":
                    document.Recipes[0].DurationTicks = 0;
                    break;
                case "recipe.resource.duplicate":
                    document.Recipes[0].Inputs.Add(new ResourceAmountDto { ResourceId = "dev.input", Amount = 3 });
                    break;
                case "recipe.inputs.empty":
                    document.Recipes[0].Inputs.Clear();
                    break;
                case "recipe.outputs.empty":
                    document.Recipes[0].Outputs.Clear();
                    break;
                case "footprint.non-positive":
                    document.Buildings[0].FootprintWidth = 0;
                    break;
                case "orientation.invalid":
                    document.Buildings[0].AllowedOrientations[0] = 4;
                    break;
                case "orientation.duplicate":
                    document.Buildings[0].AllowedOrientations.Add(0);
                    break;
                case "orientation.empty":
                    document.Buildings[0].AllowedOrientations.Clear();
                    break;
                case "buffer.capacity.negative":
                    document.Buildings[0].InputBuffer[0].Capacity = -1;
                    break;
                case "buffer.resource.duplicate":
                    document.Buildings[0].InputBuffer.Add(new ResourceCapacityDto { ResourceId = "dev.input", Capacity = 5 });
                    break;
                case "buffer.empty":
                    document.Buildings[0].RecipeId = null;
                    document.Buildings[0].InputBuffer.Clear();
                    break;
                case "buffer.missing-recipe-resource":
                    document.Buildings[0].InputBuffer[0].ResourceId = "dev.output";
                    break;
                case "building.producing-requires-buffers":
                    document.Buildings[0].InputBuffer = null;
                    break;
                case "policy.revision.non-positive":
                    document.OfflinePolicies[0].Revision = 0;
                    break;
                case "policy.revision.duplicate":
                    document.OfflinePolicies.Add(new OfflinePolicyDto
                    {
                        Revision = 1,
                        EfficiencyNumerator = 4,
                        EfficiencyDenominator = 5,
                        MaxCreditedTicks = 288000000000L,
                    });
                    break;
                case "policy.ratio.invalid":
                    document.OfflinePolicies[0].EfficiencyNumerator = 5;
                    break;
                case "policy.cap.non-positive":
                    document.OfflinePolicies[0].MaxCreditedTicks = 0;
                    break;
                case "policy.revision-1.required":
                    document.OfflinePolicies[0].Revision = 2;
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(code), code, "Unknown semantic test case.");
            }

            return document;
        }

        private static GameplayContentDocumentDto CreateValidDocument()
        {
            var read = GameplayContentReader.Read(Bytes(ValidJson));
            Assert.That(read.IsSuccess, Is.True, read.Diagnostic == null ? string.Empty : read.Diagnostic.Message);
            return read.Document;
        }

        private static DefinitionCatalog BuildDomainCatalog(GameplayContentDocumentDto document)
        {
            var resources = document.Resources.ConvertAll(dto => new ResourceDefinition(
                new ResourceId(dto.Id),
                dto.DevelopmentLabel,
                dto.PresentationKey,
                dto.IsDevelopmentOnly));

            var recipes = document.Recipes.ConvertAll(dto => new RecipeDefinition(
                new RecipeId(dto.Id),
                dto.Inputs.ConvertAll(amount => new ResourceAmountDefinition(new ResourceId(amount.ResourceId), amount.Amount)),
                dto.Outputs.ConvertAll(amount => new ResourceAmountDefinition(new ResourceId(amount.ResourceId), amount.Amount)),
                dto.DurationTicks,
                dto.IsDevelopmentOnly));

            var buildings = document.Buildings.ConvertAll(dto => new BuildingDefinition(
                new BuildingDefinitionId(dto.Id),
                new Footprint(dto.FootprintWidth, dto.FootprintHeight),
                dto.AllowedOrientations,
                dto.RecipeId == null ? (RecipeId?)null : new RecipeId(dto.RecipeId),
                BuildDomainBuffer(dto.InputBuffer, BufferRole.Input),
                BuildDomainBuffer(dto.OutputBuffer, BufferRole.Output),
                dto.PresentationKey,
                dto.IsDevelopmentOnly));

            var policies = document.OfflinePolicies.ConvertAll(dto => new OfflinePolicyDefinition(
                new OfflinePolicyRevision(dto.Revision),
                dto.EfficiencyNumerator,
                dto.EfficiencyDenominator,
                dto.MaxCreditedTicks));

            return new DefinitionCatalog(resources, recipes, buildings, policies);
        }

        private static BufferDefinition BuildDomainBuffer(IList<ResourceCapacityDto> source, BufferRole role)
        {
            if (source == null)
            {
                return null;
            }

            return new BufferDefinition(
                role,
                source.Select(capacity =>
                    new KeyValuePair<ResourceId, long>(new ResourceId(capacity.ResourceId), capacity.Capacity)));
        }

        private static string Describe(IEnumerable<ContentDiagnostic> diagnostics) =>
            string.Join("; ", diagnostics.Select(diagnostic => $"{diagnostic.Code} {diagnostic.Path}: {diagnostic.Message}"));

        private static byte[] Bytes(string json) => Encoding.UTF8.GetBytes(json);
    }
}
