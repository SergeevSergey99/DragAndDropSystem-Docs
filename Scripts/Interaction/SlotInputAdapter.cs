using System;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.Serialization;
using UnityEngine.UI;
using UDND.Core;
using UDND.Inventories;
using UDND.Slots;
using UDND.Tools;

namespace UDND.Interaction
{
    /// <summary>
    /// Thin slot adapter: forwards raw events to InputEventRouter.
    /// Contains no domain logic.
    /// </summary>
    public class SlotInputAdapter : Selectable,
        IBeginDragHandler, IDropTarget
    {
        // Global slot-hover events live on UDNDEvents (UDNDEvents.OnAnySlotHoverEnter/Exit).

        [FormerlySerializedAs("_slot")] [SerializeField] private BaseSlot baseSlot;
        [Header("Pointer Down")]
        [SerializeField, Tooltip("Call base.OnPointerDown (sets EventSystem.selectedGameObject). Enable if you need Selectable transitions on mouse press.")]
        private bool _callBaseOnPointerDown = false;
        [Header("Hover Events")]
        [SerializeField, Tooltip("Raise hover events only if the slot is not empty")]
        private bool _onlyWhenNotEmpty = true;
        [SerializeField, Tooltip("Ignore hover events during dragging")]
        private bool _ignoreHoverWhileDragging = false;
        [SerializeField, Tooltip("Local slot hover event")]
        private UnityEvent _onSlotHoverEnter = new();
        [SerializeField, Tooltip("Local slot hover exit event")]
        private UnityEvent _onSlotHoverExit = new();

        public BaseSlot BaseSlot => baseSlot;
        public bool IsHovering { get; private set; }
        
        public UnityEvent OnSlotHoverEnter => _onSlotHoverEnter;
        public UnityEvent OnSlotHoverExit => _onSlotHoverExit;


        protected override void Awake()
        {
            base.Awake();
            if (baseSlot == null)
                baseSlot = GetComponent<BaseSlot>();

            // Ensure navigation is Automatic after base class change from MonoBehaviour to Selectable.
            // Prefabs serialized before the change may have default(Navigation) = None.
            if (navigation.mode == Navigation.Mode.None)
            {
                var nav = navigation;
                nav.mode = Navigation.Mode.Automatic;
                navigation = nav;
            }
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            if (baseSlot == null || DragAndDropManager.IsInstanceExist == false)
            {
                if (IsHovering)
                    ForceHoverExit();
                return;
            }

            if (DragAndDropManager.AutoCreateInstance.IsDragging)
            {
                DragAndDropManager.AutoCreateInstance.PopDropTarget(this);
            }

            if (baseSlot.Inventory is IInventoryInteraction interactionFeedback)
            {
                interactionFeedback.NotifyPointerExit(baseSlot);
            }

            if (IsHovering)
                ForceHoverExit();
        }

        public override void OnPointerEnter(PointerEventData eventData)
        {
            base.OnPointerEnter(eventData);
            if (baseSlot?.Inventory is IInventoryInteraction interactionFeedback)
            {
                interactionFeedback.NotifyPointerEnter(baseSlot);
            }

            TryRaiseHoverEnter(eventData);
            InputEventRouter.AutoCreateInstance.RoutePointerEnter(this, eventData);
        }

        public override void OnPointerExit(PointerEventData eventData)
        {
            base.OnPointerExit(eventData);
            if (baseSlot?.Inventory is IInventoryInteraction interactionFeedback)
            {
                interactionFeedback.NotifyPointerExit(baseSlot);
            }

            TryRaiseHoverExit(eventData);
            InputEventRouter.AutoCreateInstance.RoutePointerExit(this, eventData);
        }

        public override void OnPointerDown(PointerEventData eventData)
        {
            // By default we do not call base.OnPointerDown because it invokes EventSystem.SetSelectedGameObject,
            // which is not needed for mouse input. Selection is driven only through navigation
            // (OnSelect/OnDeselect for gamepad/keyboard).
            // Enable it through _callBaseOnPointerDown if you need Selectable transitions on press.
            if (_callBaseOnPointerDown)
                base.OnPointerDown(eventData);

            if (baseSlot == null)
                return;

            if (!baseSlot.IsInteractable)
            {
                Extensions.DragAndDropLog($"OnPointerDown blocked - slot {name} is not interactable (filtered)");
                return;
            }

            if (baseSlot.Inventory is IInventoryInteraction interactionFeedback)
            {
                interactionFeedback.NotifySlotInteracted(baseSlot);
            }

            InputEventRouter.AutoCreateInstance.RoutePointerDown(this, eventData);
        }

        public override void OnPointerUp(PointerEventData eventData)
        {
            base.OnPointerUp(eventData);
            InputEventRouter.AutoCreateInstance.RoutePointerUp(this, eventData);
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            InputEventRouter.AutoCreateInstance.RouteBeginDrag(this, eventData);
        }

        public override void OnMove(AxisEventData eventData)
        {
            var next = eventData.moveDir switch
            {
                MoveDirection.Left => FindSelectableOnLeft(),
                MoveDirection.Right => FindSelectableOnRight(),
                MoveDirection.Up => FindSelectableOnUp(),
                MoveDirection.Down => FindSelectableOnDown(),
                _ => null
            };
            Extensions.DragAndDropLog($"OnMove: {name}, dir={eventData.moveDir}, found={next?.name ?? "NULL"}, allSelectables={Selectable.allSelectableCount}");
            base.OnMove(eventData);
        }

        public override void OnSelect(BaseEventData eventData)
        {
            base.OnSelect(eventData);
            Extensions.DragAndDropLog($"OnSelect: {name}, nav mode: {navigation.mode}");
            InputEventRouter.AutoCreateInstance.RouteFocusEnter(this, FocusSource.Gamepad);
        }

        public override void OnDeselect(BaseEventData eventData)
        {
            base.OnDeselect(eventData);
            InputEventRouter.AutoCreateInstance.RouteFocusExit(this, FocusSource.Gamepad);
        }

        // ===== IDropTarget =====

        public BaseSlot GetTargetSlot() => baseSlot;

        public IDropProcessor GetDropProcessor()
        {
            System.Func<InventorySwapContext, bool> swapAttempting = DragAndDropManager.AutoCreateInstance.RaiseSwapAttempting;
            System.Action<InventorySwapContext> swapCompleted = DragAndDropManager.AutoCreateInstance.RaiseSwapCompleted;

            return new InventoryDropProcessor(
                baseSlot,
                baseSlot?.Inventory,
                DragAndDropManager.AutoCreateInstance.GlobalRules,
                swapAttempting: swapAttempting,
                swapCompleted: swapCompleted);
        }

        public void OnBecomeActiveTarget()
        {
            if (baseSlot == null)
                return;

            if (baseSlot.Inventory is IInventoryInteraction interactionFeedback &&
                interactionFeedback.ShowDropPreview(baseSlot, DragAndDropManager.AutoCreateInstance.CurrentContext))
                return;

            baseSlot.Highlight(true);
        }

        public void OnBecomeInactiveTarget()
        {
            if (baseSlot == null)
                return;

            if (baseSlot.Inventory is IInventoryInteraction interactionFeedback)
                interactionFeedback.ClearDropPreview();

            baseSlot.Highlight(false);
        }

        private void TryRaiseHoverEnter(PointerEventData eventData)
        {
            if (!ShouldTriggerHoverEvent() || IsHovering)
                return;

            IsHovering = true;
            var args = CreateHoverEventArgs(eventData, true);

            if (args.Cancel)
                return;

            _onSlotHoverEnter?.Invoke();
            UDNDEvents.RaiseAnySlotHoverEnter(args);
        }

        private void TryRaiseHoverExit(PointerEventData eventData)
        {
            if (!IsHovering)
                return;

            IsHovering = false;
            var args = CreateHoverEventArgs(eventData, false);

            if (args.Cancel)
                return;

            _onSlotHoverExit?.Invoke();
            UDNDEvents.RaiseAnySlotHoverExit(args);
        }

        private void ForceHoverExit()
        {
            IsHovering = false;
            var args = CreateHoverEventArgs(null, false);
            
            if (args.Cancel)
                return;

            _onSlotHoverExit?.Invoke();
            UDNDEvents.RaiseAnySlotHoverExit(args);
        }

        private bool ShouldTriggerHoverEvent()
        {
            if (baseSlot == null || !baseSlot.IsInteractable)
                return false;

            if (_onlyWhenNotEmpty && baseSlot.IsEmpty)
                return false;

            if (_ignoreHoverWhileDragging && DragAndDropManager.IsInstanceExist && DragAndDropManager.AutoCreateInstance.IsDragging)
                return false;

            return true;
        }

        private SlotHoverEventArgs CreateHoverEventArgs(PointerEventData eventData, bool isEnter)
        {
            return new SlotHoverEventArgs(
                baseSlot?.Stack?.PrimaryAdapter,
                baseSlot,
                eventData?.position ?? Vector2.zero,
                GetComponent<RectTransform>(),
                isEnter
            );
        }
    }
}
