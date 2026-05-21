using System;
using UnityEngine;
using UDND.Tools.Inspector;

namespace UDND.Interaction
{
    [Serializable]
    public class AssetKeyBinding
    {
        [SerializeField] private string _label;
        [SerializeField] private KeyCode _key = KeyCode.None;
        [SerializeField] private ModifierKey _modifier = ModifierKey.None;
        [SerializeField] private KeyTriggerPhase _triggerPhase = KeyTriggerPhase.Down;
        [SerializeReference, ManagedReferencePicker] private AssetSafeSlotInteractionAction _action;

        public string Label => string.IsNullOrEmpty(_label)
            ? (_action != null ? _action.DisplayName : "Key Binding")
            : _label;

        public bool IsValid() => _key != KeyCode.None && _action != null;

        public KeyBinding ToRuntimeBinding()
            => new KeyBinding(_label, _key, _modifier, _triggerPhase, _action);
    }
}
