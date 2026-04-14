using System;

namespace DragAndDropSystem.Filter.Sorters
{
    /// <summary>
    /// Sorts slots alphabetically by <see cref="Core.ItemStack.DisplayName"/>.
    /// </summary>
    [Serializable]
    public class NameSorter : ISlotSorter
    {
        public int Compare(in FilterContext a, in FilterContext b)
        {
            var nameA = a.Slot.IsEmpty ? "" : a.Slot.Stack.DisplayName ?? "";
            var nameB = b.Slot.IsEmpty ? "" : b.Slot.Stack.DisplayName ?? "";
            return string.Compare(nameA, nameB, StringComparison.OrdinalIgnoreCase);
        }
    }
}
