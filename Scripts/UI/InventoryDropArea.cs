using UnityEngine;
using UnityEngine.EventSystems;
using UDND.Core;
using UDND.Interaction;
using UDND.Inventories;
using UDND.Slots;
using UDND.Tools;
using UDND.Tools.Inspector;

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
        private BaseInventory _inventory;

        [Header("Visual Feedback")]
        [SerializeField, Tooltip("Highlight the area on hover (if it can accept the item)")]
        private UnityEngine.UI.Image _areaHighlight;

        [SerializeField] private Color _highlightColor = new Color(1f, 1f, 0f, 0.3f);
        [SerializeField] private Color _normalColor = new Color(1f, 1f, 1f, 0f);

        [Header("Drop Policy Override")]
        [SerializeField, Tooltip("Optional policy override for this drop zone. If disabled, the inventory policy is used.")]
        private DropRequestPolicySettings _dropPolicyOverride = new DropRequestPolicySettings();

        [Header("Slot Selection Policy")]
        [SerializeReference, ManagedReferencePicker, InlineProperty, HideLabel,
         Tooltip("How the system picks a target slot when no explicit slot is chosen. Null = strategy default.")]
        private SlotSelectionPolicyBase _slotSelectionPolicy;

        private SlotSelection _foundSelection;

        public IInventory Inventory => _inventory;

#if UNITY_EDITOR
        // ══════════════════════════════════════════════════════════
        //  Lifecycle overrides
        // ══════════════════════════════════════════════════════════

        protected override void OnValidate()
        {
            base.OnValidate();
            if (_inventory == null)
                _inventory = GetComponentInParent<BaseInventory>();
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

            if (!TryResolveFocusedTargetSlot(out _foundSelection))
                return false;

            DragManager.PushDropTarget(this);
            var slotIndex = (_foundSelection.Slot as BaseSlot)?.Index.ToString() ?? (_foundSelection.CreateNew ? "NEW" : "AREA");
            Extensions.DragAndDropLog($"<color=cyan>[InventoryDropArea] Entered, slot={slotIndex}, inventory={_inventory.name}</color>");
            return true;
        }

        protected override void OnTargetDeactivated()
        {
            _foundSelection = SlotSelection.None;
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

        public override BaseSlot GetTargetSlot() => _foundSelection.Slot as BaseSlot;

        public override IDropProcessor GetDropProcessor()
        {
            return CreateDropProcessor(_foundSelection.Slot as BaseSlot);
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
                swapCompleted,
                _slotSelectionPolicy);
        }

        private bool TryResolveFocusedTargetSlot(out SlotSelection selection)
        {
            selection = SlotSelection.None;

            var context = DragManager.CurrentContext;
            if (context == null || context.Entries.Count == 0)
                return false;

            if (!TryBuildValidationContext(context, out var validationContext, out selection))
                return false;

            var processor = CreateDropProcessor(selection.Slot as BaseSlot);
            bool canAccept = processor.CanAcceptDrop(validationContext);
            if (!canAccept)
                Extensions.DragAndDropLog($"<color=red>[InventoryDropArea] Planner rejected drop in {_inventory.name}</color>");

            return canAccept;
        }

        private bool TryBuildValidationContext(DragContext context, out DragContext validationContext, out SlotSelection selection)
        {
            validationContext = null;
            selection = SlotSelection.None;

            if (context == null || context.Entries.Count == 0)
                return false;

            var firstEntry = context.Entries[0];
            var stack = firstEntry.Stack;
            if (stack == null || stack.PrimaryAdapter == null)
                return false;

            if (!TransferItemConversionUtility.TryResolveTargetItem(firstEntry.SourceInventory, _inventory, stack.PrimaryAdapter, out var targetPreviewItem))
                return false;

            var acceptanceRequest = new InventoryAcceptanceRequest(
                _inventory,
                targetPreviewItem,
                stack.Count,
                context,
                firstEntry,
                _slotSelectionPolicy);

            bool canAccept = _inventory.CanAcceptItem(acceptanceRequest, out BaseSlot suggestedBaseSlot);
            if (!canAccept)
            {
                Extensions.DragAndDropLog($"<color=red>[InventoryDropArea] Cannot accept itemAdapter in {_inventory.name}</color>");
                return false;
            }

            selection = suggestedBaseSlot != null
                ? SlotSelection.Existing(suggestedBaseSlot)
                : SlotSelection.New();

            validationContext = context.WithTarget(suggestedBaseSlot, _inventory);
            return true;
        }
    }
}
