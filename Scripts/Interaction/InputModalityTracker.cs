using System;
using UnityEngine;
#if UDND_INPUT_SYSTEM && ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace UDND.Interaction
{
    public sealed class InputModalityTracker : MonoBehaviour
    {
        private const float LegacyMouseMoveThreshold = 0.01f;
        private const float LegacyNavigationAxisThreshold = 0.5f;

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

        private static bool WasMouseInteractionThisFrame()
        {
#if UDND_INPUT_SYSTEM && ENABLE_INPUT_SYSTEM
            var mouse = Mouse.current;
            return mouse != null &&
                   (mouse.leftButton.wasPressedThisFrame ||
                    mouse.rightButton.wasPressedThisFrame ||
                    mouse.middleButton.wasPressedThisFrame);
#else
            return Input.GetMouseButtonDown(0)
                   || Input.GetMouseButtonDown(1)
                   || Input.GetMouseButtonDown(2)
                   || Mathf.Abs(Input.GetAxisRaw("Mouse X")) > LegacyMouseMoveThreshold
                   || Mathf.Abs(Input.GetAxisRaw("Mouse Y")) > LegacyMouseMoveThreshold;
#endif
        }

        private bool WasNavigationInteractionThisFrame()
        {
#if UDND_INPUT_SYSTEM && ENABLE_INPUT_SYSTEM
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
#else
            return Mathf.Abs(Input.GetAxisRaw("Horizontal")) >= LegacyNavigationAxisThreshold
                   || Mathf.Abs(Input.GetAxisRaw("Vertical")) >= LegacyNavigationAxisThreshold
                   //|| Input.GetButtonDown("Submit")
                   //|| Input.GetButtonDown("Cancel")
                   //|| Input.GetKeyDown(KeyCode.Tab)
                   || Input.GetKeyDown(KeyCode.UpArrow)
                   || Input.GetKeyDown(KeyCode.DownArrow)
                   || Input.GetKeyDown(KeyCode.LeftArrow)
                   || Input.GetKeyDown(KeyCode.RightArrow);
#endif
        }

        public enum InputModality
        {
            Mouse = 0,
            Navigation = 1
        }
    }
}
