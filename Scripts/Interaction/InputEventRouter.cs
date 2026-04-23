using System;
using System.Collections.Generic;
using CodeUtils;
using UniversalDragAndDrop.Selection;
using UniversalDragAndDrop.Tools;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UniversalDragAndDrop.Inventories;
using UniversalDragAndDrop.Slots;
using UniversalDragAndDrop.UI;
#if UDND_INPUT_SYSTEM
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
#endif

namespace UniversalDragAndDrop.Interaction
{
    [DisallowMultipleComponent]
    public class InputEventRouter : MonoSingleton<InputEventRouter>
    {
        [field: SerializeField]
        public InteractionBindingsProfile DefaultBindingsProfile { get; private set; }

        [Header("Navigation Focus")]
        [SerializeField, Tooltip("Automatically keep focus on a slot for gamepad/keyboard navigation")]
        private bool _autoMaintainFocus = true;
        
        [Header("Pointer Gestures")]
        [SerializeField, Min(0.01f)] private float _longClickThresholdSeconds = 0.35f;
        [SerializeField, Min(0f)] private float _clickMoveTolerancePixels = 8f;

#if !(UDND_INPUT_SYSTEM && ENABLE_INPUT_SYSTEM)
        private bool _legacyEventSystemValidated;
#endif

        // Dictionary for overriding bindings for a specific inventory (for example, different UIs or operating modes)
        private readonly Dictionary<UniversalInventory, InventoryExtraInteractionBinder> _overridesByInventory = new();
        // Dictionary storing runtime state for each inventory (hovered slot, focus source, pressed buttons, etc.)
        private readonly Dictionary<UniversalInventory, RuntimeState> _runtimeStateByInventory = new();
#if UDND_INPUT_SYSTEM
        // Dictionary storing InputAction subscriptions per inventory so they can be unsubscribed when needed
        private readonly Dictionary<UniversalInventory, List<InputActionSubscription>> _actionSubscriptionsByInventory = new ();
        // InputAction subscriptions from the default profile (global, routed into _activeInventory)
        private readonly List<InputActionSubscription> _defaultProfileSubscriptions = new();
#endif
        // The last inventory the user explicitly interacted with. Used only for inventory-scoped quick actions.
        private UniversalInventory _activeInventory;

        // Set used to deduplicate action invocations within a frame and prevent double execution from multiple events (for example, PointerDown + InputAction)
        private readonly HashSet<IntentDedupKey> _handledThisFrame = new();
        // Set tracking which mouse buttons have already been handled during global PointerUp while dragging, so they are not processed multiple times
        private readonly HashSet<PointerEventData.InputButton> _pointerUpHandledThisFrame = new ();
        // Set tracking which mouse buttons had a slot-scoped PointerDown this frame, so the global-Down dispatch does not double-fire
        private readonly HashSet<PointerEventData.InputButton> _pointerDownHandledThisFrame = new ();
        // Press time and position used to determine the click phase (Short/Long) for global pointer events
        private readonly float[] _globalPressTime = new float[3];
        private readonly Vector2[] _globalPressPosition = new Vector2[3];
        // Temporary list used to remove invalid (destroyed) inventories from dictionaries
        private readonly List<UniversalInventory> _staleInventories = new List<UniversalInventory>();

        // Hold drag settings and state
        [Header("Hold Drag")]
        [SerializeField, Tooltip("Progressive hold-drag settings. Null = feature disabled.")]
        private HoldDragSettings _holdDragSettings;

        private UniversalInventory _holdCountInventory;
        private BaseSlot _holdCountBaseSlot;
        private int _holdCountLastAmount = -1;

        public HoldDragSettings HoldDragSettings => _holdDragSettings;

        /// <summary>
        /// Fired every frame while hold count is active.
        /// Parameters: (slot, current amount, maximum stack amount).
        /// </summary>
        public static event Action<BaseSlot, int, int> OnHoldPreviewChanged;

        /// <summary>
        /// Fired when hold count ends (drag started or the button was released).
        /// </summary>
        public static event Action OnHoldPreviewEnded;

        protected override void Init()
        {
            base.Init();
#if UDND_INPUT_SYSTEM
            RebindDefaultProfileInputActions();
#endif
        }

        protected override void DeInit()
        {
#if UDND_INPUT_SYSTEM
            UnbindDefaultProfileInputActions();
#endif
            base.DeInit();
        }

        private void Update()
        {
#if !(UDND_INPUT_SYSTEM && ENABLE_INPUT_SYSTEM)
            EnsureLegacyEventSystemReady();
#endif
            ProcessGlobalPointerUpsWhileDragging();
            PollKeyBindings();
            TickHoldPreview();
        }
        private void LateUpdate()
        {
            ProcessUnhandledGlobalPointerEvents();

            _handledThisFrame.Clear();
            _pointerUpHandledThisFrame.Clear();
            _pointerDownHandledThisFrame.Clear();
            CleanupStaleInventories();

            if (_autoMaintainFocus)
                MaintainNavigationFocus();
        }

        public void RegisterExtraBinder(InventoryExtraInteractionBinder extraBinder)
        {
            if (extraBinder == null || extraBinder.Inventory == null)
                return;

            var inventory = extraBinder.Inventory;
            _overridesByInventory[inventory] = extraBinder;

#if UDND_INPUT_SYSTEM
            RebindExtraInputActions(inventory, extraBinder);
#endif
        }

        public void UnregisterExtraBinder(InventoryExtraInteractionBinder extraBinder)
        {
            if (extraBinder == null || extraBinder.Inventory == null)
                return;

            var inventory = extraBinder.Inventory;
            if (_overridesByInventory.TryGetValue(inventory, out var existing) && existing == extraBinder)
                _overridesByInventory.Remove(inventory);

#if UDND_INPUT_SYSTEM
            UnbindExtraInputActions(inventory);
#endif
        }

#if UDND_INPUT_SYSTEM
        public bool TryRouteInventoryAction(
            UniversalInventory inventory,
            InventoryActionBase action,
            InputAction.CallbackContext callbackContext)
        {
            if (inventory == null || action == null)
                return false;

            var key = new IntentDedupKey((int)callbackContext.phase, inventory, action);
            if (!_handledThisFrame.Add(key))
                return true;

            var state = GetOrCreateState(inventory);
            var activeSlot = state.FocusedSlot
                             ?? state.HoveredSlot
                             ?? inventory.ResolveAutoTransferSlot();
            if (!action.CanExecute(inventory, activeSlot))
            {
                return true;
            }

            _ = action.Execute(inventory, activeSlot);

            return true;
        }
#endif

