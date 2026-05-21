using System;
using UnityEngine;
using UDND.Tools.Inspector;

namespace UDND.Interaction
{
    [Serializable]
    public class LegacyInputActionBinding
    {
        [SerializeField] private string _label;
        [SerializeField, Tooltip("Old Input Manager button name, for example Submit or Cancel.")]
        private string _buttonName = "Submit";
        [SerializeField] private ModifierKey _modifier = ModifierKey.None;
        [SerializeField] private KeyTriggerPhase _triggerPhase = KeyTriggerPhase.Down;
        [SerializeReference, ManagedReferencePicker] private SlotInteractionAction _action;

        [NonSerialized] private bool _warningLogged;

        public LegacyInputActionBinding()
        {
        }

        public LegacyInputActionBinding(
            string label,
            string buttonName,
            ModifierKey modifier,
            KeyTriggerPhase triggerPhase,
            SlotInteractionAction action)
        {
            _label = label;
            _buttonName = buttonName;
            _modifier = modifier;
            _triggerPhase = triggerPhase;
            _action = action;
        }

        public SlotInteractionAction Action => _action;
        public KeyTriggerPhase TriggerPhase => _triggerPhase;
        public string ButtonName => NormalizeButtonName(_buttonName);
        public string Label => string.IsNullOrEmpty(_label)
            ? (_action != null ? _action.DisplayName : "Legacy Input Binding")
            : _label;

        public bool IsValid() => !string.IsNullOrEmpty(ButtonName) && _action != null;

        public bool IsTriggered()
        {
            var buttonName = ButtonName;
            if (string.IsNullOrEmpty(buttonName))
                return false;

#if ENABLE_LEGACY_INPUT_MANAGER || !(UDND_INPUT_SYSTEM && ENABLE_INPUT_SYSTEM)
            bool buttonActive;
            try
            {
                switch (_triggerPhase)
                {
                    case KeyTriggerPhase.Down:
                        buttonActive = Input.GetButtonDown(buttonName);
                        break;
                    case KeyTriggerPhase.Up:
                        buttonActive = Input.GetButtonUp(buttonName);
                        break;
                    case KeyTriggerPhase.Hold:
                        buttonActive = Input.GetButton(buttonName);
                        break;
                    default:
                        return false;
                }
            }
            catch (Exception exception) when (exception is ArgumentException || exception is InvalidOperationException)
            {
                LogWarningOnce(
                    $"[LegacyInputActionBinding] Old Input Manager button '{buttonName}' is not available or not configured. Binding '{Label}' will be ignored. {exception.Message}");
                return false;
            }

            return buttonActive && ModifierKeyHelper.MatchesModifier(_modifier);
#else
            LogWarningOnce(
                $"[LegacyInputActionBinding] Binding '{Label}' uses old Input Manager button '{buttonName}', but the Legacy Input Manager is disabled in Player Settings.");
            return false;
#endif
        }

        private static string NormalizeButtonName(string buttonName)
            => string.IsNullOrWhiteSpace(buttonName) ? string.Empty : buttonName.Trim();

        private void LogWarningOnce(string message)
        {
            if (_warningLogged)
                return;

            _warningLogged = true;
            Debug.LogWarning(message);
        }
    }
}
