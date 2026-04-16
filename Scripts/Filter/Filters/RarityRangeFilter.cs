using System;
using UnityEngine;
using UniversalDragAndDrop.Core;

namespace UniversalDragAndDrop.Filter.Filters
{
    /// <summary>
    /// Filters slots by <see cref="IFilterable.Rarity"/> within a range.
    /// </summary>
    [Serializable]
    public class RarityRangeFilter : ISlotFilter
    {
        [field: SerializeField] public int MinRarity { get; set; }
        [field: SerializeField] public int MaxRarity { get; set; } = int.MaxValue;

        public bool Evaluate(in FilterContext ctx)
        {
            if (ctx.Slot.IsEmpty) return false;

            if (ctx.Slot.Stack.PrimaryAdapter is not IFilterable f)
                return false;

            return f.Rarity >= MinRarity && f.Rarity <= MaxRarity;
        }
    }
}
