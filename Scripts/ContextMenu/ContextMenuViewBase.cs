using System.Collections.Generic;
using UnityEngine;

namespace DragAndDropSystem.ContextMenu
{
    /// <summary>
    /// Базовый класс для UI контекстного меню.
    /// Реализуйте в своём проекте (UGUI, UI Toolkit, и т.д.).
    /// </summary>
    public abstract class ContextMenuViewBase : MonoBehaviour
    {
        /// <summary>Показать меню с заданными пунктами.</summary>
        public abstract void Show(IReadOnlyList<IContextMenuEntry> entries, ContextMenuContext ctx);

        /// <summary>Скрыть меню.</summary>
        public abstract void Hide();
    }
}
