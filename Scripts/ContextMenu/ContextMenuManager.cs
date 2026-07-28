using System;
using System.Collections.Generic;
using System.Linq;
using CodeUtils;
using UnityEngine;
using UDND.Inventories;

namespace UDND.ContextMenu
{
    /// <summary>
    /// Singleton context menu manager.
    /// Add it to the scene and assign <see cref="ContextMenuViewBase"/>.
    /// </summary>
    [DisallowMultipleComponent]
    public class ContextMenuManager : MonoSingleton<ContextMenuManager>
    {
        [SerializeField] private ContextMenuViewBase _defaultViewPrefab;
        [SerializeField] private Transform _viewContainer;
        [SerializeField] private ContextMenuPreset _defaultPreset;
        [SerializeField] private ContextMenuPreset _defaultEmptySlotPreset;

        private readonly Dictionary<ContextMenuViewBase, ContextMenuViewBase> _viewCache = new();
        private readonly Dictionary<IInventory, InventoryContextMenuViewBinder> _viewBindersByInventory = new();
        private readonly Dictionary<IInventory, ContextMenuBinder> _contextBindersByInventory = new();
        
        private ContextMenuViewBase _activeView;

        public bool IsOpen { get; private set; }
        public ContextMenuPreset DefaultPreset => _defaultPreset;
        public ContextMenuPreset DefaultEmptySlotPreset => _defaultEmptySlotPreset;
        
        ContextMenuContext _lastContext;
            
        /// <summary>Called after the menu is opened.</summary>
        public event Action OnOpened;

        /// <summary>Called after the menu is closed.</summary>
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
        
        public void RegisterBinder(ContextMenuBinder binder)
        {
            if (binder == null || binder.Inventory == null)
                return;

            _contextBindersByInventory[binder.Inventory] = binder;
        }

        public void UnregisterBinder(ContextMenuBinder binder)
        {
            if (binder == null || binder.Inventory == null)
                return;

            if (_contextBindersByInventory.TryGetValue(binder.Inventory, out var existing) && existing == binder)
                _contextBindersByInventory.Remove(binder.Inventory);
        }
        
        public List<IContextMenuEntry> GetEntries(ContextMenuContext context)
        {
            if(_contextBindersByInventory.TryGetValue(context.Inventory, out var binder))
                return binder.GetEntries(context);
                
            if (context.BaseSlot == null || context.BaseSlot.IsEmpty)
                return _defaultEmptySlotPreset != null ? new List<IContextMenuEntry>(_defaultEmptySlotPreset.Entries.Where(entry => entry != null)) : new List<IContextMenuEntry>();
            return _defaultPreset != null ? new List<IContextMenuEntry>(_defaultPreset.Entries.Where(entry => entry != null)) : new List<IContextMenuEntry>();
        }

        /// <summary>
        /// Show the context menu: filters entries through <see cref="IContextMenuEntry.CanShow"/>
        /// and passes visible entries to the view.
        /// </summary>
        public void Show(IReadOnlyList<IContextMenuEntry> entries, ContextMenuContext ctx)
        {
            var visible = entries
                .Where(e => e != null && e.CanShow(ctx))
                .OrderBy(e => e.Order)
                .ToList();
            
            if (visible.Count == 0)
            {
                Hide();
                return;
            }
            
            var view = ResolveView(ctx.Inventory);
            if (view == null)
            {
                Debug.LogWarning("[ContextMenuManager] View prefab/instance is not assigned.");
                return;
            }
            if (IsOpen)
            {
                // Hide() resets _activeView, so the field is assigned only after this block —
                // a repeated click on the same slot closes the menu and stops here (toggle behaviour).
                Hide();
                if (ctx.BaseSlot == _lastContext.BaseSlot && ctx.Inventory == _lastContext.Inventory)
                {
                    return;
                }
            }
            _lastContext = ctx;

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

        private ContextMenuViewBase ResolveView(IInventory inventory)
        {
            var prefab = ResolveViewPrefab(inventory);
            if (prefab != null)
                return GetOrCreateViewInstance(prefab);
            
            return null;
        }

        private ContextMenuViewBase ResolveViewPrefab(IInventory inventory)
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
