using System;
using DragAndDropSystem.Inspector;
using UnityEngine;
using UnityEngine.InputSystem;

namespace DragAndDropSystem.Interaction
{
    [Serializable]
    public class AssetInputActionBinding
    {
        [SerializeField, Tooltip("Name for readability in the Inspector")]
        private string _label;

        [SerializeField, Tooltip("Input System action")]
        private InputActionReference _actionReference;

        [SerializeField, Tooltip("Action phase at which the action is executed")]
        private TriggerPhaseEnum _triggerPhase = TriggerPhaseEnum.Performed;

        [SerializeReference, ManagedReferencePicker] private AssetSafeSlotInteractionAction _action;

        public string Label => string.IsNullOrEmpty(_label)
            ? (_action != null ? _action.DisplayName : "InputAction Binding")
            : _label;

        public bool IsValid() => _actionReference != null && _action != null;

        public InputActionBinding ToRuntimeBinding()
            => new InputActionBinding(_label, _actionReference, _triggerPhase, _action);
    }
}
