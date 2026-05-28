using System;
using System.Collections.Generic;
using UnityEngine;
using UDND.Slots;

namespace UDND.Inventories
{
    [Serializable]
    public abstract class SlotManagementSettingsBase
    {
        public virtual IInventoryStrategy WrapRuntimeStrategy(
            IInventory inventory,
            IInventoryStrategy baseStrategy,
            Func<BaseSlot> createSlot,
            Func<List<BaseSlot>> getSlots,
            Action ensureFreeSlots)
        {
            return baseStrategy;
        }

        public virtual bool CanCreateNewSlot(IInventory inventory, int currentSlotCount)
        {
            return false;
        }

        public virtual int GetPotentialNewSlots(IInventory inventory, int currentSlotCount)
        {
            return 0;
        }

        public virtual void EnsureFreeSlots(IInventory inventory, int initialSlotCount, Func<int> countFreeSlots, Func<BaseSlot> createSlot)
        {
        }

        public virtual bool CanRemoveAnotherSlot(IInventory inventory, int currentSlotCount, int initialSlotCount, int freeSlotCount)
        {
            return false;
        }

        public virtual void HandleSlotEmptied(
            IInventory inventory,
            BaseSlot preferredBaseSlot,
            int currentSlotCount,
            int initialSlotCount,
            Func<int> countFreeSlots,
            Func<BaseSlot> findLastEmptySlot,
            Func<BaseSlot, bool> tryRemoveSlot,
            Action updateAllVisuals)
        {
        }

        internal string CaptureConfigurationJson()
        {
            return JsonUtility.ToJson(this);
        }
    }
}
