using System;
using System.Collections.Generic;
using UnityEngine;
using UDND.Core;
using UDND.Slots;

namespace UDND.Inventories
{
    public sealed class DropPreviewController
    {
        private static readonly IReadOnlyList<BaseSlot> EmptySlots = Array.Empty<BaseSlot>();

        private readonly IPlacementInventory _inventory;
        private readonly IShapedDragTargetResolver _anchorResolver;
        private readonly Func<PlacementStore> _getPlacementStore;
        private readonly InventoryTransferService _transferService = new InventoryTransferService();
        private readonly List<BaseSlot> _highlightedSlots = new List<BaseSlot>();

        /// <summary>
        /// Verdict of the preview currently on screen. Lives exactly as long as the highlight it
        /// belongs to, so feedback visuals can read it from <c>Highlight</c> instead of keeping
        /// their own copy and having to invalidate it.
        /// </summary>
        private DropVerdict _activeVerdict;
        private bool _hasActiveVerdict;

        public DropPreviewController(
            IPlacementInventory inventory,
            IShapedDragTargetResolver anchorResolver,
            Func<PlacementStore> getPlacementStore)
        {
            _inventory = inventory ?? throw new ArgumentNullException(nameof(inventory));
            _anchorResolver = anchorResolver ?? throw new ArgumentNullException(nameof(anchorResolver));
            _getPlacementStore = getPlacementStore ?? throw new ArgumentNullException(nameof(getPlacementStore));
        }

        public bool TryGetDropPreviewSlots(
            BaseSlot targetBaseSlot,
            DragContext context,
            out IReadOnlyList<BaseSlot> previewSlots,
            out bool canPlace)
        {
            bool resolved = TryResolvePreview(
                targetBaseSlot,
                context,
                probe: null,
                out previewSlots,
                out var verdict);

            canPlace = verdict.CanPlace;
            return resolved;
        }

        /// <summary>
        /// Resolves the footprint to highlight and the verdict to display.
        /// <para>
        /// <paramref name="probe"/> is the result the drop processor already computed for this
        /// hover. Passing it keeps preview and drop on one probe with one policy; omitting it makes
        /// the controller run its own with default policy resolution, which is correct only when no
        /// processor is involved (editor tooling, tests).
        /// </para>
        /// </summary>
        public bool TryResolvePreview(
            BaseSlot targetBaseSlot,
            DragContext context,
            TransferProbe probe,
            out IReadOnlyList<BaseSlot> previewSlots,
            out DropVerdict verdict)
        {
            previewSlots = EmptySlots;
            verdict = default;

            if (targetBaseSlot == null ||
                !ReferenceEquals(targetBaseSlot.Inventory, _inventory) ||
                context == null ||
                context.Entries == null ||
                context.Entries.Count == 0)
                return false;

            var entry = context.Entries[0];
            if (entry.Stack == null || entry.Stack.IsEmpty || entry.Stack.PrimaryAdapter == null)
                return false;

            probe ??= _transferService.Probe(context, _inventory, targetBaseSlot, ResolveFallbackPolicy(context));

            if (probe.CanAttempt && probe.IsExplicitTargetCandidate)
            {
                previewSlots = probe.CoveredSlots;
                verdict = DropVerdict.Accepted();
                return true;
            }

            // A probe that succeeded without claiming this slot means the drop would land somewhere
            // else in this inventory. The feedback is still a refusal — "not here" — but it carries
            // a different reason than an outright rejection.
            verdict = DropVerdict.Rejected(
                probe.CanAttempt
                    ? "The drop would land on a different slot"
                    : probe.FailureReason);

            // Rejected drops still render the in-bounds portion of their footprint, so the player
            // sees where the item would have gone and why it did not.
            if (!TransferItemConversionUtility.TryResolveTargetItem(
                    entry.SourceInventory,
                    _inventory,
                    entry.Stack.PrimaryAdapter,
                    context.ConversionSession,
                    out var targetItem))
            {
                // The item cannot exist in this inventory at all. That is a verdict, not a lack of
                // one: highlight the hovered slot so the refusal is visible instead of silent.
                previewSlots = new[] { targetBaseSlot };
                return true;
            }

            var shape = PlacementShapeUtility.Resolve(targetItem);
            var offsets = _inventory.Topology.GetPlacementOffsets(shape, entry.Orientation);
            if (offsets == null || offsets.Count <= 1)
            {
                previewSlots = new[] { targetBaseSlot };
                return true;
            }

            if (!_anchorResolver.TryResolveShapedPlacementAnchorCell(
                    targetBaseSlot,
                    context,
                    entry,
                    shape,
                    targetItem,
                    out var anchorCell))
            {
                previewSlots = new[] { targetBaseSlot };
                return true;
            }

            var coveredIndices = GetPreviewCoveredCells(anchorCell, shape, entry.Orientation);
            if (coveredIndices == null || coveredIndices.Count == 0)
            {
                previewSlots = new[] { targetBaseSlot };
                return true;
            }

            var slots = new List<BaseSlot>(coveredIndices.Count);
            for (int i = 0; i < coveredIndices.Count; i++)
            {
                var slot = _inventory.GetSlot(coveredIndices[i]);
                if (slot != null)
                    slots.Add(slot);
            }

            previewSlots = slots;

            return true;
        }

