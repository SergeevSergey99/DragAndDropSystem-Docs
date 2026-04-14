using UnityEngine;

namespace DragAndDropSystem.ContextMenu
{
    /// <summary>
    /// Base ScriptableObject for a single context menu entry.
    /// Subclass it in your project to add gameplay logic.
    /// </summary>
    public abstract class ContextMenuEntryDefinitionSO : ScriptableObject, IContextMenuEntry
    {
        [SerializeField] private string _label;
        [SerializeField] private Sprite _icon;
        [SerializeField] private int    _order = 0;

        /// <summary>Sort order in the menu (lower = higher).</summary>
        public int Order => _order;

        /// <summary>Entry text. Override for dynamic labels.</summary>
        public virtual string GetLabel(ContextMenuContext ctx) => _label;

        /// <summary>Entry icon. Override for dynamic icons.</summary>
        public virtual Sprite GetIcon(ContextMenuContext ctx) => _icon;

        /// <summary>Whether to show this entry for the current context.</summary>
        public abstract bool CanShow(ContextMenuContext ctx);

        /// <summary>Whether the entry is active. Override for disabled state.</summary>
        public virtual bool IsEnabled(ContextMenuContext ctx) => true;

        /// <summary>Execute the action.</summary>
        public abstract void Execute(ContextMenuContext ctx);
    }
}