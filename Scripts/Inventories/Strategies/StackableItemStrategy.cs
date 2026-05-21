using System;
using System.Collections.Generic;
using UDND.Core;
using UDND.Slots;

namespace UDND.Inventories
{
    /// <summary>
    /// Strategy: items are stackable (grouped by type)
    /// One item type can occupy multiple slots
    /// Supports the strategy default limit and,
    /// when allowItemOverride = true, per-item limits via IStackSizeLimitable
    /// </summary>
    [Serializable]
    public class StackableItemStrategy : StackBasedInventoryStrategyBase, IStackBasedInventoryStrategy
    {
        private bool AllowMergeOnDrop => _inventory == null || _inventory.AllowMergeOnDrop;

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

                // If the slot is empty, create a new stack
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
                // First try filling existing stacks
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

                // Then create new stacks in empty slots
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

            if (AllowMergeOnDrop)
            {
                foreach (var slot in slots)
                {
                    if (slot == targetBaseSlot || slot.IsEmpty || !slot.Stack.CanStack(stack.PrimaryAdapter))
                        continue;

                    int canFit = Math.Max(0, maxSize - slot.Stack.Count);
                    int toAdd = Math.Min(stack.Count, canFit);
                    if (toAdd <= 0 || !PassesRules(slot, stack.PrimaryAdapter, toAdd))
                        continue;

                    return TryMergeIntoSlot(stack, slot, maxSize, ensureFreeSlots, operationContext);
                }
            }

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

        public override bool CanAcceptItem(List<BaseSlot> slots, InventoryAcceptanceRequest request, bool canCreateNewSlot, int potentialNewSlots, BaseSlot baseSlotPrefab, out BaseSlot suggestedBaseSlot)
        {
            suggestedBaseSlot = null;
            var item = request?.ItemAdapter;
            var desiredCount = request?.DesiredCount ?? 0;
            if (item == null || desiredCount <= 0)
                return false;

            int maxSize = GetMaxStackSize(item, DefaultMaxStackSize, AllowItemStackOverride);

            foreach (var slot in slots)
            {
                if (!slot.IsEmpty && slot.Stack.CanStack(item))
                {
                    int canFit = Math.Max(0, maxSize - slot.Stack.Count);
                    if (canFit > 0 && PassesRules(slot, item, Math.Min(desiredCount, canFit), request))
                    {
                        suggestedBaseSlot = slot;
                        return true;
                    }
                }

                if (slot.IsEmpty && suggestedBaseSlot == null && PassesRules(slot, item, Math.Min(desiredCount, maxSize), request))
                {
                    suggestedBaseSlot = slot;
                }
            }

            if (suggestedBaseSlot != null)
                return true;

            return canCreateNewSlot && PrefabPassesRules(slots, baseSlotPrefab, item, Math.Min(desiredCount, maxSize), request);
        }

        public override int GetAcceptableCount(List<BaseSlot> slots, InventoryAcceptanceRequest request, bool canCreateNewSlot, int potentialNewSlots, BaseSlot baseSlotPrefab)
        {
            var item = request?.ItemAdapter;
            var desiredCount = request?.DesiredCount ?? 0;
            if (item == null || desiredCount <= 0)
                return 0;

            int maxSize = GetMaxStackSize(item, DefaultMaxStackSize, AllowItemStackOverride);
            int totalCapacity = 0;

            foreach (var slot in slots)
            {
                if (!slot.IsEmpty && slot.Stack.CanStack(item))
                {
                    int canFit = Math.Max(0, maxSize - slot.Stack.Count);
                    if (canFit > 0 && PassesRules(slot, item, Math.Min(desiredCount, canFit), request))
                        totalCapacity += canFit;
                }
                else if (slot.IsEmpty && PassesRules(slot, item, Math.Min(desiredCount, maxSize), request))
                {
                    totalCapacity += maxSize;
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
