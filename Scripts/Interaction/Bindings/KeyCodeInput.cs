using UnityEngine;
#if UDND_INPUT_SYSTEM && ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
#endif

namespace UDND.Interaction
{
    /// <summary>
    /// Reads a <see cref="KeyCode"/> through whichever input backend is active. With the Input System
    /// package selected in Player Settings the legacy <see cref="Input"/> class throws, so the KeyCode is
    /// translated to an Input System control instead. Unmapped KeyCodes simply never trigger.
    /// </summary>
    public static class KeyCodeInput
    {
        public static bool GetKeyDown(KeyCode key)
        {
#if UDND_INPUT_SYSTEM && ENABLE_INPUT_SYSTEM
            var control = ResolveControl(key);
            return control != null && control.wasPressedThisFrame;
#else
            return key != KeyCode.None && Input.GetKeyDown(key);
#endif
        }

        public static bool GetKeyUp(KeyCode key)
        {
#if UDND_INPUT_SYSTEM && ENABLE_INPUT_SYSTEM
            var control = ResolveControl(key);
            return control != null && control.wasReleasedThisFrame;
#else
            return key != KeyCode.None && Input.GetKeyUp(key);
#endif
        }

        public static bool GetKey(KeyCode key)
        {
#if UDND_INPUT_SYSTEM && ENABLE_INPUT_SYSTEM
            var control = ResolveControl(key);
            return control != null && control.isPressed;
#else
            return key != KeyCode.None && Input.GetKey(key);
#endif
        }

        /// <summary>Pointer position in screen space, from whichever input backend is active.</summary>
        public static Vector2 MousePosition
        {
            get
            {
#if UDND_INPUT_SYSTEM && ENABLE_INPUT_SYSTEM
                var mouse = Mouse.current;
                if (mouse != null)
                    return mouse.position.ReadValue();

                var touch = Touchscreen.current;
                return touch != null ? touch.position.ReadValue() : Vector2.zero;
#else
                return Input.mousePosition;
#endif
            }
        }

#if UDND_INPUT_SYSTEM && ENABLE_INPUT_SYSTEM
        private static ButtonControl ResolveControl(KeyCode keyCode)
        {
            if (keyCode == KeyCode.None)
                return null;

            if (keyCode >= KeyCode.Mouse0 && keyCode <= KeyCode.Mouse6)
                return ResolveMouseButton(keyCode);

            var mapped = ToKey(keyCode);
            if (mapped == Key.None)
                return null;

            var keyboard = Keyboard.current;
            return keyboard != null ? keyboard[mapped] : null;
        }

        private static ButtonControl ResolveMouseButton(KeyCode keyCode)
        {
            var mouse = Mouse.current;
            if (mouse == null)
                return null;

            switch (keyCode)
            {
                case KeyCode.Mouse0: return mouse.leftButton;
                case KeyCode.Mouse1: return mouse.rightButton;
                case KeyCode.Mouse2: return mouse.middleButton;
                case KeyCode.Mouse3: return mouse.backButton;
                case KeyCode.Mouse4: return mouse.forwardButton;
                default: return null;
            }
        }

        private static Key ToKey(KeyCode keyCode)
        {
            // Letters, function keys and the numpad digits are contiguous in both enums.
            if (keyCode >= KeyCode.A && keyCode <= KeyCode.Z)
                return Key.A + (keyCode - KeyCode.A);
            if (keyCode >= KeyCode.F1 && keyCode <= KeyCode.F12)
                return Key.F1 + (keyCode - KeyCode.F1);
            if (keyCode >= KeyCode.Keypad0 && keyCode <= KeyCode.Keypad9)
                return Key.Numpad0 + (keyCode - KeyCode.Keypad0);

            switch (keyCode)
            {
                case KeyCode.Alpha0: return Key.Digit0;
                case KeyCode.Alpha1: return Key.Digit1;
                case KeyCode.Alpha2: return Key.Digit2;
                case KeyCode.Alpha3: return Key.Digit3;
                case KeyCode.Alpha4: return Key.Digit4;
                case KeyCode.Alpha5: return Key.Digit5;
                case KeyCode.Alpha6: return Key.Digit6;
                case KeyCode.Alpha7: return Key.Digit7;
                case KeyCode.Alpha8: return Key.Digit8;
                case KeyCode.Alpha9: return Key.Digit9;

                case KeyCode.Space: return Key.Space;
                case KeyCode.Return: return Key.Enter;
                case KeyCode.KeypadEnter: return Key.NumpadEnter;
                case KeyCode.Escape: return Key.Escape;
                case KeyCode.Tab: return Key.Tab;
                case KeyCode.Backspace: return Key.Backspace;
                case KeyCode.Delete: return Key.Delete;
                case KeyCode.Insert: return Key.Insert;
                case KeyCode.Home: return Key.Home;
                case KeyCode.End: return Key.End;
                case KeyCode.PageUp: return Key.PageUp;
                case KeyCode.PageDown: return Key.PageDown;

                case KeyCode.UpArrow: return Key.UpArrow;
                case KeyCode.DownArrow: return Key.DownArrow;
                case KeyCode.LeftArrow: return Key.LeftArrow;
                case KeyCode.RightArrow: return Key.RightArrow;

                case KeyCode.LeftShift: return Key.LeftShift;
                case KeyCode.RightShift: return Key.RightShift;
                case KeyCode.LeftControl: return Key.LeftCtrl;
                case KeyCode.RightControl: return Key.RightCtrl;
                case KeyCode.LeftAlt: return Key.LeftAlt;
                case KeyCode.RightAlt: return Key.RightAlt;
                case KeyCode.LeftCommand: return Key.LeftCommand;
                case KeyCode.RightCommand: return Key.RightCommand;

                case KeyCode.CapsLock: return Key.CapsLock;
                case KeyCode.Numlock: return Key.NumLock;
                case KeyCode.ScrollLock: return Key.ScrollLock;
                case KeyCode.Print: return Key.PrintScreen;
                case KeyCode.Pause: return Key.Pause;

                case KeyCode.Minus: return Key.Minus;
                case KeyCode.Equals: return Key.Equals;
                case KeyCode.LeftBracket: return Key.LeftBracket;
                case KeyCode.RightBracket: return Key.RightBracket;
                case KeyCode.Backslash: return Key.Backslash;
                case KeyCode.Semicolon: return Key.Semicolon;
                case KeyCode.Quote: return Key.Quote;
                case KeyCode.Comma: return Key.Comma;
                case KeyCode.Period: return Key.Period;
                case KeyCode.Slash: return Key.Slash;
                case KeyCode.BackQuote: return Key.Backquote;

                case KeyCode.KeypadDivide: return Key.NumpadDivide;
                case KeyCode.KeypadMultiply: return Key.NumpadMultiply;
                case KeyCode.KeypadMinus: return Key.NumpadMinus;
                case KeyCode.KeypadPlus: return Key.NumpadPlus;
                case KeyCode.KeypadPeriod: return Key.NumpadPeriod;
                case KeyCode.KeypadEquals: return Key.NumpadEquals;

                default: return Key.None;
            }
        }
#endif
    }
}
