using System.Collections.Generic;
using UniversalDragAndDrop.Core;

namespace UniversalDragAndDrop.Inventories
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
            IReadOnlyList<int> coveredIndices = null)
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
        }

        public int AnchorIndex { get; }
        public IReadOnlyList<IItemAdapter> Adapters { get; }
        public PlacementOrientation Orientation { get; }
        public Footprint Footprint { get; }
        public IReadOnlyList<int> CoveredIndices { get; }
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
    /// Snapshot of a single slot.
    /// </summary>
    public struct InventorySlotState
    {
        public InventorySlotState(IReadOnlyList<IItemAdapter> adapters)
        {
            // Snapshot must be independent of subsequent ItemStack mutations (Split/RemoveFromStack
            // operate on the same backing list that ItemStack.Adapters returns).
            if (adapters == null || adapters.Count == 0)
            {
                Adapters = System.Array.Empty<IItemAdapter>();
                return;
            }

            var copy = new IItemAdapter[adapters.Count];
            for (int i = 0; i < adapters.Count; i++)
                copy[i] = adapters[i];
            Adapters = copy;
        }

        public IReadOnlyList<IItemAdapter> Adapters;

        public IItemAdapter ItemAdapter => Adapters.Count > 0 ? Adapters[0] : null;
        public int Count => Adapters.Count;

        public bool IsEmpty => Adapters == null || Adapters.Count <= 0;
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
