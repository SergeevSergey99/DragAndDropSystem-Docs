using System.Collections.Generic;
using DragAndDropSystem.Core;
using UnityEngine;

namespace DragAndDropSystem.UI
{
    /// <summary>
    /// Интерфейс для визуализации перетаскиваемого предмета
    /// </summary>
    public interface IDragVisual
    {
        /// <summary>
        /// Показать визуал с указанными entries
        /// </summary>
        void Show(IReadOnlyList<DragEntry> entries);

        /// <summary>
        /// Скрыть визуал
        /// </summary>
        void Hide();

        /// <summary>
        /// Обновить позицию визуала
        /// </summary>
        void UpdatePosition(Vector3 position);

        /// <summary>
        /// Проверка: визуал активен?
        /// </summary>
        bool IsVisible { get; }
    }
}
