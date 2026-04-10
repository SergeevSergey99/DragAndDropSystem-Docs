using System;
using System.Collections.Generic;
using DragAndDropSystem.Core;
using DragAndDropSystem.Slots;

namespace DragAndDropSystem.Inventories
{
    /// <summary>
    /// Base strategy with shared methods
    /// </summary>
    public abstract class InventoryStrategyBase : IInventoryStrategy
    {
        protected int _defaultMaxStackSize;
        protected bool _allowItemOverride;

        /// <summary>
        /// Set the stack limit at runtime (for example, from DataBinding).
        /// </summary>
        public void SetMaxStackSize(int maxStackSize, bool allowItemOverride)
        {
            _defaultMaxStackSize = maxStackSize;
            _allowItemOverride = allowItemOverride;
        }

        public bool TryAddQuite(List<BaseSlot> slots, ItemStack stack, int targetIndex) => TryAdd(slots, stack, targetIndex, skipRules: true);
        public abstract bool TryAdd(List<BaseSlot> slots, ItemStack stack, int targetIndex, bool skipRules = false);
        public abstract bool TryRemove(List<BaseSlot> slots, IItemAdapter itemAdapter, int count, int sourceIndex);
        public abstract bool TryAddToSlot(List<BaseSlot> slots, ItemStack stack, BaseSlot targetBaseSlot, System.Action ensureFreeSlots, SlotOperationContext operationContext);
        public abstract bool CanAcceptItem(List<BaseSlot> slots, InventoryAcceptanceRequest request, bool canCreateNewSlot, int potentialNewSlots, BaseSlot baseSlotPrefab, out BaseSlot suggestedBaseSlot);
        public abstract int GetAcceptableCount(List<BaseSlot> slots, InventoryAcceptanceRequest request, bool canCreateNewSlot, int potentialNewSlots, BaseSlot baseSlotPrefab);

        public virtual int GetItemCount(List<BaseSlot> slots, IItemAdapter itemAdapter)
        {
            int total = 0;
            foreach (var slot in slots)
            {
                if (!slot.IsEmpty && slot.Stack.CanStack(itemAdapter))
                {
                    total += slot.Stack.Count;
                }
            }
            return total;
        }

        public virtual bool Contains(List<BaseSlot> slots, IItemAdapter itemAdapter)
        {
            foreach (var slot in slots)
            {
                if (!slot.IsEmpty && slot.Stack.CanStack(itemAdapter))
                {
                    return true;
                }
            }
            return false;
        }

        public virtual int ResolveDragAmount(int stackCount, DragAmount dragAmount, int customDragAmount)
        {
            switch (dragAmount)
            {
                case DragAmount.One:
                    return 1;

                case DragAmount.HalfDown:
                    return UnityEngine.Mathf.Max(1, stackCount / 2);
                
                case DragAmount.HalfUp:
                    return UnityEngine.Mathf.Max(1, UnityEngine.Mathf.CeilToInt(stackCount / 2f));

                case DragAmount.All:
                    return stackCount;

                case DragAmount.Custom:
                    return UnityEngine.Mathf.Min(customDragAmount, stackCount);

                default:
                    return stackCount;
            }
        }

        public virtual bool RequiresStrategyPlacement(ItemStack stack) => false;
        public virtual bool UsesPerItemSlotPlanning => false;

        public virtual bool CanUseAlternativeSlot(BaseSlot baseSlot, IItemAdapter itemAdapter)
        {
            if (baseSlot == null || itemAdapter == null)
                return false;

            if (baseSlot.IsEmpty)
                return true;

            return baseSlot.Stack != null && baseSlot.Stack.CanStack(itemAdapter);
        }