        public FocusSource ResolveActiveFocusSource(UniversalInventory inventory)
        {
            if (inventory == null)
                return FocusSource.None;

            var state = GetOrCreateState(inventory);
            if (state.ActiveFocusSource != FocusSource.None)
                return state.ActiveFocusSource;

            return FocusSource.Gamepad;
        }

        public bool TryGetCurrentNavigationAnchor(out GameObject selectedObject)
        {
            selectedObject = null;
            if (!InputModalityTracker.IsNavigationModeActive)
                return false;

            var es = EventSystem.current;
            if (es == null || es.currentSelectedGameObject == null || !es.currentSelectedGameObject.activeInHierarchy)
                return false;

            selectedObject = es.currentSelectedGameObject;
            return true;
        }

        public bool IsInventoryActive(UniversalInventory inventory)
            => inventory != null && ReferenceEquals(_activeInventory, inventory);

        public BaseSlot ResolveQuickActionSlot(UniversalInventory inventory, bool requireActiveInventory = true)
        {
            if (inventory == null)
                return null;

            if (requireActiveInventory && !IsInventoryActive(inventory))
                return null;

            var state = GetOrCreateState(inventory);
            return state.FocusedSlot
                   ?? state.HoveredSlot
                   ?? inventory.ResolveAutoTransferSlot();
        }

        public void RoutePointerEnter(SlotInputAdapter adapter, PointerEventData eventData)
        {
            if (!TryGetInventory(adapter, out var inventory) || adapter?.BaseSlot == null)
                return;

            MarkInventoryActive(inventory);
            var state = GetOrCreateState(inventory);
            state.HoveredAdapter = adapter;
            state.HoveredSlot = adapter.BaseSlot;
            state.ActiveFocusSource = FocusSource.Mouse;

            if (DragAndDropManager.AutoCreateInstance.IsDragging && adapter.BaseSlot.IsInteractable)
                DragAndDropManager.AutoCreateInstance.PushDropTarget(adapter);
        }

        public void RoutePointerExit(SlotInputAdapter adapter, PointerEventData eventData)
        {
            if (!TryGetInventory(adapter, out var inventory) || adapter?.BaseSlot == null)
                return;

            var state = GetOrCreateState(inventory);
            if (ReferenceEquals(state.HoveredSlot, adapter.BaseSlot))
            {
                state.HoveredAdapter = null;
                state.HoveredSlot = null;

                if (state.FocusedSlot == null)
                    state.ActiveFocusSource = FocusSource.None;
            }

            TryClearActiveInventory(inventory);

            if (DragAndDropManager.AutoCreateInstance.IsDragging)
                DragAndDropManager.AutoCreateInstance.PopDropTarget(adapter);
        }

        public void RoutePointerDown(SlotInputAdapter adapter, PointerEventData eventData)
        {
            if (!TryGetInventory(adapter, out var inventory) || eventData == null)
                return;

            if (InputModalityTracker.IsNavigationModeActive)
            {
                var es = EventSystem.current;
                if (es != null && es.currentSelectedGameObject != null)
                    es.SetSelectedGameObject(null);
            }

            MarkInventoryActive(inventory);
            var state = GetOrCreateState(inventory);
            state.FocusedAdapter = adapter;
            state.FocusedSlot = adapter.BaseSlot;
            state.ActiveFocusSource = FocusSource.Mouse;
            state.PressedAdapter = adapter;
            state.PressedButton = eventData.button;
            state.PressedTime = Time.unscaledTime;
            state.PressedPosition = eventData.position;

            // PointerDown should allow regular slot actions (selection, inventory ops)
            // and drag start actions. Drag completion/cancel is processed on PointerUp.
            _pointerDownHandledThisFrame.Add(eventData.button);
            var interactionSnapshot = BuildInteractionSnapshot(
                InteractionInputKind.Pointer,
                inventory,
                adapter,
                eventData,
                pointerPhase: PointerTriggerPhase.Down,
                keyPhase: null,
                inputActionPhase: null,
                nativeInputContext: null);
            ExecutePointerBindings(interactionSnapshot);
        }

        public void RoutePointerUp(SlotInputAdapter adapter, PointerEventData eventData)
        {
            StopHoldCount();

            if (eventData == null)
                return;

            if (!TryResolveInventoryForPointerUp(adapter, out var inventory))
                return;

            var state = GetOrCreateState(inventory);
            bool releaseOfPressedButton = state.PressedAdapter == adapter && state.PressedButton == eventData.button;
            bool isDraggingNow = DragAndDropManager.AutoCreateInstance.IsDragging;
            bool shouldProcess = (releaseOfPressedButton && state.PressedAdapter != null) || isDraggingNow;

            if (shouldProcess)
            {
                _pointerUpHandledThisFrame.Add(eventData.button);
                if (isDraggingNow)
                {
                    var upSnapshot = BuildInteractionSnapshot(
                        InteractionInputKind.Pointer,
                        inventory,
                        adapter,
                        eventData,
                        pointerPhase: PointerTriggerPhase.Up,
                        keyPhase: null,
                        inputActionPhase: null,
                        nativeInputContext: null);
                    ExecutePointerBindings(upSnapshot);
                }
                else if (releaseOfPressedButton && state.PressedAdapter != null)
                {
                    bool handledClick = false;
                    if (TryResolveClickPhase(state, eventData, out var clickPhase))
                    {
                        var clickSnapshot = BuildInteractionSnapshot(
                            InteractionInputKind.Pointer,
                            inventory,
                            adapter,
                            eventData,
                            pointerPhase: clickPhase,
                            keyPhase: null,
                            inputActionPhase: null,
                            nativeInputContext: null);
                        handledClick = ExecutePointerBindings(clickSnapshot);

                        if (!handledClick && clickPhase != PointerTriggerPhase.Click)
                        {
                            var plainClickSnapshot = BuildInteractionSnapshot(
                                InteractionInputKind.Pointer,
                                inventory,
                                adapter,
                                eventData,
                                pointerPhase: PointerTriggerPhase.Click,
                                keyPhase: null,
                                inputActionPhase: null,
                                nativeInputContext: null);
                            handledClick = ExecutePointerBindings(plainClickSnapshot);
                        }
                    }

                    if (!handledClick)
                    {
                        var fallbackUpSnapshot = BuildInteractionSnapshot(
                            InteractionInputKind.Pointer,
                            inventory,
                            adapter,
                            eventData,
                            pointerPhase: PointerTriggerPhase.Up,
                            keyPhase: null,
                            inputActionPhase: null,
                            nativeInputContext: null);
                        ExecutePointerBindings(fallbackUpSnapshot);
                    }
                }
            }

            if (releaseOfPressedButton)
                state.PressedAdapter = null;
        }

