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
    public class UniqueItemStrategy : InventoryStrategyBase
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

            var sourcePlacement = GetSourcePlacement(geometry, request);
            var existingPlacement = geometry.GetPlacementAt(targetBaseSlot);

            // A slot covered by a different placement is occupied — can't place here.
            if (existingPlacement != null && !ReferenceEquals(existingPlacement, sourcePlacement))
                return false;

            // A slot with a direct non-placement stack is occupied.
            if (existingPlacement == null && !targetBaseSlot.IsEmpty)
                return false;

            // IsSourceSlot check removed: for explicit drops (same-inventory moves with
            // rotation) we must allow the source-covered slot as a candidate. For auto-
            // enumeration, EnumerateCandidates post-filters Create candidates at source cells.

            return TryCreatePlacementCandidate(
                geometry,
                request,
                targetBaseSlot,
                1,
                out candidate);
        }

        public override int GetAcceptableCount(
            IPlacementGeometry geometry,
            InventoryAcceptanceRequest request)
        {
            var item = request?.ItemAdapter;
            var desiredCount = request?.DesiredCount ?? 0;
            if (geometry == null || item == null || desiredCount <= 0)
                return 0;

            var slots = geometry.Slots;
            int acceptableCount = 0;
            foreach (var slot in slots)
            {
                if (slot.IsEmpty && PassesRules(slot, item, 1, request))
                    acceptableCount++;
            }

            var slotCreation = geometry.Inventory as IInventorySlotCreationCapacity;
            if (slotCreation?.CanCreateNewSlot == true &&
                slotCreation.PotentialNewSlots > 0 &&
                PrefabPassesRules(slots, slotCreation.BaseSlotPrefab, item, 1, request))
                acceptableCount += slotCreation.PotentialNewSlots;

            return Math.Min(acceptableCount, desiredCount);
        }
    }
}
