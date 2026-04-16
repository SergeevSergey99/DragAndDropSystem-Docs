using System;
using UnityEngine;
using UniversalDragAndDrop.Tools.Inspector;

namespace UniversalDragAndDrop.Interaction
{
    [Serializable]
    public class KeyBinding
    {
        [SerializeField] private string _label;
        [SerializeField] private KeyCode _key = KeyCode.None;
        [SerializeField] private ModifierKey _modifier = ModifierKey.None;
        [SerializeField] private KeyTriggerPhase _triggerPhase = KeyTriggerPhase.Down;
        [SerializeReference, ManagedReferencePicker] private SlotInteractionAction _action;

        public KeyBinding()
        {
        }

        public KeyBinding(
            string label,
            KeyCode key,
            ModifierKey modifier,
            KeyTriggerPhase triggerPhase,
            SlotInteractionAction action)
        {
            _label = label;
            _key = key;
            _modifier = modifier;
            _triggerPhase = triggerPhase;
            _action = action;
        }

        public SlotInteractionAction Action => _action;
        public string Label => string.IsNullOrEmpty(_label)
            ? (_action != null ? _action.DisplayName : "Key Binding")
            : _label;

        public bool IsValid() => _key != KeyCode.None && _action != null;

        public bool IsTriggered()
        {
            if (_key == KeyCode.None)
                return false;

            bool keyActive;
            switch (_triggerPhase)
            {
                case KeyTriggerPhase.Down:
                    keyActive = Input.GetKeyDown(_key);
                    break;
                case KeyTriggerPhase.Up:
                    keyActive = Input.GetKeyUp(_key);
                    break;
                case KeyTriggerPhase.Hold:
                    keyActive = Input.GetKey(_key);
                    break;
                default:
                    return false;
            }

            return keyActive && ModifierKeyHelper.MatchesModifier(_modifier);
        }
    }
}
