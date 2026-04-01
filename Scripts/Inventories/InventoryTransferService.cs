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
            ItemStack sourceRemovedStack,
            ItemStack transferredStack,
            bool targetWasEmptyBefore,
            int remainingInSource = 0)
        {
            SourceInventory = sourceInventory;
            TargetInventory = targetInventory;
            SourceSlot = sourceSlot;
            TargetSlot = targetSlot;
            SourceRemovedStack = sourceRemovedStack ?? ItemStack.Empty();
            TransferredStack = transferredStack ?? ItemStack.Empty();
            TargetWasEmptyBefore = targetWasEmptyBefore;
            RemainingInSource = remainingInSource;
        }

        public IInventory SourceInventory { get; }
        public IInventory TargetInventory { get; }
        public ISlot SourceSlot { get; }
        public ISlot TargetSlot { get; }

        /// <summary>
        /// Стек адаптеров, удалённых из source (до конвертации)
        /// </summary>
        public ItemStack SourceRemovedStack { get; }

        /// <summary>
        /// Стек адаптеров, добавленных в target (после конвертации)
        /// </summary>
        public ItemStack TransferredStack { get; }

        public IItemAdapter SourceItemAdapter => SourceRemovedStack.PrimaryAdapter;
        public IItemAdapter TargetItemAdapter => TransferredStack.PrimaryAdapter;
        public IItemAdapter ItemAdapter => TargetItemAdapter;
        public int Amount => TransferredStack.Count;
        public bool TargetWasEmptyBefore { get; }
        public int RemainingInSource { get; }
        public bool IsPartialTransfer => RemainingInSource > 0;
    }
}
