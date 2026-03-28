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
        [SerializeField, Tooltip("Что делать, если в целевой слот не удалось положить ничего: отклонить, попытаться обменять или искать другой слот.")]
        private BlockedTargetBehavior _blockedTarget = BlockedTargetBehavior.FindAlternative;
        [SerializeField, Tooltip("Только для SeparableStacks: разрешить merge при явном дропе на занятый слот с таким же предметом.")]
        private bool _allowMergeOnDrop = true;
        [SerializeField, Tooltip("Разрешить выполнить перенос частично, если вошла только часть запрошенного количества.")]
        private bool _allowPartial = true;
        [SerializeField, Tooltip("Как обрабатывать batch-перенос: Atomic отменяет всю операцию при первой ошибке, BestEffort переносит то, что получилось.")]
        private BatchMode _batchMode = BatchMode.BestEffort;
        [SerializeField, Tooltip("Порядок поиска альтернативных слотов для FindAlternative. Используется стратегией размещения.")]
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
        [SerializeField, LabelText("Override Drag Amount"), Tooltip("Временно переопределяет количество предметов только для текущего StartDrag.")]
        private bool _overrideAmount;
        [SerializeField, ShowIf(nameof(_overrideAmount)), LabelText("Amount"), Tooltip("Сколько предметов взять из source stack при старте драга.")]
        private DragAmount _amount = DragAmount.All;
        [SerializeField, Range(1, 100), ShowIf(nameof(ShowCustomAmount)), LabelText("Custom Amount"), Tooltip("Используется только когда Amount = Custom.")]
        private int _customAmount = 1;

        private bool ShowCustomAmount
        {
            get { return _overrideAmount && _amount == DragAmount.Custom; }
        }

        public DragRequestPolicy? TryBuild()
        {
            if (!_overrideAmount)
                return null;

            return new DragRequestPolicy(_amount, _customAmount);
        }
    }
}
