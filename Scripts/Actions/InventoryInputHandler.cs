using System;
using System.Collections.Generic;
using DragAndDropSystem.Interaction;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.InputSystem;

namespace DragAndDropSystem.Inventories
{
    /// <summary>
    /// Универсальный обработчик ввода для инвентаря через Input System.
    /// Позволяет привязывать различные действия (автоперенос, сортировка и т.д.) к клавишам.
    /// </summary>
    [DisallowMultipleComponent]
    public class InventoryInputHandler : MonoBehaviour
    {
        [SerializeField] private UniversalInventory _inventory;

        [SerializeField, Tooltip("Список привязок действий к клавишам Input System")]
        [ListDrawerSettings(ShowIndexLabels = true, ListElementLabelName = "Label")]
        private List<ActionBinding> _bindings = new List<ActionBinding>();

        [SerializeField, Tooltip("Писать предупреждения, если действие не сработало")]
        private bool _logWarnings;
        
        private readonly List<Subscription> _subscriptions = new List<Subscription>();

        private void Awake()
        {
            if (_inventory == null)
            {
                _inventory = GetComponent<UniversalInventory>();
            }
        }

        private void OnEnable()
        {
            if (_inventory == null)
            {
                Debug.LogError($"{nameof(InventoryInputHandler)} on {name}: UniversalInventory reference is missing.");
                return;
            }

            foreach (var binding in _bindings)
            {
                if (!binding.IsValid())
                    continue;

                var action = binding.ActionReference.action;
                if (action == null)
                {
                    if (_logWarnings)
                    {
                        Debug.LogWarning($"{nameof(InventoryInputHandler)} on {name}: InputActionReference '{binding.Label}' has no action.");
                    }
                    continue;
                }

                Action<InputAction.CallbackContext> handler = ctx => HandleAction(binding, ctx);

                switch (binding.TriggerPhase)
                {
                    case TriggerPhaseEnum.Started:
                        action.started += handler;
                        break;
                    case TriggerPhaseEnum.Performed:
                        action.performed += handler;
                        break;
                    case TriggerPhaseEnum.Canceled:
                        action.canceled += handler;
                        break;
                }

                _subscriptions.Add(new Subscription(action, handler, binding.TriggerPhase));
            }
        }

        private void OnDisable()
        {
            foreach (var subscription in _subscriptions)
            {
                if (subscription.Action == null)
                    continue;

                switch (subscription.Phase)
                {
                    case TriggerPhaseEnum.Started:
                        subscription.Action.started -= subscription.Handler;
                        break;
                    case TriggerPhaseEnum.Performed:
                        subscription.Action.performed -= subscription.Handler;
                        break;
                    case TriggerPhaseEnum.Canceled:
                        subscription.Action.canceled -= subscription.Handler;
                        break;
                }
            }

            _subscriptions.Clear();
        }

        private void HandleAction(ActionBinding binding, InputAction.CallbackContext context)
        {
            if (!binding.ShouldProcess(context))
                return;

            if (binding.Action == null)
            {
                if (_logWarnings)
                {
                    Debug.LogWarning($"[{name}] Input '{binding.Label}': No action assigned.");
                }
                return;
            }

            bool routed = InputEventRouter.Instance.TryRouteInventoryAction(_inventory, binding.Action, context, _logWarnings);
            if (routed)
                return;

            // Fallback: execute directly when coordinator/router path is unavailable.
            var activeSlot = _inventory.ResolveAutoTransferSlot();
            if (!binding.Action.CanExecute(_inventory, activeSlot))
            {
                if (_logWarnings)
                {
                    Debug.LogWarning($"[{name}] Input '{binding.Label}': action '{binding.Action.DisplayName}' cannot execute for inventory '{_inventory.name}'.");
                }
                return;
            }

            bool success = binding.Action.Execute(_inventory, activeSlot, _logWarnings);
            if (!success && _logWarnings)
            {
                Debug.LogWarning($"[{name}] Input '{binding.Label}': direct execute failed for inventory '{_inventory.name}'.");
            }
        }

        [Serializable]
        public class ActionBinding
        {
            [SerializeField, Tooltip("Название для читаемости в инспекторе")]
            private string _label;

            [SerializeField, Required, Tooltip("Действие Input System")]
            private InputActionReference _actionReference;

            [SerializeField, Tooltip("Стадия действия, на которой выполняется действие")]
            private TriggerPhaseEnum _triggerPhase = TriggerPhaseEnum.Performed;

            [Required, Tooltip("Действие инвентаря")]
            [LabelText("Action Type"), SerializeField]
            private InventoryActionBase _action;

            public string Label
            {
                get
                {
                    if (!string.IsNullOrEmpty(_label))
                        return _label;

                    if (_action != null)
                        return _action.DisplayName;

                    if (_actionReference != null)
                        return _actionReference.name;

                    return "Unnamed Action";
                }
            }

            public InputActionReference ActionReference => _actionReference;
            public TriggerPhaseEnum TriggerPhase => _triggerPhase;
            public InventoryActionBase Action => _action;

            public bool IsValid() => _actionReference != null && _action != null;

            public bool ShouldProcess(InputAction.CallbackContext context)
            {
                // Для composites Input System может вызывать один и тот же callback несколько раз.
                // Фильтруем лишние вызовы по фазе.
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

        public enum TriggerPhaseEnum
        {
            Started,
            Performed,
            Canceled
        }

        private readonly struct Subscription
        {
            public Subscription(InputAction action, Action<InputAction.CallbackContext> handler, TriggerPhaseEnum phase)
            {
                Action = action;
                Handler = handler;
                Phase = phase;
            }

            public InputAction Action { get; }
            public Action<InputAction.CallbackContext> Handler { get; }
            public TriggerPhaseEnum Phase { get; }
        }
    }
}
