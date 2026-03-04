using System;
using System.Collections.Generic;
using CodeUtils;
using DragAndDropSystem.Inventories;
using DragAndDropSystem.Slots;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.Serialization;

namespace DragAndDropSystem.Interaction
{
    [DisallowMultipleComponent]
    public class InputEventRouter : MonoSingleton<InputEventRouter>
    {
        [field: SerializeField] 
        public InteractionBindingsProfile DefaultBindingsProfile { get; private set; }

        // Словарь для переопределения привязок на уровне конкретного инвентаря (например, для разных UI или режимов работы)
        private readonly Dictionary<UniversalInventory, InventoryExtraInteractionBinder> _overridesByInventory = new();
        // Словарь для хранения runtime-состояния (наведенный слот, источник фокуса, нажатые кнопки и т.д.) для каждого инвентаря
        private readonly Dictionary<UniversalInventory, RuntimeState> _runtimeStateByInventory = new();
        // Словарь для хранения подписок на InputAction для каждого инвентаря, чтобы можно было отписаться при необходимости
        private readonly Dictionary<UniversalInventory, List<InputActionSubscription>> _actionSubscriptionsByInventory = new ();

        // Набор для дедупликации вызовов действий в рамках одного кадра, чтобы избежать повторного срабатывания при нескольких событиях (например, PointerDown + InputAction)
        private readonly HashSet<IntentDedupKey> _handledThisFrame = new();
        // Набор для отслеживания, какие кнопки мыши уже были обработаны в рамках глобального PointerUp во время перетаскивания, чтобы не обрабатывать их несколько раз
        private readonly HashSet<PointerEventData.InputButton> _pointerUpHandledThisFrame = new ();
        // Временный список для очистки словарей от невалидных (уничтоженных) инвентарей
        private readonly List<UniversalInventory> _staleInventories = new List<UniversalInventory>();

        private void Update()
        {
            ProcessGlobalPointerUpsWhileDragging();
        }
        private void LateUpdate()
        {
            _handledThisFrame.Clear();
            _pointerUpHandledThisFrame.Clear();
            CleanupStaleInventories();
        }

        public void RegisterExtraBinder(InventoryExtraInteractionBinder extraBinder)
        {
            if (extraBinder == null || extraBinder.Inventory == null)
                return;

            var inventory = extraBinder.Inventory;
            _overridesByInventory[inventory] = extraBinder;

            extraBinder.RebuildResolvedBindings(DefaultBindingsProfile);
            RebindCoordinatorInputActions(inventory, extraBinder);
        }

        public void UnregisterExtraBinder(InventoryExtraInteractionBinder extraBinder)
        {
            if (extraBinder == null || extraBinder.Inventory == null)
                return;

            var inventory = extraBinder.Inventory;
            if (_overridesByInventory.TryGetValue(inventory, out var existing) && existing == extraBinder)
                _overridesByInventory.Remove(inventory);

            UnbindCoordinatorInputActions(inventory);
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
            var activeSlot = state.FocusedSlot as UniversalSlot ?? inventory.ResolveAutoTransferSlot();
            if (!action.CanExecute(inventory, activeSlot))
            {
                return true;
            }

            _ = action.Execute(inventory, activeSlot);

            return true;
        }

        public void RoutePointerEnter(SlotInputAdapter adapter, PointerEventData eventData)
        {
            if (!TryGetInventory(adapter, out var inventory) || adapter?.Slot == null)
                return;

            var state = GetOrCreateState(inventory);
            state.FocusedAdapter = adapter;
            state.FocusedSlot = adapter.Slot;
            state.ActiveFocusSource = FocusSource.Mouse;

            if (DragAndDropManager.Instance.IsDragging && adapter.Slot.IsInteractable)
                DragAndDropManager.Instance.PushDropTarget(adapter);
        }

        public void RoutePointerExit(SlotInputAdapter adapter, PointerEventData eventData)
        {
            if (!TryGetInventory(adapter, out var inventory) || adapter?.Slot == null)
                return;

            var state = GetOrCreateState(inventory);
            if (state.ActiveFocusSource == FocusSource.Mouse && ReferenceEquals(state.FocusedSlot, adapter.Slot))
            {
                state.FocusedAdapter = null;
                state.FocusedSlot = null;
                state.ActiveFocusSource = FocusSource.None;
            }

            if (DragAndDropManager.Instance.IsDragging)
                DragAndDropManager.Instance.PopDropTarget(adapter);
        }

