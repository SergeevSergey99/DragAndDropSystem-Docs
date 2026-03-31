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
        public override int ResolveDragAmount(int stackCount, DragAmount dragAmount, int customDragAmount) => 1;
        public override bool RequiresStrategyPlacement(ItemStack stack) => stack != null && !stack.IsEmpty && stack.Count > 1;
        public override bool UsesPerItemSlotPlanning => true;

        public override bool CanUseAlternativeSlot(ISlot slot, IItemAdapter itemAdapter)
        {
            if (slot == null || itemAdapter == null)
                return false;

            return slot.IsEmpty;
        }

        public override IEnumerable<ISlot> EnumerateAlternativeSlots(List<ISlot> slots, IItemAdapter itemAdapter, AlternativePlacementMode mode, ISlot excludeSlot)
        {
            if (mode == AlternativePlacementMode.MergeOnly)
                yield break;

            if (slots == null || itemAdapter == null)
                yield break;

            for (int i = 0; i < slots.Count; i++)
            {
                var slot = slots[i];
                if (slot == null || ReferenceEquals(slot, excludeSlot) || !slot.IsEmpty)
                    continue;

                yield return slot;
            }
        }

        public override bool TryAdd(List<ISlot> slots, ItemStack stack, int targetIndex, bool skipRules = false)
        {
            if (stack == null || stack.IsEmpty)
                return false;

            // Проверяем целевой слот
            if (targetIndex >= 0 && targetIndex < slots.Count)
            {
                var targetSlot = slots[targetIndex];

                if (targetSlot.IsEmpty && (skipRules || PassesRules(targetSlot, stack.PrimaryAdapter, 1)))
                {
                    var singleItemStack = stack.Split(1);
                    if (singleItemStack.IsEmpty)
                        return false;

                    targetSlot.SetStack(singleItemStack);
                }
                return stack.IsEmpty;
            }

            // Распределяем предметы по пустым слотам (по 1 в каждый)
            for (int i = 0; i < slots.Count && !stack.IsEmpty; i++)
            {
                var slot = slots[i];
                if (!slot.IsEmpty)
                    continue;

                if (!skipRules && !PassesRules(slot, stack.PrimaryAdapter, 1))
                    continue;

                var singleItemStack = stack.Split(1);
                if (singleItemStack.IsEmpty)
                    return false;

                slot.SetStack(singleItemStack);
                // Продолжаем цикл, чтобы распределить оставшиеся предметы
            }

            return stack.IsEmpty;
        }

        public override bool TryRemove(List<ISlot> slots, IItemAdapter itemAdapter, int count, int sourceIndex)
        {
            if (sourceIndex >= 0 && sourceIndex < slots.Count)
            {
                var slot = slots[sourceIndex];
                if (!slot.IsEmpty && slot.Stack.CanStack(itemAdapter))
                {
                    slot.Clear();
                    return true;
                }
                return false;
            }

            // Ищем и удаляем первый найденный предмет
            int slotIndex = FindSlotWithItem(slots, itemAdapter);
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

            if (!PassesRules(targetSlot, stack.PrimaryAdapter, 1))
                return false;

            return TryPlaceIntoEmptySlot(stack, targetSlot, 1, ensureFreeSlots, operationContext);
        }

        public override bool CanAcceptItem(List<ISlot> slots, InventoryAcceptanceRequest request, bool canCreateNewSlot, int potentialNewSlots, ISlot slotPrefab, out ISlot suggestedSlot)
        {
            suggestedSlot = null;
            var item = request?.ItemAdapter;
            var desiredCount = request?.DesiredCount ?? 0;
            if (item == null || desiredCount <= 0)
                return false;

            foreach (var slot in slots)
            {
                if (slot.IsEmpty && PassesRules(slot, item, 1, request))
                {
                    suggestedSlot = slot;
                    return true;
                }
            }

            return canCreateNewSlot && potentialNewSlots > 0 && PrefabPassesRules(slots, slotPrefab, item, 1, request);
        }

        public override int GetAcceptableCount(List<ISlot> slots, InventoryAcceptanceRequest request, bool canCreateNewSlot, int potentialNewSlots, ISlot slotPrefab)
        {
            var item = request?.ItemAdapter;
            var desiredCount = request?.DesiredCount ?? 0;
            if (item == null || desiredCount <= 0)
                return 0;

            int acceptableCount = 0;
            foreach (var slot in slots)
            {
                if (slot.IsEmpty && PassesRules(slot, item, 1, request))
                    acceptableCount++;
            }

            if (canCreateNewSlot && potentialNewSlots > 0 && PrefabPassesRules(slots, slotPrefab, item, 1, request))
                acceptableCount += potentialNewSlots;

            return System.Math.Min(acceptableCount, desiredCount);
        }
    }
}
