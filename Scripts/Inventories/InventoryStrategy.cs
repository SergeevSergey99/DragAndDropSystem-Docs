using System.Collections.Generic;
using DragAndDropSystem.Core;
using DragAndDropSystem.Slots;
using DragAndDropSystem.Tools;

namespace DragAndDropSystem.Inventories
{
    /// <summary>
    /// Стратегия управления слотами инвентаря
    /// </summary>
    public interface IInventoryStrategy
    {
        /// <summary>
        /// Попытаться добавить стак в инвентарь
        /// </summary>
        bool TryAdd(List<ISlot> slots, ItemStack stack, int targetIndex);

        /// <summary>
        /// Попытаться удалить предмет из инвентаря
        /// </summary>
        bool TryRemove(List<ISlot> slots, IInventoryItem item, int count, int sourceIndex);

        /// <summary>
        /// Получить количество предмета в инвентаре
        /// </summary>
        int GetItemCount(List<ISlot> slots, IInventoryItem item);

        /// <summary>
        /// Проверить наличие предмета
        /// </summary>
        bool Contains(List<ISlot> slots, IInventoryItem item);
    }

    /// <summary>
    /// Базовая стратегия с общими методами
    /// </summary>
    public abstract class InventoryStrategyBase : IInventoryStrategy
    {
        public abstract bool TryAdd(List<ISlot> slots, ItemStack stack, int targetIndex);
        public abstract bool TryRemove(List<ISlot> slots, IInventoryItem item, int count, int sourceIndex);

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
    }

    /// <summary>
    /// Стратегия: каждый предмет занимает отдельный слот (не стакается)
    /// Используется для инвентарей с уникальными предметами
    /// </summary>
    public class UniqueItemStrategy : InventoryStrategyBase
    {
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
    }

    /// <summary>
    /// Стратегия: предметы стакаются (группируются по типу)
    /// Один тип предмета может занимать несколько слотов
    /// Лимиты стаков контролируются через правила (MaxStackSizeRule)
    /// </summary>
    public class StackableItemStrategy : InventoryStrategyBase
    {
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
    }

    /// <summary>
    /// Декоратор для динамического создания слотов
    /// Оборачивает любую стратегию (Unique или Stackable) и добавляет автоматическое создание слотов
    /// </summary>
    public class DynamicSlotDecorator : IInventoryStrategy
    {
        private readonly IInventoryStrategy _baseStrategy;
        private readonly System.Func<ISlot> _createSlotFunc;
        private readonly int _maxSlots;
        private readonly int _maxFreeSlots;
        private readonly System.Func<List<ISlot>> _getSlotsFunc;
        private readonly System.Action _ensureFreeSlotsFunc;

        public DynamicSlotDecorator(
            IInventoryStrategy baseStrategy,
            System.Func<ISlot> createSlotFunc,
            int maxSlots = 100,
            int maxFreeSlots = 1,
            System.Func<List<ISlot>> getSlotsFunc = null,
            System.Action ensureFreeSlotsFunc = null)
        {
            _baseStrategy = baseStrategy;
            _createSlotFunc = createSlotFunc;
            _maxSlots = maxSlots;
            _maxFreeSlots = maxFreeSlots;
            _getSlotsFunc = getSlotsFunc;
            _ensureFreeSlotsFunc = ensureFreeSlotsFunc;
        }

        public bool TryAdd(List<ISlot> slots, ItemStack stack, int targetIndex)
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
                        Extentions.DragAndDropLog($"<color=green>[DynamicSlots] Created slot {slots.Count - 1} to reach targetIndex {targetIndex}</color>");
                    }
                }

                // Пытаемся добавить в целевой слот
                bool added = _baseStrategy.TryAdd(slots, stack, targetIndex);

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
            bool initialAdded = _baseStrategy.TryAdd(slots, stack, -1);

            // Если не поместилось - создаем новые слоты и продолжаем
            if (!stack.IsEmpty && slots.Count < _maxSlots)
            {
                Extentions.DragAndDropLog($"<color=yellow>[DynamicSlots] Stack not empty ({stack.Count} remaining), creating new slots...</color>");

                int createdSlots = 0;
                while (!stack.IsEmpty && slots.Count < _maxSlots)
                {
                    var newSlot = _createSlotFunc();
                    if (newSlot == null)
                        break;

                    createdSlots++;
                    Extentions.DragAndDropLog($"<color=green>[DynamicSlots] Created slot {slots.Count - 1} for remaining items</color>");

                    // Пытаемся добавить в новый слот
                    _baseStrategy.TryAdd(slots, stack, slots.Count - 1);
                }

                Extentions.DragAndDropLog($"<color=cyan>[DynamicSlots] Created {createdSlots} new slots, {stack.Count} items still remaining</color>");
            }

            // После добавления обеспечиваем минимум свободных слотов
            if (stack.Count < initialCount)
            {
                _ensureFreeSlotsFunc?.Invoke();
            }

            // Возвращаем true если хоть что-то добавилось
            return stack.Count == 0;
        }

        public bool TryRemove(List<ISlot> slots, IInventoryItem item, int count, int sourceIndex)
        {
            bool removed = _baseStrategy.TryRemove(slots, item, count, sourceIndex);

            // После удаления обеспечиваем минимум свободных слотов
            if (removed)
            {
                _ensureFreeSlotsFunc?.Invoke();
            }

            return removed;
        }

        public int GetItemCount(List<ISlot> slots, IInventoryItem item)
        {
            return _baseStrategy.GetItemCount(slots, item);
        }

        public bool Contains(List<ISlot> slots, IInventoryItem item)
        {
            return _baseStrategy.Contains(slots, item);
        }
    }

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
    }
}