        public virtual IEnumerable<BaseSlot> EnumerateAlternativeSlots(List<BaseSlot> slots, IItemAdapter itemAdapter, AlternativePlacementMode mode, BaseSlot excludeBaseSlot)
        {
            if (slots == null || itemAdapter == null)
                yield break;

            switch (mode)
            {
                case AlternativePlacementMode.MergeOnly:
                    foreach (var slot in EnumerateMatchingSlots(slots, itemAdapter, excludeBaseSlot, requireMergeCandidate: true, requireEmptyCandidate: false))
                        yield return slot;
                    yield break;

                case AlternativePlacementMode.EmptyFirst:
                    foreach (var slot in EnumerateMatchingSlots(slots, itemAdapter, excludeBaseSlot, requireMergeCandidate: false, requireEmptyCandidate: true))
                        yield return slot;
                    foreach (var slot in EnumerateMatchingSlots(slots, itemAdapter, excludeBaseSlot, requireMergeCandidate: true, requireEmptyCandidate: false))
                        yield return slot;
                    yield break;

                case AlternativePlacementMode.EmptyOnly:
                    foreach (var slot in EnumerateMatchingSlots(slots, itemAdapter, excludeBaseSlot, requireMergeCandidate: false, requireEmptyCandidate: true))
                        yield return slot;
                    yield break;

                case AlternativePlacementMode.MergeFirst:
                default:
                    foreach (var slot in EnumerateMatchingSlots(slots, itemAdapter, excludeBaseSlot, requireMergeCandidate: true, requireEmptyCandidate: false))
                        yield return slot;
                    foreach (var slot in EnumerateMatchingSlots(slots, itemAdapter, excludeBaseSlot, requireMergeCandidate: false, requireEmptyCandidate: true))
                        yield return slot;
                    yield break;
            }
        }

        /// <summary>
        /// Stack limit for an item.
        /// If allowItemOverride is enabled and the item implements IStackSizeLimitable, the item limit is used.
        /// Otherwise, defaultMaxStackSize is used (0 = unlimited).
        /// </summary>
        protected static int GetMaxStackSize(IItemAdapter itemAdapter, int defaultMaxStackSize, bool allowItemOverride)
        {
            if (allowItemOverride && itemAdapter is IStackSizeLimitable limitable)
                return Math.Max(1, limitable.MaxStackSize);
            return defaultMaxStackSize > 0 ? defaultMaxStackSize : int.MaxValue;
        }

        /// <summary>
        /// Safely adds capacity of new slots (maxPerSlot x slotCount) to totalCapacity,
        /// without exceeding desiredCount and without integer overflow.
        /// </summary>
        protected static int AddSlotCapacity(int totalCapacity, int maxPerSlot, int slotCount, int desiredCount)
        {
            if (maxPerSlot <= 0 || slotCount <= 0 || totalCapacity >= desiredCount)
                return totalCapacity;

            int remaining = desiredCount - totalCapacity;

            // One slot is enough for everything remaining
            if (maxPerSlot >= remaining)
                return desiredCount;

            // ceil(remaining / maxPerSlot): how many slots are needed for full coverage
            int slotsNeeded = remaining / maxPerSlot + (remaining % maxPerSlot != 0 ? 1 : 0);
            if (slotCount >= slotsNeeded)
                return desiredCount;

            // Guarantee: slotCount < slotsNeeded -> maxPerSlot * slotCount < remaining,
            // therefore the product never exceeds remaining and overflow is impossible.
            return totalCapacity + maxPerSlot * slotCount;
        }

        protected bool PassesRules(BaseSlot baseSlot, IItemAdapter itemAdapter, int previewCount, InventoryAcceptanceRequest request = null)
        {
            if (baseSlot == null || itemAdapter == null || previewCount <= 0)
                return false;

            if (baseSlot.Inventory is UniversalInventory inventory)
                return inventory.CanAcceptByRules(baseSlot, itemAdapter, previewCount, request);

            return true;
        }

        protected int FindEmptySlotIndex(List<BaseSlot> slots)
        {
            for (int i = 0; i < slots.Count; i++)
            {
                if (slots[i].IsEmpty)
                    return i;
            }
            return -1;
        }

