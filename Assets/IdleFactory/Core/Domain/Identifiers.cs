using System;
using System.Text;

namespace IdleFactory.Domain
{
    internal static class IdentifierNormalization
    {
        public static string Normalize(string value, string parameterName)
        {
            if (value == null)
            {
                throw new ArgumentNullException(parameterName);
            }

            var normalized = value.Trim().Normalize(NormalizationForm.FormC);
            if (normalized.Length == 0)
            {
                throw new ArgumentException("Identifier must not be empty or whitespace.", parameterName);
            }

            return normalized;
        }

        public static Guid ParseUuid(string value, string parameterName)
        {
            var normalized = Normalize(value, parameterName);
            Guid parsed;
            if (!Guid.TryParseExact(normalized, "D", out parsed) || parsed == Guid.Empty)
            {
                throw new ArgumentException("Identifier must be a non-empty UUID in canonical dashed form.", parameterName);
            }

            return parsed;
        }
    }

    public readonly struct ResourceId : IEquatable<ResourceId>, IComparable<ResourceId>
    {
        private readonly string value;

        public ResourceId(string value)
        {
            this.value = IdentifierNormalization.Normalize(value, nameof(value));
        }

        public string Value => value ?? throw new InvalidOperationException("The resource identifier is uninitialized.");

        public int CompareTo(ResourceId other) => StringComparer.Ordinal.Compare(value, other.value);
        public bool Equals(ResourceId other) => StringComparer.Ordinal.Equals(value, other.value);
        public override bool Equals(object obj) => obj is ResourceId other && Equals(other);
        public override int GetHashCode() => value == null ? 0 : StringComparer.Ordinal.GetHashCode(value);
        public override string ToString() => value ?? string.Empty;
        public static bool operator ==(ResourceId left, ResourceId right) => left.Equals(right);
        public static bool operator !=(ResourceId left, ResourceId right) => !left.Equals(right);
    }

    public readonly struct RecipeId : IEquatable<RecipeId>, IComparable<RecipeId>
    {
        private readonly string value;

        public RecipeId(string value)
        {
            this.value = IdentifierNormalization.Normalize(value, nameof(value));
        }

        public string Value => value ?? throw new InvalidOperationException("The recipe identifier is uninitialized.");

        public int CompareTo(RecipeId other) => StringComparer.Ordinal.Compare(value, other.value);
        public bool Equals(RecipeId other) => StringComparer.Ordinal.Equals(value, other.value);
        public override bool Equals(object obj) => obj is RecipeId other && Equals(other);
        public override int GetHashCode() => value == null ? 0 : StringComparer.Ordinal.GetHashCode(value);
        public override string ToString() => value ?? string.Empty;
        public static bool operator ==(RecipeId left, RecipeId right) => left.Equals(right);
        public static bool operator !=(RecipeId left, RecipeId right) => !left.Equals(right);
    }

    public readonly struct BuildingDefinitionId : IEquatable<BuildingDefinitionId>, IComparable<BuildingDefinitionId>
    {
        private readonly string value;

        public BuildingDefinitionId(string value)
        {
            this.value = IdentifierNormalization.Normalize(value, nameof(value));
        }

        public string Value => value ?? throw new InvalidOperationException("The building definition identifier is uninitialized.");

        public int CompareTo(BuildingDefinitionId other) => StringComparer.Ordinal.Compare(value, other.value);
        public bool Equals(BuildingDefinitionId other) => StringComparer.Ordinal.Equals(value, other.value);
        public override bool Equals(object obj) => obj is BuildingDefinitionId other && Equals(other);
        public override int GetHashCode() => value == null ? 0 : StringComparer.Ordinal.GetHashCode(value);
        public override string ToString() => value ?? string.Empty;
        public static bool operator ==(BuildingDefinitionId left, BuildingDefinitionId right) => left.Equals(right);
        public static bool operator !=(BuildingDefinitionId left, BuildingDefinitionId right) => !left.Equals(right);
    }

