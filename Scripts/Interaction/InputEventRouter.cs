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
using UnityEngine.InputSystem.Controls;
using UnityEngine.UI;

namespace DragAndDropSystem.Interaction
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
        // Время и позиция нажатия для определения фазы клика (Short/Long) при глобальных pointer-событиях
        private readonly float[] _globalPressTime = new float[3];
        private readonly Vector2[] _globalPressPosition = new Vector2[3];
        // Временный список для очистки словарей от невалидных (уничтоженных) инвентарей
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
        /// Срабатывает каждый кадр пока активен hold count.
        /// Параметры: (слот, текущее количество, максимальное количество стака).
        /// </summary>
        public static event Action<BaseSlot, int, int> OnHoldPreviewChanged;

        /// <summary>
        /// Срабатывает когда hold count заканчивается (начался драг, отпустили кнопку).
        /// </summary>
        public static event Action OnHoldPreviewEnded;

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
            TickHoldPreview();
        }
        private void LateUpdate()
        {
            ProcessUnhandledGlobalPointerEvents();

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
            var activeSlot = state.FocusedBaseSlot
                             ?? state.HoveredBaseSlot
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

        public BaseSlot ResolveFocusedSlot(UniversalInventory inventory)
        {
            if (inventory == null)
                return null;

            var state = GetOrCreateState(inventory);
            return state.FocusedBaseSlot;
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
            return state.FocusedBaseSlot
                   ?? state.HoveredBaseSlot
                   ?? inventory.ResolveAutoTransferSlot();
        }

        public void RoutePointerEnter(SlotInputAdapter adapter, PointerEventData eventData)
        {
            if (!TryGetInventory(adapter, out var inventory) || adapter?.BaseSlot == null)
                return;

            MarkInventoryActive(inventory);
            var state = GetOrCreateState(inventory);
            state.HoveredAdapter = adapter;
            state.HoveredBaseSlot = adapter.BaseSlot;
            state.ActiveFocusSource = FocusSource.Mouse;

            if (DragAndDropManager.AutoCreateInstance.IsDragging && adapter.BaseSlot.IsInteractable)
                DragAndDropManager.AutoCreateInstance.PushDropTarget(adapter);
        }

        public void RoutePointerExit(SlotInputAdapter adapter, PointerEventData eventData)
        {
            if (!TryGetInventory(adapter, out var inventory) || adapter?.BaseSlot == null)
                return;

            var state = GetOrCreateState(inventory);
            if (ReferenceEquals(state.HoveredBaseSlot, adapter.BaseSlot))
            {
                state.HoveredAdapter = null;
                state.HoveredBaseSlot = null;

                if (state.FocusedBaseSlot == null)
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
            state.FocusedBaseSlot = adapter.BaseSlot;
            state.ActiveFocusSource = FocusSource.Mouse;
            state.PressedAdapter = adapter;
            state.PressedButton = eventData.button;
            state.PressedTime = Time.unscaledTime;
            state.PressedPosition = eventData.position;

            if (!DragAndDropManager.AutoCreateInstance.IsDragging)
            {
                // PointerDown should allow regular slot actions (selection, inventory ops)
                // and drag start actions. Drag completion/cancel is processed on PointerUp.
                ExecutePointerBindings(inventory, adapter, eventData, PointerTriggerPhase.Down, dragOnly: false);
            }
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
            if (DragAndDropManager.AutoCreateInstance.IsDragging)
                return;

            StopHoldCount();

            if (!TryGetInventory(adapter, out var inventory))
                return;

            ExecutePointerBindings(inventory, adapter, eventData, PointerTriggerPhase.BeginDrag, dragOnly: true);
        }

        public void RouteFocusEnter(SlotInputAdapter adapter, FocusSource source)
        {
            if (!TryGetInventory(adapter, out var inventory) || adapter?.BaseSlot == null)
                return;

            MarkInventoryActive(inventory);
            var state = GetOrCreateState(inventory);
            state.FocusedAdapter = adapter;
            state.FocusedBaseSlot = adapter.BaseSlot;
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
            if (state.ActiveFocusSource == source && ReferenceEquals(state.FocusedBaseSlot, adapter.BaseSlot))
            {
                state.FocusedAdapter = null;
                state.FocusedBaseSlot = null;
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
                if (state.FocusedBaseSlot == null)
                    state.ActiveFocusSource = FocusSource.None;
            }

            TryClearActiveInventory(inventory);

            if (DragAndDropManager.AutoCreateInstance.IsDragging)
                DragAndDropManager.AutoCreateInstance.PopDropTarget(dropArea);
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

            var state = GetOrCreateState(inventory);
            var adapter = state.FocusedAdapter
                          ?? ResolveAdapterFromSlot(state.FocusedBaseSlot)
                          ?? state.HoveredAdapter
                          ?? ResolveAdapterFromSlot(state.HoveredBaseSlot);

            ExecuteInputActionBindings(ResolveInputActionBindings(inventory), inventory, adapter, context);
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

            ExecuteInputActionBindings(DefaultBindingsProfile.InputActionBindingsRuntime, null, null, context);
        }

        private void ExecuteInputActionBindings(
            IReadOnlyList<InputActionBinding> bindings,
            UniversalInventory inventory,
            SlotInputAdapter adapter,
            InputAction.CallbackContext context)
        {
            var action = context.action;
            if (action == null || bindings == null)
                return;

            for (int i = 0; i < bindings.Count; i++)
            {
                var binding = bindings[i];
                if (binding == null || !binding.IsValid() || !binding.Matches(context))
                    continue;

                if (binding.Action.CanExecute(inventory, adapter, null))
                    _ = binding.Action.Execute(inventory, adapter, null);

                return;
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
            if (inventory != null && _overridesByInventory.TryGetValue(inventory, out var overrideBinder) && overrideBinder != null)
            {
                return overrideBinder.PointerBindingsResolved;
            }

            return DefaultBindingsProfile != null
                ? DefaultBindingsProfile.PointerBindingsRuntime
                : Array.Empty<PointerBinding>();
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
        /// Начать подсчёт удержания. Вызывается из StartHoldCountAction (Down фаза).
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
        /// Получить текущее накопленное количество для слота.
        /// Вызывается из StartHoldDragAction (BeginDrag фаза).
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
            if (state.HoveredBaseSlot == null && state.FocusedBaseSlot == null && state.FocusedDropArea == null)
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

        private static SlotInputAdapter ResolveAdapterFromSlot(BaseSlot baseSlot) => baseSlot.GetComponent<SlotInputAdapter>();

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
                          ?? ResolveAdapterFromSlot(state.FocusedBaseSlot)
                          ?? state.HoveredAdapter
                          ?? ResolveAdapterFromSlot(state.HoveredBaseSlot);
            var eventData = new PointerEventData(EventSystem.current) { button = button };

            _pointerUpHandledThisFrame.Add(button);
            ExecutePointerBindings(inventory, adapter, eventData, PointerTriggerPhase.Up, dragOnly: true);
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

        private void ProcessUnhandledGlobalPointerEvents()
        {
            // Во время драга глобальные pointer up обрабатываются в ProcessGlobalPointerUpsWhileDragging
            if (DragAndDropManager.IsInstanceExist && DragAndDropManager.AutoCreateInstance.IsDragging)
                return;

            var mouse = Mouse.current;
            if (mouse == null)
                return;

            TrackGlobalPressState(mouse);

            ProcessUnhandledGlobalRelease(mouse.leftButton, PointerEventData.InputButton.Left);
            ProcessUnhandledGlobalRelease(mouse.rightButton, PointerEventData.InputButton.Right);
            ProcessUnhandledGlobalRelease(mouse.middleButton, PointerEventData.InputButton.Middle);
        }

        private void TrackGlobalPressState(Mouse mouse)
        {
            if (mouse.leftButton.wasPressedThisFrame)
            {
                _globalPressTime[0] = Time.unscaledTime;
                _globalPressPosition[0] = mouse.position.ReadValue();
            }
            if (mouse.rightButton.wasPressedThisFrame)
            {
                _globalPressTime[1] = Time.unscaledTime;
                _globalPressPosition[1] = mouse.position.ReadValue();
            }
            if (mouse.middleButton.wasPressedThisFrame)
            {
                _globalPressTime[2] = Time.unscaledTime;
                _globalPressPosition[2] = mouse.position.ReadValue();
            }
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

                bool handled = ExecutePointerBindings(null, null, eventData, clickPhase, dragOnly: false);
                if (!handled && clickPhase != PointerTriggerPhase.Click)
                    handled = ExecutePointerBindings(null, null, eventData, PointerTriggerPhase.Click, dragOnly: false);
                if (!handled)
                    ExecutePointerBindings(null, null, eventData, PointerTriggerPhase.Up, dragOnly: false);
            }
            else
            {
                ExecutePointerBindings(null, null, eventData, PointerTriggerPhase.Up, dragOnly: false);
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
            public BaseSlot FocusedBaseSlot;
            public SlotInputAdapter FocusedAdapter;
            public InventoryDropArea FocusedDropArea;
            public BaseSlot HoveredBaseSlot;
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
