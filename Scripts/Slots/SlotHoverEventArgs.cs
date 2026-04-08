using DragAndDropSystem.Core;
using DragAndDropSystem.Inventories;
using UnityEngine;

namespace DragAndDropSystem.Slots
{
    /// <summary>
    /// Аргументы события наведения на слот.
    /// Содержит всю информацию о слоте, предмете и позиции для использования в tooltip и других системах.
    /// </summary>
    public class SlotHoverEventArgs
    {
        /// <summary>
        /// Предмет в слоте (может быть null если слот пустой)
        /// </summary>
        public IItemAdapter ItemAdapter { get; }

        /// <summary>
        /// Слот на который навели курсор
        /// </summary>
        public BaseSlot BaseSlot { get; }

        /// <summary>
        /// Позиция курсора в экранных координатах
        /// </summary>
        public Vector2 ScreenPosition { get; set; }

        /// <summary>
        /// RectTransform слота (для позиционирования tooltip относительно слота)
        /// </summary>
        public RectTransform SlotRectTransform { get; }

        /// <summary>
        /// True если это событие входа (OnPointerEnter), False если выхода (OnPointerExit)
        /// </summary>
        public bool IsEnter { get; }

        /// <summary>
        /// Инвентарь к которому принадлежит слот
        /// </summary>
        public IInventory Inventory { get; }

        /// <summary>
        /// Индекс слота в инвентаре
        /// </summary>
        public int SlotIndex { get; }

        /// <summary>
        /// Флаг для отмены дальнейшей обработки события (можно использовать в наследниках)
        /// </summary>
        public bool Cancel { get; set; }

        public SlotHoverEventArgs(
            IItemAdapter itemAdapter,
            BaseSlot baseSlot,
            Vector2 screenPosition,
            RectTransform rectTransform,
            bool isEnter)
        {
            ItemAdapter = itemAdapter;
            BaseSlot = baseSlot;
            ScreenPosition = screenPosition;
            SlotRectTransform = rectTransform;
            IsEnter = isEnter;
            Inventory = baseSlot?.Inventory;
            SlotIndex = baseSlot?.Index ?? -1;
            Cancel = false;
        }

        /// <summary>
        /// Проверить что в слоте есть предмет
        /// </summary>
        public bool HasItem => ItemAdapter != null;

        /// <summary>
        /// Проверить что слот пустой
        /// </summary>
        public bool IsEmpty => ItemAdapter == null;

        /// <summary>
        /// Получить позицию слота в мировых координатах
        /// </summary>
        public Vector3 GetSlotWorldPosition()
        {
            return SlotRectTransform != null ? SlotRectTransform.position : Vector3.zero;
        }

        /// <summary>
        /// Получить размер слота
        /// </summary>
        public Vector2 GetSlotSize()
        {
            return SlotRectTransform != null ? SlotRectTransform.rect.size : Vector2.zero;
        }

        public override string ToString()
        {
            return $"SlotHover[{(IsEnter ? "Enter" : "Exit")}] _PrimaryAdapter: {ItemAdapter?.DisplayName ?? "Empty"}, Slot: {SlotIndex}, Pos: {ScreenPosition}";
        }
    }
}
