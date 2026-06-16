using System;
using System.Collections.Generic;
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
            IReadOnlyList<BaseSlot> coveredSlots,
            bool isExplicitTargetCandidate)
        {
            CanAttempt = canAttempt;
            FailureReason = failureReason;
            EntryIndex = entryIndex;
            Entry = entry;
            Candidate = candidate;
            AnchorSlot = anchorSlot ?? candidate?.Anchor;
            CoveredSlots = coveredSlots ?? Array.Empty<BaseSlot>();
            IsExplicitTargetCandidate = isExplicitTargetCandidate;
        }

        public bool CanAttempt { get; }
        public string FailureReason { get; }
        public int EntryIndex { get; }
        public DragEntry Entry { get; }
        public PlacementCandidate? Candidate { get; }
        public BaseSlot AnchorSlot { get; }
        public IReadOnlyList<BaseSlot> CoveredSlots { get; }
        public bool IsExplicitTargetCandidate { get; }
        public int Orientation =>
            Candidate?.Orientation ?? Entry.Orientation;

        public static TransferProbe Accepted(
            int entryIndex,
            DragEntry entry,
            PlacementCandidate? candidate = null,
            BaseSlot anchorSlot = null,
            IReadOnlyList<BaseSlot> coveredSlots = null,
            bool isExplicitTargetCandidate = false)
            => new TransferProbe(
                true,
                null,
                entryIndex,
                entry,
                candidate,
                anchorSlot,
                coveredSlots,
                isExplicitTargetCandidate);

        public static TransferProbe Rejected(string reason)
            => new TransferProbe(
                false,
                reason ?? "Transfer probe rejected",
                -1,
                default,
                null,
                null,
                null,
                false);
    }
}