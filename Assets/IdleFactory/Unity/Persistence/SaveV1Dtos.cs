using System.Collections.Generic;

namespace IdleFactory.Unity.Persistence
{
    public sealed class SaveV1Dto
    {
        internal SaveV1Dto(
            int schemaVersion,
            string checkpointId,
            long generation,
            long departureUtcUnixMilliseconds,
            int offlinePolicyRevision,
            string reconciledFromCheckpointId,
            FactoryV1Dto factory)
        {
            SchemaVersion = schemaVersion;
            CheckpointId = checkpointId;
            Generation = generation;
            DepartureUtcUnixMilliseconds = departureUtcUnixMilliseconds;
            OfflinePolicyRevision = offlinePolicyRevision;
            ReconciledFromCheckpointId = reconciledFromCheckpointId;
            Factory = factory;
        }

        public int SchemaVersion { get; }
        public string CheckpointId { get; }
        public long Generation { get; }
        public long DepartureUtcUnixMilliseconds { get; }
        public int OfflinePolicyRevision { get; }
        public string ReconciledFromCheckpointId { get; }
        public FactoryV1Dto Factory { get; }
    }

    public sealed class FactoryV1Dto
    {
        internal FactoryV1Dto(int originX, int originY, int width, int height, IReadOnlyList<BuildingV1Dto> buildings)
        {
            OriginX = originX;
            OriginY = originY;
            Width = width;
            Height = height;
            Buildings = buildings;
        }

        public int OriginX { get; }
        public int OriginY { get; }
        public int Width { get; }
        public int Height { get; }
        public IReadOnlyList<BuildingV1Dto> Buildings { get; }
    }

    public sealed class BuildingV1Dto
    {
        internal BuildingV1Dto(
            string instanceId,
            string definitionId,
            int anchorX,
            int anchorY,
            int orientationQuarterTurns,
            bool isActive,
            IReadOnlyList<ResourceQuantityV1Dto> inputQuantities,
            IReadOnlyList<ResourceQuantityV1Dto> outputQuantities,
            ProductionV1Dto production)
        {
            InstanceId = instanceId;
            DefinitionId = definitionId;
            AnchorX = anchorX;
            AnchorY = anchorY;
            OrientationQuarterTurns = orientationQuarterTurns;
            IsActive = isActive;
            InputQuantities = inputQuantities;
            OutputQuantities = outputQuantities;
            Production = production;
        }

        public string InstanceId { get; }
        public string DefinitionId { get; }
        public int AnchorX { get; }
        public int AnchorY { get; }
        public int OrientationQuarterTurns { get; }
        public bool IsActive { get; }
        public IReadOnlyList<ResourceQuantityV1Dto> InputQuantities { get; }
        public IReadOnlyList<ResourceQuantityV1Dto> OutputQuantities { get; }
        public ProductionV1Dto Production { get; }
    }

    public sealed class ResourceQuantityV1Dto
    {
        internal ResourceQuantityV1Dto(string resourceId, long quantity)
        {
            ResourceId = resourceId;
            Quantity = quantity;
        }

        public string ResourceId { get; }
        public long Quantity { get; }
    }

    public sealed class ProductionV1Dto
    {
        internal ProductionV1Dto(string recipeId, long progressTicks)
        {
            RecipeId = recipeId;
            ProgressTicks = progressTicks;
        }

        public string RecipeId { get; }
        public long ProgressTicks { get; }
    }
}
