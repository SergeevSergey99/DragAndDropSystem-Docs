using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace DragAndDropSystem.Interaction
{
    [Serializable]
    public class AssetPointerBinding
    {
        [SerializeField] private string _label;
        [SerializeField] private PointerEventData.InputButton _button = PointerEventData.InputButton.Left;
        [SerializeField] private ModifierKey _modifier = ModifierKey.None;
        [SerializeReference] private AssetOnlySlotInteractionAction _action;

        public string Label => string.IsNullOrEmpty(_label)
            ? (_action != null ? _action.DisplayName : "Pointer Binding")
            : _label;

        public bool IsValid() => _action != null;

        public PointerBinding ToRuntimeBinding()
            => new PointerBinding(_label, _button, _modifier, _action);
    }
}
