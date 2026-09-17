using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace IdleFactory.Domain
{
    public readonly struct FactoryBounds : IEquatable<FactoryBounds>
    {
        private readonly int width;
        private readonly int height;

        public FactoryBounds(GridPosition origin, int width, int height)
        {
            if (width <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(width), width, "Factory width must be positive.");
            }

            if (height <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(height), height, "Factory height must be positive.");
            }

            checked
            {
                _ = origin.X + (width - 1);
                _ = origin.Y + (height - 1);
            }

            Origin = origin;
            this.width = width;
            this.height = height;
        }

        public GridPosition Origin { get; }
        public int Width
        {
            get
            {
                EnsureInitialized();
                return width;
            }
        }

        public int Height
        {
            get
            {
                EnsureInitialized();
                return height;
            }
        }

        public bool Contains(GridPosition position)
        {
            EnsureInitialized();
            return position.X >= Origin.X &&
                   position.Y >= Origin.Y &&
                   (long)position.X < (long)Origin.X + Width &&
                   (long)position.Y < (long)Origin.Y + Height;
        }

        public bool Equals(FactoryBounds other)
        {
            EnsureInitialized();
            other.EnsureInitialized();
            return Origin == other.Origin && Width == other.Width && Height == other.Height;
        }

        public override bool Equals(object obj) => obj is FactoryBounds other && Equals(other);

        public override int GetHashCode()
        {
            EnsureInitialized();
            unchecked
            {
                return (((Origin.GetHashCode() * 397) ^ Width) * 397) ^ Height;
            }
        }

        public static bool operator ==(FactoryBounds left, FactoryBounds right) => left.Equals(right);
        public static bool operator !=(FactoryBounds left, FactoryBounds right) => !left.Equals(right);

        private void EnsureInitialized()
        {
            if (width <= 0 || height <= 0)
            {
                throw new InvalidOperationException("The factory bounds are uninitialized.");
            }
        }
    }

    public enum BufferOperationResult
    {
        Success = 0,
        InvalidAmount = 1,
        UnknownResource = 2,
        InsufficientCapacity = 3,
        InsufficientQuantity = 4,
        ArithmeticOverflow = 5,
    }

    public sealed class ResourceBufferState
    {
        private readonly Dictionary<ResourceId, long> quantities;

        public ResourceBufferState(
            BufferDefinition definition,
            IEnumerable<KeyValuePair<ResourceId, long>> quantities)
        {
            Definition = definition ?? throw new ArgumentNullException(nameof(definition));
            if (quantities == null)
            {
                throw new ArgumentNullException(nameof(quantities));
            }

            var copy = new Dictionary<ResourceId, long>();
            foreach (var quantity in quantities)
            {
                _ = quantity.Key.Value;
                if (!Definition.Capacities.ContainsKey(quantity.Key))
                {
                    throw new ArgumentException(
                        $"Buffer quantity references undeclared resource '{quantity.Key.Value}'.",
                        nameof(quantities));
                }

                if (copy.ContainsKey(quantity.Key))
                {
                    throw new ArgumentException("Buffer quantities must contain each resource exactly once.", nameof(quantities));
                }

                var capacity = Definition.Capacities[quantity.Key];
                if (quantity.Value < 0 || quantity.Value > capacity)
                {
                    throw new ArgumentOutOfRangeException(
                        nameof(quantities),
                        quantity.Value,
                        $"Quantity for '{quantity.Key.Value}' must be between zero and capacity {capacity}.");
                }

                copy.Add(quantity.Key, quantity.Value);
            }

            if (copy.Count != Definition.Capacities.Count)
            {
                throw new ArgumentException("Buffer quantities must contain every declared resource exactly once.", nameof(quantities));
            }

            this.quantities = copy;
            Quantities = new ReadOnlyDictionary<ResourceId, long>(this.quantities);
        }

        public BufferDefinition Definition { get; }
        public IReadOnlyDictionary<ResourceId, long> Quantities { get; }

        public long Quantity(ResourceId resourceId)
        {
            _ = resourceId.Value;
            long quantity;
            if (!quantities.TryGetValue(resourceId, out quantity))
            {
                throw new KeyNotFoundException($"Buffer does not declare resource '{resourceId.Value}'.");
            }

            return quantity;
        }

        public long FreeCapacity(ResourceId resourceId)
        {
            return checked(Capacity(resourceId) - Quantity(resourceId));
        }

        public BufferOperationResult CanAdd(ResourceId resourceId, long amount)
        {
            _ = resourceId.Value;
            if (amount <= 0)
            {
                return BufferOperationResult.InvalidAmount;
            }

            long capacity;
            if (!Definition.Capacities.TryGetValue(resourceId, out capacity))
            {
                return BufferOperationResult.UnknownResource;
            }

            return amount <= capacity - quantities[resourceId]
                ? BufferOperationResult.Success
                : BufferOperationResult.InsufficientCapacity;
        }

        internal BufferOperationResult TryAdd(ResourceId resourceId, long amount)
        {
            var result = CanAdd(resourceId, amount);
            if (result == BufferOperationResult.Success)
            {
                quantities[resourceId] = checked(quantities[resourceId] + amount);
            }

            return result;
        }

        public BufferOperationResult CanRemove(ResourceId resourceId, long amount)
        {
            _ = resourceId.Value;
            if (amount <= 0)
            {
                return BufferOperationResult.InvalidAmount;
            }

            long quantity;
            if (!quantities.TryGetValue(resourceId, out quantity))
            {
                return BufferOperationResult.UnknownResource;
            }

            return amount <= quantity
                ? BufferOperationResult.Success
                : BufferOperationResult.InsufficientQuantity;
        }

        internal BufferOperationResult TryRemove(ResourceId resourceId, long amount)
        {
            var result = CanRemove(resourceId, amount);
            if (result == BufferOperationResult.Success)
            {
                quantities[resourceId] = checked(quantities[resourceId] - amount);
            }

            return result;
        }

        internal static BufferOperationResult TryExchange(
            ResourceBufferState inputBuffer,
            IEnumerable<KeyValuePair<ResourceId, long>> removals,
            ResourceBufferState outputBuffer,
            IEnumerable<KeyValuePair<ResourceId, long>> additions)
        {
            if (inputBuffer == null)
            {
                throw new ArgumentNullException(nameof(inputBuffer));
            }

            if (outputBuffer == null)
            {
                throw new ArgumentNullException(nameof(outputBuffer));
            }

            if (ReferenceEquals(inputBuffer, outputBuffer))
            {
                throw new ArgumentException("Production input and output buffers must be independently owned.");
            }

            List<KeyValuePair<ResourceId, long>> removalCopy;
            BufferOperationResult validationResult;
            if (!TryCopyExchange(removals, nameof(removals), out removalCopy, out validationResult))
            {
                return validationResult;
            }

            List<KeyValuePair<ResourceId, long>> additionCopy;
            if (!TryCopyExchange(additions, nameof(additions), out additionCopy, out validationResult))
            {
                return validationResult;
            }

            foreach (var removal in removalCopy)
            {
                var result = inputBuffer.CanRemove(removal.Key, removal.Value);
                if (result != BufferOperationResult.Success)
                {
                    return result;
                }
            }

            foreach (var addition in additionCopy)
            {
                long resultingQuantity;
                try
                {
                    resultingQuantity = checked(outputBuffer.Quantity(addition.Key) + addition.Value);
                }
                catch (KeyNotFoundException)
                {
                    return BufferOperationResult.UnknownResource;
                }
                catch (OverflowException)
                {
                    return BufferOperationResult.ArithmeticOverflow;
                }

                if (resultingQuantity > outputBuffer.Capacity(addition.Key))
                {
                    return BufferOperationResult.InsufficientCapacity;
                }
            }

            foreach (var removal in removalCopy)
            {
                inputBuffer.quantities[removal.Key] = checked(inputBuffer.quantities[removal.Key] - removal.Value);
            }

            foreach (var addition in additionCopy)
            {
                outputBuffer.quantities[addition.Key] = checked(outputBuffer.quantities[addition.Key] + addition.Value);
            }

            return BufferOperationResult.Success;
        }

        internal ResourceBufferState Copy()
        {
            return new ResourceBufferState(Definition, quantities);
        }

        private static bool TryCopyExchange(
            IEnumerable<KeyValuePair<ResourceId, long>> amounts,
            string parameterName,
            out List<KeyValuePair<ResourceId, long>> copy,
            out BufferOperationResult result)
        {
            if (amounts == null)
            {
                throw new ArgumentNullException(parameterName);
            }

            copy = new List<KeyValuePair<ResourceId, long>>();
            var resources = new HashSet<ResourceId>();
            foreach (var amount in amounts)
            {
                _ = amount.Key.Value;
                if (amount.Value <= 0 || !resources.Add(amount.Key))
                {
                    result = BufferOperationResult.InvalidAmount;
                    return false;
                }

                copy.Add(amount);
            }

            if (copy.Count == 0)
            {
                throw new ArgumentException("A production exchange must contain at least one resource.", parameterName);
            }

            result = BufferOperationResult.Success;
            return true;
        }

        private long Capacity(ResourceId resourceId)
        {
            _ = resourceId.Value;
            long capacity;
            if (!Definition.Capacities.TryGetValue(resourceId, out capacity))
            {
                throw new KeyNotFoundException($"Buffer does not declare resource '{resourceId.Value}'.");
            }

            return capacity;
        }
    }

    public sealed class ProductionState
    {
        public ProductionState(RecipeDefinition definition, long progressTicks)
        {
            Definition = definition ?? throw new ArgumentNullException(nameof(definition));
            if (progressTicks < 0 || progressTicks >= Definition.DurationTicks)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(progressTicks),
                    progressTicks,
                    "Production progress must be nonnegative and less than the recipe duration.");
            }

            ProgressTicks = progressTicks;
        }

        public RecipeDefinition Definition { get; }
        public RecipeId RecipeId => Definition.Id;
        public long ProgressTicks { get; private set; }

        internal void SetProgressTicks(long progressTicks)
        {
            if (progressTicks < 0 || progressTicks >= Definition.DurationTicks)
            {
                throw new ArgumentOutOfRangeException(nameof(progressTicks));
            }

            ProgressTicks = progressTicks;
        }

        internal ProductionState Copy() => new ProductionState(Definition, ProgressTicks);
    }

    public enum ProductionBlockerKind
    {
        Inactive = 0,
        MissingInput = 1,
        InsufficientOutputCapacity = 2,
    }

    public sealed class ProductionBlocker
    {
        public ProductionBlocker(
            ProductionBlockerKind kind,
            ResourceId? resourceId,
            long required,
            long available)
        {
            Kind = kind;
            ResourceId = resourceId;
            Required = required;
            Available = available;
        }

        public ProductionBlockerKind Kind { get; }
        public ResourceId? ResourceId { get; }
        public long Required { get; }
        public long Available { get; }
    }

    public sealed class BuildingInstanceState
    {
        public BuildingInstanceState(
            BuildingInstanceId id,
            BuildingDefinition definition,
            GridPosition anchor,
            int orientationQuarterTurns,
            bool isActive,
            ResourceBufferState inputBuffer,
            ResourceBufferState outputBuffer,
            ProductionState production)
        {
            _ = id.Value;
            Definition = definition ?? throw new ArgumentNullException(nameof(definition));
            if (!Definition.AllowedOrientations.Contains(orientationQuarterTurns))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(orientationQuarterTurns),
                    orientationQuarterTurns,
                    "The building definition does not allow this orientation.");
            }

            ValidateBuffer(inputBuffer, Definition.InputBuffer, nameof(inputBuffer));
            ValidateBuffer(outputBuffer, Definition.OutputBuffer, nameof(outputBuffer));
            ValidateProduction(production);

            Id = id;
            Anchor = anchor;
            OrientationQuarterTurns = orientationQuarterTurns;
            IsActive = isActive;
            InputBuffer = inputBuffer?.Copy();
            OutputBuffer = outputBuffer?.Copy();
            Production = production?.Copy();
        }

        public BuildingInstanceId Id { get; }
        public BuildingDefinition Definition { get; }
        public BuildingDefinitionId DefinitionId => Definition.Id;
        public GridPosition Anchor { get; }
        public int OrientationQuarterTurns { get; }
        public bool IsActive { get; private set; }
        public ResourceBufferState InputBuffer { get; }
        public ResourceBufferState OutputBuffer { get; }
        public ProductionState Production { get; }

        public IReadOnlyList<ProductionBlocker> GetProductionBlockers()
        {
            if (Production == null)
            {
                return Array.Empty<ProductionBlocker>();
            }

            var blockers = new List<ProductionBlocker>();
            if (!IsActive)
            {
                blockers.Add(new ProductionBlocker(ProductionBlockerKind.Inactive, null, 0, 0));
            }

            foreach (var input in Production.Definition.Inputs)
            {
                var available = InputBuffer.Quantity(input.ResourceId);
                if (available < input.Amount)
                {
                    blockers.Add(new ProductionBlocker(
                        ProductionBlockerKind.MissingInput,
                        input.ResourceId,
                        input.Amount,
                        available));
                }
            }

            foreach (var output in Production.Definition.Outputs)
            {
                var available = OutputBuffer.FreeCapacity(output.ResourceId);
                if (available < output.Amount)
                {
                    blockers.Add(new ProductionBlocker(
                        ProductionBlockerKind.InsufficientOutputCapacity,
                        output.ResourceId,
                        output.Amount,
                        available));
                }
            }

            return blockers.AsReadOnly();
        }

        internal void SetActive(bool isActive)
        {
            IsActive = isActive;
        }

        internal BuildingInstanceState Copy()
        {
            return new BuildingInstanceState(
                Id,
                Definition,
                Anchor,
                OrientationQuarterTurns,
                IsActive,
                InputBuffer,
                OutputBuffer,
                Production);
        }

        private void ValidateProduction(ProductionState production)
        {
            if (!Definition.RecipeId.HasValue)
            {
                if (production != null)
                {
                    throw new ArgumentException("A non-producing building cannot have production state.", nameof(production));
                }

                return;
            }

            if (production == null)
            {
                throw new ArgumentNullException(nameof(production), "A producing building requires production state.");
            }

            if (production.RecipeId != Definition.RecipeId.Value)
            {
                throw new ArgumentException("Production recipe does not match the building definition.", nameof(production));
            }
        }

        private static void ValidateBuffer(
            ResourceBufferState state,
            BufferDefinition definition,
            string parameterName)
        {
            if (definition == null)
            {
                if (state != null)
                {
                    throw new ArgumentException("Building state contains a buffer not declared by its definition.", parameterName);
                }

                return;
            }

            if (state == null)
            {
                throw new ArgumentNullException(parameterName, "Building state is missing a declared buffer.");
            }

            if (!ReferenceEquals(state.Definition, definition))
            {
                throw new ArgumentException("Buffer state must reference the building's immutable buffer definition.", parameterName);
            }
        }
    }

    public sealed class FactoryState
    {
        public FactoryState(
            FactoryBounds bounds,
            DefinitionCatalog catalog,
            IEnumerable<BuildingInstanceState> buildings)
        {
            _ = bounds.Width;
            Catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
            if (buildings == null)
            {
                throw new ArgumentNullException(nameof(buildings));
            }

            var buildingIndex = new Dictionary<BuildingInstanceId, BuildingInstanceState>();
            var occupancyIndex = new Dictionary<GridPosition, BuildingInstanceId>();
            foreach (var source in buildings)
            {
                if (source == null)
                {
                    throw new ArgumentException("Factory buildings must not contain null entries.", nameof(buildings));
                }

                if (buildingIndex.ContainsKey(source.Id))
                {
                    throw new ArgumentException($"Duplicate building instance ID '{source.Id.Value}'.", nameof(buildings));
                }

                BuildingDefinition resolvedDefinition;
                try
                {
                    resolvedDefinition = Catalog.Resolve(source.DefinitionId);
                }
                catch (KeyNotFoundException exception)
                {
                    throw new ArgumentException("Building definition reference does not resolve in the catalog.", nameof(buildings), exception);
                }

                if (!ReferenceEquals(resolvedDefinition, source.Definition))
                {
                    throw new ArgumentException(
                        "Building state must reference the immutable definition resolved by the factory catalog.",
                        nameof(buildings));
                }

                ValidateRecipeReference(source, nameof(buildings));
                var occupiedCells = source.Definition.Footprint
                    .OccupiedCells(source.Anchor, source.OrientationQuarterTurns)
                    .ToArray();
                foreach (var cell in occupiedCells)
                {
                    if (!bounds.Contains(cell))
                    {
                        throw new ArgumentException(
                            $"Building '{source.Id.Value}' footprint extends outside factory bounds.",
                            nameof(buildings));
                    }

                    if (occupancyIndex.ContainsKey(cell))
                    {
                        throw new ArgumentException($"Building footprints overlap at {cell}.", nameof(buildings));
                    }
                }

                var owned = source.Copy();
                buildingIndex.Add(owned.Id, owned);
                foreach (var cell in occupiedCells)
                {
                    occupancyIndex.Add(cell, owned.Id);
                }
            }

            Bounds = bounds;
            Buildings = new ReadOnlyDictionary<BuildingInstanceId, BuildingInstanceState>(buildingIndex);
            Occupancy = new ReadOnlyDictionary<GridPosition, BuildingInstanceId>(occupancyIndex);
        }

        public FactoryBounds Bounds { get; }
        public DefinitionCatalog Catalog { get; }
        public IReadOnlyDictionary<BuildingInstanceId, BuildingInstanceState> Buildings { get; }
        public IReadOnlyDictionary<GridPosition, BuildingInstanceId> Occupancy { get; }

        public FactoryState Copy()
        {
            return new FactoryState(Bounds, Catalog, Buildings.Values);
        }

        private void ValidateRecipeReference(BuildingInstanceState building, string parameterName)
        {
            if (building.Production == null)
            {
                return;
            }

            RecipeDefinition resolvedRecipe;
            try
            {
                resolvedRecipe = Catalog.Resolve(building.Production.RecipeId);
            }
            catch (KeyNotFoundException exception)
            {
                throw new ArgumentException("Production recipe reference does not resolve in the catalog.", parameterName, exception);
            }

            if (!ReferenceEquals(resolvedRecipe, building.Production.Definition))
            {
                throw new ArgumentException(
                    "Production state must reference the immutable recipe resolved by the factory catalog.",
                    parameterName);
            }
        }
    }
}
