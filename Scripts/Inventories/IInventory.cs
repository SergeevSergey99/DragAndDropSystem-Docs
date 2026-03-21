using System.Collections.Generic;
using DragAndDropSystem.Core;
using DragAndDropSystem.DataBinding;
using DragAndDropSystem.Slots;

namespace DragAndDropSystem.Inventories
{
    /// <summary>
    /// Базовый интерфейс для инвентаря
    /// </summary>
    public interface IInventory
    {
        /// <summary>
        /// Все слоты инвентаря
        /// </summary>
        IReadOnlyList<ISlot> Slots { get; }

        /// <summary>
        /// Количество слотов
        /// </summary>
        int SlotCount { get; }

        public InventoryDataBindingBase DataBinding { get; }
        /// <summary>
        /// Получить слот по индексу
        /// </summary>
        ISlot GetSlot(int index);

        /// <summary>
        /// Попытаться добавить предмет
        /// </summary>
        bool TryAddItem(IInventoryItem item, int count = 1, int targetSlotIndex = -1);

        /// <summary>
        /// Попытаться удалить предмет
        /// </summary>
        bool TryRemoveItem(IInventoryItem item, int count = 1, int sourceSlotIndex = -1);

        /// <summary>
        /// Попытаться добавить стак
        /// </summary>
        bool TryAddStack(ItemStack stack, int targetSlotIndex = -1);

        /// <summary>
        /// Проверить, есть ли предмет в инвентаре
        /// </summary>
        bool Contains(IInventoryItem item);

        /// <summary>
        /// Получить количество предмета в инвентаре
        /// </summary>
        int GetItemCount(IInventoryItem item);

        /// <summary>
        /// Обновить визуализацию всех слотов
        /// </summary>
        void UpdateAllVisuals();

        /// <summary>
        /// Получить количество предметов для перетаскивания из слота
        /// </summary>
        int GetDragAmount(ISlot slot);

        /// <summary>
        /// Попытаться добавить предмет в конкретный слот с учетом настроек инвентаря
        /// </summary>
        /// <param name="stack">Стак предметов для добавления</param>
        /// <param name="targetSlot">Целевой слот</param>
        /// <param name="sourceInventory">Инвентарь-источник (для событий)</param>
        /// <param name="sourceSlotIndex">Индекс исходного слота (для событий)</param>
        bool TryAddToSlot(
            ItemStack stack,
            ISlot targetSlot,
            IInventory sourceInventory = null,
            int sourceSlotIndex = -1,
            SlotOperationContext operationContext = null);

        /// <summary>
        /// Получить количество предметов данного типа, которое инвентарь может принять.
        /// Учитывает стратегию инвентаря (Unique, Stackable, SeparableStacks),
        /// свободные слоты и правила валидации.
        /// </summary>
        /// <param name="item">Предмет для проверки</param>
        /// <param name="desiredCount">Желаемое количество</param>
        /// <returns>Количество предметов, которое реально может быть принято (0 до desiredCount)</returns>
        int GetAcceptableCount(IInventoryItem item, int desiredCount);

        /// <summary>
        /// Получить количество предметов, которое инвентарь может принять
        /// в контексте конкретной drag/drop операции.
        /// </summary>
        int GetAcceptableCount(InventoryAcceptanceRequest request);
    }
}
