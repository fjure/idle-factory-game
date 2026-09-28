using System;
using System.Collections.Generic;
using IdleFactory.Domain;

namespace IdleFactory.Unity.Content
{
    /// <summary>
    /// Maps a semantically valid DTO graph into independent immutable Core definitions.
    /// </summary>
    public static class JsonDefinitionCatalogBuilder
    {
        public static ContentLoadResult Build(GameplayContentDocumentDto document)
        {
            var diagnostics = GameplayContentValidator.Validate(document);
            if (diagnostics.Count > 0)
            {
                return ContentLoadResult.Rejected(diagnostics);
            }

            try
            {
                var resources = new List<ResourceDefinition>(document.Resources.Count);
                foreach (var dto in document.Resources)
                {
                    resources.Add(new ResourceDefinition(
                        new ResourceId(dto.Id),
                        dto.DevelopmentLabel,
                        dto.PresentationKey,
                        dto.IsDevelopmentOnly));
                }

                var recipes = new List<RecipeDefinition>(document.Recipes.Count);
                foreach (var dto in document.Recipes)
                {
                    recipes.Add(new RecipeDefinition(
                        new RecipeId(dto.Id),
                        BuildAmounts(dto.Inputs),
                        BuildAmounts(dto.Outputs),
                        dto.DurationTicks,
                        dto.IsDevelopmentOnly));
                }

                var buildings = new List<BuildingDefinition>(document.Buildings.Count);
                foreach (var dto in document.Buildings)
                {
                    RecipeId? recipeId = dto.RecipeId == null ? (RecipeId?)null : new RecipeId(dto.RecipeId);
                    buildings.Add(new BuildingDefinition(
                        new BuildingDefinitionId(dto.Id),
                        new Footprint(dto.FootprintWidth, dto.FootprintHeight),
                        dto.AllowedOrientations,
                        recipeId,
                        BuildBuffer(dto.InputBuffer, BufferRole.Input),
                        BuildBuffer(dto.OutputBuffer, BufferRole.Output),
                        dto.PresentationKey,
                        dto.IsDevelopmentOnly));
                }

                var policies = new List<OfflinePolicyDefinition>(document.OfflinePolicies.Count);
                foreach (var dto in document.OfflinePolicies)
                {
                    policies.Add(new OfflinePolicyDefinition(
                        new OfflinePolicyRevision(dto.Revision),
                        dto.EfficiencyNumerator,
                        dto.EfficiencyDenominator,
                        dto.MaxCreditedTicks));
                }

                return ContentLoadResult.Loaded(new DefinitionCatalog(resources, recipes, buildings, policies));
            }
            catch (Exception exception)
            {
                return ContentLoadResult.Rejected(new ContentDiagnostic(
                    "content.internal",
                    string.Empty,
                    null,
                    null,
                    null,
                    "Domain catalog construction failed after semantic validation: " + OneLine(exception.Message)));
            }
        }

        private static List<ResourceAmountDefinition> BuildAmounts(IList<ResourceAmountDto> source)
        {
            var ordered = new List<ResourceAmountDto>(source);
            ordered.Sort((left, right) => StringComparer.Ordinal.Compare(left.ResourceId, right.ResourceId));

            var result = new List<ResourceAmountDefinition>(ordered.Count);
            foreach (var dto in ordered)
            {
                result.Add(new ResourceAmountDefinition(new ResourceId(dto.ResourceId), dto.Amount));
            }

            return result;
        }

        private static BufferDefinition BuildBuffer(IList<ResourceCapacityDto> source, BufferRole role)
        {
            if (source == null)
            {
                return null;
            }

            var ordered = new List<ResourceCapacityDto>(source);
            ordered.Sort((left, right) => StringComparer.Ordinal.Compare(left.ResourceId, right.ResourceId));

            var capacities = new List<KeyValuePair<ResourceId, long>>(ordered.Count);
            foreach (var dto in ordered)
            {
                capacities.Add(new KeyValuePair<ResourceId, long>(
                    new ResourceId(dto.ResourceId),
                    dto.Capacity));
            }

            return new BufferDefinition(role, capacities);
        }

        private static string OneLine(string message) =>
            (message ?? "Unexpected domain validation failure.")
                .Replace("\r", " ")
                .Replace("\n", " ");
    }
}
