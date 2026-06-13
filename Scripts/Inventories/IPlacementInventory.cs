using System.Collections.Generic;
using UnityEngine;
using UDND.Core;
using UDND.Slots;

namespace UDND.Inventories
{
    /// <summary>
    /// Optional inventory contract for topology-aware placement support.
    /// Code that needs placement semantics should depend on this interface instead of a concrete inventory component.
    /// </summary>
    public interface IPlacementInventory : IInventory
    {
        IInventoryTopology Topology { get; }
        IStrategy Strategy { get; }
        IReadOnlyCollection<Placement> Placements { get; }

        Placement GetPlacementAt(BaseSlot baseSlot);
        Placement GetPlacementAt(int cellIndex);
        IReadOnlyList<int> GetCoveredCells(
            int anchorIndex,
            IPlacementShape shape,
            int orientation = 0);

        bool CanPlace(
            PlacementRequest request,
            Placement ignoredA = null,
            Placement ignoredB = null);
        bool TryPlace(PlacementRequest request, out Placement placement);
        bool RemovePlacement(Placement placement);
    }

    /// <summary>
    /// Optional target-side contract for resolving shaped drag/drop anchors.
    /// Kept separate from <see cref="IPlacementInventory"/> so placement storage implementations
    /// do not have to know about DragContext/DragEntry unless they participate in UI drag targeting.
    /// </summary>
    public interface IShapedDragTargetResolver
    {
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
