using DragAndDropSystem.Core;
using DragAndDropSystem.Inventories;
using DragAndDropSystem.Interaction;
using DragAndDropSystem.Slots;
using DragAndDropSystem.Tools;
using UnityEngine;
using UnityEngine.EventSystems;

namespace DragAndDropSystem.UI
{
    /// <summary>
    /// Область дропа, привязанная к инвентарю.
    /// Позволяет дропать предметы в любое место инвентаря, а не только в конкретный слот.
    /// Делегирует обработку дропа в InventoryDropProcessor (planner/executor pipeline).
    /// </summary>
    public class InventoryDropArea : DropAreaBase
    {
        [SerializeField, Tooltip("Инвентарь, к которому привязана эта область")]
        private UniversalInventory _inventory;

        [Header("Visual Feedback")]
        [SerializeField, Tooltip("Подсвечивать область при наведении (если может принять предмет)")]
        private UnityEngine.UI.Image _areaHighlight;

        [SerializeField] private Color _highlightColor = new Color(1f, 1f, 0f, 0.3f);
        [SerializeField] private Color _normalColor = new Color(1f, 1f, 1f, 0f);

        [Header("Drop Policy Override")]
        [SerializeField, Tooltip("Опциональный override policy для этой зоны дропа. Если выключен - используется policy инвентаря.")]
        private DropRequestPolicySettings _dropPolicyOverride = new DropRequestPolicySettings();

        private ISlot _foundSlot;

        public UniversalInventory Inventory => _inventory;

#if UNITY_EDITOR
        // ══════════════════════════════════════════════════════════
        //  Lifecycle overrides
        // ══════════════════════════════════════════════════════════

        protected override void OnValidate()
        {
            base.OnValidate();
            if (_inventory == null)
                _inventory = GetComponentInParent<UniversalInventory>();
        }
#endif

        // ══════════════════════════════════════════════════════════
        //  Gamepad focus
        // ══════════════════════════════════════════════════════════

        public override void OnSelect(BaseEventData eventData)
        {
            base.OnSelect(eventData);
            InputEventRouter.AutoCreateInstance.RouteDropAreaFocusEnter(this, FocusSource.Gamepad);
        }

        public override void OnDeselect(BaseEventData eventData)
        {
            base.OnDeselect(eventData);
            InputEventRouter.AutoCreateInstance.RouteDropAreaFocusExit(this, FocusSource.Gamepad);
        }

        // ══════════════════════════════════════════════════════════
        //  DropAreaBase overrides
        // ══════════════════════════════════════════════════════════

        internal override bool TryActivateAsFocusedTarget()
        {
            if (DragManager == null || !DragManager.IsDragging || _inventory == null)
                return false;

            if (!TryResolveFocusedTargetSlot(out _foundSlot))
                return false;

            DragManager.PushDropTarget(this);
            Extensions.DragAndDropLog($"<color=cyan>[InventoryDropArea] Entered, slot={_foundSlot?.Index.ToString() ?? "AREA"}, inventory={_inventory.name}</color>");
            return true;
        }

        protected override void OnTargetDeactivated()
        {
            _foundSlot = null;
        }

        protected override void OnHighlightChanged(bool highlighted, bool canAccept)
        {
            if (_areaHighlight == null)
                return;
            _areaHighlight.color = highlighted ? _highlightColor : _normalColor;
        }

        // ══════════════════════════════════════════════════════════
        //  IDropTarget overrides (delegated processing)
        // ══════════════════════════════════════════════════════════

        public override ISlot GetTargetSlot() => _foundSlot;

        public override IDropProcessor GetDropProcessor()
        {
            return CreateDropProcessor(_foundSlot);
        }

        // ══════════════════════════════════════════════════════════
        //  Domain logic
        // ══════════════════════════════════════════════════════════

        private InventoryDropProcessor CreateDropProcessor(ISlot targetSlot)
        {
            var boundOverride = _dropPolicyOverride != null ? _dropPolicyOverride.TryBuild() : (DropRequestPolicy?)null;
            System.Func<InventorySwapContext, bool> swapAttempting = DragManager != null
                ? DragManager.RaiseSwapAttempting
                : null;
            System.Action<InventorySwapContext> swapCompleted = DragManager != null
                ? DragManager.RaiseSwapCompleted
                : null;

            return new InventoryDropProcessor(
                targetSlot,
                _inventory,
                DragManager?.GlobalRules,
                boundOverride,
                swapAttempting,
                swapCompleted);
        }

        private bool TryResolveFocusedTargetSlot(out ISlot suggestedSlot)
        {
            suggestedSlot = null;

            var context = DragManager.CurrentContext;
            if (context == null || context.Entries.Count == 0)
                return false;

            if (!TryBuildValidationContext(context, out var validationContext, out suggestedSlot))
                return false;

            var processor = CreateDropProcessor(suggestedSlot);
            bool canAccept = processor.CanAcceptDrop(validationContext);
            if (!canAccept)
                Extensions.DragAndDropLog($"<color=red>[InventoryDropArea] Planner rejected drop in {_inventory.name}</color>");

            return canAccept;
        }

        private bool TryBuildValidationContext(DragContext context, out DragContext validationContext, out ISlot suggestedSlot)
        {
            validationContext = null;
            suggestedSlot = null;

            if (context == null || context.Entries.Count == 0)
                return false;

            if (!context.IsBatchDrag)
            {
                var stack = context.Entries[0].Stack;
                if (stack == null || stack.PrimaryAdapter == null)
                    return false;

                if (!TransferItemConversionUtility.TryResolveTargetItem(context.Entries[0].SourceInventory, _inventory, stack.PrimaryAdapter, out var targetPreviewItem))
                    return false;

                var acceptanceRequest = new InventoryAcceptanceRequest(
                    _inventory,
                    targetPreviewItem,
                    stack.Count,
                    context,
                    context.Entries[0]);

                bool canAccept = _inventory.CanAcceptItem(acceptanceRequest, out suggestedSlot);
                if (!canAccept)
                {
                    Extensions.DragAndDropLog($"<color=red>[InventoryDropArea] Cannot accept itemAdapter in {_inventory.name}</color>");
                    return false;
                }
            }

            validationContext = context.WithTarget(suggestedSlot, _inventory);
            return true;
        }
    }
}
