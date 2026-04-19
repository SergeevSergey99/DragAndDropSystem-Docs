using System;
using System.Collections.Generic;
using UnityEngine;
using UniversalDragAndDrop.Slots;

namespace UniversalDragAndDrop.Inventories
{
    [Serializable]
    public sealed class DynamicSlotManagementSettings : SlotManagementSettingsBase
    {
        [SerializeField, Tooltip("Maximum number of slots.")]
        private int _maxSlots = 100;

        [SerializeField, Tooltip("Minimum number of free slots. 0 = create only on TryAddItem, not while moving into specific slots.")]
        private int _minFreeSlots = 1;

        public override IInventoryStrategy WrapRuntimeStrategy(
            UniversalInventory inventory,
            IInventoryStrategy baseStrategy,
            Func<BaseSlot> createSlot,
            Func<List<BaseSlot>> getSlots,
            Action ensureFreeSlots)
        {
            return new DynamicSlotDecorator(baseStrategy, createSlot, _maxSlots, _minFreeSlots, getSlots, ensureFreeSlots);
        }

        public override bool CanCreateNewSlot(UniversalInventory inventory, int currentSlotCount)
        {
            return currentSlotCount < _maxSlots;
        }

        public override int GetPotentialNewSlots(UniversalInventory inventory, int currentSlotCount)
        {
            return Mathf.Max(0, _maxSlots - currentSlotCount);
        }

        public override void EnsureFreeSlots(UniversalInventory inventory, int initialSlotCount, Func<int> countFreeSlots, Func<BaseSlot> createSlot)
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

        public override bool CanRemoveAnotherSlot(UniversalInventory inventory, int currentSlotCount, int initialSlotCount, int freeSlotCount)
        {
            if (currentSlotCount <= initialSlotCount)
                return false;

            return freeSlotCount > _minFreeSlots;
        }
    }
}
