using System;
using System.Collections.Generic;
using UnityEngine;
using UniversalDragAndDrop.Slots;

namespace UniversalDragAndDrop.Inventories
{
    [Serializable]
    public abstract class SlotManagementSettingsBase
    {
        public virtual IInventoryStrategy WrapRuntimeStrategy(
            UniversalInventory inventory,
            IInventoryStrategy baseStrategy,
            Func<BaseSlot> createSlot,
            Func<List<BaseSlot>> getSlots,
            Action ensureFreeSlots)
        {
            return baseStrategy;
        }

        public virtual bool CanCreateNewSlot(UniversalInventory inventory, int currentSlotCount)
        {
            return false;
        }

        public virtual int GetPotentialNewSlots(UniversalInventory inventory, int currentSlotCount)
        {
            return 0;
        }

        public virtual void EnsureFreeSlots(UniversalInventory inventory, int initialSlotCount, Func<int> countFreeSlots, Func<BaseSlot> createSlot)
        {
        }

        public virtual bool CanRemoveAnotherSlot(UniversalInventory inventory, int currentSlotCount, int initialSlotCount, int freeSlotCount)
        {
            return false;
        }

        internal string CaptureConfigurationJson()
        {
            return JsonUtility.ToJson(this);
        }
    }
}
