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
    }

}
