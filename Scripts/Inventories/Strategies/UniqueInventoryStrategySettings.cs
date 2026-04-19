using System;

namespace UniversalDragAndDrop.Inventories
{
    [Serializable]
    public sealed class UniqueInventoryStrategySettings : InventoryStrategySettingsBase
    {
        protected override bool ShowDragAmountSettings => false;

        public override IInventoryStrategy CreateRuntimeStrategy(UniversalInventory inventory)
        {
            return new UniqueItemStrategy();
        }

        public override UniversalInventory.ItemBehaviorType GetLegacyBehaviorType()
        {
            return UniversalInventory.ItemBehaviorType.Unique;
        }
    }
}
