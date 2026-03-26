using System;
using DragAndDropSystem.Inspector;
using UnityEngine;

namespace DragAndDropSystem.Core
{
    public enum BlockedTargetBehavior : byte
    {
        Reject = 0,
        Swap = 1,
        FindAlternative = 2
    }

    public enum TargetMode : byte
    {
        Strict = 0,
        Hint = 1
    }

    public enum DragAmount : byte
    {
        All = 0,
        Half = 1,
        One = 2,
        Custom = 3
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
            TargetMode? target = null,
            AlternativePlacementMode? alternativePlacement = null,
            bool? allowPartial = null)
        {
            BlockedTarget = blockedTarget;
            Target = target;
            AlternativePlacement = alternativePlacement;
            AllowPartial = allowPartial;
        }

        public BlockedTargetBehavior? BlockedTarget { get; }
        public TargetMode? Target { get; }
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
            return new DropRequestPolicy(BlockedTargetBehavior.FindAlternative, TargetMode.Hint, placement);
        }

        public static DropRequestPolicy WithPartial(bool allowPartial)
        {
            return new DropRequestPolicy(null, allowPartial: allowPartial);
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
                overridingValue.Target ?? baseValue.Target,
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

        public static readonly DragRequestPolicy All = new DragRequestPolicy(DragAmount.All);
        public static readonly DragRequestPolicy Half = new DragRequestPolicy(DragAmount.Half);
        public static readonly DragRequestPolicy One = new DragRequestPolicy(DragAmount.One);
    }

    public readonly struct ResolvedDropPolicy
    {
        public ResolvedDropPolicy(
            BlockedTargetBehavior blockedTarget,
            TargetMode target,
            bool allowPartial,
            BatchMode batchMode,
            AlternativePlacementMode alternativePlacement)
        {
            BlockedTarget = blockedTarget;
            Target = target;
            AllowPartial = allowPartial;
            BatchMode = batchMode;
            AlternativePlacement = alternativePlacement;
        }

        public BlockedTargetBehavior BlockedTarget { get; }
        public TargetMode Target { get; }
        public bool AllowPartial { get; }
        public BatchMode BatchMode { get; }
        public AlternativePlacementMode AlternativePlacement { get; }
    }

    [Serializable]
    public sealed class DropPolicySettings
    {
        [SerializeField] private BlockedTargetBehavior _blockedTarget = BlockedTargetBehavior.FindAlternative;
        [SerializeField] private TargetMode _targetMode = TargetMode.Strict;
        [SerializeField, Tooltip("Разрешить swap на этом инвентаре")]
        private bool _allowSwap = true;
        [SerializeField, Tooltip("Разрешить частичный перенос стека")]
        private bool _allowPartial = true;
        [SerializeField] private BatchMode _batchMode = BatchMode.BestEffort;
        [SerializeField] private AlternativePlacementMode _alternativePlacement = AlternativePlacementMode.MergeFirst;

        public ResolvedDropPolicy Resolve(DropRequestPolicy? requested, DragContext context)
        {
            var normalizedDefaultBlocked = !_allowSwap && _blockedTarget == BlockedTargetBehavior.Swap
                ? BlockedTargetBehavior.Reject
                : _blockedTarget;

            var blocked = requested.HasValue && requested.Value.BlockedTarget.HasValue
                ? requested.Value.BlockedTarget.Value
                : normalizedDefaultBlocked;

            var target = requested.HasValue && requested.Value.Target.HasValue
                ? requested.Value.Target.Value
                : (context != null && context.IsBatchDrag ? TargetMode.Hint : _targetMode);

            if (!_allowSwap && blocked == BlockedTargetBehavior.Swap)
                blocked = normalizedDefaultBlocked;

            var alternativePlacement = requested.HasValue && requested.Value.AlternativePlacement.HasValue
                ? requested.Value.AlternativePlacement.Value
                : _alternativePlacement;

            var allowPartial = requested.HasValue && requested.Value.AllowPartial.HasValue
                ? requested.Value.AllowPartial.Value
                : _allowPartial;

            if (target == TargetMode.Strict && blocked == BlockedTargetBehavior.FindAlternative)
                blocked = BlockedTargetBehavior.Reject;

            return new ResolvedDropPolicy(
                blocked,
                target,
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

        [SerializeField] private bool _overrideTargetMode;
        [SerializeField, ShowIf(nameof(_overrideTargetMode))]
        private TargetMode _targetMode = TargetMode.Strict;

        [SerializeField] private bool _overrideAlternativePlacement;
        [SerializeField, ShowIf(nameof(_overrideAlternativePlacement))]
        private AlternativePlacementMode _alternativePlacement = AlternativePlacementMode.MergeFirst;

        [SerializeField] private bool _overrideAllowPartial;
        [SerializeField, ShowIf(nameof(_overrideAllowPartial))]
        private bool _allowPartial = true;

        public DropRequestPolicy? TryBuild()
        {
            if (!_overrideBlockedTarget && !_overrideTargetMode && !_overrideAlternativePlacement && !_overrideAllowPartial)
                return null;

            return new DropRequestPolicy(
                _overrideBlockedTarget ? _blockedTarget : (BlockedTargetBehavior?)null,
                _overrideTargetMode ? _targetMode : (TargetMode?)null,
                _overrideAlternativePlacement ? _alternativePlacement : (AlternativePlacementMode?)null,
                _overrideAllowPartial ? _allowPartial : (bool?)null);
        }
    }

    [Serializable]
    public sealed class DragRequestPolicySettings
    {
        [SerializeField] private bool _enabled;
        [SerializeField, ShowIf(nameof(_enabled))]
        private DragAmount _amount = DragAmount.All;
        [SerializeField, Range(1, 100), ShowIf(nameof(ShowCustom))]
        private int _customAmount = 1;

        private bool ShowCustom
        {
            get { return _enabled && _amount == DragAmount.Custom; }
        }

        public DragRequestPolicy? TryBuild()
        {
            if (!_enabled)
                return null;

            return new DragRequestPolicy(_amount, _customAmount);
        }
    }
}