        protected int FindSlotWithItem(List<BaseSlot> slots, IItemAdapter itemAdapter)
        {
            for (int i = 0; i < slots.Count; i++)
            {
                if (!slots[i].IsEmpty && slots[i].Stack.CanStack(itemAdapter))
                {
                    return i;
                }
            }
            return -1;
        }

        protected bool PrefabPassesRules(List<BaseSlot> slots, BaseSlot baseSlotPrefab, IItemAdapter itemAdapter, int previewCount, InventoryAcceptanceRequest request)
        {
            if (itemAdapter == null || previewCount <= 0)
                return false;

            UniversalInventory inventory = request?.TargetInventory as UniversalInventory;
            if (inventory == null)
            {
                foreach (var slot in slots)
                {
                    inventory = slot?.Inventory as UniversalInventory;
                    if (inventory != null)
                        break;
                }
            }

            if (inventory != null)
                return inventory.CanAcceptByRules(baseSlotPrefab, itemAdapter, previewCount, request, allowForeignSlot: true);

            if (baseSlotPrefab?.SlotRuleValidator == null)
                return true;

            var context = request?.CreateValidationContext(baseSlotPrefab, previewCount, itemAdapter);
            if (context == null)
            {
                if (!ItemStack.TryCreate(new[] { itemAdapter }, out var fallbackStack))
                    return false;
                context = new DragContext(fallbackStack, null, null, baseSlotPrefab, null);
            }
            var entry = context.Entries[0];
            return baseSlotPrefab.SlotRuleValidator.ValidateDrop(context, entry).IsValid;
        }

        protected static bool TryMergeIntoSlot(ItemStack stack, BaseSlot baseSlot, int maxStackSize, System.Action ensureFreeSlots, SlotOperationContext operationContext)
        {
            int canFit = Math.Max(0, maxStackSize - baseSlot.Stack.Count);
            int toAdd = Math.Min(stack.Count, canFit);
            if (toAdd <= 0) return false;

            var movedStack = stack.Split(toAdd);
            if (movedStack.IsEmpty)
                return false;

            if (!baseSlot.Stack.TryAddToStack(movedStack))
            {
                stack.TryAddToStack(movedStack);
                return false;
            }

            baseSlot.UpdateVisuals();
            operationContext?.RecordResult(baseSlot, false, toAdd);
            ensureFreeSlots?.Invoke();
            return true;
        }

        protected static bool TryPlaceIntoEmptySlot(ItemStack stack, BaseSlot baseSlot, int maxStackSize, System.Action ensureFreeSlots, SlotOperationContext operationContext)
        {
            int toPlace = Math.Min(stack.Count, maxStackSize);
            if (toPlace <= 0) return false;

            bool slotWasEmpty = baseSlot.IsEmpty;
            var movedStack = stack.Split(toPlace);
            if (movedStack.IsEmpty)
                return false;

            baseSlot.SetStack(movedStack);
            baseSlot.UpdateVisuals();
            operationContext?.RecordResult(baseSlot, slotWasEmpty, toPlace);
            ensureFreeSlots?.Invoke();
            return true;
        }

        private IEnumerable<BaseSlot> EnumerateMatchingSlots(
            List<BaseSlot> slots,
            IItemAdapter itemAdapter,
            BaseSlot excludeBaseSlot,
            bool requireMergeCandidate,
            bool requireEmptyCandidate)
        {
            for (int i = 0; i < slots.Count; i++)
            {
                var slot = slots[i];
                if (slot == null || ReferenceEquals(slot, excludeBaseSlot))
                    continue;

                bool isEmpty = slot.IsEmpty;
                if (requireEmptyCandidate && !isEmpty)
                    continue;
                if (requireMergeCandidate && isEmpty)
                    continue;
                if (!CanUseAlternativeSlot(slot, itemAdapter))
                    continue;

                yield return slot;
            }
        }
    }
}
