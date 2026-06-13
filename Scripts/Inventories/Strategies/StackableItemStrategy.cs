using System;
using System.Collections.Generic;
using UnityEngine;
using UDND.Core;
using UDND.Slots;

namespace UDND.Inventories
{
    /// <summary>
    /// Strategy: items are stackable (grouped by type), one logical stack location per item ID (one-per-ID).
    /// A logical location is a slot for normal inventories and a placement for shaped inventories; its stack may
    /// hold more than one item (count &gt; 1), capped by the strategy / per-item limit — including shaped placements.
    /// <para>
    /// Auto-merge (default): a duplicate dropped anywhere (area-drop, auto-transfer, an empty slot, or a free grid
    /// region) consolidates into the single existing stack. Explicit-merge-only (<c>_explicitMergeOnly</c>): the
    /// stack grows only on an explicit drop directly onto it; a duplicate dropped elsewhere is rejected (no second
    /// stack of the same item).
    /// </para>
    /// For multiple separate stacks of the same item use <see cref="SeparableStacksStrategy"/>.
    /// Supports the strategy default limit and, when allowItemOverride = true, per-item limits via IStackSizeLimitable.
    /// </summary>
    [Serializable]
    public class StackableItemStrategy : StackBasedInventoryStrategyBase, IStackBasedInventoryStrategy
    {
        // Inverted on purpose: default/unset = auto-merge ON. A positive "auto-merge = true" field would
        // deserialize to false on inventories serialized before this field existed ([SerializeReference] ignores
        // C# initializers for managed references), silently turning auto-merge off. See ShapedStacking-Plan.md (C8).
        [SerializeField, Tooltip("OFF (default): dropping a duplicate anywhere auto-merges it into the single " +
            "existing stack of that item. ON: the existing stack accepts more only via an explicit drop directly " +
            "onto it; a duplicate dropped elsewhere is rejected (strict one-per-ID).")]
        private bool _explicitMergeOnly;

        // Shaped merge policy lives entirely inside the strategy (one-per-ID; auto-merge unless explicit-only).
        public override ShapedMergeDecision ResolveShapedMerge(
            IPlacementInventory inventory, IItemAdapter item, int anchorIndex,
            IPlacementShape shape, PlacementOrientation orientation, Placement sourcePlacement)
        {
            if (inventory == null || item == null)
                return ShapedMergeDecision.CreateNew;

            // one-per-ID: at most one placement of this item exists (the drag source is excluded so a
            // same-inventory relocation still creates the moved placement).
            var existing = FindMergeableShapedPlacement(inventory, item, sourcePlacement);
            if (existing == null)
                return ShapedMergeDecision.CreateNew;

            if (!_explicitMergeOnly)
                return ShapedMergeDecision.Merge(existing); // auto-consolidate wherever it was dropped

            // explicit-only: merge only when the dropped footprint overlaps the existing placement;
            // a duplicate dropped elsewhere is rejected (no second placement of the same item).
            return FindOverlappedShapedPlacement(inventory, item, anchorIndex, shape, orientation, sourcePlacement) != null
                ? ShapedMergeDecision.Merge(existing)
                : ShapedMergeDecision.Reject;
        }

        public override PlacementCandidateSource GetCandidates(
            IPlacementGeometry geometry,
            InventoryAcceptanceRequest request)
        {
            // Explicit-merge-only: automatic enumeration must not offer merge candidates.
            // The existing stack only accepts more when the user explicitly drops onto it.
            if (_explicitMergeOnly && HasMatchingStackInGeometry(geometry, request))
                return new PlacementCandidateSource(() => System.Array.Empty<PlacementCandidate>());
            return base.GetCandidates(geometry, request);
        }

