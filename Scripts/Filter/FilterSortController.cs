using System;
using System.Collections.Generic;
using UnityEngine;
using UDND.Core;
using UDND.Inventories;
using UDND.Slots;
using UDND.Tools.Inspector;

namespace UDND.Filter
{
    /// <summary>
    /// Inventory filtering and sorting controller.
    /// Manages slot visibility and order without modifying inventory data.
    /// Works with any <see cref="ISlotFilter"/> and <see cref="ISlotSorter"/> implementations.
    /// </summary>
    public class FilterSortController : MonoBehaviour
    {
        [SerializeField, Required]
        private UniversalInventory _inventory;

        [Header("Filter Settings")]
        [SerializeField, Tooltip("How filtered-out slots are displayed")]
        private FilterDisplayMode _filterDisplayMode = FilterDisplayMode.Dim;

        [SerializeField, Tooltip("Hide empty slots during filtering")]
        private bool _hideEmptySlots;

        [Header("Sort Settings")]
        [SerializeField, Tooltip("Sort direction")]
        private bool _sortAscending = true;

        [Header("Debug")]
        [ShowInInspector, ReadOnly]
        private int _visibleSlotCount;

        [ShowInInspector, ReadOnly]
        private bool _isFilterActive;

        private ISlotFilter _activeFilter;
        private ISlotSorter _activeSorter;
        private readonly List<BaseSlot> _filteredSlots = new List<BaseSlot>();
        private readonly Dictionary<BaseSlot, int> _slotIndexCache = new Dictionary<BaseSlot, int>();
        private FilterSortPreset _activePreset;

        public UniversalInventory Inventory => _inventory;
        public bool IsFilterActive => _isFilterActive;
        public int VisibleSlotCount => _visibleSlotCount;
        public FilterDisplayMode DisplayMode => _filterDisplayMode;
        public bool SortAscending => _sortAscending;
        public ISlotFilter ActiveFilter => _activeFilter;
        public ISlotSorter ActiveSorter => _activeSorter;
        public FilterSortPreset ActivePreset => _activePreset;

        public event Action OnFilterChanged;
        public event Action OnSortChanged;

        private void Awake()
        {
            if (_inventory == null)
                _inventory = GetComponent<UniversalInventory>();
        }

        private void OnEnable()
        {
            if (_inventory != null)
            {
                _inventory.OnItemAdded += OnInventoryChanged;
                _inventory.OnItemRemoved += OnInventoryChanged;
                _inventory.OnContentRefreshed += OnInventoryContentRefreshed;
            }
        }

        private void OnDisable()
        {
            if (_inventory != null)
            {
                _inventory.OnItemAdded -= OnInventoryChanged;
                _inventory.OnItemRemoved -= OnInventoryChanged;
                _inventory.OnContentRefreshed -= OnInventoryContentRefreshed;
            }
        }
        
        private void OnInventoryChanged(InventoryItemEventContext context)
        {
            ApplyFilterAndSort();
        }

        private void OnInventoryContentRefreshed()
        {
            ApplyFilterAndSort();
        }

        // ── Filter API ──────────────────────────────────────────────

        /// <summary>Set a filter from any <see cref="ISlotFilter"/> implementation.</summary>
        public void SetFilter(ISlotFilter filter)
        {
            _activePreset = null;
            _activeFilter = filter;
            _isFilterActive = filter != null;
            ApplyFilterAndSort();
            OnFilterChanged?.Invoke();
        }

        /// <summary>Set a filter from a lambda / delegate.</summary>
        public void SetFilter(FilterPredicate predicate)
        {
            SetFilter(predicate != null ? new DelegateFilter(predicate) : null);
        }

        /// <summary>Clear the active filter.</summary>
        public void ClearFilter()
        {
            _activePreset = null;
            _activeFilter = null;
            _isFilterActive = false;
            ApplyFilterAndSort();
            OnFilterChanged?.Invoke();
        }

        // ── Sort API ────────────────────────────────────────────────

        /// <summary>Set a sorter from any <see cref="ISlotSorter"/> implementation.</summary>
        public void SetSorter(ISlotSorter sorter, bool ascending = true)
        {
            _activePreset = null;
            _activeSorter = sorter;
            _sortAscending = ascending;
            ApplyFilterAndSort();
            OnSortChanged?.Invoke();
        }

        /// <summary>Set a sorter from a lambda / delegate.</summary>
        public void SetSorter(SortComparison comparison, bool ascending = true)
        {
            SetSorter(comparison != null ? new DelegateSorter(comparison) : null, ascending);
        }

        /// <summary>Clear the active sorter.</summary>
        public void ClearSorter()
        {
            _activePreset = null;
            _activeSorter = null;
            ApplyFilterAndSort();
            OnSortChanged?.Invoke();
        }

        // ── Preset API ─────────────────────────────────────────────

        /// <summary>Apply a combined filter+sort preset.</summary>
        public void ApplyPreset(FilterSortPreset preset)
        {
            _activePreset = preset;

            if (preset == null)
            {
                _activeFilter = null;
                _activeSorter = null;
                _isFilterActive = false;
                _filterDisplayMode = FilterDisplayMode.Dim;
            }
            else
            {
                _activeFilter = preset.Filter;
                _activeSorter = preset.Sorter;
                _isFilterActive = preset.Filter != null;
                _sortAscending = preset.Ascending;
                _filterDisplayMode = preset.DisplayMode;
            }

            ApplyFilterAndSort();
            OnFilterChanged?.Invoke();
            OnSortChanged?.Invoke();
        }

        // ── Combined API ────────────────────────────────────────────

