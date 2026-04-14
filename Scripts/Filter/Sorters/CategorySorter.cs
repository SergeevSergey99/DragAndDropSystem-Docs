using System;
using DragAndDropSystem.Core;

namespace DragAndDropSystem.Filter.Sorters
{
    /// <summary>
    /// Sorts slots alphabetically by <see cref="IFilterable.Category"/>.
    /// </summary>
    [Serializable]
    public class CategorySorter : ISlotSorter
    {
        public int Compare(in FilterContext a, in FilterContext b)
        {
            var catA = GetCategory(a);
            var catB = GetCategory(b);
            return string.Compare(catA, catB, StringComparison.OrdinalIgnoreCase);
        }

        private static string GetCategory(in FilterContext ctx)
        {
            if (ctx.Slot.IsEmpty) return "";
            return ctx.Slot.Stack.PrimaryAdapter is IFilterable f ? f.Category ?? "" : "";
        }
    }
}
