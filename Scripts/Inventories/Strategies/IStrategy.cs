using System.Collections.Generic;
using UDND.Core;
using UDND.Slots;

namespace UDND.Inventories
{
    /// <summary>
    /// Inventory behavior strategy.
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

        bool TryAddQuiet(List<BaseSlot> slots, ItemStack stack, int targetIndex);
        bool TryAdd(List<BaseSlot> slots, ItemStack stack, int targetIndex, bool skipRules = false);
        bool TryRemove(List<BaseSlot> slots, IItemAdapter itemAdapter, int count, int sourceIndex);
        bool TryAddToSlot(List<BaseSlot> slots, ItemStack stack, BaseSlot targetBaseSlot, System.Action ensureFreeSlots, SlotOperationContext operationContext);
        bool RequiresStrategyPlacement(ItemStack stack);
        bool UsesPerItemSlotPlanning { get; }
        bool CanUseAlternativeSlot(BaseSlot baseSlot, IItemAdapter itemAdapter);
    }
}
