using System;
using System.Collections.Generic;
using UDND.Core;
using UDND.Slots;

namespace UDND.Inventories
{
    public enum PlacementTransferOutcomeKind : byte
    {
        Create,
        Merge,
        Swap,
        OccupiedHandler
    }

    public enum EntryTransferStatus : byte
    {
        Succeeded,
        Partial,
        Failed
    }

    /// <summary>
    /// One committed mutation of an entry transfer (an entry may produce several outcomes
    /// when a large stack is distributed over multiple placements).
    /// Carries the exact transferred sub-stacks so events/DataBinding never read amounts
    /// from <see cref="DragEntry.Stack"/>.
    /// </summary>
    public sealed class PlacementTransferOutcome
    {
        public PlacementTransferOutcome(
            PlacementTransferOutcomeKind kind,
            IInventory sourceInventory,
            IInventory targetInventory,
            BaseSlot sourceBaseSlot,
            BaseSlot targetBaseSlot,
            ItemStack sourceRemovedStack,
            ItemStack transferredStack,
            PlacementSnapshot sourcePlacementSnapshot,
            PlacementSnapshot targetPlacementSnapshot,
            bool targetWasEmptyBefore)
        {
            Kind = kind;
            SourceInventory = sourceInventory;
            TargetInventory = targetInventory;
            SourceBaseSlot = sourceBaseSlot;
            TargetBaseSlot = targetBaseSlot;
            SourceRemovedStack = sourceRemovedStack ?? ItemStack.Empty();
            TransferredStack = transferredStack ?? ItemStack.Empty();
            SourcePlacementSnapshot = sourcePlacementSnapshot;
            TargetPlacementSnapshot = targetPlacementSnapshot;
            TargetWasEmptyBefore = targetWasEmptyBefore;
        }

        public PlacementTransferOutcomeKind Kind { get; }
        public IInventory SourceInventory { get; }
        public IInventory TargetInventory { get; }
        public BaseSlot SourceBaseSlot { get; }
        public BaseSlot TargetBaseSlot { get; }

        /// <summary>Adapters removed from the source for this outcome (before conversion).</summary>
        public ItemStack SourceRemovedStack { get; }

        /// <summary>Adapters added to the target for this outcome (after conversion).</summary>
        public ItemStack TransferredStack { get; }

        public PlacementSnapshot SourcePlacementSnapshot { get; }
        public PlacementSnapshot TargetPlacementSnapshot { get; }
        public bool TargetWasEmptyBefore { get; }

        public IItemAdapter SourceItem => SourceRemovedStack.PrimaryAdapter;
        public IItemAdapter TargetItem => TransferredStack.PrimaryAdapter;
        public int Amount => TransferredStack.Count;
    }

    /// <summary>
    /// Result of transferring a single <see cref="DragEntry"/> through the JIT pipeline.
    /// </summary>
    public sealed class EntryTransferResult
    {
        private EntryTransferResult(
            EntryTransferStatus status,
            int requestedAmount,
            int transferredAmount,
            string failureReason,
            IReadOnlyList<PlacementTransferOutcome> outcomes)
        {
            Status = status;
            RequestedAmount = requestedAmount;
            TransferredAmount = transferredAmount;
            FailureReason = failureReason;
            Outcomes = outcomes ?? Array.Empty<PlacementTransferOutcome>();
        }

        public EntryTransferStatus Status { get; }
        public int RequestedAmount { get; }
        public int TransferredAmount { get; }
        public int RemainingAmount => RequestedAmount - TransferredAmount;
        public string FailureReason { get; }
        public IReadOnlyList<PlacementTransferOutcome> Outcomes { get; }
        public bool Success => Status != EntryTransferStatus.Failed;

        public static EntryTransferResult Failed(int requestedAmount, string reason)
            => new EntryTransferResult(EntryTransferStatus.Failed, requestedAmount, 0, reason, null);

        public static EntryTransferResult Committed(
            int requestedAmount,
            int transferredAmount,
            IReadOnlyList<PlacementTransferOutcome> outcomes)
            => new EntryTransferResult(
                transferredAmount >= requestedAmount
                    ? EntryTransferStatus.Succeeded
                    : EntryTransferStatus.Partial,
                requestedAmount,
                transferredAmount,
                null,
                outcomes);
    }

    /// <summary>
    /// Aggregated report of a sequential best-effort batch execution.
    /// </summary>
    public sealed class TransferExecutionReport
    {
        public TransferExecutionReport(IReadOnlyList<EntryTransferResult> entryResults, string failureReason = null)
        {
            EntryResults = entryResults ?? Array.Empty<EntryTransferResult>();
            FailureReason = failureReason;

            foreach (var result in EntryResults)
            {
                RequestedAmount += result.RequestedAmount;
                TransferredAmount += result.TransferredAmount;
                if (result.Success)
                    SucceededEntries++;
                else
                    FailedEntries++;
            }
        }

        public IReadOnlyList<EntryTransferResult> EntryResults { get; }
        public int RequestedAmount { get; }
        public int TransferredAmount { get; }
        public int SucceededEntries { get; }
        public int FailedEntries { get; }
        public string FailureReason { get; }
        public bool Success => SucceededEntries > 0;
        public bool IsPartial => Success && TransferredAmount < RequestedAmount;

        public static TransferExecutionReport Rejected(string reason)
            => new TransferExecutionReport(Array.Empty<EntryTransferResult>(), reason);

        /// <summary>
        /// Builds the <see cref="DropResult"/> compatibility projection from this report.
        /// </summary>
        public DropResult ToDropResult(IInventory targetInventory)
        {
            if (!Success)
            {
                string reason = FailureReason;
                if (string.IsNullOrEmpty(reason))
                    foreach (var entry in EntryResults)
                        if (!string.IsNullOrEmpty(entry.FailureReason)) { reason = entry.FailureReason; break; }

                return DropResult.FailedBatch(
                    reason ?? "Transfer failed",
                    SucceededEntries,
                    FailedEntries);
            }

            IItemAdapter lastItemAdapter = null;
            BaseSlot lastTargetBaseSlot = null;
            PlacementSnapshot lastPlacementSnapshot = null;
            foreach (var entry in EntryResults)
                foreach (var outcome in entry.Outcomes)
                {
                    lastItemAdapter = outcome.TargetItem;
                    lastTargetBaseSlot = outcome.TargetBaseSlot;
                    lastPlacementSnapshot = outcome.TargetPlacementSnapshot;
                }

            return DropResult.SucceededBatch(
                lastItemAdapter,
                TransferredAmount,
                lastTargetBaseSlot,
                targetInventory,
                SucceededEntries,
                FailedEntries,
                IsPartial,
                remainingInSource: RequestedAmount - TransferredAmount,
                placementSnapshot: lastPlacementSnapshot);
        }
    }
}
