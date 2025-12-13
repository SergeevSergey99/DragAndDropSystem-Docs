using DragAndDropSystem.Core;
using DragAndDropSystem.Rules;
using DragAndDropSystem.Slots;
using DragAndDropSystem.Tools;

namespace DragAndDropSystem.Inventories
{
    /// <summary>
    /// Drop handler for inventory-based targets (slots and inventory areas).
    /// Encapsulates 3-tier rule validation and delegates to InventoryTransferService.
    /// </summary>
    public class InventoryDropHandler : IItemDropHandler
    {
        private readonly ISlot _targetSlot;
        private readonly IInventory _targetInventory;
        private readonly GlobalRuleValidator _globalRules;
        private readonly InventoryTransferService _transferService;

        /// <summary>
        /// Create a handler for a specific slot
        /// </summary>
        public InventoryDropHandler(
            ISlot targetSlot,
            IInventory targetInventory,
            GlobalRuleValidator globalRules,
            InventoryTransferService transferService)
        {
            _targetSlot = targetSlot;
            _targetInventory = targetInventory;
            _globalRules = globalRules;
            _transferService = transferService;
        }

        /// <summary>
        /// Create a handler for an inventory area (no specific slot)
        /// </summary>
        public InventoryDropHandler(
            IInventory targetInventory,
            GlobalRuleValidator globalRules,
            InventoryTransferService transferService)
            : this(null, targetInventory, globalRules, transferService)
        {
        }

        public bool CanAcceptDrop(DragContext context)
        {
            if (context == null || _targetInventory == null)
            {
                Extentions.DragAndDropLog("<color=red>[InventoryDropHandler] CanAcceptDrop: null context or inventory</color>");
                return false;
            }

            // Update context with our target info
            context.SetTarget(_targetSlot, _targetInventory);

            // 1. Global rules
            if (_globalRules != null)
            {
                var globalResult = _globalRules.ValidateDrop(context);
                if (!globalResult.IsValid)
                {
                    Extentions.DragAndDropLog($"<color=red>[InventoryDropHandler] Global rule failed: {globalResult.FailureReason}</color>");
                    return false;
                }
            }

            // 2. Inventory rules
            if (_targetInventory is UniversalInventory universalInventory)
            {
                var inventoryResult = universalInventory.RuleValidator.ValidateDrop(context);
                if (!inventoryResult.IsValid)
                {
                    Extentions.DragAndDropLog($"<color=red>[InventoryDropHandler] Inventory rule failed: {inventoryResult.FailureReason}</color>");
                    return false;
                }
            }

            // 3. Slot rules (only if we have a specific target slot)
            if (_targetSlot?.SlotRuleValidator != null)
            {
                var slotResult = _targetSlot.SlotRuleValidator.ValidateDrop(context);
                if (!slotResult.IsValid)
                {
                    Extentions.DragAndDropLog($"<color=red>[InventoryDropHandler] Slot rule failed: {slotResult.FailureReason}</color>");
                    return false;
                }
            }

            Extentions.DragAndDropLog("<color=green>[InventoryDropHandler] CanAcceptDrop: Success!</color>");
            return true;
        }

        public DropResult HandleDrop(DragContext context)
        {
            if (context == null)
            {
                return DropResult.Failed("Null drag context");
            }

            var source = context.SourceInventory;
            var sourceSlot = context.SourceSlot;
            var draggedStack = context.DraggedStack;

            if (source == null || sourceSlot == null || draggedStack == null)
            {
                Extentions.DragAndDropLog("<color=red>[InventoryDropHandler] HandleDrop: Invalid context</color>");
                return DropResult.Failed("Invalid drag context");
            }

            if (_targetInventory == null)
            {
                return DropResult.Failed("Target inventory is null");
            }

            // Update context with target
            context.SetTarget(_targetSlot, _targetInventory);

            Extentions.DragAndDropLog($"<color=yellow>[InventoryDropHandler] HandleDrop: {draggedStack.Count}x {draggedStack.Item.DisplayName} | TargetSlot={_targetSlot?.Index.ToString() ?? "AREA"}</color>");

            var request = new InventoryTransferRequest(
                source,
                sourceSlot,
                _targetInventory,
                _targetSlot,
                draggedStack,
                allowAlternativeSlots: true);

            if (!_transferService.TryExecuteTransfer(request, out var outcome))
            {
                Extentions.DragAndDropLog("<color=red>[InventoryDropHandler] Transfer failed</color>");
                return DropResult.Failed("Transfer failed");
            }

            // Update context with actual target slot (may differ from requested)
            if (outcome.TargetSlot != null)
            {
                context.SetTarget(outcome.TargetSlot, outcome.TargetInventory);
            }

            // Note: Events and HandleSlotEmptied are handled by DragAndDropManager

            Extentions.DragAndDropLog($"<color=green>[InventoryDropHandler] Transferred {outcome.Amount} items successfully</color>");

            return DropResult.Succeeded(
                item: outcome.Item,
                amount: outcome.Amount,
                targetSlot: outcome.TargetSlot,
                targetInventory: outcome.TargetInventory,
                isPartialTransfer: outcome.IsPartialTransfer,
                remainingInSource: outcome.RemainingInSource);
        }
    }
}
