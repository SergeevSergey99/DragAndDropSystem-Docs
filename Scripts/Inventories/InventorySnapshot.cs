using System.Collections.Generic;
using UniversalDragAndDrop.Core;

namespace UniversalDragAndDrop.Inventories
{
    /// <summary>
    /// Snapshot of inventory state. Stores the contents of each slot and their counts.
    /// </summary>
    public sealed class InventorySnapshot
    {
        public InventorySnapshot(List<InventorySlotState> slots)
        {
            Slots = slots ?? new List<InventorySlotState>();
        }

        public List<InventorySlotState> Slots { get; }
    }

    /// <summary>
    /// Snapshot of a single slot.
    /// </summary>
    public struct InventorySlotState
    {
        public InventorySlotState(IReadOnlyList<IItemAdapter> adapters)
        {
            Adapters = adapters ?? System.Array.Empty<IItemAdapter>();
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