using System;
using System.Collections.Generic;
using UDND.Core;
using UDND.Slots;

namespace UDND.Inventories
{
    /// <summary>
    /// Strategy: items are stackable (grouped by type), one logical stack location per item ID (one-per-ID).
    /// A logical location is a slot for normal inventories and a placement for shaped inventories.
    /// If the item is already present in the inventory, only that location accepts more of it.
    /// If the item is absent, it is placed into a single empty slot/placement (no overflow across locations).
    /// For multi-slot overflow of the same item use <see cref="SeparableStacksStrategy"/>.
    /// Supports the strategy default limit and,
    /// when allowItemOverride = true, per-item limits via IStackSizeLimitable.
    /// </summary>
    [Serializable]
    public class StackableItemStrategy : StackBasedInventoryStrategyBase, IStackBasedInventoryStrategy
    {
        public override bool TryAdd(List<BaseSlot> slots, ItemStack stack, int targetIndex, bool skipRules = false)
        {
            if (stack == null || stack.IsEmpty)
                return false;

            int remaining = stack.Count;
            int maxSize = GetMaxStackSize(stack.PrimaryAdapter, DefaultMaxStackSize, AllowItemStackOverride);

            // If a target slot is specified
            if (targetIndex >= 0 && targetIndex < slots.Count)
            {
                var targetSlot = slots[targetIndex];

                // If the slot is empty, create a new stack.
                // NOTE: targeted placement (targetIndex >= 0) is intentionally NOT one-per-ID-guarded:
                // it means "place into THIS explicit slot" and is the primitive DynamicSlotDecorator
                // uses to fill freshly-created overflow slots. one-per-ID is enforced in the
                // candidate/eligibility path (GetSlotCandidates) and in the bulk/targetless branches.
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
                // If the slot contains the same item, add with limit handling
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
            }
            else
            {
                // one-per-ID: if item already exists in a logical stack location
                // (slot or shaped placement), fill only that location.
                bool itemAlreadyPresent = false;
                HashSet<Placement> seenPlacements = null;
                foreach (var slot in slots)
                {
                    if (ShouldSkipDuplicatePlacementLocation(slot, ref seenPlacements))
                        continue;

                    if (!slot.IsEmpty && slot.Stack.CanStack(stack.PrimaryAdapter))
                    {
                        itemAlreadyPresent = true;
                        int canFit = Math.Max(0, maxSize - slot.Stack.Count);
                        int toAdd = Math.Min(remaining, canFit);
                        if (toAdd > 0 && (skipRules || PassesRules(slot, stack.PrimaryAdapter, toAdd)))
                        {
                            if (!TryMergeIntoSlot(stack, slot, maxSize, null, null, out int added))
                                return false;

                            remaining -= added;
                        }
                        break; // one-per-ID: no other logical location for this item
                    }
                }

                // item absent: place into one empty slot only.
                // If the item already exists, one-per-ID forbids opening a second logical location.
                if (!itemAlreadyPresent && remaining > 0)
                {
                    foreach (var slot in slots)
                    {
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
                            break; // one-per-ID: one empty logical location only
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
            HashSet<Placement> seenPlacements = null;
            foreach (var slot in slots)
            {
                if (remaining <= 0) break;

                if (ShouldSkipDuplicatePlacementLocation(slot, ref seenPlacements))
                    continue;

                if (!slot.IsEmpty && slot.Stack.CanStack(itemAdapter))
                {
                    remaining -= RemoveFromSlot(slot, remaining);
                }
            }

            return remaining < count;
        }

        public override bool TryAddToSlot(List<BaseSlot> slots, ItemStack stack, BaseSlot targetBaseSlot, Action ensureFreeSlots, SlotOperationContext operationContext)
        {
            if (stack == null || stack.IsEmpty || targetBaseSlot == null)
                return false;

            int maxSize = GetMaxStackSize(stack.PrimaryAdapter, DefaultMaxStackSize, AllowItemStackOverride);

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

            // one-per-ID: reject if item already exists in another logical location.
            if (ItemPresentInAnotherSlot(slots, targetBaseSlot, stack.PrimaryAdapter))
                return false;

            if (!PassesRules(targetBaseSlot, stack.PrimaryAdapter, Math.Min(stack.Count, maxSize)))
                return false;

            return TryPlaceIntoEmptySlot(stack, targetBaseSlot, maxSize, ensureFreeSlots, operationContext);
        }

        public override bool CanUseAlternativeSlot(BaseSlot baseSlot, IItemAdapter itemAdapter)
        {
            if (baseSlot == null || itemAdapter == null)
                return false;

            if (baseSlot.IsEmpty)
                return true;

            if (baseSlot.Stack == null || !baseSlot.Stack.CanStack(itemAdapter))
                return false;

            int maxSize = GetMaxStackSize(itemAdapter, DefaultMaxStackSize, AllowItemStackOverride);
            return baseSlot.Stack.Count < maxSize;
        }

        public override SlotAcceptanceCandidates GetSlotCandidates(
            IReadOnlyList<ISlot> slots, InventoryAcceptanceRequest request,
            bool canCreateNewSlot, int potentialNewSlots, BaseSlot baseSlotPrefab)
        {
            var item = request?.ItemAdapter;
            var desiredCount = request?.DesiredCount ?? 0;
            if (item == null || desiredCount <= 0)
                return SlotAcceptanceCandidates.None;

            int maxSize = GetMaxStackSize(item, DefaultMaxStackSize, AllowItemStackOverride);

            // one-per-ID: if item already exists, only that logical location is eligible.
            // Empty slots / new slots are never offered for an item that already exists —
            // even when its stack is full or rules-blocked (no second stack of the same item).
            HashSet<Placement> seenPlacements = null;
            foreach (var slot in slots)
            {
                if (IsSourceSlot(slot, request)) continue;
                if (ShouldSkipDuplicatePlacementLocation(slot, ref seenPlacements)) continue;
                var stack = slot.Stack;
                if (stack == null || stack.IsEmpty || !stack.CanStack(item)) continue;

                int canFit = Math.Max(0, maxSize - stack.Count);
                var logicalSlot = ResolveLogicalStackSlot(slot, slots);
                var baseSlot = ResolveBaseSlot(logicalSlot);
                if (canFit > 0 && baseSlot != null &&
                    PassesRules(baseSlot, item, Math.Min(desiredCount, canFit), request))
                {
                    return new SlotAcceptanceCandidates(
                        new List<SlotAcceptanceCandidate> { new SlotAcceptanceCandidate(logicalSlot, canFit) },
                        false, 0);
                }

                // Present but full or rules-blocked: one-per-ID forbids a second logical location.
                return SlotAcceptanceCandidates.None;
            }

            // item absent: offer empty slots + possibly new
            var emptyCandidates = new List<SlotAcceptanceCandidate>();
            foreach (var slot in slots)
            {
                if (IsSourceSlot(slot, request)) continue;
                var stack = slot.Stack;
                if (stack != null && !stack.IsEmpty) continue;
                var baseSlot = ResolveBaseSlot(slot);
                if (baseSlot == null) continue;
                if (!PassesRules(baseSlot, item, Math.Min(desiredCount, maxSize), request)) continue;
                emptyCandidates.Add(new SlotAcceptanceCandidate(slot, maxSize));
            }

            bool canCreate = canCreateNewSlot && potentialNewSlots > 0 &&
                             PrefabPassesRules(slots, baseSlotPrefab, item, Math.Min(desiredCount, maxSize), request);
            return new SlotAcceptanceCandidates(emptyCandidates, canCreate, canCreate ? potentialNewSlots : 0);
        }

        public override int GetAcceptableCount(List<BaseSlot> slots, InventoryAcceptanceRequest request, bool canCreateNewSlot, int potentialNewSlots, BaseSlot baseSlotPrefab)
        {
            var item = request?.ItemAdapter;
            var desiredCount = request?.DesiredCount ?? 0;
            if (item == null || desiredCount <= 0)
                return 0;

            int maxSize = GetMaxStackSize(item, DefaultMaxStackSize, AllowItemStackOverride);

            // one-per-ID: if item already exists, return only that logical location's remaining capacity.
            // The source logical location is excluded (a same-inventory move frees it) to stay consistent with GetSlotCandidates.
            HashSet<Placement> seenPlacements = null;
            foreach (var slot in slots)
            {
                if (IsSourceSlot(slot, request)) continue;
                if (ShouldSkipDuplicatePlacementLocation(slot, ref seenPlacements)) continue;
                if (!slot.IsEmpty && slot.Stack.CanStack(item))
                {
                    int canFit = Math.Max(0, maxSize - slot.Stack.Count);
                    if (canFit > 0 && PassesRules(slot, item, Math.Min(desiredCount, canFit), request))
                        return Math.Min(canFit, desiredCount);
                    return 0;
                }
            }

            // item absent: one empty slot
            foreach (var slot in slots)
            {
                if (IsSourceSlot(slot, request)) continue;
                if (slot.IsEmpty && PassesRules(slot, item, Math.Min(desiredCount, maxSize), request))
                    return Math.Min(maxSize, desiredCount);
            }

            // no existing or empty slot: new slot
            if (canCreateNewSlot && potentialNewSlots > 0 &&
                PrefabPassesRules(slots, baseSlotPrefab, item, Math.Min(desiredCount, maxSize), request))
                return Math.Min(maxSize, desiredCount);

            return 0;
        }

        /// <summary>
        /// one-per-ID helper: true if a stack of the same item already exists in a logical
        /// location other than <paramref name="exclude"/>. Shaped placements are counted once.
        /// </summary>
        private static bool ItemPresentInAnotherSlot(List<BaseSlot> slots, BaseSlot exclude, IItemAdapter item)
        {
            if (slots == null || item == null)
                return false;

            TryResolvePlacement(exclude, out var excludedPlacement);
            HashSet<Placement> seenPlacements = null;
            foreach (var slot in slots)
            {
                if (slot == null || ReferenceEquals(slot, exclude))
                    continue;

                if (excludedPlacement != null &&
                    TryResolvePlacement(slot, out var slotPlacement) &&
                    ReferenceEquals(slotPlacement, excludedPlacement))
                    continue;

                if (ShouldSkipDuplicatePlacementLocation(slot, ref seenPlacements))
                    continue;

                if (!slot.IsEmpty && slot.Stack.CanStack(item))
                    return true;
            }

            return false;
        }
    }
}
