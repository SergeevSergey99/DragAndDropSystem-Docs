using UnityEngine;
using UnityEngine.UI;
using UniversalDragAndDrop.Tools.Inspector;

namespace UniversalDragAndDrop.Filter
{
    /// <summary>
    /// Sort button. Applies a <see cref="SlotSorterSO"/> on click.
    /// </summary>
    [RequireComponent(typeof(Button))]
    public class SortButton : MonoBehaviour
    {
        [SerializeField, Required]
        private FilterSortController _controller;

        [SerializeField, Tooltip("Sorter asset to apply on click")]
        private SlotSorterSO _sortPreset;

        [SerializeField]
        private bool _ascending = true;

        [SerializeField, Tooltip("If true, repeated click resets the sort")]
        private bool _toggleMode = true;

        [SerializeField, Tooltip("If true, repeated click changes the sort direction")]
        private bool _toggleDirection;

        [SerializeField, Tooltip("Visual highlight of the active sort")]
        private GameObject _activeIndicator;

        [SerializeField, Tooltip("Sort direction indicator (up)")]
        private GameObject _ascendingIndicator;

        [SerializeField, Tooltip("Sort direction indicator (down)")]
        private GameObject _descendingIndicator;

        private Button _button;

        private void Awake()
        {
            _button = GetComponent<Button>();
            _button.onClick.AddListener(OnButtonClick);
        }

        private void OnEnable()
        {
            if (_controller != null)
                _controller.OnSortChanged += UpdateVisualState;
            UpdateVisualState();
        }

        private void OnDisable()
        {
            if (_controller != null)
                _controller.OnSortChanged -= UpdateVisualState;
        }

        private void OnDestroy()
        {
            if (_button != null)
                _button.onClick.RemoveListener(OnButtonClick);
        }

        private void OnButtonClick()
        {
            if (_controller == null) return;

            if (IsThisSortActive())
            {
                if (_toggleDirection)
                {
                    _ascending = !_ascending;
                    _controller.SetSorter(_sortPreset, _ascending);
                }
                else if (_toggleMode)
                {
                    _controller.ClearSorter();
                }
            }
            else if (_sortPreset != null)
            {
                _controller.SetSorter(_sortPreset, _ascending);
            }
            else
            {
                _controller.ClearSorter();
            }
        }

        private void UpdateVisualState()
        {
            bool isActive = IsThisSortActive();

            if (_activeIndicator != null)
                _activeIndicator.SetActive(isActive);

            if (_ascendingIndicator != null)
                _ascendingIndicator.SetActive(isActive && _controller.SortAscending);

            if (_descendingIndicator != null)
                _descendingIndicator.SetActive(isActive && !_controller.SortAscending);
        }

        private bool IsThisSortActive()
        {
            return _controller != null
                   && _sortPreset != null
                   && _controller.ActiveSorter != null
                   && ReferenceEquals(_controller.ActiveSorter, _sortPreset);
        }

        public void SetController(FilterSortController controller)
        {
            if (_controller != null)
                _controller.OnSortChanged -= UpdateVisualState;

            _controller = controller;

            if (_controller != null && enabled)
                _controller.OnSortChanged += UpdateVisualState;

            UpdateVisualState();
        }

        public void SetPreset(SlotSorterSO preset)
        {
            _sortPreset = preset;
            UpdateVisualState();
        }
    }
}
