using System;

namespace UniversalDragAndDrop.Inventories
{
    internal readonly struct StrategyConfiguration : IEquatable<StrategyConfiguration>
    {
        public StrategyConfiguration(
            string strategyType,
            string strategyJson,
            UniversalInventory.SlotManagementType slotManagement,
            int maxDynamicSlots,
            int maxFreeSlots,
            bool allowMergeOnDrop)
        {
            StrategyType = strategyType ?? string.Empty;
            StrategyJson = strategyJson ?? string.Empty;
            SlotManagement = slotManagement;
            MaxDynamicSlots = maxDynamicSlots;
            MaxFreeSlots = maxFreeSlots;
            AllowMergeOnDrop = allowMergeOnDrop;
        }

        public string StrategyType { get; }
        public string StrategyJson { get; }
        public UniversalInventory.SlotManagementType SlotManagement { get; }
        public int MaxDynamicSlots { get; }
        public int MaxFreeSlots { get; }
        public bool AllowMergeOnDrop { get; }

        public bool Equals(StrategyConfiguration other)
        {
            return StrategyType == other.StrategyType
                && StrategyJson == other.StrategyJson
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
                int hash = StrategyType.GetHashCode();
                hash = (hash * 397) ^ StrategyJson.GetHashCode();
                hash = (hash * 397) ^ (int)SlotManagement;
                hash = (hash * 397) ^ MaxDynamicSlots;
                hash = (hash * 397) ^ MaxFreeSlots;
                hash = (hash * 397) ^ AllowMergeOnDrop.GetHashCode();
                return hash;
            }
        }
    }
}
