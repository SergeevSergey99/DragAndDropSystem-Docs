using DragAndDropSystem.Inventories;
using DragAndDropSystem.Selection;
using DragAndDropSystem.Slots;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
#if ENABLE_REFLEX_DI
using Reflex.Attributes;
#endif

namespace DragAndDropSystem.Interaction
{
    /// <summary>
    /// Координатор интентов взаимодействия для одного инвентаря.
    /// На текущем этапе:
    /// - принимает raw pointer/focus события от router
    /// - маршрутизирует drag/drop в DragAndDropManager
    /// - маршрутизирует InventoryAction через focused slot
    /// </summary>
    [DisallowMultipleComponent]
    public class InventoryInteractionCoordinator : MonoBehaviour
    {
        private enum InteractionState
        {
            Idle = 0,
            Focused = 1,
            Pressed = 2,
            Dragging = 3
        }

        [SerializeField] private UniversalInventory _inventory;
        [SerializeField] private bool _logWarnings;
        [SerializeField] private bool _usePointerBindings = true;
        [SerializeField] private List<PointerBinding> _pointerBindings = new List<PointerBinding>();
        [SerializeField] private bool _useNavigationBindings = true;
        [SerializeField] private List<NavigationBinding> _navigationBindings = new List<NavigationBinding>();
        [SerializeField] private bool _useInputActionBindings = true;
        [SerializeField] private List<InputActionBinding> _inputActionBindings = new List<InputActionBinding>();
        [SerializeField] private bool _autoConfigureDefaultBindings = true;

        private ISlot _focusedSlot;
        private SlotInputAdapter _focusedAdapter;
        private FocusSource _activeFocusSource = FocusSource.None;
        private InteractionState _state = InteractionState.Idle;
        private SlotInputAdapter _pressedAdapter;
        private PointerEventData.InputButton _pressedButton;

        public UniversalInventory Inventory => _inventory;
        public ISlot FocusedSlot => _focusedSlot;
        public FocusSource ActiveFocusSource => _activeFocusSource;
        public bool IsDragInProgress => _dragManager != null && _dragManager.IsDragging;

#if ENABLE_REFLEX_DI
        [Inject] private DragAndDropManager _dragManager;
#else
        private DragAndDropManager _dragManager => DragAndDropManager.Instance;
#endif

        private void Awake()
        {
            if (_inventory == null)
                _inventory = GetComponent<UniversalInventory>();

            EnsureDefaultBindings();
        }

        private void OnEnable()
        {
            EnsureDefaultBindings();
            InputEventRouter.Instance.RegisterCoordinator(this);
            if (_dragManager != null)
            {
                _dragManager.OnDropCompleted += HandleDragEnded;
                _dragManager.OnDragCancelled += HandleDragEnded;
            }
        }

        private void OnDisable()
        {
            if (InputEventRouter.IsInstanceExist)
                InputEventRouter.Instance.UnregisterCoordinator(this);

            if (_dragManager != null)
            {
                _dragManager.OnDropCompleted -= HandleDragEnded;
                _dragManager.OnDragCancelled -= HandleDragEnded;
            }
        }

        public void OnPointerEnter(SlotInputAdapter adapter, PointerEventData _)
        {
            if (adapter?.Slot == null)
                return;

            OnFocusEnter(adapter, FocusSource.Mouse);
        }

        public void OnPointerExit(SlotInputAdapter adapter, PointerEventData _)
        {
            if (adapter?.Slot == null)
                return;

            OnFocusExit(adapter, FocusSource.Mouse);
        }

        public void OnPointerDown(SlotInputAdapter adapter, PointerEventData eventData)
        {
            if (adapter?.Slot == null || eventData == null)
                return;

            _pressedAdapter = adapter;
            _pressedButton = eventData.button;
            _state = InteractionState.Pressed;
        }

        public void OnPointerUp(SlotInputAdapter adapter, PointerEventData eventData)
        {
            if (eventData == null || adapter == null)
                return;

            if (_state == InteractionState.Pressed && _pressedAdapter == adapter && _pressedButton == eventData.button)
            {
                TryExecutePointerBinding(adapter, eventData);
            }

            // Выход из Pressed независимо от результата, чтобы не "залипать".
            if (_state == InteractionState.Pressed && _pressedAdapter == adapter && _pressedButton == eventData.button)
            {
                _pressedAdapter = null;
                _state = _focusedSlot != null ? InteractionState.Focused : InteractionState.Idle;
            }
        }

        public void OnBeginDrag(SlotInputAdapter adapter, PointerEventData eventData)
        {
            if (adapter?.Slot == null || eventData == null)
                return;

            if (eventData.button != PointerEventData.InputButton.Left)
                return;

            if (_dragManager.IsDragging)
                return;

            // Drag стартуем только с интерактивного и непустого слота.
            if (!adapter.Slot.IsInteractable || adapter.Slot.IsEmpty)
                return;

            bool started = _dragManager.StartDrag(adapter.Slot);
            if (started)
            {
                _state = InteractionState.Dragging;
                _pressedAdapter = null;
            }
            else if (_logWarnings)
            {
                Debug.LogWarning($"[{name}] Failed to start drag for slot {adapter.Slot.Index}");
            }
        }

