using System.Collections.Generic;
using DragAndDropSystem.Inventories;
using DragAndDropSystem.Selection;
using DragAndDropSystem.Slots;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace DragAndDropSystem.Interaction
{
    /// <summary>
    /// Router сырых событий ввода:
    /// - от slot adapters (pointer/focus/drag)
    /// - от InventoryInputHandler (Input System actions)
    /// Маршрутизирует события в per-inventory coordinator.
    /// </summary>
    [DisallowMultipleComponent]
    public class InputEventRouter : MonoBehaviour
    {
        private static InputEventRouter _instance;
        [SerializeField] private InventoryInteractionBindingsProfile _defaultBindingsProfile;

        public static bool IsInstanceExist => _instance != null;
        public InventoryInteractionBindingsProfile DefaultBindingsProfile => _defaultBindingsProfile;

        public static InputEventRouter Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = FindFirstObjectByType<InputEventRouter>();
                    if (_instance == null)
                    {
                        var go = new GameObject(nameof(InputEventRouter));
                        _instance = go.AddComponent<InputEventRouter>();
                    }
                }

                return _instance;
            }
        }

        private readonly Dictionary<IInventory, InventoryInteractionCoordinator> _byInventory =
            new Dictionary<IInventory, InventoryInteractionCoordinator>();
        private readonly Dictionary<UniversalInventory, FallbackInteractionState> _fallbackByInventory =
            new Dictionary<UniversalInventory, FallbackInteractionState>();

        private readonly HashSet<IntentDedupKey> _handledThisFrame = new HashSet<IntentDedupKey>();

        private void Awake()
        {
            if (_instance == null)
            {
                _instance = this;
            }
            else if (_instance != this)
            {
                Destroy(gameObject);
            }
        }

        private void LateUpdate()
        {
            _handledThisFrame.Clear();
        }

        public void RegisterCoordinator(InventoryInteractionCoordinator coordinator)
        {
            if (coordinator == null || coordinator.Inventory == null)
                return;

            _byInventory[coordinator.Inventory] = coordinator;
        }

        public void UnregisterCoordinator(InventoryInteractionCoordinator coordinator)
        {
            if (coordinator == null || coordinator.Inventory == null)
                return;

            if (_byInventory.TryGetValue(coordinator.Inventory, out var existing) && existing == coordinator)
            {
                _byInventory.Remove(coordinator.Inventory);
            }
        }

        public bool TryRouteInventoryAction(
            UniversalInventory inventory,
            InventoryActionBase action,
            InputAction.CallbackContext callbackContext,
            bool logWarnings)
        {
            if (inventory == null || action == null)
                return false;

            if (!TryGetCoordinator(inventory, out var coordinator))
                return false;

            var key = new IntentDedupKey((int)callbackContext.phase, null, action);
            if (!_handledThisFrame.Add(key))
                return true;

            return coordinator.RouteInventoryAction(action, callbackContext, logWarnings);
        }

        public void RoutePointerEnter(SlotInputAdapter adapter, PointerEventData eventData)
        {
            if (TryGetCoordinator(adapter, out var coordinator))
                coordinator.OnPointerEnter(adapter, eventData);
            else
                RouteFallbackPointerEnter(adapter);
        }

        public void RoutePointerExit(SlotInputAdapter adapter, PointerEventData eventData)
        {
            if (TryGetCoordinator(adapter, out var coordinator))
                coordinator.OnPointerExit(adapter, eventData);
            else
                RouteFallbackPointerExit(adapter);
        }

        public void RoutePointerDown(SlotInputAdapter adapter, PointerEventData eventData)
        {
            if (TryGetCoordinator(adapter, out var coordinator))
                coordinator.OnPointerDown(adapter, eventData);
            else
                RouteFallbackPointerDown(adapter, eventData);
        }

        public void RoutePointerUp(SlotInputAdapter adapter, PointerEventData eventData)
        {
            if (TryGetCoordinator(adapter, out var coordinator))
                coordinator.OnPointerUp(adapter, eventData);
            else
                RouteFallbackPointerUp(adapter, eventData);
        }

        public void RouteBeginDrag(SlotInputAdapter adapter, PointerEventData eventData)
        {
            if (TryGetCoordinator(adapter, out var coordinator))
                coordinator.OnBeginDrag(adapter, eventData);
        }

        public void RouteFocusEnter(SlotInputAdapter adapter, FocusSource source)
        {
            if (TryGetCoordinator(adapter, out var coordinator))
                coordinator.OnFocusEnter(adapter, source);
        }

        public void RouteFocusExit(SlotInputAdapter adapter, FocusSource source)
        {
            if (TryGetCoordinator(adapter, out var coordinator))
                coordinator.OnFocusExit(adapter, source);
        }

        public void RouteSubmit(SlotInputAdapter adapter, BaseEventData eventData)
        {
            if (TryGetCoordinator(adapter, out var coordinator))
                coordinator.OnSubmit(adapter, eventData);
            else
                RouteFallbackNavigation(adapter, InventoryInteractionCoordinator.NavigationEventType.Submit);
        }

        public void RouteCancel(SlotInputAdapter adapter, BaseEventData eventData)
        {
            if (TryGetCoordinator(adapter, out var coordinator))
                coordinator.OnCancel(adapter, eventData);
            else
                RouteFallbackNavigation(adapter, InventoryInteractionCoordinator.NavigationEventType.Cancel);
        }

        private void RouteFallbackPointerEnter(SlotInputAdapter adapter)
        {
            if (adapter?.Slot == null)
                return;

            if (DragAndDropManager.Instance.IsDragging && adapter.Slot.IsInteractable)
            {
                DragAndDropManager.Instance.PushDropTarget(adapter);
            }
        }

        private void RouteFallbackPointerExit(SlotInputAdapter adapter)
        {
            if (adapter?.Slot == null)
                return;

            if (DragAndDropManager.Instance.IsDragging)
            {
                DragAndDropManager.Instance.PopDropTarget(adapter);
            }
        }

        private void RouteFallbackPointerDown(SlotInputAdapter adapter, PointerEventData eventData)
        {
            if (!TryGetFallbackInventory(adapter, out var inventory) || eventData == null)
                return;

            var state = GetOrCreateFallbackState(inventory);
            state.PressedAdapter = adapter;
            state.PressedButton = eventData.button;

            ExecuteFallbackPointerBindings(inventory, adapter, eventData, dragOnly: true);
        }

        private void RouteFallbackPointerUp(SlotInputAdapter adapter, PointerEventData eventData)
        {
            if (!TryGetFallbackInventory(adapter, out var inventory) || eventData == null)
                return;

            var state = GetOrCreateFallbackState(inventory);
            if (state.PressedAdapter == adapter && state.PressedButton == eventData.button)
            {
                ExecuteFallbackPointerBindings(inventory, adapter, eventData, dragOnly: false);
            }

            if (state.PressedAdapter == adapter && state.PressedButton == eventData.button)
            {
                state.PressedAdapter = null;
            }
        }

        private void RouteFallbackNavigation(
            SlotInputAdapter adapter,
            InventoryInteractionCoordinator.NavigationEventType eventType)
        {
            if (!TryGetFallbackInventory(adapter, out var inventory) || _defaultBindingsProfile == null)
                return;

            var bindings = _defaultBindingsProfile.NavigationBindings;
            for (int i = 0; i < bindings.Count; i++)
            {
                var binding = bindings[i];
                if (binding == null || !binding.IsValid() || !binding.Matches(eventType))
                    continue;

                ExecuteFallbackAction(inventory, adapter, binding.Action, null);
                return;
            }
        }

        private void ExecuteFallbackPointerBindings(
            UniversalInventory inventory,
            SlotInputAdapter adapter,
            PointerEventData eventData,
            bool dragOnly)
        {
            if (_defaultBindingsProfile == null)
                return;

            var bindings = _defaultBindingsProfile.PointerBindings;
            for (int i = 0; i < bindings.Count; i++)
            {
                var binding = bindings[i];
                if (binding == null || !binding.IsValid() || !binding.Matches(eventData))
                    continue;

                bool isDrag = binding.Action is DragSlotAction;
                if (dragOnly && !isDrag)
                    continue;
                if (!dragOnly && isDrag)
                    continue;

                ExecuteFallbackAction(inventory, adapter, binding.Action, eventData);
                eventData.Use();
                return;
            }
        }

        private static void ExecuteFallbackAction(
            UniversalInventory inventory,
            SlotInputAdapter adapter,
            SlotInteractionAction action,
            PointerEventData eventData)
        {
            if (action == null)
                return;

            if (action is DragSlotAction)
            {
                if (DragAndDropManager.Instance.IsDragging)
                {
                    DragAndDropManager.Instance.CompleteDrag();
                    return;
                }

                var slot = adapter?.Slot;
                if (slot == null || slot.IsEmpty || !slot.IsInteractable)
                    return;

                DragAndDropManager.Instance.StartDrag(slot);
                return;
            }

            if (action is CancelDragAction)
            {
                if (DragAndDropManager.Instance.IsDragging)
                    DragAndDropManager.Instance.CancelDrag();
                return;
            }

            if (action is SelectionSlotAction selectionAction)
            {
                if (!SelectionManager.IsInstanceExist || selectionAction.Operation == null)
                    return;

                var slot = adapter?.Slot;
                if (slot == null)
                    return;

                var selection = SelectionManager.Instance;
                if (selectionAction.Operation.CanExecute(selection, slot))
                {
                    selectionAction.Operation.Execute(selection, slot);
                }
                return;
            }

            if (action is InventorySlotAction inventoryAction)
            {
                if (inventory == null || inventoryAction.InventoryAction == null)
                    return;

                var slot = adapter?.Slot ?? inventory.ResolveAutoTransferSlot();
                if (!inventoryAction.InventoryAction.CanExecute(inventory, slot))
                    return;

                inventoryAction.InventoryAction.Execute(inventory, slot, inventoryAction.LogWarnings);
            }
        }

        private bool TryGetFallbackInventory(SlotInputAdapter adapter, out UniversalInventory inventory)
        {
            inventory = null;
            if (adapter?.Slot?.Inventory is UniversalInventory universalInventory)
            {
                inventory = universalInventory;
                return true;
            }
            return false;
        }

        private FallbackInteractionState GetOrCreateFallbackState(UniversalInventory inventory)
        {
            if (!_fallbackByInventory.TryGetValue(inventory, out var state) || state == null)
            {
                state = new FallbackInteractionState();
                _fallbackByInventory[inventory] = state;
            }

            return state;
        }

        private bool TryGetCoordinator(SlotInputAdapter adapter, out InventoryInteractionCoordinator coordinator)
        {
            coordinator = null;
            if (adapter == null)
                return false;

            if (adapter.Coordinator != null)
            {
                coordinator = adapter.Coordinator;
                return true;
            }

            var slot = adapter.Slot;
            return slot != null && TryGetCoordinator(slot.Inventory, out coordinator);
        }

        private bool TryGetCoordinator(IInventory inventory, out InventoryInteractionCoordinator coordinator)
        {
            coordinator = null;
            if (inventory == null)
                return false;

            if (_byInventory.TryGetValue(inventory, out coordinator) && coordinator != null)
                return true;

            if (inventory is UniversalInventory universalInventory)
            {
                coordinator = universalInventory.GetComponent<InventoryInteractionCoordinator>();
                if (coordinator != null)
                {
                    _byInventory[universalInventory] = coordinator;
                    return true;
                }
            }

            return false;
        }

        private readonly struct IntentDedupKey
        {
            public IntentDedupKey(int type, ISlot slot, object token)
            {
                Type = type;
                Slot = slot;
                Token = token;
            }

            public int Type { get; }
            public ISlot Slot { get; }
            public object Token { get; }
        }

        private sealed class FallbackInteractionState
        {
            public SlotInputAdapter PressedAdapter;
            public PointerEventData.InputButton PressedButton;
        }
    }
}
