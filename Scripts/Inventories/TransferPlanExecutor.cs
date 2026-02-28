using System.Collections.Generic;
using DragAndDropSystem.Core;
using DragAndDropSystem.Slots;
using DragAndDropSystem.Tools;

namespace DragAndDropSystem.Inventories
{
    public sealed class TransferExecutionSummary
    {
        public TransferExecutionSummary(
            bool success,
            int succeededEntries,
            int failedEntries,
            int transferredAmount,
            bool isPartial,
            DropResult dropResult)
        {
            Success = success;
            SucceededEntries = succeededEntries;
            FailedEntries = failedEntries;
            TransferredAmount = transferredAmount;
            IsPartial = isPartial;
            DropResult = dropResult;
        }

        public bool Success { get; }
        public int SucceededEntries { get; }
        public int FailedEntries { get; }
        public int TransferredAmount { get; }
        public bool IsPartial { get; }
        public DropResult DropResult { get; }
    }

    /// <summary>
    /// Исполняет готовый TransferPlan.
    /// Поддерживает атомарный режим (с откатом) и BestEffort.
    /// </summary>
    public class TransferPlanExecutor
    {
        private readonly InventoryTransferService _transferService;

        public TransferPlanExecutor(InventoryTransferService transferService = null)
        {
            _transferService = transferService ?? new InventoryTransferService();
        }

        public TransferExecutionSummary Execute(TransferPlan plan)
        {
            if (plan == null || !plan.IsValid || plan.Entries == null || plan.Entries.Count == 0)
            {
                var failed = DropResult.Failed("Invalid transfer plan");
                return new TransferExecutionSummary(false, 0, 0, 0, false, failed);
            }

            bool atomic = plan.Policy.BatchExecution == BatchExecutionPolicy.Atomic;
            var snapshots = atomic ? CaptureSnapshots(plan) : null;

            if (atomic && snapshots == null)
            {
                var failed = DropResult.FailedBatch("Atomic execution requires snapshot-capable inventories", 0, plan.Entries.Count);
                return new TransferExecutionSummary(false, 0, plan.Entries.Count, 0, false, failed);
            }

            int succeededEntries = 0;
            int failedEntries = 0;
            int transferredAmount = 0;
            IInventoryItem lastItem = null;
            ISlot lastTargetSlot = null;
            bool hadPartialTransfer = false;

            foreach (var plannedEntry in plan.Entries)
            {
                if (plannedEntry == null || !plannedEntry.IsPlanned)
                {
                    failedEntries++;
                    if (atomic)
                    {
                        RestoreSnapshots(snapshots);
                        return BuildFailedSummary(
                            "Atomic execution failed: plan contains non-plannable entry",
                            succeededEntries,
                            failedEntries,
                            transferredAmount);
                    }
                    continue;
                }

                int entryTransferred = 0;
                bool entryFailed = false;

                foreach (var allocation in plannedEntry.Allocations)
                {
                    if (allocation.Slot == null || allocation.Amount <= 0)
                    {
                        entryFailed = true;
                        break;
                    }

                    var request = new InventoryTransferRequest(
                        plannedEntry.Entry.SourceInventory,
                        plannedEntry.Entry.SourceSlot,
                        plan.TargetInventory,
                        allocation.Slot,
                        new ItemStack(plannedEntry.Entry.Stack.Item, allocation.Amount),
                        allowAlternativeSlots: false);

                    if (!_transferService.TryExecuteTransfer(request, out var outcome) || outcome.Amount <= 0)
                    {
                        entryFailed = true;
                        break;
                    }

                    entryTransferred += outcome.Amount;
                    transferredAmount += outcome.Amount;
                    lastItem = outcome.Item;
                    lastTargetSlot = outcome.TargetSlot ?? allocation.Slot;
                    hadPartialTransfer |= outcome.IsPartialTransfer;
                }

                if (entryFailed)
                {
                    failedEntries++;
                    if (atomic)
                    {
                        RestoreSnapshots(snapshots);
                        return BuildFailedSummary(
                            "Atomic execution failed while applying plan",
                            succeededEntries,
                            failedEntries,
                            transferredAmount);
                    }
                }
                else if (entryTransferred > 0)
                {
                    succeededEntries++;
                    if (entryTransferred < plannedEntry.RequestedAmount)
                    {
                        hadPartialTransfer = true;
                    }
                }
                else
                {
                    failedEntries++;
                    if (atomic)
                    {
                        RestoreSnapshots(snapshots);
                        return BuildFailedSummary(
                            "Atomic execution failed: no items transferred for planned entry",
                            succeededEntries,
                            failedEntries,
                            transferredAmount);
                    }
                }
            }

            bool success = succeededEntries > 0 && (failedEntries == 0 || plan.Policy.BatchExecution == BatchExecutionPolicy.BestEffort);
            bool isPartial = hadPartialTransfer || failedEntries > 0 || (succeededEntries > 0 && failedEntries > 0);

            if (!success)
            {
                var failed = DropResult.FailedBatch(
                    "No planned entries were executed",
                    succeededEntries,
                    failedEntries);
                return new TransferExecutionSummary(false, succeededEntries, failedEntries, transferredAmount, isPartial, failed);
            }

            var result = DropResult.SucceededBatch(
                item: lastItem,
                amount: transferredAmount,
                targetSlot: lastTargetSlot,
                targetInventory: plan.TargetInventory,
                succeededEntries: succeededEntries,
                failedEntries: failedEntries,
                isPartialTransfer: isPartial,
                remainingInSource: 0);

            Extentions.DragAndDropLog($"<color=green>[TransferPlanExecutor] Executed plan: successEntries={succeededEntries}, failedEntries={failedEntries}, amount={transferredAmount}</color>");
            return new TransferExecutionSummary(success, succeededEntries, failedEntries, transferredAmount, isPartial, result);
        }

        private static TransferExecutionSummary BuildFailedSummary(
            string reason,
            int succeededEntries,
            int failedEntries,
            int transferredAmount)
        {
            var failed = DropResult.FailedBatch(reason, succeededEntries, failedEntries);
            return new TransferExecutionSummary(false, succeededEntries, failedEntries, transferredAmount, true, failed);
        }

        private static Dictionary<IInventory, InventorySnapshot> CaptureSnapshots(TransferPlan plan)
        {
            var snapshots = new Dictionary<IInventory, InventorySnapshot>();

            foreach (var entry in plan.Entries)
            {
                if (entry?.Entry.SourceInventory == null)
                    continue;

                if (!CaptureSnapshot(entry.Entry.SourceInventory, snapshots))
                    return null;
            }

            if (!CaptureSnapshot(plan.TargetInventory, snapshots))
                return null;

            return snapshots;
        }

        private static bool CaptureSnapshot(IInventory inventory, Dictionary<IInventory, InventorySnapshot> snapshots)
        {
            if (inventory == null || snapshots.ContainsKey(inventory))
                return true;

            if (inventory is not IInventorySnapshotProvider provider)
                return false;

            snapshots[inventory] = provider.CaptureSnapshot();
            return true;
        }

        private static void RestoreSnapshots(Dictionary<IInventory, InventorySnapshot> snapshots)
        {
            if (snapshots == null)
                return;

            foreach (var pair in snapshots)
            {
                if (pair.Key is IInventorySnapshotProvider provider)
                {
                    provider.RestoreSnapshot(pair.Value);
                    pair.Key.UpdateAllVisuals();
                }
            }
        }
    }
}
