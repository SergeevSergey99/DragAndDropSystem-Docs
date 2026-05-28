using UDND.Core;

namespace UDND.Inventories
{
    /// <summary>
    /// Strategy for managing inventory slots
    /// </summary>
    public interface IInventoryStrategy
    {
        /// <summary>
        /// Set the stack limit at runtime.
        /// maxStackSize = 0 means unlimited.
        /// </summary>
        void SetMaxStackSize(int maxStackSize, bool allowItemOverride);
        
        int GetMaxStackSizeForItem(IItemAdapter itemAdapter);
    }
}