        public void RouteBeginDrag(SlotInputAdapter adapter, PointerEventData eventData)
        {
            if (DragAndDropManager.AutoCreateInstance.IsDragging)
                return;

            StopHoldCount();

            if (!TryGetInventory(adapter, out var inventory))
                return;

            var interactionSnapshot = BuildInteractionSnapshot(
                InteractionInputKind.Pointer,
                inventory,
                adapter,
                eventData,
                pointerPhase: PointerTriggerPhase.BeginDrag,
                keyPhase: null,
                inputActionPhase: null,
                nativeInputContext: null);
            ExecutePointerBindings(interactionSnapshot);
        }

        public void RouteFocusEnter(SlotInputAdapter adapter, FocusSource source)
        {
            if (!TryGetInventory(adapter, out var inventory) || adapter?.BaseSlot == null)
                return;

            MarkInventoryActive(inventory);
            var state = GetOrCreateState(inventory);
            state.FocusedAdapter = adapter;
            state.FocusedSlot = adapter.BaseSlot;
            state.ActiveFocusSource = source;

            if (DragAndDropManager.AutoCreateInstance.IsDragging && adapter.BaseSlot.IsInteractable)
                DragAndDropManager.AutoCreateInstance.PushDropTarget(adapter);
        }

        public void RouteDropAreaFocusEnter(InventoryDropArea dropArea, FocusSource source)
        {
            if (dropArea == null || dropArea.Inventory == null)
                return;

            var inventory = dropArea.Inventory;
            MarkInventoryActive(inventory);
            var state = GetOrCreateState(inventory);
            state.FocusedDropArea = dropArea;
            state.ActiveFocusSource = source;

            if (DragAndDropManager.AutoCreateInstance.IsDragging)
                dropArea.TryActivateAsFocusedTarget();
        }

        public void RouteFocusExit(SlotInputAdapter adapter, FocusSource source)
        {
            if (!TryGetInventory(adapter, out var inventory) || adapter?.BaseSlot == null)
                return;

            var state = GetOrCreateState(inventory);
            if (state.ActiveFocusSource == source && ReferenceEquals(state.FocusedSlot, adapter.BaseSlot))
            {
                state.FocusedAdapter = null;
                state.FocusedSlot = null;
                state.ActiveFocusSource = FocusSource.None;
            }

            TryClearActiveInventory(inventory);

            if (DragAndDropManager.AutoCreateInstance.IsDragging)
                DragAndDropManager.AutoCreateInstance.PopDropTarget(adapter);
        }

        public void RouteDropAreaFocusExit(InventoryDropArea dropArea, FocusSource source)
        {
            if (dropArea == null || dropArea.Inventory == null)
                return;

            var inventory = dropArea.Inventory;
            var state = GetOrCreateState(inventory);
            if (state.ActiveFocusSource == source && ReferenceEquals(state.FocusedDropArea, dropArea))
            {
                state.FocusedDropArea = null;
                if (state.FocusedSlot == null)
                    state.ActiveFocusSource = FocusSource.None;
            }

            TryClearActiveInventory(inventory);

            if (DragAndDropManager.AutoCreateInstance.IsDragging)
                DragAndDropManager.AutoCreateInstance.PopDropTarget(dropArea);
        }

        private bool ExecutePointerBindings(RuntimeInteractionSnapshot interactionSnapshot)
        {
            if (interactionSnapshot?.PointerEventData == null)
                return false;

            var bindings = ResolvePointerBindings(interactionSnapshot.Inventory);

            int executedActions = 0;
            bool isDragging = DragAndDropManager.AutoCreateInstance.IsDragging;
            for (int i = 0; i < bindings.Count; i++)
            {
                var binding = bindings[i];
                if (binding == null || !binding.IsValid() || !binding.Matches(interactionSnapshot.PointerEventData, interactionSnapshot.PointerPhase ?? PointerTriggerPhase.Any))
                    continue;

                if (!isDragging && binding.Action.IsDragOnlyBinding())
                    continue;

                if (!interactionSnapshot.HasConcreteTarget && !binding.Action.AllowOutOfSlot())
                    continue;

                if (binding.Action.CanExecute(interactionSnapshot))
                {
                    _ = binding.Action.Execute(interactionSnapshot);
                    executedActions++;
                }
            }

            interactionSnapshot.PointerEventData.Use();
            return executedActions > 0;
        }

        private void PollKeyBindings()
        {
            var inventory = _activeInventory;
            var bindings = ResolveKeyBindings(inventory);
            var baseSnapshot = BuildInteractionSnapshot(
                InteractionInputKind.Key,
                inventory,
                adapter: null,
                pointerEventData: null,
                pointerPhase: null,
                keyPhase: null,
                inputActionPhase: null,
                nativeInputContext: null);
            bool isDragging = DragAndDropManager.AutoCreateInstance.IsDragging;
            for (int i = 0; i < bindings.Count; i++)
            {
                var binding = bindings[i];
                if (binding == null || !binding.IsValid() || !binding.IsTriggered())
                    continue;

                if (!isDragging && binding.Action.IsDragOnlyBinding())
                    continue;

                var interactionSnapshot = baseSnapshot.WithKeyPhase(binding.TriggerPhase);

                if (!interactionSnapshot.HasConcreteTarget && !binding.Action.AllowOutOfSlot())
                    continue;

                if (binding.Action.CanExecute(interactionSnapshot))
                {
                    _ = binding.Action.Execute(interactionSnapshot);
                }
            }
        }

#if UDND_INPUT_SYSTEM
        private void HandleExtraInputAction(UniversalInventory inventory, InputAction.CallbackContext context)
        {
            if (inventory == null)
                return;

            // InputAction is global: subscriptions exist on every inventory with an ExtraBinder.
            // Handle only the active inventory, otherwise Submit on one inventory
            // would trigger actions on all the others.
            if (!ReferenceEquals(inventory, _activeInventory))
                return;

            var state = GetOrCreateState(inventory);
            var adapter = state.FocusedAdapter
                          ?? ResolveAdapterFromSlot(state.FocusedSlot)
                          ?? state.HoveredAdapter
                          ?? ResolveAdapterFromSlot(state.HoveredSlot);

            var interactionSnapshot = BuildInteractionSnapshot(
                InteractionInputKind.InputAction,
                inventory,
                adapter,
                pointerEventData: null,
                pointerPhase: null,
                keyPhase: null,
                inputActionPhase: TriggerPhaseFromCallbackContext(context),
                nativeInputContext: context);

            ExecuteInputActionBindings(ResolveInputActionBindings(inventory), interactionSnapshot, context);
        }