        public bool ShowDropPreview(BaseSlot targetBaseSlot, DragContext context)
            => ShowDropPreview(targetBaseSlot, context, null);

        public bool ShowDropPreview(BaseSlot targetBaseSlot, DragContext context, TransferProbe probe)
        {
            ClearDropPreview();

            if (!TryResolvePreview(targetBaseSlot, context, probe, out var previewSlots, out var verdict) ||
                previewSlots == null ||
                previewSlots.Count == 0)
                return false;

            // Publish the verdict and the covered set before highlighting: a slot that renders
            // drop feedback reads them back from inside Highlight.
            _activeVerdict = verdict;
            _hasActiveVerdict = true;
            for (int i = 0; i < previewSlots.Count; i++)
            {
                var slot = previewSlots[i];
                if (slot != null)
                    _highlightedSlots.Add(slot);
            }

            for (int i = 0; i < _highlightedSlots.Count; i++)
                _highlightedSlots[i].Highlight(true);

            return _highlightedSlots.Count > 0;
        }

        /// <summary>
        /// Verdict for a slot of the preview currently on screen, if it is part of one.
        /// Returns false for any other slot: no preview means no opinion, which is not the same
        /// as a refusal and must not be rendered as one.
        /// </summary>
        public bool TryGetActiveDropVerdict(BaseSlot baseSlot, out DropVerdict verdict)
        {
            verdict = default;
            if (!_hasActiveVerdict || baseSlot == null)
                return false;

            for (int i = 0; i < _highlightedSlots.Count; i++)
            {
                if (!ReferenceEquals(_highlightedSlots[i], baseSlot))
                    continue;

                verdict = _activeVerdict;
                return true;
            }

            return false;
        }

        public void ClearDropPreview()
        {
            _hasActiveVerdict = false;

            for (int i = 0; i < _highlightedSlots.Count; i++)
                _highlightedSlots[i]?.Highlight(false);

            _highlightedSlots.Clear();
        }

        /// <summary>
        /// Policy used when no processor supplied a probe. It cannot see a processor's bound
        /// override, which is exactly why callers that have a processor must pass its probe.
        /// </summary>
        private ResolvedDropPolicy ResolveFallbackPolicy(DragContext context)
        {
            return _inventory is IDropPolicyProvider policyProvider
                ? policyProvider.ResolveDropPolicy(null, context)
                : new ResolvedDropPolicy(
                    BlockedTargetResolutionKind.FindAlternative,
                    null,
                    true,
                    PartialTransferMode.Allow);
        }

        private IReadOnlyList<int> GetPreviewCoveredCells(
            Vector2Int anchorCell,
            IPlacementShape shape,
            int orientation)
        {
            return _getPlacementStore().GetCoveredIndices(
                anchorCell,
                shape,
                orientation,
                PlacementBoundsMode.IncludeOnlyInBounds);
        }
    }
}