        private static bool HasMatchingStackInGeometry(IPlacementGeometry geometry, InventoryAcceptanceRequest request)
        {
            if (geometry == null || request?.ItemAdapter == null)
                return false;

            var sourcePlacement = GetSourcePlacement(geometry, request);
            foreach (var placement in geometry.Placements)
            {
                if (placement != null && !ReferenceEquals(placement, sourcePlacement) &&
                    placement.Stack != null && placement.Stack.CanStack(request.ItemAdapter))
                    return true;
            }

            if (geometry.Placements.Count == 0)
            {
                foreach (var slot in geometry.Slots)
                {
                    if (slot != null && !IsSourceSlot(slot, request) &&
                        !slot.IsEmpty && slot.Stack != null &&
                        slot.Stack.CanStack(request.ItemAdapter))
                        return true;
                }
            }

            return false;
        }

        public override bool TryGetCandidate(
            IPlacementGeometry geometry,
            InventoryAcceptanceRequest request,
            BaseSlot targetBaseSlot,
            out PlacementCandidate candidate)
        {
            candidate = default;
            if (geometry == null || request == null || targetBaseSlot == null ||
                request.ItemAdapter == null || request.DesiredCount <= 0)
                return false;

            int maxSize = GetMaxStackSize(
                request.ItemAdapter,
                DefaultMaxStackSize,
                AllowItemStackOverride);

            var sourcePlacement = GetSourcePlacement(geometry, request);
            var explicitPlacement = geometry.GetPlacementAt(targetBaseSlot);

            // When the target slot is covered by our own source placement, the source will be
            // vacated before the new placement lands. Skip all merge paths and fall through to
            // TryCreatePlacementCandidate so the grab-offset anchor is computed correctly
            // (e.g. same-inventory rotation that keeps the item partially over its old footprint).
            bool coveredBySource = explicitPlacement != null &&
                ReferenceEquals(explicitPlacement, sourcePlacement);

            if (!coveredBySource)
            {
                if (IsSourceSlot(targetBaseSlot, request))
                    return false;

                if (explicitPlacement != null)
                {
                    if (ReferenceEquals(explicitPlacement, sourcePlacement) ||
                        explicitPlacement.Stack == null ||
                        !explicitPlacement.Stack.CanStack(request.ItemAdapter))
                        return false;

                    int capacity = Math.Min(
                        request.DesiredCount,
                        Math.Max(0, maxSize - explicitPlacement.Stack.Count));
                    var anchor = geometry.Inventory.GetSlot(explicitPlacement.AnchorIndex);
                    if (capacity <= 0 || anchor == null ||
                        !PassesRules(anchor, request.ItemAdapter, capacity, request))
                        return false;

                    candidate = PlacementCandidate.Merge(explicitPlacement, anchor, capacity);
                    return true;
                }

                if (!targetBaseSlot.IsEmpty)
                {
                    if (targetBaseSlot.Stack == null ||
                        !targetBaseSlot.Stack.CanStack(request.ItemAdapter))
                        return false;

                    int capacity = Math.Min(
                        request.DesiredCount,
                        Math.Max(0, maxSize - targetBaseSlot.Stack.Count));
                    if (capacity <= 0 ||
                        !PassesRules(targetBaseSlot, request.ItemAdapter, capacity, request))
                        return false;

                    var entry = request.SourceEntry;
                    candidate = PlacementCandidate.Merge(
                        targetBaseSlot,
                        entry?.Orientation ?? PlacementOrientation.Rot0,
                        entry?.Shape ?? PlacementShapeUtility.Resolve(request.ItemAdapter),
                        capacity);
                    return true;
                }

                foreach (var placement in geometry.Placements)
                {
                    if (placement == null || ReferenceEquals(placement, sourcePlacement) ||
                        placement.Stack == null || !placement.Stack.CanStack(request.ItemAdapter))
                        continue;

                    if (_explicitMergeOnly)
                        return false;

                    int capacity = Math.Min(
                        request.DesiredCount,
                        Math.Max(0, maxSize - placement.Stack.Count));
                    var anchor = geometry.Inventory.GetSlot(placement.AnchorIndex);
                    if (capacity <= 0 || anchor == null ||
                        !PassesRules(anchor, request.ItemAdapter, capacity, request))
                        return false;

                    candidate = PlacementCandidate.Merge(placement, anchor, capacity);
                    return true;
                }

                if (geometry.Placements.Count == 0)
                {
                    for (int i = 0; i < geometry.Slots.Count; i++)
                    {
                        var slot = geometry.Slots[i];
                        if (slot == null || ReferenceEquals(slot, targetBaseSlot) ||
                            IsSourceSlot(slot, request) || slot.IsEmpty ||
                            slot.Stack == null || !slot.Stack.CanStack(request.ItemAdapter))
                            continue;

                        if (_explicitMergeOnly)
                            return false;

                        int capacity = Math.Min(
                            request.DesiredCount,
                            Math.Max(0, maxSize - slot.Stack.Count));
                        if (capacity <= 0 ||
                            !PassesRules(slot, request.ItemAdapter, capacity, request))
                            return false;

                        var entry = request.SourceEntry;
                        candidate = PlacementCandidate.Merge(
                            slot,
                            entry?.Orientation ?? PlacementOrientation.Rot0,
                            entry?.Shape ?? PlacementShapeUtility.Resolve(request.ItemAdapter),
                            capacity);
                        return true;
                    }
                }
            }

            int emptyCapacity = Math.Min(request.DesiredCount, maxSize);
            if (emptyCapacity <= 0)
                return false;

            return TryCreatePlacementCandidate(
                geometry,
                request,
                targetBaseSlot,
                emptyCapacity,
                out candidate);
        }

