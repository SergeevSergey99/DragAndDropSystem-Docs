using System;
using DragAndDropSystem.Core;

namespace DragAndDropSystem.Filter.Sorters
{
    /// <summary>
    /// Sorts slots by <see cref="IFilterable.Rarity"/> (numeric).
    /// </summary>
    [Serializable]
    public class RaritySorter : ISlotSorter
    {
        public int Compare(in FilterContext a, in FilterContext b)
        {
            return GetRarity(a).CompareTo(GetRarity(b));
        }

        private static int GetRarity(in FilterContext ctx)
        {
            if (ctx.Slot.IsEmpty) return -1;
            return ctx.Slot.Stack.PrimaryAdapter is IFilterable f ? f.Rarity : 0;
        }
    }
}
