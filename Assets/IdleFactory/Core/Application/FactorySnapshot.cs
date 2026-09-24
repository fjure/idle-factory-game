using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using IdleFactory.Domain;

namespace IdleFactory.Core.Application
{
    /// <summary>
    /// A detached, view-independent description of all factory state relevant to deterministic
    /// simulation comparisons. Application revisions intentionally live outside this value.
    /// </summary>
    public sealed class SemanticFactorySnapshot : IEquatable<SemanticFactorySnapshot>
    {
        private SemanticFactorySnapshot(
            FactoryBounds bounds,
            IEnumerable<SemanticOccupiedCell> occupiedCells,
            IEnumerable<SemanticBuildingSnapshot> buildings)
        {
            Bounds = bounds;
            OccupiedCells = Copy(occupiedCells, nameof(occupiedCells));
            Buildings = Copy(buildings, nameof(buildings));
            Checksum = ComputeChecksum(this);
        }

        public FactoryBounds Bounds { get; }
        public IReadOnlyList<SemanticOccupiedCell> OccupiedCells { get; }
        public IReadOnlyList<SemanticBuildingSnapshot> Buildings { get; }

        /// <summary>
        /// Stable SHA-256 diagnostic for this snapshot. Equality compares the captured fields
        /// directly and never uses this checksum as its oracle.
        /// </summary>
        public string Checksum { get; }

        public static SemanticFactorySnapshot Capture(FactoryState factory)
        {
            if (factory == null)
            {
                throw new ArgumentNullException(nameof(factory));
            }

            var occupiedCells = factory.Occupancy
                .Select(cell => new SemanticOccupiedCell(cell.Key, cell.Value))
                .OrderBy(cell => cell.Position.Y)
                .ThenBy(cell => cell.Position.X)
                .ThenBy(cell => cell.BuildingId);

            var buildings = factory.Buildings.Values
                .OrderBy(building => building.Id)
                .Select(CaptureBuilding);

            return new SemanticFactorySnapshot(factory.Bounds, occupiedCells, buildings);
        }

        public bool Equals(SemanticFactorySnapshot other)
        {
            if (ReferenceEquals(this, other))
            {
                return true;
            }

            if (other == null || Bounds != other.Bounds ||
                OccupiedCells.Count != other.OccupiedCells.Count ||
                Buildings.Count != other.Buildings.Count)
            {
                return false;
            }

            for (var index = 0; index < OccupiedCells.Count; index++)
            {
                if (!OccupiedCells[index].Equals(other.OccupiedCells[index]))
                {
                    return false;
                }
            }

            for (var index = 0; index < Buildings.Count; index++)
            {
                if (!Buildings[index].Equals(other.Buildings[index]))
                {
                    return false;
                }
            }

            return true;
        }

        public override bool Equals(object obj) => Equals(obj as SemanticFactorySnapshot);

        public override int GetHashCode()
        {
            unchecked
            {
                return (((Bounds.GetHashCode() * 397) ^ OccupiedCells.Count) * 397) ^ Buildings.Count;
            }
        }

        public static bool operator ==(SemanticFactorySnapshot left, SemanticFactorySnapshot right) =>
            Equals(left, right);

        public static bool operator !=(SemanticFactorySnapshot left, SemanticFactorySnapshot right) =>
            !Equals(left, right);

        private static SemanticBuildingSnapshot CaptureBuilding(BuildingInstanceState building)
        {
            var production = building.Production;
            var inputBuffer = CaptureBuffer(building.InputBuffer);
            var outputBuffer = CaptureBuffer(building.OutputBuffer);
            var recipeInputs = production == null
                ? Array.Empty<SemanticRecipeAmount>()
                : production.Definition.Inputs
                    .Select(amount => new SemanticRecipeAmount(amount.ResourceId, amount.Amount))
                    .OrderBy(amount => amount.ResourceId)
                    .ToArray();
            var recipeOutputs = production == null
                ? Array.Empty<SemanticRecipeAmount>()
                : production.Definition.Outputs
                    .Select(amount => new SemanticRecipeAmount(amount.ResourceId, amount.Amount))
                    .OrderBy(amount => amount.ResourceId)
                    .ToArray();
            var blockers = building.GetProductionBlockers()
                .Select(blocker => new SemanticBlockerSnapshot(
                    blocker.Kind,
                    blocker.ResourceId,
                    blocker.Required,
                    blocker.Available))
                .OrderBy(blocker => blocker.Kind)
                .ThenBy(blocker => blocker.ResourceId.HasValue ? blocker.ResourceId.Value.Value : string.Empty,
                    StringComparer.Ordinal)
                .ThenBy(blocker => blocker.Required)
                .ThenBy(blocker => blocker.Available);

            return new SemanticBuildingSnapshot(
                building.Id,
                building.DefinitionId,
                building.Anchor,
                building.OrientationQuarterTurns,
                building.Definition.Footprint,
                building.Definition.AllowedOrientations,
                building.IsActive,
                production?.RecipeId,
                production?.Definition.DurationTicks ?? 0,
                production?.ProgressTicks ?? 0,
                recipeInputs,
                recipeOutputs,
                inputBuffer,
                outputBuffer,
                blockers);
        }

