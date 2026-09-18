using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using IdleFactory.Domain;

namespace IdleFactory.Core.Application
{
    public interface IBuildingInstanceIdSource
    {
        CommandResult<BuildingInstanceId> Next();
    }

    public enum PlacementPreviewRejection
    {
        UnknownDefinition = 0,
        UnsupportedOrientation = 1,
        OutOfBounds = 2,
        Occupied = 3,
    }

    public sealed class PlacementPreview
    {
        public PlacementPreview(
            IEnumerable<GridPosition> cells,
            IEnumerable<PlacementPreviewRejection> rejections)
        {
            Cells = Copy(cells, nameof(cells));
            Rejections = Copy(rejections, nameof(rejections));
        }

        public IReadOnlyList<GridPosition> Cells { get; }
        public IReadOnlyList<PlacementPreviewRejection> Rejections { get; }
        public bool IsValid => Rejections.Count == 0;

        private static IReadOnlyList<T> Copy<T>(IEnumerable<T> source, string parameterName)
        {
            if (source == null)
            {
                throw new ArgumentNullException(parameterName);
            }

            return new ReadOnlyCollection<T>(source.ToList());
        }
    }

    public sealed class PlacementCommandResult
    {
        public PlacementCommandResult(
            BuildingInstanceId instanceId,
            IEnumerable<GridPosition> occupiedCells,
            long revision)
        {
            _ = instanceId.Value;
            if (revision < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(revision));
            }

            if (occupiedCells == null)
            {
                throw new ArgumentNullException(nameof(occupiedCells));
            }

            InstanceId = instanceId;
            OccupiedCells = new ReadOnlyCollection<GridPosition>(occupiedCells.ToList());
            Revision = revision;
        }

