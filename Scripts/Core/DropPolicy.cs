using System;
using UnityEngine;

namespace UniversalDragAndDrop.Core
{
    public enum BlockedTargetBehavior : byte
    {
        Reject = 0,
        Swap = 1,
        FindAlternative = 2
    }

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

    public enum AlternativePlacementMode : byte
    {
        MergeFirst = 0,
        EmptyFirst = 1,
        MergeOnly = 2,
        EmptyOnly = 3
    }

    public readonly struct DropRequestPolicy
    {
        public DropRequestPolicy(
            BlockedTargetBehavior? blockedTarget,
            AlternativePlacementMode? alternativePlacement = null,
            bool? allowPartial = null)
        {
            BlockedTarget = blockedTarget;
            AlternativePlacement = alternativePlacement;
            AllowPartial = allowPartial;
        }

        public BlockedTargetBehavior? BlockedTarget { get; }
        public AlternativePlacementMode? AlternativePlacement { get; }
        public bool? AllowPartial { get; }

        public static DropRequestPolicy WithBlocked(BlockedTargetBehavior behavior)
        {
            return new DropRequestPolicy(behavior);
        }

        public static DropRequestPolicy WithSwap()
        {
            return new DropRequestPolicy(BlockedTargetBehavior.Swap);
        }

        public static DropRequestPolicy WithFindAlternative(AlternativePlacementMode? placement = null)
        {
            return new DropRequestPolicy(BlockedTargetBehavior.FindAlternative, placement);
        }

        public static DropRequestPolicy WithPartial(bool allowPartial)
        {
            return new DropRequestPolicy(null, null, allowPartial);
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
                overridingValue.BlockedTarget ?? baseValue.BlockedTarget,
                overridingValue.AlternativePlacement ?? baseValue.AlternativePlacement,
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
            BlockedTargetBehavior blockedTarget,
            bool allowPartial,
            BatchMode batchMode,
            AlternativePlacementMode alternativePlacement)
        {
            BlockedTarget = blockedTarget;
            AllowPartial = allowPartial;
            BatchMode = batchMode;
            AlternativePlacement = alternativePlacement;
        }

        public BlockedTargetBehavior BlockedTarget { get; }
        public bool AllowPartial { get; }
        public BatchMode BatchMode { get; }
        public AlternativePlacementMode AlternativePlacement { get; }
    }

}
