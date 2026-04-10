using DragAndDropSystem.Slots;

namespace DragAndDropSystem.Inventories
{
    /// <summary>
    /// Context for an add-to-slot operation.
    /// Allows retrieving the actual slot where the item was placed.
    /// </summary>
    public class SlotOperationContext
    {
        /// <summary>
        /// Actual slot where the item ended up (including auto-merge and repacking).
        /// </summary>
        public BaseSlot ResolvedBaseSlot { get; private set; }

        /// <summary>
        /// Whether the slot was empty before the operation (useful for visuals and animations).
        /// </summary>
        public bool TargetWasEmptyBefore { get; private set; }

        /// <summary>
        /// Number of items that actually ended up in the slot.
        /// </summary>
        public int AddedCount { get; private set; }

        public void RecordResult(BaseSlot baseSlot, bool wasEmptyBefore, int addedCount)
        {
            ResolvedBaseSlot = baseSlot;
            TargetWasEmptyBefore = wasEmptyBefore;
            AddedCount = addedCount;
        }

        public void ResetResult()
        {
            ResolvedBaseSlot = null;
            TargetWasEmptyBefore = false;
            AddedCount = 0;
        }
    }
}
