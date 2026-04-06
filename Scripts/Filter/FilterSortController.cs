using System;
using System.Collections.Generic;
using DragAndDropSystem.Core;
using DragAndDropSystem.Inspector;
using DragAndDropSystem.Inventories;
using DragAndDropSystem.Slots;
using UnityEngine;

namespace DragAndDropSystem.Filter
{
    /// <summary>
    /// Контроллер фильтрации и сортировки инвентаря.
    /// Управляет видимостью и порядком слотов, не изменяя данные инвентаря.
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
        private Comparison<ISlot> _currentSort;
        private List<ISlot> _filteredSlots = new List<ISlot>();
        private FilterPreset _activeFilterPreset;

        public UniversalInventory Inventory => _inventory;
        public bool IsFilterActive => _isFilterActive;
        public int VisibleSlotCount => _visibleSlotCount;
        public FilterDisplayMode DisplayMode => _filterDisplayMode;
        public SortMode CurrentSortMode => _sortMode;
        public bool SortAscending => _sortAscending;
        public FilterPreset ActiveFilterPreset => _activeFilterPreset;

        /// <summary>
        /// Событие изменения фильтра/сортировки
        /// </summary>
        public event Action OnFilterChanged;

        public enum FilterDisplayMode
        {
            /// <summary>
            /// Скрывать отфильтрованные слоты (SetActive(false))
            /// </summary>
            Hide,

            /// <summary>
            /// Затемнять отфильтрованные слоты, но оставлять видимыми
            /// </summary>
            Dim,

            /// <summary>
            /// Перемещать отфильтрованные слоты в конец
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
            // Переприменяем фильтр при изменении инвентаря
            ApplyFilterAndSort();
        }

        /// <summary>
        /// Установить фильтр по предикату
        /// </summary>
        public void SetFilter(Predicate<IItemAdapter> filter)
        {
            _activeFilterPreset = null;
            _currentFilter = filter;
            _isFilterActive = filter != null;
            ApplyFilterAndSort();
        }

        /// <summary>
        /// Применить фильтр из пресета и запомнить его как активный источник фильтра.
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
        /// Установить фильтр по категории (для IFilterable предметов)
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
        /// Установить фильтр по редкости (для IFilterable предметов)
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
        /// Установить текстовый фильтр по имени
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
        /// Очистить фильтр
        /// </summary>
        public void ClearFilter()
        {
            _activeFilterPreset = null;
            _currentFilter = null;
            _isFilterActive = false;
            ApplyFilterAndSort();
        }

        /// <summary>
        /// Установить режим сортировки
        /// </summary>
        public void SetSortMode(SortMode mode, bool ascending = true)
        {
            _sortMode = mode;
            _sortAscending = ascending;
            _currentSort = CreateSortComparison(mode, ascending);
            ApplyFilterAndSort();
        }

        /// <summary>
        /// Установить кастомную сортировку
        /// </summary>
        public void SetCustomSort(Comparison<ISlot> comparison)
        {
            _sortMode = SortMode.Custom;
            _currentSort = comparison;
            ApplyFilterAndSort();
        }

        /// <summary>
        /// Очистить сортировку (вернуть оригинальный порядок)
        /// </summary>
        public void ClearSort()
        {
            _sortMode = SortMode.None;
            _currentSort = null;
            ApplyFilterAndSort();
        }

        /// <summary>
        /// Очистить все фильтры и сортировку
        /// </summary>
        public void ClearAll()
        {
            _activeFilterPreset = null;
            _currentFilter = null;
            _isFilterActive = false;
            _sortMode = SortMode.None;
            _currentSort = null;
            ApplyFilterAndSort();
        }

