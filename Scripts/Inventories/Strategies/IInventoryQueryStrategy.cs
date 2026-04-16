using System.Collections.Generic;
using UniversalDragAndDrop.Core;
using UniversalDragAndDrop.Slots;

namespace UniversalDragAndDrop.Inventories
{
    /// <summary>
    /// Responsible for read-only queries against inventory contents.
    /// </summary>
    public interface IInventoryQueryStrategy
    {
        int GetItemCount(List<BaseSlot> slots, IItemAdapter itemAdapter);
        bool Contains(List<BaseSlot> slots, IItemAdapter itemAdapter);
    }
}
