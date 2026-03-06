using System.Collections.Generic;
using System.Linq;
using CodeUtils;
using UnityEngine;

namespace DragAndDropSystem.ContextMenu
{
    /// <summary>
    /// Синглтон-менеджер контекстного меню.
    /// Добавьте на сцену и назначьте <see cref="ContextMenuViewBase"/>.
    /// </summary>
    [DisallowMultipleComponent]
    public class ContextMenuManager : MonoSingleton<ContextMenuManager>
    {
        [SerializeField] private ContextMenuViewBase _view;

        public bool IsOpen { get; private set; }

        /// <summary>
        /// Показать контекстное меню: фильтрует записи через <see cref="ContextMenuEntryDefinitionSO.CanShow"/>
        /// и передаёт видимые пункты во view.
        /// </summary>
        public void Show(IReadOnlyList<ContextMenuEntryDefinitionSO> entries, ContextMenuContext ctx)
        {
            if (_view == null)
            {
                Debug.LogWarning("[ContextMenuManager] View is not assigned.");
                return;
            }

            var visible = entries
                .Where(e => e != null && e.CanShow(ctx))
                .OrderBy(e => e.Order)
                .ToList();

            if (visible.Count == 0)
                return;

            _view.Show(visible, ctx);
            IsOpen = true;
        }

        public void Hide()
        {
            if (_view != null)
                _view.Hide();

            IsOpen = false;
        }
    }
}
