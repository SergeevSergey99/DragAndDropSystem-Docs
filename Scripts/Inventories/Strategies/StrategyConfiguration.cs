using System;

namespace UniversalDragAndDrop.Inventories
{
    internal readonly struct StrategyConfiguration : IEquatable<StrategyConfiguration>
    {
        public StrategyConfiguration(
            UniversalInventory.ItemBehaviorType itemBehavior,
            int maxStackSize,
            bool allowItemStackOverride,
            UniversalInventory.SlotManagementType slotManagement,
            int maxDynamicSlots,
            int maxFreeSlots,
            bool allowMergeOnDrop)
        {
            ItemBehavior = itemBehavior;
            MaxStackSize = maxStackSize;
            AllowItemStackOverride = allowItemStackOverride;
            SlotManagement = slotManagement;
            MaxDynamicSlots = maxDynamicSlots;
            MaxFreeSlots = maxFreeSlots;
            AllowMergeOnDrop = allowMergeOnDrop;
        }

        public UniversalInventory.ItemBehaviorType ItemBehavior { get; }
        public int MaxStackSize { get; }
        public bool AllowItemStackOverride { get; }
        public UniversalInventory.SlotManagementType SlotManagement { get; }
        public int MaxDynamicSlots { get; }
        public int MaxFreeSlots { get; }
        public bool AllowMergeOnDrop { get; }

        public bool Equals(StrategyConfiguration other)
        {
            return ItemBehavior == other.ItemBehavior
                && MaxStackSize == other.MaxStackSize
                && AllowItemStackOverride == other.AllowItemStackOverride
                && SlotManagement == other.SlotManagement
                && MaxDynamicSlots == other.MaxDynamicSlots
                && MaxFreeSlots == other.MaxFreeSlots
                && AllowMergeOnDrop == other.AllowMergeOnDrop;
        }

        public override bool Equals(object obj) => obj is StrategyConfiguration other && Equals(other);

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = (int)ItemBehavior;
                hash = (hash * 397) ^ MaxStackSize;
                hash = (hash * 397) ^ AllowItemStackOverride.GetHashCode();
                hash = (hash * 397) ^ (int)SlotManagement;
                hash = (hash * 397) ^ MaxDynamicSlots;
                hash = (hash * 397) ^ MaxFreeSlots;
                hash = (hash * 397) ^ AllowMergeOnDrop.GetHashCode();
                return hash;
            }
        }
    }
}