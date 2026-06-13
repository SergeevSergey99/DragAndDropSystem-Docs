using System;
using System.Collections.Generic;
using UDND.Core;
using UDND.Slots;

namespace UDND.Inventories
{
    /// <summary>
    /// Strategy: each item occupies its own slot (not stackable)
    /// Used for inventories with unique items
    /// </summary>
    [Serializable]
    public class UniqueItemStrategy : InventoryStrategyBase, IUniqueInventoryStrategy
    {
        protected override bool ShowDragAmountSettings => false;
        public override int ResolveDragAmount(int stackCount, DragAmount dragAmount, int customDragAmount) => 1;
        // Unique items never stack: one per slot/placement (count 1), including shaped placements.
        // The candidate capacity must therefore stay at one.
        public override int GetMaxStackSizeForItem(IItemAdapter itemAdapter) => itemAdapter == null ? 0 : 1;

        public override bool TryGetCandidate(
            IPlacementGeometry geometry,
            InventoryAcceptanceRequest request,
            BaseSlot targetBaseSlot,
            out PlacementCandidate candidate)
        {
            candidate = default;
            if (geometry == null || request == null || targetBaseSlot == null ||
                request.ItemAdapter == null || request.DesiredCount <= 0)
                return false;

            if (IsSourceSlot(targetBaseSlot, request) ||
                geometry.GetPlacementAt(targetBaseSlot) != null ||
                !targetBaseSlot.IsEmpty)
                return false;

            return TryCreatePlacementCandidate(
                geometry,
                request,
                targetBaseSlot,
                1,
                out candidate);
        }

        public override int GetAcceptableCount(List<BaseSlot> slots, InventoryAcceptanceRequest request, bool canCreateNewSlot, int potentialNewSlots, BaseSlot baseSlotPrefab)
        {
            var item = request?.ItemAdapter;
            var desiredCount = request?.DesiredCount ?? 0;
            if (item == null || desiredCount <= 0)
                return 0;

            int acceptableCount = 0;
            foreach (var slot in slots)
            {
                if (slot.IsEmpty && PassesRules(slot, item, 1, request))
                    acceptableCount++;
            }

            if (canCreateNewSlot && potentialNewSlots > 0 && PrefabPassesRules(slots, baseSlotPrefab, item, 1, request))
                acceptableCount += potentialNewSlots;

            return Math.Min(acceptableCount, desiredCount);
        }
    }
}
