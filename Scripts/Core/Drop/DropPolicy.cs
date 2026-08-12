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

    /// <summary>How many placements one incoming swap entry may displace.</summary>
    public enum MultiSwapMode : byte
    {
        /// <summary>Preserve the legacy one-placement swap behavior.</summary>
        Single = 0,

        /// <summary>Displace every placement covered by the incoming footprint and preserve offsets.</summary>
        PreserveOffsets = 1
    }

    public readonly struct DropRequestPolicy
    {
        public DropRequestPolicy(
            BlockedTargetResolutionKind? blockedTargetResolution,
            PlacementCandidateOrderer alternativeOrderer = null,
            bool? allowSameInventoryAlternativePlacement = null,
            PartialTransferMode? partialTransferMode = null,
            MultiSwapMode? multiSwapMode = null)
        {
            BlockedTargetResolution = blockedTargetResolution;
            AlternativeOrderer = alternativeOrderer;
            AllowSameInventoryAlternativePlacement = allowSameInventoryAlternativePlacement;
            PartialTransferMode = partialTransferMode;
            MultiSwapMode = multiSwapMode;
        }

        public BlockedTargetResolutionKind? BlockedTargetResolution { get; }
        public PlacementCandidateOrderer AlternativeOrderer { get; }
        public bool? AllowSameInventoryAlternativePlacement { get; }
        public PartialTransferMode? PartialTransferMode { get; }
        public MultiSwapMode? MultiSwapMode { get; }

        public static DropRequestPolicy WithReject()
            => new DropRequestPolicy(BlockedTargetResolutionKind.Reject);

        public static DropRequestPolicy WithSwap(MultiSwapMode mode = Core.MultiSwapMode.Single)
            => new DropRequestPolicy(BlockedTargetResolutionKind.Swap, multiSwapMode: mode);

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
                overridingValue.MultiSwapMode ?? baseValue.MultiSwapMode);
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
            MultiSwapMode multiSwapMode = Core.MultiSwapMode.Single)
        {
            BlockedTargetResolution = blockedTargetResolution;
            AlternativeOrderer = alternativeOrderer;
            AllowSameInventoryAlternativePlacement = allowSameInventoryAlternativePlacement;
            PartialTransferMode = partialTransferMode;
            MultiSwapMode = multiSwapMode;
        }

        public BlockedTargetResolutionKind BlockedTargetResolution { get; }
        public PlacementCandidateOrderer AlternativeOrderer { get; }
        public bool AllowSameInventoryAlternativePlacement { get; }
        public PartialTransferMode PartialTransferMode { get; }
        public MultiSwapMode MultiSwapMode { get; }
    }
}
