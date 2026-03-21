using System.Collections.Generic;
using DragAndDropSystem.Core;
using DragAndDropSystem.Slots;

namespace DragAndDropSystem.Inventories
{
    /// <summary>
    /// Стратегия: предметы стакаются (группируются по типу)
    /// Один тип предмета может занимать несколько слотов
    /// Лимиты стаков контролируются через правила (MaxStackSizeRule)
    /// </summary>
    public class StackableItemStrategy : InventoryStrategyBase
    {
        private readonly bool _autoMergeOnDrop;

        public StackableItemStrategy(bool autoMergeOnDrop = true)
        {
            _autoMergeOnDrop = autoMergeOnDrop;
        }

        public override bool TryAdd(List<ISlot> slots, ItemStack stack, int targetIndex)
        {
            if (stack == null || stack.IsEmpty)
                return false;

            int remaining = stack.Count;

            // Если указан целевой слот
            if (targetIndex >= 0 && targetIndex < slots.Count)
            {
                var targetSlot = slots[targetIndex];

                // Если слот пустой - создаем новый стак
                if (targetSlot.IsEmpty)
                {
                    if (PassesRules(targetSlot, stack.Item, remaining))
                    {
                        var newStack = new ItemStack(stack.Item, remaining);
                        targetSlot.SetStack(newStack);
                        remaining = 0;
                    }
                }
                // Если в слоте тот же предмет - добавляем
                else if (targetSlot.Stack.CanStack(stack.Item))
                {
                    if (PassesRules(targetSlot, stack.Item, remaining))
                    {
                        targetSlot.Stack.AddToStack(remaining);
                        targetSlot.UpdateVisuals();
                        remaining = 0;
                    }
                }
            }
            else
            {
                // Сначала пытаемся заполнить существующие стаки
                foreach (var slot in slots)
                {
                    if (remaining <= 0) break;

                    if (!slot.IsEmpty && slot.Stack.CanStack(stack.Item))
                    {
                        if (!PassesRules(slot, stack.Item, remaining))
                            continue;

                        slot.Stack.AddToStack(remaining);
                        slot.UpdateVisuals();
                        remaining = 0;
                    }
                }

                // Затем создаем новые стаки в пустых слотах
                if (remaining > 0)
                {
                    foreach (var slot in slots)
                    {
                        if (remaining <= 0) break;

                        if (slot.IsEmpty)
                        {
                            if (!PassesRules(slot, stack.Item, remaining))
                                continue;

                            var newStack = new ItemStack(stack.Item, remaining);
                            slot.SetStack(newStack);
                            remaining = 0;
                        }
                    }
                }
            }

            // Обновляем исходный стак
            int added = stack.Count - remaining;
            stack.RemoveFromStack(added);

            return stack.IsEmpty;
        }

        public override bool TryRemove(List<ISlot> slots, IInventoryItem item, int count, int sourceIndex)
        {
            int remaining = count;

            if (sourceIndex >= 0 && sourceIndex < slots.Count)
            {
                var slot = slots[sourceIndex];
                if (!slot.IsEmpty && slot.Stack.Item.ItemId == item.ItemId)
                {
                    int removed = slot.Stack.RemoveFromStack(remaining);
                    slot.UpdateVisuals();
                    return removed > 0;
                }
                return false;
            }

            // Удаляем из всех слотов с этим предметом
            foreach (var slot in slots)
            {
                if (remaining <= 0) break;

                if (!slot.IsEmpty && slot.Stack.Item.ItemId == item.ItemId)
                {
                    remaining -= slot.Stack.RemoveFromStack(remaining);
                    slot.UpdateVisuals();
                }
            }

            return remaining < count;
        }

        public override bool TryAddToSlot(List<ISlot> slots, ItemStack stack, ISlot targetSlot, System.Action ensureFreeSlots, SlotOperationContext operationContext)
        {
            if (stack == null || stack.IsEmpty || targetSlot == null)
                return false;

            if (!targetSlot.IsEmpty)
            {
                if (!targetSlot.Stack.CanStack(stack.Item) || !PassesRules(targetSlot, stack.Item, stack.Count))
                    return false;

                return TryMergeIntoSlot(stack, targetSlot, ensureFreeSlots, operationContext);
            }

            if (_autoMergeOnDrop)
            {
                foreach (var slot in slots)
                {
                    if (slot == targetSlot || slot.IsEmpty || !slot.Stack.CanStack(stack.Item))
                        continue;

                    if (!PassesRules(slot, stack.Item, stack.Count))
                        continue;

                    return TryMergeIntoSlot(stack, slot, ensureFreeSlots, operationContext);
                }
            }

            if (!PassesRules(targetSlot, stack.Item, stack.Count))
                return false;

            return TryPlaceIntoEmptySlot(stack, targetSlot, ensureFreeSlots, operationContext);
        }

        public override bool CanAcceptItem(List<ISlot> slots, InventoryAcceptanceRequest request, bool canCreateNewSlot, int potentialNewSlots, ISlot slotPrefab, out ISlot suggestedSlot)
        {
            suggestedSlot = null;
            var item = request?.Item;
            var desiredCount = request?.DesiredCount ?? 0;
            if (item == null || desiredCount <= 0)
                return false;

            foreach (var slot in slots)
            {
                if (!slot.IsEmpty && slot.Stack.CanStack(item) && PassesRules(slot, item, 1, request))
                {
                    suggestedSlot = slot;
                    return true;
                }

                if (slot.IsEmpty && suggestedSlot == null && PassesRules(slot, item, 1, request))
                {
                    suggestedSlot = slot;
                }
            }

            if (suggestedSlot != null)
                return true;

            return canCreateNewSlot && PrefabPassesRules(slots, slotPrefab, item, 1, request);
        }

        public override int GetAcceptableCount(List<ISlot> slots, InventoryAcceptanceRequest request, bool canCreateNewSlot, int potentialNewSlots, ISlot slotPrefab)
        {
            var item = request?.Item;
            var desiredCount = request?.DesiredCount ?? 0;
            if (item == null || desiredCount <= 0)
                return 0;

            bool hasStackableSlot = false;
            bool hasEmptySlot = false;

            foreach (var slot in slots)
            {
                if (!slot.IsEmpty && slot.Stack.CanStack(item) && PassesRules(slot, item, 1, request))
                {
                    hasStackableSlot = true;
                    break;
                }

                if (slot.IsEmpty && PassesRules(slot, item, 1, request))
                    hasEmptySlot = true;
            }

            if (hasStackableSlot || hasEmptySlot)
                return desiredCount;

            return canCreateNewSlot && PrefabPassesRules(slots, slotPrefab, item, 1, request)
                ? desiredCount
                : 0;
        }
    }
}
