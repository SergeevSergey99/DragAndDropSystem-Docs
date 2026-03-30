using DragAndDropSystem.Inventories;
using DragAndDropSystem.Slots;

namespace DragAndDropSystem.Core
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
        public ISlot TargetSlot { get; }

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
            ISlot targetSlot,
            IInventory targetInventory,
            string failureReason,
            bool isPartialTransfer,
            int remainingInSource,
            int succeededEntries,
            int failedEntries)
        {
            Success = success;
            ItemAdapter = itemAdapter;
            Amount = amount;
            TargetSlot = targetSlot;
            TargetInventory = targetInventory;
            FailureReason = failureReason;
            IsPartialTransfer = isPartialTransfer;
            RemainingInSource = remainingInSource;
            SucceededEntries = succeededEntries;
            FailedEntries = failedEntries;
        }

        /// <summary>
        /// Create a successful drop result
        /// </summary>
        public static DropResult Succeeded(
            IItemAdapter itemAdapter,
            int amount,
            ISlot targetSlot = null,
            IInventory targetInventory = null,
            bool isPartialTransfer = false,
            int remainingInSource = 0,
            int succeededEntries = 1,
            int failedEntries = 0)
        {
            return new DropResult(
                success: true,
                itemAdapter: itemAdapter,
                amount: amount,
                targetSlot: targetSlot,
                targetInventory: targetInventory,
                failureReason: null,
                isPartialTransfer: isPartialTransfer,
                remainingInSource: remainingInSource,
                succeededEntries: succeededEntries,
                failedEntries: failedEntries);
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
                targetSlot: null,
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
            ISlot targetSlot,
            IInventory targetInventory,
            int succeededEntries,
            int failedEntries,
            bool isPartialTransfer,
            int remainingInSource = 0)
        {
            return new DropResult(
                success: true,
                itemAdapter: itemAdapter,
                amount: amount,
                targetSlot: targetSlot,
                targetInventory: targetInventory,
                failureReason: null,
                isPartialTransfer: isPartialTransfer,
                remainingInSource: remainingInSource,
                succeededEntries: succeededEntries,
                failedEntries: failedEntries);
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
                targetSlot: null,
                targetInventory: null,
                failureReason: reason,
                isPartialTransfer: false,
                remainingInSource: 0,
                succeededEntries: succeededEntries,
                failedEntries: failedEntries);
        }
    }
}
