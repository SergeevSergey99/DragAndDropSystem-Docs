using System;
using System.Collections.Generic;
using DragAndDropSystem.Core;
using DragAndDropSystem.Slots;

namespace DragAndDropSystem.Inventories
{
    /// <summary>
    /// Пытается переупаковать предметы в инвентаре, чтобы освободить подходящий слот
    /// для нового предмета. Stateless — все зависимости передаются через параметры.
    /// </summary>
    public static class SlotRelocationService
    {
        /// <summary>
        /// Попытаться переместить occupant из подходящего слота, затем повторить размещение.
        /// </summary>
        public static bool TryRelocateAndRetry(
            List<ISlot> slots,
            ItemStack stack,
            int targetSlotIndex,
            IPlacementStrategy placementStrategy,
            Func<ISlot, IItemAdapter, int, bool> canAcceptByRules,
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

        private static List<ISlot> BuildRelocationCandidates(
            List<ISlot> slots,
            ItemStack stack,
            int targetSlotIndex,
            Func<ISlot, IItemAdapter, int, bool> canAcceptByRules)
        {
            var result = new List<ISlot>();

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

                if (canAcceptByRules(slot, stack.ItemAdapter, stack.Count))
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
            List<ISlot> slots,
            ISlot slotToFree,
            ItemStack incomingStack,
            Func<ISlot, IItemAdapter, int, bool> canAcceptByRules)
        {
            if (slotToFree == null || slotToFree.IsEmpty)
                return false;

            var occupantStack = slotToFree.Stack;
            if (occupantStack == null || occupantStack.IsEmpty)
                return false;

            var destinations = new List<ISlot>();
            for (int i = 0; i < slots.Count; i++)
            {
                var slot = slots[i];
                if (slot == slotToFree)
                    continue;

                if (!slot.IsEmpty)
                {
                    if (!slot.Stack.CanStack(occupantStack.ItemAdapter))
                        continue;

                    if (!canAcceptByRules(slot, occupantStack.ItemAdapter, occupantStack.Count))
                        continue;

                    destinations.Add(slot);
                }
                else
                {
                    if (!canAcceptByRules(slot, occupantStack.ItemAdapter, occupantStack.Count))
                        continue;

                    destinations.Add(slot);
                }
            }

            destinations.Sort((a, b) => CompareDestinations(a, b, incomingStack, canAcceptByRules));

            foreach (var destination in destinations)
            {
                if (TryMoveStack(slotToFree, destination, canAcceptByRules))
                    return true;
            }

            return false;
        }

        private static int CompareDestinations(
            ISlot a,
            ISlot b,
            ItemStack incomingStack,
            Func<ISlot, IItemAdapter, int, bool> canAcceptByRules)
        {
            bool aAllowsIncoming = canAcceptByRules(a, incomingStack.ItemAdapter, 1);
            bool bAllowsIncoming = canAcceptByRules(b, incomingStack.ItemAdapter, 1);

            if (aAllowsIncoming != bAllowsIncoming)
                return aAllowsIncoming ? 1 : -1;

            return a.Index.CompareTo(b.Index);
        }

        private static bool TryMoveStack(
            ISlot sourceSlot,
            ISlot destinationSlot,
            Func<ISlot, IItemAdapter, int, bool> canAcceptByRules)
        {
            if (sourceSlot == null || destinationSlot == null)
                return false;

            var sourceStack = sourceSlot.Stack;
            if (sourceStack == null || sourceStack.IsEmpty)
                return false;

            int amountToMove = sourceStack.Count;
            var itemToMove = sourceStack.ItemAdapter;

            if (!destinationSlot.IsEmpty)
            {
                if (!destinationSlot.Stack.CanStack(itemToMove))
                    return false;

                if (!canAcceptByRules(destinationSlot, itemToMove, amountToMove))
                    return false;

                destinationSlot.Stack.AddToStack(amountToMove);
                destinationSlot.UpdateVisuals();
                sourceSlot.Clear();
                return true;
            }

            if (!canAcceptByRules(destinationSlot, itemToMove, amountToMove))
                return false;

            var movedStack = new ItemStack(itemToMove, amountToMove);
            destinationSlot.SetStack(movedStack);
            destinationSlot.UpdateVisuals();
            sourceSlot.Clear();
            return true;
        }
    }
}
