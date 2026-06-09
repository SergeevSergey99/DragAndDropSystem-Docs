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

        private static IReadOnlyList<int> GetCoveredIndicesCore(
            Vector2Int anchorCell,
            IPlacementShape shape,
            PlacementOrientation orientation,
            IInventoryTopology topology,
            PlacementBoundsMode boundsMode)
        {
            var offsets = topology.GetPlacementOffsets(shape, orientation);
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

    }
}
