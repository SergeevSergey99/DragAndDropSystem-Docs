using UnityEngine;

namespace DragAndDropSystem.ContextMenu
{
    /// <summary>
    /// Runtime-контракт пункта контекстного меню.
    /// Позволяет смешивать asset-based и scene-based entries в одном списке.
    /// </summary>
    public interface IContextMenuEntry
    {
        int Order { get; }
        string GetLabel(ContextMenuContext ctx);
        Sprite GetIcon(ContextMenuContext ctx);
        bool CanShow(ContextMenuContext ctx);
        /// <summary>Активен ли пункт. false — пункт отображается, но недоступен для выбора.</summary>
        bool IsEnabled(ContextMenuContext ctx);
        void Execute(ContextMenuContext ctx);
    }
}
