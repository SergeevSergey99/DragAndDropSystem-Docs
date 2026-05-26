using System;
using System.Collections.Generic;
using UnityEngine;
using UDND.Slots;

namespace UDND.Core
{
    /// <summary>
    /// Immutable placement state captured for shaped transfers, events, and drop results.
    /// </summary>
    public sealed class PlacementSnapshot
    {
        public PlacementSnapshot(
            int anchorIndex,
            PlacementOrientation orientation,
            Vector2Int boundingSize,
            IReadOnlyList<int> coveredIndices = null,
            BaseSlot anchorBaseSlot = null,
            IReadOnlyList<BaseSlot> coveredBaseSlots = null,
            IReadOnlyList<Vector2Int> coveredOffsets = null)
        {
            AnchorIndex = anchorIndex;
            Orientation = orientation;
            CoveredIndices = Copy(coveredIndices);
            CoveredOffsets = Copy(coveredOffsets);
            BoundingSize = boundingSize.x > 0 && boundingSize.y > 0 ? boundingSize : Vector2Int.one;
            AnchorBaseSlot = anchorBaseSlot;
            CoveredBaseSlots = Copy(coveredBaseSlots);
        }

        public int AnchorIndex { get; }
        public PlacementOrientation Orientation { get; }
        public IReadOnlyList<int> CoveredIndices { get; }
        public IReadOnlyList<Vector2Int> CoveredOffsets { get; }
        public Vector2Int BoundingSize { get; }
        public BaseSlot AnchorBaseSlot { get; }
        public IReadOnlyList<BaseSlot> CoveredBaseSlots { get; }
        public bool HasCoveredCells => CoveredIndices.Count > 0;

        public static PlacementSnapshot FromPlacement(
            Placement placement,
            Func<int, BaseSlot> slotResolver = null)
        {
            if (placement == null)
                return null;

            return new PlacementSnapshot(
                placement.AnchorIndex,
                placement.Orientation,
                PlacementShapeUtility.GetBoundingSize(placement.Shape, placement.Orientation),
                placement.CoveredIndices,
                slotResolver?.Invoke(placement.AnchorIndex),
                ResolveSlots(placement.CoveredIndices, slotResolver),
                ResolveOffsets(placement.Shape, placement.Orientation));
        }

        private static IReadOnlyList<Vector2Int> ResolveOffsets(
            IPlacementShape shape,
            PlacementOrientation orientation)
        {
            if (shape == null || !shape.SupportsOrientation(orientation))
                return Array.Empty<Vector2Int>();

            return Copy(shape.GetOffsets(orientation));
        }

        private static IReadOnlyList<BaseSlot> ResolveSlots(
            IReadOnlyList<int> coveredIndices,
            Func<int, BaseSlot> slotResolver)
        {
            if (coveredIndices == null || coveredIndices.Count == 0 || slotResolver == null)
                return Array.Empty<BaseSlot>();

            var slots = new BaseSlot[coveredIndices.Count];
            for (int i = 0; i < coveredIndices.Count; i++)
                slots[i] = slotResolver(coveredIndices[i]);
            return slots;
        }

        private static IReadOnlyList<T> Copy<T>(IReadOnlyList<T> source)
        {
            if (source == null || source.Count == 0)
                return Array.Empty<T>();

            var copy = new T[source.Count];
            for (int i = 0; i < source.Count; i++)
                copy[i] = source[i];
            return copy;
        }
    }
}
