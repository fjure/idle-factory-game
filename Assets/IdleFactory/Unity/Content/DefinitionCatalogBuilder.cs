using System;
using System.Collections.Generic;
using IdleFactory.Domain;
using UnityEngine;

namespace IdleFactory.Unity.Content
{
    public static class DefinitionCatalogBuilder
    {
        public static DefinitionCatalog Build(DefinitionCatalogAsset source)
        {
            RequireAlive(source, nameof(source));

            var resourceAssets = SnapshotAssets(source.Resources, "catalog resources");
            var recipeAssets = SnapshotAssets(source.Recipes, "catalog recipes");
            var buildingAssets = SnapshotAssets(source.Buildings, "catalog buildings");
            var policyAssets = SnapshotAssets(source.OfflinePolicies, "catalog offline policies");

            var resourceSnapshots = SnapshotResources(resourceAssets);
            var recipeSnapshots = SnapshotRecipes(recipeAssets);
            var buildingSnapshots = SnapshotBuildings(buildingAssets);
            var policySnapshots = SnapshotPolicies(policyAssets);

            var resourcesByAsset = new Dictionary<ResourceDefinitionAsset, ResourceDefinition>();
            var resources = new List<ResourceDefinition>(resourceSnapshots.Length);
            foreach (var snapshot in resourceSnapshots)
            {
                var definition = new ResourceDefinition(
                    new ResourceId(snapshot.Id),
                    snapshot.DevelopmentLabel,
                    snapshot.PresentationKey,
                    snapshot.IsDevelopmentOnly);
                AddRegistered(resourcesByAsset, snapshot.Asset, definition, "resource");
                resources.Add(definition);
            }

            var recipesByAsset = new Dictionary<RecipeDefinitionAsset, RecipeDefinition>();
            var recipes = new List<RecipeDefinition>(recipeSnapshots.Length);
            foreach (var snapshot in recipeSnapshots)
            {
                var inputs = BuildAmounts(snapshot.Inputs, resourcesByAsset, "recipe input");
                var outputs = BuildAmounts(snapshot.Outputs, resourcesByAsset, "recipe output");
                var definition = new RecipeDefinition(
                    new RecipeId(snapshot.Id),
                    inputs,
                    outputs,
                    snapshot.DurationTicks,
                    snapshot.IsDevelopmentOnly);
                AddRegistered(recipesByAsset, snapshot.Asset, definition, "recipe");
                recipes.Add(definition);
            }

            var policies = new List<OfflinePolicyDefinition>(policySnapshots.Length);
            foreach (var snapshot in policySnapshots)
            {
                policies.Add(new OfflinePolicyDefinition(
                    new OfflinePolicyRevision(snapshot.Revision),
                    snapshot.EfficiencyNumerator,
                    snapshot.EfficiencyDenominator,
                    snapshot.MaxCreditedTicks));
            }

            var buildings = new List<BuildingDefinition>(buildingSnapshots.Length);
            foreach (var snapshot in buildingSnapshots)
            {
                RecipeId? recipeId = null;
                if (snapshot.Recipe != null)
                {
                    recipeId = GetRegistered(recipesByAsset, snapshot.Recipe, "building recipe").Id;
                }

                var inputBuffer = BuildBuffer(snapshot.InputBuffer, BufferRole.Input, resourcesByAsset);
                var outputBuffer = BuildBuffer(snapshot.OutputBuffer, BufferRole.Output, resourcesByAsset);
                buildings.Add(new BuildingDefinition(
                    new BuildingDefinitionId(snapshot.Id),
                    new Footprint(snapshot.FootprintWidth, snapshot.FootprintHeight),
                    snapshot.AllowedOrientations,
                    recipeId,
                    inputBuffer,
                    outputBuffer,
                    snapshot.PresentationKey,
                    snapshot.IsDevelopmentOnly));
            }

            return new DefinitionCatalog(resources, recipes, buildings, policies);
        }

        private static ResourceSnapshot[] SnapshotResources(ResourceDefinitionAsset[] assets)
        {
            var snapshots = new ResourceSnapshot[assets.Length];
            for (var index = 0; index < assets.Length; index++)
            {
                var asset = assets[index];
                snapshots[index] = new ResourceSnapshot(
                    asset,
                    asset.Id,
                    asset.DevelopmentLabel,
                    asset.PresentationKey,
                    asset.IsDevelopmentOnly);
            }

            return snapshots;
        }

        private static RecipeSnapshot[] SnapshotRecipes(RecipeDefinitionAsset[] assets)
        {
            var snapshots = new RecipeSnapshot[assets.Length];
            for (var index = 0; index < assets.Length; index++)
            {
                var asset = assets[index];
                snapshots[index] = new RecipeSnapshot(
                    asset,
                    asset.Id,
                    SnapshotAmounts(asset.Inputs, "recipe inputs"),
                    SnapshotAmounts(asset.Outputs, "recipe outputs"),
                    asset.DurationTicks,
                    asset.IsDevelopmentOnly);
            }

            return snapshots;
        }

