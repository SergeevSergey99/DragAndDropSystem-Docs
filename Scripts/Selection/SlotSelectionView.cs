using System;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;
using UDND.Core;
using UDND.Slots;

namespace UDND.Selection
{
    /// <summary>
    /// View component: subscribes to SelectionManager and updates slot UI
    /// depending on whether this slot is selected.
    ///
    /// It does not know how the selection happened and only reacts to the state.
    /// Add it to the same GameObject as UniversalSlot.
    /// </summary>
    public class SlotSelectionView : MonoBehaviour
    {
        [SerializeField] private BaseSlot baseSlot;

        [Header("Visuals")]
        [SerializeField] private GameObject _selectionHighlight;
        [SerializeField] private Graphic    _backgroundGraphic;
        [SerializeField] private Color      _selectedColor = new Color(1f, 0.85f, 0.1f, 1f);
        [SerializeField] private Color      _defaultColor  = Color.white;

        /// <summary>
        /// Current selection state of this slot
        /// </summary>
        public bool IsSelected { get; private set; }

        /// <summary>
        /// Called when the state changes: true = selected, false = deselected
        /// </summary>
        public event Action<bool> OnSelectionStateChanged;

        private void Awake()
        {
            if (baseSlot == null)
                baseSlot = GetComponent<BaseSlot>();
        }

        private void OnEnable()
        {
            UDNDEvents.OnSelectionChanged += Refresh;
            // Sync immediately because the component may have been enabled while selection was already active
            if (SelectionManager.IsInstanceExist)
                Refresh(SelectionManager.AutoCreateInstance.CurrentContext);
        }

        private void OnDisable()
        {
            UDNDEvents.OnSelectionChanged -= Refresh;
        }

        private void Refresh(SelectionContext context)
        {
            bool selected = context.Contains(baseSlot);
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