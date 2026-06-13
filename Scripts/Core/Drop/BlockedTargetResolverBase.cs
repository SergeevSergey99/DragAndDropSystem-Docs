using System;
using System.Collections.Generic;
using UDND.Inventories;
using UDND.Slots;

namespace UDND.Core
{
    public enum BlockedTargetResolutionKind
    {
        Reject = 0,
        AlternativeSlots = 1,
        Swap = 2,
        SwapTargets = Swap
    }

    public sealed class BlockedTargetResolution
    {
        private static readonly BlockedTargetResolution Rejected =
            new BlockedTargetResolution(BlockedTargetResolutionKind.Reject, System.Array.Empty<BaseSlot>());

        private BlockedTargetResolution(
            BlockedTargetResolutionKind kind,
            IEnumerable<BaseSlot> targetSlots)
        {
            Kind = kind;
            TargetSlots = targetSlots ?? System.Array.Empty<BaseSlot>();
        }

        public BlockedTargetResolutionKind Kind { get; }
        public IEnumerable<BaseSlot> TargetSlots { get; }

        public static BlockedTargetResolution Reject()
        {
            return Rejected;
        }

        public static BlockedTargetResolution AlternativeSlots(IEnumerable<BaseSlot> targetSlots)
        {
            return new BlockedTargetResolution(BlockedTargetResolutionKind.AlternativeSlots, targetSlots);
        }

        public static BlockedTargetResolution SwapTargets(IEnumerable<BaseSlot> targetSlots)
        {
            return new BlockedTargetResolution(BlockedTargetResolutionKind.SwapTargets, targetSlots);
        }
    }

    public sealed class BlockedTargetResolutionContext
    {
        public BlockedTargetResolutionContext(
            DragContext dragContext,
            DragEntry dragEntry,
            IInventory targetInventory,
            BaseSlot hintedTargetBaseSlot,
            bool preferHint,
            List<BaseSlot> targetSlots,
            IItemAdapter targetItemAdapter,
            BaseSlot excludedAlternativeBaseSlot,
            Func<BaseSlot, IItemAdapter, bool> canUseAlternativeSlot,
            IReadOnlyList<ISlot> acceptanceCandidates = null)
        {
            DragContext = dragContext;
            DragEntry = dragEntry;
            TargetInventory = targetInventory;
            HintedTargetBaseSlot = hintedTargetBaseSlot;
            PreferHint = preferHint;
            TargetSlots = targetSlots;
            TargetItemAdapter = targetItemAdapter;
            ExcludedAlternativeBaseSlot = excludedAlternativeBaseSlot;
            CanUseAlternativeSlot = canUseAlternativeSlot;
            AcceptanceCandidates = acceptanceCandidates;
        }

        public DragContext DragContext { get; }
        public DragEntry DragEntry { get; }
        public IInventory TargetInventory { get; }
        public BaseSlot HintedTargetBaseSlot { get; }
        public bool PreferHint { get; }
        public List<BaseSlot> TargetSlots { get; }
        public IItemAdapter TargetItemAdapter { get; }
        public BaseSlot ExcludedAlternativeBaseSlot { get; }
        public Func<BaseSlot, IItemAdapter, bool> CanUseAlternativeSlot { get; }

        /// <summary>
        /// Pre-computed eligible candidates from GetSlotCandidates (strategy-aware, respects rules and one-per-ID).
        /// When present, FindAlternativeBlockedTargetResolver uses this list instead of per-slot CanUseAlternativeSlot.
        /// </summary>
        public IReadOnlyList<ISlot> AcceptanceCandidates { get; }

        public bool IsSameInventorySlotDrop =>
            HintedTargetBaseSlot != null &&
            DragEntry.SourceBaseSlot != null &&
            DragEntry.SourceInventory != null &&
            TargetInventory != null &&
            ReferenceEquals(DragEntry.SourceInventory, TargetInventory);
    }

    [Serializable]
    public abstract class BlockedTargetResolverBase
    {
        public virtual BlockedTargetResolution Resolve(BlockedTargetResolutionContext context)
        {
            return BlockedTargetResolution.Reject();
        }
    }
}
