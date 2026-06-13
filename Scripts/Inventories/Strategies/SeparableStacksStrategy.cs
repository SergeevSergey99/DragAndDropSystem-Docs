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
    public class SeparableStacksStrategy : StackBasedInventoryStrategyBase, IStackBasedInventoryStrategy, ISeparableStacksInventoryStrategy
    {
        private int GetMaxStackSize(IItemAdapter itemAdapter) =>
            GetMaxStackSize(itemAdapter, DefaultMaxStackSize, AllowItemStackOverride);

        // Separable: explicit merge only — a shaped drop merges only when its footprint overlaps an existing
        // same-item placement; otherwise it creates a new, separate placement (multiple stacks allowed).
        public override ShapedMergeDecision ResolveShapedMerge(
            IPlacementInventory inventory, IItemAdapter item, int anchorIndex,
            IPlacementShape shape, PlacementOrientation orientation, Placement sourcePlacement)
        {
            var overlapped = FindOverlappedShapedPlacement(inventory, item, anchorIndex, shape, orientation, sourcePlacement);
            return overlapped != null
                ? ShapedMergeDecision.Merge(overlapped)
                : ShapedMergeDecision.CreateNew;
        }

        public override bool TryAdd(List<BaseSlot> slots, ItemStack stack, int targetIndex, bool skipRules = false)
        {
            if (stack == null || stack.IsEmpty)
                return false;

            int remaining = stack.Count;
            int maxSize = GetMaxStackSize(stack.PrimaryAdapter);

            // If a specific slot is specified (drag to slot)
            if (targetIndex >= 0 && targetIndex < slots.Count)
            {
                var targetSlot = slots[targetIndex];

                // If the slot is empty, place with limit handling
                if (targetSlot.IsEmpty)
                {
                    int toPlace = Math.Min(remaining, maxSize);
                    if (toPlace > 0 && (skipRules || PassesRules(targetSlot, stack.PrimaryAdapter, toPlace)))
                    {
                        var movedStack = stack.Split(toPlace);
                        if (movedStack.IsEmpty)
                            return false;

                        targetSlot.SetStack(movedStack);
                        remaining -= toPlace;
                    }
                }
                // If the slot contains the same item, merge with limit handling
                else if (targetSlot.Stack.CanStack(stack.PrimaryAdapter))
                {
                    int canFit = Math.Max(0, maxSize - targetSlot.Stack.Count);
                    int toAdd = Math.Min(remaining, canFit);
                    if (toAdd > 0 && (skipRules || PassesRules(targetSlot, stack.PrimaryAdapter, toAdd)))
                    {
                        if (!TryMergeIntoSlot(stack, targetSlot, maxSize, null, null, out int added))
                            return false;

                        remaining -= added;
                    }
                }
                // Otherwise we cannot add (slot is occupied by another item)
            }
            else
            {
                // If no slot is specified (TryAddItem), this is a programmatic add
                // First try adding to existing stacks
                foreach (var slot in slots)
                {
                    if (remaining <= 0) break;

                    if (!slot.IsEmpty && slot.Stack.CanStack(stack.PrimaryAdapter))
                    {
                        int canFit = Math.Max(0, maxSize - slot.Stack.Count);
                        int toAdd = Math.Min(remaining, canFit);
                        if (toAdd > 0 && (skipRules || PassesRules(slot, stack.PrimaryAdapter, toAdd)))
                        {
                            if (!TryMergeIntoSlot(stack, slot, maxSize, null, null, out int added))
                                return false;

                            remaining -= added;
                        }
                    }
                }

                // If no existing stack was found or not everything fit, create new stacks in empty slots
                if (remaining > 0)
                {
                    foreach (var slot in slots)
                    {
                        if (remaining <= 0) break;

                        if (slot.IsEmpty)
                        {
                            int toPlace = Math.Min(remaining, maxSize);
                            if (toPlace > 0 && (skipRules || PassesRules(slot, stack.PrimaryAdapter, toPlace)))
                            {
                                var movedStack = stack.Split(toPlace);
                                if (movedStack.IsEmpty)
                                    return false;

                                slot.SetStack(movedStack);
                                remaining -= toPlace;
                            }
                        }
                    }
                }
            }
            return stack.IsEmpty;
        }

        public override bool TryRemove(List<BaseSlot> slots, IItemAdapter itemAdapter, int count, int sourceIndex)
        {
            int remaining = count;

            if (sourceIndex >= 0 && sourceIndex < slots.Count)
            {
                var slot = slots[sourceIndex];
                if (!slot.IsEmpty && slot.Stack.CanStack(itemAdapter))
                {
                    int removed = RemoveFromSlot(slot, remaining);
                    return removed > 0;
                }
                return false;
            }

            // Remove from all slots containing this item
            foreach (var slot in slots)
            {
                if (remaining <= 0) break;

                if (!slot.IsEmpty && slot.Stack.CanStack(itemAdapter))
                {
                    remaining -= RemoveFromSlot(slot, remaining);
                }
            }

            return remaining < count;
        }

        public override bool CanUseAlternativeSlot(BaseSlot baseSlot, IItemAdapter itemAdapter)
        {
            if (baseSlot == null || itemAdapter == null)
                return false;

            if (baseSlot.IsEmpty)
                return true;

            if (baseSlot.Stack == null || !baseSlot.Stack.CanStack(itemAdapter))
                return false;

            // A slot is suitable for merge only if it has free space
            int maxSize = GetMaxStackSize(itemAdapter);
            return baseSlot.Stack.Count < maxSize;
        }

        public override bool TryAddToSlot(List<BaseSlot> slots, ItemStack stack, BaseSlot targetBaseSlot, Action ensureFreeSlots, SlotOperationContext operationContext)
        {
            if (stack == null || stack.IsEmpty || targetBaseSlot == null)
                return false;

            int maxSize = GetMaxStackSize(stack.PrimaryAdapter);

            if (!targetBaseSlot.IsEmpty)
            {
                if (!targetBaseSlot.Stack.CanStack(stack.PrimaryAdapter))
                    return false;

                int canFit = Math.Max(0, maxSize - targetBaseSlot.Stack.Count);
                int toAdd = Math.Min(stack.Count, canFit);
                if (toAdd <= 0 || !PassesRules(targetBaseSlot, stack.PrimaryAdapter, toAdd))
                    return false;

                return TryMergeIntoSlot(stack, targetBaseSlot, maxSize, ensureFreeSlots, operationContext);
            }

            int toPlace = Math.Min(stack.Count, maxSize);
            if (toPlace <= 0 || !PassesRules(targetBaseSlot, stack.PrimaryAdapter, toPlace))
                return false;

            var movedStack = stack.Split(toPlace);
            if (movedStack.IsEmpty)
                return false;

            targetBaseSlot.SetStack(movedStack);
            targetBaseSlot.UpdateVisuals();
            operationContext?.RecordResult(targetBaseSlot, true, toPlace);
            ensureFreeSlots?.Invoke();
            return true;
        }

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

        public override int GetAcceptableCount(List<BaseSlot> slots, InventoryAcceptanceRequest request, bool canCreateNewSlot, int potentialNewSlots, BaseSlot baseSlotPrefab)
        {
            var item = request?.ItemAdapter;
            var desiredCount = request?.DesiredCount ?? 0;
            if (item == null || desiredCount <= 0)
                return 0;

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

            if (canCreateNewSlot && totalCapacity < desiredCount)
            {
                if (PrefabPassesRules(slots, baseSlotPrefab, item, Math.Min(desiredCount, maxSize), request))
                    totalCapacity = AddSlotCapacity(totalCapacity, maxSize, Math.Max(1, potentialNewSlots), desiredCount);
            }

            return Math.Min(totalCapacity, desiredCount);
        }
    }
}