        /// <summary>Clear all filtering and sorting, restore original state.</summary>
        public void ClearAll()
        {
            _activePreset = null;
            _activeFilter = null;
            _activeSorter = null;
            _isFilterActive = false;
            ApplyFilterAndSort();
            OnFilterChanged?.Invoke();
            OnSortChanged?.Invoke();
        }

        /// <summary>
        /// Re-evaluate current filter and sort.
        /// Call after changing runtime fields on the active filter/sorter instances.
        /// </summary>
        [Button("Refresh")]
        public void Refresh()
        {
            ApplyFilterAndSort();
        }

        /// <summary>Set display mode for filtered-out slots.</summary>
        public void SetDisplayMode(FilterDisplayMode mode)
        {
            _filterDisplayMode = mode;
            ApplyFilterAndSort();
        }

        // ── Query API ───────────────────────────────────────────────

        /// <summary>Get the list of visible (filter-passing) slots.</summary>
        public IReadOnlyList<BaseSlot> GetVisibleSlots() => _filteredSlots;

        /// <summary>Check whether a slot is visible (passes the filter).</summary>
        public bool IsSlotVisible(BaseSlot baseSlot) => _filteredSlots.Contains(baseSlot);

        // ── Core Pipeline ───────────────────────────────────────────

        [Button("Apply Filter & Sort")]
        private void ApplyFilterAndSort()
        {
            if (_inventory == null)
                return;

            var slots = _inventory.Slots;
            _filteredSlots.Clear();
            _slotIndexCache.Clear();
            _visibleSlotCount = 0;

            // Phase 1: Evaluate filter and cache indices
            for (int i = 0; i < slots.Count; i++)
            {
                var slot = slots[i];
                _slotIndexCache[slot] = i;
                var ctx = new FilterContext(slot, _inventory, slots, i);

                if (EvaluateSlot(slot, in ctx))
                {
                    _filteredSlots.Add(slot);
                    _visibleSlotCount++;
                }
            }

            // Phase 2: Sort visible slots
            if (_activeSorter != null && _filteredSlots.Count > 1)
            {
                int direction = _sortAscending ? 1 : -1;
                _filteredSlots.Sort((a, b) =>
                {
                    var ctxA = new FilterContext(a, _inventory, slots, _slotIndexCache[a]);
                    var ctxB = new FilterContext(b, _inventory, slots, _slotIndexCache[b]);
                    return _activeSorter.Compare(in ctxA, in ctxB) * direction;
                });
            }

            // Phase 3: Apply visual changes
            ApplyVisualChanges(slots);
        }

        private bool EvaluateSlot(BaseSlot baseSlot, in FilterContext ctx)
        {
            if (baseSlot.IsEmpty)
                return !_hideEmptySlots && !_isFilterActive;

            if (_activeFilter == null)
                return true;

            return _activeFilter.Evaluate(in ctx);
        }

        private void ApplyVisualChanges(IReadOnlyList<BaseSlot> allSlots)
        {
            var filteredSet = new HashSet<BaseSlot>(_filteredSlots);

            switch (_filterDisplayMode)
            {
                case FilterDisplayMode.Hide:
                    ApplyHideMode(allSlots, filteredSet);
                    break;
                case FilterDisplayMode.Dim:
                    ApplyDimMode(allSlots, filteredSet);
                    break;
                case FilterDisplayMode.MoveToEnd:
                    ApplyMoveToEndMode(allSlots, filteredSet);
                    break;
            }
        }

        private void ApplyHideMode(IReadOnlyList<BaseSlot> allSlots, HashSet<BaseSlot> visibleSlots)
        {
            int siblingIndex = 0;

            foreach (var slot in _filteredSlots)
            {
                slot.Transform.gameObject.SetActive(true);
                slot.SetInteractable(true);
                slot.Transform.SetSiblingIndex(siblingIndex++);
            }

            foreach (var slot in allSlots)
            {
                if (!visibleSlots.Contains(slot))
                {
                    slot.Transform.gameObject.SetActive(false);
                    slot.SetInteractable(false);
                }
            }
        }

        private void ApplyDimMode(IReadOnlyList<BaseSlot> allSlots, HashSet<BaseSlot> visibleSlots)
        {
            int siblingIndex = 0;

            foreach (var slot in _filteredSlots)
            {
                slot.Transform.gameObject.SetActive(true);
                slot.SetInteractable(true);
                slot.Transform.SetSiblingIndex(siblingIndex++);
            }

            foreach (var slot in allSlots)
            {
                if (!visibleSlots.Contains(slot))
                {
                    slot.Transform.gameObject.SetActive(true);
                    slot.SetInteractable(false);
                    slot.Transform.SetSiblingIndex(siblingIndex++);
                }
            }
        }

        private void ApplyMoveToEndMode(IReadOnlyList<BaseSlot> allSlots, HashSet<BaseSlot> visibleSlots)
        {
            int siblingIndex = 0;

            foreach (var slot in _filteredSlots)
            {
                slot.Transform.gameObject.SetActive(true);
                slot.SetInteractable(true);
                slot.Transform.SetSiblingIndex(siblingIndex++);
            }

            foreach (var slot in allSlots)
            {
                if (!visibleSlots.Contains(slot))
                {
                    slot.Transform.gameObject.SetActive(true);
                    slot.SetInteractable(false);
                    slot.Transform.SetSiblingIndex(siblingIndex++);
                }
            }
        }

#if UNITY_EDITOR
        [Button("Test: Clear All"), FoldoutGroup("Debug Actions")]
        private void TestClearAll()
        {
            ClearAll();
        }
#endif
    }
}
