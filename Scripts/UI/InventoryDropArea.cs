using DragAndDropSystem.Core;
using DragAndDropSystem.Inventories;
using DragAndDropSystem.Interaction;
using DragAndDropSystem.Slots;
using DragAndDropSystem.Tools;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace DragAndDropSystem.UI
{
    /// <summary>
    /// Компонент для области дропа инвентаря
    /// Позволяет дропать предметы в любое место инвентаря, а не только в конкретный слот
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public class InventoryDropArea : Selectable, IDropTarget, ISubmitHandler, ICancelHandler
    {
        private DragAndDropManager _dragManager => DragAndDropManager.IsInstanceExist ? DragAndDropManager.Instance : null;

        [SerializeField, Tooltip("Инвентарь, к которому привязана эта область")]
        private UniversalInventory _inventory;

        [Header("Visual Feedback")]
        [SerializeField, Tooltip("Подсвечивать область при наведении (если может принять предмет)")]
        private UnityEngine.UI.Image _areaHighlight;

        [SerializeField] private Color _highlightColor = new Color(1f, 1f, 0f, 0.3f);
        [SerializeField] private Color _normalColor = new Color(1f, 1f, 1f, 0f);

        [Header("Drop Policy Override")]
        [SerializeField, Tooltip("Опциональный override policy для этой зоны дропа. Если выключен - используется policy инвентаря.")]
        private DropPolicySettings _dropPolicyOverride = new DropPolicySettings();

        private ISlot _foundSlot;
        private bool _isHighlighted;
        private UnityEngine.UI.Graphic _raycastGraphic;

        public UniversalInventory Inventory => _inventory;

        private void OnValidate()
        {
            // Автоматически находим инвентарь на этом объекте или родителе
            if (_inventory == null)
            {
                _inventory = GetComponentInParent<UniversalInventory>();
            }

            if (_raycastGraphic == null)
            {
                _raycastGraphic = GetComponent<UnityEngine.UI.Graphic>();
            }
        }

        protected override void Awake()
        {
            base.Awake();

            if (navigation.mode == Navigation.Mode.None)
            {
                var nav = navigation;
                nav.mode = Navigation.Mode.Automatic;
                navigation = nav;
            }

            if (_raycastGraphic == null)
            {
                _raycastGraphic = GetComponent<UnityEngine.UI.Graphic>();
            }
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            SubscribeToStateEvents();
            RefreshInteractionState();
        }

        public override void OnPointerEnter(PointerEventData eventData)
        {
            base.OnPointerEnter(eventData);

            if (_dragManager == null || !_dragManager.IsDragging || _inventory == null)
                return;

            TryActivateAsFocusedTarget();
        }

        /// <summary>
        /// Подсветить/снять подсветку области
        /// </summary>
        private void HighlightArea(bool highlight)
        {
            if (_areaHighlight == null)
                return;

            _isHighlighted = highlight;
            _areaHighlight.color = highlight ? _highlightColor : _normalColor;
        }

        public override void OnPointerExit(PointerEventData eventData)
        {
            base.OnPointerExit(eventData);

            if (_dragManager == null || !_dragManager.IsDragging)
                return;

            // Удаляем себя из стека целей
            _dragManager.PopDropTarget(this);

            _foundSlot = null;

            Extentions.DragAndDropLog($"<color=cyan>[InventoryDropArea] Exited</color>");
        }

        protected override void OnDisable()
        {
            UnsubscribeFromStateEvents();
            base.OnDisable();
            if (!DragAndDropManager.IsInstanceExist) return;
            // Удаляем себя из стека при отключении
            if (_dragManager != null && _dragManager.IsDragging)
            {
                _dragManager.PopDropTarget(this);
            }
        }

        public override void OnSelect(BaseEventData eventData)
        {
            base.OnSelect(eventData);
            InputEventRouter.Instance.RouteDropAreaFocusEnter(this, FocusSource.Gamepad);
        }

        public override void OnDeselect(BaseEventData eventData)
        {
            base.OnDeselect(eventData);
            InputEventRouter.Instance.RouteDropAreaFocusExit(this, FocusSource.Gamepad);
        }

        public void OnSubmit(BaseEventData eventData)
        {
            InputEventRouter.Instance.RouteSubmit(this, eventData);
        }

        public void OnCancel(BaseEventData eventData)
        {
            InputEventRouter.Instance.RouteCancel(this, eventData);
        }

        public bool TryActivateAsFocusedTarget()
        {
            if (_dragManager == null || !_dragManager.IsDragging || _inventory == null)
                return false;

            if (!TryResolveFocusedTargetSlot(out _foundSlot))
                return false;

            _dragManager.PushDropTarget(this);
            Extentions.DragAndDropLog($"<color=cyan>[InventoryDropArea] Entered, slot={_foundSlot?.Index.ToString() ?? "AREA"}, inventory={_inventory.name}</color>");
            return true;
        }

        // ===== IDropTarget Implementation =====

        public ISlot GetTargetSlot() => _foundSlot;

        public IDropProcessor GetDropProcessor()
        {
            System.Func<InventorySwapContext, bool> swapAttempting = _dragManager != null
                ? _dragManager.RaiseSwapAttempting
                : null;
            System.Action<InventorySwapContext> swapCompleted = _dragManager != null
                ? _dragManager.RaiseSwapCompleted
                : null;

            return new InventoryDropProcessor(
                _foundSlot,
                _inventory,
                _dragManager?.GlobalRules,
                _dragManager?.TransferService,
                _dropPolicyOverride?.BuildOrNull(),
                swapAttempting,
                swapCompleted);
        }

        public void OnBecomeActiveTarget()
        {
            // Подсвечиваем область когда становимся активной целью
            HighlightArea(true);
        }

        public void OnBecomeInactiveTarget()
        {
            // Снимаем подсветку когда перестаём быть активной целью
            HighlightArea(false);
        }

        private bool TryResolveFocusedTargetSlot(out ISlot suggestedSlot)
        {
            suggestedSlot = null;

            var context = _dragManager.CurrentContext;
            if (context == null || context.Entries.Count == 0)
                return false;

            var stack = context.Entries[0].Stack;
            if (stack == null || stack.Item == null)
                return false;

            bool canAccept = _inventory.CanAcceptItem(stack.Item, stack.Count, out suggestedSlot);
            if (!canAccept)
            {
                Extentions.DragAndDropLog($"<color=red>[InventoryDropArea] Cannot accept item in {_inventory.name}</color>");
                return false;
            }

            return true;
        }

        private bool ShouldAllowInteraction()
        {
            if (_dragManager == null || !_dragManager.IsDragging)
                return false;

            return true;
        }

        private void SubscribeToStateEvents()
        {
            DragAndDropManager.Instance.OnDragStarted += HandleDragStateChanged;
            DragAndDropManager.Instance.OnDragCancelled += HandleDragStateChanged;
            DragAndDropManager.Instance.OnDropCompleted += HandleDragStateChanged;

            InputEventRouter.Instance.OnNavigationModeChanged += HandleNavigationModeChanged;
        }

        private void UnsubscribeFromStateEvents()
        {
            if (DragAndDropManager.IsInstanceExist)
            {
                DragAndDropManager.Instance.OnDragStarted -= HandleDragStateChanged;
                DragAndDropManager.Instance.OnDragCancelled -= HandleDragStateChanged;
                DragAndDropManager.Instance.OnDropCompleted -= HandleDragStateChanged;
            }

            if (InputEventRouter.IsInstanceExist)
                InputEventRouter.Instance.OnNavigationModeChanged -= HandleNavigationModeChanged;
        }

        private void HandleDragStateChanged(DragContext _) => RefreshInteractionState();
        private void HandleNavigationModeChanged(bool _) => RefreshInteractionState();

        private void RefreshInteractionState()
        {
            if (_raycastGraphic != null)
                _raycastGraphic.raycastTarget = _dragManager != null && _dragManager.IsDragging;

            bool shouldBeInteractable = ShouldAllowInteraction();
            if (interactable == shouldBeInteractable)
                return;

            interactable = shouldBeInteractable;

            if (!shouldBeInteractable &&
                EventSystem.current != null &&
                EventSystem.current.currentSelectedGameObject == gameObject)
            {
                EventSystem.current.SetSelectedGameObject(null);
            }
        }
    }
}
