using System;
using IdleFactory.Domain;
using UnityEngine;

namespace IdleFactory.Unity.Presentation
{
    /// <summary>
    /// Maps logical cells to the XY world plane. World origin is the lower-left corner of logical
    /// cell (0,0); exact cell boundaries belong to the cell on their positive side.
    /// </summary>
    public sealed class GridWorldMapper
    {
        public GridWorldMapper(float cellSize, Vector2 worldOrigin)
        {
            if (!IsFinite(cellSize) || cellSize <= 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(cellSize));
            }

            if (!IsFinite(worldOrigin.x) || !IsFinite(worldOrigin.y))
            {
                throw new ArgumentOutOfRangeException(nameof(worldOrigin));
            }

            CellSize = cellSize;
            WorldOrigin = worldOrigin;
        }

        public float CellSize { get; }
        public Vector2 WorldOrigin { get; }

        public Vector2 CellOrigin(GridPosition position)
        {
            return new Vector2(
                WorldOrigin.x + position.X * CellSize,
                WorldOrigin.y + position.Y * CellSize);
        }

        public Vector2 CellCenter(GridPosition position)
        {
            return CellOrigin(position) + Vector2.one * (CellSize * 0.5f);
        }

        public GridPosition WorldToGrid(Vector2 worldPosition)
        {
            if (!IsFinite(worldPosition.x) || !IsFinite(worldPosition.y))
            {
                throw new ArgumentOutOfRangeException(nameof(worldPosition));
            }

            var x = Math.Floor(((double)worldPosition.x - WorldOrigin.x) / CellSize);
            var y = Math.Floor(((double)worldPosition.y - WorldOrigin.y) / CellSize);
            if (x < int.MinValue || x > int.MaxValue || y < int.MinValue || y > int.MaxValue)
            {
                throw new OverflowException("World position lies outside the logical Int32 grid range.");
            }

            return new GridPosition((int)x, (int)y);
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }
}
