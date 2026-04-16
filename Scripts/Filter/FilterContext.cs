using System.Collections.Generic;
using UniversalDragAndDrop.Inventories;
using UniversalDragAndDrop.Slots;

namespace UniversalDragAndDrop.Filter
{
    /// <summary>
    /// Context passed to filters and sorters during evaluation.
    /// Provides full access to the slot, its inventory, and sibling slots.
    /// </summary>
    public readonly struct FilterContext
    {
        /// <summary>Slot being evaluated.</summary>
        public readonly BaseSlot Slot;

        /// <summary>Inventory that owns the slot.</summary>
        public readonly UniversalInventory Inventory;

        /// <summary>All slots in the inventory (for cross-slot logic like "first unique only").</summary>
        public readonly IReadOnlyList<BaseSlot> AllSlots;

        /// <summary>Index of <see cref="Slot"/> within <see cref="AllSlots"/>.</summary>
        public readonly int SlotIndex;

        public FilterContext(BaseSlot slot, UniversalInventory inventory, IReadOnlyList<BaseSlot> allSlots, int slotIndex)
        {
            Slot = slot;
            Inventory = inventory;
            AllSlots = allSlots;
            SlotIndex = slotIndex;
        }
    }
}