        protected override bool CanCreateDynamicCandidate(
            IPlacementGeometry geometry,
            InventoryAcceptanceRequest request)
        {
            if (geometry == null || request?.ItemAdapter == null)
                return false;

            var sourcePlacement = GetSourcePlacement(geometry, request);
            foreach (var placement in geometry.Placements)
            {
                if (placement != null &&
                    !ReferenceEquals(placement, sourcePlacement) &&
                    placement.Stack != null &&
                    placement.Stack.CanStack(request.ItemAdapter))
                    return false;
            }

            if (geometry.Placements.Count != 0)
                return true;

            for (int i = 0; i < geometry.Slots.Count; i++)
            {
                var slot = geometry.Slots[i];
                if (slot != null &&
                    !IsSourceSlot(slot, request) &&
                    !slot.IsEmpty &&
                    slot.Stack != null &&
                    slot.Stack.CanStack(request.ItemAdapter))
                    return false;
            }

            return true;
        }

        public override int GetAcceptableCount(List<BaseSlot> slots, InventoryAcceptanceRequest request, bool canCreateNewSlot, int potentialNewSlots, BaseSlot baseSlotPrefab)
        {
            var item = request?.ItemAdapter;
            var desiredCount = request?.DesiredCount ?? 0;
            if (item == null || desiredCount <= 0)
                return 0;

            int maxSize = GetMaxStackSize(item, DefaultMaxStackSize, AllowItemStackOverride);

            // one-per-ID: if item already exists, return only that logical location's remaining capacity.
            // The source logical location is excluded because a same-inventory move frees it.
            HashSet<Placement> seenPlacements = null;
            foreach (var slot in slots)
            {
                if (IsSourceSlot(slot, request)) continue;
                if (ShouldSkipDuplicatePlacementLocation(slot, ref seenPlacements)) continue;
                if (!slot.IsEmpty && slot.Stack.CanStack(item))
                {
                    int canFit = Math.Max(0, maxSize - slot.Stack.Count);
                    if (canFit > 0 && PassesRules(slot, item, Math.Min(desiredCount, canFit), request))
                        return Math.Min(canFit, desiredCount);
                    return 0;
                }
            }

            // item absent: one empty slot
            foreach (var slot in slots)
            {
                if (IsSourceSlot(slot, request)) continue;
                if (slot.IsEmpty && PassesRules(slot, item, Math.Min(desiredCount, maxSize), request))
                    return Math.Min(maxSize, desiredCount);
            }

            // no existing or empty slot: new slot
            if (canCreateNewSlot && potentialNewSlots > 0 &&
                PrefabPassesRules(slots, baseSlotPrefab, item, Math.Min(desiredCount, maxSize), request))
                return Math.Min(maxSize, desiredCount);

            return 0;
        }

    }
}
