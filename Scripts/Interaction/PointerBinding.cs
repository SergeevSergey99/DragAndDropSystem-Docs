using System;
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
        [SerializeReference] private SlotInteractionAction _action;

        public PointerBinding()
        {
        }

        public PointerBinding(string label, PointerEventData.InputButton button, ModifierKey modifier, SlotInteractionAction action)
        {
            _label = label;
            _button = button;
            _modifier = modifier;
            _action = action;
        }

        public SlotInteractionAction Action => _action;
        public string Label => string.IsNullOrEmpty(_label)
            ? (_action != null ? _action.DisplayName : "Pointer Binding")
            : _label;

        public bool IsValid() => _action != null;

        public bool Matches(PointerEventData eventData)
        {
            if (eventData == null || eventData.button != _button)
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

        private static bool IsCtrlPressed()
        {
            var keyboard = Keyboard.current;
            if (keyboard != null)
                return keyboard.leftCtrlKey.isPressed || keyboard.rightCtrlKey.isPressed;

            return Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl);
        }

        private static bool IsShiftPressed()
        {
            var keyboard = Keyboard.current;
            if (keyboard != null)
                return keyboard.leftShiftKey.isPressed || keyboard.rightShiftKey.isPressed;

            return Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
        }

        private static bool IsAltPressed()
        {
            var keyboard = Keyboard.current;
            if (keyboard != null)
                return keyboard.leftAltKey.isPressed || keyboard.rightAltKey.isPressed;

            return Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt);
        }
    }
        
    public enum ModifierKey
    {
        None = 0,
        Ctrl = 1,
        Shift = 2,
        Alt = 3
    }

    public enum NavigationEventType
    {
        Submit = 0,
        Cancel = 1
    }

    public enum BindingMergeMode
    {
        LocalOnly = 0,
        ProfileOnly = 1,
        LocalThenProfile = 2
    }
}