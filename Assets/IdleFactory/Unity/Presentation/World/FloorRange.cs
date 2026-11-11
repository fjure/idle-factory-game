using System;
using System.Collections.Generic;
using IdleFactory.Run.Application;
using UnityEngine;
using UnityEngine.Rendering;

namespace IdleFactory.Unity.Presentation
{
    /// <summary>An inclusive block of floor cells (see <see cref="SlotGridLayout.FloorCellsFor"/>).</summary>
    public readonly struct FloorRange : IEquatable<FloorRange>
    {
        public static readonly FloorRange Empty = new FloorRange(0, -1, 0, -1);

        public FloorRange(int minColumn, int maxColumn, int minRow, int maxRow)
        {
            MinColumn = minColumn;
            MaxColumn = maxColumn;
            MinRow = minRow;
            MaxRow = maxRow;
        }

        public int MinColumn { get; }
        public int MaxColumn { get; }
        public int MinRow { get; }
        public int MaxRow { get; }

        public int ColumnCount => Math.Max(0, MaxColumn - MinColumn + 1);
        public int RowCount => Math.Max(0, MaxRow - MinRow + 1);
        public int Count => ColumnCount * RowCount;
        public bool IsEmpty => Count == 0;

        public bool Equals(FloorRange other) =>
            (IsEmpty && other.IsEmpty) ||
            (MinColumn == other.MinColumn && MaxColumn == other.MaxColumn && MinRow == other.MinRow && MaxRow == other.MaxRow);

        public override bool Equals(object obj) => obj is FloorRange other && Equals(other);

        public override int GetHashCode() => IsEmpty ? 0 : ((MinColumn * 397 ^ MaxColumn) * 397 ^ MinRow) * 397 ^ MaxRow;
    }
}
