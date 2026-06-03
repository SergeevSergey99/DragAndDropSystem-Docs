using System;
using System.Collections.Generic;
using UDND.Slots;

namespace UDND.Inventories
{
    public readonly struct SlotAcceptanceCandidate
    {
        public readonly ISlot Slot;
        public readonly int RemainingCapacity;

        public SlotAcceptanceCandidate(ISlot slot, int remainingCapacity)
        {
            Slot = slot;
            RemainingCapacity = remainingCapacity;
        }
    }

    public sealed class SlotAcceptanceCandidates
    {
        public static readonly SlotAcceptanceCandidates None = new SlotAcceptanceCandidates(
            Array.Empty<SlotAcceptanceCandidate>(), false, 0);

        public SlotAcceptanceCandidates(
            IReadOnlyList<SlotAcceptanceCandidate> slots,
            bool canCreateNewSlot,
            int potentialNewSlots)
        {
            Slots = slots ?? Array.Empty<SlotAcceptanceCandidate>();
            CanCreateNewSlot = canCreateNewSlot;
            PotentialNewSlots = potentialNewSlots;
        }

        public IReadOnlyList<SlotAcceptanceCandidate> Slots { get; }
        public bool CanCreateNewSlot { get; }
        public int PotentialNewSlots { get; }
        public bool HasAny => (Slots != null && Slots.Count > 0) || CanCreateNewSlot;
    }
}
