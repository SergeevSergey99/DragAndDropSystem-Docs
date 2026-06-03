using UDND.Core;
using UDND.Inventories;
using UDND.Rules;

namespace UDND.Slots
{
    public interface ISlot
    {
        int Index { get; }
        IReadOnlyItemStack Stack { get; }
        IInventory Inventory { get; }
        SlotRuleValidator SlotRuleValidator { get; }
    }
}
