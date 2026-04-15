using DragAndDropSystem.Tools.Inspector;
using UnityEngine;
using UnityEngine.UI;

namespace DragAndDropSystem.Filter
{
    /// <summary>
    /// Filter button. Applies a <see cref="SlotFilterSO"/> on click.
    /// </summary>
    [RequireComponent(typeof(Button))]
    public class FilterButton : MonoBehaviour
    {
        [SerializeField, Required]
        private FilterSortController _controller;

        [SerializeField, Tooltip("Filter asset to apply on click")]
        private SlotFilterSO _filterPreset;

        [SerializeField, Tooltip("If true, repeated click resets the filter")]
        private bool _toggleMode = true;

        [SerializeField, Tooltip("Visual highlight of the active filter")]
        private GameObject _activeIndicator;

        private Button _button;

        private void Awake()
        {
            _button = GetComponent<Button>();
            _button.onClick.AddListener(OnButtonClick);
        }

        private void OnEnable()
        {
            if (_controller != null)
                _controller.OnFilterChanged += UpdateVisualState;
            UpdateVisualState();
        }

        private void OnDisable()
        {
            if (_controller != null)
                _controller.OnFilterChanged -= UpdateVisualState;
        }

        private void OnDestroy()
        {
            if (_button != null)
                _button.onClick.RemoveListener(OnButtonClick);
        }

        private void OnButtonClick()
        {
            if (_controller == null) return;

            if (_toggleMode && IsThisFilterActive())
                _controller.ClearFilter();
            else if (_filterPreset != null)
                _controller.SetFilter(_filterPreset);
            else
                _controller.ClearFilter();
        }

        private void UpdateVisualState()
        {
            if (_activeIndicator != null)
                _activeIndicator.SetActive(IsThisFilterActive());
        }

        private bool IsThisFilterActive()
        {
            return _controller != null
                   && _filterPreset != null
                   && _controller.IsFilterActive
                   && ReferenceEquals(_controller.ActiveFilter, _filterPreset);
        }

        public void SetController(FilterSortController controller)
        {
            if (_controller != null)
                _controller.OnFilterChanged -= UpdateVisualState;

            _controller = controller;

            if (_controller != null && enabled)
                _controller.OnFilterChanged += UpdateVisualState;

            UpdateVisualState();
        }

        public void SetPreset(SlotFilterSO preset)
        {
            _filterPreset = preset;
            UpdateVisualState();
        }
    }
}