        private static SemanticResourceStorage[] CaptureBuffer(ResourceBufferState buffer)
        {
            if (buffer == null)
            {
                return Array.Empty<SemanticResourceStorage>();
            }

            return buffer.Definition.Capacities
                .OrderBy(capacity => capacity.Key)
                .Select(capacity => new SemanticResourceStorage(
                    buffer.Definition.Role,
                    capacity.Key,
                    buffer.Quantity(capacity.Key),
                    capacity.Value))
                .ToArray();
        }

        private static IReadOnlyList<T> Copy<T>(IEnumerable<T> source, string parameterName)
        {
            if (source == null)
            {
                throw new ArgumentNullException(parameterName);
            }

            var copy = source.ToArray();
            if (copy.Any(item => ReferenceEquals(item, null)))
            {
                throw new ArgumentException("Semantic snapshot collections must not contain null entries.", parameterName);
            }

            return new ReadOnlyCollection<T>(copy);
        }

        private static string ComputeChecksum(SemanticFactorySnapshot snapshot)
        {
            var canonical = new StringBuilder(256);
            canonical.Append("IdleFactory.SemanticFactorySnapshot.v1|");
            AppendInt(canonical, snapshot.Bounds.Origin.X);
            AppendInt(canonical, snapshot.Bounds.Origin.Y);
            AppendInt(canonical, snapshot.Bounds.Width);
            AppendInt(canonical, snapshot.Bounds.Height);
            AppendInt(canonical, snapshot.OccupiedCells.Count);
            foreach (var cell in snapshot.OccupiedCells)
            {
                AppendInt(canonical, cell.Position.X);
                AppendInt(canonical, cell.Position.Y);
                AppendString(canonical, cell.BuildingId.Value);
            }

            AppendInt(canonical, snapshot.Buildings.Count);
            foreach (var building in snapshot.Buildings)
            {
                AppendString(canonical, building.InstanceId.Value);
                AppendString(canonical, building.DefinitionId.Value);
                AppendInt(canonical, building.Anchor.X);
                AppendInt(canonical, building.Anchor.Y);
                AppendInt(canonical, building.OrientationQuarterTurns);
                AppendInt(canonical, building.Footprint.Width);
                AppendInt(canonical, building.Footprint.Height);
                AppendInt(canonical, building.AllowedOrientations.Count);
                foreach (var orientation in building.AllowedOrientations)
                {
                    AppendInt(canonical, orientation);
                }

                canonical.Append(building.IsActive ? "1|" : "0|");
                AppendNullableString(canonical, building.RecipeId.HasValue
                    ? building.RecipeId.Value.Value
                    : null);
                AppendLong(canonical, building.RecipeDurationTicks);
                AppendLong(canonical, building.ProgressTicks);
                AppendRecipeAmounts(canonical, building.RecipeInputs);
                AppendRecipeAmounts(canonical, building.RecipeOutputs);
                AppendResourceStorage(canonical, building.Inputs);
                AppendResourceStorage(canonical, building.Outputs);
                AppendInt(canonical, building.Blockers.Count);
                foreach (var blocker in building.Blockers)
                {
                    AppendInt(canonical, (int)blocker.Kind);
                    AppendNullableString(canonical, blocker.ResourceId.HasValue
                        ? blocker.ResourceId.Value.Value
                        : null);
                    AppendLong(canonical, blocker.Required);
                    AppendLong(canonical, blocker.Available);
                }
            }

            using (var sha = SHA256.Create())
            {
                var hash = sha.ComputeHash(Encoding.UTF8.GetBytes(canonical.ToString()));
                var hexadecimal = new StringBuilder(hash.Length * 2);
                foreach (var value in hash)
                {
                    hexadecimal.Append(value.ToString("x2", CultureInfo.InvariantCulture));
                }

                return hexadecimal.ToString();
            }
        }

