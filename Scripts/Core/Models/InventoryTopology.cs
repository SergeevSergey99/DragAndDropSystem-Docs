using System;
using System.Collections.Generic;
using UnityEngine;

namespace UDND.Core
{
    public interface IInventoryTopology
    {
        int CellCount { get; }
        int OrientationCount { get; }
        bool Contains(Vector2Int cell);
        bool TryToIndex(Vector2Int cell, out int index);
        Vector2Int ToCell(int index);
        bool IsValidIndex(int index);
        PlacementOrientation NormalizeOrientation(PlacementOrientation orientation);
        PlacementOrientation Rotate(PlacementOrientation orientation, int steps);
        float GetVisualAngleDegrees(PlacementOrientation orientation);
        PlacementOrientation GetOrientationForVisualAngleDegrees(float angle);
        Vector2Int RotateOffset(
            Vector2Int offset,
            IPlacementShape shape,
            PlacementOrientation from,
            PlacementOrientation to);
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
        public int OrientationCount => 1;

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

        public PlacementOrientation NormalizeOrientation(PlacementOrientation orientation)
            => PlacementOrientation.Step0;

        public PlacementOrientation Rotate(PlacementOrientation orientation, int steps)
            => PlacementOrientation.Step0;

        public float GetVisualAngleDegrees(PlacementOrientation orientation)
            => 0f;

        public PlacementOrientation GetOrientationForVisualAngleDegrees(float angle)
            => PlacementOrientation.Step0;

        public Vector2Int RotateOffset(
            Vector2Int offset,
            IPlacementShape shape,
            PlacementOrientation from,
            PlacementOrientation to)
            => Vector2Int.zero;

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
        public int OrientationCount => 4;
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

        public PlacementOrientation NormalizeOrientation(PlacementOrientation orientation)
            => PlacementOrientationUtility.Normalize(orientation, OrientationCount);

        public PlacementOrientation Rotate(PlacementOrientation orientation, int steps)
            => PlacementOrientationUtility.Rotate(orientation, steps, OrientationCount);

        public float GetVisualAngleDegrees(PlacementOrientation orientation)
            => -90f * (int)NormalizeOrientation(orientation);

        public PlacementOrientation GetOrientationForVisualAngleDegrees(float angle)
            => NormalizeOrientation((PlacementOrientation)Mathf.RoundToInt(-angle / 90f));

        public Vector2Int RotateOffset(
            Vector2Int offset,
            IPlacementShape shape,
            PlacementOrientation from,
            PlacementOrientation to)
        {
            var normalizedFrom = NormalizeOrientation(from);
            var normalizedTo = NormalizeOrientation(to);
            int turns = PlacementOrientationUtility.Distance(
                normalizedFrom,
                normalizedTo,
                OrientationCount);
            var rotated = offset;
            var size = PlacementShapeUtility.GetBoundingSize(shape, normalizedFrom);

            for (int i = 0; i < turns; i++)
            {
                rotated = new Vector2Int(size.y - 1 - rotated.y, rotated.x);
                size = new Vector2Int(size.y, size.x);
            }

            return rotated;
        }

        public IReadOnlyList<Vector2Int> GetPlacementOffsets(
            IPlacementShape shape,
            PlacementOrientation orientation)
        {
            shape ??= RectPlacementShape.One;
            var normalized = NormalizeOrientation(orientation);
            if (!shape.SupportsOrientation(normalized))
                return Array.Empty<Vector2Int>();

            return shape.GetOffsets(normalized) ?? Array.Empty<Vector2Int>();
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
        public int OrientationCount => _inner.OrientationCount;

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

        public PlacementOrientation NormalizeOrientation(PlacementOrientation orientation)
            => _inner.NormalizeOrientation(orientation);

        public PlacementOrientation Rotate(PlacementOrientation orientation, int steps)
            => _inner.Rotate(orientation, steps);

        public float GetVisualAngleDegrees(PlacementOrientation orientation)
            => _inner.GetVisualAngleDegrees(orientation);

        public PlacementOrientation GetOrientationForVisualAngleDegrees(float angle)
            => _inner.GetOrientationForVisualAngleDegrees(angle);

        public Vector2Int RotateOffset(
            Vector2Int offset,
            IPlacementShape shape,
            PlacementOrientation from,
            PlacementOrientation to)
            => _inner.RotateOffset(offset, shape, from, to);

        public IReadOnlyList<Vector2Int> GetPlacementOffsets(
            IPlacementShape shape,
            PlacementOrientation orientation)
            => _inner.GetPlacementOffsets(shape, orientation);

        private int CurrentSlotCount => Math.Max(0, _slotCountProvider());
    }

    public static class PlacementOrientationUtility
    {
        public static PlacementOrientation Normalize(
            PlacementOrientation orientation,
            int orientationCount)
        {
            int count = Math.Max(1, orientationCount);
            int value = ((int)orientation % count + count) % count;
            return (PlacementOrientation)value;
        }

        public static PlacementOrientation Rotate(
            PlacementOrientation orientation,
            int steps,
            int orientationCount)
        {
            int count = Math.Max(1, orientationCount);
            int value = ((int)Normalize(orientation, count) + steps) % count;
            if (value < 0)
                value += count;
            return (PlacementOrientation)value;
        }

        public static int Distance(
            PlacementOrientation from,
            PlacementOrientation to,
            int orientationCount)
        {
            int count = Math.Max(1, orientationCount);
            int distance =
                (int)Normalize(to, count) -
                (int)Normalize(from, count);
            return distance < 0 ? distance + count : distance;
        }
    }
}
