using UDND.Core;

namespace UDND.Inventories
{
    /// <summary>
    /// Result of a slot swap operation. Contains copies of stacks before and after the swap.
    /// </summary>
    public readonly struct SwapOperationResult
    {
        public SwapOperationResult(
            ItemStack targetStackBefore,
            ItemStack sourceStackBefore,
            ItemStack targetStackAfter,
            ItemStack sourceStackAfter,
            PlacementSnapshot targetRemovedSnapshot = null,
            PlacementSnapshot sourceRemovedSnapshot = null,
            PlacementSnapshot targetAddedSnapshot = null,
            PlacementSnapshot sourceAddedSnapshot = null)
        {
            TargetStackBefore = targetStackBefore;
            SourceStackBefore = sourceStackBefore;
            TargetStackAfter = targetStackAfter;
            SourceStackAfter = sourceStackAfter;
            TargetRemovedSnapshot = targetRemovedSnapshot;
            SourceRemovedSnapshot = sourceRemovedSnapshot;
            TargetAddedSnapshot = targetAddedSnapshot;
            SourceAddedSnapshot = sourceAddedSnapshot;
        }

        public ItemStack TargetStackBefore { get; }
        public ItemStack SourceStackBefore { get; }
        public ItemStack TargetStackAfter { get; }
        public ItemStack SourceStackAfter { get; }

        // Placement geometry captured around the swap so item events carry the correct footprint
        // (anchor/orientation/covered cells) instead of lazily resolving it from post-swap state.
        /// <summary>Footprint of the target item before it was removed (its real geometry).</summary>
        public PlacementSnapshot TargetRemovedSnapshot { get; }
        /// <summary>Footprint of the source item before it was removed.</summary>
        public PlacementSnapshot SourceRemovedSnapshot { get; }
        /// <summary>Footprint of the item placed into the target inventory.</summary>
        public PlacementSnapshot TargetAddedSnapshot { get; }
        /// <summary>Footprint of the item placed into the source inventory.</summary>
        public PlacementSnapshot SourceAddedSnapshot { get; }

        public bool HasData =>
            TargetStackBefore != null &&
            SourceStackBefore != null &&
            TargetStackAfter != null &&
            SourceStackAfter != null;
    }
}
