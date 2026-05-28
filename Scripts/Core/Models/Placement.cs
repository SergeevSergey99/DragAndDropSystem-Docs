using System;
using System.Collections.Generic;
using UnityEngine;

namespace UDND.Core
{
    public enum PlacementOrientation : byte
    {
        Rot0 = 0,
        Rot90 = 1,
        Rot180 = 2,
        Rot270 = 3
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

        public bool TryToIndex(Vector2Int cell, out int index)
        {
            if (!Contains(cell))
            {
                index = -1;
                return false;
            }

            index = ToIndex(cell);
            return true;
        }

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
            IPlacementShape shape = null)
        {
            Stack = stack;
            AnchorIndex = anchorIndex;
            Orientation = orientation;
            Shape = shape ?? PlacementShapeUtility.Resolve(stack?.PrimaryAdapter);
            BoundingSize = PlacementShapeUtility.GetBoundingSize(Shape, orientation);
        }

        public static PlacementRequest For(
            ItemStack stack,
            int anchorIndex,
            PlacementOrientation orientation = PlacementOrientation.Rot0)
            => new PlacementRequest(
                stack,
                anchorIndex,
                orientation,
                PlacementShapeUtility.Resolve(stack?.PrimaryAdapter));

        public ItemStack Stack { get; }
        public int AnchorIndex { get; }
        public PlacementOrientation Orientation { get; }
        public IPlacementShape Shape { get; }
        public Vector2Int BoundingSize { get; }
    }

    /// <summary>
    /// Runtime placement record owned by an inventory.
    /// Placement references are short-lived: operations that rebuild occupancy,
    /// such as dynamic slot removal, may orphan old instances and create
    /// replacement placements. Re-resolve through the owning inventory when
    /// current identity matters.
    /// </summary>
    public sealed class Placement
    {
        private int[] _coveredIndices;
        private IReadOnlyList<int> _coveredIndicesView;

        public Placement(
            Vector2Int anchorCell,
            int anchorIndex,
            PlacementOrientation orientation,
            IPlacementShape shape,
            ItemStack stack,
            IReadOnlyList<int> coveredIndices)
        {
            AnchorCell = anchorCell;
            AnchorIndex = anchorIndex;
            Orientation = orientation;
            Shape = shape ?? PlacementShapeUtility.Resolve(stack?.PrimaryAdapter);
            BoundingSize = PlacementShapeUtility.GetBoundingSize(Shape, orientation);
            MutableStack = stack?.CreateCopy() ?? ItemStack.Empty();
            SetCoveredIndices(coveredIndices, anchorIndex);
        }

        public Vector2Int AnchorCell { get; }
        public int AnchorIndex { get; }
        public PlacementOrientation Orientation { get; }
        public IPlacementShape Shape { get; }
        public Vector2Int BoundingSize { get; }
        /// <summary>Read-only view of the inventory-owned stack.</summary>
        public IReadOnlyItemStack Stack => MutableStack;
        internal ItemStack MutableStack { get; }
        public IReadOnlyList<int> CoveredIndices => _coveredIndicesView;

        private void SetCoveredIndices(IReadOnlyList<int> coveredIndices, int fallbackAnchorIndex)
        {
            if (coveredIndices == null || coveredIndices.Count == 0)
            {
                _coveredIndices = new[] { fallbackAnchorIndex };
                _coveredIndicesView = Array.AsReadOnly(_coveredIndices);
                return;
            }

            _coveredIndices = new int[coveredIndices.Count];
            for (int i = 0; i < coveredIndices.Count; i++)
                _coveredIndices[i] = coveredIndices[i];
            _coveredIndicesView = Array.AsReadOnly(_coveredIndices);
        }
    }
}
