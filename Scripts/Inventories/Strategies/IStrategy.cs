using System.Collections.Generic;
using UDND.Core;
using UDND.Slots;

namespace UDND.Inventories
{
    /// <summary>
    /// Read-only inventory behavior strategy. Produces placement candidates and resolves
    /// drag amounts; all mutation is performed by the caller through narrow inventory primitives.
    /// </summary>
    public interface IStrategy
    {
        bool TryGetCandidate(
            IPlacementGeometry geometry,
            InventoryAcceptanceRequest request,
            BaseSlot targetBaseSlot,
            out PlacementCandidate candidate);

        PlacementCandidateSource GetCandidates(
            IPlacementGeometry geometry,
            InventoryAcceptanceRequest request);
        
        /// <summary>
        /// Set the stack limit at runtime.
        /// maxStackSize = 0 means unlimited.
        /// </summary>
        void SetMaxStackSize(int maxStackSize, bool allowItemOverride);
        
        int GetMaxStackSizeForItem(IItemAdapter itemAdapter);
        
        int GetAcceptableCount(List<BaseSlot> slots, InventoryAcceptanceRequest request, bool canCreateNewSlot, int potentialNewSlots, BaseSlot baseSlotPrefab);

        /// <summary>
        /// Decides whether a shaped drop should merge into an existing placement, create a new one, or be rejected.
        /// The strategy owns the merge policy (one-per-ID, auto/explicit).
        /// </summary>
        ShapedMergeDecision ResolveShapedMerge(
            IPlacementInventory inventory,
            IItemAdapter item,
            int anchorIndex,
            IPlacementShape shape,
            PlacementOrientation orientation,
            Placement sourcePlacement);
    }
}