        public void RoutePointerDown(SlotInputAdapter adapter, PointerEventData eventData)
        {
            if (!TryGetInventory(adapter, out var inventory) || eventData == null)
                return;

            var state = GetOrCreateState(inventory);
            state.PressedAdapter = adapter;
            state.PressedButton = eventData.button;

            if (!DragAndDropManager.Instance.IsDragging)
            {
                // PointerDown should allow regular slot actions (selection, inventory ops)
                // and drag start actions. Drag completion/cancel is processed on PointerUp.
                ExecutePointerBindings(inventory, adapter, eventData, dragOnly: false);
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
                bool dragOnly = isDraggingNow;
                ExecutePointerBindings(inventory, adapter, eventData, dragOnly);
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

            var state = GetOrCreateState(inventory);
            state.FocusedAdapter = adapter;
            state.FocusedSlot = adapter.Slot;
            state.ActiveFocusSource = source;

            if (DragAndDropManager.Instance.IsDragging && adapter.Slot.IsInteractable)
                DragAndDropManager.Instance.PushDropTarget(adapter);
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

            if (DragAndDropManager.Instance.IsDragging)
                DragAndDropManager.Instance.PopDropTarget(adapter);
        }

        public void RouteSubmit(SlotInputAdapter adapter, BaseEventData eventData)
        {
            if (!TryGetInventory(adapter, out var inventory))
                return;

            ExecuteNavigationBindings(inventory, adapter, NavigationEventType.Submit);
        }

        public void RouteCancel(SlotInputAdapter adapter, BaseEventData eventData)
        {
            if (!TryGetInventory(adapter, out var inventory))
                return;

            ExecuteNavigationBindings(inventory, adapter, NavigationEventType.Cancel);
        }

        private void ExecutePointerBindings(
            UniversalInventory inventory,
            SlotInputAdapter adapter,
            PointerEventData eventData,
            bool dragOnly)
        {
            var bindings = ResolvePointerBindings(inventory, out bool useBindings);
            if (!useBindings || eventData == null)
                return;

            if (!dragOnly && adapter?.Slot == null)
                return;

            for (int i = 0; i < bindings.Count; i++)
            {
                var binding = bindings[i];
                if (binding == null || !binding.IsValid() || !binding.Matches(eventData))
                    continue;

                bool isDragBinding = IsDragBindingAction(binding.Action);
                if (dragOnly && !isDragBinding)
                    continue;

                if (binding.Action.CanExecute(inventory, adapter, eventData))
                {
                    _ = binding.Action.Execute(inventory, adapter, eventData);
                    eventData.Use();
                }

                return;
            }
        }

        private void ExecuteNavigationBindings(
            UniversalInventory inventory,
            SlotInputAdapter adapter,
            NavigationEventType eventType)
        {
            var bindings = ResolveNavigationBindings(inventory, out bool useBindings);
            if (!useBindings)
                return;

            if (DragAndDropManager.Instance.IsDragging && eventType != NavigationEventType.Cancel)
                return;

            for (int i = 0; i < bindings.Count; i++)
            {
                var binding = bindings[i];
                if (binding == null || !binding.IsValid() || !binding.Matches(eventType))
                    continue;

                if (binding.Action.CanExecute(inventory, adapter, null))
                {
                    _ = binding.Action.Execute(inventory, adapter, null);
                }
                
                return;
            }
        }

        private void HandleCoordinatorInputAction(UniversalInventory inventory, InputAction.CallbackContext context)
        {
            if (inventory == null)
                return;

            var bindings = ResolveInputActionBindings(inventory, out bool useBindings);
            if (!useBindings)
                return;

            var action = context.action;
            if (action == null)
                return;

            var state = GetOrCreateState(inventory);
            var adapter = state.FocusedAdapter ?? ResolveAdapterFromSlot(state.FocusedSlot);

            for (int i = 0; i < bindings.Count; i++)
            {
                var binding = bindings[i];
                if (binding == null || !binding.IsValid() || !binding.Matches(action))
                    continue;

                if (binding.Action.CanExecute(inventory, adapter, null))
                {
                    _ = binding.Action.Execute(inventory, adapter, null);
                }

                return;
            }
        }

        private void RebindCoordinatorInputActions(UniversalInventory inventory, InventoryExtraInteractionBinder binder)
        {
            UnbindCoordinatorInputActions(inventory);

            if (binder == null)
                return;

            binder.RebuildResolvedBindings(DefaultBindingsProfile);
            if (!binder.UseInputActionBindingsResolved)
                return;

            var bindings = binder.InputActionBindingsResolved;
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

                Action<InputAction.CallbackContext> handler = ctx => HandleCoordinatorInputAction(inventory, ctx);
                inputAction.performed += handler;
                subs.Add(new InputActionSubscription(inputAction, handler));
            }
        }

