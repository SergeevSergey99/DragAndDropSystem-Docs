using System.Collections.Generic;
using UnityEngine;

namespace DragAndDropSystem.ContextMenu
{
    /// <summary>
    /// Base class for context menu UI.
    /// Implement it in your project (UGUI, UI Toolkit, etc.).
    /// </summary>
    public abstract class ContextMenuViewBase : MonoBehaviour
    {
        /// <summary>Show the menu with the specified entries.</summary>
        public abstract void Show(IReadOnlyList<IContextMenuEntry> entries, ContextMenuContext ctx);

        /// <summary>Hide the menu.</summary>
        public abstract void Hide();
    }
}