        private static void AppendRecipeAmounts(StringBuilder builder, IReadOnlyList<SemanticRecipeAmount> amounts)
        {
            AppendInt(builder, amounts.Count);
            foreach (var amount in amounts)
            {
                AppendString(builder, amount.ResourceId.Value);
                AppendLong(builder, amount.Amount);
            }
        }

        private static void AppendResourceStorage(StringBuilder builder, IReadOnlyList<SemanticResourceStorage> storage)
        {
            AppendInt(builder, storage.Count);
            foreach (var resource in storage)
            {
                AppendInt(builder, (int)resource.Role);
                AppendString(builder, resource.ResourceId.Value);
                AppendLong(builder, resource.Quantity);
                AppendLong(builder, resource.Capacity);
            }
        }

        private static void AppendNullableString(StringBuilder builder, string value)
        {
            if (value == null)
            {
                builder.Append("N|");
                return;
            }

            builder.Append("S|");
            AppendString(builder, value);
        }

        private static void AppendString(StringBuilder builder, string value)
        {
            builder.Append(value.Length.ToString(CultureInfo.InvariantCulture));
            builder.Append(':').Append(value).Append('|');
        }

        private static void AppendInt(StringBuilder builder, int value)
        {
            builder.Append(value.ToString(CultureInfo.InvariantCulture)).Append('|');
        }

        private static void AppendLong(StringBuilder builder, long value)
        {
            builder.Append(value.ToString(CultureInfo.InvariantCulture)).Append('|');
        }
    }

    public sealed class SemanticOccupiedCell : IEquatable<SemanticOccupiedCell>
    {
        internal SemanticOccupiedCell(GridPosition position, BuildingInstanceId buildingId)
        {
            Position = position;
            BuildingId = buildingId;
        }

        public GridPosition Position { get; }
        public BuildingInstanceId BuildingId { get; }

        public bool Equals(SemanticOccupiedCell other) =>
            other != null && Position == other.Position && BuildingId == other.BuildingId;

        public override bool Equals(object obj) => Equals(obj as SemanticOccupiedCell);

        public override int GetHashCode()
        {
            unchecked
            {
                return (Position.GetHashCode() * 397) ^ BuildingId.GetHashCode();
            }
        }
    }

    public sealed class SemanticBuildingSnapshot : IEquatable<SemanticBuildingSnapshot>
    {
        internal SemanticBuildingSnapshot(
            BuildingInstanceId instanceId,
            BuildingDefinitionId definitionId,
            GridPosition anchor,
            int orientationQuarterTurns,
            Footprint footprint,
            IEnumerable<int> allowedOrientations,
            bool isActive,
            RecipeId? recipeId,
            long recipeDurationTicks,
            long progressTicks,
            IEnumerable<SemanticRecipeAmount> recipeInputs,
            IEnumerable<SemanticRecipeAmount> recipeOutputs,
            IEnumerable<SemanticResourceStorage> inputs,
            IEnumerable<SemanticResourceStorage> outputs,
            IEnumerable<SemanticBlockerSnapshot> blockers)
        {
            InstanceId = instanceId;
            DefinitionId = definitionId;
            Anchor = anchor;
            OrientationQuarterTurns = orientationQuarterTurns;
            Footprint = footprint;
            AllowedOrientations = Copy(allowedOrientations, nameof(allowedOrientations));
            IsActive = isActive;
            RecipeId = recipeId;
            RecipeDurationTicks = recipeDurationTicks;
            ProgressTicks = progressTicks;
            RecipeInputs = Copy(recipeInputs, nameof(recipeInputs));
            RecipeOutputs = Copy(recipeOutputs, nameof(recipeOutputs));
            Inputs = Copy(inputs, nameof(inputs));
            Outputs = Copy(outputs, nameof(outputs));
            Blockers = Copy(blockers, nameof(blockers));
        }

        public BuildingInstanceId InstanceId { get; }
        public BuildingDefinitionId DefinitionId { get; }
        public GridPosition Anchor { get; }
        public int OrientationQuarterTurns { get; }
        public Footprint Footprint { get; }
        public IReadOnlyList<int> AllowedOrientations { get; }
        public bool IsActive { get; }
        public RecipeId? RecipeId { get; }
        public long RecipeDurationTicks { get; }
        public long ProgressTicks { get; }
        public IReadOnlyList<SemanticRecipeAmount> RecipeInputs { get; }
        public IReadOnlyList<SemanticRecipeAmount> RecipeOutputs { get; }
        public IReadOnlyList<SemanticResourceStorage> Inputs { get; }
        public IReadOnlyList<SemanticResourceStorage> Outputs { get; }
        public IReadOnlyList<SemanticBlockerSnapshot> Blockers { get; }

