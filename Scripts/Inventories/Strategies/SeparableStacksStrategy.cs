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

        // Placement resolution (validation, same-source footprint, explicit merge onto the target,
        // create fallback) lives in StackBasedInventoryStrategyBase. Separable stacks never merge into
        // a stack elsewhere, so the base default (TryResolveAdditionalMergeCandidate => NotApplicable)
        // already gives the "each drop makes a new separate stack" behavior — nothing to override here.

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
