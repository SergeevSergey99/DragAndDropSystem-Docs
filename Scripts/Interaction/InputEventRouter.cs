using System;
using System.Collections.Generic;
using CodeUtils;
using DragAndDropSystem.Inventories;
using DragAndDropSystem.Selection;
using DragAndDropSystem.Slots;
using DragAndDropSystem.Tools;
using DragAndDropSystem.UI;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace DragAndDropSystem.Interaction
{
    [DisallowMultipleComponent]
    public class InputEventRouter : MonoSingleton<InputEventRouter>
    {
        [field: SerializeField]
        public InteractionBindingsProfile DefaultBindingsProfile { get; private set; }

        [Header("Navigation Focus")]
        [SerializeField, Tooltip("Автоматически поддерживать фокус на слоте для gamepad/keyboard навигации")]
        private bool _autoMaintainFocus = true;

        [Header("Pointer Gestures")]
        [SerializeField, Min(0.01f)] private float _longClickThresholdSeconds = 0.35f;
        [SerializeField, Min(0f)] private float _clickMoveTolerancePixels = 8f;

        // Словарь для переопределения привязок на уровне конкретного инвентаря (например, для разных UI или режимов работы)
        private readonly Dictionary<UniversalInventory, InventoryExtraInteractionBinder> _overridesByInventory = new();
        // Словарь для хранения runtime-состояния (наведенный слот, источник фокуса, нажатые кнопки и т.д.) для каждого инвентаря
        private readonly Dictionary<UniversalInventory, RuntimeState> _runtimeStateByInventory = new();
        // Словарь для хранения подписок на InputAction для каждого инвентаря, чтобы можно было отписаться при необходимости
        private readonly Dictionary<UniversalInventory, List<InputActionSubscription>> _actionSubscriptionsByInventory = new ();
        // Подписки на InputAction из default profile (глобальные, роутятся в _activeInventory)
        private readonly List<InputActionSubscription> _defaultProfileSubscriptions = new();
        // Последний инвентарь, с которым пользователь явно взаимодействовал. Используется только для inventory-scoped quick actions.
        private UniversalInventory _activeInventory;

        // Набор для дедупликации вызовов действий в рамках одного кадра, чтобы избежать повторного срабатывания при нескольких событиях (например, PointerDown + InputAction)
        private readonly HashSet<IntentDedupKey> _handledThisFrame = new();
        // Набор для отслеживания, какие кнопки мыши уже были обработаны в рамках глобального PointerUp во время перетаскивания, чтобы не обрабатывать их несколько раз
        private readonly HashSet<PointerEventData.InputButton> _pointerUpHandledThisFrame = new ();
        // Временный список для очистки словарей от невалидных (уничтоженных) инвентарей
        private readonly List<UniversalInventory> _staleInventories = new List<UniversalInventory>();

        protected override void Init()
        {
            base.Init();
            RebindDefaultProfileInputActions();
        }

        protected override void DeInit()
        {
            UnbindDefaultProfileInputActions();
            base.DeInit();
        }

        private void Update()
        {
            ProcessGlobalPointerUpsWhileDragging();
        }
        private void LateUpdate()
        {
            _handledThisFrame.Clear();
            _pointerUpHandledThisFrame.Clear();
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

            RebindExtraInputActions(inventory, extraBinder);
        }

        public void UnregisterExtraBinder(InventoryExtraInteractionBinder extraBinder)
        {
            if (extraBinder == null || extraBinder.Inventory == null)
                return;

            var inventory = extraBinder.Inventory;
            if (_overridesByInventory.TryGetValue(inventory, out var existing) && existing == extraBinder)
                _overridesByInventory.Remove(inventory);

            UnbindExtraInputActions(inventory);
        }

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
            var activeSlot = state.FocusedSlot as UniversalSlot
                             ?? state.HoveredSlot as UniversalSlot
                             ?? inventory.ResolveAutoTransferSlot();
            if (!action.CanExecute(inventory, activeSlot))
            {
                return true;
            }

            _ = action.Execute(inventory, activeSlot);

            return true;
        }

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

        public UniversalSlot ResolveFocusedSlot(UniversalInventory inventory)
        {
            if (inventory == null)
                return null;

            var state = GetOrCreateState(inventory);
            return state.FocusedSlot as UniversalSlot;
        }

        public bool IsInventoryActive(UniversalInventory inventory)
            => inventory != null && ReferenceEquals(_activeInventory, inventory);

        public UniversalSlot ResolveQuickActionSlot(UniversalInventory inventory, bool requireActiveInventory = true)
        {
            if (inventory == null)
                return null;

            if (requireActiveInventory && !IsInventoryActive(inventory))
                return null;

            var state = GetOrCreateState(inventory);
            return state.FocusedSlot as UniversalSlot
                   ?? state.HoveredSlot as UniversalSlot
                   ?? inventory.ResolveAutoTransferSlot();
        }

        public void RoutePointerEnter(SlotInputAdapter adapter, PointerEventData eventData)
        {
            if (!TryGetInventory(adapter, out var inventory) || adapter?.Slot == null)
                return;

            MarkInventoryActive(inventory);
            var state = GetOrCreateState(inventory);
            state.HoveredAdapter = adapter;
            state.HoveredSlot = adapter.Slot;
            state.ActiveFocusSource = FocusSource.Mouse;

            if (DragAndDropManager.Instance.IsDragging && adapter.Slot.IsInteractable)
                DragAndDropManager.Instance.PushDropTarget(adapter);
        }

        public void RoutePointerExit(SlotInputAdapter adapter, PointerEventData eventData)
        {
            if (!TryGetInventory(adapter, out var inventory) || adapter?.Slot == null)
                return;

            var state = GetOrCreateState(inventory);
            if (ReferenceEquals(state.HoveredSlot, adapter.Slot))
            {
                state.HoveredAdapter = null;
                state.HoveredSlot = null;

                if (state.FocusedSlot == null)
                    state.ActiveFocusSource = FocusSource.None;
            }

            TryClearActiveInventory(inventory);

            if (DragAndDropManager.Instance.IsDragging)
                DragAndDropManager.Instance.PopDropTarget(adapter);
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
            state.FocusedSlot = adapter.Slot;
            state.ActiveFocusSource = FocusSource.Mouse;
            state.PressedAdapter = adapter;
            state.PressedButton = eventData.button;
            state.PressedTime = Time.unscaledTime;
            state.PressedPosition = eventData.position;

            if (!DragAndDropManager.Instance.IsDragging)
            {
                // PointerDown should allow regular slot actions (selection, inventory ops)
                // and drag start actions. Drag completion/cancel is processed on PointerUp.
                ExecutePointerBindings(inventory, adapter, eventData, PointerTriggerPhase.Down, dragOnly: false);
            }
        }

        public void RoutePointerUp(SlotInputAdapter adapter, PointerEventData eventData)
        {
            if (eventData == null)
                return;

            if (!TryResolveInventoryForPointerUp(adapter, out var inventory))
                return;

            var state = GetOrCreateState(inventory);
            bool releaseOfPressedButton = state.PressedAdapter == adapter && state.PressedButton == eventData.button;
            bool isDraggingNow = DragAndDropManager.Instance.IsDragging;
            bool shouldProcess = (releaseOfPressedButton && state.PressedAdapter != null) || isDraggingNow;

            if (shouldProcess)
            {
                _pointerUpHandledThisFrame.Add(eventData.button);
                if (isDraggingNow)
                {
                    ExecutePointerBindings(inventory, adapter, eventData, PointerTriggerPhase.Up, dragOnly: true);
                }
                else if (releaseOfPressedButton && state.PressedAdapter != null)
                {
                    bool handledClick = false;
                    if (TryResolveClickPhase(state, eventData, out var clickPhase))
                    {
                        handledClick = ExecutePointerBindings(
                            inventory,
                            adapter,
                            eventData,
                            clickPhase,
                            dragOnly: false);

                        if (!handledClick && clickPhase != PointerTriggerPhase.Click)
                        {
                            handledClick = ExecutePointerBindings(
                                inventory,
                                adapter,
                                eventData,
                                PointerTriggerPhase.Click,
                                dragOnly: false);
                        }
                    }

                    if (!handledClick)
                    {
                        ExecutePointerBindings(
                            inventory,
                            adapter,
                            eventData,
                            PointerTriggerPhase.Up,
                            dragOnly: false);
                    }
                }
            }

            if (releaseOfPressedButton)
                state.PressedAdapter = null;
        }

        public void RouteBeginDrag(SlotInputAdapter adapter, PointerEventData eventData)
        {
            // no-op: drag is binding-driven only
        }

        public void RouteFocusEnter(SlotInputAdapter adapter, FocusSource source)
        {
            if (!TryGetInventory(adapter, out var inventory) || adapter?.Slot == null)
                return;

            MarkInventoryActive(inventory);
            var state = GetOrCreateState(inventory);
            state.FocusedAdapter = adapter;
            state.FocusedSlot = adapter.Slot;
            state.ActiveFocusSource = source;

            if (DragAndDropManager.Instance.IsDragging && adapter.Slot.IsInteractable)
                DragAndDropManager.Instance.PushDropTarget(adapter);
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

            if (DragAndDropManager.Instance.IsDragging)
                dropArea.TryActivateAsFocusedTarget();
        }

        public void RouteFocusExit(SlotInputAdapter adapter, FocusSource source)
        {
            if (!TryGetInventory(adapter, out var inventory) || adapter?.Slot == null)
                return;

            var state = GetOrCreateState(inventory);
            if (state.ActiveFocusSource == source && ReferenceEquals(state.FocusedSlot, adapter.Slot))
            {
                state.FocusedAdapter = null;
                state.FocusedSlot = null;
                state.ActiveFocusSource = FocusSource.None;
            }

            TryClearActiveInventory(inventory);

            if (DragAndDropManager.Instance.IsDragging)
                DragAndDropManager.Instance.PopDropTarget(adapter);
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

            if (DragAndDropManager.Instance.IsDragging)
                DragAndDropManager.Instance.PopDropTarget(dropArea);
        }

        private bool ExecutePointerBindings(
            UniversalInventory inventory,
            SlotInputAdapter adapter,
            PointerEventData eventData,
            PointerTriggerPhase phase,
            bool dragOnly)
        {
            if (eventData == null) return false;

            var bindings = ResolvePointerBindings(inventory);

            if (!dragOnly && adapter?.Slot == null)
                return false;

            for (int i = 0; i < bindings.Count; i++)
            {
                var binding = bindings[i];
                if (binding == null || !binding.IsValid() || !binding.Matches(eventData, phase))
                    continue;

                if (dragOnly && !binding.Action.IsDragBinding())
                    continue;

                if (binding.Action.CanExecute(inventory, adapter, eventData))
                {
                    _ = binding.Action.Execute(inventory, adapter, eventData);
                    eventData.Use();
                    return true;
                }
            }

            return false;
        }

        private void HandleExtraInputAction(UniversalInventory inventory, InputAction.CallbackContext context)
        {
            if (inventory == null)
                return;

            // InputAction — глобальный: подписки есть на все инвентари с ExtraBinder.
            // Обрабатываем только активный инвентарь, иначе Submit на одном инвентаре
            // вызовет действия на всех остальных.
            if (!ReferenceEquals(inventory, _activeInventory))
                return;

            var bindings = ResolveInputActionBindings(inventory);

            var action = context.action;
            if (action == null)
                return;

            var state = GetOrCreateState(inventory);
            var adapter = state.FocusedAdapter
                          ?? ResolveAdapterFromSlot(state.FocusedSlot)
                          ?? state.HoveredAdapter
                          ?? ResolveAdapterFromSlot(state.HoveredSlot);

            for (int i = 0; i < bindings.Count; i++)
            {
                var binding = bindings[i];
                if (binding == null || !binding.IsValid() || !binding.Matches(context))
                    continue;

                if (binding.Action.CanExecute(inventory, adapter, null))
                {
                    _ = binding.Action.Execute(inventory, adapter, null);
                }

                return;
            }
        }

        private void HandleDefaultProfileInputAction(InputAction.CallbackContext context)
        {
            if (_activeInventory == null)
                return;

            HandleExtraInputAction(_activeInventory, context);
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

            // Подписываем только локальные InputAction (local + profile).
            // Глобальные подписки делает HandleDefaultProfileInputAction.
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

        private IReadOnlyList<PointerBinding> ResolvePointerBindings(UniversalInventory inventory)
        {
            if (_overridesByInventory.TryGetValue(inventory, out var overrideBinder) && overrideBinder != null)
            {
                return overrideBinder.PointerBindingsResolved;
            }

            return DefaultBindingsProfile != null
                ? DefaultBindingsProfile.PointerBindingsRuntime
                : Array.Empty<PointerBinding>();
        }

        private IReadOnlyList<InputActionBinding> ResolveInputActionBindings(UniversalInventory inventory)
        {
            if (_overridesByInventory.TryGetValue(inventory, out var overrideBinder) && overrideBinder != null)
            {
                return overrideBinder.InputActionBindingsResolved;
            }

            return DefaultBindingsProfile != null
                ? DefaultBindingsProfile.InputActionBindingsRuntime
                : Array.Empty<InputActionBinding>();
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
            if (_runtimeStateByInventory.Count == 0 && _overridesByInventory.Count == 0 && _actionSubscriptionsByInventory.Count == 0)
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

            foreach (var kv in _actionSubscriptionsByInventory)
            {
                if (kv.Key == null && !_staleInventories.Contains(kv.Key))
                    _staleInventories.Add(kv.Key);
            }

            for (int i = 0; i < _staleInventories.Count; i++)
            {
                var stale = _staleInventories[i];
                if (stale != null)
                    continue;

                _runtimeStateByInventory.Remove(stale);
                _overridesByInventory.Remove(stale);
                _actionSubscriptionsByInventory.Remove(stale);

                if (ReferenceEquals(_activeInventory, stale))
                    _activeInventory = null;
            }
        }

        private static SlotInputAdapter ResolveAdapterFromSlot(ISlot slot)
        {
            if (slot is UniversalSlot universalSlot)
                return universalSlot.GetComponent<SlotInputAdapter>();

            return null;
        }

        private bool TryGetInventory(SlotInputAdapter adapter, out UniversalInventory inventory)
        {
            inventory = null;
            if (adapter?.Slot?.Inventory is UniversalInventory universalInventory)
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

            if (!DragAndDropManager.IsInstanceExist || !DragAndDropManager.Instance.IsDragging)
                return false;

            var context = DragAndDropManager.Instance.CurrentContext;
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
            if (!DragAndDropManager.IsInstanceExist || !DragAndDropManager.Instance.IsDragging)
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
            ExecutePointerBindings(inventory, adapter, eventData, PointerTriggerPhase.Up, dragOnly: true);
        }

        private bool TryResolveInventoryForGlobalPointerUp(out UniversalInventory inventory)
        {
            inventory = null;

            if (!DragAndDropManager.IsInstanceExist || !DragAndDropManager.Instance.IsDragging)
                return false;

            var context = DragAndDropManager.Instance.CurrentContext;
            if (context != null && context.Entries.Count > 0)
            {
                inventory = context.Entries[0].SourceInventory as UniversalInventory;
                return inventory != null;
            }

            return false;
        }

        private static bool WasPointerButtonReleasedThisFrame(PointerEventData.InputButton button)
        {
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
        }

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
                if (slots[i] is UniversalSlot slot)
                {
                    var adapter = slot.GetComponent<SlotInputAdapter>();
                    if (adapter != null && adapter.isActiveAndEnabled)
                        return adapter;
                }
            }

            var dropArea = inventory.GetComponentInChildren<InventoryDropArea>(includeInactive: false);
            if (dropArea != null && dropArea.isActiveAndEnabled)
                return dropArea;

            return null;
        }

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

        private sealed class RuntimeState
        {
            public ISlot FocusedSlot;
            public SlotInputAdapter FocusedAdapter;
            public InventoryDropArea FocusedDropArea;
            public ISlot HoveredSlot;
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
