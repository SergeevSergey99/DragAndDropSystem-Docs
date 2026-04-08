using System.Collections.Generic;
using DragAndDropSystem.Core;
using DragAndDropSystem.Slots;
using DragAndDropSystem.Tools;

namespace DragAndDropSystem.Inventories
{
    /// <summary>
    /// Декоратор для динамического создания слотов
    /// Оборачивает любую стратегию (Unique или Stackable) и добавляет автоматическое создание слотов
    /// </summary>
    public class DynamicSlotDecorator : IInventoryStrategy
    {
        private readonly IInventoryStrategy _baseStrategy;
        private readonly System.Func<BaseSlot> _createSlotFunc;
        private readonly int _maxSlots;
        private readonly int _maxFreeSlots;
        private readonly System.Func<List<BaseSlot>> _getSlotsFunc;
        private readonly System.Action _ensureFreeSlotsFunc;

        public DynamicSlotDecorator(
            IInventoryStrategy baseStrategy,
            System.Func<BaseSlot> createSlotFunc,
            int maxSlots = 100,
            int maxFreeSlots = 1,
            System.Func<List<BaseSlot>> getSlotsFunc = null,
            System.Action ensureFreeSlotsFunc = null)
        {
            _baseStrategy = baseStrategy;
            _createSlotFunc = createSlotFunc;
            _maxSlots = maxSlots;
            _maxFreeSlots = maxFreeSlots;
            _getSlotsFunc = getSlotsFunc;
            _ensureFreeSlotsFunc = ensureFreeSlotsFunc;
        }

        public bool TryAddQuite(List<BaseSlot> slots, ItemStack stack, int targetIndex) => TryAdd(slots, stack, targetIndex, skipRules: true);

        public bool TryAdd(List<BaseSlot> slots, ItemStack stack, int targetIndex, bool skipRules = false)
        {
            if (stack == null || stack.IsEmpty)
                return false;

            int initialCount = stack.Count;

            // РЕЖИМ 1: targetIndex указан (перенос в конкретный слот)
            if (targetIndex >= 0)
            {
                // Если targetIndex выходит за пределы - создаем слоты до него (только если maxFreeSlots > 0)
                if (targetIndex >= slots.Count && _maxFreeSlots > 0 && slots.Count < _maxSlots)
                {
                    while (slots.Count <= targetIndex && slots.Count < _maxSlots)
                    {
                        var newSlot = _createSlotFunc();
                        if (newSlot == null)
                            break;
                        Extensions.DragAndDropLog($"<color=green>[DynamicSlots] Created slot {slots.Count - 1} to reach targetIndex {targetIndex}</color>");
                    }
                }

                // Пытаемся добавить в целевой слот
                bool added = _baseStrategy.TryAdd(slots, stack, targetIndex, skipRules);

                // После добавления обеспечиваем минимум свободных слотов
                if (added && stack.IsEmpty)
                {
                    _ensureFreeSlotsFunc?.Invoke();
                }

                return added;
            }

            // РЕЖИМ 2: targetIndex не указан (добавление через TryAddItem)
            // В этом режиме мы ВСЕГДА создаем слоты если нужно (независимо от maxFreeSlots)

            // Сначала пробуем добавить в существующие слоты
            bool initialAdded = _baseStrategy.TryAdd(slots, stack, -1, skipRules);

            // Если не поместилось - создаем новые слоты и продолжаем
            if (!stack.IsEmpty && slots.Count < _maxSlots)
            {
                Extensions.DragAndDropLog($"<color=yellow>[DynamicSlots] Stack not empty ({stack.Count} remaining), creating new slots...</color>");

                int createdSlots = 0;
                while (!stack.IsEmpty && slots.Count < _maxSlots)
                {
                    var newSlot = _createSlotFunc();
                    if (newSlot == null)
                        break;

                    createdSlots++;
                    Extensions.DragAndDropLog($"<color=green>[DynamicSlots] Created slot {slots.Count - 1} for remaining items</color>");

                    // Пытаемся добавить в новый слот
                    _baseStrategy.TryAdd(slots, stack, slots.Count - 1, skipRules);
                }

                Extensions.DragAndDropLog($"<color=cyan>[DynamicSlots] Created {createdSlots} new slots, {stack.Count} items still remaining</color>");
            }

            // После добавления обеспечиваем минимум свободных слотов
            if (stack.Count < initialCount)
            {
                _ensureFreeSlotsFunc?.Invoke();
            }

            // Возвращаем true если хоть что-то добавилось
            return stack.Count == 0;
        }

        public bool TryRemove(List<BaseSlot> slots, IItemAdapter itemAdapter, int count, int sourceIndex)
        {
            bool removed = _baseStrategy.TryRemove(slots, itemAdapter, count, sourceIndex);

            // После удаления обеспечиваем минимум свободных слотов
            if (removed)
            {
                _ensureFreeSlotsFunc?.Invoke();
            }

            return removed;
        }

        public int GetItemCount(List<BaseSlot> slots, IItemAdapter itemAdapter)
        {
            return _baseStrategy.GetItemCount(slots, itemAdapter);
        }

        public bool Contains(List<BaseSlot> slots, IItemAdapter itemAdapter)
        {
            return _baseStrategy.Contains(slots, itemAdapter);
        }

        public int ResolveDragAmount(int stackCount, DragAmount dragAmount, int customDragAmount)
        {
            return _baseStrategy.ResolveDragAmount(stackCount, dragAmount, customDragAmount);
        }

        public bool RequiresStrategyPlacement(ItemStack stack)
        {
            return _baseStrategy.RequiresStrategyPlacement(stack);
        }

        public bool UsesPerItemSlotPlanning => _baseStrategy.UsesPerItemSlotPlanning;

        public bool CanUseAlternativeSlot(BaseSlot baseSlot, IItemAdapter itemAdapter)
        {
            return _baseStrategy.CanUseAlternativeSlot(baseSlot, itemAdapter);
        }

        public IEnumerable<BaseSlot> EnumerateAlternativeSlots(List<BaseSlot> slots, IItemAdapter itemAdapter, AlternativePlacementMode mode, BaseSlot excludeBaseSlot)
        {
            return _baseStrategy.EnumerateAlternativeSlots(slots, itemAdapter, mode, excludeBaseSlot);
        }

        public bool TryAddToSlot(List<BaseSlot> slots, ItemStack stack, BaseSlot targetBaseSlot, System.Action ensureFreeSlots, SlotOperationContext operationContext)
        {
            return _baseStrategy.TryAddToSlot(slots, stack, targetBaseSlot, ensureFreeSlots, operationContext);
        }

        public bool CanAcceptItem(List<BaseSlot> slots, InventoryAcceptanceRequest request, bool canCreateNewSlot, int potentialNewSlots, BaseSlot baseSlotPrefab, out BaseSlot suggestedBaseSlot)
        {
            return _baseStrategy.CanAcceptItem(slots, request, canCreateNewSlot, potentialNewSlots, baseSlotPrefab, out suggestedBaseSlot);
        }

        public int GetAcceptableCount(List<BaseSlot> slots, InventoryAcceptanceRequest request, bool canCreateNewSlot, int potentialNewSlots, BaseSlot baseSlotPrefab)
        {
            return _baseStrategy.GetAcceptableCount(slots, request, canCreateNewSlot, potentialNewSlots, baseSlotPrefab);
        }

        public void SetMaxStackSize(int maxStackSize, bool allowItemOverride)
        {
            _baseStrategy.SetMaxStackSize(maxStackSize, allowItemOverride);
        }
    }
}
