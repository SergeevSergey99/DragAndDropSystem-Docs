using System;
using System.Collections.Generic;
using UniversalDragAndDrop.Core;
using UniversalDragAndDrop.Slots;

namespace UniversalDragAndDrop.Inventories
{
    /// <summary>
    /// Attempts to repack items in an inventory to free a suitable slot
    /// for a new item. Stateless: all dependencies are passed through parameters.
    /// </summary>
    public static class SlotRelocationService
    {
        /// <summary>
        /// Try moving the occupant out of a suitable slot, then attempt placement again.
        /// </summary>
        public static bool TryRelocateAndRetry(
            List<BaseSlot> slots,
            ItemStack stack,
            int targetSlotIndex,
            IPlacementStrategy placementStrategy,
            Func<BaseSlot, IItemAdapter, int, bool> canAcceptByRules,
            Func<InventorySnapshot> captureSnapshot,
            Action<InventorySnapshot> restoreSnapshot)
        {
            if (stack == null || stack.IsEmpty)
                return false;

            var snapshot = captureSnapshot();
            var candidates = BuildRelocationCandidates(slots, stack, targetSlotIndex, canAcceptByRules);

            bool isFirstAttempt = true;
            foreach (var slot in candidates)
            {
                if (!isFirstAttempt)
                {
                    restoreSnapshot(snapshot);
                }
                else
                {
                    isFirstAttempt = false;
                }

                if (!TryRelocateOccupant(slots, slot, stack, canAcceptByRules))
                    continue;

                if (placementStrategy.TryAdd(slots, stack, targetSlotIndex))
                    return true;
            }

            restoreSnapshot(snapshot);
            return false;
        }

        private static List<BaseSlot> BuildRelocationCandidates(
            List<BaseSlot> slots,
            ItemStack stack,
            int targetSlotIndex,
            Func<BaseSlot, IItemAdapter, int, bool> canAcceptByRules)
        {
            var result = new List<BaseSlot>();

            if (targetSlotIndex >= 0 && targetSlotIndex < slots.Count)
            {
                var targetedSlot = slots[targetSlotIndex];
                if (!result.Contains(targetedSlot))
                    result.Add(targetedSlot);
            }

            for (int i = 0; i < slots.Count; i++)
            {
                var slot = slots[i];
                if (result.Contains(slot))
                    continue;

                if (slot.IsEmpty)
                    continue;

                if (canAcceptByRules(slot, stack.PrimaryAdapter, stack.Count))
                    result.Add(slot);
            }

            for (int i = 0; i < slots.Count; i++)
            {
                var slot = slots[i];
                if (result.Contains(slot))
                    continue;

                if (!slot.IsEmpty)
                    result.Add(slot);
            }

            return result;
        }

        private static bool TryRelocateOccupant(
            List<BaseSlot> slots,
            BaseSlot baseSlotToFree,
            ItemStack incomingStack,
            Func<BaseSlot, IItemAdapter, int, bool> canAcceptByRules)
        {
            if (baseSlotToFree == null || baseSlotToFree.IsEmpty)
                return false;

            var occupantStack = baseSlotToFree.Stack;
            if (occupantStack == null || occupantStack.IsEmpty)
                return false;

            var destinations = new List<BaseSlot>();
            for (int i = 0; i < slots.Count; i++)
            {
                var slot = slots[i];
                if (slot == baseSlotToFree)
                    continue;

                if (!slot.IsEmpty)
                {
                    if (!slot.Stack.CanStack(occupantStack.PrimaryAdapter))
                        continue;

                    if (!canAcceptByRules(slot, occupantStack.PrimaryAdapter, occupantStack.Count))
                        continue;

                    destinations.Add(slot);
                }
                else
                {
                    if (!canAcceptByRules(slot, occupantStack.PrimaryAdapter, occupantStack.Count))
                        continue;

                    destinations.Add(slot);
                }
            }

            destinations.Sort((a, b) => CompareDestinations(a, b, incomingStack, canAcceptByRules));

            foreach (var destination in destinations)
            {
                if (TryMoveStack(baseSlotToFree, destination, canAcceptByRules))
                    return true;
            }

            return false;
        }

        private static int CompareDestinations(
            BaseSlot a,
            BaseSlot b,
            ItemStack incomingStack,
            Func<BaseSlot, IItemAdapter, int, bool> canAcceptByRules)
        {
            bool aAllowsIncoming = canAcceptByRules(a, incomingStack.PrimaryAdapter, 1);
            bool bAllowsIncoming = canAcceptByRules(b, incomingStack.PrimaryAdapter, 1);

            if (aAllowsIncoming != bAllowsIncoming)
                return aAllowsIncoming ? 1 : -1;

            return a.Index.CompareTo(b.Index);
        }

        private static bool TryMoveStack(
            BaseSlot sourceBaseSlot,
            BaseSlot destinationBaseSlot,
            Func<BaseSlot, IItemAdapter, int, bool> canAcceptByRules)
        {
            if (sourceBaseSlot == null || destinationBaseSlot == null)
                return false;

            var sourceStack = sourceBaseSlot.Stack;
            if (sourceStack == null || sourceStack.IsEmpty)
                return false;

            int amountToMove = sourceStack.Count;
            var itemToMove = sourceStack.PrimaryAdapter;

            if (!destinationBaseSlot.IsEmpty)
            {
                if (!destinationBaseSlot.Stack.CanStack(itemToMove))
                    return false;

                if (!canAcceptByRules(destinationBaseSlot, itemToMove, amountToMove))
                    return false;

                var destinationStore = destinationBaseSlot.Inventory as ISlotStackStore;
                bool added = destinationStore != null
                    ? destinationStore.TryAddToSlotStack(destinationBaseSlot, sourceStack)
                    : destinationBaseSlot.Stack.TryAddToStack(sourceStack);

                if (!added)
                    return false;

                destinationBaseSlot.UpdateVisuals();
                sourceBaseSlot.Clear();
                return true;
            }

            if (!canAcceptByRules(destinationBaseSlot, itemToMove, amountToMove))
                return false;

            if (!ItemStack.TryCreate(sourceStack.Adapters, out var movedStack))
                return false;

            var emptyDestinationStore = destinationBaseSlot.Inventory as ISlotStackStore;
            if (emptyDestinationStore != null)
            {
                if (!emptyDestinationStore.TrySetStackForSlot(destinationBaseSlot, movedStack))
                    return false;
            }
            else
            {
                destinationBaseSlot.SetStack(movedStack);
            }

            destinationBaseSlot.UpdateVisuals();
            sourceBaseSlot.Clear();
            return true;
        }
    }
}
