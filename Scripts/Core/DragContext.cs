using DragAndDropSystem.Slots;
using DragAndDropSystem.Inventories;

namespace DragAndDropSystem.Core
{
    /// <summary>
    /// Контекст операции перетаскивания
    /// Содержит всю информацию о текущей операции drag-and-drop
    /// </summary>
    public class DragContext
    {
        public ItemStack DraggedStack { get; private set; }
        public ISlot SourceSlot { get; private set; }
        public IInventory SourceInventory { get; private set; }

        public ISlot TargetSlot { get; set; }
        public IInventory TargetInventory { get; set; }

        public bool IsSameSlot => SourceSlot == TargetSlot;
        public bool IsSameInventory => SourceInventory == TargetInventory;
        public bool HasTarget => TargetSlot != null && TargetInventory != null;

        public DragContext(ItemStack stack, ISlot sourceSlot, IInventory sourceInventory)
        {
            DraggedStack = stack;
            SourceSlot = sourceSlot;
            SourceInventory = sourceInventory;
        }

        public void SetTarget(ISlot targetSlot, IInventory targetInventory)
        {
            TargetSlot = targetSlot;
            TargetInventory = targetInventory;
        }

        public void ClearTarget()
        {
            TargetSlot = null;
            TargetInventory = null;
        }
    }
}
