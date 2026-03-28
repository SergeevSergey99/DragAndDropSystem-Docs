using System;
using DragAndDropSystem.Inspector;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace DragAndDropSystem.Interaction
{
    [Serializable]
    public class PointerBinding
    {
        [SerializeField] private string _label;
        [SerializeField] private PointerEventData.InputButton _button = PointerEventData.InputButton.Left;
        [SerializeField] private ModifierKey _modifier = ModifierKey.None;
        [SerializeField] private PointerTriggerPhase _triggerPhase = PointerTriggerPhase.Any;
        [SerializeReference, ManagedReferencePicker] private SlotInteractionAction _action;

        public PointerBinding()
        {
        }

        public PointerBinding(
            string label,
            PointerEventData.InputButton button,
            ModifierKey modifier,
            PointerTriggerPhase triggerPhase,
            SlotInteractionAction action)
        {
            _label = label;
            _button = button;
            _modifier = modifier;
            _triggerPhase = triggerPhase;
            _action = action;
        }

        public SlotInteractionAction Action => _action;
        public string Label => string.IsNullOrEmpty(_label)
            ? (_action != null ? _action.DisplayName : "Pointer Binding")
            : _label;

        public bool IsValid() => _action != null;

        public bool Matches(PointerEventData eventData, PointerTriggerPhase eventPhase)
        {
            if (eventData == null || eventData.button != _button)
                return false;

            if (!PhaseMatches(eventPhase))
                return false;

            bool ctrl = IsCtrlPressed();
            bool shift = IsShiftPressed();
            bool alt = IsAltPressed();

            switch (_modifier)
            {
                case ModifierKey.None:
                    return !ctrl && !shift && !alt;
                case ModifierKey.Ctrl:
                    return ctrl && !shift && !alt;
                case ModifierKey.Shift:
                    return shift && !ctrl && !alt;
                case ModifierKey.Alt:
                    return alt && !ctrl && !shift;
                default:
                    return false;
            }
        }

        private bool PhaseMatches(PointerTriggerPhase eventPhase)
        {
            if (_triggerPhase == PointerTriggerPhase.Any)
                return eventPhase != PointerTriggerPhase.BeginDrag;

            return _triggerPhase == eventPhase;
        }

        private static bool IsCtrlPressed()
        {
            var keyboard = Keyboard.current;
            return keyboard != null
                   && (keyboard.leftCtrlKey.isPressed || keyboard.rightCtrlKey.isPressed);
        }

        private static bool IsShiftPressed()
        {
            var keyboard = Keyboard.current;
            return keyboard != null
                   && (keyboard.leftShiftKey.isPressed || keyboard.rightShiftKey.isPressed);
        }

        private static bool IsAltPressed()
        {
            var keyboard = Keyboard.current;
            return keyboard != null
                   && (keyboard.leftAltKey.isPressed || keyboard.rightAltKey.isPressed);
        }
    }
}
