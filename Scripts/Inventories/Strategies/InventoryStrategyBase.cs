using System;
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
        protected int _defaultMaxStackSize;
        protected bool _allowItemOverride;

        /// <summary>
        /// Задать лимит стака в рантайме (например, из DataBinding).
        /// </summary>
        public void SetMaxStackSize(int maxStackSize, bool allowItemOverride)
        {
            _defaultMaxStackSize = maxStackSize;
            _allowItemOverride = allowItemOverride;
        }

        public abstract bool TryAdd(List<ISlot> slots, ItemStack stack, int targetIndex);
        public abstract bool TryRemove(List<ISlot> slots, IInventoryItem item, int count, int sourceIndex);
        public abstract bool TryAddToSlot(List<ISlot> slots, ItemStack stack, ISlot targetSlot, System.Action ensureFreeSlots, SlotOperationContext operationContext);
        public abstract bool CanAcceptItem(List<ISlot> slots, InventoryAcceptanceRequest request, bool canCreateNewSlot, int potentialNewSlots, ISlot slotPrefab, out ISlot suggestedSlot);
        public abstract int GetAcceptableCount(List<ISlot> slots, InventoryAcceptanceRequest request, bool canCreateNewSlot, int potentialNewSlots, ISlot slotPrefab);

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

        /// <summary>
        /// Лимит стака для предмета.
        /// Если allowItemOverride и предмет реализует IStackSizeLimitable — используется лимит предмета.
        /// Иначе — defaultMaxStackSize (0 = без ограничений).
        /// </summary>
        protected static int GetMaxStackSize(IInventoryItem item, int defaultMaxStackSize, bool allowItemOverride)
        {
            if (allowItemOverride && item is IStackSizeLimitable limitable)
                return Math.Max(1, limitable.MaxStackSize);
            return defaultMaxStackSize > 0 ? defaultMaxStackSize : int.MaxValue;
        }

        protected bool PassesRules(ISlot slot, IInventoryItem item, int previewCount, InventoryAcceptanceRequest request = null)
        {
            if (slot == null || item == null || previewCount <= 0)
                return false;

            if (slot.Inventory is UniversalInventory inventory)
                return inventory.CanAcceptByRules(slot, item, previewCount, request);

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

        protected bool PrefabPassesRules(List<ISlot> slots, ISlot slotPrefab, IInventoryItem item, int previewCount, InventoryAcceptanceRequest request)
        {
            if (item == null || previewCount <= 0)
                return false;

            UniversalInventory inventory = request?.TargetInventory as UniversalInventory;
            if (inventory == null)
            {
                foreach (var slot in slots)
                {
                    inventory = slot?.Inventory as UniversalInventory;
                    if (inventory != null)
                        break;
                }
            }

            if (inventory != null)
                return inventory.CanAcceptByRules(slotPrefab, item, previewCount, request, allowForeignSlot: true);

            if (slotPrefab?.SlotRuleValidator == null)
                return true;

            var context = request?.CreateValidationContext(slotPrefab, previewCount, item)
                ?? new DragContext(new ItemStack(item, previewCount), null, null, slotPrefab, null);
            var entry = context.Entries[0];
            return slotPrefab.SlotRuleValidator.ValidateDrop(context, entry).IsValid;
        }

        protected static bool TryMergeIntoSlot(ItemStack stack, ISlot slot, int maxStackSize, System.Action ensureFreeSlots, SlotOperationContext operationContext)
        {
            int canFit = Math.Max(0, maxStackSize - slot.Stack.Count);
            int toAdd = Math.Min(stack.Count, canFit);
            if (toAdd <= 0) return false;

            slot.Stack.AddToStack(toAdd);
            stack.RemoveFromStack(toAdd);
            slot.UpdateVisuals();
            operationContext?.RecordResult(slot, false, toAdd);
            ensureFreeSlots?.Invoke();
            return true;
        }

        protected static bool TryPlaceIntoEmptySlot(ItemStack stack, ISlot slot, int maxStackSize, System.Action ensureFreeSlots, SlotOperationContext operationContext)
        {
            int toPlace = Math.Min(stack.Count, maxStackSize);
            if (toPlace <= 0) return false;

            bool slotWasEmpty = slot.IsEmpty;
            slot.SetStack(new ItemStack(stack.Item, toPlace));
            stack.RemoveFromStack(toPlace);
            slot.UpdateVisuals();
            operationContext?.RecordResult(slot, slotWasEmpty, toPlace);
            ensureFreeSlots?.Invoke();
            return true;
        }
    }
}
