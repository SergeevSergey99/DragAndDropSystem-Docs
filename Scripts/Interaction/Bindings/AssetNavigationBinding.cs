using System;
using UnityEngine;

namespace DragAndDropSystem.Interaction
{
    [Serializable]
    public class AssetNavigationBinding
    {
        [SerializeField] private string _label;
        [SerializeField] private NavigationEventType _eventType = NavigationEventType.Submit;
        [SerializeReference] private AssetOnlySlotInteractionAction _action;

        public string Label => string.IsNullOrEmpty(_label)
            ? (_action != null ? _action.DisplayName : "Navigation Binding")
            : _label;

        public bool IsValid() => _action != null;

        public NavigationBinding ToRuntimeBinding()
            => new NavigationBinding(_label, _eventType, _action);
    }
}