        public void OnFocusEnter(SlotInputAdapter adapter, FocusSource source)
        {
            if (adapter?.Slot == null)
                return;

            // Источник фокуса: последнее взаимодействие побеждает.
            _activeFocusSource = source;
            _focusedSlot = adapter.Slot;
            _focusedAdapter = adapter;

            if (_state == InteractionState.Idle)
                _state = InteractionState.Focused;

            // Новый pipeline: координатор управляет drop target стеком.
            if (_dragManager.IsDragging && adapter.Slot.IsInteractable)
            {
                _dragManager.PushDropTarget(adapter);
            }
        }

        public void OnFocusExit(SlotInputAdapter adapter, FocusSource source)
        {
            if (adapter?.Slot == null)
                return;

            // Игнорируем exit от источника, который сейчас не активен.
            if (source != _activeFocusSource)
                return;

            if (_dragManager.IsDragging)
            {
                _dragManager.PopDropTarget(adapter);
            }

            if (ReferenceEquals(_focusedSlot, adapter.Slot))
            {
                _focusedSlot = null;
                _focusedAdapter = null;
                _activeFocusSource = FocusSource.None;
                if (_state != InteractionState.Dragging)
                    _state = InteractionState.Idle;
            }
        }

        public void OnSubmit(SlotInputAdapter adapter, BaseEventData _)
        {
            TryExecuteNavigationBinding(adapter, NavigationEventType.Submit);
        }

        public void OnCancel(SlotInputAdapter adapter, BaseEventData _)
        {
            TryExecuteNavigationBinding(adapter, NavigationEventType.Cancel);
        }

        public bool RouteInventoryAction(
            InventoryActionBase action,
            InputAction.CallbackContext context,
            bool logWarnings)
        {
            if (_inventory == null || action == null)
                return false;

            if (_useInputActionBindings && TryExecuteInputActionBinding(context))
                return true;

            var activeSlot = _focusedSlot as UniversalSlot ?? _inventory.ResolveAutoTransferSlot();
            if (!action.CanExecute(_inventory, activeSlot))
            {
                if (logWarnings || _logWarnings)
                {
                    Debug.LogWarning($"[{name}] Action '{action.DisplayName}' cannot be executed for inventory '{_inventory.name}'.");
                }
                return true;
            }

            bool success = action.Execute(_inventory, activeSlot, logWarnings || _logWarnings);
            if (!success && (logWarnings || _logWarnings))
            {
                Debug.LogWarning($"[{name}] Action '{action.DisplayName}' failed for inventory '{_inventory.name}'.");
            }

            return true;
        }

        public void TryExecuteDrag(UniversalSlot slot)
        {
            if (_dragManager.IsDragging)
            {
                _dragManager.CompleteDrag();
                return;
            }

            if (slot == null || slot.IsEmpty || !slot.IsInteractable)
                return;

            bool started = _dragManager.StartDrag(slot);
            if (started)
            {
                _state = InteractionState.Dragging;
            }
        }

        public void TryCancelDrag()
        {
            if (_dragManager.IsDragging)
            {
                _dragManager.CancelDrag();
            }
        }

        private void HandleDragEnded(DragAndDropSystem.Core.DragContext _)
        {
            _state = _focusedSlot != null ? InteractionState.Focused : InteractionState.Idle;
            _pressedAdapter = null;
        }

        private void TryExecutePointerBinding(SlotInputAdapter adapter, PointerEventData eventData)
        {
            if (!_usePointerBindings || adapter?.Slot == null || eventData == null)
                return;

            if (_state == InteractionState.Dragging)
                return;

            for (int i = 0; i < _pointerBindings.Count; i++)
            {
                var binding = _pointerBindings[i];
                if (binding == null || !binding.IsValid())
                    continue;
                if (!binding.Matches(eventData))
                    continue;

                if (binding.Action.CanExecute(this, adapter, eventData))
                {
                    binding.Action.Execute(this, adapter, eventData);
                    eventData.Use();
                }
                else if (_logWarnings)
                {
                    Debug.LogWarning($"[{name}] Pointer binding '{binding.Label}' cannot execute.");
                }
                return;
            }
        }

