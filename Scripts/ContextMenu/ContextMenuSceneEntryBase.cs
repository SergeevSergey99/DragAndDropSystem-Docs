using UnityEngine;

namespace UniversalDragAndDrop.ContextMenu
{
    /// <summary>
    /// Base scene-bound context menu entry.
    /// Used for scene-bound logic that cannot be stored in an asset entry.
    /// </summary>
    public abstract class ContextMenuSceneEntryBase : MonoBehaviour, IContextMenuEntry
    {
        [SerializeField] private string _label;
        [SerializeField] private Sprite _icon;
        [SerializeField] private int _order;

        public int Order => _order;

        public virtual string GetLabel(ContextMenuContext ctx) => _label;
        public virtual Sprite GetIcon(ContextMenuContext ctx) => _icon;

        public abstract bool CanShow(ContextMenuContext ctx);

        /// <summary>Whether the entry is active. Override for disabled state.</summary>
        public virtual bool IsEnabled(ContextMenuContext ctx) => true;

        public abstract void Execute(ContextMenuContext ctx);
    }
}
