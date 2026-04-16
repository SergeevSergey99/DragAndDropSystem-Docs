using UnityEngine;
using UnityEngine.UI;
using UniversalDragAndDrop.Tools.Inspector;

namespace UniversalDragAndDrop.Filter
{
    /// <summary>
    /// Universal filter/sort button. Applies a <see cref="FilterSortPreset"/> on click.
    /// Stateless — all visual state is read from <see cref="FilterSortController"/>.
    /// </summary>
    [RequireComponent(typeof(Button))]
    public class FilterSortButton : MonoBehaviour
    {
        [SerializeField, Required]
        private FilterSortController _controller;

        [SerializeField]
        private FilterSortPreset _preset;

        [SerializeField, Tooltip("Repeated click clears filter/sort")]
        private bool _toggleMode = true;

        [SerializeField, Tooltip("Repeated click reverses sort direction")]
        private bool _toggleDirection;

        [Header("Visuals")]
        [SerializeField, Tooltip("Shown when this preset is active")]
        private GameObject _activeIndicator;

        [SerializeField, Tooltip("Shown when sort is ascending")]
        private GameObject _ascendingIndicator;

        [SerializeField, Tooltip("Shown when sort is descending")]
        private GameObject _descendingIndicator;

        private Button _button;

        private void Awake()
        {
            _button = GetComponent<Button>();
            _button.onClick.AddListener(OnButtonClick);
        }

        private void OnEnable()
        {
            Subscribe();
            UpdateVisualState();
        }

        private void OnDisable()
        {
            Unsubscribe();
        }

        private void OnDestroy()
        {
            if (_button != null)
                _button.onClick.RemoveListener(OnButtonClick);
        }

        private void OnButtonClick()
        {
            if (_controller == null || _preset == null)
                return;

            if (IsThisPresetActive())
            {
                if (_toggleDirection && _preset.Sorter != null)
                {
                    _controller.SetSorter(_preset.Sorter, !_controller.SortAscending);
                }
                else if (_toggleMode)
                {
                    _controller.ClearAll();
                }
            }
            else
            {
                _controller.ApplyPreset(_preset);
            }
        }

        private void UpdateVisualState()
        {
            bool isActive = IsThisPresetActive();

            if (_activeIndicator != null)
                _activeIndicator.SetActive(isActive);

            if (_ascendingIndicator != null)
                _ascendingIndicator.SetActive(isActive && _controller != null && _controller.SortAscending);

            if (_descendingIndicator != null)
                _descendingIndicator.SetActive(isActive && _controller != null && !_controller.SortAscending);
        }

        private bool IsThisPresetActive()
        {
            return _controller != null
                   && _preset != null
                   && ReferenceEquals(_controller.ActivePreset, _preset);
        }

        private void OnFilterOrSortChanged()
        {
            UpdateVisualState();
        }

        private void Subscribe()
        {
            if (_controller == null) return;
            _controller.OnFilterChanged += OnFilterOrSortChanged;
            _controller.OnSortChanged += OnFilterOrSortChanged;
        }

        private void Unsubscribe()
        {
            if (_controller == null) return;
            _controller.OnFilterChanged -= OnFilterOrSortChanged;
            _controller.OnSortChanged -= OnFilterOrSortChanged;
        }

        /// <summary>Set the controller programmatically.</summary>
        public void SetController(FilterSortController controller)
        {
            Unsubscribe();
            _controller = controller;
            if (enabled) Subscribe();
            UpdateVisualState();
        }

        /// <summary>Set the preset programmatically.</summary>
        public void SetPreset(FilterSortPreset preset)
        {
            _preset = preset;
            UpdateVisualState();
        }
    }
}
