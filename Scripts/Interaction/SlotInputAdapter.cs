using System;
using DragAndDropSystem.Core;
using DragAndDropSystem.Inventories;
using DragAndDropSystem.Slots;
using DragAndDropSystem.Tools;
using UnityEngine;
using UnityEngine.Events;
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
        public static event Action<SlotHoverEventArgs> OnAnySlotHoverEnter;
        public static event Action<SlotHoverEventArgs> OnAnySlotHoverExit;

        [SerializeField] private UniversalSlot _slot;
        [Header("Hover Events")]
        [SerializeField, Tooltip("Вызывать hover-события только если слот не пустой")]
        private bool _onlyWhenNotEmpty = true;
        [SerializeField, Tooltip("Игнорировать hover-события во время перетаскивания")]
        private bool _ignoreHoverWhileDragging = false;
        [SerializeField, Tooltip("Локальное событие наведения на слот")]
        private UnityEvent<SlotHoverEventArgs> _onSlotHoverEnter = new();
        [SerializeField, Tooltip("Локальное событие ухода курсора со слота")]
        private UnityEvent<SlotHoverEventArgs> _onSlotHoverExit = new();

        public UniversalSlot Slot => _slot;
        public bool IsHovering { get; private set; }
        public UnityEvent<SlotHoverEventArgs> OnSlotHoverEnter => _onSlotHoverEnter;
        public UnityEvent<SlotHoverEventArgs> OnSlotHoverExit => _onSlotHoverExit;
        public event Action<SlotHoverEventArgs> HoverEntered;
        public event Action<SlotHoverEventArgs> HoverExited;


        private void Awake()
        {
            if (_slot == null)
                _slot = GetComponent<UniversalSlot>();
        }

        private void OnDisable()
        {
            if (_slot == null || DragAndDropManager.IsInstanceExist == false)
            {
                if (IsHovering)
                    ForceHoverExit();
                return;
            }

            if (DragAndDropManager.Instance.IsDragging)
            {
                DragAndDropManager.Instance.PopDropTarget(this);
            }

            if (_slot.Inventory is UniversalInventory universalInventory)
            {
                universalInventory.NotifyPointerExit(_slot);
            }

            if (IsHovering)
                ForceHoverExit();
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (_slot?.Inventory is UniversalInventory universalInventory)
            {
                universalInventory.NotifyPointerEnter(_slot);
            }

            TryRaiseHoverEnter(eventData);
            InputEventRouter.Instance.RoutePointerEnter(this, eventData);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            if (_slot?.Inventory is UniversalInventory universalInventory)
            {
                universalInventory.NotifyPointerExit(_slot);
            }

            TryRaiseHoverExit(eventData);
            InputEventRouter.Instance.RoutePointerExit(this, eventData);
        }

        public void OnPointerDown(PointerEventData eventData)
        {
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
            InputEventRouter.Instance.RoutePointerUp(this, eventData);
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            InputEventRouter.Instance.RouteBeginDrag(this, eventData);
        }

        public void OnDrag(PointerEventData eventData)
        {
            // Intentionally no-op.
            // Unity UI drag pipeline may require IDragHandler for stable BeginDrag dispatch.
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

        public IDropProcessor GetDropProcessor()
        {
            System.Func<InventorySwapContext, bool> swapAttempting = DragAndDropManager.Instance.RaiseSwapAttempting;
            System.Action<InventorySwapContext> swapCompleted = DragAndDropManager.Instance.RaiseSwapCompleted;

            return new InventoryDropProcessor(
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

        public bool GetIsHovering() => IsHovering;

        public void SimulateHoverEnter()
        {
            if (IsHovering)
                return;

            TryRaiseHoverEnter(new PointerEventData(EventSystem.current)
            {
                position = Input.mousePosition
            });
        }

        public void SimulateHoverExit()
        {
            if (!IsHovering)
                return;

            TryRaiseHoverExit(new PointerEventData(EventSystem.current)
            {
                position = Input.mousePosition
            });
        }

        private void TryRaiseHoverEnter(PointerEventData eventData)
        {
            if (!ShouldTriggerHoverEvent() || IsHovering)
                return;

            IsHovering = true;
            var args = CreateHoverEventArgs(eventData, true);

            HoverEntered?.Invoke(args);
            if (args.Cancel)
                return;

            _onSlotHoverEnter?.Invoke(args);
            OnAnySlotHoverEnter?.Invoke(args);
        }

        private void TryRaiseHoverExit(PointerEventData eventData)
        {
            if (!IsHovering)
                return;

            IsHovering = false;
            var args = CreateHoverEventArgs(eventData, false);

            HoverExited?.Invoke(args);
            if (args.Cancel)
                return;

            _onSlotHoverExit?.Invoke(args);
            OnAnySlotHoverExit?.Invoke(args);
        }

        private void ForceHoverExit()
        {
            IsHovering = false;
            var args = CreateHoverEventArgs(null, false);
            HoverExited?.Invoke(args);
            if (args.Cancel)
                return;

            _onSlotHoverExit?.Invoke(args);
            OnAnySlotHoverExit?.Invoke(args);
        }

        private bool ShouldTriggerHoverEvent()
        {
            if (_slot == null || !_slot.IsInteractable)
                return false;

            if (_onlyWhenNotEmpty && _slot.IsEmpty)
                return false;

            if (_ignoreHoverWhileDragging && DragAndDropManager.IsInstanceExist && DragAndDropManager.Instance.IsDragging)
                return false;

            return true;
        }

        private SlotHoverEventArgs CreateHoverEventArgs(PointerEventData eventData, bool isEnter)
        {
            return new SlotHoverEventArgs(
                _slot?.Stack?.Item,
                _slot,
                eventData?.position ?? Vector2.zero,
                GetComponent<RectTransform>(),
                isEnter
            );
        }
    }
}
