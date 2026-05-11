using System.Collections.Generic;
using UniversalDragAndDrop.Core;
using UniversalDragAndDrop.Slots;

namespace UniversalDragAndDrop.Inventories
{
    /// <summary>
    /// Responsible for placement, removal, and slot-level behavior specifics of a strategy.
    /// </summary>
    public interface IPlacementStrategy
    {
        bool TryAddQuite(List<BaseSlot> slots, ItemStack stack, int targetIndex);
        bool TryAdd(List<BaseSlot> slots, ItemStack stack, int targetIndex, bool skipRules = false);
        bool TryRemove(List<BaseSlot> slots, IItemAdapter itemAdapter, int count, int sourceIndex);
        bool TryAddToSlot(List<BaseSlot> slots, ItemStack stack, BaseSlot targetBaseSlot, System.Action ensureFreeSlots, SlotOperationContext operationContext);
        bool RequiresStrategyPlacement(ItemStack stack);
        bool UsesPerItemSlotPlanning { get; }
        bool CanUseAlternativeSlot(BaseSlot baseSlot, IItemAdapter itemAdapter);

        /// <summary>
        /// Plan placement for a multi-cell (shaped) item against the target inventory.
        /// Strategies return <see cref="ShapedPlacementPlanOutcome.NotApplicable"/> for the
        /// 1×1 case so the planner falls back to slot-allocation logic.
        /// </summary>
        ShapedPlacementPlanResult TryPlanShapedPlacement(ShapedPlacementPlanContext context);

        /// <summary>
        /// Apply a previously planned shaped placement to the target inventory.
        /// Strategies return <see cref="ShapedPlacementExecutionOutcome.NotApplicable"/> for the
        /// 1×1 case so the executor falls back to slot-based placement.
        /// </summary>
        ShapedPlacementExecutionResult TryExecuteShapedPlacement(ShapedPlacementExecutionContext context);
    }

    public interface IStackLimitStrategy
    {
        int GetMaxStackSizeForItem(IItemAdapter itemAdapter);
    }
}