        private void HandleDefaultProfileInputAction(InputAction.CallbackContext context)
        {
            if (_activeInventory != null)
            {
                HandleExtraInputAction(_activeInventory, context);
                return;
            }

            if (DefaultBindingsProfile == null)
                return;

            var interactionSnapshot = BuildInteractionSnapshot(
                InteractionInputKind.InputAction,
                inventory: null,
                adapter: null,
                pointerEventData: null,
                pointerPhase: null,
                keyPhase: null,
                inputActionPhase: TriggerPhaseFromCallbackContext(context),
                nativeInputContext: context);

            ExecuteInputActionBindings(DefaultBindingsProfile.InputActionBindingsRuntime, interactionSnapshot, context);
        }

        private void ExecuteInputActionBindings(
            IReadOnlyList<InputActionBinding> bindings,
            RuntimeInteractionSnapshot interactionSnapshot,
            InputAction.CallbackContext context)
        {
            var action = context.action;
            if (action == null || bindings == null)
                return;

            bool isDragging = DragAndDropManager.AutoCreateInstance.IsDragging;
            for (int i = 0; i < bindings.Count; i++)
            {
                var binding = bindings[i];
                if (binding == null || !binding.IsValid() || !binding.Matches(context))
                    continue;

                if (!isDragging && binding.Action.IsDragOnlyBinding())
                    continue;

                if (!interactionSnapshot.HasConcreteTarget && !binding.Action.AllowOutOfSlot())
                    continue;

                if (binding.Action.CanExecute(interactionSnapshot))
                    _ = binding.Action.Execute(interactionSnapshot);
            }
        }

        private void RebindDefaultProfileInputActions()
        {
            UnbindDefaultProfileInputActions();

            if (DefaultBindingsProfile == null)
                return;

            var bindings = DefaultBindingsProfile.InputActionBindingsRuntime;
            for (int i = 0; i < bindings.Count; i++)
            {
                var binding = bindings[i];
                if (binding == null || !binding.IsValid())
                    continue;

                var inputAction = binding.ActionReference.action;
                if (inputAction == null)
                    continue;

                Action<InputAction.CallbackContext> handler = HandleDefaultProfileInputAction;
                switch (binding.TriggerPhase)
                {
                    case TriggerPhaseEnum.Started:
                        inputAction.started += handler;
                        break;
                    case TriggerPhaseEnum.Performed:
                        inputAction.performed += handler;
                        break;
                    case TriggerPhaseEnum.Canceled:
                        inputAction.canceled += handler;
                        break;
                }
                _defaultProfileSubscriptions.Add(new InputActionSubscription(inputAction, handler, binding.TriggerPhase));
            }
        }

        private void UnbindDefaultProfileInputActions()
        {
            for (int i = 0; i < _defaultProfileSubscriptions.Count; i++)
            {
                var sub = _defaultProfileSubscriptions[i];
                if (sub.Action == null)
                    continue;

                switch (sub.Phase)
                {
                    case TriggerPhaseEnum.Started:
                        sub.Action.started -= sub.Handler;
                        break;
                    case TriggerPhaseEnum.Performed:
                        sub.Action.performed -= sub.Handler;
                        break;
                    case TriggerPhaseEnum.Canceled:
                        sub.Action.canceled -= sub.Handler;
                        break;
                }
            }

            _defaultProfileSubscriptions.Clear();
        }

        private void RebindExtraInputActions(UniversalInventory inventory, InventoryExtraInteractionBinder binder)
        {
            UnbindExtraInputActions(inventory);

            if (binder == null) return;

            // Subscribe only local InputActions (local + profile).
            // Global subscriptions are handled by HandleDefaultProfileInputAction.
            var bindings = binder.LocalInputActionBindings;
            var subs = new List<InputActionSubscription>();
            _actionSubscriptionsByInventory[inventory] = subs;

            for (int i = 0; i < bindings.Count; i++)
            {
                var binding = bindings[i];
                if (binding == null || !binding.IsValid())
                    continue;

                var inputAction = binding.ActionReference.action;
                if (inputAction == null)
                    continue;

                Action<InputAction.CallbackContext> handler = ctx => HandleExtraInputAction(inventory, ctx);
                switch (binding.TriggerPhase)
                {
                    case TriggerPhaseEnum.Started:
                        inputAction.started += handler;
                        break;
                    case TriggerPhaseEnum.Performed:
                        inputAction.performed += handler;
                        break;
                    case TriggerPhaseEnum.Canceled:
                        inputAction.canceled += handler;
                        break;
                }
                subs.Add(new InputActionSubscription(inputAction, handler, binding.TriggerPhase));
            }
        }

        private void UnbindExtraInputActions(UniversalInventory inventory)
        {
            if (inventory == null)
                return;

            if (!_actionSubscriptionsByInventory.TryGetValue(inventory, out var subs))
                return;

            for (int i = 0; i < subs.Count; i++)
            {
                var sub = subs[i];
                if (sub.Action == null)
                    continue;

                switch (sub.Phase)
                {
                    case TriggerPhaseEnum.Started:
                        sub.Action.started -= sub.Handler;
                        break;
                    case TriggerPhaseEnum.Performed:
                        sub.Action.performed -= sub.Handler;
                        break;
                    case TriggerPhaseEnum.Canceled:
                        sub.Action.canceled -= sub.Handler;
                        break;
                }
            }

            _actionSubscriptionsByInventory.Remove(inventory);
        }
#endif

        private IReadOnlyList<PointerBinding> ResolvePointerBindings(UniversalInventory inventory)
        {
            if (inventory != null && _overridesByInventory.TryGetValue(inventory, out var overrideBinder) && overrideBinder != null)
            {
                return overrideBinder.PointerBindingsResolved;
            }

            return DefaultBindingsProfile != null
                ? DefaultBindingsProfile.PointerBindingsRuntime
                : Array.Empty<PointerBinding>();
        }

        private IReadOnlyList<KeyBinding> ResolveKeyBindings(UniversalInventory inventory)
        {
            if (inventory != null && _overridesByInventory.TryGetValue(inventory, out var overrideBinder) && overrideBinder != null)
            {
                return overrideBinder.KeyBindingsResolved;
            }

            return DefaultBindingsProfile != null
                ? DefaultBindingsProfile.KeyBindingsRuntime
                : Array.Empty<KeyBinding>();
        }

#if UDND_INPUT_SYSTEM
        private static TriggerPhaseEnum TriggerPhaseFromCallbackContext(InputAction.CallbackContext context)
        {
            switch (context.phase)
            {
                case InputActionPhase.Started:
                    return TriggerPhaseEnum.Started;
                case InputActionPhase.Canceled:
                    return TriggerPhaseEnum.Canceled;
                case InputActionPhase.Performed:
                default:
                    return TriggerPhaseEnum.Performed;
            }
        }

