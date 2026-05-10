using System.Collections.Generic;
using UnityEngine;
using UniversalDragAndDrop.Core;
using UniversalDragAndDrop.Slots;

namespace UniversalDragAndDrop.Inventories
{
    /// <summary>
    /// Inventory-owned slot stack storage. Mutating live slot stacks should go through this contract.
    /// </summary>
    public interface ISlotStackStore
    {
        bool TryGetStackForSlot(BaseSlot baseSlot, out IReadOnlyItemStack stack);
        bool TrySetStackForSlot(BaseSlot baseSlot, ItemStack stack);
        bool TryClearSlot(BaseSlot baseSlot);
        bool TryGetPlacementAt(BaseSlot baseSlot, out Placement placement);
        Vector2Int GetGrabOffset(Placement placement, BaseSlot baseSlot);
        bool TrySplitFromSlot(BaseSlot baseSlot, int amount, out ItemStack splitStack);
        bool TryAddToSlotStack(BaseSlot baseSlot, ItemStack stack);
        bool TryRemoveFromSlot(BaseSlot baseSlot, IReadOnlyList<IItemAdapter> adapters, out int removed);
    }
}