        private void EnsureDefaultBindings()
        {
            if (!_autoConfigureDefaultBindings)
                return;

            if (_pointerBindings.Count == 0)
            {
                _pointerBindings.Add(new PointerBinding(
                    "LMB Drag",
                    PointerEventData.InputButton.Left,
                    ModifierKey.None,
                    new DragSlotAction()));
                _pointerBindings.Add(new PointerBinding(
                    "LMB Ctrl Toggle",
                    PointerEventData.InputButton.Left,
                    ModifierKey.Ctrl,
                    new SelectionSlotAction(new ToggleSlotOperation())));
                _pointerBindings.Add(new PointerBinding(
                    "LMB Shift Range",
                    PointerEventData.InputButton.Left,
                    ModifierKey.Shift,
                    new SelectionSlotAction(new RangeSelectOperation())));
                _pointerBindings.Add(new PointerBinding(
                    "RMB Select",
                    PointerEventData.InputButton.Right,
                    ModifierKey.None,
                    new SelectionSlotAction(new ClearAndSelectOperation())));
            }

            if (_navigationBindings.Count == 0)
            {
                _navigationBindings.Add(new NavigationBinding("Submit Drag", NavigationEventType.Submit, new DragSlotAction()));
                _navigationBindings.Add(new NavigationBinding("Cancel Drag", NavigationEventType.Cancel, new CancelDragAction()));
            }
        }

        private bool TryExecuteInputActionBinding(InputAction.CallbackContext context)
        {
            var inputAction = context.action;
            if (inputAction == null)
                return false;

            var adapter = _focusedAdapter ?? ResolveAdapterFromFocusedSlot();

            for (int i = 0; i < _inputActionBindings.Count; i++)
            {
                var binding = _inputActionBindings[i];
                if (binding == null || !binding.IsValid())
                    continue;
                if (!binding.Matches(inputAction))
                    continue;

                if (binding.Action.CanExecute(this, adapter, null))
                {
                    binding.Action.Execute(this, adapter, null);
                }
                else if (_logWarnings)
                {
                    Debug.LogWarning($"[{name}] Input binding '{binding.Label}' cannot execute.");
                }
                return true;
            }

            return false;
        }

        private SlotInputAdapter ResolveAdapterFromFocusedSlot()
        {
            if (_focusedSlot is UniversalSlot universalSlot)
            {
                return universalSlot.GetComponent<SlotInputAdapter>();
            }
            return null;
        }

        private void TryExecuteNavigationBinding(SlotInputAdapter adapter, NavigationEventType eventType)
        {
            if (!_useNavigationBindings || adapter?.Slot == null)
                return;

            if (_state == InteractionState.Dragging && eventType != NavigationEventType.Cancel)
                return;

            for (int i = 0; i < _navigationBindings.Count; i++)
            {
                var binding = _navigationBindings[i];
                if (binding == null || !binding.IsValid())
                    continue;
                if (!binding.Matches(eventType))
                    continue;

                if (binding.Action.CanExecute(this, adapter, null))
                {
                    binding.Action.Execute(this, adapter, null);
                }
                else if (_logWarnings)
                {
                    Debug.LogWarning($"[{name}] Navigation binding '{binding.Label}' cannot execute.");
                }
                return;
            }
        }

        [Serializable]
        public class PointerBinding
        {
            [SerializeField] private string _label;
            [SerializeField] private PointerEventData.InputButton _button = PointerEventData.InputButton.Left;
            [SerializeField] private ModifierKey _modifier = ModifierKey.None;
            [SerializeReference] private SlotInteractionAction _action;

            public PointerBinding()
            {
            }

            public PointerBinding(string label, PointerEventData.InputButton button, ModifierKey modifier, SlotInteractionAction action)
            {
                _label = label;
                _button = button;
                _modifier = modifier;
                _action = action;
            }

            public SlotInteractionAction Action => _action;
            public string Label => string.IsNullOrEmpty(_label)
                ? (_action != null ? _action.DisplayName : "Pointer Binding")
                : _label;

            public bool IsValid() => _action != null;

            public bool Matches(PointerEventData eventData)
            {
                if (eventData == null || eventData.button != _button)
                    return false;

                bool ctrl = Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl);
                bool shift = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
                bool alt = Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt);

                switch (_modifier)
                {
                    case ModifierKey.None:
                        return !ctrl && !shift && !alt;
                    case ModifierKey.Ctrl:
                        return ctrl && !shift && !alt;
                    case ModifierKey.Shift:
                        return shift && !ctrl && !alt;
                    case ModifierKey.Alt:
                        return alt && !ctrl && !shift;
                    default:
                        return false;
                }
            }
        }

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

        [Serializable]
        public class InputActionBinding
        {
            [SerializeField] private string _label;
            [SerializeField] private InputActionReference _actionReference;
            [SerializeReference] private SlotInteractionAction _action;

            public SlotInteractionAction Action => _action;
            public string Label => string.IsNullOrEmpty(_label)
                ? (_action != null ? _action.DisplayName : "InputAction Binding")
                : _label;

            public bool IsValid() => _actionReference != null && _action != null;

            public bool Matches(InputAction runtimeAction)
            {
                if (runtimeAction == null || _actionReference == null)
                    return false;

                var configured = _actionReference.action;
                if (configured == null)
                    return false;

                return ReferenceEquals(configured, runtimeAction) || configured.name == runtimeAction.name;
            }
        }

        public enum ModifierKey
        {
            None = 0,
            Ctrl = 1,
            Shift = 2,
            Alt = 3
        }

        public enum NavigationEventType
        {
            Submit = 0,
            Cancel = 1
        }
    }
}
