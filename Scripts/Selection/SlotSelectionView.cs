using System;
using DragAndDropSystem.Slots;
using UnityEngine;
using UnityEngine.UI;

namespace DragAndDropSystem.Selection
{
    /// <summary>
    /// Компонент-вьюха: подписывается на SelectionManager и меняет UI слота
    /// в зависимости от того, выделен ли этот слот.
    ///
    /// Не знает ничего о том как произошло выделение — только реагирует на факт.
    /// Добавьте на тот же GameObject что и UniversalSlot.
    /// </summary>
    public class SlotSelectionView : MonoBehaviour
    {
        [SerializeField] private UniversalSlot _slot;

        [Header("Visuals")]
        [SerializeField] private GameObject _selectionHighlight;
        [SerializeField] private Graphic    _backgroundGraphic;
        [SerializeField] private Color      _selectedColor = new Color(1f, 0.85f, 0.1f, 1f);
        [SerializeField] private Color      _defaultColor  = Color.white;

        /// <summary>
        /// Текущее состояние выделения этого слота
        /// </summary>
        public bool IsSelected { get; private set; }

        /// <summary>
        /// Вызывается при смене состояния: true = выделен, false = снято
        /// </summary>
        public event Action<bool> OnSelectionStateChanged;

        private void Awake()
        {
            if (_slot == null)
                _slot = GetComponent<UniversalSlot>();
        }

        private void OnEnable()
        {
            if (!SelectionManager.IsInstanceExist) return;
            SelectionManager.Instance.OnSelectionChanged += HandleSelectionChanged;
            // Синхронизируемся сразу — компонент мог включиться пока выделение уже было активно
            Refresh(SelectionManager.Instance.CurrentContext);
        }

        private void OnDisable()
        {
            if (!SelectionManager.IsInstanceExist) return;
            SelectionManager.Instance.OnSelectionChanged -= HandleSelectionChanged;
        }

        private void HandleSelectionChanged(object sender, SelectionChangedEventArgs args)
            => Refresh(args.Context);

        private void Refresh(SelectionContext context)
        {
            bool selected = context.Contains(_slot);
            if (selected == IsSelected) return;

            IsSelected = selected;
            ApplyVisuals();
            OnSelectionStateChanged?.Invoke(IsSelected);
        }

        private void ApplyVisuals()
        {
            if (_selectionHighlight != null)
                _selectionHighlight.SetActive(IsSelected);

            if (_backgroundGraphic != null)
                _backgroundGraphic.color = IsSelected ? _selectedColor : _defaultColor;
        }
    }
}
