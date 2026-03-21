using DragAndDropSystem.Core;
using DragAndDropSystem.Slots;

namespace DragAndDropSystem.Inventories
{
    internal readonly struct TargetPlacementOperation
    {
        public TargetPlacementOperation(
            IInventory targetInventory,
            ISlot requestedSlot,
            IInventory sourceInventory,
            ISlot sourceSlot,
            ItemStack transferStack,
            int transferAmount,
            InventorySnapshot targetSnapshot,
            bool allowAlternativeSlots,
            SlotOperationContext operationContext)
        {
            TargetInventory = targetInventory;
            RequestedSlot = requestedSlot;
            SourceInventory = sourceInventory;
            SourceSlot = sourceSlot;
            TransferStack = transferStack;
            TransferAmount = transferAmount;
            TargetSnapshot = targetSnapshot;
            AllowAlternativeSlots = allowAlternativeSlots;
            OperationContext = operationContext;
        }

        public IInventory TargetInventory { get; }
        public ISlot RequestedSlot { get; }
        public IInventory SourceInventory { get; }
        public ISlot SourceSlot { get; }
        public ItemStack TransferStack { get; }
        public int TransferAmount { get; }
        public InventorySnapshot TargetSnapshot { get; }
        public bool AllowAlternativeSlots { get; }
        public SlotOperationContext OperationContext { get; }

        public UniversalInventory AlternativeTargetInventory => TargetInventory as UniversalInventory;

        public bool CanSearchAlternativeSlot =>
            AllowAlternativeSlots &&
            AlternativeTargetInventory != null &&
            TransferStack != null &&
            !TransferStack.IsEmpty;

        public bool RequiresStrategyPlacement =>
            AlternativeTargetInventory != null &&
            AlternativeTargetInventory.Strategy.RequiresStrategyPlacement(TransferStack);
    }
}
