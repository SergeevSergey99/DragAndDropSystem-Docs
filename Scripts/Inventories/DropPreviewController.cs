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
        private readonly List<BaseSlot> _highlightedSlots = new List<BaseSlot>();

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
            previewSlots = EmptySlots;
            canPlace = false;

            if (targetBaseSlot == null ||
                !ReferenceEquals(targetBaseSlot.Inventory, _inventory) ||
                context == null ||
                context.Entries == null ||
                context.Entries.Count == 0)
                return false;

            var entry = context.Entries[0];
            if (entry.Stack == null || entry.Stack.IsEmpty || entry.Stack.PrimaryAdapter == null)
                return false;

            if (!TransferItemConversionUtility.TryResolveTargetItem(
                    entry.SourceInventory,
                    _inventory,
                    entry.Stack.PrimaryAdapter,
                    out var targetItem))
                return false;

            var shape = PlacementShapeUtility.Resolve(targetItem);
            if (!_inventory.Grid.HasValue || PlacementShapeUtility.IsSingleCell(shape, entry.Orientation))
            {
                previewSlots = new[] { targetBaseSlot };
                canPlace = true;
                return true;
            }

            if (!_anchorResolver.TryResolveShapedPlacementAnchorCell(
                    targetBaseSlot,
                    context,
                    entry,
                    shape,
                    targetItem,
                    out var anchorCell))
                return true;

            bool hasValidAnchor = _inventory.TryGetIndexForCell(anchorCell, out int anchorIndex);
            var coveredIndices = GetPreviewCoveredCells(anchorCell, shape, entry.Orientation);
            if (coveredIndices == null || coveredIndices.Count == 0)
                return true;

            var slots = new List<BaseSlot>(coveredIndices.Count);
            for (int i = 0; i < coveredIndices.Count; i++)
            {
                var slot = _inventory.GetSlot(coveredIndices[i]);
                if (slot != null)
                    slots.Add(slot);
            }

            previewSlots = slots;

            var acceptanceRequest = new InventoryAcceptanceRequest(
                _inventory,
                targetItem,
                entry.Stack.Count,
                context,
                entry);
            var previewStack = acceptanceRequest.CreatePreviewStack(entry.Stack.Count, targetItem);
            if (previewStack == null)
                return true;

            if (!hasValidAnchor)
                return true;

            var ignoredPlacement = ReferenceEquals(entry.SourceInventory, _inventory)
                ? entry.SourcePlacement
                : null;
            canPlace = _inventory.CanPlace(
                new PlacementRequest(previewStack, anchorIndex, entry.Orientation, shape),
                ignoredPlacement);
            return true;
        }

        public bool ShowDropPreview(BaseSlot targetBaseSlot, DragContext context)
        {
            ClearDropPreview();

            if (!TryGetDropPreviewSlots(targetBaseSlot, context, out var previewSlots, out _) ||
                previewSlots == null ||
                previewSlots.Count == 0)
                return false;

            for (int i = 0; i < previewSlots.Count; i++)
            {
                var slot = previewSlots[i];
                if (slot == null)
                    continue;

                slot.Highlight(true);
                _highlightedSlots.Add(slot);
            }

            return _highlightedSlots.Count > 0;
        }

        public void ClearDropPreview()
        {
            for (int i = 0; i < _highlightedSlots.Count; i++)
                _highlightedSlots[i]?.Highlight(false);

            _highlightedSlots.Clear();
        }

        private IReadOnlyList<int> GetPreviewCoveredCells(
            Vector2Int anchorCell,
            IPlacementShape shape,
            PlacementOrientation orientation)
        {
            return _getPlacementStore().GetCoveredIndices(
                anchorCell,
                shape,
                orientation,
                PlacementBoundsMode.IncludeOnlyInBounds);
        }
    }
}
