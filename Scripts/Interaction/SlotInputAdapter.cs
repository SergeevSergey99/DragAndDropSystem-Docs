using DragAndDropSystem.Core;
using DragAndDropSystem.Inventories;
using DragAndDropSystem.Selection;
using DragAndDropSystem.Slots;
using DragAndDropSystem.Tools;
using UnityEngine;
using UnityEngine.EventSystems;
#if ENABLE_REFLEX_DI
using Reflex.Attributes;
#endif

namespace DragAndDropSystem.Interaction
{
    /// <summary>
    /// Тонкий slot-adapter: пересылает raw-события в InputEventRouter.
    /// Доменную логику не содержит.
    /// </summary>
    public class SlotInputAdapter : MonoBehaviour,
        IPointerEnterHandler, IPointerExitHandler,
        IPointerDownHandler, IPointerUpHandler,
        IBeginDragHandler,
        ISelectHandler, IDeselectHandler,
        ISubmitHandler, ICancelHandler,
        IDropTarget
    {
        [SerializeField] private UniversalSlot _slot;
        [SerializeField] private InventoryInteractionCoordinator _coordinator;
        [SerializeField] private bool _disableLegacyComponentsOnEnable = true;

        public UniversalSlot Slot => _slot;
        public InventoryInteractionCoordinator Coordinator => _coordinator;

#if ENABLE_REFLEX_DI
        [Inject] private DragAndDropManager _dragManager;
#else
        private DragAndDropManager _dragManager => DragAndDropManager.Instance;
#endif

        private void Awake()
        {
            if (_slot == null)
                _slot = GetComponent<UniversalSlot>();

            if (_coordinator == null)
                _coordinator = GetComponentInParent<InventoryInteractionCoordinator>();
            if (_coordinator == null && _slot?.Inventory is UniversalInventory inventory)
                _coordinator = inventory.GetComponent<InventoryInteractionCoordinator>();
            if (_coordinator == null && Application.isPlaying && _slot?.Inventory is UniversalInventory runtimeInventory)
                _coordinator = runtimeInventory.gameObject.AddComponent<InventoryInteractionCoordinator>();
        }

        private void OnEnable()
        {
            if (_coordinator == null)
                _coordinator = GetComponentInParent<InventoryInteractionCoordinator>();
            if (_coordinator == null && _slot?.Inventory is UniversalInventory inventory)
                _coordinator = inventory.GetComponent<InventoryInteractionCoordinator>();
            if (_coordinator == null && Application.isPlaying && _slot?.Inventory is UniversalInventory runtimeInventory)
                _coordinator = runtimeInventory.gameObject.AddComponent<InventoryInteractionCoordinator>();

            if (!_disableLegacyComponentsOnEnable)
                return;
            if (_coordinator == null)
                return;

            var legacyDrag = GetComponent<DragDropEventListener>();
            if (legacyDrag != null && legacyDrag.enabled)
            {
                legacyDrag.enabled = false;
                Extentions.DragAndDropLog($"Disabled legacy DragDropEventListener on '{name}'");
            }

            var legacySelection = GetComponent<SlotPointerSelectionTrigger>();
            if (legacySelection != null && legacySelection.enabled)
            {
                legacySelection.enabled = false;
                Extentions.DragAndDropLog($"Disabled legacy SlotPointerSelectionTrigger on '{name}'");
            }
        }

        private void OnDisable()
        {
            if (_slot == null || _dragManager == null)
                return;

            if (_dragManager.IsDragging)
            {
                _dragManager.PopDropTarget(this);
            }

            if (_slot.Inventory is UniversalInventory universalInventory)
            {
                universalInventory.NotifyPointerExit(_slot);
            }
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (_slot?.Inventory is UniversalInventory universalInventory)
            {
                universalInventory.NotifyPointerEnter(_slot);
            }

            InputEventRouter.Instance.RoutePointerEnter(this, eventData);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            if (_slot?.Inventory is UniversalInventory universalInventory)
            {
                universalInventory.NotifyPointerExit(_slot);
            }

            InputEventRouter.Instance.RoutePointerExit(this, eventData);
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (_slot == null || _slot.IsEmpty)
                return;

            if (!_slot.IsInteractable)
            {
                Extentions.DragAndDropLog($"OnPointerDown blocked - slot {name} is not interactable (filtered)");
                return;
            }

            if (_slot.Inventory is UniversalInventory universalInventory)
            {
                universalInventory.NotifySlotInteracted(_slot);
            }

            InputEventRouter.Instance.RoutePointerDown(this, eventData);
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            InputEventRouter.Instance.RoutePointerUp(this, eventData);
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            InputEventRouter.Instance.RouteBeginDrag(this, eventData);
        }

        public void OnSelect(BaseEventData eventData)
        {
            InputEventRouter.Instance.RouteFocusEnter(this, FocusSource.Gamepad);
        }

        public void OnDeselect(BaseEventData eventData)
        {
            InputEventRouter.Instance.RouteFocusExit(this, FocusSource.Gamepad);
        }

        public void OnSubmit(BaseEventData eventData)
        {
            InputEventRouter.Instance.RouteSubmit(this, eventData);
        }

        public void OnCancel(BaseEventData eventData)
        {
            InputEventRouter.Instance.RouteCancel(this, eventData);
        }

        // ===== IDropTarget =====

        public ISlot GetTargetSlot() => _slot;

        public IItemDropHandler GetDropHandler()
        {
            System.Func<InventorySwapContext, bool> swapAttempting = _dragManager != null
                ? _dragManager.RaiseSwapAttempting
                : null;
            System.Action<InventorySwapContext> swapCompleted = _dragManager != null
                ? _dragManager.RaiseSwapCompleted
                : null;

            return new InventoryDropHandler(
                _slot,
                _slot?.Inventory,
                _dragManager?.GlobalRules,
                _dragManager?.TransferService,
                policyOverride: null,
                swapAttempting: swapAttempting,
                swapCompleted: swapCompleted);
        }

        public void OnBecomeActiveTarget()
        {
            if (_slot != null)
                _slot.Highlight(true);
        }

        public void OnBecomeInactiveTarget()
        {
            if (_slot != null)
                _slot.Highlight(false);
        }
    }
}
