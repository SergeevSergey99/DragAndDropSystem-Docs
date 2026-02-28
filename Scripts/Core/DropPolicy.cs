using System;
using Sirenix.OdinInspector;
using UnityEngine;

namespace DragAndDropSystem.Core
{
    /// <summary>
    /// Поведение при попытке дропа в занятый целевой слот.
    /// </summary>
    public enum OccupiedTargetPolicy
    {
        Reject = 0,
        TrySwap = 1,
        TryAlternativeSlots = 2
    }

    /// <summary>
    /// Поведение при нехватке места в целевом инвентаре.
    /// </summary>
    public enum CapacityPolicy
    {
        RejectAll = 0,
        Partial = 1
    }

    /// <summary>
    /// Режим выполнения batch-операции.
    /// </summary>
    public enum BatchExecutionPolicy
    {
        Atomic = 0,
        BestEffort = 1
    }

    /// <summary>
    /// Как интерпретировать целевой слот из UI.
    /// </summary>
    public enum TargetUsagePolicy
    {
        StrictTarget = 0,
        TargetAsHint = 1
    }

    /// <summary>
    /// Политика дропа. Один объект описывает поведение single и batch переноса.
    /// </summary>
    [Serializable]
    public sealed class DropPolicy
    {
        public OccupiedTargetPolicy OccupiedTarget { get; }
        public CapacityPolicy Capacity { get; }
        public BatchExecutionPolicy BatchExecution { get; }
        public TargetUsagePolicy TargetUsage { get; }

        public DropPolicy(
            OccupiedTargetPolicy occupiedTarget,
            CapacityPolicy capacity,
            BatchExecutionPolicy batchExecution,
            TargetUsagePolicy targetUsage)
        {
            OccupiedTarget = occupiedTarget;
            Capacity = capacity;
            BatchExecution = batchExecution;
            TargetUsage = targetUsage;
        }

        public DropPolicy WithOccupiedTarget(OccupiedTargetPolicy value) =>
            new DropPolicy(value, Capacity, BatchExecution, TargetUsage);

        public DropPolicy WithCapacity(CapacityPolicy value) =>
            new DropPolicy(OccupiedTarget, value, BatchExecution, TargetUsage);

        public DropPolicy WithBatchExecution(BatchExecutionPolicy value) =>
            new DropPolicy(OccupiedTarget, Capacity, value, TargetUsage);

        public DropPolicy WithTargetUsage(TargetUsagePolicy value) =>
            new DropPolicy(OccupiedTarget, Capacity, BatchExecution, value);

        /// <summary>
        /// Поведение по умолчанию для одиночного d&d.
        /// </summary>
        public static DropPolicy SingleDefault =>
            new DropPolicy(
                occupiedTarget: OccupiedTargetPolicy.TryAlternativeSlots,
                capacity: CapacityPolicy.Partial,
                batchExecution: BatchExecutionPolicy.BestEffort,
                targetUsage: TargetUsagePolicy.StrictTarget);

        /// <summary>
        /// Атомарный batch: если что-то не влезло/невалидно, отклоняем всю операцию.
        /// </summary>
        public static DropPolicy BatchAtomic =>
            new DropPolicy(
                occupiedTarget: OccupiedTargetPolicy.TryAlternativeSlots,
                capacity: CapacityPolicy.RejectAll,
                batchExecution: BatchExecutionPolicy.Atomic,
                targetUsage: TargetUsagePolicy.TargetAsHint);

        /// <summary>
        /// Batch с частичным успехом.
        /// </summary>
        public static DropPolicy BatchBestEffort =>
            new DropPolicy(
                occupiedTarget: OccupiedTargetPolicy.TryAlternativeSlots,
                capacity: CapacityPolicy.Partial,
                batchExecution: BatchExecutionPolicy.BestEffort,
                targetUsage: TargetUsagePolicy.TargetAsHint);
    }

    /// <summary>
    /// Inspector-friendly настройки policy.
    /// </summary>
    [Serializable]
    public sealed class DropPolicySettings
    {
        [SerializeField, Tooltip("Если выключено, используется policy по умолчанию/из контекста.")]
        private bool _enabled;

        [SerializeField, ShowIf(nameof(_enabled))] private OccupiedTargetPolicy _occupiedTarget = OccupiedTargetPolicy.TryAlternativeSlots;
        [SerializeField, ShowIf(nameof(_enabled))] private CapacityPolicy _capacity = CapacityPolicy.RejectAll;
        [SerializeField, ShowIf(nameof(_enabled))] private BatchExecutionPolicy _batchExecution = BatchExecutionPolicy.Atomic;
        [SerializeField, ShowIf(nameof(_enabled))] private TargetUsagePolicy _targetUsage = TargetUsagePolicy.TargetAsHint;

        public bool Enabled => _enabled;

        public DropPolicy BuildOrNull()
        {
            if (!_enabled)
                return null;

            return new DropPolicy(_occupiedTarget, _capacity, _batchExecution, _targetUsage);
        }
    }
}
