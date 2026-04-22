using System;

namespace UniversalDragAndDrop.Core
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

    public readonly struct DropRequestPolicy
    {
        public DropRequestPolicy(
            BlockedTargetResolverBase blockedTargetResolver,
            bool? allowPartial = null)
        {
            BlockedTargetResolver = blockedTargetResolver;
            AllowPartial = allowPartial;
        }

        public BlockedTargetResolverBase BlockedTargetResolver { get; }
        public bool? AllowPartial { get; }

        public static DropRequestPolicy WithResolver(BlockedTargetResolverBase resolver)
        {
            return new DropRequestPolicy(resolver);
        }

        public static DropRequestPolicy WithSwap()
        {
            return new DropRequestPolicy(new SwapBlockedTargetResolver());
        }

        public static DropRequestPolicy WithFindAlternative(IAlternativePlacementStrategy placementStrategy = null)
        {
            var resolver = new FindAlternativeBlockedTargetResolver();
            if (placementStrategy != null)
                resolver.SetAlternativePlacementStrategy(placementStrategy);

            return new DropRequestPolicy(resolver);
        }

        public static DropRequestPolicy WithPartial(bool allowPartial)
        {
            return new DropRequestPolicy(null, allowPartial);
        }

        public static DropRequestPolicy? Merge(DropRequestPolicy? basePolicy, DropRequestPolicy? overridingPolicy)
        {
            if (!basePolicy.HasValue)
                return overridingPolicy;

            if (!overridingPolicy.HasValue)
                return basePolicy;

            var baseValue = basePolicy.Value;
            var overridingValue = overridingPolicy.Value;
            return new DropRequestPolicy(
                overridingValue.BlockedTargetResolver ?? baseValue.BlockedTargetResolver,
                overridingValue.AllowPartial ?? baseValue.AllowPartial);
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
        {
            BlockedTargetResolver = blockedTargetResolver;
            AllowPartial = allowPartial;
            BatchMode = batchMode;
        }

        public BlockedTargetResolverBase BlockedTargetResolver { get; }
        public bool AllowPartial { get; }
        public BatchMode BatchMode { get; }
    }
}