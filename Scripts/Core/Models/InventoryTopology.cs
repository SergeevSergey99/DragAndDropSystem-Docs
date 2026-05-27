using System;
using UnityEngine;

namespace UDND.Core
{
    public interface IInventoryTopology
    {
        int CellCount { get; }
        bool Contains(Vector2Int cell);
        bool TryToIndex(Vector2Int cell, out int index);
        Vector2Int ToCell(int index);
        bool IsValidIndex(int index);
    }

    public readonly struct SlotTopology : IInventoryTopology, IEquatable<SlotTopology>
    {
        public SlotTopology(int slotCount)
        {
            CellCount = Math.Max(0, slotCount);
        }

        public int CellCount { get; }

        public bool Contains(Vector2Int cell)
            => cell.y == 0 && cell.x >= 0 && cell.x < CellCount;

        public bool TryToIndex(Vector2Int cell, out int index)
        {
            if (!Contains(cell))
            {
                index = -1;
                return false;
            }

            index = cell.x;
            return true;
        }

        public Vector2Int ToCell(int index)
            => new Vector2Int(index, 0);

        public bool IsValidIndex(int index)
            => index >= 0 && index < CellCount;

        public bool Equals(SlotTopology other)
            => CellCount == other.CellCount;

        public override bool Equals(object obj)
            => obj is SlotTopology other && Equals(other);

        public override int GetHashCode()
            => CellCount;

        public override string ToString()
            => $"Slots:{CellCount}";
    }

    public readonly struct RectGridTopology : IInventoryTopology, IEquatable<RectGridTopology>
    {
        private readonly GridTopology _grid;

        public RectGridTopology(GridTopology grid)
        {
            _grid = grid.Normalized();
        }

        public RectGridTopology(int columns, int rows)
            : this(new GridTopology(columns, rows))
        {
        }

        public int Columns => _grid.Columns;
        public int Rows => _grid.Rows;
        public int CellCount => _grid.CellCount;
        public GridTopology Grid => _grid;

        public bool Contains(Vector2Int cell)
            => _grid.Contains(cell);

        public bool TryToIndex(Vector2Int cell, out int index)
        {
            if (!_grid.Contains(cell))
            {
                index = -1;
                return false;
            }

            index = _grid.ToIndex(cell);
            return true;
        }

        public Vector2Int ToCell(int index)
            => _grid.ToCell(index);

        public bool IsValidIndex(int index)
            => _grid.IsValidIndex(index);

        public bool Equals(RectGridTopology other)
            => _grid.Equals(other._grid);

        public override bool Equals(object obj)
            => obj is RectGridTopology other && Equals(other);

        public override int GetHashCode()
            => _grid.GetHashCode();

        public override string ToString()
            => _grid.ToString();
    }
}
