using System;
using UnityEngine;

namespace DragAndDropSystem.Interaction
{
    [Serializable]
    public class NavigationBinding
    {
        [SerializeField] private string _label;
        [SerializeField] private NavigationEventType _eventType = NavigationEventType.Submit;
        [SerializeReference] private SlotInteractionAction _action;

        public NavigationBinding()
        {
        }

        public NavigationBinding(string label, NavigationEventType eventType, SlotInteractionAction action)
        {
            _label = label;
            _eventType = eventType;
            _action = action;
        }

        public SlotInteractionAction Action => _action;
        public string Label => string.IsNullOrEmpty(_label)
            ? (_action != null ? _action.DisplayName : "Navigation Binding")
            : _label;

        public bool IsValid() => _action != null;
        public bool Matches(NavigationEventType eventType) => _eventType == eventType;
    }
}