        private IReadOnlyList<InputActionBinding> ResolveInputActionBindings(UniversalInventory inventory)
        {
            if (inventory != null && _overridesByInventory.TryGetValue(inventory, out var overrideBinder) && overrideBinder != null)
            {
                return overrideBinder.InputActionBindingsResolved;
            }

            return DefaultBindingsProfile != null
                ? DefaultBindingsProfile.InputActionBindingsRuntime
                : Array.Empty<InputActionBinding>();
        }
#endif

        public float GetPressedTime(UniversalInventory inventory)
        {
            if (inventory != null && _runtimeStateByInventory.TryGetValue(inventory, out var state))
                return state.PressedTime;
            return -1f;
        }

        public float GetHoldDuration(UniversalInventory inventory)
        {
            float pressedTime = GetPressedTime(inventory);
            return pressedTime >= 0f ? Mathf.Max(0f, Time.unscaledTime - pressedTime) : 0f;
        }

        /// <summary>
        /// Start hold counting. Called from StartHoldCountAction (Down phase).
        /// </summary>
        public void BeginHoldCount(UniversalInventory inventory, BaseSlot baseSlot)
        {
            if (_holdDragSettings == null || baseSlot == null || baseSlot.IsEmpty)
                return;

            _holdCountInventory = inventory;
            _holdCountBaseSlot = baseSlot;
            _holdCountLastAmount = -1;
        }

        /// <summary>
        /// Get the current accumulated amount for the slot.
        /// Called from StartHoldDragAction (BeginDrag phase).
        /// </summary>
        public int GetHoldDragAmount(BaseSlot baseSlot)
        {
            if (_holdDragSettings == null || _holdCountBaseSlot == null || baseSlot == null)
                return baseSlot != null && !baseSlot.IsEmpty ? baseSlot.Stack.Count : 0;

            if (!ReferenceEquals(_holdCountBaseSlot, baseSlot))
                return baseSlot.IsEmpty ? 0 : baseSlot.Stack.Count;

            float holdDuration = GetHoldDuration(_holdCountInventory);
            return _holdDragSettings.ComputeAmount(holdDuration, baseSlot.Stack.Count);
        }

        private void TickHoldPreview()
        {
            if (_holdCountBaseSlot == null || _holdDragSettings == null)
                return;

            if (_holdCountBaseSlot.IsEmpty || DragAndDropManager.AutoCreateInstance.IsDragging)
            {
                StopHoldCount();
                return;
            }

            float holdDuration = GetHoldDuration(_holdCountInventory);
            int amount = _holdDragSettings.ComputeAmount(holdDuration, _holdCountBaseSlot.Stack.Count);

            if (amount != _holdCountLastAmount)
            {
                _holdCountLastAmount = amount;
                OnHoldPreviewChanged?.Invoke(_holdCountBaseSlot, amount, _holdCountBaseSlot.Stack.Count);
            }
        }

        private void StopHoldCount()
        {
            if (_holdCountBaseSlot == null)
                return;

            _holdCountInventory = null;
            _holdCountBaseSlot = null;
            _holdCountLastAmount = -1;
            OnHoldPreviewEnded?.Invoke();
        }

        private RuntimeState GetOrCreateState(UniversalInventory inventory)
        {
            if (!_runtimeStateByInventory.TryGetValue(inventory, out var state) || state == null)
            {
                state = new RuntimeState();
                _runtimeStateByInventory[inventory] = state;
            }

            return state;
        }

        private void MarkInventoryActive(UniversalInventory inventory)
        {
            if (inventory != null)
                _activeInventory = inventory;
        }

        private void TryClearActiveInventory(UniversalInventory inventory)
        {
            if (!ReferenceEquals(_activeInventory, inventory))
                return;

            var state = GetOrCreateState(inventory);
            if (state.HoveredSlot == null && state.FocusedSlot == null && state.FocusedDropArea == null)
                _activeInventory = null;
        }

        private void CleanupStaleInventories()
        {
            if (_runtimeStateByInventory.Count == 0 && _overridesByInventory.Count == 0
#if UDND_INPUT_SYSTEM
                && _actionSubscriptionsByInventory.Count == 0
#endif
               )
                return;

            _staleInventories.Clear();

            foreach (var kv in _runtimeStateByInventory)
            {
                if (kv.Key == null)
                    _staleInventories.Add(kv.Key);
            }

            foreach (var kv in _overridesByInventory)
            {
                if (kv.Key == null && !_staleInventories.Contains(kv.Key))
                    _staleInventories.Add(kv.Key);
            }

#if UDND_INPUT_SYSTEM
            foreach (var kv in _actionSubscriptionsByInventory)
            {
                if (kv.Key == null && !_staleInventories.Contains(kv.Key))
                    _staleInventories.Add(kv.Key);
            }
#endif

            for (int i = 0; i < _staleInventories.Count; i++)
            {
                var stale = _staleInventories[i];
                if (stale != null)
                    continue;

                _runtimeStateByInventory.Remove(stale);
                _overridesByInventory.Remove(stale);
#if UDND_INPUT_SYSTEM
                _actionSubscriptionsByInventory.Remove(stale);
#endif

                if (ReferenceEquals(_activeInventory, stale))
                    _activeInventory = null;
            }
        }

        private static SlotInputAdapter ResolveAdapterFromSlot(BaseSlot baseSlot)
        {
            if (baseSlot == null) return null;
            return baseSlot.GetComponent<SlotInputAdapter>();
        }

        private RuntimeInteractionSnapshot BuildInteractionSnapshot(
            InteractionInputKind inputKind,
            UniversalInventory inventory,
            SlotInputAdapter adapter,
            PointerEventData pointerEventData,
            PointerTriggerPhase? pointerPhase,
            KeyTriggerPhase? keyPhase,
            TriggerPhaseEnum? inputActionPhase,
            object nativeInputContext)
        {
            RuntimeState state = inventory != null ? GetOrCreateState(inventory) : null;
            var resolvedAdapter = adapter
                                  ?? state?.FocusedAdapter
                                  ?? ResolveAdapterFromSlot(state?.FocusedSlot)
                                  ?? state?.HoveredAdapter
                                  ?? ResolveAdapterFromSlot(state?.HoveredSlot);

            var resolvedBaseSlot = resolvedAdapter?.BaseSlot
                                   ?? state?.FocusedSlot
                                   ?? state?.HoveredSlot;

