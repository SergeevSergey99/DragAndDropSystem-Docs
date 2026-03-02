using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace DragAndDropSystem.Interaction
{
    [Serializable]
    public class InputActionBinding
    {
        [SerializeField] private string _label;
        [SerializeField] private InputActionReference _actionReference;
        [SerializeReference] private SlotInteractionAction _action;

        public InputActionReference ActionReference => _actionReference;
        public SlotInteractionAction Action => _action;
        public string Label => string.IsNullOrEmpty(_label)
            ? (_action != null ? _action.DisplayName : "InputAction Binding")
            : _label;

        public bool IsValid() => _actionReference != null && _action != null;

        public bool Matches(InputAction runtimeAction)
        {
            if (runtimeAction == null || _actionReference == null)
                return false;

            var configured = _actionReference.action;
            if (configured == null)
                return false;

            return ReferenceEquals(configured, runtimeAction) || configured.id == runtimeAction.id;
        }
    }
}