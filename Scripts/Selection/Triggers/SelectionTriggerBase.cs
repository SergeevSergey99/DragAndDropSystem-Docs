using UnityEngine;
using UDND.Slots;
using UDND.Tools.Inspector;

namespace UDND.Selection
{
    /// <summary>
    /// Base class for selection triggers.
    /// Responsible for WHEN the operation is executed.
    /// The concrete selection logic lives in SelectionOperationBase.
    /// </summary>
    public abstract class SelectionTriggerBase : MonoBehaviour
    {
        [SerializeField, SerializeReference, ManagedReferencePicker]
        protected SelectionOperationBase _operation;

        [SerializeField] protected bool _logWarnings;

        /// <summary>
        /// Execute the operation. Called by specific triggers.
        /// </summary>
        /// <param name="contextBaseSlot">Slot that initiated the operation (null for buttons/hotkeys)</param>
        protected void TryExecute(BaseSlot contextBaseSlot = null)
        {
            var manager = SelectionManager.AutoCreateInstance;
            if (manager == null)
            {
                if (_logWarnings)
                    Debug.LogWarning($"[{name}] SelectionManager not found.");
                return;
            }

            if (_operation == null)
            {
                if (_logWarnings)
                    Debug.LogWarning($"[{name}] No operation assigned.");
                return;
            }

            if (!_operation.CanExecute(manager, contextBaseSlot))
            {
                if (_logWarnings)
                    Debug.LogWarning($"[{name}] Operation '{_operation.DisplayName}' cannot execute.");
                return;
            }

            _operation.Execute(manager, contextBaseSlot);
        }
    }
}
