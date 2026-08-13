using System;
using UDND.Inventories;

namespace UDND.Core
{
    public enum DragAmount : byte
    {
        All = 0,
        HalfDown = 1,
        HalfUp = 4,
        One = 2,
        Custom = 3
    }

    public enum DragAmountStepRounding : byte
    {
        Floor = 0,
        Ceil = 1,
        Nearest = 2
    }

    public enum PartialTransferMode : byte
    {
        Allow = 0,
        RequireFull = 1
    }

    public enum BlockedTargetResolutionKind : byte
    {
        Reject = 0,
        FindAlternative = 1,
        Swap = 2
    }

    /// <summary>
    /// How many placements one incoming swap entry may displace.
    /// <para>
    /// Only meaningful where an item can cover more than one cell. In an inventory whose items
    /// always occupy exactly one cell the incoming footprint can never reach a second placement,
    /// so both values behave identically.
    /// </para>
    /// </summary>
    public enum SwapDisplacementMode : byte
    {
        /// <summary>Displace at most the one placement under the target slot.</summary>
        SinglePlacement = 0,

        /// <summary>Displace every placement the incoming footprint covers.</summary>
        AllCoveredPlacements = 1
    }

    /// <summary>
    /// What a swap does when the incoming footprint covers a placement only partly.
    /// <para>
    /// Items of different shapes never cover each other exactly, so this is the common case rather
    /// than an edge one, and where the displaced item should go has no single obvious answer.
    /// </para>
    /// </summary>
    public enum PartialOverlapSwapMode : byte
    {
        /// <summary>
        /// Refuse the swap when a displaced item is not covered whole although the incoming shape
        /// could have contained it. An item too small or the wrong shape to ever cover the one under
        /// it still swaps: otherwise dropping a small item onto a large one could never work.
        /// </summary>
        Reject = 0,

        /// <summary>
        /// Place the displaced item at the position its own grab offset implies: it keeps the offset
        /// it had from the incoming item, measured from the source anchor. If that does not fit, the
        /// swap is refused.
        /// </summary>
        WithDragOffset = 1,

        /// <summary>
        /// Search for a free position, preferring the cells the swap frees, then positions that lean
        /// on those cells and extend into free neighbours, and only then anywhere in the source.
        /// </summary>
        VacatedArea = 2
    }

    public readonly struct DropRequestPolicy
    {
        public DropRequestPolicy(
            BlockedTargetResolutionKind? blockedTargetResolution,
            PlacementCandidateOrderer alternativeOrderer = null,
            bool? allowSameInventoryAlternativePlacement = null,
            PartialTransferMode? partialTransferMode = null,
            SwapDisplacementMode? swapDisplacementMode = null,
            PartialOverlapSwapMode? partialOverlapSwap = null)
        {
            BlockedTargetResolution = blockedTargetResolution;
            AlternativeOrderer = alternativeOrderer;
            AllowSameInventoryAlternativePlacement = allowSameInventoryAlternativePlacement;
            PartialTransferMode = partialTransferMode;
            SwapDisplacementMode = swapDisplacementMode;
            PartialOverlapSwap = partialOverlapSwap;
        }

        public BlockedTargetResolutionKind? BlockedTargetResolution { get; }
        public PlacementCandidateOrderer AlternativeOrderer { get; }
        public bool? AllowSameInventoryAlternativePlacement { get; }
        public PartialTransferMode? PartialTransferMode { get; }
        public SwapDisplacementMode? SwapDisplacementMode { get; }
        public PartialOverlapSwapMode? PartialOverlapSwap { get; }

        public static DropRequestPolicy WithReject()
            => new DropRequestPolicy(BlockedTargetResolutionKind.Reject);

        public static DropRequestPolicy WithSwap(
            SwapDisplacementMode mode = Core.SwapDisplacementMode.SinglePlacement,
            PartialOverlapSwapMode partialOverlap = Core.PartialOverlapSwapMode.Reject)
            => new DropRequestPolicy(
                BlockedTargetResolutionKind.Swap,
                swapDisplacementMode: mode,
                partialOverlapSwap: partialOverlap);

        public static DropRequestPolicy WithAlternativeOrderer(
            PlacementCandidateOrderer orderer = null,
            bool allowSameInventoryAlternativePlacement = true)
            => new DropRequestPolicy(BlockedTargetResolutionKind.FindAlternative, orderer, allowSameInventoryAlternativePlacement);

        public static DropRequestPolicy WithPartial(bool allowPartial)
            => new DropRequestPolicy(
                null,
                partialTransferMode: allowPartial
                    ? Core.PartialTransferMode.Allow
                    : Core.PartialTransferMode.RequireFull);

        public static DropRequestPolicy? Merge(
            DropRequestPolicy? basePolicy,
            DropRequestPolicy? overridingPolicy)
        {
            if (!basePolicy.HasValue)
                return overridingPolicy;
            if (!overridingPolicy.HasValue)
                return basePolicy;

            var baseValue = basePolicy.Value;
            var overridingValue = overridingPolicy.Value;
            return new DropRequestPolicy(
                overridingValue.BlockedTargetResolution ?? baseValue.BlockedTargetResolution,
                overridingValue.AlternativeOrderer ?? baseValue.AlternativeOrderer,
                overridingValue.AllowSameInventoryAlternativePlacement ??
                baseValue.AllowSameInventoryAlternativePlacement,
                overridingValue.PartialTransferMode ?? baseValue.PartialTransferMode,
                overridingValue.SwapDisplacementMode ?? baseValue.SwapDisplacementMode,
                overridingValue.PartialOverlapSwap ?? baseValue.PartialOverlapSwap);
        }
    }

    public readonly struct DragRequestPolicy
    {
        public DragRequestPolicy(DragAmount amount, int customAmount = 0)
        {
            Amount = amount;
            CustomAmount = amount == DragAmount.Custom ? Math.Max(1, customAmount) : 0;
        }

        public DragAmount? Amount { get; }
        public int CustomAmount { get; }
    }

    public readonly struct ResolvedDropPolicy
    {
        public ResolvedDropPolicy(
            BlockedTargetResolutionKind blockedTargetResolution,
            PlacementCandidateOrderer alternativeOrderer,
            bool allowSameInventoryAlternativePlacement,
            PartialTransferMode partialTransferMode,
            SwapDisplacementMode swapDisplacementMode = Core.SwapDisplacementMode.SinglePlacement,
            PartialOverlapSwapMode partialOverlapSwap = Core.PartialOverlapSwapMode.Reject)
        {
            BlockedTargetResolution = blockedTargetResolution;
            AlternativeOrderer = alternativeOrderer;
            AllowSameInventoryAlternativePlacement = allowSameInventoryAlternativePlacement;
            PartialTransferMode = partialTransferMode;
            SwapDisplacementMode = swapDisplacementMode;
            PartialOverlapSwap = partialOverlapSwap;
        }

        public BlockedTargetResolutionKind BlockedTargetResolution { get; }
        public PlacementCandidateOrderer AlternativeOrderer { get; }
        public bool AllowSameInventoryAlternativePlacement { get; }
        public PartialTransferMode PartialTransferMode { get; }
        public SwapDisplacementMode SwapDisplacementMode { get; }
        public PartialOverlapSwapMode PartialOverlapSwap { get; }
    }
}
