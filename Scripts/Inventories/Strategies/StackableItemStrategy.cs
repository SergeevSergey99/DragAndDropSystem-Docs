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
    public class StackableItemStrategy : StackBasedInventoryStrategyBase
    {
        // Inverted on purpose: default/unset = auto-merge ON. A positive "auto-merge = true" field would
        // deserialize to false on inventories serialized before this field existed ([SerializeReference] ignores
        // C# initializers for managed references), silently turning auto-merge off. See ShapedStacking-Plan.md (C8).
        [SerializeField, Tooltip("OFF (default): dropping a duplicate anywhere auto-merges it into the single " +
            "existing stack of that item. ON: the existing stack accepts more only via an explicit drop directly " +
            "onto it; a duplicate dropped elsewhere is rejected (strict one-per-ID).")]
        private bool _explicitMergeOnly;

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

        // One-per-ID auto-merge: an empty target consolidates into the single existing stack of the
        // same item elsewhere (unless _explicitMergeOnly). The shared scaffold (validation, same-source
        // footprint, explicit merge onto the target, create fallback) lives in the base.
        protected override AdditionalMergeOutcome TryResolveAdditionalMergeCandidate(
            IPlacementGeometry geometry,
            InventoryAcceptanceRequest request,
            BaseSlot targetBaseSlot,
            int maxSize,
            Placement sourcePlacement,
            out PlacementCandidate candidate)
        {
            candidate = default;

            foreach (var placement in geometry.Placements)
            {
                if (placement == null || ReferenceEquals(placement, sourcePlacement) ||
                    placement.Stack == null || !placement.Stack.CanStack(request.ItemAdapter))
                    continue;

                if (_explicitMergeOnly)
                    return AdditionalMergeOutcome.Rejected;

                return TryMergeIntoPlacement(geometry, request, placement, maxSize, out candidate)
                    ? AdditionalMergeOutcome.Resolved
                    : AdditionalMergeOutcome.Rejected;
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
                        return AdditionalMergeOutcome.Rejected;

                    return TryMergeIntoSlotStack(request, slot, maxSize, out candidate)
                        ? AdditionalMergeOutcome.Resolved
                        : AdditionalMergeOutcome.Rejected;
                }
            }

            return AdditionalMergeOutcome.NotApplicable;
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

        public override int GetAcceptableCount(
            IPlacementGeometry geometry,
            InventoryAcceptanceRequest request)
        {
            var item = request?.ItemAdapter;
            var desiredCount = request?.DesiredCount ?? 0;
            if (geometry == null || item == null || desiredCount <= 0)
                return 0;

            var slots = geometry.Slots;
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
            var slotCreation = geometry.Inventory as IInventorySlotCreationCapacity;
            if (slotCreation?.CanCreateNewSlot == true &&
                slotCreation.PotentialNewSlots > 0 &&
                PrefabPassesRules(
                    slots,
                    slotCreation.BaseSlotPrefab,
                    item,
                    Math.Min(desiredCount, maxSize),
                    request))
                return Math.Min(maxSize, desiredCount);

            return 0;
        }

    }
}
