using System;

namespace DragAndDropSystem.Filter.Sorters
{
    /// <summary>
    /// Sorts slots by stack count (larger stacks first by default).
    /// </summary>
    [Serializable]
    public class StackCountSorter : ISlotSorter
    {
        public int Compare(in FilterContext a, in FilterContext b)
        {
            var countA = a.Slot.IsEmpty ? 0 : a.Slot.Stack.Count;
            var countB = b.Slot.IsEmpty ? 0 : b.Slot.Stack.Count;
            return countA.CompareTo(countB);
        }
    }
}
