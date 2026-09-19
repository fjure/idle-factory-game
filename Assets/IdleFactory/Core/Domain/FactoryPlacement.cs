using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
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
            BuildingInstanceState placedBuilding,
            IEnumerable<GridPosition> occupiedCells)
        {
            Outcome = outcome;
            Candidate = candidate;
            PlacedBuilding = placedBuilding;
            OccupiedCells = new ReadOnlyCollection<GridPosition>(occupiedCells.ToList());
        }

        public PlacementOutcome Outcome { get; }
        public FactoryState Candidate { get; }
        public BuildingInstanceState PlacedBuilding { get; }
        public IReadOnlyList<GridPosition> OccupiedCells { get; }
        public bool IsSuccess => Outcome == PlacementOutcome.Success;
    }

    public sealed class PlacementEvaluation
    {
        internal PlacementEvaluation(
            IEnumerable<GridPosition> occupiedCells,
            IEnumerable<PlacementOutcome> failures)
        {
            OccupiedCells = new ReadOnlyCollection<GridPosition>(occupiedCells.ToList());
            Failures = new ReadOnlyCollection<PlacementOutcome>(failures.ToList());
        }

        public IReadOnlyList<GridPosition> OccupiedCells { get; }
        public IReadOnlyList<PlacementOutcome> Failures { get; }
        public bool IsValid => Failures.Count == 0;
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
            if (instanceId == default(BuildingInstanceId))
            {
                return Rejected(PlacementOutcome.InvalidRequest);
            }

            var evaluation = Evaluate(current, definitionId, anchor, orientationQuarterTurns);
            if (evaluation.Failures.Count > 0 &&
                evaluation.Failures[0] == PlacementOutcome.InvalidRequest)
            {
                return Rejected(PlacementOutcome.InvalidRequest);
            }

            if (current.Buildings.ContainsKey(instanceId))
            {
                return Rejected(PlacementOutcome.DuplicateInstanceId);
            }

            if (!evaluation.IsValid)
            {
                return Rejected(evaluation.Failures[0]);
            }

            var definition = current.Catalog.Buildings[definitionId];
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

            return new PlacementResult(
                PlacementOutcome.Success,
                candidate,
                placedBuilding,
                evaluation.OccupiedCells);
        }

        public static PlacementEvaluation Evaluate(
            FactoryState current,
            BuildingDefinitionId definitionId,
            GridPosition anchor,
            int orientationQuarterTurns)
        {
            if (current == null || definitionId == default(BuildingDefinitionId))
            {
                return Evaluated(Array.Empty<GridPosition>(), PlacementOutcome.InvalidRequest);
            }

            BuildingDefinition definition;
            if (!current.Catalog.Buildings.TryGetValue(definitionId, out definition))
            {
                return Evaluated(Array.Empty<GridPosition>(), PlacementOutcome.UnknownDefinition);
            }

            var failures = new List<PlacementOutcome>();
            if (!definition.AllowedOrientations.Contains(orientationQuarterTurns))
            {
                failures.Add(PlacementOutcome.UnsupportedOrientation);
            }

            GridPosition[] occupiedCells;
            try
            {
                occupiedCells = definition.Footprint
                    .OccupiedCells(anchor, orientationQuarterTurns)
                    .ToArray();
            }
            catch (ArgumentOutOfRangeException)
            {
                occupiedCells = Array.Empty<GridPosition>();
            }
            catch (OverflowException)
            {
                occupiedCells = Array.Empty<GridPosition>();
                failures.Add(PlacementOutcome.OutOfBounds);
            }

            if (occupiedCells.Any(cell => !current.Bounds.Contains(cell)) &&
                !failures.Contains(PlacementOutcome.OutOfBounds))
            {
                failures.Add(PlacementOutcome.OutOfBounds);
            }

            if (occupiedCells.Any(current.Occupancy.ContainsKey))
            {
                failures.Add(PlacementOutcome.Overlap);
            }

            return new PlacementEvaluation(occupiedCells, failures);
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
            return new PlacementResult(outcome, null, null, Array.Empty<GridPosition>());
        }

        private static PlacementEvaluation Evaluated(
            IEnumerable<GridPosition> occupiedCells,
            params PlacementOutcome[] failures)
        {
            return new PlacementEvaluation(occupiedCells, failures);
        }
    }
}
