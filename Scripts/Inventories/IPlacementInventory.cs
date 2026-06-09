using System.Collections.Generic;
using UnityEngine;
using UDND.Core;
using UDND.Slots;

namespace UDND.Inventories
{
    /// <summary>
    /// Optional inventory contract for grid/shaped placement support.
    /// Code that needs placement semantics should depend on this interface instead of a concrete inventory component.
    /// </summary>
    public interface IPlacementInventory : IInventory
    {
        GridTopology? Grid { get; }
        IPlacementStrategy PlacementStrategy { get; }
        IReadOnlyCollection<Placement> Placements { get; }

        Placement GetPlacementAt(BaseSlot baseSlot);
        Placement GetPlacementAt(int cellIndex);
        IReadOnlyList<int> GetCoveredCells(
            int anchorIndex,
            IPlacementShape shape,
            PlacementOrientation orientation = PlacementOrientation.Rot0);

        Vector2Int GetCellForIndex(int index);
        bool TryGetIndexForCell(Vector2Int cell, out int index);

        bool CanPlace(PlacementRequest request);
        bool CanPlace(PlacementRequest request, Placement ignoredPlacement);
        bool CanPlace(PlacementRequest request, Placement ignoredA, Placement ignoredB);
        bool TryPlace(PlacementRequest request);
        bool TryPlace(PlacementRequest request, out Placement placement);
        bool RemovePlacement(Placement placement);
        bool RemovePlacementAt(BaseSlot baseSlot);
        bool RemovePlacementAt(int cellIndex);
    }

    /// <summary>
    /// Optional target-side contract for resolving shaped drag/drop anchors.
    /// Kept separate from <see cref="IPlacementInventory"/> so placement storage implementations
    /// do not have to know about DragContext/DragEntry unless they participate in UI drag targeting.
    /// </summary>
    public interface IShapedDragTargetResolver
    {
        IShapedPlacementAnchorStrategy ShapedPlacementAnchorStrategy { get; }

        bool TryResolveShapedPlacementAnchorCell(
            BaseSlot targetBaseSlot,
            DragContext context,
            DragEntry entry,
            IPlacementShape shape,
            IItemAdapter targetItemAdapter,
            out Vector2Int anchorCell);

        bool TryResolveShapedPlacementAnchor(
            BaseSlot targetBaseSlot,
            DragContext context,
            DragEntry entry,
            IPlacementShape shape,
            IItemAdapter targetItemAdapter,
            out Vector2Int anchorCell,
            out int anchorIndex);
    }
}
