using System;
using System.Collections.Generic;
using UnityEngine;
using UDND.Core;
using UDND.Slots;

namespace UDND.Inventories
{
    /// <summary>
    /// Advisory result for the first currently viable entry/candidate.
    /// It does not reserve state or predict the complete batch execution.
    /// </summary>
    public sealed class TransferProbe
    {
        private TransferProbe(
            bool canAttempt,
            string failureReason,
            int entryIndex,
            DragEntry entry,
            PlacementCandidate? candidate,
            BaseSlot anchorSlot,
            IReadOnlyList<BaseSlot> coveredSlots)
        {
            CanAttempt = canAttempt;
            FailureReason = failureReason;
            EntryIndex = entryIndex;
            Entry = entry;
            Candidate = candidate;
            AnchorSlot = anchorSlot ?? candidate?.Anchor;
            CoveredSlots = coveredSlots ?? Array.Empty<BaseSlot>();
        }

        public bool CanAttempt { get; }
        public string FailureReason { get; }
        public int EntryIndex { get; }
        public DragEntry Entry { get; }
        public PlacementCandidate? Candidate { get; }
        public BaseSlot AnchorSlot { get; }
        public IReadOnlyList<BaseSlot> CoveredSlots { get; }
        public PlacementOrientation Orientation =>
            Candidate?.Orientation ?? Entry.Orientation;

        public static TransferProbe Accepted(
            int entryIndex,
            DragEntry entry,
            PlacementCandidate? candidate = null,
            BaseSlot anchorSlot = null,
            IReadOnlyList<BaseSlot> coveredSlots = null)
            => new TransferProbe(
                true,
                null,
                entryIndex,
                entry,
                candidate,
                anchorSlot,
                coveredSlots);

        public static TransferProbe Rejected(string reason)
            => new TransferProbe(
                false,
                reason ?? "Transfer probe rejected",
                -1,
                default,
                null,
                null,
                null);
    }

    public readonly struct InventoryTransferRequest
    {
        public InventoryTransferRequest(
            IInventory sourceInventory,
            BaseSlot sourceBaseSlot,
            IInventory targetInventory,
            BaseSlot targetBaseSlot,
            ItemStack draggedStack,
            PlacementOrientation orientation = PlacementOrientation.Rot0)
        {
            SourceInventory = sourceInventory;
            SourceBaseSlot = sourceBaseSlot;
            TargetInventory = targetInventory;
            TargetBaseSlot = targetBaseSlot;
            DraggedStack = draggedStack;
            Orientation = orientation;
        }

        public IInventory SourceInventory { get; }
        public BaseSlot SourceBaseSlot { get; }
        public IInventory TargetInventory { get; }
        public BaseSlot TargetBaseSlot { get; }
        public ItemStack DraggedStack { get; }
        public PlacementOrientation Orientation { get; }

        public bool IsValid =>
            SourceInventory != null &&
            SourceBaseSlot != null &&
            TargetInventory != null &&
            DraggedStack != null &&
            DraggedStack.PrimaryAdapter != null &&
            DraggedStack.Count > 0;
    }

    public readonly struct InventoryTransferResult
    {
        public InventoryTransferResult(
            IInventory sourceInventory,
            IInventory targetInventory,
            BaseSlot sourceBaseSlot,
            BaseSlot targetBaseSlot,
            ItemStack sourceRemovedStack,
            ItemStack transferredStack,
            bool targetWasEmptyBefore,
            int remainingInSource = 0,
            PlacementSnapshot targetPlacementSnapshot = null,
            PlacementSnapshot sourcePlacementSnapshot = null)
        {
            SourceInventory = sourceInventory;
            TargetInventory = targetInventory;
            SourceBaseSlot = sourceBaseSlot;
            TargetBaseSlot = targetBaseSlot;
            SourceRemovedStack = sourceRemovedStack ?? ItemStack.Empty();
            TransferredStack = transferredStack ?? ItemStack.Empty();
            TargetWasEmptyBefore = targetWasEmptyBefore;
            RemainingInSource = remainingInSource;
            TargetPlacementSnapshot = targetPlacementSnapshot;
            SourcePlacementSnapshot = sourcePlacementSnapshot;
        }

        public IInventory SourceInventory { get; }
        public IInventory TargetInventory { get; }
        public BaseSlot SourceBaseSlot { get; }
        public BaseSlot TargetBaseSlot { get; }

        /// <summary>
        /// Stack of adapters removed from the source (before conversion)
        /// </summary>
        public ItemStack SourceRemovedStack { get; }

        /// <summary>
        /// Stack of adapters added to the target (after conversion)
        /// </summary>
        public ItemStack TransferredStack { get; }

        public IItemAdapter SourceItemAdapter => SourceRemovedStack.PrimaryAdapter;
        public IItemAdapter TargetItemAdapter => TransferredStack.PrimaryAdapter;
        public IItemAdapter ItemAdapter => TargetItemAdapter;
        public int Amount => TransferredStack.Count;
        public bool TargetWasEmptyBefore { get; }
        public int RemainingInSource { get; }
        public bool IsPartialTransfer => RemainingInSource > 0;
        public PlacementSnapshot SourcePlacementSnapshot { get; }
        public PlacementSnapshot TargetPlacementSnapshot { get; }
        public PlacementSnapshot PlacementSnapshot => TargetPlacementSnapshot;
        public BaseSlot AnchorSlot => TargetPlacementSnapshot?.AnchorBaseSlot ?? TargetBaseSlot;
        public IReadOnlyList<BaseSlot> CoveredSlots => TargetPlacementSnapshot?.CoveredBaseSlots ?? Array.Empty<BaseSlot>();
        public IReadOnlyList<int> CoveredIndices => TargetPlacementSnapshot?.CoveredIndices ?? Array.Empty<int>();
        public IReadOnlyList<Vector2Int> CoveredOffsets => TargetPlacementSnapshot?.CoveredOffsets ?? Array.Empty<Vector2Int>();
        public int AnchorIndex => TargetPlacementSnapshot != null && TargetPlacementSnapshot.AnchorIndex >= 0
            ? TargetPlacementSnapshot.AnchorIndex
            : TargetBaseSlot?.Index ?? -1;
        public PlacementOrientation Orientation => TargetPlacementSnapshot?.Orientation ?? PlacementOrientation.Rot0;
        public Vector2Int BoundingSize => TargetPlacementSnapshot?.BoundingSize ?? Vector2Int.one;
    }
}
