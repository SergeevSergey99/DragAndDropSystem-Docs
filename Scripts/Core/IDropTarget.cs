using DragAndDropSystem.Inventories;
using DragAndDropSystem.Slots;

namespace DragAndDropSystem.Core
{
    /// <summary>
    /// Интерфейс для объектов, которые могут быть целью drop операции
    /// Реализуется компонентами, которые принимают drag-and-drop (слоты, области и т.д.)
    /// </summary>
    public interface IDropTarget
    {
        /// <summary>
        /// Получить целевой слот (может быть null для областей типа InventoryDropArea)
        /// </summary>
        ISlot GetTargetSlot();

        /// <summary>
        /// Получить целевой инвентарь
        /// </summary>
        IInventory GetTargetInventory();

        /// <summary>
        /// Вызывается когда этот target становится активным (верхним в стеке целей)
        /// Используется для визуальной подсветки
        /// </summary>
        void OnBecomeActiveTarget();

        /// <summary>
        /// Вызывается когда этот target перестаёт быть активным
        /// Используется для снятия визуальной подсветки
        /// </summary>
        void OnBecomeInactiveTarget();
    }
}