    public readonly struct BuildingInstanceId : IEquatable<BuildingInstanceId>, IComparable<BuildingInstanceId>
    {
        private readonly Guid value;

        public BuildingInstanceId(string value)
        {
            this.value = IdentifierNormalization.ParseUuid(value, nameof(value));
        }

        public BuildingInstanceId(Guid value)
        {
            if (value == Guid.Empty)
            {
                throw new ArgumentException("Identifier UUID must not be empty.", nameof(value));
            }

            this.value = value;
        }

        public string Value => Uuid.ToString("D");
        public Guid Uuid => value == Guid.Empty
            ? throw new InvalidOperationException("The building instance identifier is uninitialized.")
            : value;

        public int CompareTo(BuildingInstanceId other) => value.CompareTo(other.value);
        public bool Equals(BuildingInstanceId other) => value.Equals(other.value);
        public override bool Equals(object obj) => obj is BuildingInstanceId other && Equals(other);
        public override int GetHashCode() => value.GetHashCode();
        public override string ToString() => value == Guid.Empty ? string.Empty : value.ToString("D");
        public static bool operator ==(BuildingInstanceId left, BuildingInstanceId right) => left.Equals(right);
        public static bool operator !=(BuildingInstanceId left, BuildingInstanceId right) => !left.Equals(right);
    }

    public readonly struct CheckpointId : IEquatable<CheckpointId>, IComparable<CheckpointId>
    {
        private readonly Guid value;

        public CheckpointId(string value)
        {
            this.value = IdentifierNormalization.ParseUuid(value, nameof(value));
        }

        public CheckpointId(Guid value)
        {
            if (value == Guid.Empty)
            {
                throw new ArgumentException("Identifier UUID must not be empty.", nameof(value));
            }

            this.value = value;
        }

        public string Value => Uuid.ToString("D");
        public Guid Uuid => value == Guid.Empty
            ? throw new InvalidOperationException("The checkpoint identifier is uninitialized.")
            : value;

        public int CompareTo(CheckpointId other) => value.CompareTo(other.value);
        public bool Equals(CheckpointId other) => value.Equals(other.value);
        public override bool Equals(object obj) => obj is CheckpointId other && Equals(other);
        public override int GetHashCode() => value.GetHashCode();
        public override string ToString() => value == Guid.Empty ? string.Empty : value.ToString("D");
        public static bool operator ==(CheckpointId left, CheckpointId right) => left.Equals(right);
        public static bool operator !=(CheckpointId left, CheckpointId right) => !left.Equals(right);
    }

    public readonly struct OfflinePolicyRevision : IEquatable<OfflinePolicyRevision>, IComparable<OfflinePolicyRevision>
    {
        private readonly int value;

        public OfflinePolicyRevision(int value)
        {
            if (value <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(value), value, "Offline policy revision must be positive.");
            }

            this.value = value;
        }

        public int Value
        {
            get
            {
                EnsureInitialized();
                return value;
            }
        }

        public int CompareTo(OfflinePolicyRevision other)
        {
            EnsureInitialized();
            other.EnsureInitialized();
            return value.CompareTo(other.value);
        }

        public bool Equals(OfflinePolicyRevision other)
        {
            EnsureInitialized();
            other.EnsureInitialized();
            return value == other.value;
        }

        public override bool Equals(object obj)
        {
            EnsureInitialized();
            return obj is OfflinePolicyRevision other && Equals(other);
        }

        public override int GetHashCode()
        {
            EnsureInitialized();
            return value;
        }

        public override string ToString() => Value.ToString();
        public static bool operator ==(OfflinePolicyRevision left, OfflinePolicyRevision right) => left.Equals(right);
        public static bool operator !=(OfflinePolicyRevision left, OfflinePolicyRevision right) => !left.Equals(right);

        private void EnsureInitialized()
        {
            if (value <= 0)
            {
                throw new InvalidOperationException("The offline policy revision is uninitialized.");
            }
        }
    }
}
