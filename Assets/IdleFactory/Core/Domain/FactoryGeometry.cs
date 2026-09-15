using System;
using System.Collections.Generic;

namespace IdleFactory.Domain
{
    public readonly struct GridPosition : IEquatable<GridPosition>
    {
        public GridPosition(int x, int y)
        {
            X = x;
            Y = y;
        }

        public int X { get; }
        public int Y { get; }

        public GridPosition Offset(int xOffset, int yOffset)
        {
            return new GridPosition(checked(X + xOffset), checked(Y + yOffset));
        }

        public bool Equals(GridPosition other) => X == other.X && Y == other.Y;
        public override bool Equals(object obj) => obj is GridPosition other && Equals(other);

        public override int GetHashCode()
        {
            unchecked
            {
                return (X * 397) ^ Y;
            }
        }

        public override string ToString() => $"({X}, {Y})";
        public static bool operator ==(GridPosition left, GridPosition right) => left.Equals(right);
        public static bool operator !=(GridPosition left, GridPosition right) => !left.Equals(right);
    }

    public readonly struct Footprint : IEquatable<Footprint>
    {
        public Footprint(int width, int height)
        {
            if (width <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(width), width, "Footprint width must be positive.");
            }

            if (height <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(height), height, "Footprint height must be positive.");
            }

            Width = width;
            Height = height;
        }

        public int Width { get; }
        public int Height { get; }

        public IEnumerable<GridPosition> OccupiedCells(GridPosition anchor, int orientationQuarterTurns)
        {
            EnsureInitialized();

            int orientedWidth;
            int orientedHeight;
            switch (orientationQuarterTurns)
            {
                case 0:
                case 2:
                    orientedWidth = Width;
                    orientedHeight = Height;
                    break;
                case 1:
                case 3:
                    orientedWidth = Height;
                    orientedHeight = Width;
                    break;
                default:
                    throw new ArgumentOutOfRangeException(
                        nameof(orientationQuarterTurns),
                        orientationQuarterTurns,
                        "Orientation must be one of the four quarter turns 0, 1, 2, or 3.");
            }

            // Validate the far corner before yielding so overflow cannot expose a partial footprint.
            checked
            {
                _ = anchor.X + orientedWidth - 1;
                _ = anchor.Y + orientedHeight - 1;
            }

            return EnumerateCells(anchor, orientedWidth, orientedHeight);
        }

        private static IEnumerable<GridPosition> EnumerateCells(
            GridPosition anchor,
            int orientedWidth,
            int orientedHeight)
        {
            for (var yOffset = 0; yOffset < orientedHeight; yOffset++)
            {
                for (var xOffset = 0; xOffset < orientedWidth; xOffset++)
                {
                    yield return anchor.Offset(xOffset, yOffset);
                }
            }
        }

        private void EnsureInitialized()
        {
            if (Width <= 0 || Height <= 0)
            {
                throw new InvalidOperationException("The footprint is uninitialized.");
            }
        }

        public bool Equals(Footprint other) => Width == other.Width && Height == other.Height;
        public override bool Equals(object obj) => obj is Footprint other && Equals(other);

        public override int GetHashCode()
        {
            unchecked
            {
                return (Width * 397) ^ Height;
            }
        }

        public override string ToString() => $"{Width}x{Height}";
        public static bool operator ==(Footprint left, Footprint right) => left.Equals(right);
        public static bool operator !=(Footprint left, Footprint right) => !left.Equals(right);
    }
}
