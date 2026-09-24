using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using IdleFactory.Domain;
using IdleFactory.Simulation;

namespace IdleFactory.Core.Application
{
    public sealed class ResourceCommandResult
    {
        internal ResourceCommandResult(
            long revision,
            BuildingInstanceId instanceId,
            BufferRole bufferRole,
            ResourceId resourceId,
            long quantity,
            long capacity,
            long freeCapacity)
        {
            if (revision < 0 || quantity < 0 || capacity < 0 || quantity > capacity ||
                freeCapacity < 0 || freeCapacity != capacity - quantity)
            {
                throw new ArgumentOutOfRangeException(nameof(revision));
            }

            _ = instanceId.Value;
            _ = resourceId.Value;
            Revision = revision;
            InstanceId = instanceId;
            BufferRole = bufferRole;
            ResourceId = resourceId;
            Quantity = quantity;
            Capacity = capacity;
            FreeCapacity = freeCapacity;
        }

        public long Revision { get; }
        public BuildingInstanceId InstanceId { get; }
        public BufferRole BufferRole { get; }
        public ResourceId ResourceId { get; }
        public long Quantity { get; }
        public long Capacity { get; }
        public long FreeCapacity { get; }
    }

    public sealed class AdvanceSimulationResult
    {
        internal AdvanceSimulationResult(
            long revision,
            FactorySnapshot factory,
            ProductionAdvanceSummary summary,
            IEnumerable<MachineInspectionSnapshot> machines)
        {
            if (revision < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(revision));
            }

            Factory = factory ?? throw new ArgumentNullException(nameof(factory));
            Summary = summary ?? throw new ArgumentNullException(nameof(summary));
            if (Factory.Revision != revision)
            {
                throw new ArgumentException("The factory snapshot revision must match the command result.", nameof(factory));
            }

            Revision = revision;
            Machines = CopyMachines(machines, revision);
            if (Factory.Buildings.Count != Machines.Count || Summary.Buildings.Count != Machines.Count)
            {
                throw new ArgumentException("The command result must describe every factory machine.", nameof(machines));
            }

            var factoryIds = Factory.Buildings.Select(building => building.InstanceId).ToArray();
            var machineIds = Machines.Select(machine => machine.InstanceId).ToArray();
            var changedIds = Summary.Buildings.Select(building => building.BuildingId).ToArray();
            if (!factoryIds.SequenceEqual(machineIds) || !factoryIds.SequenceEqual(changedIds))
            {
                throw new ArgumentException("Factory, inspection and change results must use the same machine order.", nameof(machines));
            }
        }

        public long Revision { get; }
        public FactorySnapshot Factory { get; }
        public ProductionAdvanceSummary Summary { get; }
        public IReadOnlyList<MachineInspectionSnapshot> Machines { get; }

        private static IReadOnlyList<MachineInspectionSnapshot> CopyMachines(
            IEnumerable<MachineInspectionSnapshot> source,
            long revision)
        {
            if (source == null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            var copy = new List<MachineInspectionSnapshot>();
            foreach (var machine in source)
            {
                if (machine == null)
                {
                    throw new ArgumentException("Machine inspection results must not contain null entries.", nameof(source));
                }

                if (machine.Revision != revision)
                {
                    throw new ArgumentException("Every machine inspection must use the command result revision.", nameof(source));
                }

                copy.Add(machine);
            }

            return new ReadOnlyCollection<MachineInspectionSnapshot>(copy);
        }
    }

    public sealed class ProductionCommands
    {
        private readonly FactorySession session;

        public ProductionCommands(FactorySession session)
        {
            this.session = session ?? throw new ArgumentNullException(nameof(session));
        }

        public CommandResult<MachineInspectionSnapshot> GetMachineInspection(BuildingInstanceId instanceId) =>
            session.GetMachineInspection(instanceId);

        public CommandResult<MachineInspectionSnapshot> SetBuildingActive(
            BuildingInstanceId instanceId,
            bool isActive)
        {
            var admission = session.Admit(ApplicationOperation.OrdinaryMutation);
            if (!admission.Succeeded)
            {
                return CommandResult<MachineInspectionSnapshot>.Rejected(admission.Outcome, admission.Message);
            }

            if (!HasValue(instanceId))
            {
                return CommandResult<MachineInspectionSnapshot>.Rejected(
                    CommandOutcome.InvalidRequest,
                    "Building instance ID is required.");
            }

            var candidate = session.CreateStateCopy();
            BuildingInstanceState building;
            if (!candidate.Buildings.TryGetValue(instanceId, out building))
            {
                return CommandResult<MachineInspectionSnapshot>.Rejected(
                    CommandOutcome.NotFound,
                    "Building instance was not found.");
            }

            building.SetActive(isActive);
            var publication = session.ReplaceState(candidate);
            if (!publication.Succeeded)
            {
                return CommandResult<MachineInspectionSnapshot>.Rejected(publication.Outcome, publication.Message);
            }

            return session.GetMachineInspection(instanceId);
        }

        public CommandResult<ResourceCommandResult> AddResource(
            BuildingInstanceId instanceId,
            BufferRole bufferRole,
            ResourceId resourceId,
            long amount) =>
            ChangeResource(instanceId, bufferRole, resourceId, amount, add: true);

        public CommandResult<ResourceCommandResult> RemoveResource(
            BuildingInstanceId instanceId,
            BufferRole bufferRole,
            ResourceId resourceId,
            long amount) =>
            ChangeResource(instanceId, bufferRole, resourceId, amount, add: false);

