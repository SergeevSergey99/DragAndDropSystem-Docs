using System;
using System.Collections.Generic;
using UnityEngine;
using UDND.Slots;

namespace UDND.Inventories
{
    [Serializable]
    public sealed class DynamicSlotManagementSettings : SlotManagementSettingsBase
    {
        [SerializeField, Tooltip("Maximum number of slots.")]
        private int _maxSlots = 100;

        [SerializeField, Tooltip("Minimum number of free slots. 0 = create only on TryAddItem, not while moving into specific slots.")]
        private int _minFreeSlots = 1;

        public override IInventoryStrategy WrapRuntimeStrategy(
            IInventory inventory,
            IInventoryStrategy baseStrategy,
            Func<BaseSlot> createSlot,
            Func<List<BaseSlot>> getSlots,
            Action ensureFreeSlots)
        {
            return new DynamicSlotDecorator(baseStrategy, createSlot, _maxSlots, _minFreeSlots, getSlots, ensureFreeSlots);
        }

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
            int slotsToCreate = _minFreeSlots - freeSlots;
            for (int i = 0; i < slotsToCreate && inventory != null && inventory.SlotCount < _maxSlots; i++)
            {
                createSlot();
            }
        }

        public override bool CanRemoveAnotherSlot(IInventory inventory, int currentSlotCount, int initialSlotCount, int freeSlotCount)
        {
            if (currentSlotCount <= initialSlotCount)
                return false;

            return freeSlotCount > _minFreeSlots;
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
            if (inventory == null || countFreeSlots == null || findLastEmptySlot == null || tryRemoveSlot == null)
                return;

            bool removedAny = false;
            if (preferredBaseSlot != null
                && preferredBaseSlot.IsEmpty
                && CanRemoveAnotherSlot(inventory, currentSlotCount, initialSlotCount, countFreeSlots()))
            {
                removedAny |= tryRemoveSlot(preferredBaseSlot);
            }

            while (CanRemoveAnotherSlot(inventory, inventory.SlotCount, initialSlotCount, countFreeSlots()))
            {
                BaseSlot slotToRemove = findLastEmptySlot();
                if (slotToRemove == null || !tryRemoveSlot(slotToRemove))
                    break;

                removedAny = true;
            }

            if (removedAny)
                updateAllVisuals?.Invoke();
        }
    }
}
