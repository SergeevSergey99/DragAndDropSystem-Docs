using System;
using System.Collections.Generic;
using DragAndDropSystem.Core;
using DragAndDropSystem.Tools.Inspector;
using DragAndDropSystem.Inventories;
using DragAndDropSystem.Slots;
using UnityEngine;

namespace DragAndDropSystem.Filter
{
    /// <summary>
    /// Inventory filtering and sorting controller.
    /// Manages slot visibility and order without modifying inventory data.
    /// </summary>
    public class FilterSortController : MonoBehaviour
    {
        [SerializeField, Required]
        private UniversalInventory _inventory;

        [Header("Filter Settings")]
        [SerializeField, Tooltip("Hide filtered slots (SetActive) or only dim them")]
        private FilterDisplayMode _filterDisplayMode = FilterDisplayMode.Dim;

        [SerializeField, Tooltip("Hide empty slots during filtering")]
        private bool _hideEmptySlots = false;

        [Header("Sort Settings")]
        [SerializeField, Tooltip("Current sort mode")]
        private SortMode _sortMode = SortMode.None;

        [SerializeField, Tooltip("Sort direction")]
        private bool _sortAscending = true;

        [Header("Debug")]
        [ShowInInspector, ReadOnly]
        private int _visibleSlotCount;

        [ShowInInspector, ReadOnly]
        private bool _isFilterActive;

        private Predicate<IItemAdapter> _currentFilter;
        private Comparison<BaseSlot> _currentSort;
        private List<BaseSlot> _filteredSlots = new List<BaseSlot>();
        private FilterPreset _activeFilterPreset;
        private SortPreset _activeSortPreset;

        public UniversalInventory Inventory => _inventory;
        public bool IsFilterActive => _isFilterActive;
        public int VisibleSlotCount => _visibleSlotCount;
        public FilterDisplayMode DisplayMode => _filterDisplayMode;
        public SortMode CurrentSortMode => _sortMode;
        public bool SortAscending => _sortAscending;
        public FilterPreset ActiveFilterPreset => _activeFilterPreset;
        public SortPreset ActiveSortPreset => _activeSortPreset;

        /// <summary>
        /// Event raised when filtering or sorting changes
        /// </summary>
        public event Action OnFilterChanged;

        public enum FilterDisplayMode
        {
            /// <summary>
            /// Hide filtered slots (SetActive(false))
            /// </summary>
            Hide,

            /// <summary>
            /// Dim filtered slots but keep them visible
            /// </summary>
            Dim,

            /// <summary>
            /// Move filtered slots to the end
            /// </summary>
            MoveToEnd
        }

        public enum SortMode
        {
            None,
            ByName,
            ByCategory,
            ByRarity,
            BySortValue,
            Custom
        }

        private void Awake()
        {
            if (_inventory == null)
            {
                _inventory = GetComponent<UniversalInventory>();
            }
        }

        private void OnEnable()
        {
            if (_inventory != null)
            {
                _inventory.OnItemAdded += OnInventoryChanged;
                _inventory.OnItemRemoved += OnInventoryChanged;
            }
        }

        private void OnDisable()
        {
            if (_inventory != null)
            {
                _inventory.OnItemAdded -= OnInventoryChanged;
                _inventory.OnItemRemoved -= OnInventoryChanged;
            }
        }

        private void OnInventoryChanged(InventoryItemEventContext context)
        {
            // Reapply the filter when the inventory changes
            ApplyFilterAndSort();
        }

        /// <summary>
        /// Set a filter using a predicate
        /// </summary>
        public void SetFilter(Predicate<IItemAdapter> filter)
        {
            _activeFilterPreset = null;
            _currentFilter = filter;
            _isFilterActive = filter != null;
            ApplyFilterAndSort();
        }

        /// <summary>
        /// Apply a filter from a preset and remember it as the active filter source.
        /// </summary>
        public void ApplyFilterPreset(FilterPreset preset)
        {
            _activeFilterPreset = preset;

            if (preset == null || preset.Type == FilterPreset.FilterType.None)
            {
                _currentFilter = null;
                _isFilterActive = false;
                ApplyFilterAndSort();
                return;
            }

            switch (preset.Type)
            {
                case FilterPreset.FilterType.Category:
                    if (string.IsNullOrEmpty(preset.Category))
                    {
                        _currentFilter = null;
                        _isFilterActive = false;
                    }
                    else
                    {
                        _currentFilter = item =>
                        {
                            if (item is IFilterable filterable)
                                return string.Equals(filterable.Category, preset.Category, StringComparison.OrdinalIgnoreCase);
                            return false;
                        };
                        _isFilterActive = true;
                    }
                    break;

                case FilterPreset.FilterType.Rarity:
                    _currentFilter = item =>
                    {
                        if (item is IFilterable filterable)
                            return filterable.Rarity >= preset.MinRarity && filterable.Rarity <= preset.MaxRarity;
                        return false;
                    };
                    _isFilterActive = true;
                    break;

                case FilterPreset.FilterType.Name:
                    if (string.IsNullOrEmpty(preset.SearchText))
                    {
                        _currentFilter = null;
                        _isFilterActive = false;
                    }
                    else
                    {
                        _currentFilter = item =>
                            item.DisplayName != null &&
                            item.DisplayName.IndexOf(preset.SearchText, StringComparison.OrdinalIgnoreCase) >= 0;
                        _isFilterActive = true;
                    }
                    break;

                default:
                    _currentFilter = null;
                    _isFilterActive = false;
                    break;
            }

            ApplyFilterAndSort();
        }

