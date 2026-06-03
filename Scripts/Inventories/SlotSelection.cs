using UDND.Slots;

namespace UDND.Inventories
{
    public readonly struct SlotSelection
    {
        public readonly bool Accepted;
        public readonly ISlot Slot;
        public readonly bool CreateNew;

        private SlotSelection(bool accepted, ISlot slot, bool createNew)
        {
            Accepted = accepted;
            Slot = slot;
            CreateNew = createNew;
        }

        public static SlotSelection None => new SlotSelection(false, null, false);
        public static SlotSelection Existing(ISlot slot) => new SlotSelection(true, slot, false);
        public static SlotSelection New() => new SlotSelection(true, null, true);
    }
}
