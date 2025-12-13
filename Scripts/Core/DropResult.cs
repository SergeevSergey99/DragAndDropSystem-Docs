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
        /// The item that was dropped
        /// </summary>
        public IInventoryItem Item { get; }

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

        private DropResult(
            bool success,
            IInventoryItem item,
            int amount,
            ISlot targetSlot,
            IInventory targetInventory,
            string failureReason,
            bool isPartialTransfer,
            int remainingInSource)
        {
            Success = success;
            Item = item;
            Amount = amount;
            TargetSlot = targetSlot;
            TargetInventory = targetInventory;
            FailureReason = failureReason;
            IsPartialTransfer = isPartialTransfer;
            RemainingInSource = remainingInSource;
        }

        /// <summary>
        /// Create a successful drop result
        /// </summary>
        public static DropResult Succeeded(
            IInventoryItem item,
            int amount,
            ISlot targetSlot = null,
            IInventory targetInventory = null,
            bool isPartialTransfer = false,
            int remainingInSource = 0)
        {
            return new DropResult(
                success: true,
                item: item,
                amount: amount,
                targetSlot: targetSlot,
                targetInventory: targetInventory,
                failureReason: null,
                isPartialTransfer: isPartialTransfer,
                remainingInSource: remainingInSource);
        }

        /// <summary>
        /// Create a failed drop result
        /// </summary>
        public static DropResult Failed(string reason)
        {
            return new DropResult(
                success: false,
                item: null,
                amount: 0,
                targetSlot: null,
                targetInventory: null,
                failureReason: reason,
                isPartialTransfer: false,
                remainingInSource: 0);
        }
    }
}
