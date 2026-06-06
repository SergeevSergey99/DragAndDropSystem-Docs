using System;
using System.Collections.Generic;
using UnityEngine;
using UDND.Core;
using UDND.Slots;
using UDND.Tools.Inspector;

namespace UDND.Inventories
{
    /// <summary>
    /// Base strategy with shared methods
    /// </summary>
    [Serializable]
    public abstract class InventoryStrategyBase : IInventoryStrategy, IPlacementStrategy, IAcceptanceStrategy, IDragPolicy, IInventoryQueryStrategy
    {
        [SerializeField, LabelText("Drag Amount"), Tooltip("How many items to take when dragging from a stack.")]
        [ShowIf(nameof(ShowDragAmountSettings))]
        private DragAmount _dragAmount = DragAmount.All;

        [SerializeField, Tooltip("Item amount for Custom drag.")]
        [ShowIf(nameof(ShowCustomDragAmount))]
        private int _customDragAmount = 1;

        private bool ShowCustomDragAmount => ShowDragAmountSettings && _dragAmount == DragAmount.Custom;
        protected virtual bool ShowDragAmountSettings => true;

        /// <summary>
        /// Set the stack limit at runtime (for example, from DataBinding).
        /// </summary>
        public virtual void SetMaxStackSize(int maxStackSize, bool allowItemOverride)
        {
        }

        public virtual int GetMaxStackSizeForItem(IItemAdapter itemAdapter)
        {
            return itemAdapter == null ? 0 : int.MaxValue;
        }

        public bool TryAddQuiet(List<BaseSlot> slots, ItemStack stack, int targetIndex) => TryAdd(slots, stack, targetIndex, skipRules: true);
        public abstract bool TryAdd(List<BaseSlot> slots, ItemStack stack, int targetIndex, bool skipRules = false);
        public abstract bool TryRemove(List<BaseSlot> slots, IItemAdapter itemAdapter, int count, int sourceIndex);
        public abstract bool TryAddToSlot(List<BaseSlot> slots, ItemStack stack, BaseSlot targetBaseSlot, System.Action ensureFreeSlots, SlotOperationContext operationContext);
        public abstract SlotAcceptanceCandidates GetSlotCandidates(IReadOnlyList<ISlot> slots, InventoryAcceptanceRequest request, bool canCreateNewSlot, int potentialNewSlots, BaseSlot baseSlotPrefab);
        public virtual SlotSelectionPolicyBase DefaultSlotSelectionPolicy => FirstSlotSelectionPolicy.Instance;
        public abstract int GetAcceptableCount(List<BaseSlot> slots, InventoryAcceptanceRequest request, bool canCreateNewSlot, int potentialNewSlots, BaseSlot baseSlotPrefab);

        public virtual int GetItemCount(List<BaseSlot> slots, IItemAdapter itemAdapter)
        {
            int total = 0;
            HashSet<Placement> seenPlacements = null;
            foreach (var slot in slots)
            {
                if (ShouldSkipDuplicatePlacementLocation(slot, ref seenPlacements))
                    continue;

                if (!slot.IsEmpty && slot.Stack.CanStack(itemAdapter))
                {
                    total += slot.Stack.Count;
                }
            }
            return total;
        }

        public virtual bool Contains(List<BaseSlot> slots, IItemAdapter itemAdapter)
        {
            HashSet<Placement> seenPlacements = null;
            foreach (var slot in slots)
            {
                if (ShouldSkipDuplicatePlacementLocation(slot, ref seenPlacements))
                    continue;

                if (!slot.IsEmpty && slot.Stack.CanStack(itemAdapter))
                {
                    return true;
                }
            }
            return false;
        }

