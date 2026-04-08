using DragAndDropSystem.Core;
using DragAndDropSystem.Slots;

namespace DragAndDropSystem.Inventories
{
    internal readonly struct TargetPlacementOperation
    {
        public TargetPlacementOperation(
            IInventory targetInventory,
            BaseSlot requestedBaseSlot,
            IInventory sourceInventory,
            BaseSlot sourceBaseSlot,
            ItemStack transferStack,
            int transferAmount,
            InventorySnapshot targetSnapshot,
            SlotOperationContext operationContext)
        {
            TargetInventory = targetInventory;
            RequestedBaseSlot = requestedBaseSlot;
            SourceInventory = sourceInventory;
            SourceBaseSlot = sourceBaseSlot;
            TransferStack = transferStack;
            TransferAmount = transferAmount;
            TargetSnapshot = targetSnapshot;
            OperationContext = operationContext;
        }

        public IInventory TargetInventory { get; }
        public BaseSlot RequestedBaseSlot { get; }
        public IInventory SourceInventory { get; }
        public BaseSlot SourceBaseSlot { get; }
        public ItemStack TransferStack { get; }
        public int TransferAmount { get; }
        public InventorySnapshot TargetSnapshot { get; }
        public SlotOperationContext OperationContext { get; }

        public UniversalInventory AlternativeTargetInventory => TargetInventory as UniversalInventory;

        public bool RequiresStrategyPlacement =>
            AlternativeTargetInventory != null &&
            AlternativeTargetInventory.PlacementStrategy.RequiresStrategyPlacement(TransferStack);
    }
}
