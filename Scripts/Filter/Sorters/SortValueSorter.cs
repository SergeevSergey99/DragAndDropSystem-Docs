using System;
using UDND.Core;

namespace UDND.Filter.Sorters
{
    /// <summary>
    /// Sorts slots by <see cref="ISortable.SortValue"/> (numeric).
    /// </summary>
    [Serializable]
    public class SortValueSorter : ISlotSorter
    {
        public int Compare(in FilterContext a, in FilterContext b)
        {
            return GetSortValue(a).CompareTo(GetSortValue(b));
        }

        private static int GetSortValue(in FilterContext ctx)
        {
            if (ctx.Slot.IsEmpty) return int.MinValue;
            return ctx.Slot.Stack.PrimaryAdapter is ISortable s ? s.SortValue : 0;
        }
    }
}