        /// <summary>
        /// Set a category filter (for IFilterable items)
        /// </summary>
        public void SetCategoryFilter(string category)
        {
            if (string.IsNullOrEmpty(category))
            {
                ClearFilter();
                return;
            }

            SetFilter(item =>
            {
                if (item is IFilterable filterable)
                {
                    return string.Equals(filterable.Category, category, StringComparison.OrdinalIgnoreCase);
                }
                return false;
            });
        }

        /// <summary>
        /// Set a rarity filter (for IFilterable items)
        /// </summary>
        public void SetRarityFilter(int minRarity, int maxRarity = int.MaxValue)
        {
            SetFilter(item =>
            {
                if (item is IFilterable filterable)
                {
                    return filterable.Rarity >= minRarity && filterable.Rarity <= maxRarity;
                }
                return false;
            });
        }

        /// <summary>
        /// Set a text filter by name
        /// </summary>
        public void SetNameFilter(string searchText)
        {
            if (string.IsNullOrEmpty(searchText))
            {
                ClearFilter();
                return;
            }

            SetFilter(item =>
                item.DisplayName != null &&
                item.DisplayName.IndexOf(searchText, StringComparison.OrdinalIgnoreCase) >= 0);
        }

        /// <summary>
        /// Clear the filter
        /// </summary>
        public void ClearFilter()
        {
            _activeFilterPreset = null;
            _currentFilter = null;
            _isFilterActive = false;
            ApplyFilterAndSort();
        }

        /// <summary>
        /// Set the sort mode
        /// </summary>
        public void SetSortMode(SortMode mode, bool ascending = true)
        {
            _activeSortPreset = null;
            _sortMode = mode;
            _sortAscending = ascending;
            _currentSort = CreateSortComparison(mode, ascending);
            ApplyFilterAndSort();
        }

        /// <summary>
        /// Apply sorting from a preset and remember it as the active sort source.
        /// </summary>
        public void ApplySortPreset(SortPreset preset, bool ascending)
        {
            _activeSortPreset = preset;

            if (preset == null || preset.Mode == SortPreset.SortMode.None)
            {
                _sortMode = SortMode.None;
                _sortAscending = ascending;
                _currentSort = null;
                ApplyFilterAndSort();
                return;
            }

            _sortMode = ConvertPresetSortMode(preset.Mode);
            _sortAscending = ascending;
            _currentSort = CreateSortComparison(_sortMode, ascending);
            ApplyFilterAndSort();
        }

        /// <summary>
        /// Set custom sorting
        /// </summary>
        public void SetCustomSort(Comparison<BaseSlot> comparison)
        {
            _sortMode = SortMode.Custom;
            _currentSort = comparison;
            ApplyFilterAndSort();
        }

        /// <summary>
        /// Clear sorting (restore the original order)
        /// </summary>
        public void ClearSort()
        {
            _activeSortPreset = null;
            _sortMode = SortMode.None;
            _currentSort = null;
            ApplyFilterAndSort();
        }

        /// <summary>
        /// Clear all filtering and sorting
        /// </summary>
        public void ClearAll()
        {
            _activeFilterPreset = null;
            _activeSortPreset = null;
            _currentFilter = null;
            _isFilterActive = false;
            _sortMode = SortMode.None;
            _currentSort = null;
            ApplyFilterAndSort();
        }

        /// <summary>
        /// Apply the current filter and sorting
        /// </summary>
        [Button("Apply Filter & Sort")]
        public void ApplyFilterAndSort()
        {
            if (_inventory == null)
                return;

            var slots = _inventory.Slots;
            _filteredSlots.Clear();
            _visibleSlotCount = 0;

            // Phase 1: Determine visibility for each slot
            foreach (var slot in slots)
            {
                bool passesFilter = EvaluateSlot(slot);

                if (passesFilter)
                {
                    _filteredSlots.Add(slot);
                    _visibleSlotCount++;
                }
            }

            // Phase 2: Sort visible slots
            if (_currentSort != null && _filteredSlots.Count > 1)
            {
                _filteredSlots.Sort(_currentSort);
            }

            // Phase 3: Apply visual changes
            ApplyVisualChanges(slots);

            OnFilterChanged?.Invoke();
        }

