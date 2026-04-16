using System;
using UnityEngine;

namespace UniversalDragAndDrop.Filter.Filters
{
    /// <summary>
    /// Filters slots by substring match in <see cref="Core.IItemAdapter.DisplayName"/>.
    /// </summary>
    [Serializable]
    public class NameSearchFilter : ISlotFilter
    {
        [field: SerializeField] public string SearchText { get; set; }

        public bool Evaluate(in FilterContext ctx)
        {
            if (ctx.Slot.IsEmpty) return false;
            if (string.IsNullOrEmpty(SearchText)) return true;

            var displayName = ctx.Slot.Stack.DisplayName;
            return displayName != null
                   && displayName.IndexOf(SearchText, StringComparison.OrdinalIgnoreCase) >= 0;
        }
    }
}
