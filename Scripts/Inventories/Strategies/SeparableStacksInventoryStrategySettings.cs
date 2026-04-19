using System;
namespace UniversalDragAndDrop.Inventories
{
    [Serializable]
    public sealed class SeparableStacksInventoryStrategySettings : StackBasedInventoryStrategySettingsBase
    {
        public override IInventoryStrategy CreateRuntimeStrategy(UniversalInventory inventory)
        {
            return new SeparableStacksStrategy(inventory, DefaultMaxStackSize, AllowItemStackOverride);
        }

        public override UniversalInventory.ItemBehaviorType GetLegacyBehaviorType()
        {
            return UniversalInventory.ItemBehaviorType.SeparableStacks;
        }
    }
}
