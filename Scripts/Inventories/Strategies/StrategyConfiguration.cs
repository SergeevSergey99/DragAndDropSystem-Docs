using System;

namespace UniversalDragAndDrop.Inventories
{
    internal readonly struct StrategyConfiguration : IEquatable<StrategyConfiguration>
    {
        public StrategyConfiguration(
            string strategyType,
            string strategyJson,
            string slotManagementType,
            string slotManagementJson,
            bool allowMergeOnDrop)
        {
            StrategyType = strategyType ?? string.Empty;
            StrategyJson = strategyJson ?? string.Empty;
            SlotManagementType = slotManagementType ?? string.Empty;
            SlotManagementJson = slotManagementJson ?? string.Empty;
            AllowMergeOnDrop = allowMergeOnDrop;
        }

        public string StrategyType { get; }
        public string StrategyJson { get; }
        public string SlotManagementType { get; }
        public string SlotManagementJson { get; }
        public bool AllowMergeOnDrop { get; }

        public bool Equals(StrategyConfiguration other)
        {
            return StrategyType == other.StrategyType
                && StrategyJson == other.StrategyJson
                && SlotManagementType == other.SlotManagementType
                && SlotManagementJson == other.SlotManagementJson
                && AllowMergeOnDrop == other.AllowMergeOnDrop;
        }

        public override bool Equals(object obj) => obj is StrategyConfiguration other && Equals(other);

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = StrategyType.GetHashCode();
                hash = (hash * 397) ^ StrategyJson.GetHashCode();
                hash = (hash * 397) ^ SlotManagementType.GetHashCode();
                hash = (hash * 397) ^ SlotManagementJson.GetHashCode();
                hash = (hash * 397) ^ AllowMergeOnDrop.GetHashCode();
                return hash;
            }
        }
    }
}
