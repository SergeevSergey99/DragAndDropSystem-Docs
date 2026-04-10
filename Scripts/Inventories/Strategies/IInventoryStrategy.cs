using System.Collections.Generic;
using DragAndDropSystem.Core;
using DragAndDropSystem.Slots;

namespace DragAndDropSystem.Inventories
{

    /// <summary>
    /// Strategy for managing inventory slots
    /// </summary>
    public interface IInventoryStrategy : IPlacementStrategy, IAcceptanceStrategy, IDragPolicy, IInventoryQueryStrategy
    {
        /// <summary>
        /// Set the stack limit at runtime.
        /// maxStackSize = 0 means unlimited.
        /// </summary>
        void SetMaxStackSize(int maxStackSize, bool allowItemOverride);
    }

}