        private bool EvaluateSlot(BaseSlot baseSlot)
        {
            // Empty slots
            if (baseSlot.IsEmpty)
            {
                return !_hideEmptySlots && !_isFilterActive;
            }

            // If the filter is not active, all items are visible
            if (_currentFilter == null)
            {
                return true;
            }

            // Apply the filter to the item
            return _currentFilter(baseSlot.Stack.PrimaryAdapter);
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

            // First show and order visible slots
            foreach (var slot in _filteredSlots)
            {
                slot.Transform.gameObject.SetActive(true);
                slot.SetInteractable(true);
                slot.Transform.SetSiblingIndex(siblingIndex++);
            }

            // Hide the rest
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

            // Visible slots first
            foreach (var slot in _filteredSlots)
            {
                slot.Transform.gameObject.SetActive(true);
                slot.SetInteractable(true);
                slot.Transform.SetSiblingIndex(siblingIndex++);
            }

            // Then invisible (dimmed) ones in original order
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

            // Visible slots at the beginning (sorted)
            foreach (var slot in _filteredSlots)
            {
                slot.Transform.gameObject.SetActive(true);
                slot.SetInteractable(true);
                slot.Transform.SetSiblingIndex(siblingIndex++);
            }

            // Invisible ones at the end
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

        private Comparison<BaseSlot> CreateSortComparison(SortMode mode, bool ascending)
        {
            int direction = ascending ? 1 : -1;

            switch (mode)
            {
                case SortMode.ByName:
                    return (a, b) =>
                    {
                        var nameA = a.IsEmpty ? "" : a.Stack.DisplayName ?? "";
                        var nameB = b.IsEmpty ? "" : b.Stack.DisplayName ?? "";
                        return string.Compare(nameA, nameB, StringComparison.OrdinalIgnoreCase) * direction;
                    };

                case SortMode.ByCategory:
                    return (a, b) =>
                    {
                        var catA = GetCategory(a);
                        var catB = GetCategory(b);
                        return string.Compare(catA, catB, StringComparison.OrdinalIgnoreCase) * direction;
                    };

                case SortMode.ByRarity:
                    return (a, b) =>
                    {
                        var rarityA = GetRarity(a);
                        var rarityB = GetRarity(b);
                        return rarityA.CompareTo(rarityB) * direction;
                    };

                case SortMode.BySortValue:
                    return (a, b) =>
                    {
                        var valueA = GetSortValue(a);
                        var valueB = GetSortValue(b);
                        return valueA.CompareTo(valueB) * direction;
                    };

                default:
                    return null;
            }
        }

        private static SortMode ConvertPresetSortMode(SortPreset.SortMode mode)
        {
            switch (mode)
            {
                case SortPreset.SortMode.ByName:
                    return SortMode.ByName;
                case SortPreset.SortMode.ByCategory:
                    return SortMode.ByCategory;
                case SortPreset.SortMode.ByRarity:
                    return SortMode.ByRarity;
                case SortPreset.SortMode.BySortValue:
                    return SortMode.BySortValue;
                default:
                    return SortMode.None;
            }
        }

        private string GetCategory(BaseSlot baseSlot)
        {
            if (baseSlot.IsEmpty)
                return "";

            if (baseSlot.Stack.PrimaryAdapter is IFilterable filterable)
                return filterable.Category ?? "";

            return "";
        }

        private int GetRarity(BaseSlot baseSlot)
        {
            if (baseSlot.IsEmpty)
                return -1;

            if (baseSlot.Stack.PrimaryAdapter is IFilterable filterable)
                return filterable.Rarity;

            return 0;
        }

        private int GetSortValue(BaseSlot baseSlot)
        {
            if (baseSlot.IsEmpty)
                return int.MinValue;

            if (baseSlot.Stack.PrimaryAdapter is ISortable sortable)
                return sortable.SortValue;

            return 0;
        }

        /// <summary>
        /// Get the list of visible (filter-passing) slots
        /// </summary>
        public IReadOnlyList<BaseSlot> GetVisibleSlots()
        {
            return _filteredSlots.AsReadOnly();
        }

        /// <summary>
        /// Check whether a slot is visible (passes the filter)
        /// </summary>
        public bool IsSlotVisible(BaseSlot baseSlot)
        {
            return _filteredSlots.Contains(baseSlot);
        }

#if UNITY_EDITOR
        [Button("Test: Filter Weapons"), FoldoutGroup("Debug Actions")]
        private void TestFilterWeapons()
        {
            SetCategoryFilter("Weapon");
        }

        [Button("Test: Sort by Name"), FoldoutGroup("Debug Actions")]
        private void TestSortByName()
        {
            SetSortMode(SortMode.ByName);
        }

        [Button("Test: Clear All"), FoldoutGroup("Debug Actions")]
        private void TestClearAll()
        {
            ClearAll();
        }
#endif
    }
}
