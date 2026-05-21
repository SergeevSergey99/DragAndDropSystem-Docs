using System;

namespace UDND.Inventories
{
    internal readonly struct StrategyConfiguration : IEquatable<StrategyConfiguration>
    {
        public StrategyConfiguration(
            string strategyType,
            string strategyJson,
            string slotManagementType,
            string slotManagementJson,
            bool allowMergeOnDrop,
            bool useGridTopology,
            string gridTopology,
            string slotShapedItemPolicy)
        {
            StrategyType = strategyType ?? string.Empty;
            StrategyJson = strategyJson ?? string.Empty;
            SlotManagementType = slotManagementType ?? string.Empty;
            SlotManagementJson = slotManagementJson ?? string.Empty;
            AllowMergeOnDrop = allowMergeOnDrop;
            UseGridTopology = useGridTopology;
            GridTopology = gridTopology ?? string.Empty;
            SlotShapedItemPolicy = slotShapedItemPolicy ?? string.Empty;
        }

        public string StrategyType { get; }
        public string StrategyJson { get; }
        public string SlotManagementType { get; }
        public string SlotManagementJson { get; }
        public bool AllowMergeOnDrop { get; }
        public bool UseGridTopology { get; }
        public string GridTopology { get; }
        public string SlotShapedItemPolicy { get; }

        public bool Equals(StrategyConfiguration other)
        {
            return StrategyType == other.StrategyType
                && StrategyJson == other.StrategyJson
                && SlotManagementType == other.SlotManagementType
                && SlotManagementJson == other.SlotManagementJson
                && AllowMergeOnDrop == other.AllowMergeOnDrop
                && UseGridTopology == other.UseGridTopology
                && GridTopology == other.GridTopology
                && SlotShapedItemPolicy == other.SlotShapedItemPolicy;
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
                hash = (hash * 397) ^ UseGridTopology.GetHashCode();
                hash = (hash * 397) ^ GridTopology.GetHashCode();
                hash = (hash * 397) ^ SlotShapedItemPolicy.GetHashCode();
                return hash;
            }
        }
    }
}
