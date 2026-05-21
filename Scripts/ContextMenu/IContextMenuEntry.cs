using UnityEngine;

namespace UDND.ContextMenu
{
    /// <summary>
    /// Runtime contract for a context menu entry.
    /// Allows mixing asset-based and scene-based entries in the same list.
    /// </summary>
    public interface IContextMenuEntry
    {
        int Order { get; }
        string GetLabel(ContextMenuContext ctx);
        Sprite GetIcon(ContextMenuContext ctx);
        bool CanShow(ContextMenuContext ctx);
        /// <summary>Whether the entry is active. false means the entry is visible but cannot be selected.</summary>
        bool IsEnabled(ContextMenuContext ctx);
        void Execute(ContextMenuContext ctx);
    }
}
