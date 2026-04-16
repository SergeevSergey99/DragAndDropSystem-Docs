using UnityEngine;
using UniversalDragAndDrop.Tools.Inspector;

namespace UniversalDragAndDrop.Filter
{
    /// <summary>
    /// Combined filter + sort preset as a single ScriptableObject asset.
    /// Assign to <see cref="FilterSortButton"/> or apply via <see cref="FilterSortController.ApplyPreset"/>.
    /// </summary>
    [CreateAssetMenu(fileName = "FilterSortPreset", menuName = "DragAndDrop/Filter/Filter Sort Preset", order = 102)]
    public class FilterSortPreset : ScriptableObject
    {
        [Header("Filter")]
        [SerializeReference, ManagedReferencePicker, Tooltip("Filter logic (null = no filtering)")]
        private ISlotFilter _filter;

        [Header("Sort")]
        [SerializeReference, ManagedReferencePicker, Tooltip("Sort logic (null = no sorting)")]
        private ISlotSorter _sorter;

        [SerializeField]
        private bool _ascending = true;

        [Header("Display")]
        [SerializeField]
        private FilterDisplayMode _displayMode = FilterDisplayMode.Hide;

        public ISlotFilter Filter => _filter;
        public ISlotSorter Sorter => _sorter;
        public bool Ascending => _ascending;
        public FilterDisplayMode DisplayMode => _displayMode;
    }
}
