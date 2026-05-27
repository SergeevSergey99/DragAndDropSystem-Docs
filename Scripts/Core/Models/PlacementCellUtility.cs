using System;
using System.Collections.Generic;
using UnityEngine;

namespace UDND.Core
{
    public enum PlacementBoundsMode
    {
        RequireAllInBounds = 0,
        IncludeOnlyInBounds = 1
    }

    public static class PlacementCellUtility
    {
        private static readonly IReadOnlyList<int> EmptyIndices = Array.Empty<int>();

        public static IReadOnlyList<int> GetCoveredIndices(
            int anchorIndex,
            IPlacementShape shape,
            PlacementOrientation orientation,
            GridTopology? grid,
            int slotCount,
            PlacementBoundsMode boundsMode)
        {
            if (slotCount <= 0 || anchorIndex < 0)
                return EmptyIndices;

            if (!grid.HasValue)
                return GetCollapsedAnchorIndex(anchorIndex, slotCount);

            var topology = new RectGridTopology(grid.Value);
            if (!topology.IsValidIndex(anchorIndex))
                return EmptyIndices;

            return GetCoveredIndices(
                topology.ToCell(anchorIndex),
                shape,
                orientation,
                topology,
                slotCount,
                boundsMode);
        }

        public static IReadOnlyList<int> GetCoveredIndices(
            Vector2Int anchorCell,
            IPlacementShape shape,
            PlacementOrientation orientation,
            GridTopology? grid,
            int slotCount,
            PlacementBoundsMode boundsMode)
        {
            if (slotCount <= 0)
                return EmptyIndices;

            if (!grid.HasValue)
            {
                int anchorIndex = anchorCell.y == 0 ? anchorCell.x : -1;
                return GetCollapsedAnchorIndex(anchorIndex, slotCount);
            }

            return GetCoveredIndices(
                anchorCell,
                shape,
                orientation,
                new RectGridTopology(grid.Value),
                slotCount,
                boundsMode);
        }

        public static IReadOnlyList<int> GetCoveredIndices(
            int anchorIndex,
            IPlacementShape shape,
            PlacementOrientation orientation,
            IInventoryTopology topology,
            PlacementBoundsMode boundsMode)
        {
            if (topology == null || !topology.IsValidIndex(anchorIndex))
                return EmptyIndices;

            return GetCoveredIndices(
                topology.ToCell(anchorIndex),
                shape,
                orientation,
                topology,
                boundsMode);
        }

        public static IReadOnlyList<int> GetCoveredIndices(
            Vector2Int anchorCell,
            IPlacementShape shape,
            PlacementOrientation orientation,
            IInventoryTopology topology,
            PlacementBoundsMode boundsMode)
        {
            if (topology == null || topology.CellCount <= 0)
                return EmptyIndices;

            return GetCoveredIndicesCore(
                anchorCell,
                shape,
                orientation,
                topology,
                boundsMode);
        }

        private static IReadOnlyList<int> GetCoveredIndices(
            Vector2Int anchorCell,
            IPlacementShape shape,
            PlacementOrientation orientation,
            IInventoryTopology topology,
            int slotCount,
            PlacementBoundsMode boundsMode)
        {
            if (topology == null || slotCount <= 0)
                return EmptyIndices;

            return GetCoveredIndicesCore(
                anchorCell,
                shape,
                orientation,
                new SlotCountLimitedTopology(topology, slotCount),
                boundsMode);
        }

        private static IReadOnlyList<int> GetCoveredIndicesCore(
            Vector2Int anchorCell,
            IPlacementShape shape,
            PlacementOrientation orientation,
            IInventoryTopology topology,
            PlacementBoundsMode boundsMode)
        {
            if (shape == null)
                shape = RectPlacementShape.One;

            if (!shape.SupportsOrientation(orientation))
                return EmptyIndices;

            var offsets = shape.GetOffsets(orientation);
            if (offsets == null || offsets.Count == 0)
                return EmptyIndices;

            var result = new List<int>(offsets.Count);
            for (int i = 0; i < offsets.Count; i++)
            {
                var cell = anchorCell + offsets[i];
                if (!topology.TryToIndex(cell, out int index))
                {
                    if (boundsMode == PlacementBoundsMode.RequireAllInBounds)
                        return EmptyIndices;

                    continue;
                }

                result.Add(index);
            }

            return result.Count > 0 ? result : EmptyIndices;
        }

        private static IReadOnlyList<int> GetCollapsedAnchorIndex(int anchorIndex, int slotCount)
        {
            return anchorIndex >= 0 && anchorIndex < slotCount
                ? new[] { anchorIndex }
                : EmptyIndices;
        }

    }
}
