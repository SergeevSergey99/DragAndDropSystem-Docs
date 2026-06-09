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
            bool useGridTopology,
            string gridTopology)
        {
            StrategyType = strategyType ?? string.Empty;
            StrategyJson = strategyJson ?? string.Empty;
            SlotManagementType = slotManagementType ?? string.Empty;
            SlotManagementJson = slotManagementJson ?? string.Empty;
            UseGridTopology = useGridTopology;
            GridTopology = gridTopology ?? string.Empty;
        }

        public string StrategyType { get; }
        public string StrategyJson { get; }
        public string SlotManagementType { get; }
        public string SlotManagementJson { get; }
        public bool UseGridTopology { get; }
        public string GridTopology { get; }

        public bool Equals(StrategyConfiguration other)
        {
            return StrategyType == other.StrategyType
                && StrategyJson == other.StrategyJson
                && SlotManagementType == other.SlotManagementType
                && SlotManagementJson == other.SlotManagementJson
                && UseGridTopology == other.UseGridTopology
                && GridTopology == other.GridTopology;
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
                hash = (hash * 397) ^ UseGridTopology.GetHashCode();
                hash = (hash * 397) ^ GridTopology.GetHashCode();
                return hash;
            }
        }
    }
}
