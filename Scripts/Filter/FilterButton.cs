using DragAndDropSystem.Inspector;
using UnityEngine;
using UnityEngine.UI;

namespace DragAndDropSystem.Filter
{
    /// <summary>
    /// Компонент для кнопки фильтра. Привязывается к Button и применяет пресет фильтра при клике.
    /// </summary>
    [RequireComponent(typeof(Button))]
    public class FilterButton : MonoBehaviour
    {
        [SerializeField, Required]
        private FilterSortController _controller;

        [SerializeField]
        private FilterPreset _filterPreset;

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
            {
                _controller.OnFilterChanged += UpdateVisualState;
            }

            UpdateVisualState();
        }

        private void OnDisable()
        {
            if (_controller != null)
            {
                _controller.OnFilterChanged -= UpdateVisualState;
            }
        }

        private void OnDestroy()
        {
            if (_button != null)
            {
                _button.onClick.RemoveListener(OnButtonClick);
            }
        }

        private void OnButtonClick()
        {
            if (_controller == null)
                return;

            if (_toggleMode && IsThisFilterActive())
            {
                _controller.ClearFilter();
            }
            else
            {
                if (_filterPreset != null)
                {
                    _filterPreset.ApplyTo(_controller);
                }
                else
                {
                    _controller.ClearFilter();
                }
            }
        }

        private void UpdateVisualState()
        {
            if (_activeIndicator != null)
            {
                _activeIndicator.SetActive(IsThisFilterActive());
            }
        }

        private bool IsThisFilterActive()
        {
            return _controller != null &&
                   _filterPreset != null &&
                   _controller.IsFilterActive &&
                   ReferenceEquals(_controller.ActiveFilterPreset, _filterPreset);
        }

        /// <summary>
        /// Программно установить контроллер
        /// </summary>
        public void SetController(FilterSortController controller)
        {
            if (_controller != null)
            {
                _controller.OnFilterChanged -= UpdateVisualState;
            }

            _controller = controller;

            if (_controller != null && enabled)
            {
                _controller.OnFilterChanged += UpdateVisualState;
            }

            UpdateVisualState();
        }

        /// <summary>
        /// Программно установить пресет
        /// </summary>
        public void SetPreset(FilterPreset preset)
        {
            _filterPreset = preset;
            UpdateVisualState();
        }
    }
}
