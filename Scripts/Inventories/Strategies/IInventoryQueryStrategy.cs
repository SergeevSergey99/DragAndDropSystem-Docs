using System.Collections.Generic;
using DragAndDropSystem.Core;
using DragAndDropSystem.Slots;

namespace DragAndDropSystem.Inventories
{
    /// <summary>
    /// Отвечает за read-only запросы к содержимому инвентаря.
    /// </summary>
    public interface IInventoryQueryStrategy
    {
        int GetItemCount(List<BaseSlot> slots, IItemAdapter itemAdapter);
        bool Contains(List<BaseSlot> slots, IItemAdapter itemAdapter);
    }
}
