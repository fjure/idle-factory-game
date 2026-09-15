using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace IdleFactory.Domain
{
    public sealed class DefinitionCatalog
    {
        public const int SupportedOfflinePolicyRevision = 1;
        public const long SupportedEfficiencyNumerator = 4;
        public const long SupportedEfficiencyDenominator = 5;
        public const long SupportedMaxCreditedTicks = 288000000000L;

        public DefinitionCatalog(
            IEnumerable<ResourceDefinition> resources,
            IEnumerable<RecipeDefinition> recipes,
            IEnumerable<BuildingDefinition> buildings,
            IEnumerable<OfflinePolicyDefinition> offlinePolicies)
        {
            var resourceIndex = BuildUniqueIndex(resources, definition => definition.Id, id => _ = id.Value, nameof(resources));
            var recipeIndex = BuildUniqueIndex(recipes, definition => definition.Id, id => _ = id.Value, nameof(recipes));
            var buildingIndex = BuildUniqueIndex(buildings, definition => definition.Id, id => _ = id.Value, nameof(buildings));
            var policyIndex = BuildUniqueIndex(
                offlinePolicies,
                definition => definition.Revision,
                revision => _ = revision.Value,
                nameof(offlinePolicies));

            ValidateSupportedPolicy(policyIndex);
            ValidateGraph(resourceIndex, recipeIndex, buildingIndex);

            Resources = new ReadOnlyDictionary<ResourceId, ResourceDefinition>(resourceIndex);
            Recipes = new ReadOnlyDictionary<RecipeId, RecipeDefinition>(recipeIndex);
            Buildings = new ReadOnlyDictionary<BuildingDefinitionId, BuildingDefinition>(buildingIndex);
            OfflinePolicies = new ReadOnlyDictionary<OfflinePolicyRevision, OfflinePolicyDefinition>(policyIndex);
        }

        public IReadOnlyDictionary<ResourceId, ResourceDefinition> Resources { get; }
        public IReadOnlyDictionary<RecipeId, RecipeDefinition> Recipes { get; }
        public IReadOnlyDictionary<BuildingDefinitionId, BuildingDefinition> Buildings { get; }
        public IReadOnlyDictionary<OfflinePolicyRevision, OfflinePolicyDefinition> OfflinePolicies { get; }

        public ResourceDefinition Resolve(ResourceId id)
        {
            _ = id.Value;
            ResourceDefinition definition;
            if (!Resources.TryGetValue(id, out definition))
            {
                throw new KeyNotFoundException($"Unknown resource definition '{id.Value}'.");
            }

            return definition;
        }

        public RecipeDefinition Resolve(RecipeId id)
        {
            _ = id.Value;
            RecipeDefinition definition;
            if (!Recipes.TryGetValue(id, out definition))
            {
                throw new KeyNotFoundException($"Unknown recipe definition '{id.Value}'.");
            }

            return definition;
        }

        public BuildingDefinition Resolve(BuildingDefinitionId id)
        {
            _ = id.Value;
            BuildingDefinition definition;
            if (!Buildings.TryGetValue(id, out definition))
            {
                throw new KeyNotFoundException($"Unknown building definition '{id.Value}'.");
            }

            return definition;
        }

        public OfflinePolicyDefinition Resolve(OfflinePolicyRevision revision)
        {
            _ = revision.Value;
            OfflinePolicyDefinition definition;
            if (!OfflinePolicies.TryGetValue(revision, out definition))
            {
                throw new KeyNotFoundException($"Unknown offline policy revision '{revision.Value}'.");
            }

            return definition;
        }

        private static Dictionary<TKey, TDefinition> BuildUniqueIndex<TKey, TDefinition>(
            IEnumerable<TDefinition> definitions,
            Func<TDefinition, TKey> getKey,
            Action<TKey> validateKey,
            string parameterName)
            where TDefinition : class
        {
            if (definitions == null)
            {
                throw new ArgumentNullException(parameterName);
            }

            var index = new Dictionary<TKey, TDefinition>();
            foreach (var definition in definitions)
            {
                if (definition == null)
                {
                    throw new ArgumentException("Definition collections must not contain null.", parameterName);
                }

                var key = getKey(definition);
                validateKey(key);
                if (index.ContainsKey(key))
                {
                    throw new ArgumentException("Definition IDs must be unique after normalization.", parameterName);
                }

                index.Add(key, definition);
            }

            return index;
        }

        private static void ValidateSupportedPolicy(
            IDictionary<OfflinePolicyRevision, OfflinePolicyDefinition> policies)
        {
            if (policies.Count != 1)
            {
                throw new ArgumentException("Feature 001 requires exactly offline policy revision 1.", nameof(policies));
            }

            OfflinePolicyDefinition policy;
            var revision = new OfflinePolicyRevision(SupportedOfflinePolicyRevision);
            if (!policies.TryGetValue(revision, out policy) ||
                policy.EfficiencyNumerator != SupportedEfficiencyNumerator ||
                policy.EfficiencyDenominator != SupportedEfficiencyDenominator ||
                policy.MaxCreditedTicks != SupportedMaxCreditedTicks)
            {
                throw new ArgumentException(
                    "Feature 001 supports only offline policy revision 1 with 4/5 efficiency and an eight-hour cap.",
                    nameof(policies));
            }
        }

        private static void ValidateGraph(
            IDictionary<ResourceId, ResourceDefinition> resources,
            IDictionary<RecipeId, RecipeDefinition> recipes,
            IDictionary<BuildingDefinitionId, BuildingDefinition> buildings)
        {
            foreach (var recipe in recipes.Values)
            {
                EnsureRecipeResourcesExist(recipe.Inputs, resources, recipe.Id, "input");
                EnsureRecipeResourcesExist(recipe.Outputs, resources, recipe.Id, "output");
            }

            foreach (var building in buildings.Values)
            {
                EnsureBufferResourcesExist(building.InputBuffer, resources, building.Id);
                EnsureBufferResourcesExist(building.OutputBuffer, resources, building.Id);

                if (!building.RecipeId.HasValue)
                {
                    continue;
                }

                RecipeDefinition recipe;
                if (!recipes.TryGetValue(building.RecipeId.Value, out recipe))
                {
                    throw new ArgumentException(
                        $"Building '{building.Id.Value}' references unknown recipe '{building.RecipeId.Value.Value}'.",
                        nameof(buildings));
                }

                if (building.InputBuffer == null || building.OutputBuffer == null)
                {
                    throw new ArgumentException(
                        $"Producing building '{building.Id.Value}' requires both input and output buffers.",
                        nameof(buildings));
                }

                EnsureBufferCoversRecipe(building.InputBuffer, recipe.Inputs, building.Id, "input");
                EnsureBufferCoversRecipe(building.OutputBuffer, recipe.Outputs, building.Id, "output");
            }
        }

        private static void EnsureRecipeResourcesExist(
            IEnumerable<ResourceAmountDefinition> amounts,
            IDictionary<ResourceId, ResourceDefinition> resources,
            RecipeId recipeId,
            string side)
        {
            foreach (var amount in amounts)
            {
                if (!resources.ContainsKey(amount.ResourceId))
                {
                    throw new ArgumentException(
                        $"Recipe '{recipeId.Value}' has unknown {side} resource '{amount.ResourceId.Value}'.");
                }
            }
        }

        private static void EnsureBufferResourcesExist(
            BufferDefinition buffer,
            IDictionary<ResourceId, ResourceDefinition> resources,
            BuildingDefinitionId buildingId)
        {
            if (buffer == null)
            {
                return;
            }

            foreach (var resourceId in buffer.Capacities.Keys)
            {
                if (!resources.ContainsKey(resourceId))
                {
                    throw new ArgumentException(
                        $"Building '{buildingId.Value}' buffer references unknown resource '{resourceId.Value}'.");
                }
            }
        }

        private static void EnsureBufferCoversRecipe(
            BufferDefinition buffer,
            IEnumerable<ResourceAmountDefinition> amounts,
            BuildingDefinitionId buildingId,
            string side)
        {
            foreach (var amount in amounts)
            {
                if (!buffer.Capacities.ContainsKey(amount.ResourceId))
                {
                    throw new ArgumentException(
                        $"Building '{buildingId.Value}' {side} buffer does not declare recipe resource '{amount.ResourceId.Value}'.");
                }
            }
        }
    }
}