            return new RuntimeInteractionSnapshot(
                inputKind: inputKind,
                inventory: inventory,
                activeSlot: resolvedBaseSlot,
                focusedSlot: state?.FocusedSlot,
                hoveredSlot: state?.HoveredSlot,
                pressedSlot: state?.PressedAdapter?.BaseSlot,
                dropArea: state?.FocusedDropArea,
                activeFocusSource: state?.ActiveFocusSource ?? FocusSource.None,
                pointerEventData: pointerEventData,
                pointerPhase: pointerPhase,
                keyPhase: keyPhase,
                inputActionPhase: inputActionPhase,
                nativeInputContext: nativeInputContext,
                isDragging: DragAndDropManager.IsInstanceExist && DragAndDropManager.AutoCreateInstance.IsDragging,
                currentDragContext: DragAndDropManager.IsInstanceExist ? DragAndDropManager.AutoCreateInstance.CurrentContext : null,
                selection: SelectionManager.IsInstanceExist ? SelectionManager.AutoCreateInstance.CurrentContext : SelectionContext.Empty);
        }

        private bool TryGetInventory(SlotInputAdapter adapter, out UniversalInventory inventory)
        {
            inventory = null;
            if (adapter?.BaseSlot?.Inventory is UniversalInventory universalInventory)
            {
                inventory = universalInventory;
                return true;
            }
            return false;
        }

        private bool TryResolveInventoryForPointerUp(SlotInputAdapter adapter, out UniversalInventory inventory)
        {
            if (TryGetInventory(adapter, out inventory))
                return true;

            if (!DragAndDropManager.IsInstanceExist || !DragAndDropManager.AutoCreateInstance.IsDragging)
                return false;

            var context = DragAndDropManager.AutoCreateInstance.CurrentContext;
            if (context != null && context.Entries.Count > 0)
            {
                inventory = context.Entries[0].SourceInventory as UniversalInventory;
                return inventory != null;
            }

            return false;
        }

        private bool TryResolveClickPhase(RuntimeState state, PointerEventData eventData, out PointerTriggerPhase phase)
        {
            phase = PointerTriggerPhase.Click;
            if (state == null || eventData == null)
                return false;

            var pressPosition = state.PressedPosition;
            float sqrDistance = (eventData.position - pressPosition).sqrMagnitude;
            float sqrTolerance = _clickMoveTolerancePixels * _clickMoveTolerancePixels;
            if (sqrDistance > sqrTolerance)
                return false;

            float pressDuration = Mathf.Max(0f, Time.unscaledTime - state.PressedTime);
            phase = pressDuration >= _longClickThresholdSeconds
                ? PointerTriggerPhase.ClickLong
                : PointerTriggerPhase.ClickShort;

            return true;
        }

        private void ProcessGlobalPointerUpsWhileDragging()
        {
            if (!DragAndDropManager.IsInstanceExist || !DragAndDropManager.AutoCreateInstance.IsDragging)
                return;

            ProcessGlobalPointerUp(PointerEventData.InputButton.Left);
            ProcessGlobalPointerUp(PointerEventData.InputButton.Right);
            ProcessGlobalPointerUp(PointerEventData.InputButton.Middle);
        }

        private void ProcessGlobalPointerUp(PointerEventData.InputButton button)
        {
            if (_pointerUpHandledThisFrame.Contains(button))
                return;

            if (!WasPointerButtonReleasedThisFrame(button))
                return;

            if (!TryResolveInventoryForGlobalPointerUp(out var inventory))
                return;

            var state = GetOrCreateState(inventory);
            var adapter = state.PressedAdapter
                          ?? state.FocusedAdapter
                          ?? ResolveAdapterFromSlot(state.FocusedSlot)
                          ?? state.HoveredAdapter
                          ?? ResolveAdapterFromSlot(state.HoveredSlot);
            var eventData = new PointerEventData(EventSystem.current) { button = button };

            _pointerUpHandledThisFrame.Add(button);
            var interactionSnapshot = BuildInteractionSnapshot(
                InteractionInputKind.Pointer,
                inventory,
                adapter,
                eventData,
                pointerPhase: PointerTriggerPhase.Up,
                keyPhase: null,
                inputActionPhase: null,
                nativeInputContext: null);
            ExecutePointerBindings(interactionSnapshot);
        }

        private bool TryResolveInventoryForGlobalPointerUp(out UniversalInventory inventory)
        {
            inventory = null;

            if (!DragAndDropManager.IsInstanceExist || !DragAndDropManager.AutoCreateInstance.IsDragging)
                return false;

            var context = DragAndDropManager.AutoCreateInstance.CurrentContext;
            if (context != null && context.Entries.Count > 0)
            {
                inventory = context.Entries[0].SourceInventory as UniversalInventory;
                return inventory != null;
            }

            return false;
        }

        private static bool WasPointerButtonReleasedThisFrame(PointerEventData.InputButton button)
        {
#if UDND_INPUT_SYSTEM && ENABLE_INPUT_SYSTEM
            var mouse = Mouse.current;
            if (mouse == null)
                return false;

            switch (button)
            {
                case PointerEventData.InputButton.Left:
                    return mouse.leftButton.wasReleasedThisFrame;
                case PointerEventData.InputButton.Right:
                    return mouse.rightButton.wasReleasedThisFrame;
                case PointerEventData.InputButton.Middle:
                    return mouse.middleButton.wasReleasedThisFrame;
                default:
                    return false;
            }
#else
            switch (button)
            {
                case PointerEventData.InputButton.Left:
                    return Input.GetMouseButtonUp(0);
                case PointerEventData.InputButton.Right:
                    return Input.GetMouseButtonUp(1);
                case PointerEventData.InputButton.Middle:
                    return Input.GetMouseButtonUp(2);
                default:
                    return false;
            }
#endif
        }

#if !(UDND_INPUT_SYSTEM && ENABLE_INPUT_SYSTEM)
        private void EnsureLegacyEventSystemReady()
        {
            if (_legacyEventSystemValidated)
                return;

            var eventSystem = EventSystem.current;
            if (eventSystem == null)
                eventSystem = FindFirstObjectByType<EventSystem>();

            if (eventSystem == null)
            {
                var eventSystemObject = new GameObject("EventSystem");
                eventSystem = eventSystemObject.AddComponent<EventSystem>();
                eventSystemObject.AddComponent<StandaloneInputModule>();
                Debug.LogWarning(
                    "[InputEventRouter] No EventSystem was found. Created EventSystem with StandaloneInputModule for legacy input compatibility.");
                _legacyEventSystemValidated = true;
                return;
            }

            if (eventSystem.GetComponent<BaseInputModule>() == null)
            {
                eventSystem.gameObject.AddComponent<StandaloneInputModule>();
                Debug.LogWarning(
                    "[InputEventRouter] EventSystem has no active input module. Added StandaloneInputModule because Input System support is disabled.");
            }

            _legacyEventSystemValidated = true;
        }
#endif

