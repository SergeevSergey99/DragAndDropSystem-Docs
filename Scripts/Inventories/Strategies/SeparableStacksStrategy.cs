using System.Collections.Generic;
using DragAndDropSystem.Core;
using DragAndDropSystem.Slots;

namespace DragAndDropSystem.Inventories
{
    /// <summary>
    /// Стратегия в стиле Heroes of Might & Magic
    /// Предметы могут стакаться, но НЕ автоматически мержатся
    /// Можно иметь несколько стаков одного предмета в разных слотах
    /// Мерж происходит только при явном дропе на тот же предмет (если allowMergeOnDrop = true)
    /// </summary>
    public class SeparableStacksStrategy : InventoryStrategyBase
    {
        private readonly bool _allowMergeOnDrop;

        public SeparableStacksStrategy(bool allowMergeOnDrop = true)
        {
            _allowMergeOnDrop = allowMergeOnDrop;
        }

        public override bool TryAdd(List<ISlot> slots, ItemStack stack, int targetIndex)
        {
            if (stack == null || stack.IsEmpty)
                return false;

            int remaining = stack.Count;

            // Если указан конкретный слот (drag to slot)
            if (targetIndex >= 0 && targetIndex < slots.Count)
            {
                var targetSlot = slots[targetIndex];

                // Если слот пустой - просто кладём стак
                if (targetSlot.IsEmpty)
                {
                    if (PassesRules(targetSlot, stack.Item, remaining))
                    {
                        var newStack = new ItemStack(stack.Item, remaining);
                        targetSlot.SetStack(newStack);
                        remaining = 0;
                    }
                }
                // Если в слоте ТОТ ЖЕ предмет и разрешён мерж - объединяем
                else if (_allowMergeOnDrop && targetSlot.Stack.CanStack(stack.Item))
                {
                    if (PassesRules(targetSlot, stack.Item, remaining))
                    {
                        targetSlot.Stack.AddToStack(remaining);
                        targetSlot.UpdateVisuals();
                        remaining = 0;
                    }
                }
                // Иначе не можем добавить (слот занят другим предметом или мерж выключен)
            }
            else
            {
                // Если слот не указан (TryAddItem) - это программное добавление
                // Сначала пытаемся добавить к существующим стакам
                foreach (var slot in slots)
                {
                    if (remaining <= 0) break;

                    if (!slot.IsEmpty && slot.Stack.CanStack(stack.Item))
                    {
                        if (PassesRules(slot, stack.Item, remaining))
                        {
                            slot.Stack.AddToStack(remaining);
                            slot.UpdateVisuals();
                            remaining = 0;
                            break;
                        }
                    }
                }

                // Если не нашли существующий стак или не удалось добавить - создаём новый в пустом слоте
                if (remaining > 0)
                {
                    foreach (var slot in slots)
                    {
                        if (remaining <= 0) break;

                        if (slot.IsEmpty && PassesRules(slot, stack.Item, remaining))
                        {
                            var newStack = new ItemStack(stack.Item, remaining);
                            slot.SetStack(newStack);
                            remaining = 0;
                            break;
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

        public override bool CanUseAlternativeSlot(ISlot slot, IInventoryItem item)
        {
            if (slot == null || item == null)
                return false;

            if (slot.IsEmpty)
                return true;

            return _allowMergeOnDrop && slot.Stack != null && slot.Stack.CanStack(item);
        }

        public override bool TryAddToSlot(List<ISlot> slots, ItemStack stack, ISlot targetSlot, System.Action ensureFreeSlots, SlotOperationContext operationContext)
        {
            if (stack == null || stack.IsEmpty || targetSlot == null)
                return false;

            if (!targetSlot.IsEmpty)
            {
                if (!_allowMergeOnDrop || !targetSlot.Stack.CanStack(stack.Item) || !PassesRules(targetSlot, stack.Item, stack.Count))
                    return false;

                return TryMergeIntoSlot(stack, targetSlot, ensureFreeSlots, operationContext);
            }

            if (!PassesRules(targetSlot, stack.Item, stack.Count))
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
                if (slot.IsEmpty && PassesRules(slot, item, desiredCount))
                {
                    suggestedSlot = slot;
                    return true;
                }

                if (_allowMergeOnDrop && !slot.IsEmpty && slot.Stack.CanStack(item) && PassesRules(slot, item, desiredCount))
                {
                    suggestedSlot = slot;
                    return true;
                }
            }

            return canCreateNewSlot && PrefabPassesRules(slotPrefab, item, desiredCount);
        }

        public override int GetAcceptableCount(List<ISlot> slots, IInventoryItem item, int desiredCount, bool canCreateNewSlot, int potentialNewSlots, ISlot slotPrefab)
        {
            if (item == null || desiredCount <= 0)
                return 0;

            if (canCreateNewSlot)
                return desiredCount;

            foreach (var slot in slots)
            {
                if (slot.IsEmpty && PassesRules(slot, item, desiredCount))
                    return desiredCount;

                if (_allowMergeOnDrop && !slot.IsEmpty && slot.Stack.CanStack(item) && PassesRules(slot, item, desiredCount))
                    return desiredCount;
            }

            return 0;
        }
    }
}