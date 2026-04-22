using System.Collections.Generic;
using UniversalDragAndDrop.Core;
using UniversalDragAndDrop.Slots;
using UniversalDragAndDrop.Tools;

namespace UniversalDragAndDrop.Inventories
{
    /// <summary>
    /// Decorator for dynamic slot creation
    /// Wraps any strategy (Unique or Stackable) and adds automatic slot creation
    /// </summary>
    public class DynamicSlotDecorator : IInventoryStrategy
    {
        private readonly IInventoryStrategy _baseStrategy;
        private readonly System.Func<BaseSlot> _createSlotFunc;
        private readonly int _maxSlots;
        private readonly int _maxFreeSlots;
        private readonly System.Func<List<BaseSlot>> _getSlotsFunc;
        private readonly System.Action _ensureFreeSlotsFunc;

        public DynamicSlotDecorator(
            IInventoryStrategy baseStrategy,
            System.Func<BaseSlot> createSlotFunc,
            int maxSlots = 100,
            int maxFreeSlots = 1,
            System.Func<List<BaseSlot>> getSlotsFunc = null,
            System.Action ensureFreeSlotsFunc = null)
        {
            _baseStrategy = baseStrategy;
            _createSlotFunc = createSlotFunc;
            _maxSlots = maxSlots;
            _maxFreeSlots = maxFreeSlots;
            _getSlotsFunc = getSlotsFunc;
            _ensureFreeSlotsFunc = ensureFreeSlotsFunc;
        }

        public bool TryAddQuite(List<BaseSlot> slots, ItemStack stack, int targetIndex) => TryAdd(slots, stack, targetIndex, skipRules: true);

        public bool TryAdd(List<BaseSlot> slots, ItemStack stack, int targetIndex, bool skipRules = false)
        {
            if (stack == null || stack.IsEmpty)
                return false;

            int initialCount = stack.Count;

            // MODE 1: targetIndex is specified (transfer into a specific slot)
            if (targetIndex >= 0)
            {
                // If targetIndex is out of range, create slots up to it (only if maxFreeSlots > 0)
                if (targetIndex >= slots.Count && _maxFreeSlots > 0 && slots.Count < _maxSlots)
                {
                    while (slots.Count <= targetIndex && slots.Count < _maxSlots)
                    {
                        var newSlot = _createSlotFunc();
                        if (newSlot == null)
                            break;
                        Extensions.DragAndDropLog($"<color=green>[DynamicSlots] Created slot {slots.Count - 1} to reach targetIndex {targetIndex}</color>");
                    }
                }

                // Try to add into the target slot
                bool added = _baseStrategy.TryAdd(slots, stack, targetIndex, skipRules);

                // Ensure the minimum number of free slots after adding
                if (added && stack.IsEmpty)
                {
                    _ensureFreeSlotsFunc?.Invoke();
                }

                return added;
            }

            // MODE 2: targetIndex is not specified (addition through TryAddItem)
            // In this mode we ALWAYS create slots if needed (regardless of maxFreeSlots)

            // First try adding into existing slots
            bool initialAdded = _baseStrategy.TryAdd(slots, stack, -1, skipRules);

            // If everything does not fit, create new slots and continue
            if (!stack.IsEmpty && slots.Count < _maxSlots)
            {
                Extensions.DragAndDropLog($"<color=yellow>[DynamicSlots] Stack not empty ({stack.Count} remaining), creating new slots...</color>");

                int createdSlots = 0;
                while (!stack.IsEmpty && slots.Count < _maxSlots)
                {
                    var newSlot = _createSlotFunc();
                    if (newSlot == null)
                        break;

                    createdSlots++;
                    Extensions.DragAndDropLog($"<color=green>[DynamicSlots] Created slot {slots.Count - 1} for remaining items</color>");

                    // Try to add into the new slot
                    _baseStrategy.TryAdd(slots, stack, slots.Count - 1, skipRules);
                }

                Extensions.DragAndDropLog($"<color=cyan>[DynamicSlots] Created {createdSlots} new slots, {stack.Count} items still remaining</color>");
            }

            // Ensure the minimum number of free slots after adding
            if (stack.Count < initialCount)
            {
                _ensureFreeSlotsFunc?.Invoke();
            }

            // Return true if anything was added at all
            return stack.Count == 0;
        }

        public bool TryRemove(List<BaseSlot> slots, IItemAdapter itemAdapter, int count, int sourceIndex)
        {
            bool removed = _baseStrategy.TryRemove(slots, itemAdapter, count, sourceIndex);

            // Ensure the minimum number of free slots after removal
            if (removed)
            {
                _ensureFreeSlotsFunc?.Invoke();
            }

            return removed;
        }

        public int GetItemCount(List<BaseSlot> slots, IItemAdapter itemAdapter)
        {
            return _baseStrategy.GetItemCount(slots, itemAdapter);
        }

        public bool Contains(List<BaseSlot> slots, IItemAdapter itemAdapter)
        {
            return _baseStrategy.Contains(slots, itemAdapter);
        }

        public int ResolveDragAmount(int stackCount, DragAmount dragAmount, int customDragAmount)
        {
            return _baseStrategy.ResolveDragAmount(stackCount, dragAmount, customDragAmount);
        }

        public bool RequiresStrategyPlacement(ItemStack stack)
        {
            return _baseStrategy.RequiresStrategyPlacement(stack);
        }

        public bool UsesPerItemSlotPlanning => _baseStrategy.UsesPerItemSlotPlanning;

        public bool CanUseAlternativeSlot(BaseSlot baseSlot, IItemAdapter itemAdapter)
        {
            return _baseStrategy.CanUseAlternativeSlot(baseSlot, itemAdapter);
        }

        public bool TryAddToSlot(List<BaseSlot> slots, ItemStack stack, BaseSlot targetBaseSlot, System.Action ensureFreeSlots, SlotOperationContext operationContext)
        {
            return _baseStrategy.TryAddToSlot(slots, stack, targetBaseSlot, ensureFreeSlots, operationContext);
        }

        public bool CanAcceptItem(List<BaseSlot> slots, InventoryAcceptanceRequest request, bool canCreateNewSlot, int potentialNewSlots, BaseSlot baseSlotPrefab, out BaseSlot suggestedBaseSlot)
        {
            return _baseStrategy.CanAcceptItem(slots, request, canCreateNewSlot, potentialNewSlots, baseSlotPrefab, out suggestedBaseSlot);
        }

        public int GetAcceptableCount(List<BaseSlot> slots, InventoryAcceptanceRequest request, bool canCreateNewSlot, int potentialNewSlots, BaseSlot baseSlotPrefab)
        {
            return _baseStrategy.GetAcceptableCount(slots, request, canCreateNewSlot, potentialNewSlots, baseSlotPrefab);
        }

        public void SetMaxStackSize(int maxStackSize, bool allowItemOverride)
        {
            _baseStrategy.SetMaxStackSize(maxStackSize, allowItemOverride);
        }
    }
}