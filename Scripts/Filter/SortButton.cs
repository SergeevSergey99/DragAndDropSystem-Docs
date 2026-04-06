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

        [SerializeField, Tooltip("If true, repeated click resets the sort")]
        private bool _toggleMode = true;

        [SerializeField, Tooltip("If true, repeated click changes the sort direction")]
        private bool _toggleDirection = false;

        [SerializeField, Tooltip("Visual highlight of the active sort")]
        private GameObject _activeIndicator;

        [SerializeField, Tooltip("Sort direction indicator (up)")]
        private GameObject _ascendingIndicator;

        [SerializeField, Tooltip("Sort direction indicator (down)")]
        private GameObject _descendingIndicator;

        private Button _button;
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

            if (IsThisSortActive())
            {
                if (_toggleDirection)
                {
                    _currentAscending = !_currentAscending;
                    ApplySort();
                }
                else if (_toggleMode)
                {
                    _controller.ClearSort();
                }
            }
            else
            {
                ApplySort();
            }
        }

        private void ApplySort()
        {
            if (_sortPreset != null)
            {
                _controller.ApplySortPreset(_sortPreset, _currentAscending);
            }
            else
            {
                _controller.ClearSort();
            }
        }

        private void UpdateVisualState()
        {
            bool isSortActive = IsThisSortActive();

            if (_activeIndicator != null)
            {
                _activeIndicator.SetActive(isSortActive);
            }

            if (_ascendingIndicator != null)
            {
                _ascendingIndicator.SetActive(isSortActive && _controller.SortAscending);
            }

            if (_descendingIndicator != null)
            {
                _descendingIndicator.SetActive(isSortActive && !_controller.SortAscending);
            }
        }

        private bool IsThisSortActive()
        {
            return _controller != null &&
                   _sortPreset != null &&
                   _controller.CurrentSortMode != FilterSortController.SortMode.None &&
                   ReferenceEquals(_controller.ActiveSortPreset, _sortPreset);
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
        public void SetPreset(SortPreset preset)
        {
            _sortPreset = preset;
            if (preset != null)
            {
                _currentAscending = preset.Ascending;
            }

            UpdateVisualState();
        }
    }
}
