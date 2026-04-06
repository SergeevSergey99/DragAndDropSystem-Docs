using System;
using DragAndDropSystem.Core;
using DragAndDropSystem.Slots;
using UnityEngine;

namespace DragAndDropSystem.Filter
{
    /// <summary>
    /// ScriptableObject пресет сортировки для удобной настройки в инспекторе.
    /// Можно назначать на кнопки UI для быстрого переключения сортировки.
    /// </summary>
    [CreateAssetMenu(fileName = "SortPreset", menuName = "DragAndDrop/Filter/Sort Preset", order = 101)]
    public class SortPreset : ScriptableObject
    {
        [Header("Sort Settings")]
        [SerializeField]
        private SortMode _sortMode = SortMode.None;

        [SerializeField]
        private bool _ascending = true;

        public SortMode Mode => _sortMode;
        public bool Ascending => _ascending;

        public enum SortMode
        {
            None,
            ByName,
            ByCategory,
            ByRarity,
            BySortValue
        }

        /// <summary>
        /// Применить пресет к контроллеру (только сортировку)
        /// </summary>
        public void ApplyTo(FilterSortController controller)
        {
            if (controller == null)
                return;

            controller.ApplySortPreset(this, _ascending);
        }

        /// <summary>
        /// Создать компаратор сортировки из пресета
        /// </summary>
        public Comparison<ISlot> CreateComparison()
        {
            int direction = _ascending ? 1 : -1;

            switch (_sortMode)
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

        private static FilterSortController.SortMode ConvertToControllerMode(SortMode mode)
        {
            switch (mode)
            {
                case SortMode.ByName:
                    return FilterSortController.SortMode.ByName;
                case SortMode.ByCategory:
                    return FilterSortController.SortMode.ByCategory;
                case SortMode.ByRarity:
                    return FilterSortController.SortMode.ByRarity;
                case SortMode.BySortValue:
                    return FilterSortController.SortMode.BySortValue;
                default:
                    return FilterSortController.SortMode.None;
            }
        }

        private static string GetCategory(ISlot slot)
        {
            if (slot.IsEmpty)
                return "";

            if (slot.Stack.PrimaryAdapter is IFilterable filterable)
                return filterable.Category ?? "";

            return "";
        }

        private static int GetRarity(ISlot slot)
        {
            if (slot.IsEmpty)
                return -1;

            if (slot.Stack.PrimaryAdapter is IFilterable filterable)
                return filterable.Rarity;

            return 0;
        }

        private static int GetSortValue(ISlot slot)
        {
            if (slot.IsEmpty)
                return int.MinValue;

            if (slot.Stack.PrimaryAdapter is ISortable sortable)
                return sortable.SortValue;

            return 0;
        }
    }
}