        private static BuildingSnapshot[] SnapshotBuildings(BuildingDefinitionAsset[] assets)
        {
            var snapshots = new BuildingSnapshot[assets.Length];
            for (var index = 0; index < assets.Length; index++)
            {
                var asset = assets[index];
                var recipe = asset.Recipe;
                if (!ReferenceEquals(recipe, null))
                {
                    RequireAlive(recipe, "building recipe");
                }

                snapshots[index] = new BuildingSnapshot(
                    asset.Id,
                    asset.FootprintWidth,
                    asset.FootprintHeight,
                    SnapshotValues(asset.AllowedOrientations, "building orientations"),
                    recipe,
                    SnapshotBuffer(asset.HasInputBuffer, asset.InputCapacities, "input buffer"),
                    SnapshotBuffer(asset.HasOutputBuffer, asset.OutputCapacities, "output buffer"),
                    asset.PresentationKey,
                    asset.IsDevelopmentOnly);
            }

            return snapshots;
        }

        private static PolicySnapshot[] SnapshotPolicies(OfflinePolicyAsset[] assets)
        {
            var snapshots = new PolicySnapshot[assets.Length];
            for (var index = 0; index < assets.Length; index++)
            {
                var asset = assets[index];
                snapshots[index] = new PolicySnapshot(
                    asset.Revision,
                    asset.EfficiencyNumerator,
                    asset.EfficiencyDenominator,
                    asset.MaxCreditedTicks);
            }

            return snapshots;
        }

        private static AmountSnapshot[] SnapshotAmounts(
            List<RecipeDefinitionAsset.ResourceAmountRecord> records,
            string label)
        {
            if (records == null)
            {
                throw new ArgumentException($"{label} collection is missing.", label);
            }

            var snapshots = new AmountSnapshot[records.Count];
            for (var index = 0; index < records.Count; index++)
            {
                var record = records[index];
                if (record == null)
                {
                    throw new ArgumentException($"{label} contains a null entry.", label);
                }

                RequireAlive(record.Resource, $"{label} resource");
                snapshots[index] = new AmountSnapshot(record.Resource, record.Amount);
            }

            return snapshots;
        }

        private static BufferSnapshot SnapshotBuffer(
            bool isPresent,
            List<BuildingDefinitionAsset.ResourceCapacityRecord> records,
            string label)
        {
            if (!isPresent)
            {
                if (records != null && records.Count != 0)
                {
                    throw new ArgumentException($"{label} has entries while its presence flag is false.", label);
                }

                return null;
            }

            if (records == null)
            {
                throw new ArgumentException($"{label} collection is missing while present.", label);
            }

            var capacities = new CapacitySnapshot[records.Count];
            for (var index = 0; index < records.Count; index++)
            {
                var record = records[index];
                if (record == null)
                {
                    throw new ArgumentException($"{label} contains a null entry.", label);
                }

                RequireAlive(record.Resource, $"{label} resource");
                capacities[index] = new CapacitySnapshot(record.Resource, record.Capacity);
            }

            return new BufferSnapshot(capacities);
        }

        private static T[] SnapshotAssets<T>(List<T> assets, string label)
            where T : UnityEngine.Object
        {
            if (assets == null)
            {
                throw new ArgumentException($"{label} collection is missing.", label);
            }

            var snapshot = new T[assets.Count];
            for (var index = 0; index < assets.Count; index++)
            {
                RequireAlive(assets[index], label);
                snapshot[index] = assets[index];
            }

            return snapshot;
        }

        private static int[] SnapshotValues(List<int> values, string label)
        {
            if (values == null)
            {
                throw new ArgumentException($"{label} collection is missing.", label);
            }

            return values.ToArray();
        }

        private static List<ResourceAmountDefinition> BuildAmounts(
            AmountSnapshot[] snapshots,
            IDictionary<ResourceDefinitionAsset, ResourceDefinition> resources,
            string label)
        {
            var amounts = new List<ResourceAmountDefinition>(snapshots.Length);
            foreach (var snapshot in snapshots)
            {
                var resource = GetRegistered(resources, snapshot.Resource, label);
                amounts.Add(new ResourceAmountDefinition(resource.Id, snapshot.Amount));
            }

            return amounts;
        }

        private static BufferDefinition BuildBuffer(
            BufferSnapshot snapshot,
            BufferRole role,
            IDictionary<ResourceDefinitionAsset, ResourceDefinition> resources)
        {
            if (snapshot == null)
            {
                return null;
            }

            var capacities = new List<KeyValuePair<ResourceId, long>>(snapshot.Capacities.Length);
            foreach (var capacity in snapshot.Capacities)
            {
                var resource = GetRegistered(resources, capacity.Resource, "buffer resource");
                capacities.Add(new KeyValuePair<ResourceId, long>(resource.Id, capacity.Capacity));
            }

            return new BufferDefinition(role, capacities);
        }