        public BuildingInstanceId InstanceId { get; }
        public IReadOnlyList<GridPosition> OccupiedCells { get; }
        public long Revision { get; }
    }

    public enum SelectionResolutionKind
    {
        Empty = 0,
        Selected = 1,
        Missing = 2,
    }

    public sealed class SelectionResolution
    {
        private SelectionResolution(
            SelectionResolutionKind kind,
            BuildingInstanceId? requestedInstanceId,
            MachineInspectionSnapshot inspection)
        {
            Kind = kind;
            RequestedInstanceId = requestedInstanceId;
            Inspection = inspection;
        }

        public SelectionResolutionKind Kind { get; }
        public BuildingInstanceId? RequestedInstanceId { get; }
        public MachineInspectionSnapshot Inspection { get; }

        internal static SelectionResolution Empty() =>
            new SelectionResolution(SelectionResolutionKind.Empty, null, null);

        internal static SelectionResolution Selected(
            BuildingInstanceId instanceId,
            MachineInspectionSnapshot inspection) =>
            new SelectionResolution(
                SelectionResolutionKind.Selected,
                instanceId,
                inspection ?? throw new ArgumentNullException(nameof(inspection)));

        internal static SelectionResolution Missing(BuildingInstanceId instanceId) =>
            new SelectionResolution(SelectionResolutionKind.Missing, instanceId, null);
    }

    public sealed class PlacementCommands
    {
        private readonly FactorySession session;
        private readonly IBuildingInstanceIdSource instanceIds;

        public PlacementCommands(FactorySession session, IBuildingInstanceIdSource instanceIds)
        {
            this.session = session ?? throw new ArgumentNullException(nameof(session));
            this.instanceIds = instanceIds ?? throw new ArgumentNullException(nameof(instanceIds));
        }

        public CommandResult<PlacementCommandResult> PlaceBuilding(
            BuildingDefinitionId definitionId,
            GridPosition anchor,
            int orientationQuarterTurns)
        {
            var admission = session.Admit(ApplicationOperation.OrdinaryMutation);
            if (!admission.Succeeded)
            {
                return CommandResult<PlacementCommandResult>.Rejected(admission.Outcome, admission.Message);
            }

            var nextId = instanceIds.Next();
            if (!nextId.Succeeded)
            {
                return CommandResult<PlacementCommandResult>.Rejected(nextId.Outcome, nextId.Message);
            }

            if (nextId.Value == default(BuildingInstanceId))
            {
                return CommandResult<PlacementCommandResult>.Rejected(
                    CommandOutcome.InvalidRequest,
                    "The building instance ID source returned an empty ID.");
            }

            var placement = FactoryPlacement.TryPlace(
                session.CreateStateCopy(),
                nextId.Value,
                definitionId,
                anchor,
                orientationQuarterTurns);
            if (!placement.IsSuccess)
            {
                return CommandResult<PlacementCommandResult>.Rejected(
                    Map(placement.Outcome),
                    Message(placement.Outcome));
            }

            var occupiedCells = placement.PlacedBuilding.Definition.Footprint
                .OccupiedCells(anchor, orientationQuarterTurns)
                .ToArray();
            session.ReplaceState(placement.Candidate);
            var revision = session.GetFactorySnapshot().Revision;
            return CommandResult<PlacementCommandResult>.Success(
                new PlacementCommandResult(nextId.Value, occupiedCells, revision));
        }

        public PlacementPreview GetPlacementPreview(
            BuildingDefinitionId definitionId,
            GridPosition anchor,
            int orientationQuarterTurns)
        {
            var state = session.CreateStateCopy();
            BuildingDefinition definition;
            if (definitionId == default(BuildingDefinitionId) ||
                !session.Catalog.Buildings.TryGetValue(definitionId, out definition))
            {
                return new PlacementPreview(
                    Array.Empty<GridPosition>(),
                    new[] { PlacementPreviewRejection.UnknownDefinition });
            }

            var rejections = new List<PlacementPreviewRejection>();
            if (!definition.AllowedOrientations.Contains(orientationQuarterTurns))
            {
                rejections.Add(PlacementPreviewRejection.UnsupportedOrientation);
            }

            GridPosition[] cells;
            try
            {
                cells = definition.Footprint.OccupiedCells(anchor, orientationQuarterTurns).ToArray();
            }
            catch (ArgumentOutOfRangeException)
            {
                cells = Array.Empty<GridPosition>();
            }
            catch (OverflowException)
            {
                cells = Array.Empty<GridPosition>();
                rejections.Add(PlacementPreviewRejection.OutOfBounds);
            }

            if (cells.Any(cell => !state.Bounds.Contains(cell)) &&
                !rejections.Contains(PlacementPreviewRejection.OutOfBounds))
            {
                rejections.Add(PlacementPreviewRejection.OutOfBounds);
            }

            if (cells.Any(state.Occupancy.ContainsKey))
            {
                rejections.Add(PlacementPreviewRejection.Occupied);
            }

            return new PlacementPreview(cells, rejections);
        }

        public FactorySnapshot GetFactorySnapshot() => session.GetFactorySnapshot();

        public SelectionResolution ResolveSelection(BuildingInstanceId? selectedInstanceId)
        {
            if (!selectedInstanceId.HasValue)
            {
                return SelectionResolution.Empty();
            }

            var instanceId = selectedInstanceId.Value;
            if (instanceId == default(BuildingInstanceId))
            {
                return SelectionResolution.Missing(instanceId);
            }

            var inspection = session.GetMachineInspection(instanceId);
            return inspection.Succeeded
                ? SelectionResolution.Selected(instanceId, inspection.Value)
                : SelectionResolution.Missing(instanceId);
        }

        private static CommandOutcome Map(PlacementOutcome outcome)
        {
            switch (outcome)
            {
                case PlacementOutcome.InvalidRequest:
                case PlacementOutcome.DuplicateInstanceId:
                    return CommandOutcome.InvalidRequest;
                case PlacementOutcome.UnknownDefinition:
                    return CommandOutcome.NotFound;
                case PlacementOutcome.UnsupportedOrientation:
                    return CommandOutcome.InvalidDefinition;
                case PlacementOutcome.OutOfBounds:
                    return CommandOutcome.OutOfBounds;
                case PlacementOutcome.Overlap:
                    return CommandOutcome.Occupied;
                default:
                    return CommandOutcome.InvalidRequest;
            }
        }

        private static string Message(PlacementOutcome outcome)
        {
            switch (outcome)
            {
                case PlacementOutcome.UnknownDefinition:
                    return "Building definition was not found.";
                case PlacementOutcome.UnsupportedOrientation:
                    return "Building orientation is not supported by the definition.";
                case PlacementOutcome.OutOfBounds:
                    return "The complete building footprint must be inside the factory bounds.";
                case PlacementOutcome.Overlap:
                    return "The building footprint overlaps an occupied cell.";
                case PlacementOutcome.DuplicateInstanceId:
                    return "The building instance ID is already in use.";
                default:
                    return "The placement request is invalid.";
            }
        }
    }
}
