using System;

namespace IdleFactory.Unity.Presentation
{
    /// <summary>
    /// The stable identity of a building location in the world (Feature 006, data-model.md BuildingLocationId, FR-038). Today it equals the
    /// slot index. A position is always looked up from the id through <see cref="FactoryWorldLayout"/> and is never used as identity.
    /// </summary>
    public readonly struct BuildingLocationId : IEquatable<BuildingLocationId>, IComparable<BuildingLocationId>
    {
        public BuildingLocationId(int value)
        {
            Value = value;
        }

        public int Value { get; }

        public bool Equals(BuildingLocationId other) => Value == other.Value;

        public override bool Equals(object obj) => obj is BuildingLocationId other && Equals(other);

        public override int GetHashCode() => Value;

        public int CompareTo(BuildingLocationId other) => Value.CompareTo(other.Value);

        public override string ToString() => "location#" + Value;

        public static bool operator ==(BuildingLocationId left, BuildingLocationId right) => left.Equals(right);

        public static bool operator !=(BuildingLocationId left, BuildingLocationId right) => !left.Equals(right);
    }
}
