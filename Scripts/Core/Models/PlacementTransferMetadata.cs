using System;
using System.Collections.Generic;
using UniversalDragAndDrop.Slots;

namespace UniversalDragAndDrop.Core
{
    /// <summary>
    /// Placement-aware metadata for shaped transfers, events, and drop results.
    /// </summary>
    public sealed class PlacementTransferMetadata
    {
        public PlacementTransferMetadata(
            int anchorIndex,
            PlacementOrientation orientation,
            Footprint footprint,
            IReadOnlyList<int> coveredIndices = null,
            BaseSlot anchorBaseSlot = null,
            IReadOnlyList<BaseSlot> coveredBaseSlots = null)
        {
            AnchorIndex = anchorIndex;
            Orientation = orientation;
            Footprint = footprint.Normalized();
            CoveredIndices = Copy(coveredIndices);
            AnchorBaseSlot = anchorBaseSlot;
            CoveredBaseSlots = Copy(coveredBaseSlots);
        }

        public int AnchorIndex { get; }
        public PlacementOrientation Orientation { get; }
        public Footprint Footprint { get; }
        public IReadOnlyList<int> CoveredIndices { get; }
        public BaseSlot AnchorBaseSlot { get; }
        public IReadOnlyList<BaseSlot> CoveredBaseSlots { get; }
        public bool HasCoveredCells => CoveredIndices.Count > 0;

        public static PlacementTransferMetadata FromPlacement(
            Placement placement,
            Func<int, BaseSlot> slotResolver = null)
        {
            if (placement == null)
                return null;

            return new PlacementTransferMetadata(
                placement.AnchorIndex,
                placement.Orientation,
                placement.Footprint,
                placement.CoveredIndices,
                slotResolver?.Invoke(placement.AnchorIndex),
                ResolveSlots(placement.CoveredIndices, slotResolver));
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
