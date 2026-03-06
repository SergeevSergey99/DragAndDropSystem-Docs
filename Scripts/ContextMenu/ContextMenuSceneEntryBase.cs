using UnityEngine;

namespace DragAndDropSystem.ContextMenu
{
    /// <summary>
    /// Базовый сценовый пункт контекстного меню.
    /// Используется для scene-bound логики, которую нельзя хранить в asset entry.
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
        public abstract void Execute(ContextMenuContext ctx);
    }
}
