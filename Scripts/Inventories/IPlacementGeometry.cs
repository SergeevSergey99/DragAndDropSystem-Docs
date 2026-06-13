using System.Collections.Generic;
using UDND.Core;
using UDND.Slots;

namespace UDND.Inventories
{
    public interface IPlacementGeometry
    {
        IInventory Inventory { get; }
        IReadOnlyList<BaseSlot> Slots { get; }
        IReadOnlyCollection<Placement> Placements { get; }

        Placement GetPlacementAt(BaseSlot slot);
        bool TryResolveAnchor(
            BaseSlot targetSlot,
            InventoryAcceptanceRequest request,
            out BaseSlot anchorSlot);
        bool CanPlace(
            ItemStack previewStack,
            BaseSlot anchorSlot,
            IPlacementShape shape,
            int orientation,
            Placement ignoredPlacement = null);
        IReadOnlyList<BaseSlot> GetCoveredSlots(
            BaseSlot anchorSlot,
            IPlacementShape shape,
            int orientation);
    }
}
