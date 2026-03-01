using System;
using System.Collections.Generic;
using DragAndDropSystem.Slots;
using UnityEngine;
using UnityEngine.EventSystems;

namespace DragAndDropSystem.Selection
{
    /// <summary>
    /// Триггер выделения через клик на слоте с поддержкой модификаторов (Ctrl, Shift, Alt).
    /// Добавьте на тот же GameObject что и UniversalSlot.
    ///
    /// Типовая настройка:
    ///   None  → ClearAndSelectOperation  (обычный клик)
    ///   Ctrl  → ToggleSlotOperation       (добавить/убрать из выделения)
    ///   Shift → RangeSelectOperation      (диапазон от последнего)
    /// </summary>
    public class SlotPointerSelectionTrigger : MonoBehaviour, IPointerClickHandler
    {
        [SerializeField] private UniversalSlot _slot;
        [SerializeField] private bool _logWarnings;

        [SerializeField]
        private List<ModifierBinding> _bindings = new List<ModifierBinding>();

        private void Awake()
        {
            if (_slot == null)
                _slot = GetComponent<UniversalSlot>();
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (eventData.button != PointerEventData.InputButton.Left) return;
            if (SelectionManager.IsInstanceExist == false) return;

            foreach (var binding in _bindings)
            {
                if (!binding.IsValid()) continue;
                if (!binding.ModifierMatches()) continue;

                if (binding.Operation.CanExecute(SelectionManager.Instance, _slot))
                    binding.Operation.Execute(SelectionManager.Instance, _slot);
                else if (_logWarnings)
                    Debug.LogWarning($"[{name}] Operation '{binding.Operation.DisplayName}' cannot execute.");

                return; // Выполняем только первый подходящий binding
            }
        }

        [Serializable]
        public class ModifierBinding
        {
            [Tooltip("Какой модификатор должен быть зажат. None = без модификаторов.")]
            public ModifierKey Modifier = ModifierKey.None;

            [SerializeReference]
            public SelectionOperationBase Operation;

            public bool IsValid() => Operation != null;

            public bool ModifierMatches()
            {
                bool ctrl  = Input.GetKey(KeyCode.LeftControl)  || Input.GetKey(KeyCode.RightControl);
                bool shift = Input.GetKey(KeyCode.LeftShift)    || Input.GetKey(KeyCode.RightShift);
                bool alt   = Input.GetKey(KeyCode.LeftAlt)      || Input.GetKey(KeyCode.RightAlt);

                switch (Modifier)
                {
                    case ModifierKey.None:  return !ctrl && !shift && !alt;
                    case ModifierKey.Ctrl:  return ctrl  && !shift && !alt;
                    case ModifierKey.Shift: return shift && !ctrl  && !alt;
                    case ModifierKey.Alt:   return alt   && !ctrl  && !shift;
                    default:                return false;
                }
            }
        }

        public enum ModifierKey { None, Ctrl, Shift, Alt }
    }
}