        public virtual int ResolveDragAmount(int stackCount, DragAmount dragAmount, int customDragAmount)
        {
            DragAmount amount = dragAmount;
            int customAmount = customDragAmount;

            if (amount == DragAmount.All && customAmount == 0)
            {
                amount = _dragAmount;
                customAmount = _customDragAmount;
            }

            switch (amount)
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
                    return Mathf.Min(customAmount, stackCount);

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

        /// <summary>
        /// Stack limit for an item.
        /// If allowItemOverride is enabled and the item implements IStackSizeLimitable, the item limit is used.
        /// Otherwise, defaultMaxStackSize is used (0 = unlimited).
        /// </summary>
        protected static int GetMaxStackSize(IItemAdapter itemAdapter, int defaultMaxStackSize, bool allowItemOverride)
        {
            // Shaped (multi-cell) items are no longer forced to a max stack of 1: a placement may carry
            // a stack with count > 1, capped by the strategy limit just like single-cell items.
            // See ShapedStacking-Plan.md (C2). Unique items stay count 1 via UniqueItemStrategy (literal 1).
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

            if (baseSlot.Inventory is IInventoryRuleEvaluator ruleEvaluator)
                return ruleEvaluator.CanAcceptByRules(baseSlot, itemAdapter, previewCount, request);

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

            IInventoryRuleEvaluator ruleEvaluator = request?.TargetInventory as IInventoryRuleEvaluator;
            if (ruleEvaluator == null)
            {
                foreach (var slot in slots)
                {
                    ruleEvaluator = slot?.Inventory as IInventoryRuleEvaluator;
                    if (ruleEvaluator != null)
                        break;
                }
            }

            if (ruleEvaluator != null)
                return ruleEvaluator.CanAcceptByRules(baseSlotPrefab, itemAdapter, previewCount, request, allowForeignSlot: true);

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

        protected bool PrefabPassesRules(IReadOnlyList<ISlot> slots, BaseSlot baseSlotPrefab, IItemAdapter itemAdapter, int previewCount, InventoryAcceptanceRequest request)
        {
            if (itemAdapter == null || previewCount <= 0)
                return false;

            IInventoryRuleEvaluator ruleEvaluator = request?.TargetInventory as IInventoryRuleEvaluator;
            if (ruleEvaluator == null)
            {
                foreach (var slot in slots)
                {
                    ruleEvaluator = slot?.Inventory as IInventoryRuleEvaluator;
                    if (ruleEvaluator != null)
                        break;
                }
            }

            if (ruleEvaluator != null)
                return ruleEvaluator.CanAcceptByRules(baseSlotPrefab, itemAdapter, previewCount, request, allowForeignSlot: true);

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

        protected static BaseSlot ResolveBaseSlot(ISlot slot)
        {
            if (slot is BaseSlot b)
                return b;
            var inv = slot?.Inventory;
            if (inv == null)
                return null;
            var slotList = inv.Slots;
            int idx = slot.Index;
            return idx >= 0 && idx < slotList.Count ? slotList[idx] : null;
        }

        protected static bool IsSourceSlot(ISlot slot, InventoryAcceptanceRequest request)
        {
            if (slot == null || request?.SourceBaseSlot == null)
                return false;

            var baseSlot = ResolveBaseSlot(slot);
            if (baseSlot == null || !ReferenceEquals(request.SourceInventory, baseSlot.Inventory))
                return false;

            if (baseSlot.Index == request.SourceBaseSlot.Index)
                return true;

            if (request.SourceInventory is IPlacementInventory placementInventory)
            {
                var sourcePlacement = placementInventory.GetPlacementAt(request.SourceBaseSlot);
                if (sourcePlacement == null)
                    return false;

                var slotPlacement = placementInventory.GetPlacementAt(baseSlot);
                return ReferenceEquals(sourcePlacement, slotPlacement);
            }

            return false;
        }

        protected static bool ShouldSkipDuplicatePlacementLocation(ISlot slot, ref HashSet<Placement> seenPlacements)
        {
            if (!TryResolvePlacement(slot, out var placement))
                return false;

            seenPlacements ??= new HashSet<Placement>();
            return !seenPlacements.Add(placement);
        }

        protected static ISlot ResolveLogicalStackSlot(ISlot slot, IReadOnlyList<ISlot> slots)
        {
            if (!TryResolvePlacement(slot, out var placement))
                return slot;

            if (slot.Index == placement.AnchorIndex)
                return slot;

            var inventory = slot.Inventory;
            if (inventory == null || slots == null)
                return slot;

            for (int i = 0; i < slots.Count; i++)
            {
                var candidate = slots[i];
                if (candidate != null &&
                    candidate.Index == placement.AnchorIndex &&
                    ReferenceEquals(candidate.Inventory, inventory))
                    return candidate;
            }

            return slot;
        }

        protected static bool TryResolvePlacement(ISlot slot, out Placement placement)
        {
            placement = null;
            var baseSlot = ResolveBaseSlot(slot);
            if (baseSlot?.Inventory is not IPlacementInventory placementInventory)
                return false;

            placement = placementInventory.GetPlacementAt(baseSlot);
            return placement != null;
        }

        protected static bool TryMergeIntoSlot(ItemStack stack, BaseSlot baseSlot, int maxStackSize, System.Action ensureFreeSlots, SlotOperationContext operationContext)
            => TryMergeIntoSlot(stack, baseSlot, maxStackSize, ensureFreeSlots, operationContext, out _);

        protected static bool TryMergeIntoSlot(
            ItemStack stack,
            BaseSlot baseSlot,
            int maxStackSize,
            System.Action ensureFreeSlots,
            SlotOperationContext operationContext,
            out int addedAmount)
        {
            addedAmount = 0;
            int canFit = Math.Max(0, maxStackSize - baseSlot.Stack.Count);
            int toAdd = Math.Min(stack.Count, canFit);
            if (toAdd <= 0) return false;

            var movedStack = stack.Split(toAdd);
            if (movedStack.IsEmpty)
                return false;

            if (baseSlot.Inventory == null)
            {
                stack.TryAddToStack(movedStack);
                return false;
            }

            int before = baseSlot.Stack.Count;
            if (!baseSlot.Inventory.TryAddToSlotStack(baseSlot, movedStack))
            {
                stack.TryAddToStack(movedStack);
                return false;
            }

            baseSlot.UpdateVisuals();
            addedAmount = Math.Max(0, baseSlot.Stack.Count - before);
            if (addedAmount <= 0)
            {
                stack.TryAddToStack(movedStack);
                return false;
            }

            operationContext?.RecordResult(baseSlot, false, addedAmount);
            ensureFreeSlots?.Invoke();
            return true;
        }

        protected static int RemoveFromSlot(BaseSlot baseSlot, int amount)
        {
            if (baseSlot == null || amount <= 0)
                return 0;

            if (baseSlot.Inventory != null &&
                baseSlot.Inventory.TrySplitFromSlot(baseSlot, amount, out var removedStack))
                return removedStack.Count;

            return 0;
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

        internal string CaptureConfigurationJson()
        {
            return JsonUtility.ToJson(this);
        }
    }
}
