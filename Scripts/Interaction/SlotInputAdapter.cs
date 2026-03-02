using DragAndDropSystem.Core;
using DragAndDropSystem.Inventories;
using DragAndDropSystem.Slots;
using DragAndDropSystem.Tools;
using UnityEngine;
using UnityEngine.EventSystems;

namespace DragAndDropSystem.Interaction
{
    /// <summary>
    /// Тонкий slot-adapter: пересылает raw-события в InputEventRouter.
    /// Доменную логику не содержит.
    /// </summary>
    public class SlotInputAdapter : MonoBehaviour,
        IPointerEnterHandler, IPointerExitHandler,
        IPointerDownHandler, IPointerUpHandler,
        IBeginDragHandler, IDragHandler,
        ISelectHandler, IDeselectHandler,
        ISubmitHandler, ICancelHandler,
        IDropTarget
    {
        [SerializeField] private UniversalSlot _slot;
        [SerializeField] private InventoryInteractionCoordinator _coordinator;

        public UniversalSlot Slot => _slot;
        public InventoryInteractionCoordinator Coordinator => _coordinator;


        private void Awake()
        {
            if (_slot == null)
                _slot = GetComponent<UniversalSlot>();

            EnsureCoordinator();
        }

        private void OnEnable()
        {
            EnsureCoordinator();
        }

        private void OnDisable()
        {
            if (_slot == null || DragAndDropManager.IsInstanceExist == false)
                return;

            if (DragAndDropManager.Instance.IsDragging)
            {
                DragAndDropManager.Instance.PopDropTarget(this);
            }

            if (_slot.Inventory is UniversalInventory universalInventory)
            {
                universalInventory.NotifyPointerExit(_slot);
            }
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            EnsureCoordinator();

            if (_slot?.Inventory is UniversalInventory universalInventory)
            {
                universalInventory.NotifyPointerEnter(_slot);
            }

            InputEventRouter.Instance.RoutePointerEnter(this, eventData);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            EnsureCoordinator();

            if (_slot?.Inventory is UniversalInventory universalInventory)
            {
                universalInventory.NotifyPointerExit(_slot);
            }

            InputEventRouter.Instance.RoutePointerExit(this, eventData);
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            EnsureCoordinator();

            if (_slot == null)
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
            EnsureCoordinator();
            InputEventRouter.Instance.RoutePointerUp(this, eventData);
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            EnsureCoordinator();
            InputEventRouter.Instance.RouteBeginDrag(this, eventData);
        }

        public void OnDrag(PointerEventData eventData)
        {
            // Intentionally no-op.
            // Unity UI drag pipeline may require IDragHandler for stable BeginDrag dispatch.
        }

        public void OnSelect(BaseEventData eventData)
        {
            EnsureCoordinator();
            InputEventRouter.Instance.RouteFocusEnter(this, FocusSource.Gamepad);
        }

        public void OnDeselect(BaseEventData eventData)
        {
            EnsureCoordinator();
            InputEventRouter.Instance.RouteFocusExit(this, FocusSource.Gamepad);
        }

        public void OnSubmit(BaseEventData eventData)
        {
            EnsureCoordinator();
            InputEventRouter.Instance.RouteSubmit(this, eventData);
        }

        public void OnCancel(BaseEventData eventData)
        {
            EnsureCoordinator();
            InputEventRouter.Instance.RouteCancel(this, eventData);
        }

        private void EnsureCoordinator()
        {
            if (_slot == null)
                _slot = GetComponent<UniversalSlot>();

            if (_coordinator == null)
                _coordinator = GetComponentInParent<InventoryInteractionCoordinator>();
            if (_coordinator == null && _slot?.Inventory is UniversalInventory inventory)
                _coordinator = inventory.GetComponent<InventoryInteractionCoordinator>();
        }

        // ===== IDropTarget =====

        public ISlot GetTargetSlot() => _slot;

        public IItemDropHandler GetDropHandler()
        {
            System.Func<InventorySwapContext, bool> swapAttempting = DragAndDropManager.Instance.RaiseSwapAttempting;
            System.Action<InventorySwapContext> swapCompleted = DragAndDropManager.Instance.RaiseSwapCompleted;

            return new InventoryDropHandler(
                _slot,
                _slot?.Inventory,
                DragAndDropManager.Instance.GlobalRules,
                DragAndDropManager.Instance.TransferService,
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