        /// <summary>
        /// Применить текущий фильтр и сортировку
        /// </summary>
        [Button("Apply Filter & Sort")]
        public void ApplyFilterAndSort()
        {
            if (_inventory == null)
                return;

            var slots = _inventory.Slots;
            _filteredSlots.Clear();
            _visibleSlotCount = 0;

            // Фаза 1: Определить видимость каждого слота
            foreach (var slot in slots)
            {
                bool passesFilter = EvaluateSlot(slot);

                if (passesFilter)
                {
                    _filteredSlots.Add(slot);
                    _visibleSlotCount++;
                }
            }

            // Фаза 2: Сортировка видимых слотов
            if (_currentSort != null && _filteredSlots.Count > 1)
            {
                _filteredSlots.Sort(_currentSort);
            }

            // Фаза 3: Применить визуальные изменения
            ApplyVisualChanges(slots);

            OnFilterChanged?.Invoke();
        }

        private bool EvaluateSlot(ISlot slot)
        {
            // Пустые слоты
            if (slot.IsEmpty)
            {
                return !_hideEmptySlots && !_isFilterActive;
            }

            // Если фильтр не активен - все предметы видимы
            if (_currentFilter == null)
            {
                return true;
            }

            // Применяем фильтр к предмету
            return _currentFilter(slot.Stack.PrimaryAdapter);
        }

        private void ApplyVisualChanges(IReadOnlyList<ISlot> allSlots)
        {
            var filteredSet = new HashSet<ISlot>(_filteredSlots);

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

        private void ApplyHideMode(IReadOnlyList<ISlot> allSlots, HashSet<ISlot> visibleSlots)
        {
            int siblingIndex = 0;

            // Сначала показываем и упорядочиваем видимые слоты
            foreach (var slot in _filteredSlots)
            {
                slot.Transform.gameObject.SetActive(true);
                slot.SetInteractable(true);
                slot.Transform.SetSiblingIndex(siblingIndex++);
            }

            // Скрываем остальные
            foreach (var slot in allSlots)
            {
                if (!visibleSlots.Contains(slot))
                {
                    slot.Transform.gameObject.SetActive(false);
                    slot.SetInteractable(false);
                }
            }
        }

        private void ApplyDimMode(IReadOnlyList<ISlot> allSlots, HashSet<ISlot> visibleSlots)
        {
            int siblingIndex = 0;

            // Сначала видимые слоты
            foreach (var slot in _filteredSlots)
            {
                slot.Transform.gameObject.SetActive(true);
                slot.SetInteractable(true);
                slot.Transform.SetSiblingIndex(siblingIndex++);
            }

            // Затем невидимые (затемненные) в оригинальном порядке
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

        private void ApplyMoveToEndMode(IReadOnlyList<ISlot> allSlots, HashSet<ISlot> visibleSlots)
        {
            int siblingIndex = 0;

            // Видимые слоты в начале (отсортированные)
            foreach (var slot in _filteredSlots)
            {
                slot.Transform.gameObject.SetActive(true);
                slot.SetInteractable(true);
                slot.Transform.SetSiblingIndex(siblingIndex++);
            }

            // Невидимые в конце
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

        private Comparison<ISlot> CreateSortComparison(SortMode mode, bool ascending)
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

        private string GetCategory(ISlot slot)
        {
            if (slot.IsEmpty)
                return "";

            if (slot.Stack.PrimaryAdapter is IFilterable filterable)
                return filterable.Category ?? "";

            return "";
        }

        private int GetRarity(ISlot slot)
        {
            if (slot.IsEmpty)
                return -1;

            if (slot.Stack.PrimaryAdapter is IFilterable filterable)
                return filterable.Rarity;

            return 0;
        }

        private int GetSortValue(ISlot slot)
        {
            if (slot.IsEmpty)
                return int.MinValue;

            if (slot.Stack.PrimaryAdapter is ISortable sortable)
                return sortable.SortValue;

            return 0;
        }

        /// <summary>
        /// Получить список видимых (прошедших фильтр) слотов
        /// </summary>
        public IReadOnlyList<ISlot> GetVisibleSlots()
        {
            return _filteredSlots.AsReadOnly();
        }

        /// <summary>
        /// Проверить, виден ли слот (проходит фильтр)
        /// </summary>
        public bool IsSlotVisible(ISlot slot)
        {
            return _filteredSlots.Contains(slot);
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