        private void ProcessUnhandledGlobalPointerEvents()
        {
            // During drag, global pointer-up events are handled in ProcessGlobalPointerUpsWhileDragging
            if (DragAndDropManager.IsInstanceExist && DragAndDropManager.AutoCreateInstance.IsDragging)
                return;

#if UDND_INPUT_SYSTEM && ENABLE_INPUT_SYSTEM
            var mouse = Mouse.current;
            if (mouse == null)
                return;

            TrackGlobalPressState(mouse);

            ProcessUnhandledGlobalRelease(mouse.leftButton, PointerEventData.InputButton.Left);
            ProcessUnhandledGlobalRelease(mouse.rightButton, PointerEventData.InputButton.Right);
            ProcessUnhandledGlobalRelease(mouse.middleButton, PointerEventData.InputButton.Middle);
#else
            TrackGlobalPressStateLegacy();

            ProcessUnhandledGlobalReleaseLegacy(0, PointerEventData.InputButton.Left);
            ProcessUnhandledGlobalReleaseLegacy(1, PointerEventData.InputButton.Right);
            ProcessUnhandledGlobalReleaseLegacy(2, PointerEventData.InputButton.Middle);
#endif
        }

#if UDND_INPUT_SYSTEM && ENABLE_INPUT_SYSTEM
        private void TrackGlobalPressState(Mouse mouse)
        {
            ProcessUnhandledGlobalPress(mouse.leftButton, PointerEventData.InputButton.Left, mouse);
            ProcessUnhandledGlobalPress(mouse.rightButton, PointerEventData.InputButton.Right, mouse);
            ProcessUnhandledGlobalPress(mouse.middleButton, PointerEventData.InputButton.Middle, mouse);
        }

        private void ProcessUnhandledGlobalPress(ButtonControl button, PointerEventData.InputButton inputButton, Mouse mouse)
        {
            if (!button.wasPressedThisFrame)
                return;

            int idx = (int)inputButton;
            var mousePos = mouse.position.ReadValue();
            _globalPressTime[idx] = Time.unscaledTime;
            _globalPressPosition[idx] = mousePos;

            if (_pointerDownHandledThisFrame.Contains(inputButton))
                return;

            var es = EventSystem.current;
            if (es == null)
                return;

            _pointerDownHandledThisFrame.Add(inputButton);
            var eventData = new PointerEventData(es) { button = inputButton, position = mousePos };
            var interactionSnapshot = BuildInteractionSnapshot(
                InteractionInputKind.Pointer,
                _activeInventory,
                adapter: null,
                eventData,
                pointerPhase: PointerTriggerPhase.Down,
                keyPhase: null,
                inputActionPhase: null,
                nativeInputContext: null);
            ExecutePointerBindings(interactionSnapshot);
        }

        private void ProcessUnhandledGlobalRelease(ButtonControl button, PointerEventData.InputButton inputButton)
        {
            if (!button.wasReleasedThisFrame)
                return;

            if (_pointerUpHandledThisFrame.Contains(inputButton))
                return;

            var es = EventSystem.current;
            if (es == null)
                return;

            int idx = (int)inputButton;
            var mousePos = Mouse.current.position.ReadValue();
            var eventData = new PointerEventData(es) { button = inputButton, position = mousePos };
            _pointerUpHandledThisFrame.Add(inputButton);

            float pressDuration = Mathf.Max(0f, Time.unscaledTime - _globalPressTime[idx]);
            float sqrDist = (mousePos - _globalPressPosition[idx]).sqrMagnitude;
            float sqrTolerance = _clickMoveTolerancePixels * _clickMoveTolerancePixels;

            if (sqrDist <= sqrTolerance)
            {
                var clickPhase = pressDuration >= _longClickThresholdSeconds
                    ? PointerTriggerPhase.ClickLong
                    : PointerTriggerPhase.ClickShort;

                var clickSnapshot = BuildInteractionSnapshot(
                    InteractionInputKind.Pointer,
                    _activeInventory,
                    adapter: null,
                    eventData,
                    pointerPhase: clickPhase,
                    keyPhase: null,
                    inputActionPhase: null,
                    nativeInputContext: null);
                bool handled = ExecutePointerBindings(clickSnapshot);
                if (!handled && clickPhase != PointerTriggerPhase.Click)
                {
                    var plainClickSnapshot = BuildInteractionSnapshot(
                        InteractionInputKind.Pointer,
                        _activeInventory,
                        adapter: null,
                        eventData,
                        pointerPhase: PointerTriggerPhase.Click,
                        keyPhase: null,
                        inputActionPhase: null,
                        nativeInputContext: null);
                    handled = ExecutePointerBindings(plainClickSnapshot);
                }
                if (!handled)
                {
                    var upSnapshot = BuildInteractionSnapshot(
                        InteractionInputKind.Pointer,
                        _activeInventory,
                        adapter: null,
                        eventData,
                        pointerPhase: PointerTriggerPhase.Up,
                        keyPhase: null,
                        inputActionPhase: null,
                        nativeInputContext: null);
                    ExecutePointerBindings(upSnapshot);
                }
            }
            else
            {
                var upSnapshot = BuildInteractionSnapshot(
                    InteractionInputKind.Pointer,
                    _activeInventory,
                    adapter: null,
                    eventData,
                    pointerPhase: PointerTriggerPhase.Up,
                    keyPhase: null,
                    inputActionPhase: null,
                    nativeInputContext: null);
                ExecutePointerBindings(upSnapshot);
            }
        }
#else
        private void TrackGlobalPressStateLegacy()
        {
            ProcessUnhandledGlobalPressLegacy(0, PointerEventData.InputButton.Left);
            ProcessUnhandledGlobalPressLegacy(1, PointerEventData.InputButton.Right);
            ProcessUnhandledGlobalPressLegacy(2, PointerEventData.InputButton.Middle);
        }

        private void ProcessUnhandledGlobalPressLegacy(int mouseButton, PointerEventData.InputButton inputButton)
        {
            if (!Input.GetMouseButtonDown(mouseButton))
                return;

            int idx = (int)inputButton;
            Vector2 mousePos = Input.mousePosition;
            _globalPressTime[idx] = Time.unscaledTime;
            _globalPressPosition[idx] = mousePos;

            if (_pointerDownHandledThisFrame.Contains(inputButton))
                return;

            var es = EventSystem.current;
            if (es == null)
                return;

            _pointerDownHandledThisFrame.Add(inputButton);
            var eventData = new PointerEventData(es) { button = inputButton, position = mousePos };
            var interactionSnapshot = BuildInteractionSnapshot(
                InteractionInputKind.Pointer,
                _activeInventory,
                adapter: null,
                eventData,
                pointerPhase: PointerTriggerPhase.Down,
                keyPhase: null,
                inputActionPhase: null,
                nativeInputContext: null);
            ExecutePointerBindings(interactionSnapshot);
        }

