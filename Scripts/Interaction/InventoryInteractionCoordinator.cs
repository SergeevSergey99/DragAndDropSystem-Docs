using DragAndDropSystem.Inventories;
using DragAndDropSystem.Slots;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

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

        private ISlot _focusedSlot;
        private FocusSource _activeFocusSource = FocusSource.None;
        private InteractionState _state = InteractionState.Idle;
        private SlotInputAdapter _pressedAdapter;
        private PointerEventData.InputButton _pressedButton;

        public UniversalInventory Inventory => _inventory;
        public ISlot FocusedSlot => _focusedSlot;
        public FocusSource ActiveFocusSource => _activeFocusSource;

        private DragAndDropManager _dragManager => DragAndDropManager.Instance;

        private void Awake()
        {
            if (_inventory == null)
                _inventory = GetComponent<UniversalInventory>();
        }

        private void OnEnable()
        {
            InputEventRouter.Instance.RegisterCoordinator(this);
        }

        private void OnDisable()
        {
            if (InputEventRouter.IsInstanceExist)
                InputEventRouter.Instance.UnregisterCoordinator(this);
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
                _activeFocusSource = FocusSource.None;
                if (_state != InteractionState.Dragging)
                    _state = InteractionState.Idle;
            }
        }

        public bool RouteInventoryAction(
            InventoryActionBase action,
            InputAction.CallbackContext _,
            bool logWarnings)
        {
            if (_inventory == null || action == null)
                return false;

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
    }
}
