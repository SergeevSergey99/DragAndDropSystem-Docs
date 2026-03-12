using System;
using DragAndDropSystem.Core;
using DragAndDropSystem.Inventories;
using DragAndDropSystem.Slots;
using DragAndDropSystem.Tools;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace DragAndDropSystem.Interaction
{
    /// <summary>
    /// Тонкий slot-adapter: пересылает raw-события в InputEventRouter.
    /// Доменную логику не содержит.
    /// </summary>
    public class SlotInputAdapter : Selectable,
        IBeginDragHandler, IDropTarget
    {
        public static event Action<SlotHoverEventArgs> OnAnySlotHoverEnter;
        public static event Action<SlotHoverEventArgs> OnAnySlotHoverExit;

        [SerializeField] private UniversalSlot _slot;
        [Header("Pointer Down")]
        [SerializeField, Tooltip("Вызывать base.OnPointerDown (устанавливает EventSystem.selectedGameObject). Включить, если нужны Selectable transitions при нажатии мышью.")]
        private bool _callBaseOnPointerDown = false;
        [Header("Hover Events")]
        [SerializeField, Tooltip("Вызывать hover-события только если слот не пустой")]
        private bool _onlyWhenNotEmpty = true;
        [SerializeField, Tooltip("Игнорировать hover-события во время перетаскивания")]
        private bool _ignoreHoverWhileDragging = false;
        [SerializeField, Tooltip("Локальное событие наведения на слот")]
        private UnityEvent _onSlotHoverEnter = new();
        [SerializeField, Tooltip("Локальное событие ухода курсора со слота")]
        private UnityEvent _onSlotHoverExit = new();

        public UniversalSlot Slot => _slot;
        public bool IsHovering { get; private set; }
        
        public UnityEvent OnSlotHoverEnter => _onSlotHoverEnter;
        public UnityEvent OnSlotHoverExit => _onSlotHoverExit;


        protected override void Awake()
        {
            base.Awake();
            if (_slot == null)
                _slot = GetComponent<UniversalSlot>();

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

        public override void OnPointerEnter(PointerEventData eventData)
        {
            base.OnPointerEnter(eventData);
            if (_slot?.Inventory is UniversalInventory universalInventory)
            {
                universalInventory.NotifyPointerEnter(_slot);
            }

            TryRaiseHoverEnter(eventData);
            InputEventRouter.Instance.RoutePointerEnter(this, eventData);
        }

        public override void OnPointerExit(PointerEventData eventData)
        {
            base.OnPointerExit(eventData);
            if (_slot?.Inventory is UniversalInventory universalInventory)
            {
                universalInventory.NotifyPointerExit(_slot);
            }

            TryRaiseHoverExit(eventData);
            InputEventRouter.Instance.RoutePointerExit(this, eventData);
        }

        public override void OnPointerDown(PointerEventData eventData)
        {
            // По умолчанию не вызываем base.OnPointerDown — он делает EventSystem.SetSelectedGameObject,
            // что не нужно при работе мышью. Selection управляется только через navigation
            // (OnSelect/OnDeselect для gamepad/keyboard).
            // Включается через _callBaseOnPointerDown, если нужны Selectable transitions при нажатии.
            if (_callBaseOnPointerDown)
                base.OnPointerDown(eventData);

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

        public override void OnPointerUp(PointerEventData eventData)
        {
            base.OnPointerUp(eventData);
            InputEventRouter.Instance.RoutePointerUp(this, eventData);
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            InputEventRouter.Instance.RouteBeginDrag(this, eventData);
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
            Extentions.DragAndDropLog($"OnMove: {name}, dir={eventData.moveDir}, found={next?.name ?? "NULL"}, allSelectables={Selectable.allSelectableCount}");
            base.OnMove(eventData);
        }

        public override void OnSelect(BaseEventData eventData)
        {
            base.OnSelect(eventData);
            Extentions.DragAndDropLog($"OnSelect: {name}, nav mode: {navigation.mode}");
            InputEventRouter.Instance.RouteFocusEnter(this, FocusSource.Gamepad);
        }

        public override void OnDeselect(BaseEventData eventData)
        {
            base.OnDeselect(eventData);
            InputEventRouter.Instance.RouteFocusExit(this, FocusSource.Gamepad);
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

        private void TryRaiseHoverEnter(PointerEventData eventData)
        {
            if (!ShouldTriggerHoverEvent() || IsHovering)
                return;

            IsHovering = true;
            var args = CreateHoverEventArgs(eventData, true);

            if (args.Cancel)
                return;

            _onSlotHoverEnter?.Invoke();
            OnAnySlotHoverEnter?.Invoke(args);
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
            OnAnySlotHoverExit?.Invoke(args);
        }

        private void ForceHoverExit()
        {
            IsHovering = false;
            var args = CreateHoverEventArgs(null, false);
            
            if (args.Cancel)
                return;

            _onSlotHoverExit?.Invoke();
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
