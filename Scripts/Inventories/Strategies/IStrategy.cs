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
        PlacementCandidateOrderer DefaultOrderer { get; }

        bool TryGetCandidate(
            IPlacementGeometry geometry,
            InventoryAcceptanceRequest request,
            BaseSlot targetBaseSlot,
            out PlacementCandidate candidate);

        PlacementCandidateSource GetCandidates(
            IPlacementGeometry geometry,
            InventoryAcceptanceRequest request);
    }
}
