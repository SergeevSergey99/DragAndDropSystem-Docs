using System.Collections.Generic;
using DragAndDropSystem.Inventories;
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

        public static bool IsInstanceExist => _instance != null;

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
        }

        public void RoutePointerExit(SlotInputAdapter adapter, PointerEventData eventData)
        {
            if (TryGetCoordinator(adapter, out var coordinator))
                coordinator.OnPointerExit(adapter, eventData);
        }

        public void RoutePointerDown(SlotInputAdapter adapter, PointerEventData eventData)
        {
            if (TryGetCoordinator(adapter, out var coordinator))
                coordinator.OnPointerDown(adapter, eventData);
        }

        public void RoutePointerUp(SlotInputAdapter adapter, PointerEventData eventData)
        {
            if (TryGetCoordinator(adapter, out var coordinator))
                coordinator.OnPointerUp(adapter, eventData);
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

            return _byInventory.TryGetValue(inventory, out coordinator) && coordinator != null;
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
    }
}
