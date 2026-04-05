using System;
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
    /// Поддерживает дефолтный лимит стратегии и,
    /// при allowItemOverride = true, per-item лимиты стаков через IStackSizeLimitable
    /// </summary>
    public class SeparableStacksStrategy : InventoryStrategyBase
    {
        private readonly bool _allowMergeOnDrop;

        public SeparableStacksStrategy(bool allowMergeOnDrop = true, int defaultMaxStackSize = 0, bool allowItemOverride = true)
        {
            _allowMergeOnDrop = allowMergeOnDrop;
            _defaultMaxStackSize = defaultMaxStackSize;
            _allowItemOverride = allowItemOverride;
        }

        private int GetMaxStackSize(IItemAdapter itemAdapter) =>
            GetMaxStackSize(itemAdapter, _defaultMaxStackSize, _allowItemOverride);

        public override bool TryAdd(List<ISlot> slots, ItemStack stack, int targetIndex, bool skipRules = false)
        {
            if (stack == null || stack.IsEmpty)
                return false;

            int remaining = stack.Count;
            int maxSize = GetMaxStackSize(stack.PrimaryAdapter);

            // Если указан конкретный слот (drag to slot)
            if (targetIndex >= 0 && targetIndex < slots.Count)
            {
                var targetSlot = slots[targetIndex];

                // Если слот пустой - кладём с учётом лимита
                if (targetSlot.IsEmpty)
                {
                    int toPlace = Math.Min(remaining, maxSize);
                    if (toPlace > 0 && (skipRules || PassesRules(targetSlot, stack.PrimaryAdapter, toPlace)))
                    {
                        var movedStack = stack.Split(toPlace);
                        if (movedStack.IsEmpty)
                            return false;

                        targetSlot.SetStack(movedStack);
                        remaining -= toPlace;
                    }
                }
                // Если в слоте ТОТ ЖЕ предмет и разрешён мерж - объединяем с учётом лимита
                else if (_allowMergeOnDrop && targetSlot.Stack.CanStack(stack.PrimaryAdapter))
                {
                    int canFit = Math.Max(0, maxSize - targetSlot.Stack.Count);
                    int toAdd = Math.Min(remaining, canFit);
                    if (toAdd > 0 && (skipRules || PassesRules(targetSlot, stack.PrimaryAdapter, toAdd)))
                    {
                        var movedStack = stack.Split(toAdd);
                        if (movedStack.IsEmpty)
                            return false;

                        if (!targetSlot.Stack.TryAddToStack(movedStack))
                        {
                            stack.TryAddToStack(movedStack);
                            return false;
                        }

                        targetSlot.UpdateVisuals();
                        remaining -= toAdd;
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

                    if (!slot.IsEmpty && slot.Stack.CanStack(stack.PrimaryAdapter))
                    {
                        int canFit = Math.Max(0, maxSize - slot.Stack.Count);
                        int toAdd = Math.Min(remaining, canFit);
                        if (toAdd > 0 && (skipRules || PassesRules(slot, stack.PrimaryAdapter, toAdd)))
                        {
                            var movedStack = stack.Split(toAdd);
                            if (movedStack.IsEmpty)
                                return false;

                            if (!slot.Stack.TryAddToStack(movedStack))
                            {
                                stack.TryAddToStack(movedStack);
                                return false;
                            }

                            slot.UpdateVisuals();
                            remaining -= toAdd;
                        }
                    }
                }

                // Если не нашли существующий стак или не всё влезло - создаём новые стаки в пустых слотах
                if (remaining > 0)
                {
                    foreach (var slot in slots)
                    {
                        if (remaining <= 0) break;

                        if (slot.IsEmpty)
                        {
                            int toPlace = Math.Min(remaining, maxSize);
                            if (toPlace > 0 && (skipRules || PassesRules(slot, stack.PrimaryAdapter, toPlace)))
                            {
                                var movedStack = stack.Split(toPlace);
                                if (movedStack.IsEmpty)
                                    return false;

                                slot.SetStack(movedStack);
                                remaining -= toPlace;
                            }
                        }
                    }
                }
            }
            return stack.IsEmpty;
        }

        public override bool TryRemove(List<ISlot> slots, IItemAdapter itemAdapter, int count, int sourceIndex)
        {
            int remaining = count;

            if (sourceIndex >= 0 && sourceIndex < slots.Count)
            {
                var slot = slots[sourceIndex];
                if (!slot.IsEmpty && slot.Stack.CanStack(itemAdapter))
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

                if (!slot.IsEmpty && slot.Stack.CanStack(itemAdapter))
                {
                    remaining -= slot.Stack.RemoveFromStack(remaining);
                    slot.UpdateVisuals();
                }
            }

            return remaining < count;
        }

        public override bool CanUseAlternativeSlot(ISlot slot, IItemAdapter itemAdapter)
        {
            if (slot == null || itemAdapter == null)
                return false;

            if (slot.IsEmpty)
                return true;

            if (!_allowMergeOnDrop || slot.Stack == null || !slot.Stack.CanStack(itemAdapter))
                return false;

            // Слот подходит для мержа только если есть свободное место
            int maxSize = GetMaxStackSize(itemAdapter);
            return slot.Stack.Count < maxSize;
        }

        public override bool TryAddToSlot(List<ISlot> slots, ItemStack stack, ISlot targetSlot, Action ensureFreeSlots, SlotOperationContext operationContext)
        {
            if (stack == null || stack.IsEmpty || targetSlot == null)
                return false;

            int maxSize = GetMaxStackSize(stack.PrimaryAdapter);

            if (!targetSlot.IsEmpty)
            {
                if (!_allowMergeOnDrop || !targetSlot.Stack.CanStack(stack.PrimaryAdapter))
                    return false;

                int canFit = Math.Max(0, maxSize - targetSlot.Stack.Count);
                int toAdd = Math.Min(stack.Count, canFit);
                if (toAdd <= 0 || !PassesRules(targetSlot, stack.PrimaryAdapter, toAdd))
                    return false;

                var mergeStack = stack.Split(toAdd);
                if (mergeStack.IsEmpty)
                    return false;

                if (!targetSlot.Stack.TryAddToStack(mergeStack))
                {
                    stack.TryAddToStack(mergeStack);
                    return false;
                }

                targetSlot.UpdateVisuals();
                operationContext?.RecordResult(targetSlot, false, toAdd);
                ensureFreeSlots?.Invoke();
                return true;
            }

            int toPlace = Math.Min(stack.Count, maxSize);
            if (toPlace <= 0 || !PassesRules(targetSlot, stack.PrimaryAdapter, toPlace))
                return false;

            var movedStack = stack.Split(toPlace);
            if (movedStack.IsEmpty)
                return false;

            targetSlot.SetStack(movedStack);
            targetSlot.UpdateVisuals();
            operationContext?.RecordResult(targetSlot, true, toPlace);
            ensureFreeSlots?.Invoke();
            return true;
        }

        public override bool CanAcceptItem(List<ISlot> slots, InventoryAcceptanceRequest request, bool canCreateNewSlot, int potentialNewSlots, ISlot slotPrefab, out ISlot suggestedSlot)
        {
            suggestedSlot = null;
            var item = request?.ItemAdapter;
            var desiredCount = request?.DesiredCount ?? 0;
            if (item == null || desiredCount <= 0)
                return false;

            int maxSize = GetMaxStackSize(item);

            foreach (var slot in slots)
            {
                if (slot.IsEmpty && PassesRules(slot, item, Math.Min(desiredCount, maxSize), request))
                {
                    suggestedSlot = slot;
                    return true;
                }

                if (_allowMergeOnDrop && !slot.IsEmpty && slot.Stack.CanStack(item))
                {
                    int canFit = Math.Max(0, maxSize - slot.Stack.Count);
                    if (canFit > 0 && PassesRules(slot, item, Math.Min(desiredCount, canFit), request))
                    {
                        suggestedSlot = slot;
                        return true;
                    }
                }
            }

            return canCreateNewSlot && PrefabPassesRules(slots, slotPrefab, item, Math.Min(desiredCount, maxSize), request);
        }

        public override int GetAcceptableCount(List<ISlot> slots, InventoryAcceptanceRequest request, bool canCreateNewSlot, int potentialNewSlots, ISlot slotPrefab)
        {
            var item = request?.ItemAdapter;
            var desiredCount = request?.DesiredCount ?? 0;
            if (item == null || desiredCount <= 0)
                return 0;

            int maxSize = GetMaxStackSize(item);
            int totalCapacity = 0;

            foreach (var slot in slots)
            {
                if (slot.IsEmpty && PassesRules(slot, item, Math.Min(desiredCount, maxSize), request))
                {
                    totalCapacity += maxSize;
                }
                else if (_allowMergeOnDrop && !slot.IsEmpty && slot.Stack.CanStack(item))
                {
                    int canFit = Math.Max(0, maxSize - slot.Stack.Count);
                    if (canFit > 0 && PassesRules(slot, item, Math.Min(desiredCount, canFit), request))
                        totalCapacity += canFit;
                }

                if (totalCapacity >= desiredCount)
                    return desiredCount;
            }

            if (canCreateNewSlot && totalCapacity < desiredCount)
            {
                if (PrefabPassesRules(slots, slotPrefab, item, Math.Min(desiredCount, maxSize), request))
                {
                    long newCapacity = (long)maxSize * Math.Max(1, potentialNewSlots);
                    totalCapacity = (int)Math.Min((long)totalCapacity + newCapacity, desiredCount);
                }
            }

            return Math.Min(totalCapacity, desiredCount);
        }
    }
}
