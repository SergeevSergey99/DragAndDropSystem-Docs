using System;
using UnityEngine;
using UDND.Core;

namespace UDND.Filter.Filters
{
    /// <summary>
    /// Filters slots by <see cref="IFilterable.Category"/>.
    /// </summary>
    [Serializable]
    public class CategoryFilter : ISlotFilter
    {
        [field: SerializeField] public string Category { get; set; }

        public bool Evaluate(in FilterContext ctx)
        {
            if (ctx.Slot.IsEmpty) return false;

            return ctx.Slot.Stack.PrimaryAdapter is IFilterable f
                   && string.Equals(f.Category, Category, StringComparison.OrdinalIgnoreCase);
        }
    }
}