        private void UnbindCoordinatorInputActions(UniversalInventory inventory)
        {
            if (inventory == null)
                return;

            if (!_actionSubscriptionsByInventory.TryGetValue(inventory, out var subs))
                return;

            for (int i = 0; i < subs.Count; i++)
            {
                var sub = subs[i];
                if (sub.Action != null)
                    sub.Action.performed -= sub.Handler;
            }

            _actionSubscriptionsByInventory.Remove(inventory);
        }

        private IReadOnlyList<PointerBinding> ResolvePointerBindings(
            UniversalInventory inventory,
            out bool useBindings)
        {
            if (_overridesByInventory.TryGetValue(inventory, out var overrideCoordinator) && overrideCoordinator != null)
            {
                overrideCoordinator.RebuildResolvedBindings(DefaultBindingsProfile);
                useBindings = overrideCoordinator.UsePointerBindingsResolved;
                return overrideCoordinator.PointerBindingsResolved;
            }

            useBindings = DefaultBindingsProfile != null && DefaultBindingsProfile.UsePointerBindings;
            
            return DefaultBindingsProfile != null
                ? DefaultBindingsProfile.PointerBindings
                : Array.Empty<PointerBinding>();
        }

        private IReadOnlyList<NavigationBinding> ResolveNavigationBindings(
            UniversalInventory inventory,
            out bool useBindings)
        {
            if (_overridesByInventory.TryGetValue(inventory, out var overrideCoordinator) && overrideCoordinator != null)
            {
                overrideCoordinator.RebuildResolvedBindings(DefaultBindingsProfile);
                useBindings = overrideCoordinator.UseNavigationBindingsResolved;
                return overrideCoordinator.NavigationBindingsResolved;
            }

            useBindings = DefaultBindingsProfile != null && DefaultBindingsProfile.UseNavigationBindings;
            
            return DefaultBindingsProfile != null
                ? DefaultBindingsProfile.NavigationBindings
                : Array.Empty<NavigationBinding>();
        }

        private IReadOnlyList<InputActionBinding> ResolveInputActionBindings(
            UniversalInventory inventory,
            out bool useBindings)
        {
            if (_overridesByInventory.TryGetValue(inventory, out var overrideCoordinator) && overrideCoordinator != null)
            {
                overrideCoordinator.RebuildResolvedBindings(DefaultBindingsProfile);
                useBindings = overrideCoordinator.UseInputActionBindingsResolved;
                return overrideCoordinator.InputActionBindingsResolved;
            }

            useBindings = DefaultBindingsProfile != null && DefaultBindingsProfile.UseInputActionBindings;
            
            return DefaultBindingsProfile != null
                ? DefaultBindingsProfile.InputActionBindings
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

        private static bool IsDragBindingAction(SlotInteractionAction action)
            => action is DragSlotAction || action is StartMultiDragAction || action is CompleteDragAction;

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
            var adapter = state.PressedAdapter ?? state.FocusedAdapter ?? ResolveAdapterFromSlot(state.FocusedSlot);
            var eventData = new PointerEventData(EventSystem.current) { button = button };

            _pointerUpHandledThisFrame.Add(button);
            ExecutePointerBindings(inventory, adapter, eventData, dragOnly: true);
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
            if (mouse != null)
            {
                switch (button)
                {
                    case PointerEventData.InputButton.Left:
                        return mouse.leftButton.wasReleasedThisFrame;
                    case PointerEventData.InputButton.Right:
                        return mouse.rightButton.wasReleasedThisFrame;
                    case PointerEventData.InputButton.Middle:
                        return mouse.middleButton.wasReleasedThisFrame;
                }
            }

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

        private sealed class RuntimeState
        {
            public ISlot FocusedSlot;
            public SlotInputAdapter FocusedAdapter;
            public FocusSource ActiveFocusSource;
            public SlotInputAdapter PressedAdapter;
            public PointerEventData.InputButton PressedButton;
        }

        private readonly struct IntentDedupKey
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
        }
    }
}
