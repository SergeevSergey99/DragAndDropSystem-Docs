using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using UniversalDragAndDrop.Core;
using UniversalDragAndDrop.Rules;
using UniversalDragAndDrop.Slots;

namespace UniversalDragAndDrop.Inventories
{
    /// <summary>
    /// Builds and executes auto-transfer operations through the same planner/executor
    /// pipeline used by drag-and-drop handlers.
    /// </summary>
    public sealed class AutoTransferService
    {
        public bool TryCreateContext(
            IReadOnlyList<BaseSlot> sourceSlots,
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
            var seen = new HashSet<BaseSlot>();

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

                if (slot.Stack == null || slot.Stack.IsEmpty)
                    continue;

                if (!ItemStack.TryCreate(slot.Stack.Adapters.Take(dragAmount), out var entryStack))
                    continue;

                entries.Add(new DragEntry(entryStack, slot, sourceInventory));
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

        public async Task<(DropResult result, TransferExecutionSummary summary)> ExecuteAsync(
            DragContext context,
            IInventory targetInventory,
            GlobalRuleValidator globalRules,
            System.Func<InventorySwapContext, bool> swapAttempting,
            System.Action<InventorySwapContext> swapCompleted,
            CancellationToken cancellationToken,
            DropRequestPolicy? requestedPolicy = null)
        {
            if (context == null)
                return (DropResult.Failed("Auto-transfer context is null"), null);

            if (targetInventory == null)
                return (DropResult.Failed("Auto-transfer target inventory is null"), null);

            var handler = new InventoryDropProcessor(
                targetBaseSlot: null,
                targetInventory: targetInventory,
                globalRules: globalRules,
                swapAttempting: swapAttempting,
                swapCompleted: swapCompleted);

            if (!handler.CanAcceptDrop(context, requestedPolicy))
                return (DropResult.Failed("Auto-transfer plan rejected"), null);

            var summary = await handler.ProcessDropWithSummaryAsync(context, requestedPolicy, cancellationToken);
            return (summary.DropResult, summary);
        }
    }
}
