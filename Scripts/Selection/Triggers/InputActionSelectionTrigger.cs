using UnityEngine;
using UnityEngine.InputSystem;

namespace DragAndDropSystem.Selection
{
    /// <summary>
    /// Триггер выделения через Input System Action.
    /// Пример: Ctrl+A → SelectAllOperation, Escape → ClearSelectionOperation.
    /// </summary>
    public class InputActionSelectionTrigger : SelectionTriggerBase
    {
        [SerializeField] private InputActionReference _actionReference;
        [SerializeField] private TriggerPhase _triggerPhase = TriggerPhase.Performed;

        private void OnEnable()
        {
            if (_actionReference?.action == null)
            {
                if (_logWarnings)
                    Debug.LogWarning($"[{name}] InputActionReference is not assigned.");
                return;
            }

            switch (_triggerPhase)
            {
                case TriggerPhase.Started:   _actionReference.action.started   += OnAction; break;
                case TriggerPhase.Performed: _actionReference.action.performed += OnAction; break;
                case TriggerPhase.Canceled:  _actionReference.action.canceled  += OnAction; break;
            }
        }

        private void OnDisable()
        {
            if (_actionReference?.action == null) return;
            _actionReference.action.started   -= OnAction;
            _actionReference.action.performed -= OnAction;
            _actionReference.action.canceled  -= OnAction;
        }

        private void OnAction(InputAction.CallbackContext ctx) => TryExecute();

        public enum TriggerPhase { Started, Performed, Canceled }
    }
}
