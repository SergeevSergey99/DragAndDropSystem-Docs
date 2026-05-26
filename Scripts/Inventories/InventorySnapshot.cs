using System.Collections.Generic;
using UnityEngine;
using UDND.Core;

namespace UDND.Inventories
{
    /// <summary>
    /// Inventory state snapshot used for rollback. Restoration reads <see cref="Placements"/>
    /// only — placements are the single source of truth. <see cref="SlotCount"/> records
    /// how many slots existed at capture time so dynamic inventories can shrink/grow back.
    /// </summary>
    public sealed class InventorySnapshot
    {
        public InventorySnapshot(int slotCount, IReadOnlyList<InventoryPlacementState> placements = null)
        {
            SlotCount = slotCount < 0 ? 0 : slotCount;
            Placements = placements == null || placements.Count == 0
                ? System.Array.Empty<InventoryPlacementState>()
                : Copy(placements);
        }

        public int SlotCount { get; }
        public IReadOnlyList<InventoryPlacementState> Placements { get; }

        private static InventoryPlacementState[] Copy(IReadOnlyList<InventoryPlacementState> placements)
        {
            var copy = new InventoryPlacementState[placements.Count];
            for (int i = 0; i < placements.Count; i++)
                copy[i] = placements[i];
            return copy;
        }
    }

    /// <summary>
    /// Snapshot of a placement transaction.
    /// For 1x1 slot inventories this is equivalent to a single occupied slot,
    /// but it also preserves placement metadata needed by grid inventories.
    /// </summary>
    public struct InventoryPlacementState
    {
        public InventoryPlacementState(
            int anchorIndex,
            IReadOnlyList<IItemAdapter> adapters,
            PlacementOrientation orientation,
            Footprint footprint,
            IReadOnlyList<int> coveredIndices = null,
            IReadOnlyList<Vector2Int> coveredOffsets = null,
            Vector2Int? boundingSize = null)
        {
            AnchorIndex = anchorIndex;
            Adapters = adapters == null || adapters.Count == 0
                ? System.Array.Empty<IItemAdapter>()
                : Copy(adapters);
            Orientation = orientation;
            Footprint = footprint.Normalized();
            CoveredIndices = coveredIndices == null || coveredIndices.Count == 0
                ? System.Array.Empty<int>()
                : Copy(coveredIndices);
            CoveredOffsets = coveredOffsets == null || coveredOffsets.Count == 0
                ? System.Array.Empty<Vector2Int>()
                : Copy(coveredOffsets);
            BoundingSize = boundingSize ?? Footprint.GetSize(orientation);
        }

        public int AnchorIndex { get; }
        public IReadOnlyList<IItemAdapter> Adapters { get; }
        public PlacementOrientation Orientation { get; }
        public Footprint Footprint { get; }
        public IReadOnlyList<int> CoveredIndices { get; }
        public IReadOnlyList<Vector2Int> CoveredOffsets { get; }
        public Vector2Int BoundingSize { get; }
        public bool IsEmpty => Adapters == null || Adapters.Count <= 0;

        private static T[] Copy<T>(IReadOnlyList<T> items)
        {
            var copy = new T[items.Count];
            for (int i = 0; i < items.Count; i++)
                copy[i] = items[i];
            return copy;
        }
    }

    /// <summary>
    /// Interface for inventories capable of creating state snapshots (for rollback operations).
    /// </summary>
    public interface IInventorySnapshotProvider
    {
        InventorySnapshot CaptureSnapshot();
        void RestoreSnapshot(InventorySnapshot snapshot);
    }
}
