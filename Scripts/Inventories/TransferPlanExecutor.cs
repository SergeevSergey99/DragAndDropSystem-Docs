using System;
using System.Collections.Generic;
using UnityEngine;
using UDND.Core;
using UDND.Slots;

namespace UDND.Inventories
{
    public readonly struct ExecutedTransferEntry
    {
        public ExecutedTransferEntry(
            BaseSlot sourceBaseSlot,
            BaseSlot targetBaseSlot,
            IItemAdapter itemAdapter,
            int amount,
            PlacementSnapshot targetPlacementSnapshot = null,
            PlacementSnapshot sourcePlacementSnapshot = null)
        {
            SourceBaseSlot = sourceBaseSlot;
            TargetBaseSlot = targetBaseSlot;
            ItemAdapter = itemAdapter;
            Amount = amount;
            TargetPlacementSnapshot = targetPlacementSnapshot;
            SourcePlacementSnapshot = sourcePlacementSnapshot;
        }

        public BaseSlot SourceBaseSlot { get; }
        public BaseSlot TargetBaseSlot { get; }
        public IItemAdapter ItemAdapter { get; }
        public int Amount { get; }
        public PlacementSnapshot SourcePlacementSnapshot { get; }
        public PlacementSnapshot TargetPlacementSnapshot { get; }
        public PlacementSnapshot PlacementSnapshot => TargetPlacementSnapshot;
        public BaseSlot AnchorSlot => TargetPlacementSnapshot?.AnchorBaseSlot ?? TargetBaseSlot;
        public IReadOnlyList<BaseSlot> CoveredSlots =>
            TargetPlacementSnapshot?.CoveredBaseSlots ?? Array.Empty<BaseSlot>();
        public IReadOnlyList<int> CoveredIndices =>
            TargetPlacementSnapshot?.CoveredIndices ?? Array.Empty<int>();
        public IReadOnlyList<Vector2Int> CoveredOffsets =>
            TargetPlacementSnapshot?.CoveredOffsets ?? Array.Empty<Vector2Int>();
        public int AnchorIndex => TargetPlacementSnapshot != null &&
                                  TargetPlacementSnapshot.AnchorIndex >= 0
            ? TargetPlacementSnapshot.AnchorIndex
            : TargetBaseSlot?.Index ?? -1;
        public PlacementOrientation Orientation =>
            TargetPlacementSnapshot?.Orientation ?? PlacementOrientation.Rot0;
        public Vector2Int BoundingSize =>
            TargetPlacementSnapshot?.BoundingSize ?? Vector2Int.one;
    }

    public sealed class TransferExecutionSummary
    {
        public TransferExecutionSummary(
            bool success,
            int succeededEntries,
            int failedEntries,
            int transferredAmount,
            bool isPartial,
            DropResult dropResult,
            IReadOnlyList<ExecutedTransferEntry> executedEntries = null)
        {
            Success = success;
            SucceededEntries = succeededEntries;
            FailedEntries = failedEntries;
            TransferredAmount = transferredAmount;
            IsPartial = isPartial;
            DropResult = dropResult;
            ExecutedEntries = executedEntries ?? Array.Empty<ExecutedTransferEntry>();
        }

        public bool Success { get; }
        public int SucceededEntries { get; }
        public int FailedEntries { get; }
        public int TransferredAmount { get; }
        public bool IsPartial { get; }
        public DropResult DropResult { get; }
        public IReadOnlyList<ExecutedTransferEntry> ExecutedEntries { get; }
    }
}
