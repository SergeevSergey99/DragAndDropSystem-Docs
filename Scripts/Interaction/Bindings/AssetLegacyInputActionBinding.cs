using System;
using UnityEngine;
using UDND.Tools.Inspector;

namespace UDND.Interaction
{
    [Serializable]
    public class AssetLegacyInputActionBinding
    {
        [SerializeField] private string _label;
        [SerializeField, Tooltip("Old Input Manager button name, for example Submit or Cancel.")]
        private string _buttonName = "Submit";
        [SerializeField] private ModifierKey _modifier = ModifierKey.None;
        [SerializeField] private KeyTriggerPhase _triggerPhase = KeyTriggerPhase.Down;
        [SerializeReference, ManagedReferencePicker] private AssetSafeSlotInteractionAction _action;

        public string Label => string.IsNullOrEmpty(_label)
            ? (_action != null ? _action.DisplayName : "Legacy Input Binding")
            : _label;

        public bool IsValid() => !string.IsNullOrWhiteSpace(_buttonName) && _action != null;

        public LegacyInputActionBinding ToRuntimeBinding()
            => new LegacyInputActionBinding(_label, _buttonName, _modifier, _triggerPhase, _action);
    }
}
