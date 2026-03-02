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
        [SerializeField] private bool _useGlobalBindingsProfile = true;
        [SerializeField] private InventoryInteractionBindingsProfile _bindingsProfile;
        [SerializeField] private BindingMergeMode _pointerBindingMode = BindingMergeMode.LocalThenProfile;
        [SerializeField] private bool _usePointerBindings = true;
        [SerializeField] private List<PointerBinding> _pointerBindings = new List<PointerBinding>();
        [SerializeField] private BindingMergeMode _navigationBindingMode = BindingMergeMode.LocalThenProfile;
        [SerializeField] private bool _useNavigationBindings = true;
        [SerializeField] private List<NavigationBinding> _navigationBindings = new List<NavigationBinding>();
        [SerializeField] private BindingMergeMode _inputActionBindingMode = BindingMergeMode.LocalThenProfile;
        [SerializeField] private bool _useInputActionBindings = true;
        [SerializeField] private List<InputActionBinding> _inputActionBindings = new List<InputActionBinding>();
        
        private ISlot _focusedSlot;
        private SlotInputAdapter _focusedAdapter;
        private FocusSource _activeFocusSource = FocusSource.None;
        private InteractionState _state = InteractionState.Idle;
        private SlotInputAdapter _pressedAdapter;
        private PointerEventData.InputButton _pressedButton;
        private readonly List<InputActionSubscription> _inputActionSubscriptions = new List<InputActionSubscription>();
        private readonly List<PointerBinding> _resolvedPointerBindings = new List<PointerBinding>();
        private readonly List<NavigationBinding> _resolvedNavigationBindings = new List<NavigationBinding>();
        private readonly List<InputActionBinding> _resolvedInputActionBindings = new List<InputActionBinding>();
        private bool _resolvedUsePointerBindings = true;
        private bool _resolvedUseNavigationBindings = true;
        private bool _resolvedUseInputActionBindings = true;
        private int _lastInputBindingFrame = -1;
        private InputAction _lastInputBindingAction;

        public UniversalInventory Inventory => _inventory;
        public ISlot FocusedSlot => _focusedSlot;
        public FocusSource ActiveFocusSource => _activeFocusSource;

        private void Awake()
        {
            if (_inventory == null)
                _inventory = GetComponent<UniversalInventory>();

            ResolveBindings();
        }

        private void OnEnable()
        {
            ResolveBindings();
            InputEventRouter.Instance.RegisterCoordinator(this);
            DragAndDropManager.Instance.OnDropCompleted += HandleDragEnded;
            DragAndDropManager.Instance.OnDragCancelled += HandleDragEnded;
            BindInputActions();
        }

        private void OnDisable()
        {
            if (InputEventRouter.IsInstanceExist)
                InputEventRouter.Instance.UnregisterCoordinator(this);

            if (DragAndDropManager.IsInstanceExist)
            {
                DragAndDropManager.Instance.OnDropCompleted -= HandleDragEnded;
                DragAndDropManager.Instance.OnDragCancelled -= HandleDragEnded;
            }

            UnbindInputActions();
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

            // Drag-binding executes on pointer down to support configurable mouse buttons
            // without relying on raw BeginDrag (which is left-button-oriented in UI modules).
            if (!DragAndDropManager.Instance.IsDragging)
            {
                TryExecutePointerBinding(adapter, eventData, dragOnly: true);
            }
        }

        public void OnPointerUp(SlotInputAdapter adapter, PointerEventData eventData)
        {
            if (eventData == null || adapter == null)
                return;

            if (_state == InteractionState.Pressed && _pressedAdapter == adapter && _pressedButton == eventData.button)
            {
                bool dragOnly = DragAndDropManager.Instance.IsDragging;
                TryExecutePointerBinding(adapter, eventData, dragOnly);
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
            // Intentionally no-op.
            // Drag start must be driven only by configured bindings
            // (pointer/navigation/input-action), not by raw BeginDrag.
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
            if (DragAndDropManager.Instance.IsDragging && adapter.Slot.IsInteractable)
            {
                DragAndDropManager.Instance.PushDropTarget(adapter);
            }
        }

        public void OnFocusExit(SlotInputAdapter adapter, FocusSource source)
        {
            if (adapter?.Slot == null)
                return;

            // Игнорируем exit от источника, который сейчас не активен.
            if (source != _activeFocusSource)
                return;

            if (DragAndDropManager.Instance.IsDragging)
            {
                DragAndDropManager.Instance.PopDropTarget(adapter);
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

            if (_resolvedUseInputActionBindings && TryExecuteInputActionBinding(context))
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
            if (DragAndDropManager.Instance.IsDragging)
            {
                DragAndDropManager.Instance.CompleteDrag();
                return;
            }

            if (slot == null || slot.IsEmpty || !slot.IsInteractable)
                return;

            bool started = DragAndDropManager.Instance.StartDrag(slot);
            if (started)
            {
                _state = InteractionState.Dragging;
            }
        }

        public void TryCancelDrag()
        {
            if (DragAndDropManager.Instance.IsDragging)
            {
                DragAndDropManager.Instance.CancelDrag();
            }
        }

        private void HandleDragEnded(DragAndDropSystem.Core.DragContext _)
        {
            _state = _focusedSlot != null ? InteractionState.Focused : InteractionState.Idle;
            _pressedAdapter = null;
        }

        private void TryExecutePointerBinding(SlotInputAdapter adapter, PointerEventData eventData, bool dragOnly)
        {
            if (!_resolvedUsePointerBindings || adapter?.Slot == null || eventData == null)
                return;

            if (_state == InteractionState.Dragging && !dragOnly)
                return;

            for (int i = 0; i < _resolvedPointerBindings.Count; i++)
            {
                var binding = _resolvedPointerBindings[i];
                if (binding == null || !binding.IsValid())
                    continue;
                if (!binding.Matches(eventData))
                    continue;
                if (dragOnly && !(binding.Action is DragSlotAction))
                    continue;
                if (!dragOnly && binding.Action is DragSlotAction)
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

        private void ResolveBindings()
        {
            _resolvedPointerBindings.Clear();
            _resolvedNavigationBindings.Clear();
            _resolvedInputActionBindings.Clear();

            var profile = ResolveProfile();
            if (profile != null)
            {
                BuildMergedBindings(_pointerBindingMode, _usePointerBindings, _pointerBindings, profile.UsePointerBindings, profile.PointerBindings, _resolvedPointerBindings, out _resolvedUsePointerBindings);
                BuildMergedBindings(_navigationBindingMode, _useNavigationBindings, _navigationBindings, profile.UseNavigationBindings, profile.NavigationBindings, _resolvedNavigationBindings, out _resolvedUseNavigationBindings);
                BuildMergedBindings(_inputActionBindingMode, _useInputActionBindings, _inputActionBindings, profile.UseInputActionBindings, profile.InputActionBindings, _resolvedInputActionBindings, out _resolvedUseInputActionBindings);
                return;
            }

            _resolvedUsePointerBindings = _usePointerBindings;
            _resolvedUseNavigationBindings = _useNavigationBindings;
            _resolvedUseInputActionBindings = _useInputActionBindings;
            _resolvedPointerBindings.AddRange(_pointerBindings);
            _resolvedNavigationBindings.AddRange(_navigationBindings);
            _resolvedInputActionBindings.AddRange(_inputActionBindings);
        }

        private bool TryExecuteInputActionBinding(InputAction.CallbackContext context)
        {
            var inputAction = context.action;
            if (inputAction == null)
                return false;

            // Защита от двойного выполнения в одном кадре (router + прямой subscription).
            if (_lastInputBindingFrame == Time.frameCount && ReferenceEquals(_lastInputBindingAction, inputAction))
                return true;

            var adapter = _focusedAdapter ?? ResolveAdapterFromFocusedSlot();

            for (int i = 0; i < _resolvedInputActionBindings.Count; i++)
            {
                var binding = _resolvedInputActionBindings[i];
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

                _lastInputBindingFrame = Time.frameCount;
                _lastInputBindingAction = inputAction;
                return true;
            }

            return false;
        }

        private void BindInputActions()
        {
            UnbindInputActions();

            if (!_resolvedUseInputActionBindings)
                return;

            for (int i = 0; i < _resolvedInputActionBindings.Count; i++)
            {
                var binding = _resolvedInputActionBindings[i];
                if (binding == null || !binding.IsValid())
                    continue;

                var action = binding.ActionReference.action;
                if (action == null)
                    continue;

                Action<InputAction.CallbackContext> handler = HandleInputActionPerformed;
                action.performed += handler;
                _inputActionSubscriptions.Add(new InputActionSubscription(action, handler));
            }
        }

        private void UnbindInputActions()
        {
            for (int i = 0; i < _inputActionSubscriptions.Count; i++)
            {
                var sub = _inputActionSubscriptions[i];
                if (sub.Action == null)
                    continue;

                sub.Action.performed -= sub.Handler;
            }

            _inputActionSubscriptions.Clear();
        }

        private void HandleInputActionPerformed(InputAction.CallbackContext context)
        {
            if (!_resolvedUseInputActionBindings)
                return;

            bool handled = TryExecuteInputActionBinding(context);
            if (!handled && _logWarnings)
            {
                var actionName = context.action != null ? context.action.name : "<null>";
                Debug.LogWarning($"[{name}] No matching coordinator input binding for action '{actionName}'.");
            }
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
            if (!_resolvedUseNavigationBindings || adapter?.Slot == null)
                return;

            if (_state == InteractionState.Dragging && eventType != NavigationEventType.Cancel)
                return;

            for (int i = 0; i < _resolvedNavigationBindings.Count; i++)
            {
                var binding = _resolvedNavigationBindings[i];
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

        private InventoryInteractionBindingsProfile ResolveProfile()
        {
            if (_bindingsProfile != null)
                return _bindingsProfile;

            if (!_useGlobalBindingsProfile)
                return null;

            return InputEventRouter.Instance.DefaultBindingsProfile;
        }

        private static void BuildMergedBindings<TBinding>(
            BindingMergeMode mode,
            bool localEnabled,
            List<TBinding> localBindings,
            bool profileEnabled,
            IReadOnlyList<TBinding> profileBindings,
            List<TBinding> destination,
            out bool isEnabled)
            where TBinding : class
        {
            switch (mode)
            {
                case BindingMergeMode.LocalOnly:
                    isEnabled = localEnabled;
                    AppendValidBindings(localBindings, destination);
                    return;

                case BindingMergeMode.ProfileOnly:
                    isEnabled = profileEnabled;
                    AppendValidBindings(profileBindings, destination);
                    return;

                case BindingMergeMode.LocalThenProfile:
                    isEnabled = localEnabled && profileEnabled;
                    AppendValidBindings(localBindings, destination);
                    AppendValidBindings(profileBindings, destination);
                    return;

                default:
                    isEnabled = localEnabled;
                    AppendValidBindings(localBindings, destination);
                    return;
            }
        }

        private static void AppendValidBindings<TBinding>(IReadOnlyList<TBinding> source, List<TBinding> destination)
            where TBinding : class
        {
            if (source == null)
                return;

            for (int i = 0; i < source.Count; i++)
            {
                var binding = source[i];
                if (binding != null)
                    destination.Add(binding);
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

            public InputActionReference ActionReference => _actionReference;
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

        private readonly struct InputActionSubscription
        {
            public InputActionSubscription(InputAction action, Action<InputAction.CallbackContext> handler)
            {
                Action = action;
                Handler = handler;
            }

            public InputAction Action { get; }
            public Action<InputAction.CallbackContext> Handler { get; }
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

        public enum BindingMergeMode
        {
            LocalOnly = 0,
            ProfileOnly = 1,
            LocalThenProfile = 2
        }
    }
}
