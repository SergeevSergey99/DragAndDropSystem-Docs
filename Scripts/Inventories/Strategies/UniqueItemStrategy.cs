using System.Collections.Generic;
using DragAndDropSystem.Core;
using DragAndDropSystem.Slots;

namespace DragAndDropSystem.Inventories
{
    /// <summary>
    /// Стратегия: каждый предмет занимает отдельный слот (не стакается)
    /// Используется для инвентарей с уникальными предметами
    /// </summary>
    public class UniqueItemStrategy : InventoryStrategyBase
    {
        public override int ResolveDragAmount(int stackCount, UniversalInventory.DragAmountType dragAmount, int customDragAmount) => 1;
        public override bool RequiresStrategyPlacement(ItemStack stack) => stack != null && !stack.IsEmpty && stack.Count > 1;
        public override bool UsesPerItemSlotPlanning => true;

        public override bool CanUseAlternativeSlot(ISlot slot, IInventoryItem item)
        {
            if (slot == null || item == null)
                return false;

            return slot.IsEmpty;
        }

        public override bool TryAdd(List<ISlot> slots, ItemStack stack, int targetIndex)
        {
            if (stack == null || stack.IsEmpty)
                return false;

            // Проверяем целевой слот
            if (targetIndex >= 0 && targetIndex < slots.Count)
            {
                var targetSlot = slots[targetIndex];

                if (targetSlot.IsEmpty && PassesRules(targetSlot, stack.Item, 1))
                {
                    var singleItemStack = new ItemStack(stack.Item, 1);
                    targetSlot.SetStack(singleItemStack);
                    stack.RemoveFromStack(1);
                }
                return stack.IsEmpty;
            }

            // Распределяем предметы по пустым слотам (по 1 в каждый)
            for (int i = 0; i < slots.Count && !stack.IsEmpty; i++)
            {
                var slot = slots[i];
                if (!slot.IsEmpty)
                    continue;

                if (!PassesRules(slot, stack.Item, 1))
                    continue;

                var singleItemStack = new ItemStack(stack.Item, 1);
                slot.SetStack(singleItemStack);
                stack.RemoveFromStack(1);
                // Продолжаем цикл, чтобы распределить оставшиеся предметы
            }

            return stack.IsEmpty;
        }

        public override bool TryRemove(List<ISlot> slots, IInventoryItem item, int count, int sourceIndex)
        {
            if (sourceIndex >= 0 && sourceIndex < slots.Count)
            {
                var slot = slots[sourceIndex];
                if (!slot.IsEmpty && slot.Stack.Item.ItemId == item.ItemId)
                {
                    slot.Clear();
                    return true;
                }
                return false;
            }

            // Ищем и удаляем первый найденный предмет
            int slotIndex = FindSlotWithItem(slots, item);
            if (slotIndex >= 0)
            {
                slots[slotIndex].Clear();
                return true;
            }

            return false;
        }

        public override bool TryAddToSlot(List<ISlot> slots, ItemStack stack, ISlot targetSlot, System.Action ensureFreeSlots, SlotOperationContext operationContext)
        {
            if (stack == null || stack.IsEmpty || targetSlot == null || !targetSlot.IsEmpty)
                return false;

            if (!PassesRules(targetSlot, stack.Item, 1))
                return false;

            return TryPlaceIntoEmptySlot(stack, targetSlot, ensureFreeSlots, operationContext);
        }

        public override bool CanAcceptItem(List<ISlot> slots, IInventoryItem item, int desiredCount, bool canCreateNewSlot, int potentialNewSlots, ISlot slotPrefab, out ISlot suggestedSlot)
        {
            suggestedSlot = null;
            if (item == null || desiredCount <= 0)
                return false;

            foreach (var slot in slots)
            {
                if (slot.IsEmpty && PassesRules(slot, item, 1))
                {
                    suggestedSlot = slot;
                    return true;
                }
            }

            return canCreateNewSlot && potentialNewSlots > 0 && PrefabPassesRules(slotPrefab, item, 1);
        }

        public override int GetAcceptableCount(List<ISlot> slots, IInventoryItem item, int desiredCount, bool canCreateNewSlot, int potentialNewSlots, ISlot slotPrefab)
        {
            if (item == null || desiredCount <= 0)
                return 0;

            int acceptableCount = 0;
            foreach (var slot in slots)
            {
                if (slot.IsEmpty && PassesRules(slot, item, 1))
                    acceptableCount++;
            }

            if (canCreateNewSlot && potentialNewSlots > 0 && PrefabPassesRules(slotPrefab, item, 1))
                acceptableCount += potentialNewSlots;

            return System.Math.Min(acceptableCount, desiredCount);
        }
    }
}