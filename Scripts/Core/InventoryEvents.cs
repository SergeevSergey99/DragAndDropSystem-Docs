using System;
using DragAndDropSystem.Inventories;
using DragAndDropSystem.Slots;

namespace DragAndDropSystem.Core
{
    /// <summary>
    /// Inventory event arguments (item add/remove)
    /// </summary>
    public class InventoryItemEventContext
    {
        public ItemStack Stack { get; }
        public int SlotIndex { get; }

        /// <summary>
        /// Source inventory (where the item came from)
        /// Null if the item was not added from another inventory
        /// </summary>
        public IInventory SourceInventory { get; }

        /// <summary>
        /// Target inventory (where the item was placed)
        /// Null if the item was not removed into another inventory
        /// </summary>
        public IInventory TargetInventory { get; }

        /// <summary>
        /// Source slot (where the item came from)
        /// Null if the slot is unknown or unavailable
        /// </summary>
        public BaseSlot SourceBaseSlot { get; }

        /// <summary>
        /// Target slot (where the item was placed)
        /// Null if the slot is unknown or unavailable
        /// </summary>
        public BaseSlot TargetBaseSlot { get; }

        public InventoryItemEventContext(
            ItemStack stack,
            int slotIndex = -1,
            IInventory sourceInventory = null,
            IInventory targetInventory = null,
            BaseSlot sourceBaseSlot = null,
            BaseSlot targetBaseSlot = null)
        {
            Stack = stack ?? ItemStack.Empty();
            SlotIndex = slotIndex;
            SourceInventory = sourceInventory;
            TargetInventory = targetInventory;
            SourceBaseSlot = sourceBaseSlot;
            TargetBaseSlot = targetBaseSlot;
        }
    }

    /// <summary>
    /// Item swap event arguments
    /// </summary>
    public class InventorySwapContext
    {
        /// <summary>
        /// Stack from the source slot (will be moved to the target)
        /// </summary>
        public ItemStack SourceStack { get; }

        /// <summary>
        /// Stack from the target slot (will be moved to the source)
        /// </summary>
        public ItemStack TargetStack { get; }

        /// <summary>
        /// Source slot (where dragging started)
        /// </summary>
        public BaseSlot SourceBaseSlot { get; }

        /// <summary>
        /// Target slot (where we want to drop)
        /// </summary>
        public BaseSlot TargetBaseSlot { get; }

        /// <summary>
        /// Source inventory
        /// </summary>
        public IInventory SourceInventory { get; }

        /// <summary>
        /// Target inventory
        /// </summary>
        public IInventory TargetInventory { get; }

        /// <summary>
        /// Can be set to true to cancel the swap
        /// </summary>
        public bool Cancel { get; set; }

        public InventorySwapContext(
            ItemStack sourceStack,
            ItemStack targetStack,
            BaseSlot sourceBaseSlot,
            BaseSlot targetBaseSlot,
            IInventory sourceInventory,
            IInventory targetInventory)
        {
            SourceStack = sourceStack;
            TargetStack = targetStack;
            SourceBaseSlot = sourceBaseSlot;
            TargetBaseSlot = targetBaseSlot;
            SourceInventory = sourceInventory;
            TargetInventory = targetInventory;
            Cancel = false;
        }
    }
}