        private static void AddRegistered<TAsset, TDefinition>(
            IDictionary<TAsset, TDefinition> definitions,
            TAsset asset,
            TDefinition definition,
            string label)
            where TAsset : UnityEngine.Object
        {
            if (definitions.ContainsKey(asset))
            {
                throw new ArgumentException($"Catalog contains the same {label} asset more than once.", label);
            }

            definitions.Add(asset, definition);
        }

        private static TDefinition GetRegistered<TAsset, TDefinition>(
            IDictionary<TAsset, TDefinition> definitions,
            TAsset asset,
            string label)
            where TAsset : UnityEngine.Object
        {
            RequireAlive(asset, label);
            TDefinition definition;
            if (!definitions.TryGetValue(asset, out definition))
            {
                throw new ArgumentException($"{label} must reference an asset registered in the catalog.", label);
            }

            return definition;
        }

        private static void RequireAlive<T>(T asset, string parameterName)
            where T : UnityEngine.Object
        {
            if (ReferenceEquals(asset, null))
            {
                throw new ArgumentNullException(parameterName);
            }

            if (!asset)
            {
                throw new ArgumentException("Referenced Unity asset has been destroyed.", parameterName);
            }
        }

        private sealed class ResourceSnapshot
        {
            public ResourceSnapshot(ResourceDefinitionAsset asset, string id, string label, string key, bool developmentOnly)
            {
                Asset = asset;
                Id = id;
                DevelopmentLabel = label;
                PresentationKey = key;
                IsDevelopmentOnly = developmentOnly;
            }

            public ResourceDefinitionAsset Asset { get; }
            public string Id { get; }
            public string DevelopmentLabel { get; }
            public string PresentationKey { get; }
            public bool IsDevelopmentOnly { get; }
        }

        private sealed class RecipeSnapshot
        {
            public RecipeSnapshot(RecipeDefinitionAsset asset, string id, AmountSnapshot[] inputs, AmountSnapshot[] outputs, long duration, bool developmentOnly)
            {
                Asset = asset;
                Id = id;
                Inputs = inputs;
                Outputs = outputs;
                DurationTicks = duration;
                IsDevelopmentOnly = developmentOnly;
            }

            public RecipeDefinitionAsset Asset { get; }
            public string Id { get; }
            public AmountSnapshot[] Inputs { get; }
            public AmountSnapshot[] Outputs { get; }
            public long DurationTicks { get; }
            public bool IsDevelopmentOnly { get; }
        }

        private sealed class BuildingSnapshot
        {
            public BuildingSnapshot(string id, int width, int height, int[] orientations, RecipeDefinitionAsset recipe, BufferSnapshot input, BufferSnapshot output, string key, bool developmentOnly)
            {
                Id = id;
                FootprintWidth = width;
                FootprintHeight = height;
                AllowedOrientations = orientations;
                Recipe = recipe;
                InputBuffer = input;
                OutputBuffer = output;
                PresentationKey = key;
                IsDevelopmentOnly = developmentOnly;
            }

            public string Id { get; }
            public int FootprintWidth { get; }
            public int FootprintHeight { get; }
            public int[] AllowedOrientations { get; }
            public RecipeDefinitionAsset Recipe { get; }
            public BufferSnapshot InputBuffer { get; }
            public BufferSnapshot OutputBuffer { get; }
            public string PresentationKey { get; }
            public bool IsDevelopmentOnly { get; }
        }

        private sealed class PolicySnapshot
        {
            public PolicySnapshot(int revision, long numerator, long denominator, long maxTicks)
            {
                Revision = revision;
                EfficiencyNumerator = numerator;
                EfficiencyDenominator = denominator;
                MaxCreditedTicks = maxTicks;
            }

            public int Revision { get; }
            public long EfficiencyNumerator { get; }
            public long EfficiencyDenominator { get; }
            public long MaxCreditedTicks { get; }
        }

        private sealed class BufferSnapshot
        {
            public BufferSnapshot(CapacitySnapshot[] capacities)
            {
                Capacities = capacities;
            }

            public CapacitySnapshot[] Capacities { get; }
        }

        private readonly struct AmountSnapshot
        {
            public AmountSnapshot(ResourceDefinitionAsset resource, long amount)
            {
                Resource = resource;
                Amount = amount;
            }

            public ResourceDefinitionAsset Resource { get; }
            public long Amount { get; }
        }

        private readonly struct CapacitySnapshot
        {
            public CapacitySnapshot(ResourceDefinitionAsset resource, long capacity)
            {
                Resource = resource;
                Capacity = capacity;
            }

            public ResourceDefinitionAsset Resource { get; }
            public long Capacity { get; }
        }
    }
}
