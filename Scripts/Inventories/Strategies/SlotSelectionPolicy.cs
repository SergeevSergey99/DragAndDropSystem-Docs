using System;
using UnityEngine;

namespace UDND.Inventories
{
    [Serializable]
    public abstract class SlotSelectionPolicyBase
    {
        public abstract SlotSelection Select(SlotAcceptanceCandidates candidates, InventoryAcceptanceRequest request);
    }

    [Serializable]
    public sealed class FirstSlotSelectionPolicy : SlotSelectionPolicyBase
    {
        public static readonly FirstSlotSelectionPolicy Instance = new FirstSlotSelectionPolicy();

        public override SlotSelection Select(SlotAcceptanceCandidates candidates, InventoryAcceptanceRequest request)
        {
            if (candidates == null)
                return SlotSelection.None;

            if (candidates.Slots != null && candidates.Slots.Count > 0)
                return SlotSelection.Existing(candidates.Slots[0].Slot);

            if (candidates.CanCreateNewSlot)
                return SlotSelection.New();

            return SlotSelection.None;
        }
    }

    [Serializable]
    public sealed class StackFirstSlotSelectionPolicy : SlotSelectionPolicyBase
    {
        public static readonly StackFirstSlotSelectionPolicy Instance = new StackFirstSlotSelectionPolicy();

        public override SlotSelection Select(SlotAcceptanceCandidates candidates, InventoryAcceptanceRequest request)
        {
            if (candidates == null)
                return SlotSelection.None;

            int firstEmpty = -1;
            if (candidates.Slots != null)
            {
                for (int i = 0; i < candidates.Slots.Count; i++)
                {
                    var slot = candidates.Slots[i].Slot;
                    if (slot?.Stack != null && !slot.Stack.IsEmpty)
                        return SlotSelection.Existing(slot);
                    if (firstEmpty < 0)
                        firstEmpty = i;
                }

                if (firstEmpty >= 0)
                    return SlotSelection.Existing(candidates.Slots[firstEmpty].Slot);
            }

            if (candidates.CanCreateNewSlot)
                return SlotSelection.New();

            return SlotSelection.None;
        }
    }
}
