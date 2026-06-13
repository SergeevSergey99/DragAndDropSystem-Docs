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

    public enum BatchMode : byte
    {
        Atomic = 0,
        BestEffort = 1
    }

    public enum PartialTransferMode : byte
    {
        Allow = 0,
        RequireFull = 1
    }

    public readonly struct DropRequestPolicy
    {
        public DropRequestPolicy(
            BlockedTargetResolverBase blockedTargetResolver,
            bool? allowPartial = null)
            : this(
                ResolveKind(blockedTargetResolver),
                ResolveOrderer(blockedTargetResolver),
                ResolveSameInventoryAlternative(blockedTargetResolver),
                allowPartial.HasValue
                    ? allowPartial.Value
                        ? Core.PartialTransferMode.Allow
                        : Core.PartialTransferMode.RequireFull
                    : (Core.PartialTransferMode?)null,
                blockedTargetResolver)
        {
        }

        public DropRequestPolicy(
            BlockedTargetResolutionKind? blockedTargetResolution,
            PlacementCandidateOrderer alternativeOrderer = null,
            bool? allowSameInventoryAlternativePlacement = null,
            PartialTransferMode? partialTransferMode = null)
            : this(
                blockedTargetResolution,
                alternativeOrderer,
                allowSameInventoryAlternativePlacement,
                partialTransferMode,
                null)
        {
        }

        private DropRequestPolicy(
            BlockedTargetResolutionKind? blockedTargetResolution,
            PlacementCandidateOrderer alternativeOrderer,
            bool? allowSameInventoryAlternativePlacement,
            PartialTransferMode? partialTransferMode,
            BlockedTargetResolverBase legacyResolver)
        {
            BlockedTargetResolution = blockedTargetResolution;
            AlternativeOrderer = alternativeOrderer;
            AllowSameInventoryAlternativePlacement = allowSameInventoryAlternativePlacement;
            PartialTransferMode = partialTransferMode;
            BlockedTargetResolver = legacyResolver;
        }

        public BlockedTargetResolutionKind? BlockedTargetResolution { get; }
        public PlacementCandidateOrderer AlternativeOrderer { get; }
        public bool? AllowSameInventoryAlternativePlacement { get; }
        public PartialTransferMode? PartialTransferMode { get; }

        // Legacy projection used by TransferPlanner until the JIT pipeline replaces it.
        public BlockedTargetResolverBase BlockedTargetResolver { get; }
        public bool? AllowPartial => PartialTransferMode.HasValue
            ? PartialTransferMode.Value == Core.PartialTransferMode.Allow
            : (bool?)null;

        public static DropRequestPolicy WithResolver(BlockedTargetResolverBase resolver)
        {
            return new DropRequestPolicy(resolver);
        }

        public static DropRequestPolicy WithSwap()
        {
            return new DropRequestPolicy(
                BlockedTargetResolutionKind.Swap,
                null,
                null,
                null,
                legacyResolver: new SwapBlockedTargetResolver());
        }

        public static DropRequestPolicy WithFindAlternative(
            IAlternativePlacementStrategy placementStrategy = null,
            bool allowSameInventoryAlternativePlacement = true)
        {
            var resolver = new FindAlternativeBlockedTargetResolver(
                placementStrategy,
                allowSameInventoryAlternativePlacement);
            return new DropRequestPolicy(
                BlockedTargetResolutionKind.AlternativeSlots,
                ResolveOrderer(resolver),
                allowSameInventoryAlternativePlacement,
                null,
                resolver);
        }

        public static DropRequestPolicy WithAlternativeOrderer(
            PlacementCandidateOrderer orderer = null,
            bool allowSameInventoryAlternativePlacement = true)
        {
            return new DropRequestPolicy(
                BlockedTargetResolutionKind.AlternativeSlots,
                orderer ?? MergeFirstPlacementCandidateOrderer.Instance,
                allowSameInventoryAlternativePlacement);
        }

        public static DropRequestPolicy WithPartial(bool allowPartial)
        {
            return new DropRequestPolicy(
                null,
                partialTransferMode: allowPartial
                    ? Core.PartialTransferMode.Allow
                    : Core.PartialTransferMode.RequireFull);
        }

        public static DropRequestPolicy? Merge(DropRequestPolicy? basePolicy, DropRequestPolicy? overridingPolicy)
        {
            if (!basePolicy.HasValue)
                return overridingPolicy;

            if (!overridingPolicy.HasValue)
                return basePolicy;

            var baseValue = basePolicy.Value;
            var overridingValue = overridingPolicy.Value;
            var legacyResolver =
                overridingValue.BlockedTargetResolver ?? baseValue.BlockedTargetResolver;
            return new DropRequestPolicy(
                overridingValue.BlockedTargetResolution ?? baseValue.BlockedTargetResolution,
                overridingValue.AlternativeOrderer ?? baseValue.AlternativeOrderer,
                overridingValue.AllowSameInventoryAlternativePlacement ??
                baseValue.AllowSameInventoryAlternativePlacement,
                overridingValue.PartialTransferMode ?? baseValue.PartialTransferMode,
                legacyResolver);
        }

        internal static BlockedTargetResolutionKind? ResolveKind(
            BlockedTargetResolverBase resolver)
        {
            if (resolver == null)
                return null;
            if (resolver is FindAlternativeBlockedTargetResolver)
                return BlockedTargetResolutionKind.AlternativeSlots;
            if (resolver is SwapBlockedTargetResolver)
                return BlockedTargetResolutionKind.Swap;
            return BlockedTargetResolutionKind.Reject;
        }

        internal static PlacementCandidateOrderer ResolveOrderer(
            BlockedTargetResolverBase resolver)
        {
            if (resolver is not FindAlternativeBlockedTargetResolver alternative)
                return null;

            return alternative.AlternativePlacementStrategy switch
            {
                EmptyFirstAlternativePlacementStrategy _ =>
                    EmptyFirstPlacementCandidateOrderer.Instance,
                EmptyOnlyAlternativePlacementStrategy _ =>
                    new EmptyOnlyPlacementCandidateOrderer(),
                MergeOnlyAlternativePlacementStrategy _ =>
                    new MergeOnlyPlacementCandidateOrderer(),
                _ => MergeFirstPlacementCandidateOrderer.Instance
            };
        }

        internal static bool? ResolveSameInventoryAlternative(
            BlockedTargetResolverBase resolver)
        {
            return resolver is FindAlternativeBlockedTargetResolver alternative
                ? alternative.AllowSameInventoryAlternativePlacement
                : (bool?)null;
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
            BlockedTargetResolverBase blockedTargetResolver,
            bool allowPartial,
            BatchMode batchMode)
            : this(
                DropRequestPolicy.ResolveKind(blockedTargetResolver) ??
                BlockedTargetResolutionKind.AlternativeSlots,
                DropRequestPolicy.ResolveOrderer(blockedTargetResolver) ??
                MergeFirstPlacementCandidateOrderer.Instance,
                DropRequestPolicy.ResolveSameInventoryAlternative(blockedTargetResolver) ?? true,
                allowPartial
                    ? PartialTransferMode.Allow
                    : PartialTransferMode.RequireFull,
                batchMode,
                blockedTargetResolver)
        {
        }

        public ResolvedDropPolicy(
            BlockedTargetResolutionKind blockedTargetResolution,
            PlacementCandidateOrderer alternativeOrderer,
            bool allowSameInventoryAlternativePlacement,
            PartialTransferMode partialTransferMode,
            BatchMode batchMode = BatchMode.BestEffort,
            BlockedTargetResolverBase legacyResolver = null)
        {
            BlockedTargetResolution = blockedTargetResolution;
            AlternativeOrderer = alternativeOrderer ??
                MergeFirstPlacementCandidateOrderer.Instance;
            AllowSameInventoryAlternativePlacement =
                allowSameInventoryAlternativePlacement;
            PartialTransferMode = partialTransferMode;
            BatchMode = batchMode;
            BlockedTargetResolver = legacyResolver;
        }

        public BlockedTargetResolutionKind BlockedTargetResolution { get; }
        public PlacementCandidateOrderer AlternativeOrderer { get; }
        public bool AllowSameInventoryAlternativePlacement { get; }
        public PartialTransferMode PartialTransferMode { get; }

        // Legacy projections used while TransferPlanner/Executor are still present.
        public BlockedTargetResolverBase BlockedTargetResolver { get; }
        public bool AllowPartial => PartialTransferMode == Core.PartialTransferMode.Allow;
        public BatchMode BatchMode { get; }
    }
}
