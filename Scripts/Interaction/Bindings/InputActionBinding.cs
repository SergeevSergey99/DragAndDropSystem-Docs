using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace DragAndDropSystem.Interaction
{
    [Serializable]
    public class InputActionBinding
    {
        [SerializeField, Tooltip("Название для читаемости в инспекторе")]
        private string _label;

        [SerializeField, Tooltip("Действие Input System")]
        private InputActionReference _actionReference;

        [SerializeField, Tooltip("Стадия действия, на которой выполняется действие")]
        private TriggerPhaseEnum _triggerPhase = TriggerPhaseEnum.Performed;

        [SerializeReference] private SlotInteractionAction _action;

        public InputActionBinding()
        {
        }

        public InputActionBinding(
            string label,
            InputActionReference actionReference,
            TriggerPhaseEnum triggerPhase,
            SlotInteractionAction action)
        {
            _label = label;
            _actionReference = actionReference;
            _triggerPhase = triggerPhase;
            _action = action;
        }

        public InputActionReference ActionReference => _actionReference;
        public SlotInteractionAction Action => _action;
        public string Label => string.IsNullOrEmpty(_label)
            ? (_action != null ? _action.DisplayName : "InputAction Binding")
            : _label;
        public TriggerPhaseEnum TriggerPhase => _triggerPhase;

        public bool IsValid() => _actionReference != null && _action != null;

        public bool Matches(InputAction.CallbackContext context)
        {
            if (context.action == null || _actionReference == null)
                return false;

            var configured = _actionReference.action;
            if (configured == null)
                return false;

            return (ReferenceEquals(configured, context.action) || configured.id == context.action.id)
                   && ShouldProcess(context);
        }

        bool ShouldProcess(InputAction.CallbackContext context)
        {
            switch (_triggerPhase)
            {
                case TriggerPhaseEnum.Started:
                    return context.phase == InputActionPhase.Started;
                case TriggerPhaseEnum.Performed:
                    return context.phase == InputActionPhase.Performed;
                case TriggerPhaseEnum.Canceled:
                    return context.phase == InputActionPhase.Canceled;
                default:
                    return false;
            }
        }
    }
}
