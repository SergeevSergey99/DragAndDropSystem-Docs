using System;
using System.Collections.Generic;
using UDND.Core;
using UDND.Slots;

namespace UDND.Inventories
{
    public sealed class InventoryPlacementGeometry : IPlacementGeometry
    {
        public InventoryPlacementGeometry(IInventory inventory)
        {
            Inventory = inventory ?? throw new ArgumentNullException(nameof(inventory));
            _placementInventory = inventory as IPlacementInventory;
        }

        private readonly IPlacementInventory _placementInventory;

        public IInventory Inventory { get; }
        public IReadOnlyList<BaseSlot> Slots => Inventory.Slots;
        public IReadOnlyCollection<Placement> Placements =>
            _placementInventory?.Placements ?? Array.Empty<Placement>();

        public Placement GetPlacementAt(BaseSlot slot)
            => _placementInventory?.GetPlacementAt(slot);

        public bool TryResolveAnchor(
            BaseSlot targetSlot,
            InventoryAcceptanceRequest request,
            out BaseSlot anchorSlot)
        {
            anchorSlot = null;
            if (targetSlot == null || !ReferenceEquals(targetSlot.Inventory, Inventory))
                return false;

            if (request?.SourceEntry is DragEntry entry &&
                _placementInventory is IShapedDragTargetResolver resolver &&
                !PlacementShapeUtility.IsSingleCell(
                    entry.Shape,
                    entry.Orientation,
                    _placementInventory.Topology))
            {
                if (!resolver.TryResolveShapedPlacementAnchor(
                        targetSlot,
                        request.Context,
                        entry,
                        entry.Shape,
                        request.ItemAdapter,
                        out _,
                        out int anchorIndex))
                    return false;

                anchorSlot = _placementInventory.GetSlot(anchorIndex);
                return anchorSlot != null;
            }

            anchorSlot = targetSlot;
            return true;
        }

        public bool CanPlace(
            ItemStack previewStack,
            BaseSlot anchorSlot,
            IPlacementShape shape,
            int orientation,
            Placement ignoredPlacement = null)
        {
            if (previewStack == null || previewStack.IsEmpty || anchorSlot == null)
                return false;

            if (_placementInventory != null)
            {
                return _placementInventory.CanPlace(
                    new PlacementRequest(previewStack, anchorSlot.Index, orientation, shape),
                    ignoredPlacement);
            }

            return PlacementShapeUtility.IsSingleCell(
                       shape,
                       orientation,
                       new SlotTopology(Inventory.SlotCount)) &&
                   (anchorSlot.IsEmpty || ReferenceEquals(anchorSlot, ignoredPlacement));
        }

        public IReadOnlyList<BaseSlot> GetCoveredSlots(
            BaseSlot anchorSlot,
            IPlacementShape shape,
            int orientation)
        {
            if (anchorSlot == null)
                return Array.Empty<BaseSlot>();

            if (_placementInventory == null)
            {
                return PlacementShapeUtility.IsSingleCell(
                        shape,
                        orientation,
                        new SlotTopology(Inventory.SlotCount))
                    ? new[] { anchorSlot }
                    : Array.Empty<BaseSlot>();
            }

            var indices = _placementInventory.GetCoveredCells(anchorSlot.Index, shape, orientation);
            if (indices == null || indices.Count == 0)
                return Array.Empty<BaseSlot>();

            var result = new List<BaseSlot>(indices.Count);
            for (int i = 0; i < indices.Count; i++)
            {
                var slot = _placementInventory.GetSlot(indices[i]);
                if (slot == null)
                    return Array.Empty<BaseSlot>();
                result.Add(slot);
            }

            return result;
        }
    }
}
