using System;
using System.Collections.Generic;
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
        IReadOnlyList<Vector2Int> GetPlacementOffsets(
            IPlacementShape shape,
            PlacementOrientation orientation);
    }

    public readonly struct SlotTopology : IInventoryTopology, IEquatable<SlotTopology>
    {
        private static readonly IReadOnlyList<Vector2Int> AnchorOnlyOffsets =
            Array.AsReadOnly(new[] { Vector2Int.zero });

        private readonly int _slotCount;
        private readonly Func<int> _slotCountProvider;

        public SlotTopology(int slotCount)
        {
            _slotCount = Math.Max(0, slotCount);
            _slotCountProvider = null;
        }

        public SlotTopology(Func<int> slotCountProvider)
        {
            _slotCount = 0;
            _slotCountProvider = slotCountProvider;
        }

        public int CellCount => _slotCountProvider != null
            ? Math.Max(0, _slotCountProvider())
            : _slotCount;

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

        public IReadOnlyList<Vector2Int> GetPlacementOffsets(
            IPlacementShape shape,
            PlacementOrientation orientation)
            => AnchorOnlyOffsets;

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

        public IReadOnlyList<Vector2Int> GetPlacementOffsets(
            IPlacementShape shape,
            PlacementOrientation orientation)
        {
            shape ??= RectPlacementShape.One;
            if (!shape.SupportsOrientation(orientation))
                return Array.Empty<Vector2Int>();

            return shape.GetOffsets(orientation) ?? Array.Empty<Vector2Int>();
        }

        public bool Equals(RectGridTopology other)
            => _grid.Equals(other._grid);

        public override bool Equals(object obj)
            => obj is RectGridTopology other && Equals(other);

        public override int GetHashCode()
            => _grid.GetHashCode();

        public override string ToString()
            => _grid.ToString();
    }

    public sealed class SlotCountLimitedTopology : IInventoryTopology
    {
        private readonly IInventoryTopology _inner;
        private readonly Func<int> _slotCountProvider;

        public SlotCountLimitedTopology(IInventoryTopology inner, int slotCount)
            : this(inner, () => slotCount)
        {
        }

        public SlotCountLimitedTopology(IInventoryTopology inner, Func<int> slotCountProvider)
        {
            _inner = inner ?? new SlotTopology(0);
            _slotCountProvider = slotCountProvider ?? (() => _inner.CellCount);
        }

        public int CellCount => Math.Min(_inner.CellCount, CurrentSlotCount);

        public bool Contains(Vector2Int cell)
            => TryToIndex(cell, out _);

        public bool TryToIndex(Vector2Int cell, out int index)
        {
            if (!_inner.TryToIndex(cell, out index) || index < 0 || index >= CurrentSlotCount)
            {
                index = -1;
                return false;
            }

            return true;
        }

        public Vector2Int ToCell(int index)
            => _inner.ToCell(index);

        public bool IsValidIndex(int index)
            => index >= 0 && index < CurrentSlotCount && _inner.IsValidIndex(index);

        public IReadOnlyList<Vector2Int> GetPlacementOffsets(
            IPlacementShape shape,
            PlacementOrientation orientation)
            => _inner.GetPlacementOffsets(shape, orientation);

        private int CurrentSlotCount => Math.Max(0, _slotCountProvider());
    }
}
