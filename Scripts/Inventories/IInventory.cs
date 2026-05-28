using System.Collections.Generic;
using UDND.Core;
using UDND.DataBinding;
using UDND.Slots;

namespace UDND.Inventories
{
    /// <summary>
    /// Base inventory interface
    /// </summary>
    public interface IInventory : ISlotStackStore
    {
        /// <summary>
        /// All inventory slots
        /// </summary>
        IReadOnlyList<BaseSlot> Slots { get; }

        /// <summary>
        /// Number of slots
        /// </summary>
        int SlotCount { get; }

        public InventoryDataBindingBase DataBinding { get; }
        /// <summary>
        /// Get a slot by index
        /// </summary>
        BaseSlot GetSlot(int index);

        /// <summary>
        /// Try to add a stack
        /// </summary>
        bool TryAddStack(ItemStack stack, int targetSlotIndex = -1);

        /// <summary>
        /// Check whether the inventory contains an item
        /// </summary>
        bool Contains(IItemAdapter itemAdapter);

        /// <summary>
        /// Update visuals for all slots
        /// </summary>
        void UpdateAllVisuals();

        /// <summary>
        /// Get the number of items to drag from a slot
        /// </summary>
        int GetDragAmount(BaseSlot baseSlot, DragAmount? overrideAmount = null, int? overrideCustom = null);

        /// <summary>
        /// Try to add an item to a specific slot using the inventory settings
        /// </summary>
        /// <param name="stack">Item stack to add</param>
        /// <param name="targetBaseSlot">Target slot</param>
        /// <param name="sourceInventory">Source inventory (for events)</param>
        /// <param name="sourceSlotIndex">Source slot index (for events)</param>
        bool TryAddToSlot(
            ItemStack stack,
            BaseSlot targetBaseSlot,
            IInventory sourceInventory = null,
            int sourceSlotIndex = -1,
            SlotOperationContext operationContext = null);

        /// <summary>
        /// Remove items from a source slot and emit the inventory's normal removal side effects.
        /// Used by external drop processors that do not go through TransferPlanExecutor.
        /// </summary>
        int RemoveItemsFromSlot(
            BaseSlot sourceBaseSlot,
            ItemStack stackToRemove,
            IInventory targetInventory = null,
            BaseSlot targetBaseSlot = null);

        /// <summary>
        /// Get the number of items the inventory can accept
        /// in the context of a specific drag/drop operation.
        /// </summary>
        int GetAcceptableCount(InventoryAcceptanceRequest request);
    }
}
