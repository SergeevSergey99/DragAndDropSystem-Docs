using DragAndDropSystem.Core;
using DragAndDropSystem.Slots;

namespace DragAndDropSystem.Inventories
{
    public readonly struct InventoryTransferRequest
    {
        public InventoryTransferRequest(
            IInventory sourceInventory,
            ISlot sourceSlot,
            IInventory targetInventory,
            ISlot targetSlot,
            ItemStack draggedStack)
        {
            SourceInventory = sourceInventory;
            SourceSlot = sourceSlot;
            TargetInventory = targetInventory;
            TargetSlot = targetSlot;
            DraggedStack = draggedStack;
        }

        public IInventory SourceInventory { get; }
        public ISlot SourceSlot { get; }
        public IInventory TargetInventory { get; }
        public ISlot TargetSlot { get; }
        public ItemStack DraggedStack { get; }

        public bool IsValid =>
            SourceInventory != null &&
            SourceSlot != null &&
            TargetInventory != null &&
            DraggedStack != null &&
            DraggedStack.PrimaryAdapter != null &&
            DraggedStack.Count > 0;
    }

    public readonly struct InventoryTransferResult
    {
        public InventoryTransferResult(
            IInventory sourceInventory,
            IInventory targetInventory,
            ISlot sourceSlot,
            ISlot targetSlot,
            IItemAdapter sourceItemAdapter,
            IItemAdapter targetItemAdapter,
            int amount,
            bool targetWasEmptyBefore,
            int remainingInSource = 0)
        {
            SourceInventory = sourceInventory;
            TargetInventory = targetInventory;
            SourceSlot = sourceSlot;
            TargetSlot = targetSlot;
            SourceItemAdapter = sourceItemAdapter;
            TargetItemAdapter = targetItemAdapter ?? sourceItemAdapter;
            Amount = amount;
            TargetWasEmptyBefore = targetWasEmptyBefore;
            RemainingInSource = remainingInSource;
        }

        public IInventory SourceInventory { get; }
        public IInventory TargetInventory { get; }
        public ISlot SourceSlot { get; }
        public ISlot TargetSlot { get; }
        public IItemAdapter SourceItemAdapter { get; }
        public IItemAdapter TargetItemAdapter { get; }
        public IItemAdapter ItemAdapter => TargetItemAdapter;
        public int Amount { get; }
        public bool TargetWasEmptyBefore { get; }
        public int RemainingInSource { get; }
        public bool IsPartialTransfer => RemainingInSource > 0;
    }
}
