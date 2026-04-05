using System;
using System.Collections.Generic;
using DragAndDropSystem.Core;
using DragAndDropSystem.Slots;

namespace DragAndDropSystem.Inventories
{
    /// <summary>
    /// Базовая стратегия с общими методами
    /// </summary>
    public abstract class InventoryStrategyBase : IInventoryStrategy
    {
        protected int _defaultMaxStackSize;
        protected bool _allowItemOverride;

        /// <summary>
        /// Задать лимит стака в рантайме (например, из DataBinding).
        /// </summary>
        public void SetMaxStackSize(int maxStackSize, bool allowItemOverride)
        {
            _defaultMaxStackSize = maxStackSize;
            _allowItemOverride = allowItemOverride;
        }

        public bool TryAddQuite(List<ISlot> slots, ItemStack stack, int targetIndex) => TryAdd(slots, stack, targetIndex, skipRules: true);
        public abstract bool TryAdd(List<ISlot> slots, ItemStack stack, int targetIndex, bool skipRules = false);
        public abstract bool TryRemove(List<ISlot> slots, IItemAdapter itemAdapter, int count, int sourceIndex);
        public abstract bool TryAddToSlot(List<ISlot> slots, ItemStack stack, ISlot targetSlot, System.Action ensureFreeSlots, SlotOperationContext operationContext);
        public abstract bool CanAcceptItem(List<ISlot> slots, InventoryAcceptanceRequest request, bool canCreateNewSlot, int potentialNewSlots, ISlot slotPrefab, out ISlot suggestedSlot);
        public abstract int GetAcceptableCount(List<ISlot> slots, InventoryAcceptanceRequest request, bool canCreateNewSlot, int potentialNewSlots, ISlot slotPrefab);

        public virtual int GetItemCount(List<ISlot> slots, IItemAdapter itemAdapter)
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

        public virtual bool Contains(List<ISlot> slots, IItemAdapter itemAdapter)
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

        public virtual bool CanUseAlternativeSlot(ISlot slot, IItemAdapter itemAdapter)
        {
            if (slot == null || itemAdapter == null)
                return false;

            if (slot.IsEmpty)
                return true;

            return slot.Stack != null && slot.Stack.CanStack(itemAdapter);
        }

        public virtual IEnumerable<ISlot> EnumerateAlternativeSlots(List<ISlot> slots, IItemAdapter itemAdapter, AlternativePlacementMode mode, ISlot excludeSlot)
        {
            if (slots == null || itemAdapter == null)
                yield break;

            switch (mode)
            {
                case AlternativePlacementMode.MergeOnly:
                    foreach (var slot in EnumerateMatchingSlots(slots, itemAdapter, excludeSlot, requireMergeCandidate: true, requireEmptyCandidate: false))
                        yield return slot;
                    yield break;

                case AlternativePlacementMode.EmptyFirst:
                    foreach (var slot in EnumerateMatchingSlots(slots, itemAdapter, excludeSlot, requireMergeCandidate: false, requireEmptyCandidate: true))
                        yield return slot;
                    foreach (var slot in EnumerateMatchingSlots(slots, itemAdapter, excludeSlot, requireMergeCandidate: true, requireEmptyCandidate: false))
                        yield return slot;
                    yield break;

                case AlternativePlacementMode.EmptyOnly:
                    foreach (var slot in EnumerateMatchingSlots(slots, itemAdapter, excludeSlot, requireMergeCandidate: false, requireEmptyCandidate: true))
                        yield return slot;
                    yield break;

                case AlternativePlacementMode.MergeFirst:
                default:
                    foreach (var slot in EnumerateMatchingSlots(slots, itemAdapter, excludeSlot, requireMergeCandidate: true, requireEmptyCandidate: false))
                        yield return slot;
                    foreach (var slot in EnumerateMatchingSlots(slots, itemAdapter, excludeSlot, requireMergeCandidate: false, requireEmptyCandidate: true))
                        yield return slot;
                    yield break;
            }
        }

        /// <summary>
        /// Лимит стака для предмета.
        /// Если allowItemOverride и предмет реализует IStackSizeLimitable — используется лимит предмета.
        /// Иначе — defaultMaxStackSize (0 = без ограничений).
        /// </summary>
        protected static int GetMaxStackSize(IItemAdapter itemAdapter, int defaultMaxStackSize, bool allowItemOverride)
        {
            if (allowItemOverride && itemAdapter is IStackSizeLimitable limitable)
                return Math.Max(1, limitable.MaxStackSize);
            return defaultMaxStackSize > 0 ? defaultMaxStackSize : int.MaxValue;
        }

