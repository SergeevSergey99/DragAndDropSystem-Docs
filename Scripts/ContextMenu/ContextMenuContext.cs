using DragAndDropSystem.Core;
using DragAndDropSystem.Interaction;
using DragAndDropSystem.Slots;
using UnityEngine;

namespace DragAndDropSystem.ContextMenu
{
    public struct ContextMenuContext
    {
        /// <summary>Инвентарь, на котором вызвано меню.</summary>
        public Inventories.UniversalInventory Inventory;

        /// <summary>Слот, на котором кликнули (может быть null).</summary>
        public ISlot Slot;

        /// <summary>Предмет в слоте. null если слот пустой.</summary>
        public IItemAdapter ItemAdapter;

        /// <summary>Количество предметов в стаке.</summary>
        public int ItemCount;

        /// <summary>Экранная позиция клика.</summary>
        public Vector2 ScreenPosition;

        /// <summary>Источник ввода (мышь / геймпад / и т.д.).</summary>
        public FocusSource InputSource;
    }
}