        public bool Equals(SemanticBuildingSnapshot other)
        {
            return other != null &&
                   InstanceId == other.InstanceId &&
                   DefinitionId == other.DefinitionId &&
                   Anchor == other.Anchor &&
                   OrientationQuarterTurns == other.OrientationQuarterTurns &&
                   Footprint == other.Footprint &&
                   SequenceEqual(AllowedOrientations, other.AllowedOrientations) &&
                   IsActive == other.IsActive &&
                   RecipeId == other.RecipeId &&
                   RecipeDurationTicks == other.RecipeDurationTicks &&
                   ProgressTicks == other.ProgressTicks &&
                   SequenceEqual(RecipeInputs, other.RecipeInputs) &&
                   SequenceEqual(RecipeOutputs, other.RecipeOutputs) &&
                   SequenceEqual(Inputs, other.Inputs) &&
                   SequenceEqual(Outputs, other.Outputs) &&
                   SequenceEqual(Blockers, other.Blockers);
        }

        public override bool Equals(object obj) => Equals(obj as SemanticBuildingSnapshot);

        public override int GetHashCode()
        {
            unchecked
            {
                return (((InstanceId.GetHashCode() * 397) ^ DefinitionId.GetHashCode()) * 397) ^ ProgressTicks.GetHashCode();
            }
        }

        private static IReadOnlyList<T> Copy<T>(IEnumerable<T> source, string parameterName)
        {
            if (source == null)
            {
                throw new ArgumentNullException(parameterName);
            }

            var values = source.ToArray();
            if (values.Any(value => ReferenceEquals(value, null)))
            {
                throw new ArgumentException("Semantic building collections must not contain null entries.", parameterName);
            }

            return new ReadOnlyCollection<T>(values);
        }

        private static bool SequenceEqual<T>(IReadOnlyList<T> left, IReadOnlyList<T> right)
            where T : IEquatable<T>
        {
            if (left.Count != right.Count)
            {
                return false;
            }

            for (var index = 0; index < left.Count; index++)
            {
                if (!left[index].Equals(right[index]))
                {
                    return false;
                }
            }

            return true;
        }
    }

    public sealed class SemanticRecipeAmount : IEquatable<SemanticRecipeAmount>
    {
        internal SemanticRecipeAmount(ResourceId resourceId, long amount)
        {
            ResourceId = resourceId;
            Amount = amount;
        }

        public ResourceId ResourceId { get; }
        public long Amount { get; }

        public bool Equals(SemanticRecipeAmount other) =>
            other != null && ResourceId == other.ResourceId && Amount == other.Amount;

        public override bool Equals(object obj) => Equals(obj as SemanticRecipeAmount);

        public override int GetHashCode()
        {
            unchecked
            {
                return (ResourceId.GetHashCode() * 397) ^ Amount.GetHashCode();
            }
        }
    }

    public sealed class SemanticResourceStorage : IEquatable<SemanticResourceStorage>
    {
        internal SemanticResourceStorage(BufferRole role, ResourceId resourceId, long quantity, long capacity)
        {
            Role = role;
            ResourceId = resourceId;
            Quantity = quantity;
            Capacity = capacity;
        }

        public BufferRole Role { get; }
        public ResourceId ResourceId { get; }
        public long Quantity { get; }
        public long Capacity { get; }

        public bool Equals(SemanticResourceStorage other) =>
            other != null && Role == other.Role && ResourceId == other.ResourceId &&
            Quantity == other.Quantity && Capacity == other.Capacity;

        public override bool Equals(object obj) => Equals(obj as SemanticResourceStorage);

        public override int GetHashCode()
        {
            unchecked
            {
                return (((int)Role * 397) ^ ResourceId.GetHashCode()) * 397 ^ Quantity.GetHashCode() ^ Capacity.GetHashCode();
            }
        }
    }

    public sealed class SemanticBlockerSnapshot : IEquatable<SemanticBlockerSnapshot>
    {
        internal SemanticBlockerSnapshot(
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

        public bool Equals(SemanticBlockerSnapshot other) =>
            other != null && Kind == other.Kind && ResourceId == other.ResourceId &&
            Required == other.Required && Available == other.Available;

        public override bool Equals(object obj) => Equals(obj as SemanticBlockerSnapshot);

        public override int GetHashCode()
        {
            unchecked
            {
                return (((int)Kind * 397) ^ ResourceId.GetHashCode()) * 397 ^ Required.GetHashCode() ^ Available.GetHashCode();
            }
        }
    }
}
