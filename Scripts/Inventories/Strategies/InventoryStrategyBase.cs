using System.Collections.Generic;
using DragAndDropSystem.Core;
using DragAndDropSystem.Slots;

namespace DragAndDropSystem.Inventories
{
    /// <summary>
    /// Базовая стратегия с общими методами
    /// </summary>
    public abstract class InventoryStrategyBase : IInventoryStrategy
    {
        public abstract bool TryAdd(List<ISlot> slots, ItemStack stack, int targetIndex);
        public abstract bool TryRemove(List<ISlot> slots, IInventoryItem item, int count, int sourceIndex);
        public abstract bool TryAddToSlot(List<ISlot> slots, ItemStack stack, ISlot targetSlot, System.Action ensureFreeSlots, SlotOperationContext operationContext);
        public abstract bool CanAcceptItem(List<ISlot> slots, IInventoryItem item, int desiredCount, bool canCreateNewSlot, int potentialNewSlots, ISlot slotPrefab, out ISlot suggestedSlot);
        public abstract int GetAcceptableCount(List<ISlot> slots, IInventoryItem item, int desiredCount, bool canCreateNewSlot, int potentialNewSlots, ISlot slotPrefab);

        public virtual int GetItemCount(List<ISlot> slots, IInventoryItem item)
        {
            int total = 0;
            foreach (var slot in slots)
            {
                if (!slot.IsEmpty && slot.Stack.Item.ItemId == item.ItemId)
                {
                    total += slot.Stack.Count;
                }
            }
            return total;
        }

        public virtual bool Contains(List<ISlot> slots, IInventoryItem item)
        {
            foreach (var slot in slots)
            {
                if (!slot.IsEmpty && slot.Stack.Item.ItemId == item.ItemId)
                {
                    return true;
                }
            }
            return false;
        }

        public virtual int ResolveDragAmount(int stackCount, UniversalInventory.DragAmountType dragAmount, int customDragAmount)
        {
            switch (dragAmount)
            {
                case UniversalInventory.DragAmountType.One:
                    return 1;

                case UniversalInventory.DragAmountType.Half:
                    return UnityEngine.Mathf.Max(1, stackCount / 2);

                case UniversalInventory.DragAmountType.All:
                    return stackCount;

                case UniversalInventory.DragAmountType.Custom:
                    return UnityEngine.Mathf.Min(customDragAmount, stackCount);

                default:
                    return stackCount;
            }
        }

        public virtual bool RequiresStrategyPlacement(ItemStack stack) => false;
        public virtual bool UsesPerItemSlotPlanning => false;

        public virtual bool CanUseAlternativeSlot(ISlot slot, IInventoryItem item)
        {
            if (slot == null || item == null)
                return false;

            if (slot.IsEmpty)
                return true;

            return slot.Stack != null && slot.Stack.CanStack(item);
        }

        protected bool PassesRules(ISlot slot, IInventoryItem item, int previewCount)
        {
            if (slot == null || item == null || previewCount <= 0)
                return false;

            if (slot.Inventory is UniversalInventory inventory)
                return inventory.CanAcceptByRules(slot, item, previewCount);

            return true;
        }

        protected int FindEmptySlotIndex(List<ISlot> slots)
        {
            for (int i = 0; i < slots.Count; i++)
            {
                if (slots[i].IsEmpty)
                    return i;
            }
            return -1;
        }

        protected int FindSlotWithItem(List<ISlot> slots, IInventoryItem item)
        {
            for (int i = 0; i < slots.Count; i++)
            {
                if (!slots[i].IsEmpty && slots[i].Stack.Item.ItemId == item.ItemId)
                {
                    return i;
                }
            }
            return -1;
        }

        protected bool PrefabPassesRules(ISlot slotPrefab, IInventoryItem item, int previewCount)
        {
            if (slotPrefab?.SlotRuleValidator == null || item == null || previewCount <= 0)
                return true;

            var context = new DragContext(new ItemStack(item, previewCount), null, null);
            var entry = context.Entries[0];
            return slotPrefab.SlotRuleValidator.ValidateDrop(context, entry).IsValid;
        }

        protected static bool TryMergeIntoSlot(ItemStack stack, ISlot slot, System.Action ensureFreeSlots, SlotOperationContext operationContext)
        {
            int countBefore = stack.Count;
            slot.Stack.AddToStack(stack.Count);
            int added = countBefore;
            stack.RemoveFromStack(added);
            slot.UpdateVisuals();
            operationContext?.RecordResult(slot, false, added);

            if (added > 0)
                ensureFreeSlots?.Invoke();

            return added > 0;
        }

        protected static bool TryPlaceIntoEmptySlot(ItemStack stack, ISlot slot, System.Action ensureFreeSlots, SlotOperationContext operationContext)
        {
            bool slotWasEmpty = slot.IsEmpty;
            var newStack = new ItemStack(stack.Item, stack.Count);
            slot.SetStack(newStack);
            slot.UpdateVisuals();
            int placed = stack.Count;
            stack.RemoveFromStack(stack.Count);
            operationContext?.RecordResult(slot, slotWasEmpty, placed);

            if (placed > 0)
                ensureFreeSlots?.Invoke();

            return placed > 0;
        }
    }
}
