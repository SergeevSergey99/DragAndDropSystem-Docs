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
                return anchorIndex < slotCount
                    ? new[] { anchorIndex }
                    : EmptyIndices;

            var topology = grid.Value.Normalized();
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
                return anchorIndex >= 0 && anchorIndex < slotCount
                    ? new[] { anchorIndex }
                    : EmptyIndices;
            }

            return GetCoveredIndices(
                anchorCell,
                shape,
                orientation,
                grid.Value.Normalized(),
                slotCount,
                boundsMode);
        }

        private static IReadOnlyList<int> GetCoveredIndices(
            Vector2Int anchorCell,
            IPlacementShape shape,
            PlacementOrientation orientation,
            GridTopology topology,
            int slotCount,
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
                if (!topology.Contains(cell))
                {
                    if (boundsMode == PlacementBoundsMode.RequireAllInBounds)
                        return EmptyIndices;

                    continue;
                }

                int index = topology.ToIndex(cell);
                if (index < 0 || index >= slotCount)
                {
                    if (boundsMode == PlacementBoundsMode.RequireAllInBounds)
                        return EmptyIndices;

                    continue;
                }

                result.Add(index);
            }

            return result.Count > 0 ? result : EmptyIndices;
        }
    }
}
