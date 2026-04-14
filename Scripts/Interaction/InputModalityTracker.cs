#if DNDS_INPUT_SYSTEM
using System;
using CodeUtils;
using UnityEngine;
using UnityEngine.InputSystem;

namespace DragAndDropSystem.Interaction
{
    public sealed class InputModalityTracker : MonoBehaviour
    {
        public static event Action<InputModality> OnModalityChanged;
        public static event Action<bool> OnNavigationModeChanged;
        
        public static InputModality CurrentModality { get; private set; } = InputModality.Mouse;
        public static bool IsNavigationModeActive => CurrentModality == InputModality.Navigation;


        private void Update()
        {
            if (CurrentModality != InputModality.Mouse && WasMouseInteractionThisFrame())
            {
                MarkPointerInteraction();
            }
            else if (CurrentModality != InputModality.Navigation && WasNavigationInteractionThisFrame())
            {
                MarkNavigationInteraction();
            }
        }

        private void MarkPointerInteraction()
        {
            SetModality(InputModality.Mouse);
        }

        private void MarkNavigationInteraction()
        {
            SetModality(InputModality.Navigation);
        }

        private void SetModality(InputModality modality)
        {
            if (CurrentModality == modality)
                return;

            CurrentModality = modality;
            OnModalityChanged?.Invoke(modality);
            OnNavigationModeChanged?.Invoke(modality == InputModality.Navigation);
        }

        private bool WasNavigationInteractionThisFrame()
        {
            var gamepad = Gamepad.current;
            if (gamepad != null)
            {
                var dpad = gamepad.dpad;
                if (dpad.up.wasPressedThisFrame || dpad.down.wasPressedThisFrame ||
                    dpad.left.wasPressedThisFrame || dpad.right.wasPressedThisFrame)
                    return true;

                var stick = gamepad.leftStick;
                if (stick.up.wasPressedThisFrame || stick.down.wasPressedThisFrame ||
                    stick.left.wasPressedThisFrame || stick.right.wasPressedThisFrame)
                    return true;
            }

            var keyboard = Keyboard.current;
            if (keyboard == null)
                return false;

            if (keyboard.upArrowKey.wasPressedThisFrame || keyboard.downArrowKey.wasPressedThisFrame ||
                keyboard.leftArrowKey.wasPressedThisFrame || keyboard.rightArrowKey.wasPressedThisFrame)
                return true;

            return keyboard.wKey.wasPressedThisFrame || keyboard.aKey.wasPressedThisFrame ||
                   keyboard.sKey.wasPressedThisFrame || keyboard.dKey.wasPressedThisFrame;
        }

        private static bool WasMouseInteractionThisFrame()
        {
            var mouse = Mouse.current;
            return mouse != null &&
                   (mouse.leftButton.wasPressedThisFrame ||
                    mouse.rightButton.wasPressedThisFrame ||
                    mouse.middleButton.wasPressedThisFrame);
        }
        
        public enum InputModality
        {
            Mouse = 0,
            Navigation = 1
        }
    }
}
#endif
