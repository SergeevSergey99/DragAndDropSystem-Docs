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
    public abstract class InventoryStrategyBase : IStrategy
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
        public abstract int GetAcceptableCount(
            IPlacementGeometry geometry,
            InventoryAcceptanceRequest request);

        public abstract bool TryGetCandidate(
            IPlacementGeometry geometry,
            InventoryAcceptanceRequest request,
            BaseSlot targetBaseSlot,
            out PlacementCandidate candidate);

        public virtual PlacementCandidateSource GetCandidates(
            IPlacementGeometry geometry,
            InventoryAcceptanceRequest request)
        {
            return new PlacementCandidateSource(
                () => EnumerateCandidates(geometry, request));
        }

        private IEnumerable<PlacementCandidate> EnumerateCandidates(
            IPlacementGeometry geometry,
            InventoryAcceptanceRequest request)
        {
            if (geometry == null || request == null)
                yield break;

            var seenPlacements = new HashSet<Placement>();
            var seenAnchors = new HashSet<BaseSlot>();
            for (int i = 0; i < geometry.Slots.Count; i++)
            {
                var target = geometry.Slots[i];
                if (target == null ||
                    !TryGetCandidate(geometry, request, target, out var candidate))
                    continue;

                // Auto-enumeration: skip Create candidates whose resolved anchor lands on a cell
                // covered by the drag source placement. This prevents suggesting positions that
                // are trivially displaced (e.g. anchor stays on a source cell due to grab offset).
                // Explicit drops bypass this via direct TryGetCandidate calls, so they're unaffected.
                if (candidate.Kind == PlacementCandidateKind.Create &&
                    IsSourceSlot(candidate.Anchor, request))
                    continue;

                if (candidate.Kind == PlacementCandidateKind.Merge)
                {
                    if (candidate.TargetPlacement != null)
                    {
                        if (!seenPlacements.Add(candidate.TargetPlacement))
                            continue;
                    }
                    else if (candidate.Anchor == null || !seenAnchors.Add(candidate.Anchor))
                    {
                        continue;
                    }
                }
                else if (candidate.Anchor == null || !seenAnchors.Add(candidate.Anchor))
                {
                    continue;
                }

                yield return candidate;
            }

            var slotCreation = geometry.Inventory as IInventorySlotCreationCapacity;
            int dynamicCapacity = Math.Min(
                request.DesiredCount,
                GetMaxStackSizeForItem(request.ItemAdapter));
            if (slotCreation?.CanCreateNewSlot != true ||
                slotCreation.PotentialNewSlots <= 0 ||
                dynamicCapacity <= 0 ||
                !CanCreateDynamicCandidate(geometry, request) ||
                !PrefabPassesRules(
                    geometry.Slots,
                    slotCreation.BaseSlotPrefab,
                    request.ItemAdapter,
                    dynamicCapacity,
                    request))
                yield break;

            var entry = request.SourceEntry;
            var shape = entry?.Shape ?? PlacementShapeUtility.Resolve(request.ItemAdapter);
            var orientation = entry?.Orientation ?? 0;
            yield return PlacementCandidate.NewDynamicSlot(
                orientation,
                shape,
                dynamicCapacity);
        }

        protected virtual bool CanCreateDynamicCandidate(
            IPlacementGeometry geometry,
            InventoryAcceptanceRequest request)
        {
            return true;
        }

        protected static Placement GetSourcePlacement(
            IPlacementGeometry geometry,
            InventoryAcceptanceRequest request)
        {
            if (geometry == null || request?.SourceEntry is not DragEntry entry ||
                !ReferenceEquals(entry.SourceInventory, geometry.Inventory))
                return null;

            return entry.SourcePlacement ?? geometry.GetPlacementAt(entry.SourceBaseSlot);
        }

        protected bool TryCreatePlacementCandidate(
            IPlacementGeometry geometry,
            InventoryAcceptanceRequest request,
            BaseSlot targetBaseSlot,
            int capacity,
            out PlacementCandidate candidate)
        {
            candidate = default;
            if (geometry == null || request == null || targetBaseSlot == null || capacity <= 0 ||
                !geometry.TryResolveAnchor(targetBaseSlot, request, out var anchor))
                return false;

            var entry = request.SourceEntry;
            var shape = entry?.Shape ?? PlacementShapeUtility.Resolve(request.ItemAdapter);
            var orientation = entry?.Orientation ?? 0;
            var previewStack = request.CreatePreviewStack(capacity);
            var sourcePlacement = GetSourcePlacement(geometry, request);
            bool movesWholeRemainingStack =
                request.SourceBaseSlot?.Stack != null &&
                capacity >= request.SourceBaseSlot.Stack.Count;
            if (previewStack == null ||
                !PassesRules(anchor, request.ItemAdapter, capacity, request) ||
                !geometry.CanPlace(
                    previewStack,
                    anchor,
                    shape,
                    orientation,
                    movesWholeRemainingStack ? sourcePlacement : null))
                return false;

            candidate = PlacementCandidate.Create(anchor, orientation, shape, capacity);
            return true;
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

        protected bool PrefabPassesRules(IReadOnlyList<BaseSlot> slots, BaseSlot baseSlotPrefab, IItemAdapter itemAdapter, int previewCount, InventoryAcceptanceRequest request)
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

        internal string CaptureConfigurationJson()
        {
            return JsonUtility.ToJson(this);
        }
    }
}
