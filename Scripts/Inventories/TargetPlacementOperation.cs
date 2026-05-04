using UniversalDragAndDrop.Core;
using UniversalDragAndDrop.Slots;

namespace UniversalDragAndDrop.Inventories
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
            PlacementOrientation orientation,
            InventorySnapshot targetSnapshot,
            SlotOperationContext operationContext,
            PlannedPlacementAllocation? placementAllocation = null)
        {
            TargetInventory = targetInventory;
            RequestedBaseSlot = requestedBaseSlot;
            SourceInventory = sourceInventory;
            SourceBaseSlot = sourceBaseSlot;
            TransferStack = transferStack;
            TransferAmount = transferAmount;
            Orientation = orientation;
            TargetSnapshot = targetSnapshot;
            OperationContext = operationContext;
            PlacementAllocation = placementAllocation;
        }

        public IInventory TargetInventory { get; }
        public BaseSlot RequestedBaseSlot { get; }
        public IInventory SourceInventory { get; }
        public BaseSlot SourceBaseSlot { get; }
        public ItemStack TransferStack { get; }
        public int TransferAmount { get; }
        public PlacementOrientation Orientation { get; }
        public InventorySnapshot TargetSnapshot { get; }
        public SlotOperationContext OperationContext { get; }
        public PlannedPlacementAllocation? PlacementAllocation { get; }

        public UniversalInventory AlternativeTargetInventory => TargetInventory as UniversalInventory;

        public bool RequiresStrategyPlacement =>
            AlternativeTargetInventory != null &&
            AlternativeTargetInventory.PlacementStrategy.RequiresStrategyPlacement(TransferStack);
    }
}
