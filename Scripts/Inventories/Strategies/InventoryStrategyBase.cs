using System;
using System.Collections.Generic;
using UnityEngine;
using UniversalDragAndDrop.Core;
using UniversalDragAndDrop.Slots;
using UniversalDragAndDrop.Tools.Inspector;

namespace UniversalDragAndDrop.Inventories
{
    /// <summary>
    /// Base strategy with shared methods
    /// </summary>
    [Serializable]
    public abstract class InventoryStrategyBase : IInventoryStrategy
    {
        [SerializeField, LabelText("Drag Amount"), Tooltip("How many items to take when dragging from a stack.")]
        [ShowIf(nameof(ShowDragAmountSettings))]
        private DragAmount _dragAmount = DragAmount.All;

        [SerializeField, Tooltip("Item amount for Custom drag.")]
        [ShowIf(nameof(ShowCustomDragAmount))]
        private int _customDragAmount = 1;

        [NonSerialized] protected UniversalInventory _inventory;
        private static bool _warnedMissingStackStoreForMerge;
        private static bool _warnedMissingStackStoreForRemove;

        private bool ShowCustomDragAmount => ShowDragAmountSettings && _dragAmount == DragAmount.Custom;
        protected virtual bool ShowDragAmountSettings => true;

        public virtual void BindInventory(UniversalInventory inventory)
        {
            _inventory = inventory;
        }

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

        public virtual ShapedPlacementPlanResult TryPlanShapedPlacement(ShapedPlacementPlanContext context)
        {
            if (context.Footprint.IsSingleCell)
                return ShapedPlacementPlanResult.NotApplicable();

            if (context.TargetInventory is not IPlacementInventory targetPlacementInventory || !targetPlacementInventory.Grid.HasValue)
                return ShapedPlacementPlanResult.NotApplicable();

            if (context.RequestedAmount != 1)
                return ShapedPlacementPlanResult.Rejected("Shaped grid placement requires a single item");

            if (context.TargetBaseSlotHint == null ||
                !ReferenceEquals(context.TargetBaseSlotHint.Inventory, context.TargetInventory))
                return ShapedPlacementPlanResult.Rejected("Shaped grid placement requires a target cell");

            if (!targetPlacementInventory.TryResolveShapedPlacementAnchor(
                    context.TargetBaseSlotHint,
                    context.DragContext,
                    context.Entry,
                    context.Footprint,
                    context.TargetItemAdapter,
                    out _,
                    out int anchorIndex))
                return ShapedPlacementPlanResult.Rejected("Shaped item anchor is outside the target grid");

            var acceptanceRequest = new InventoryAcceptanceRequest(
                context.TargetInventory,
                context.TargetItemAdapter,
                context.RequestedAmount,
                context.DragContext,
                context.Entry);
            var previewStack = acceptanceRequest.CreatePreviewStack(context.RequestedAmount, context.TargetItemAdapter);
            if (previewStack == null)
                return ShapedPlacementPlanResult.Rejected("Failed to create shaped placement preview");

            var ignoredPlacement = ReferenceEquals(context.TargetInventory, context.Entry.SourceInventory)
                ? context.Entry.SourcePlacement
                : null;
            var placementRequest = new PlacementRequest(
                previewStack,
                anchorIndex,
                context.Orientation,
                context.Footprint);
            if (!targetPlacementInventory.CanPlace(placementRequest, ignoredPlacement))
                return ShapedPlacementPlanResult.Rejected("Target grid cells are not available");

            return ShapedPlacementPlanResult.Planned(new PlannedPlacementAllocation(
                anchorIndex,
                context.Orientation,
                context.Footprint,
                context.RequestedAmount));
        }

        public virtual ShapedPlacementExecutionResult TryExecuteShapedPlacement(ShapedPlacementExecutionContext context)
        {
            if (context.TargetInventory is not IPlacementInventory targetPlacementInventory || !targetPlacementInventory.Grid.HasValue)
                return ShapedPlacementExecutionResult.NotApplicable();

            if (context.TransferStack == null || context.TransferStack.IsEmpty)
                return ShapedPlacementExecutionResult.NotApplicable();

            var allocation = context.Allocation;
            var footprint = allocation.Footprint;
            if (footprint.IsSingleCell || footprint.Equals(default(Footprint)))
                footprint = Footprint.Resolve(context.TransferStack.PrimaryAdapter);
            if (footprint.IsSingleCell)
                return ShapedPlacementExecutionResult.NotApplicable();

            if (context.TransferStack.Count > 1 || context.TransferAmount != context.TransferStack.Count)
                return ShapedPlacementExecutionResult.Failed("Shaped placement requires a single item");

            var placedStack = context.TransferStack.CreateCopy(context.TransferAmount);
            if (placedStack == null || placedStack.IsEmpty)
                return ShapedPlacementExecutionResult.Failed("Failed to copy stack for shaped placement");

            int anchorIndex = allocation.AnchorIndex;
            if (anchorIndex < 0)
                return ShapedPlacementExecutionResult.Failed("Invalid shaped placement anchor");

            var resolvedAnchorSlot = targetPlacementInventory.GetSlot(anchorIndex);
            if (resolvedAnchorSlot == null)
                return ShapedPlacementExecutionResult.Failed("Anchor slot not found");

            bool wasEmpty = resolvedAnchorSlot.IsEmpty;
            var request = new PlacementRequest(placedStack, anchorIndex, allocation.Orientation, footprint);

            if (!targetPlacementInventory.TryPlace(request, out _))
                return ShapedPlacementExecutionResult.Failed("Inventory rejected shaped placement");

            context.TransferStack.RemoveFromStack(placedStack.Count);
            targetPlacementInventory.UpdateAllVisuals();

            return ShapedPlacementExecutionResult.Placed(resolvedAnchorSlot, wasEmpty, placedStack.Count);
        }

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
            if (!Footprint.Resolve(itemAdapter).IsSingleCell)
                return 1;

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

            var stackStore = baseSlot.Inventory as ISlotStackStore;
            if (stackStore == null)
            {
                if (!_warnedMissingStackStoreForMerge)
                {
                    Debug.LogWarning("[InventoryStrategyBase] Cannot merge into slot: inventory does not implement ISlotStackStore.");
                    _warnedMissingStackStoreForMerge = true;
                }

                stack.TryAddToStack(movedStack);
                return false;
            }

            int before = baseSlot.Stack.Count;
            if (!stackStore.TryAddToSlotStack(baseSlot, movedStack))
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

            if (baseSlot.Inventory is ISlotStackStore stackStore &&
                stackStore.TrySplitFromSlot(baseSlot, amount, out var removedStack))
                return removedStack.Count;

            if (baseSlot.Inventory is not ISlotStackStore && !_warnedMissingStackStoreForRemove)
            {
                Debug.LogWarning("[InventoryStrategyBase] Cannot remove from slot: inventory does not implement ISlotStackStore.");
                _warnedMissingStackStoreForRemove = true;
            }

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
