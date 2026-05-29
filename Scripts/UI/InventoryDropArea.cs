using UnityEngine;
using UnityEngine.EventSystems;
using UDND.Core;
using UDND.Interaction;
using UDND.Inventories;
using UDND.Slots;
using UDND.Tools;

namespace UDND.UI
{
    /// <summary>
    /// Inventory-bound drop area.
    /// Allows dropping items anywhere inside the inventory, not just onto a specific slot.
    /// Delegates drop handling to InventoryDropProcessor (planner/executor pipeline).
    /// </summary>
    public class InventoryDropArea : DropAreaBase
    {
        [SerializeField, Tooltip("Inventory bound to this area")]
        private UniversalInventory _inventory;

        [Header("Visual Feedback")]
        [SerializeField, Tooltip("Highlight the area on hover (if it can accept the item)")]
        private UnityEngine.UI.Image _areaHighlight;

        [SerializeField] private Color _highlightColor = new Color(1f, 1f, 0f, 0.3f);
        [SerializeField] private Color _normalColor = new Color(1f, 1f, 1f, 0f);

        [Header("Drop Policy Override")]
        [SerializeField, Tooltip("Optional policy override for this drop zone. If disabled, the inventory policy is used.")]
        private DropRequestPolicySettings _dropPolicyOverride = new DropRequestPolicySettings();

        private BaseSlot _foundBaseSlot;

        public IInventory Inventory => _inventory;

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
            InputEventRouter.Instance.RouteDropAreaFocusExit(this, FocusSource.Gamepad);
        }

        // ══════════════════════════════════════════════════════════
        //  DropAreaBase overrides
        // ══════════════════════════════════════════════════════════

        internal override bool TryActivateAsFocusedTarget()
        {
            if (DragManager == null || !DragManager.IsDragging || _inventory == null)
                return false;

            if (!TryResolveFocusedTargetSlot(out _foundBaseSlot))
                return false;

            DragManager.PushDropTarget(this);
            Extensions.DragAndDropLog($"<color=cyan>[InventoryDropArea] Entered, slot={_foundBaseSlot?.Index.ToString() ?? "AREA"}, inventory={_inventory.name}</color>");
            return true;
        }

        protected override void OnTargetDeactivated()
        {
            _foundBaseSlot = null;
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

        public override BaseSlot GetTargetSlot() => _foundBaseSlot;

        public override IDropProcessor GetDropProcessor()
        {
            return CreateDropProcessor(_foundBaseSlot);
        }

        // ══════════════════════════════════════════════════════════
        //  Domain logic
        // ══════════════════════════════════════════════════════════

        private InventoryDropProcessor CreateDropProcessor(BaseSlot targetBaseSlot)
        {
            var boundOverride = _dropPolicyOverride != null ? _dropPolicyOverride.TryBuild() : (DropRequestPolicy?)null;
            System.Func<InventorySwapContext, bool> swapAttempting = DragManager != null
                ? DragManager.RaiseSwapAttempting
                : null;
            System.Action<InventorySwapContext> swapCompleted = DragManager != null
                ? DragManager.RaiseSwapCompleted
                : null;

            return new InventoryDropProcessor(
                targetBaseSlot,
                _inventory,
                DragManager?.GlobalRules,
                boundOverride,
                swapAttempting,
                swapCompleted);
        }

        private bool TryResolveFocusedTargetSlot(out BaseSlot suggestedBaseSlot)
        {
            suggestedBaseSlot = null;

            var context = DragManager.CurrentContext;
            if (context == null || context.Entries.Count == 0)
                return false;

            if (!TryBuildValidationContext(context, out var validationContext, out suggestedBaseSlot))
                return false;

            var processor = CreateDropProcessor(suggestedBaseSlot);
            bool canAccept = processor.CanAcceptDrop(validationContext);
            if (!canAccept)
                Extensions.DragAndDropLog($"<color=red>[InventoryDropArea] Planner rejected drop in {_inventory.name}</color>");

            return canAccept;
        }

        private bool TryBuildValidationContext(DragContext context, out DragContext validationContext, out BaseSlot suggestedBaseSlot)
        {
            validationContext = null;
            suggestedBaseSlot = null;

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

                bool canAccept = _inventory.CanAcceptItem(acceptanceRequest, out suggestedBaseSlot);
                if (!canAccept)
                {
                    Extensions.DragAndDropLog($"<color=red>[InventoryDropArea] Cannot accept itemAdapter in {_inventory.name}</color>");
                    return false;
                }
            }

            validationContext = context.WithTarget(suggestedBaseSlot, _inventory);
            return true;
        }
    }
}
