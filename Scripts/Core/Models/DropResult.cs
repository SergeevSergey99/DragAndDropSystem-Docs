using System;
using System.Collections.Generic;
using UDND.Inventories;
using UDND.Slots;

namespace UDND.Core
{
    /// <summary>
    /// Result of a drop operation performed by IDropHandler.
    /// Immutable struct containing operation outcome and details.
    /// </summary>
    public readonly struct DropResult
    {
        /// <summary>
        /// Whether the drop operation succeeded
        /// </summary>
        public bool Success { get; }

        /// <summary>
        /// The itemAdapter that was dropped
        /// </summary>
        public IItemAdapter ItemAdapter { get; }

        /// <summary>
        /// Number of items successfully dropped
        /// </summary>
        public int Amount { get; }

        /// <summary>
        /// The slot where items were placed (null for world drops or area drops)
        /// </summary>
        public BaseSlot TargetBaseSlot { get; }

        /// <summary>
        /// The inventory where items were placed (null for world drops)
        /// </summary>
        public IInventory TargetInventory { get; }

        /// <summary>
        /// Reason for failure (null if successful)
        /// </summary>
        public string FailureReason { get; }

        /// <summary>
        /// Whether this was a partial transfer (some items remain in source)
        /// </summary>
        public bool IsPartialTransfer { get; }

        /// <summary>
        /// Number of items remaining in source after transfer
        /// </summary>
        public int RemainingInSource { get; }

        /// <summary>
        /// Number of batch entries executed successfully (1 for classic single transfer).
        /// </summary>
        public int SucceededEntries { get; }

        /// <summary>
        /// Number of batch entries that failed to execute.
        /// </summary>
        public int FailedEntries { get; }

        private DropResult(
            bool success,
            IItemAdapter itemAdapter,
            int amount,
            BaseSlot targetBaseSlot,
            IInventory targetInventory,
            string failureReason,
            bool isPartialTransfer,
            int remainingInSource,
            int succeededEntries,
            int failedEntries,
            PlacementSnapshot placementSnapshot = null)
        {
            Success = success;
            ItemAdapter = itemAdapter;
            Amount = amount;
            TargetBaseSlot = targetBaseSlot;
            TargetInventory = targetInventory;
            FailureReason = failureReason;
            IsPartialTransfer = isPartialTransfer;
            RemainingInSource = remainingInSource;
            SucceededEntries = succeededEntries;
            FailedEntries = failedEntries;
            PlacementSnapshot = placementSnapshot;
        }

        public PlacementSnapshot PlacementSnapshot { get; }
        public BaseSlot AnchorSlot => PlacementSnapshot?.AnchorBaseSlot ?? TargetBaseSlot;
        public IReadOnlyList<BaseSlot> CoveredSlots => PlacementSnapshot?.CoveredBaseSlots ?? Array.Empty<BaseSlot>();
        public IReadOnlyList<int> CoveredIndices => PlacementSnapshot?.CoveredIndices ?? Array.Empty<int>();
        public int AnchorIndex => PlacementSnapshot != null && PlacementSnapshot.AnchorIndex >= 0
            ? PlacementSnapshot.AnchorIndex
            : TargetBaseSlot?.Index ?? -1;
        public PlacementOrientation Orientation => PlacementSnapshot?.Orientation ?? PlacementOrientation.Rot0;
        public Footprint Footprint => PlacementSnapshot?.Footprint ?? Footprint.One;

        /// <summary>
        /// Create a successful drop result
        /// </summary>
        public static DropResult Succeeded(
            IItemAdapter itemAdapter,
            int amount,
            BaseSlot targetBaseSlot = null,
            IInventory targetInventory = null,
            bool isPartialTransfer = false,
            int remainingInSource = 0,
            int succeededEntries = 1,
            int failedEntries = 0,
            PlacementSnapshot placementSnapshot = null)
        {
            return new DropResult(
                success: true,
                itemAdapter: itemAdapter,
                amount: amount,
                targetBaseSlot: targetBaseSlot,
                targetInventory: targetInventory,
                failureReason: null,
                isPartialTransfer: isPartialTransfer,
                remainingInSource: remainingInSource,
                succeededEntries: succeededEntries,
                failedEntries: failedEntries,
                placementSnapshot: placementSnapshot);
        }

        /// <summary>
        /// Create a failed drop result
        /// </summary>
        public static DropResult Failed(string reason)
        {
            return new DropResult(
                success: false,
                itemAdapter: null,
                amount: 0,
                targetBaseSlot: null,
                targetInventory: null,
                failureReason: reason,
                isPartialTransfer: false,
                remainingInSource: 0,
                succeededEntries: 0,
                failedEntries: 1);
        }

        /// <summary>
        /// Create a successful batch drop result.
        /// </summary>
        public static DropResult SucceededBatch(
            IItemAdapter itemAdapter,
            int amount,
            BaseSlot targetBaseSlot,
            IInventory targetInventory,
            int succeededEntries,
            int failedEntries,
            bool isPartialTransfer,
            int remainingInSource = 0,
            PlacementSnapshot placementSnapshot = null)
        {
            return new DropResult(
                success: true,
                itemAdapter: itemAdapter,
                amount: amount,
                targetBaseSlot: targetBaseSlot,
                targetInventory: targetInventory,
                failureReason: null,
                isPartialTransfer: isPartialTransfer,
                remainingInSource: remainingInSource,
                succeededEntries: succeededEntries,
                failedEntries: failedEntries,
                placementSnapshot: placementSnapshot);
        }

        /// <summary>
        /// Create a failed batch drop result.
        /// </summary>
        public static DropResult FailedBatch(
            string reason,
            int succeededEntries,
            int failedEntries)
        {
            return new DropResult(
                success: false,
                itemAdapter: null,
                amount: 0,
                targetBaseSlot: null,
                targetInventory: null,
                failureReason: reason,
                isPartialTransfer: false,
                remainingInSource: 0,
                succeededEntries: succeededEntries,
                failedEntries: failedEntries);
        }
    }
}
