using System.Collections.Generic;
using UDND.Core;
using UDND.Slots;

namespace UDND.Inventories
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
