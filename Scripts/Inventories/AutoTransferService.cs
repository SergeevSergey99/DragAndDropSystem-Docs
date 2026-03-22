using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using DragAndDropSystem.Core;
using DragAndDropSystem.Rules;
using DragAndDropSystem.Slots;

namespace DragAndDropSystem.Inventories
{
    /// <summary>
    /// Builds and executes auto-transfer operations through the same planner/executor
    /// pipeline used by drag-and-drop handlers.
    /// </summary>
    public sealed class AutoTransferService
    {
        public bool TryCreateContext(
            IReadOnlyList<ISlot> sourceSlots,
            IInventory sourceInventory,
            IInventory targetInventory,
            out DragContext context,
            out string failureReason)
        {
            context = null;
            failureReason = null;

            if (sourceSlots == null || sourceSlots.Count == 0)
            {
                failureReason = "Source slots are empty";
                return false;
            }

            if (sourceInventory == null || targetInventory == null)
            {
                failureReason = "Source or target inventory is null";
                return false;
            }

            var entries = new List<DragEntry>(sourceSlots.Count);
            var seen = new HashSet<ISlot>();

            for (int i = 0; i < sourceSlots.Count; i++)
            {
                var slot = sourceSlots[i];
                if (slot == null || slot.IsEmpty || slot.Inventory == null || !slot.IsInteractable)
                    continue;

                if (!ReferenceEquals(slot.Inventory, sourceInventory))
                    continue;

                if (!seen.Add(slot))
                    continue;

                int dragAmount = slot.Inventory.GetDragAmount(slot);
                if (dragAmount <= 0)
                    continue;

                var item = slot.Stack?.Item;
                if (item == null)
                    continue;

                entries.Add(new DragEntry(new ItemStack(item, dragAmount), slot, sourceInventory));
            }

            if (entries.Count == 0)
            {
                failureReason = "No valid source slots for auto-transfer";
                return false;
            }

            context = new DragContext(entries)
            {
                TargetInventory = targetInventory
            };

            return true;
        }

        public DropResult Execute(
            DragContext context,
            IInventory targetInventory,
            GlobalRuleValidator globalRules,
            InventoryTransferService transferService,
            System.Func<InventorySwapContext, bool> swapAttempting,
            System.Action<InventorySwapContext> swapCompleted,
            out TransferExecutionSummary executionSummary,
            DropPolicy policyOverride = null)
        {
            executionSummary = null;

            if (context == null)
                return DropResult.Failed("Auto-transfer context is null");

            if (targetInventory == null)
                return DropResult.Failed("Auto-transfer target inventory is null");

            var handler = new InventoryDropProcessor(
                targetSlot: null,
                targetInventory: targetInventory,
                globalRules: globalRules,
                transferService: transferService,
                policyOverride: policyOverride,
                swapAttempting: swapAttempting,
                swapCompleted: swapCompleted);

            if (!handler.CanAcceptDrop(context))
                return DropResult.Failed("Auto-transfer plan rejected");

            executionSummary = handler.ProcessDropWithSummary(context);
            return executionSummary.DropResult;
        }

        public async Task<DropResult> ExecuteAsync(
            DragContext context,
            IInventory targetInventory,
            GlobalRuleValidator globalRules,
            InventoryTransferService transferService,
            System.Func<InventorySwapContext, bool> swapAttempting,
            System.Action<InventorySwapContext> swapCompleted,
            CancellationToken cancellationToken,
            System.Action<TransferExecutionSummary> summarySink = null,
            DropPolicy policyOverride = null)
        {
            if (context == null)
                return DropResult.Failed("Auto-transfer context is null");

            if (targetInventory == null)
                return DropResult.Failed("Auto-transfer target inventory is null");

            var handler = new InventoryDropProcessor(
                targetSlot: null,
                targetInventory: targetInventory,
                globalRules: globalRules,
                transferService: transferService,
                policyOverride: policyOverride,
                swapAttempting: swapAttempting,
                swapCompleted: swapCompleted);

            if (!handler.CanAcceptDrop(context))
                return DropResult.Failed("Auto-transfer plan rejected");

            var executionSummary = await handler.ProcessDropWithSummaryAsync(context, cancellationToken);
            summarySink?.Invoke(executionSummary);
            return executionSummary.DropResult;
        }
    }
}
