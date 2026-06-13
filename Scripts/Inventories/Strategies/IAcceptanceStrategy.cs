using System.Collections.Generic;
using UDND.Core;
using UDND.Slots;

namespace UDND.Inventories
{
    public enum ShapedMergeKind
    {
        /// <summary>Place the shaped item as a new placement (existing geometry path).</summary>
        CreateNew,
        /// <summary>Merge the shaped item into <see cref="ShapedMergeDecision.MergeTarget"/>.</summary>
        MergeIntoExisting,
        /// <summary>The drop is not allowed (e.g. one-per-ID and the item already exists elsewhere).</summary>
        Reject
    }

    /// <summary>
    /// Strategy-owned decision for how a shaped drop relates to existing placements. All merge semantics
    /// (one-per-ID, auto vs explicit) live inside the strategy.
    /// </summary>
    public readonly struct ShapedMergeDecision
    {
        private ShapedMergeDecision(ShapedMergeKind kind, Placement mergeTarget)
        {
            Kind = kind;
            MergeTarget = mergeTarget;
        }

        public ShapedMergeKind Kind { get; }
        public Placement MergeTarget { get; }

        public static ShapedMergeDecision CreateNew => new ShapedMergeDecision(ShapedMergeKind.CreateNew, null);
        public static ShapedMergeDecision Reject => new ShapedMergeDecision(ShapedMergeKind.Reject, null);
        public static ShapedMergeDecision Merge(Placement target) => new ShapedMergeDecision(ShapedMergeKind.MergeIntoExisting, target);
    }
}
