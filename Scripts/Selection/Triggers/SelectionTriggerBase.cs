using DragAndDropSystem.Slots;
using DragAndDropSystem.Inspector;
using UnityEngine;

namespace DragAndDropSystem.Selection
{
    /// <summary>
    /// Базовый класс для триггеров выделения.
    /// Отвечает за ТО, КОГДА выполняется операция.
    /// Конкретная логика что делать — в SelectionOperationBase.
    /// </summary>
    public abstract class SelectionTriggerBase : MonoBehaviour
    {
        [SerializeField, SerializeReference, ManagedReferencePicker]
        protected SelectionOperationBase _operation;

        [SerializeField] protected bool _logWarnings;

        /// <summary>
        /// Выполнить операцию. Вызывается конкретными триггерами.
        /// </summary>
        /// <param name="contextSlot">Слот, инициировавший операцию (null для кнопок/хоткеев)</param>
        protected void TryExecute(ISlot contextSlot = null)
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

            if (!_operation.CanExecute(manager, contextSlot))
            {
                if (_logWarnings)
                    Debug.LogWarning($"[{name}] Operation '{_operation.DisplayName}' cannot execute.");
                return;
            }

            _operation.Execute(manager, contextSlot);
        }
    }
}
