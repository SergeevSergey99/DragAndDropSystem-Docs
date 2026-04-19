using System;
namespace UniversalDragAndDrop.Inventories
{
    [Serializable]
    public sealed class StackableInventoryStrategySettings : StackBasedInventoryStrategySettingsBase
    {
        public override IInventoryStrategy CreateRuntimeStrategy(UniversalInventory inventory)
        {
            return new StackableItemStrategy(inventory, DefaultMaxStackSize, AllowItemStackOverride);
        }
    }
}