        private void ProcessUnhandledGlobalReleaseLegacy(int mouseButton, PointerEventData.InputButton inputButton)
        {
            if (!Input.GetMouseButtonUp(mouseButton))
                return;

            if (_pointerUpHandledThisFrame.Contains(inputButton))
                return;

            var es = EventSystem.current;
            if (es == null)
                return;

            Vector2 mousePos = Input.mousePosition;
            var eventData = new PointerEventData(es) { button = inputButton, position = mousePos };
            _pointerUpHandledThisFrame.Add(inputButton);

            int idx = (int)inputButton;
            float pressDuration = Mathf.Max(0f, Time.unscaledTime - _globalPressTime[idx]);
            float sqrDist = (mousePos - _globalPressPosition[idx]).sqrMagnitude;
            float sqrTolerance = _clickMoveTolerancePixels * _clickMoveTolerancePixels;

            if (sqrDist <= sqrTolerance)
            {
                var clickPhase = pressDuration >= _longClickThresholdSeconds
                    ? PointerTriggerPhase.ClickLong
                    : PointerTriggerPhase.ClickShort;

                var clickSnapshot = BuildInteractionSnapshot(
                    InteractionInputKind.Pointer,
                    _activeInventory,
                    adapter: null,
                    eventData,
                    pointerPhase: clickPhase,
                    keyPhase: null,
                    inputActionPhase: null,
                    nativeInputContext: null);
                bool handled = ExecutePointerBindings(clickSnapshot);
                if (!handled && clickPhase != PointerTriggerPhase.Click)
                {
                    var plainClickSnapshot = BuildInteractionSnapshot(
                        InteractionInputKind.Pointer,
                        _activeInventory,
                        adapter: null,
                        eventData,
                        pointerPhase: PointerTriggerPhase.Click,
                        keyPhase: null,
                        inputActionPhase: null,
                        nativeInputContext: null);
                    handled = ExecutePointerBindings(plainClickSnapshot);
                }
                if (!handled)
                {
                    var upSnapshot = BuildInteractionSnapshot(
                        InteractionInputKind.Pointer,
                        _activeInventory,
                        adapter: null,
                        eventData,
                        pointerPhase: PointerTriggerPhase.Up,
                        keyPhase: null,
                        inputActionPhase: null,
                        nativeInputContext: null);
                    ExecutePointerBindings(upSnapshot);
                }
            }
            else
            {
                var upSnapshot = BuildInteractionSnapshot(
                    InteractionInputKind.Pointer,
                    _activeInventory,
                    adapter: null,
                    eventData,
                    pointerPhase: PointerTriggerPhase.Up,
                    keyPhase: null,
                    inputActionPhase: null,
                    nativeInputContext: null);
                ExecutePointerBindings(upSnapshot);
            }
        }
#endif

        private void MaintainNavigationFocus()
        {
            if (!InputModalityTracker.IsNavigationModeActive)
                return;

            var es = EventSystem.current;
            if (es == null)
                return;

            var selected = es.currentSelectedGameObject;
            if (selected != null && selected.activeInHierarchy)
                return;

            var target = FindBestFocusTarget();
            if (target != null)
                es.SetSelectedGameObject(target.gameObject);
        }

        private Selectable FindBestFocusTarget()
        {
            if (_activeInventory != null && _activeInventory.isActiveAndEnabled)
            {
                var target = FindFirstActiveNavigationTarget(_activeInventory);
                if (target != null)
                    return target;
            }

            var allSelectables = Selectable.allSelectablesArray;
            for (int i = 0; i < Selectable.allSelectableCount; i++)
            {
                var candidate = allSelectables[i];
                if (candidate == null || !candidate.isActiveAndEnabled)
                    continue;

                if (candidate is SlotInputAdapter || candidate is InventoryDropArea)
                    return candidate;
            }

            return null;
        }

        private static Selectable FindFirstActiveNavigationTarget(UniversalInventory inventory)
        {
            var slots = inventory.Slots;
            for (int i = 0; i < slots.Count; i++)
            {
                if (slots[i] == null) continue;
                var adapter = slots[i].GetComponent<SlotInputAdapter>();
                if (adapter != null && adapter.isActiveAndEnabled)
                    return adapter;
            }

            var dropArea = inventory.GetComponentInChildren<InventoryDropArea>(includeInactive: false);
            if (dropArea != null && dropArea.isActiveAndEnabled)
                return dropArea;

            return null;
        }

#if UDND_INPUT_SYSTEM
        private readonly struct InputActionSubscription
        {
            public InputActionSubscription(InputAction action, Action<InputAction.CallbackContext> handler, TriggerPhaseEnum phase)
            {
                Action = action;
                Handler = handler;
                Phase = phase;
            }

            public InputAction Action { get; }
            public Action<InputAction.CallbackContext> Handler { get; }
            public TriggerPhaseEnum Phase { get; }
        }
#endif

        private sealed class RuntimeState
        {
            public BaseSlot FocusedSlot;
            public SlotInputAdapter FocusedAdapter;
            public InventoryDropArea FocusedDropArea;
            public BaseSlot HoveredSlot;
            public SlotInputAdapter HoveredAdapter;
            public FocusSource ActiveFocusSource;
            public SlotInputAdapter PressedAdapter;
            public PointerEventData.InputButton PressedButton;
            public float PressedTime;
            public Vector2 PressedPosition;
        }

        private readonly struct IntentDedupKey : IEquatable<IntentDedupKey>
        {
            public IntentDedupKey(int type, object scope, object token)
            {
                Type = type;
                Scope = scope;
                Token = token;
            }

            public int Type { get; }
            public object Scope { get; }
            public object Token { get; }

            public bool Equals(IntentDedupKey other)
                => Type == other.Type
                   && ReferenceEquals(Scope, other.Scope)
                   && ReferenceEquals(Token, other.Token);

            public override bool Equals(object obj) => obj is IntentDedupKey other && Equals(other);

            public override int GetHashCode()
            {
                unchecked
                {
                    int hash = Type;
                    hash = (hash * 397) ^ System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(Scope);
                    hash = (hash * 397) ^ System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(Token);
                    return hash;
                }
            }
        }
    }
}
