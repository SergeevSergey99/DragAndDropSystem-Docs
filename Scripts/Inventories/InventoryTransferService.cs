using DragAndDropSystem.Core;
using DragAndDropSystem.Slots;

namespace DragAndDropSystem.Inventories
{
    public readonly struct InventoryTransferRequest
    {
        public InventoryTransferRequest(
            IInventory sourceInventory,
            BaseSlot sourceBaseSlot,
            IInventory targetInventory,
            BaseSlot targetBaseSlot,
            ItemStack draggedStack)
        {
            SourceInventory = sourceInventory;
            SourceBaseSlot = sourceBaseSlot;
            TargetInventory = targetInventory;
            TargetBaseSlot = targetBaseSlot;
            DraggedStack = draggedStack;
        }

        public IInventory SourceInventory { get; }
        public BaseSlot SourceBaseSlot { get; }
        public IInventory TargetInventory { get; }
        public BaseSlot TargetBaseSlot { get; }
        public ItemStack DraggedStack { get; }

        public bool IsValid =>
            SourceInventory != null &&
            SourceBaseSlot != null &&
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
            BaseSlot sourceBaseSlot,
            BaseSlot targetBaseSlot,
            ItemStack sourceRemovedStack,
            ItemStack transferredStack,
            bool targetWasEmptyBefore,
            int remainingInSource = 0)
        {
            SourceInventory = sourceInventory;
            TargetInventory = targetInventory;
            SourceBaseSlot = sourceBaseSlot;
            TargetBaseSlot = targetBaseSlot;
            SourceRemovedStack = sourceRemovedStack ?? ItemStack.Empty();
            TransferredStack = transferredStack ?? ItemStack.Empty();
            TargetWasEmptyBefore = targetWasEmptyBefore;
            RemainingInSource = remainingInSource;
        }

        public IInventory SourceInventory { get; }
        public IInventory TargetInventory { get; }
        public BaseSlot SourceBaseSlot { get; }
        public BaseSlot TargetBaseSlot { get; }

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
