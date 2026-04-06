using UnityEngine;

namespace DragAndDropSystem.Core
{
    /// <summary>
    /// Базовый интерфейс для любого предмета в инвентаре
    /// Минимальный набор свойств для работы системы
    /// </summary>
    public interface IItemAdapter
    {
        /// <summary>
        /// Уникальный идентификатор предмета
        /// </summary>
        string ItemId { get; }

        /// <summary>
        /// Item icon для отображения
        /// </summary>
        Sprite Icon { get; }

        /// <summary>
        /// Отображаемое имя предмета
        /// </summary>
        string DisplayName { get; }
    }
}