        /// <summary>
        /// Безопасно прибавляет вместимость новых слотов (maxPerSlot × slotCount) к totalCapacity,
        /// не превышая desiredCount и без integer overflow.
        /// </summary>
        protected static int AddSlotCapacity(int totalCapacity, int maxPerSlot, int slotCount, int desiredCount)
        {
            if (maxPerSlot <= 0 || slotCount <= 0 || totalCapacity >= desiredCount)
                return totalCapacity;

            int remaining = desiredCount - totalCapacity;

            // Одного слота хватает на всё оставшееся
            if (maxPerSlot >= remaining)
                return desiredCount;

            // ceil(remaining / maxPerSlot) — сколько слотов нужно для полного покрытия
            int slotsNeeded = remaining / maxPerSlot + (remaining % maxPerSlot != 0 ? 1 : 0);
            if (slotCount >= slotsNeeded)
                return desiredCount;

            // Гарантия: slotCount < slotsNeeded → maxPerSlot * slotCount < remaining,
            // поэтому произведение не превышает remaining и overflow невозможен.
            return totalCapacity + maxPerSlot * slotCount;
        }

        protected bool PassesRules(ISlot slot, IItemAdapter itemAdapter, int previewCount, InventoryAcceptanceRequest request = null)
        {
            if (slot == null || itemAdapter == null || previewCount <= 0)
                return false;

            if (slot.Inventory is UniversalInventory inventory)
                return inventory.CanAcceptByRules(slot, itemAdapter, previewCount, request);

            return true;
        }

        protected int FindEmptySlotIndex(List<ISlot> slots)
        {
            for (int i = 0; i < slots.Count; i++)
            {
                if (slots[i].IsEmpty)
                    return i;
            }
            return -1;
        }

        protected int FindSlotWithItem(List<ISlot> slots, IItemAdapter itemAdapter)
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

        protected bool PrefabPassesRules(List<ISlot> slots, ISlot slotPrefab, IItemAdapter itemAdapter, int previewCount, InventoryAcceptanceRequest request)
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
                return inventory.CanAcceptByRules(slotPrefab, itemAdapter, previewCount, request, allowForeignSlot: true);

            if (slotPrefab?.SlotRuleValidator == null)
                return true;

            var context = request?.CreateValidationContext(slotPrefab, previewCount, itemAdapter);
            if (context == null)
            {
                if (!ItemStack.TryCreate(new[] { itemAdapter }, out var fallbackStack))
                    return false;
                context = new DragContext(fallbackStack, null, null, slotPrefab, null);
            }
            var entry = context.Entries[0];
            return slotPrefab.SlotRuleValidator.ValidateDrop(context, entry).IsValid;
        }

        protected static bool TryMergeIntoSlot(ItemStack stack, ISlot slot, int maxStackSize, System.Action ensureFreeSlots, SlotOperationContext operationContext)
        {
            int canFit = Math.Max(0, maxStackSize - slot.Stack.Count);
            int toAdd = Math.Min(stack.Count, canFit);
            if (toAdd <= 0) return false;

            var movedStack = stack.Split(toAdd);
            if (movedStack.IsEmpty)
                return false;

            if (!slot.Stack.TryAddToStack(movedStack))
            {
                stack.TryAddToStack(movedStack);
                return false;
            }

            slot.UpdateVisuals();
            operationContext?.RecordResult(slot, false, toAdd);
            ensureFreeSlots?.Invoke();
            return true;
        }

        protected static bool TryPlaceIntoEmptySlot(ItemStack stack, ISlot slot, int maxStackSize, System.Action ensureFreeSlots, SlotOperationContext operationContext)
        {
            int toPlace = Math.Min(stack.Count, maxStackSize);
            if (toPlace <= 0) return false;

            bool slotWasEmpty = slot.IsEmpty;
            var movedStack = stack.Split(toPlace);
            if (movedStack.IsEmpty)
                return false;

            slot.SetStack(movedStack);
            slot.UpdateVisuals();
            operationContext?.RecordResult(slot, slotWasEmpty, toPlace);
            ensureFreeSlots?.Invoke();
            return true;
        }

        private IEnumerable<ISlot> EnumerateMatchingSlots(
            List<ISlot> slots,
            IItemAdapter itemAdapter,
            ISlot excludeSlot,
            bool requireMergeCandidate,
            bool requireEmptyCandidate)
        {
            for (int i = 0; i < slots.Count; i++)
            {
                var slot = slots[i];
                if (slot == null || ReferenceEquals(slot, excludeSlot))
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
