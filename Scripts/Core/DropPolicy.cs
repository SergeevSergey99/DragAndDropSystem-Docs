using System;
using DragAndDropSystem.Tools.Inspector;
using UnityEngine;

namespace DragAndDropSystem.Core
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

    [Serializable]
    public sealed class DropPolicySettings
    {
        [SerializeField, Tooltip("What to do if nothing can be placed into the target slot: reject, try swapping, or look for another slot.")]
        private BlockedTargetBehavior _blockedTarget = BlockedTargetBehavior.FindAlternative;
        [SerializeField, Tooltip("For SeparableStacks only: allow merge when explicitly dropping onto an occupied slot with the same item.")]
        private bool _allowMergeOnDrop = true;
        [SerializeField, Tooltip("Allow partial transfer if only part of the requested amount fits.")]
        private bool _allowPartial = true;
        [SerializeField, Tooltip("How to process batch transfers: Atomic cancels the whole operation on the first error, BestEffort transfers whatever succeeds.")]
        private BatchMode _batchMode = BatchMode.BestEffort;
        [SerializeField, Tooltip("Order of alternative slot search for FindAlternative. Used by the placement strategy.")]
        private AlternativePlacementMode _alternativePlacement = AlternativePlacementMode.MergeFirst;

        public bool AllowMergeOnDrop => _allowMergeOnDrop;

        public ResolvedDropPolicy Resolve(DropRequestPolicy? requested, DragContext context)
        {
            var blocked = requested.HasValue && requested.Value.BlockedTarget.HasValue
                ? requested.Value.BlockedTarget.Value
                : _blockedTarget;

            var alternativePlacement = requested.HasValue && requested.Value.AlternativePlacement.HasValue
                ? requested.Value.AlternativePlacement.Value
                : _alternativePlacement;

            var allowPartial = requested.HasValue && requested.Value.AllowPartial.HasValue
                ? requested.Value.AllowPartial.Value
                : _allowPartial;

            return new ResolvedDropPolicy(
                blocked,
                allowPartial,
                _batchMode,
                alternativePlacement);
        }
    }

    [Serializable]
    public sealed class DropRequestPolicySettings
    {
        [SerializeField] private bool _overrideBlockedTarget;
        [SerializeField, ShowIf(nameof(_overrideBlockedTarget))]
        private BlockedTargetBehavior _blockedTarget = BlockedTargetBehavior.FindAlternative;

        [SerializeField] private bool _overrideAlternativePlacement;
        [SerializeField, ShowIf(nameof(_overrideAlternativePlacement))]
        private AlternativePlacementMode _alternativePlacement = AlternativePlacementMode.MergeFirst;

        [SerializeField] private bool _overrideAllowPartial;
        [SerializeField, ShowIf(nameof(_overrideAllowPartial))]
        private bool _allowPartial = true;

        public DropRequestPolicy? TryBuild()
        {
            if (!_overrideBlockedTarget && !_overrideAlternativePlacement && !_overrideAllowPartial)
                return null;

            return new DropRequestPolicy(
                _overrideBlockedTarget ? _blockedTarget : (BlockedTargetBehavior?)null,
                _overrideAlternativePlacement ? _alternativePlacement : (AlternativePlacementMode?)null,
                _overrideAllowPartial ? _allowPartial : (bool?)null);
        }
    }

    [Serializable]
    public sealed class DragRequestPolicySettings
    {
        [SerializeField, LabelText("Override Drag Amount"), Tooltip("Temporarily overrides the item amount only for the current StartDrag.")]
        private bool _overrideAmount;
        [SerializeField, ShowIf(nameof(_overrideAmount)), LabelText("Amount"), Tooltip("How many items to take from the source stack when starting a drag.")]
        private DragAmount _amount = DragAmount.All;
        [SerializeField, ShowIf(nameof(ShowCustomAmount)), LabelText("Custom Amount"), Tooltip("Used only when Amount = Custom.")]
        private int _customAmount = 1;

        private bool ShowCustomAmount => _overrideAmount && _amount == DragAmount.Custom;

        public DragRequestPolicy? TryBuild()
        {
            if (!_overrideAmount)
                return null;

            return new DragRequestPolicy(_amount, _customAmount);
        }
    }
}
