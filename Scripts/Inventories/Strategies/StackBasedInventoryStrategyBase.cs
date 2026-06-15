using System;
using UnityEngine;
using UDND.Core;
using UDND.Slots;
using UDND.Tools.Inspector;

namespace UDND.Inventories
{
    [Serializable]
    public abstract class StackBasedInventoryStrategyBase : InventoryStrategyBase
    {
        /// <summary>
        /// Result of the strategy-specific merge step for an empty target slot
        /// (<see cref="TryResolveAdditionalMergeCandidate"/>).
        /// </summary>
        protected enum AdditionalMergeOutcome
        {
            /// <summary>No applicable merge target; fall through to creating a new placement.</summary>
            NotApplicable,

            /// <summary>A merge candidate was produced.</summary>
            Resolved,

            /// <summary>A merge target exists but cannot accept the drop; reject without creating.</summary>
            Rejected,
        }

        [SerializeField, Tooltip("Maximum stack size. 0 or less = unlimited.")]
        private int _maxStackSize;

        [SerializeField, Tooltip("Allow items to override the stack limit via IStackSizeLimitable.")]
        [ShowIf(nameof(ShowItemStackOverride))]
        private bool _allowItemStackOverride;

        private bool ShowItemStackOverride => _maxStackSize > 0;

        public override void SetMaxStackSize(int maxStackSize, bool allowItemOverride)
        {
            _maxStackSize = maxStackSize;
            _allowItemStackOverride = allowItemOverride;
        }

        public override int GetMaxStackSizeForItem(IItemAdapter itemAdapter)
        {
            if (itemAdapter == null)
                return 0;

            return GetMaxStackSize(itemAdapter, _maxStackSize, _allowItemStackOverride);
        }

        protected int DefaultMaxStackSize => _maxStackSize;
        protected bool AllowItemStackOverride => _allowItemStackOverride;

        /// <summary>
        /// Shared placement resolution for all stack-based strategies. Handles validation,
        /// the same-source footprint case, an explicit drop directly onto an existing
        /// placement/stack, and the create fallback. Strategies differ only in how they treat
        /// an empty target — see <see cref="TryResolveAdditionalMergeCandidate"/>.
        /// </summary>
        public sealed override bool TryGetCandidate(
            IPlacementGeometry geometry,
            InventoryAcceptanceRequest request,
            BaseSlot targetBaseSlot,
            out PlacementCandidate candidate)
        {
            candidate = default;
            if (geometry == null || request == null || targetBaseSlot == null ||
                request.ItemAdapter == null || request.DesiredCount <= 0)
                return false;

            int maxSize = GetMaxStackSize(request.ItemAdapter, DefaultMaxStackSize, AllowItemStackOverride);
            var sourcePlacement = GetSourcePlacement(geometry, request);
            var placement = geometry.GetPlacementAt(targetBaseSlot);

            // When the target slot is covered by our own source placement, the source will be
            // vacated before the new placement lands. Skip all merge paths and fall through to
            // TryCreatePlacementCandidate so the grab-offset anchor is computed correctly
            // (same-inventory move/rotation that keeps the item over its old footprint).
            bool coveredBySource = placement != null && ReferenceEquals(placement, sourcePlacement);

            if (!coveredBySource)
            {
                if (IsSourceSlot(targetBaseSlot, request))
                    return false;

                // Explicit drop directly onto an existing same-item placement: merge into it.
                if (placement != null)
                    return TryMergeIntoPlacement(geometry, request, placement, maxSize, out candidate);

                // Explicit drop onto a non-placement stack in the slot: merge into it.
                if (!targetBaseSlot.IsEmpty)
                    return TryMergeIntoSlotStack(request, targetBaseSlot, maxSize, out candidate);

                // Empty target: strategy-specific behavior (e.g. one-per-ID auto-merge into a
                // stack elsewhere). Separable stacks leave this NotApplicable.
                switch (TryResolveAdditionalMergeCandidate(
                            geometry, request, targetBaseSlot, maxSize, sourcePlacement, out candidate))
                {
                    case AdditionalMergeOutcome.Resolved:
                        return true;
                    case AdditionalMergeOutcome.Rejected:
                        return false;
                }
            }

            int createCapacity = Math.Min(request.DesiredCount, maxSize);
            if (createCapacity <= 0)
                return false;

            return TryCreatePlacementCandidate(
                geometry, request, targetBaseSlot, createCapacity, out candidate);
        }

        /// <summary>
        /// Strategy-specific handling for an empty target with no covering source placement.
        /// The base (separate-stacks) behavior never merges elsewhere, so the target always
        /// becomes a new placement.
        /// </summary>
        protected virtual AdditionalMergeOutcome TryResolveAdditionalMergeCandidate(
            IPlacementGeometry geometry,
            InventoryAcceptanceRequest request,
            BaseSlot targetBaseSlot,
            int maxSize,
            Placement sourcePlacement,
            out PlacementCandidate candidate)
        {
            candidate = default;
            return AdditionalMergeOutcome.NotApplicable;
        }

        /// <summary>Builds a Merge candidate into an existing placement, or fails if it cannot accept the drop.</summary>
        protected bool TryMergeIntoPlacement(
            IPlacementGeometry geometry,
            InventoryAcceptanceRequest request,
            Placement placement,
            int maxSize,
            out PlacementCandidate candidate)
        {
            candidate = default;
            if (placement?.Stack == null || !placement.Stack.CanStack(request.ItemAdapter))
                return false;

            int capacity = Math.Min(request.DesiredCount, Math.Max(0, maxSize - placement.Stack.Count));
            var anchor = geometry.Inventory.GetSlot(placement.AnchorIndex);
            if (capacity <= 0 || anchor == null ||
                !PassesRules(anchor, request.ItemAdapter, capacity, request))
                return false;

            candidate = PlacementCandidate.Merge(placement, anchor, capacity);
            return true;
        }

        /// <summary>Builds a Merge candidate into a non-placement slot stack, or fails if it cannot accept the drop.</summary>
        protected bool TryMergeIntoSlotStack(
            InventoryAcceptanceRequest request,
            BaseSlot slot,
            int maxSize,
            out PlacementCandidate candidate)
        {
            candidate = default;
            if (slot?.Stack == null || !slot.Stack.CanStack(request.ItemAdapter))
                return false;

            int capacity = Math.Min(request.DesiredCount, Math.Max(0, maxSize - slot.Stack.Count));
            if (capacity <= 0 || !PassesRules(slot, request.ItemAdapter, capacity, request))
                return false;

            var entry = request.SourceEntry;
            candidate = PlacementCandidate.Merge(
                slot,
                entry?.Orientation ?? 0,
                entry?.Shape ?? PlacementShapeUtility.Resolve(request.ItemAdapter),
                capacity);
            return true;
        }
    }
}
