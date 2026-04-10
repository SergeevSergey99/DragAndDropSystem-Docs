using System;
using DragAndDropSystem.Core;
using DragAndDropSystem.Tools.Inspector;
using UnityEngine;

namespace DragAndDropSystem.Filter
{
    /// <summary>
    /// ScriptableObject filter preset for convenient Inspector configuration.
    /// Can be assigned to UI buttons for quick filter switching.
    /// </summary>
    [CreateAssetMenu(fileName = "FilterPreset", menuName = "DragAndDrop/Filter/Filter Preset", order = 100)]
    public class FilterPreset : ScriptableObject
    {
        [Header("Filter Type")]
        [SerializeField]
        private FilterType _filterType = FilterType.None;

        [Header("Category Filter")]
        [SerializeField, ShowIf(nameof(_filterType), nameof(FilterType.Category))]
        private string _category;

        [Header("Rarity Filter")]
        [SerializeField, ShowIf(nameof(_filterType), nameof(FilterType.Rarity))]
        private int _minRarity = 0;

        [SerializeField, ShowIf(nameof(_filterType), nameof(FilterType.Rarity))]
        private int _maxRarity = 100;

        [Header("Name Filter")]
        [SerializeField, ShowIf(nameof(_filterType), nameof(FilterType.Name))]
        private string _searchText;

        public FilterType Type => _filterType;
        public string Category => _category;
        public int MinRarity => _minRarity;
        public int MaxRarity => _maxRarity;
        public string SearchText => _searchText;

        public enum FilterType
        {
            None,
            Category,
            Rarity,
            Name
        }

        /// <summary>
        /// Apply the preset to the controller (filter only)
        /// </summary>
        public void ApplyTo(FilterSortController controller)
        {
            if (controller == null)
                return;

            controller.ApplyFilterPreset(this);
        }

        /// <summary>
        /// Create a filter predicate from the preset
        /// </summary>
        public Predicate<IItemAdapter> CreateFilter()
        {
            switch (_filterType)
            {
                case FilterType.Category:
                    return item =>
                    {
                        if (item is IFilterable filterable)
                            return string.Equals(filterable.Category, _category, StringComparison.OrdinalIgnoreCase);
                        return false;
                    };

                case FilterType.Rarity:
                    return item =>
                    {
                        if (item is IFilterable filterable)
                            return filterable.Rarity >= _minRarity && filterable.Rarity <= _maxRarity;
                        return false;
                    };

                case FilterType.Name:
                    return item =>
                        item.DisplayName != null &&
                        item.DisplayName.IndexOf(_searchText, StringComparison.OrdinalIgnoreCase) >= 0;

                default:
                    return null;
            }
        }
    }
}
