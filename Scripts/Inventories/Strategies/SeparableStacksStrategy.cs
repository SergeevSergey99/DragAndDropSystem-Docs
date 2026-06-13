using System;
using System.Collections.Generic;
using UDND.Core;
using UDND.Slots;

namespace UDND.Inventories
{
    /// <summary>
    /// Heroes of Might & Magic style strategy.
    /// Items can stack but do NOT merge automatically. Multiple stacks of the same item can exist in different
    /// slots/placements; each stack (including a shaped placement) may hold count &gt; 1, capped by the limit.
    /// Merge happens only on an explicit drop onto the same item (for shaped items: when the dropped footprint
    /// overlaps an existing same-item placement); otherwise a new separate stack/placement is created.
    /// Supports the strategy default limit and,
    /// when allowItemOverride = true, per-item stack limits via IStackSizeLimitable
    /// </summary>
    [Serializable]
    public class SeparableStacksStrategy : StackBasedInventoryStrategyBase
    {
        private int GetMaxStackSize(IItemAdapter itemAdapter) =>
            GetMaxStackSize(itemAdapter, DefaultMaxStackSize, AllowItemStackOverride);

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

            if (IsSourceSlot(targetBaseSlot, request))
                return false;

            int maxSize = GetMaxStackSize(request.ItemAdapter);
            var placement = geometry.GetPlacementAt(targetBaseSlot);
            if (placement != null)
            {
                if (ReferenceEquals(placement, GetSourcePlacement(geometry, request)) ||
                    placement.Stack == null || !placement.Stack.CanStack(request.ItemAdapter))
                    return false;

                int mergeCapacity = Math.Min(
                    request.DesiredCount,
                    Math.Max(0, maxSize - placement.Stack.Count));
                var anchor = geometry.Inventory.GetSlot(placement.AnchorIndex);
                if (mergeCapacity <= 0 || anchor == null ||
                    !PassesRules(anchor, request.ItemAdapter, mergeCapacity, request))
                    return false;

                candidate = PlacementCandidate.Merge(placement, anchor, mergeCapacity);
                return true;
            }

            if (!targetBaseSlot.IsEmpty)
            {
                if (targetBaseSlot.Stack == null ||
                    !targetBaseSlot.Stack.CanStack(request.ItemAdapter))
                    return false;

                int mergeCapacity = Math.Min(
                    request.DesiredCount,
                    Math.Max(0, maxSize - targetBaseSlot.Stack.Count));
                if (mergeCapacity <= 0 ||
                    !PassesRules(targetBaseSlot, request.ItemAdapter, mergeCapacity, request))
                    return false;

                var entry = request.SourceEntry;
                candidate = PlacementCandidate.Merge(
                    targetBaseSlot,
                    entry?.Orientation ?? PlacementOrientation.Rot0,
                    entry?.Shape ?? PlacementShapeUtility.Resolve(request.ItemAdapter),
                    mergeCapacity);
                return true;
            }

            int createCapacity = Math.Min(request.DesiredCount, maxSize);
            if (createCapacity <= 0)
                return false;

            return TryCreatePlacementCandidate(
                geometry,
                request,
                targetBaseSlot,
                createCapacity,
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
            int maxSize = GetMaxStackSize(item);
            int totalCapacity = 0;

            foreach (var slot in slots)
            {
                if (slot.IsEmpty && PassesRules(slot, item, Math.Min(desiredCount, maxSize), request))
                {
                    totalCapacity += maxSize;
                }
                else if (!slot.IsEmpty && slot.Stack.CanStack(item))
                {
                    int canFit = Math.Max(0, maxSize - slot.Stack.Count);
                    if (canFit > 0 && PassesRules(slot, item, Math.Min(desiredCount, canFit), request))
                        totalCapacity += canFit;
                }

                if (totalCapacity >= desiredCount)
                    return desiredCount;
            }

            var slotCreation = geometry.Inventory as IInventorySlotCreationCapacity;
            if (slotCreation?.CanCreateNewSlot == true && totalCapacity < desiredCount)
            {
                if (PrefabPassesRules(slots, slotCreation.BaseSlotPrefab, item, Math.Min(desiredCount, maxSize), request))
                    totalCapacity = AddSlotCapacity(
                        totalCapacity,
                        maxSize,
                        Math.Max(1, slotCreation.PotentialNewSlots),
                        desiredCount);
            }

            return Math.Min(totalCapacity, desiredCount);
        }
    }
}
