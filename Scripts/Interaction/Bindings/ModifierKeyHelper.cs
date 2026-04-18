using UnityEngine;

namespace UniversalDragAndDrop.Interaction
{
    public static class ModifierKeyHelper
    {
        public static bool IsCtrlPressed()
        {
#if UDND_INPUT_SYSTEM && ENABLE_INPUT_SYSTEM
            var keyboard = UnityEngine.InputSystem.Keyboard.current;
            return keyboard != null
                   && (keyboard.leftCtrlKey.isPressed || keyboard.rightCtrlKey.isPressed);
#else
            return Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl);
#endif
        }

        public static bool IsShiftPressed()
        {
#if UDND_INPUT_SYSTEM && ENABLE_INPUT_SYSTEM
            var keyboard = UnityEngine.InputSystem.Keyboard.current;
            return keyboard != null
                   && (keyboard.leftShiftKey.isPressed || keyboard.rightShiftKey.isPressed);
#else
            return Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
#endif
        }

        public static bool IsAltPressed()
        {
#if UDND_INPUT_SYSTEM && ENABLE_INPUT_SYSTEM
            var keyboard = UnityEngine.InputSystem.Keyboard.current;
            return keyboard != null
                   && (keyboard.leftAltKey.isPressed || keyboard.rightAltKey.isPressed);
#else
            return Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt);
#endif
        }

        public static bool MatchesModifier(ModifierKey modifier)
        {
            bool ctrl = IsCtrlPressed();
            bool shift = IsShiftPressed();
            bool alt = IsAltPressed();

            switch (modifier)
            {
                case ModifierKey.Any:
                    return true;
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
    }
}
