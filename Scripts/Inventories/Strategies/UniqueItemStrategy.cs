using System;
using System.Collections.Generic;
using UniversalDragAndDrop.Core;
using UniversalDragAndDrop.Slots;

namespace UniversalDragAndDrop.Inventories
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
        public override bool RequiresStrategyPlacement(ItemStack stack) => stack != null && !stack.IsEmpty && stack.Count > 1;
        public override bool UsesPerItemSlotPlanning => true;

        public override bool CanUseAlternativeSlot(BaseSlot baseSlot, IItemAdapter itemAdapter)
        {
            if (baseSlot == null || itemAdapter == null)
                return false;

            return baseSlot.IsEmpty;
        }

        public override IEnumerable<BaseSlot> EnumerateAlternativeSlots(List<BaseSlot> slots, IItemAdapter itemAdapter, AlternativePlacementMode mode, BaseSlot excludeBaseSlot)
        {
            if (mode == AlternativePlacementMode.MergeOnly)
                yield break;

            if (slots == null || itemAdapter == null)
                yield break;

            for (int i = 0; i < slots.Count; i++)
            {
                var slot = slots[i];
                if (slot == null || ReferenceEquals(slot, excludeBaseSlot) || !slot.IsEmpty)
                    continue;

                yield return slot;
            }
        }

        public override bool TryAdd(List<BaseSlot> slots, ItemStack stack, int targetIndex, bool skipRules = false)
        {
            if (stack == null || stack.IsEmpty)
                return false;

            // Check the target slot
            if (targetIndex >= 0 && targetIndex < slots.Count)
            {
                var targetSlot = slots[targetIndex];

                if (targetSlot.IsEmpty && (skipRules || PassesRules(targetSlot, stack.PrimaryAdapter, 1)))
                {
                    var singleItemStack = stack.Split(1);
                    if (singleItemStack.IsEmpty)
                        return false;

                    targetSlot.SetStack(singleItemStack);
                }
                return stack.IsEmpty;
            }

            // Distribute items across empty slots (1 per slot)
            for (int i = 0; i < slots.Count && !stack.IsEmpty; i++)
            {
                var slot = slots[i];
                if (!slot.IsEmpty)
                    continue;

                if (!skipRules && !PassesRules(slot, stack.PrimaryAdapter, 1))
                    continue;

                var singleItemStack = stack.Split(1);
                if (singleItemStack.IsEmpty)
                    return false;

                slot.SetStack(singleItemStack);
                // Continue the loop to distribute remaining items
            }

            return stack.IsEmpty;
        }

        public override bool TryRemove(List<BaseSlot> slots, IItemAdapter itemAdapter, int count, int sourceIndex)
        {
            if (sourceIndex >= 0 && sourceIndex < slots.Count)
            {
                var slot = slots[sourceIndex];
                if (!slot.IsEmpty && slot.Stack.CanStack(itemAdapter))
                {
                    slot.Clear();
                    return true;
                }
                return false;
            }

            // Find and remove the first matching item
            int slotIndex = FindSlotWithItem(slots, itemAdapter);
            if (slotIndex >= 0)
            {
                slots[slotIndex].Clear();
                return true;
            }

            return false;
        }

        public override bool TryAddToSlot(List<BaseSlot> slots, ItemStack stack, BaseSlot targetBaseSlot, System.Action ensureFreeSlots, SlotOperationContext operationContext)
        {
            if (stack == null || stack.IsEmpty || targetBaseSlot == null || !targetBaseSlot.IsEmpty)
                return false;

            if (!PassesRules(targetBaseSlot, stack.PrimaryAdapter, 1))
                return false;

            return TryPlaceIntoEmptySlot(stack, targetBaseSlot, 1, ensureFreeSlots, operationContext);
        }

        public override bool CanAcceptItem(List<BaseSlot> slots, InventoryAcceptanceRequest request, bool canCreateNewSlot, int potentialNewSlots, BaseSlot baseSlotPrefab, out BaseSlot suggestedBaseSlot)
        {
            suggestedBaseSlot = null;
            var item = request?.ItemAdapter;
            var desiredCount = request?.DesiredCount ?? 0;
            if (item == null || desiredCount <= 0)
                return false;

            foreach (var slot in slots)
            {
                if (slot.IsEmpty && PassesRules(slot, item, 1, request))
                {
                    suggestedBaseSlot = slot;
                    return true;
                }
            }

            return canCreateNewSlot && potentialNewSlots > 0 && PrefabPassesRules(slots, baseSlotPrefab, item, 1, request);
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

            return System.Math.Min(acceptableCount, desiredCount);
        }
    }
}
