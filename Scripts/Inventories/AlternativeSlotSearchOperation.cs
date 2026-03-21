using DragAndDropSystem.Core;
using DragAndDropSystem.Slots;

namespace DragAndDropSystem.Inventories
{
    internal readonly struct AlternativeSlotSearchOperation
    {
        public AlternativeSlotSearchOperation(
            UniversalInventory targetInventory,
            ItemStack transferStack,
            IInventory sourceInventory,
            ISlot sourceSlot)
        {
            TargetInventory = targetInventory;
            TransferStack = transferStack;
            SourceInventory = sourceInventory;
            SourceSlot = sourceSlot;
        }

        public UniversalInventory TargetInventory { get; }
        public ItemStack TransferStack { get; }
        public IInventory SourceInventory { get; }
        public ISlot SourceSlot { get; }
    }
}