        public CommandResult<AdvanceSimulationResult> AdvanceSimulation(long elapsedTicks)
        {
            var advance = session.AdvanceProduction(elapsedTicks);
            if (!advance.Succeeded)
            {
                return CommandResult<AdvanceSimulationResult>.Rejected(advance.Outcome, advance.Message);
            }

            var captured = advance.Value;
            var revision = session.Revision;
            var factorySnapshot = session.CreateFactorySnapshot(captured.Candidate, revision);
            var inspections = captured.Candidate.Buildings.Values
                .OrderBy(building => building.Id)
                .Select(building => session.CreateMachineInspectionSnapshot(
                    captured.Candidate,
                    revision,
                    building));

            return CommandResult<AdvanceSimulationResult>.Success(
                new AdvanceSimulationResult(revision, factorySnapshot, captured.Summary, inspections));
        }

        private CommandResult<ResourceCommandResult> ChangeResource(
            BuildingInstanceId instanceId,
            BufferRole bufferRole,
            ResourceId resourceId,
            long amount,
            bool add)
        {
            var admission = session.Admit(ApplicationOperation.OrdinaryMutation);
            if (!admission.Succeeded)
            {
                return CommandResult<ResourceCommandResult>.Rejected(admission.Outcome, admission.Message);
            }

            if (!HasValue(instanceId))
            {
                return CommandResult<ResourceCommandResult>.Rejected(
                    CommandOutcome.InvalidRequest,
                    "Building instance ID is required.");
            }

            if (!HasValue(resourceId))
            {
                return CommandResult<ResourceCommandResult>.Rejected(
                    CommandOutcome.InvalidRequest,
                    "Resource ID is required.");
            }

            if (amount <= 0)
            {
                return CommandResult<ResourceCommandResult>.Rejected(
                    CommandOutcome.InvalidRequest,
                    "Resource amount must be positive.");
            }

            if (!IsSupportedBufferRole(bufferRole))
            {
                return CommandResult<ResourceCommandResult>.Rejected(
                    CommandOutcome.InvalidRequest,
                    "A valid target buffer role is required.");
            }

            var candidate = session.CreateStateCopy();
            BuildingInstanceState building;
            if (!candidate.Buildings.TryGetValue(instanceId, out building))
            {
                return CommandResult<ResourceCommandResult>.Rejected(
                    CommandOutcome.NotFound,
                    "Building instance was not found.");
            }

            ResourceBufferState buffer;
            if (!TryGetBuffer(building, bufferRole, out buffer))
            {
                return CommandResult<ResourceCommandResult>.Rejected(
                    CommandOutcome.InvalidRequest,
                    "The requested buffer is not available for this building.");
            }

            var operation = add
                ? buffer.TryAdd(resourceId, amount)
                : buffer.TryRemove(resourceId, amount);
            if (operation != BufferOperationResult.Success)
            {
                return CommandResult<ResourceCommandResult>.Rejected(
                    MapBufferResult(operation),
                    BufferResultMessage(operation));
            }

            var publication = session.ReplaceState(candidate);
            if (!publication.Succeeded)
            {
                return CommandResult<ResourceCommandResult>.Rejected(publication.Outcome, publication.Message);
            }

            var capacity = buffer.Definition.Capacities[resourceId];
            var quantity = buffer.Quantity(resourceId);
            return CommandResult<ResourceCommandResult>.Success(new ResourceCommandResult(
                session.Revision,
                instanceId,
                bufferRole,
                resourceId,
                quantity,
                capacity,
                checked(capacity - quantity)));
        }

        private static bool HasValue(BuildingInstanceId value)
        {
            try
            {
                _ = value.Value;
                return true;
            }
            catch (InvalidOperationException)
            {
                return false;
            }
        }

        private static bool HasValue(ResourceId value)
        {
            try
            {
                _ = value.Value;
                return true;
            }
            catch (InvalidOperationException)
            {
                return false;
            }
        }

        private static bool IsSupportedBufferRole(BufferRole bufferRole) =>
            bufferRole == BufferRole.Input || bufferRole == BufferRole.Output;

        private static bool TryGetBuffer(
            BuildingInstanceState building,
            BufferRole bufferRole,
            out ResourceBufferState buffer)
        {
            buffer = bufferRole == BufferRole.Input
                ? building.InputBuffer
                : bufferRole == BufferRole.Output
                    ? building.OutputBuffer
                    : null;
            return buffer != null;
        }

        private static CommandOutcome MapBufferResult(BufferOperationResult result)
        {
            switch (result)
            {
                case BufferOperationResult.InvalidAmount:
                    return CommandOutcome.InvalidRequest;
                case BufferOperationResult.UnknownResource:
                    return CommandOutcome.NotFound;
                case BufferOperationResult.InsufficientCapacity:
                    return CommandOutcome.InsufficientCapacity;
                case BufferOperationResult.InsufficientQuantity:
                    return CommandOutcome.InsufficientResource;
                case BufferOperationResult.ArithmeticOverflow:
                    return CommandOutcome.InvalidRequest;
                default:
                    throw new ArgumentOutOfRangeException(nameof(result), result, "Unsupported buffer result.");
            }
        }

        private static string BufferResultMessage(BufferOperationResult result)
        {
            switch (result)
            {
                case BufferOperationResult.InvalidAmount:
                    return "Resource amount must be positive.";
                case BufferOperationResult.UnknownResource:
                    return "Resource is not declared by the selected buffer.";
                case BufferOperationResult.InsufficientCapacity:
                    return "The selected buffer does not have enough free capacity.";
                case BufferOperationResult.InsufficientQuantity:
                    return "The selected buffer does not contain enough of this resource.";
                case BufferOperationResult.ArithmeticOverflow:
                    return "The resource calculation exceeded its supported numeric range.";
                default:
                    throw new ArgumentOutOfRangeException(nameof(result), result, "Unsupported buffer result.");
            }
        }
    }
}
