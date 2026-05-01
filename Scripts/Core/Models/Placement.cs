using System;
using System.Collections.Generic;
using UnityEngine;

namespace UniversalDragAndDrop.Core
{
    public interface IItemFootprintProvider
    {
        Footprint Footprint { get; }
    }

    [Serializable]
    public struct Footprint : IEquatable<Footprint>
    {
        [SerializeField] private int _width;
        [SerializeField] private int _height;

        public Footprint(int width, int height)
        {
            _width = Math.Max(1, width);
            _height = Math.Max(1, height);
        }

        public int Width => Math.Max(1, _width);
        public int Height => Math.Max(1, _height);
        public bool IsSingleCell => Width == 1 && Height == 1;

        public static Footprint One => new Footprint(1, 1);

        public static Footprint Resolve(IItemAdapter itemAdapter)
        {
            if (itemAdapter is IItemFootprintProvider provider)
                return provider.Footprint.Normalized();

            return One;
        }

        public Footprint Normalized() => new Footprint(Width, Height);

        public Vector2Int GetSize(PlacementOrientation orientation)
            => orientation == PlacementOrientation.Rot90
                ? new Vector2Int(Height, Width)
                : new Vector2Int(Width, Height);

        public bool Equals(Footprint other) => Width == other.Width && Height == other.Height;
        public override bool Equals(object obj) => obj is Footprint other && Equals(other);
        public override int GetHashCode() => (Width * 397) ^ Height;
        public override string ToString() => $"{Width}x{Height}";
    }

    public enum PlacementOrientation : byte
    {
        Rot0 = 0,
        Rot90 = 1
    }

    public enum SlotShapedItemPolicy : byte
    {
        Accept = 0,
        Reject = 1
    }

    [Serializable]
    public struct GridTopology : IEquatable<GridTopology>
    {
        [SerializeField] private int _columns;
        [SerializeField] private int _rows;

        public GridTopology(int columns, int rows)
        {
            _columns = Math.Max(1, columns);
            _rows = Math.Max(1, rows);
        }

        public int Columns => Math.Max(1, _columns);
        public int Rows => Math.Max(1, _rows);
        public int CellCount => Columns * Rows;

        public GridTopology Normalized() => new GridTopology(Columns, Rows);

        public bool IsValidIndex(int index) => index >= 0 && index < CellCount;
        public int ToIndex(Vector2Int cell) => cell.y * Columns + cell.x;
        public Vector2Int ToCell(int index) => new Vector2Int(index % Columns, index / Columns);

        public bool Contains(Vector2Int cell)
            => cell.x >= 0 && cell.y >= 0 && cell.x < Columns && cell.y < Rows;

        public bool Equals(GridTopology other) => Columns == other.Columns && Rows == other.Rows;
        public override bool Equals(object obj) => obj is GridTopology other && Equals(other);
        public override int GetHashCode() => (Columns * 397) ^ Rows;
        public override string ToString() => $"{Columns}x{Rows}";
    }

    public readonly struct PlacementRequest
    {
        public PlacementRequest(
            ItemStack stack,
            int anchorIndex,
            PlacementOrientation orientation = PlacementOrientation.Rot0,
            Footprint? footprint = null)
        {
            Stack = stack;
            AnchorIndex = anchorIndex;
            Orientation = orientation;
            Footprint = footprint.HasValue
                ? footprint.Value.Normalized()
                : UniversalDragAndDrop.Core.Footprint.Resolve(stack?.PrimaryAdapter);
        }

        public ItemStack Stack { get; }
        public int AnchorIndex { get; }
        public PlacementOrientation Orientation { get; }
        public Footprint Footprint { get; }
    }

    public sealed class Placement
    {
        private readonly List<int> _coveredIndices;

        public Placement(
            Vector2Int anchorCell,
            int anchorIndex,
            PlacementOrientation orientation,
            Footprint footprint,
            ItemStack stack,
            IReadOnlyList<int> coveredIndices)
        {
            AnchorCell = anchorCell;
            AnchorIndex = anchorIndex;
            Orientation = orientation;
            Footprint = footprint.Normalized();
            Stack = stack ?? ItemStack.Empty();
            _coveredIndices = coveredIndices != null
                ? new List<int>(coveredIndices)
                : new List<int> { anchorIndex };
        }

        public Vector2Int AnchorCell { get; private set; }
        public int AnchorIndex { get; private set; }
        public PlacementOrientation Orientation { get; }
        public Footprint Footprint { get; }
        public ItemStack Stack { get; }
        public IReadOnlyList<int> CoveredIndices => _coveredIndices;

        internal void MoveAnchor(Vector2Int anchorCell, int anchorIndex, IReadOnlyList<int> coveredIndices)
        {
            AnchorCell = anchorCell;
            AnchorIndex = anchorIndex;
            _coveredIndices.Clear();
            if (coveredIndices != null)
                _coveredIndices.AddRange(coveredIndices);
            else
                _coveredIndices.Add(anchorIndex);
        }
    }
}
