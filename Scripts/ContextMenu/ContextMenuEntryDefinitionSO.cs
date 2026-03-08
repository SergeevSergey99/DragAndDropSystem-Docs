using UnityEngine;

namespace DragAndDropSystem.ContextMenu
{
    /// <summary>
    /// Базовый SO для одного пункта контекстного меню.
    /// Субклассируйте в своём проекте чтобы добавить игровую логику.
    /// </summary>
    public abstract class ContextMenuEntryDefinitionSO : ScriptableObject, IContextMenuEntry
    {
        [SerializeField] private string _label;
        [SerializeField] private Sprite _icon;
        [SerializeField] private int    _order = 0;

        /// <summary>Порядок сортировки в меню (меньше = выше).</summary>
        public int Order => _order;

        /// <summary>Текст пункта. Переопределяйте для динамических лейблов.</summary>
        public virtual string GetLabel(ContextMenuContext ctx) => _label;

        /// <summary>Иконка пункта. Переопределяйте для динамических иконок.</summary>
        public virtual Sprite GetIcon(ContextMenuContext ctx) => _icon;

        /// <summary>Показывать ли этот пункт для данного контекста.</summary>
        public abstract bool CanShow(ContextMenuContext ctx);

        /// <summary>Активен ли пункт. Переопределяйте для disabled-состояния.</summary>
        public virtual bool IsEnabled(ContextMenuContext ctx) => true;

        /// <summary>Выполнить действие.</summary>
        public abstract void Execute(ContextMenuContext ctx);
    }
}
