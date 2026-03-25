using System.Collections.Generic;
using DragAndDropSystem.Core;
using DragAndDropSystem.Slots;

namespace DragAndDropSystem.Inventories
{

    /// <summary>
    /// Стратегия управления слотами инвентаря
    /// </summary>
    public interface IInventoryStrategy : IPlacementStrategy, IAcceptanceStrategy, IDragPolicy, IInventoryQueryStrategy
    {
        /// <summary>
        /// Задать лимит стака в рантайме.
        /// maxStackSize = 0 означает без ограничений.
        /// </summary>
        void SetMaxStackSize(int maxStackSize, bool allowItemOverride);
    }

}
