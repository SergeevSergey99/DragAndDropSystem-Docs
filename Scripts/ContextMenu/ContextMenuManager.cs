using System;
using System.Collections.Generic;
using System.Linq;
using CodeUtils;
using DragAndDropSystem.Inventories;
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
        [SerializeField] private ContextMenuViewBase _defaultViewPrefab;
        [SerializeField] private Transform _viewContainer;

        private readonly Dictionary<ContextMenuViewBase, ContextMenuViewBase> _viewCache = new Dictionary<ContextMenuViewBase, ContextMenuViewBase>();
        private readonly Dictionary<UniversalInventory, InventoryContextMenuViewBinder> _viewBindersByInventory = new Dictionary<UniversalInventory, InventoryContextMenuViewBinder>();
        private ContextMenuViewBase _activeView;

        public bool IsOpen { get; private set; }

        /// <summary>Вызывается после открытия меню.</summary>
        public event Action OnOpened;

        /// <summary>Вызывается после закрытия меню.</summary>
        public event Action OnClosed;

        public void RegisterViewBinder(InventoryContextMenuViewBinder binder)
        {
            if (binder == null || binder.Inventory == null)
                return;

            _viewBindersByInventory[binder.Inventory] = binder;
        }

        public void UnregisterViewBinder(InventoryContextMenuViewBinder binder)
        {
            if (binder == null || binder.Inventory == null)
                return;

            if (_viewBindersByInventory.TryGetValue(binder.Inventory, out var existing) && existing == binder)
                _viewBindersByInventory.Remove(binder.Inventory);
        }

        /// <summary>
        /// Показать контекстное меню: фильтрует записи через <see cref="IContextMenuEntry.CanShow"/>
        /// и передаёт видимые пункты во view.
        /// </summary>
        public void Show(IReadOnlyList<IContextMenuEntry> entries, ContextMenuContext ctx)
        {
            var view = ResolveView(ctx?.Inventory);
            if (view == null)
            {
                Debug.LogWarning("[ContextMenuManager] View prefab/instance is not assigned.");
                return;
            }

            if (IsOpen)
                Hide();

            var visible = entries
                .Where(e => e != null && e.CanShow(ctx))
                .OrderBy(e => e.Order)
                .ToList();

            if (visible.Count == 0)
            {
                IsOpen = false;
                return;
            }

            _activeView = view;
            _activeView.Show(visible, ctx);
            IsOpen = true;
            OnOpened?.Invoke();
        }

        public void Hide()
        {
            if (!IsOpen)
                return;

            if (_activeView != null)
                _activeView.Hide();

            IsOpen = false;
            _activeView = null;
            OnClosed?.Invoke();
        }

        private ContextMenuViewBase ResolveView(UniversalInventory inventory)
        {
            var prefab = ResolveViewPrefab(inventory);
            if (prefab != null)
                return GetOrCreateViewInstance(prefab);
            
            return null;
        }

        private ContextMenuViewBase ResolveViewPrefab(UniversalInventory inventory)
        {
            if (inventory != null &&
                _viewBindersByInventory.TryGetValue(inventory, out var binder) &&
                binder != null &&
                binder.ViewPrefab != null)
            {
                return binder.ViewPrefab;
            }

            return _defaultViewPrefab;
        }

        private ContextMenuViewBase GetOrCreateViewInstance(ContextMenuViewBase prefab)
        {
            if (prefab == null)
                return null;

            if (_viewCache.TryGetValue(prefab, out var cachedView) && cachedView != null)
                return cachedView;

            var container = _viewContainer != null ? _viewContainer : transform;
            var instance = Instantiate(prefab, container);
            _viewCache[prefab] = instance;
            return instance;
        }
    }
}
