using System;
using UnityEngine;
using UDND.Slots;

namespace UDND.Inventories
{
    [Serializable]
    public sealed class DynamicSlotManagementSettings : SlotManagementSettingsBase
    {
        [SerializeField, Tooltip("Maximum number of slots.")]
        private int _maxSlots = 100;

        [SerializeField, Tooltip("Maximum number of maintained free slots. Explicit target-index adds can still create slots up to the requested index.")]
        private int _maxFreeSlots = 0;

        public override bool CanCreateNewSlot(IInventory inventory, int currentSlotCount)
        {
            return currentSlotCount < _maxSlots;
        }

        public override int GetPotentialNewSlots(IInventory inventory, int currentSlotCount)
        {
            return Mathf.Max(0, _maxSlots - currentSlotCount);
        }

        public override void EnsureFreeSlots(IInventory inventory, int initialSlotCount, Func<int> countFreeSlots, Func<BaseSlot> createSlot)
        {
            if (countFreeSlots == null || createSlot == null)
                return;

            int freeSlots = countFreeSlots();
            int slotsToCreate = _maxFreeSlots - freeSlots;
            for (int i = 0; i < slotsToCreate && inventory != null && inventory.SlotCount < _maxSlots; i++)
            {
                createSlot();
            }
        }

        public override bool CanRemoveAnotherSlot(IInventory inventory, int currentSlotCount, int initialSlotCount, int freeSlotCount)
        {
            if (currentSlotCount <= initialSlotCount)
                return false;

            return freeSlotCount > _maxFreeSlots;
        }

        public override void HandleSlotEmptied(
            IInventory inventory,
            BaseSlot preferredBaseSlot,
            int currentSlotCount,
            int initialSlotCount,
            Func<int> countFreeSlots,
            Func<BaseSlot> findLastEmptySlot,
            Func<BaseSlot, bool> tryRemoveSlot,
            Action updateAllVisuals)
        {
            if (inventory == null || countFreeSlots == null || tryRemoveSlot == null)
                return;

            bool removedAny = false;
            if (preferredBaseSlot != null
                && preferredBaseSlot.IsEmpty
                && CanRemoveAnotherSlot(inventory, currentSlotCount, initialSlotCount, countFreeSlots()))
            {
                removedAny |= tryRemoveSlot(preferredBaseSlot);
            }

            if (removedAny)
                updateAllVisuals?.Invoke();
        }
    }
}
