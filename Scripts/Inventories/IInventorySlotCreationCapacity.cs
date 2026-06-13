using UDND.Slots;

namespace UDND.Inventories
{
    /// <summary>
    /// Exposes dynamic-slot creation parameters to the planner.
    /// Implemented by UniversalInventory and queried by candidate enumeration.
    /// </summary>
    internal interface IInventorySlotCreationCapacity
    {
        bool CanCreateNewSlot { get; }
        int PotentialNewSlots { get; }
        BaseSlot BaseSlotPrefab { get; }
    }
}
