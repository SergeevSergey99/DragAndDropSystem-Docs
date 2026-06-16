using System;
using System.Collections.Generic;
using CodeUtils;
using UnityEngine;
using UDND.Core;
using UDND.Inventories;
using UDND.Slots;

namespace UDND.Selection
{
    /// <summary>
    /// Singleton that manages slot selection state.
    /// Internally uses mutable structures and exposes only immutable SelectionContext outside.
    /// </summary>
    [DisallowMultipleComponent]
    public class SelectionManager : MonoSingleton<SelectionManager>
    {
        // Internal mutable state
        private readonly HashSet<BaseSlot> _selected = new HashSet<BaseSlot>();
        private readonly Dictionary<IInventory, List<BaseSlot>> _byInventory = new Dictionary<IInventory, List<BaseSlot>>();

        // Last selected slot, used for range selection (Shift+Click)
        private BaseSlot _lastSelectedBaseSlot;

        /// <summary>
        /// Current immutable snapshot of selection.
        /// Recreated on every change.
        /// </summary>
        public SelectionContext CurrentContext { get; private set; } = SelectionContext.Empty;

        /// <summary>
        /// Called whenever selection changes
        /// </summary>
        // Selection-changed event lives on UDNDEvents (UDNDEvents.OnSelectionChanged).

        // ===== Public API =====

        public bool IsSelected(BaseSlot baseSlot) => baseSlot != null && _selected.Contains(baseSlot);

        /// <summary>
        /// Add a slot to selection
        /// </summary>
        public void Select(BaseSlot baseSlot)
        {
            if (baseSlot == null || _selected.Contains(baseSlot)) return;
            AddInternal(baseSlot);
            _lastSelectedBaseSlot = baseSlot;
            RebuildContext();
        }

        /// <summary>
        /// Remove a slot from selection
        /// </summary>
        public void Deselect(BaseSlot baseSlot)
        {
            if (baseSlot == null || !_selected.Contains(baseSlot)) return;
            RemoveInternal(baseSlot);
            RebuildContext();
        }

        /// <summary>
        /// Toggle slot selection state
        /// </summary>
        public void Toggle(BaseSlot baseSlot)
        {
            if (baseSlot == null) return;
            if (_selected.Contains(baseSlot)) Deselect(baseSlot);
            else Select(baseSlot);
        }

        /// <summary>
        /// Select a range of slots from the last selected one to the specified slot (within one inventory).
        /// If there is no last selected slot or it belongs to another inventory, only the specified slot is selected.
        /// </summary>
        public void SelectRange(BaseSlot toBaseSlot)
        {
            if (toBaseSlot == null) return;

            if (_lastSelectedBaseSlot == null || _lastSelectedBaseSlot.Inventory != toBaseSlot.Inventory)
            {
                Select(toBaseSlot);
                return;
            }

            int from = _lastSelectedBaseSlot.Index;
            int to   = toBaseSlot.Index;
            int min  = Mathf.Min(from, to);
            int max  = Mathf.Max(from, to);

            var inventory = toBaseSlot.Inventory;
            for (int i = min; i <= max; i++)
            {
                var slot = inventory.GetSlot(i);
                if (slot != null && !_selected.Contains(slot))
                    AddInternal(slot);
            }

            _lastSelectedBaseSlot = toBaseSlot;
            RebuildContext();
        }

        /// <summary>
        /// Select all slots in the inventory
        /// </summary>
        public void SelectAll(IInventory inventory)
        {
            if (inventory == null) return;

            foreach (var slot in inventory.Slots)
            {
                if (slot != null && !_selected.Contains(slot))
                    AddInternal(slot);
            }

            RebuildContext();
        }

        /// <summary>
        /// Clear all selection
        /// </summary>
        public void Clear()
        {
            if (_selected.Count == 0) return;
            _selected.Clear();
            _byInventory.Clear();
            _lastSelectedBaseSlot = null;
            RebuildContext();
        }

        // ===== Private methods =====

        private void AddInternal(BaseSlot baseSlot)
        {
            _selected.Add(baseSlot);

            if (!_byInventory.TryGetValue(baseSlot.Inventory, out var list))
            {
                list = new List<BaseSlot>();
                _byInventory[baseSlot.Inventory] = list;
            }
            list.Add(baseSlot);
        }

        private void RemoveInternal(BaseSlot baseSlot)
        {
            _selected.Remove(baseSlot);

            if (!_byInventory.TryGetValue(baseSlot.Inventory, out var list)) return;
            list.Remove(baseSlot);
            if (list.Count == 0)
                _byInventory.Remove(baseSlot.Inventory);
        }

        private void RebuildContext()
        {
            // Build a flat list from _byInventory to preserve ordering by inventory
            var allSlots = new List<BaseSlot>(_selected.Count);
            foreach (var slots in _byInventory.Values)
                allSlots.AddRange(slots);

            CurrentContext = new SelectionContext(_byInventory, allSlots, _selected);
            UDNDEvents.RaiseSelectionChanged(CurrentContext);
        }
    }
}