using System.Collections.Generic;
using UniversalDragAndDrop.Core;
using UniversalDragAndDrop.Slots;

namespace UniversalDragAndDrop.Inventories
{
    /// <summary>
    /// Responsible for preview validation of whether the inventory can accept items in the current operation context.
    /// </summary>
    public interface IAcceptanceStrategy
    {
        bool CanAcceptItem(List<BaseSlot> slots, InventoryAcceptanceRequest request, bool canCreateNewSlot, int potentialNewSlots, BaseSlot baseSlotPrefab, out BaseSlot suggestedBaseSlot);
        int GetAcceptableCount(List<BaseSlot> slots, InventoryAcceptanceRequest request, bool canCreateNewSlot, int potentialNewSlots, BaseSlot baseSlotPrefab);
    }
}
