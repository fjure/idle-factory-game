using System;
using System.Collections.Generic;
using System.Linq;

namespace IdleFactory.Domain
{
    public enum PlacementOutcome
    {
        Success = 0,
        InvalidRequest = 1,
        UnknownDefinition = 2,
        UnsupportedOrientation = 3,
        OutOfBounds = 4,
        Overlap = 5,
        DuplicateInstanceId = 6,
    }

    public sealed class PlacementResult
    {
        internal PlacementResult(
            PlacementOutcome outcome,
            FactoryState candidate,
            BuildingInstanceState placedBuilding)
        {
            Outcome = outcome;
            Candidate = candidate;
            PlacedBuilding = placedBuilding;
        }

        public PlacementOutcome Outcome { get; }
        public FactoryState Candidate { get; }
        public BuildingInstanceState PlacedBuilding { get; }
        public bool IsSuccess => Outcome == PlacementOutcome.Success;
    }

    public static class FactoryPlacement
    {
        public static PlacementResult TryPlace(
            FactoryState current,
            BuildingInstanceId instanceId,
            BuildingDefinitionId definitionId,
            GridPosition anchor,
            int orientationQuarterTurns)
        {
            if (current == null ||
                instanceId == default(BuildingInstanceId) ||
                definitionId == default(BuildingDefinitionId))
            {
                return Rejected(PlacementOutcome.InvalidRequest);
            }

            if (current.Buildings.ContainsKey(instanceId))
            {
                return Rejected(PlacementOutcome.DuplicateInstanceId);
            }

            BuildingDefinition definition;
            if (!current.Catalog.Buildings.TryGetValue(definitionId, out definition))
            {
                return Rejected(PlacementOutcome.UnknownDefinition);
            }

            if (!definition.AllowedOrientations.Contains(orientationQuarterTurns))
            {
                return Rejected(PlacementOutcome.UnsupportedOrientation);
            }

            GridPosition[] occupiedCells;
            try
            {
                occupiedCells = definition.Footprint
                    .OccupiedCells(anchor, orientationQuarterTurns)
                    .ToArray();
            }
            catch (OverflowException)
            {
                return Rejected(PlacementOutcome.OutOfBounds);
            }

            foreach (var cell in occupiedCells)
            {
                if (!current.Bounds.Contains(cell))
                {
                    return Rejected(PlacementOutcome.OutOfBounds);
                }
            }

            foreach (var cell in occupiedCells)
            {
                if (current.Occupancy.ContainsKey(cell))
                {
                    return Rejected(PlacementOutcome.Overlap);
                }
            }

            var placedSource = new BuildingInstanceState(
                instanceId,
                definition,
                anchor,
                orientationQuarterTurns,
                true,
                CreateEmptyBuffer(definition.InputBuffer),
                CreateEmptyBuffer(definition.OutputBuffer),
                CreateProduction(current.Catalog, definition));
            var candidate = new FactoryState(
                current.Bounds,
                current.Catalog,
                current.Buildings.Values.Concat(new[] { placedSource }));
            var placedBuilding = candidate.Buildings[instanceId];

            return new PlacementResult(PlacementOutcome.Success, candidate, placedBuilding);
        }

        private static ResourceBufferState CreateEmptyBuffer(BufferDefinition definition)
        {
            if (definition == null)
            {
                return null;
            }

            return new ResourceBufferState(
                definition,
                definition.Capacities.Keys.Select(resourceId =>
                    new KeyValuePair<ResourceId, long>(resourceId, 0)));
        }

        private static ProductionState CreateProduction(
            DefinitionCatalog catalog,
            BuildingDefinition definition)
        {
            return definition.RecipeId.HasValue
                ? new ProductionState(catalog.Resolve(definition.RecipeId.Value), 0)
                : null;
        }

        private static PlacementResult Rejected(PlacementOutcome outcome)
        {
            return new PlacementResult(outcome, null, null);
        }
    }
}
