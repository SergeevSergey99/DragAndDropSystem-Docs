using System.Collections.Generic;
using UDND.Core;
using UDND.Slots;

namespace UDND.Inventories
{
    /// <summary>
    /// Responsible for preview validation of whether the inventory can accept items in the current operation context.
    /// </summary>
    public interface IAcceptanceStrategy
    {
        SlotAcceptanceCandidates GetSlotCandidates(IReadOnlyList<ISlot> slots, InventoryAcceptanceRequest request, bool canCreateNewSlot, int potentialNewSlots, BaseSlot baseSlotPrefab);
        SlotSelectionPolicyBase DefaultSlotSelectionPolicy { get; }
        int GetAcceptableCount(List<BaseSlot> slots, InventoryAcceptanceRequest request, bool canCreateNewSlot, int potentialNewSlots, BaseSlot baseSlotPrefab);
    }
}
