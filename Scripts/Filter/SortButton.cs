using DragAndDropSystem.Inspector;
using UnityEngine;
using UnityEngine.UI;

namespace DragAndDropSystem.Filter
{
    /// <summary>
    /// Компонент для кнопки сортировки. Привязывается к Button и применяет пресет сортировки при клике.
    /// </summary>
    [RequireComponent(typeof(Button))]
    public class SortButton : MonoBehaviour
    {
        [SerializeField, Required]
        private FilterSortController _controller;

        [SerializeField]
        private SortPreset _sortPreset;

        [SerializeField, Tooltip("Если true, повторный клик сбрасывает сортировку")]
        private bool _toggleMode = true;

        [SerializeField, Tooltip("Если true, повторный клик меняет направление сортировки")]
        private bool _toggleDirection = false;

        [SerializeField, Tooltip("Визуальное выделение активной сортировки")]
        private GameObject _activeIndicator;

        [SerializeField, Tooltip("Индикатор направления сортировки (вверх)")]
        private GameObject _ascendingIndicator;

        [SerializeField, Tooltip("Индикатор направления сортировки (вниз)")]
        private GameObject _descendingIndicator;

        private Button _button;
        private bool _isActive;
        private bool _currentAscending = true;

        private void Awake()
        {
            _button = GetComponent<Button>();
            _button.onClick.AddListener(OnButtonClick);

            if (_sortPreset != null)
            {
                _currentAscending = _sortPreset.Ascending;
            }
        }

        private void OnEnable()
        {
            if (_controller != null)
            {
                _controller.OnFilterChanged += UpdateVisualState;
            }
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

            if (_isActive)
            {
                if (_toggleDirection)
                {
                    // Переключить направление
                    _currentAscending = !_currentAscending;
                    ApplySort();
                }
                else if (_toggleMode)
                {
                    // Сбросить сортировку
                    _controller.ClearSort();
                    _isActive = false;
                }
            }
            else
            {
                // Применить сортировку
                ApplySort();
                _isActive = true;
            }

            UpdateVisualState();
        }

        private void ApplySort()
        {
            if (_sortPreset != null)
            {
                // Применяем пресет с текущим направлением
                _controller.SetSortMode(
                    ConvertPresetMode(_sortPreset.Mode),
                    _currentAscending);
            }
            else
            {
                _controller.ClearSort();
            }
        }

        private FilterSortController.SortMode ConvertPresetMode(SortPreset.SortMode mode)
        {
            switch (mode)
            {
                case SortPreset.SortMode.ByName:
                    return FilterSortController.SortMode.ByName;
                case SortPreset.SortMode.ByCategory:
                    return FilterSortController.SortMode.ByCategory;
                case SortPreset.SortMode.ByRarity:
                    return FilterSortController.SortMode.ByRarity;
                case SortPreset.SortMode.BySortValue:
                    return FilterSortController.SortMode.BySortValue;
                default:
                    return FilterSortController.SortMode.None;
            }
        }

        private void UpdateVisualState()
        {
            bool isSortActive = _controller.CurrentSortMode != FilterSortController.SortMode.None;

            if (_activeIndicator != null)
            {
                _activeIndicator.SetActive(_isActive && isSortActive);
            }

            if (_ascendingIndicator != null)
            {
                _ascendingIndicator.SetActive(_isActive && isSortActive && _currentAscending);
            }

            if (_descendingIndicator != null)
            {
                _descendingIndicator.SetActive(_isActive && isSortActive && !_currentAscending);
            }
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
        }

        /// <summary>
        /// Программно установить пресет
        /// </summary>
        public void SetPreset(SortPreset preset)
        {
            _sortPreset = preset;
            if (preset != null)
            {
                _currentAscending = preset.Ascending;
            }
        }
    }
}
