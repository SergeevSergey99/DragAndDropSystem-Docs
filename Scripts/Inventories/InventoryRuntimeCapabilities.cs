using UDND.Core;
using UDND.Rules;
using UDND.Slots;

namespace UDND.Inventories
{
    public interface IInventoryRuleEvaluator
    {
        InventoryRuleValidator RuleValidator { get; }

        bool CanAcceptByRules(
            BaseSlot baseSlot,
            IItemAdapter itemAdapter,
            int previewCount,
            InventoryAcceptanceRequest request = null,
            bool allowForeignSlot = false);
    }

    /// <summary>
    /// Custom handling for a drop onto an occupied slot (for example, inserting an item into a
    /// container that sits in that slot). Implemented by a DataBinding, resolved by the transfer
    /// pipeline through the target inventory's DataBinding.
    ///
    /// Do not implement this base interface directly: choose a timing variant
    /// (<see cref="IPreRuleOccupiedSlotDropHandler"/> or <see cref="IPostRuleOccupiedSlotDropHandler"/>)
    /// so the pipeline knows whether to consult the handler before or after the target drop rules.
    /// A DataBinding may implement both to hook both points.
    /// </summary>
    public interface IOccupiedSlotDropHandler
    {
        bool CheckOccupiedSlotDrop(DragEntry entry, BaseSlot occupiedBaseSlot);
        bool ExecuteOccupiedSlotDrop(DragEntry entry, BaseSlot occupiedBaseSlot);
    }

    /// <summary>
    /// Occupied-slot drop handler evaluated BEFORE the target inventory's drop/placement rules.
    /// When <see cref="IOccupiedSlotDropHandler.CheckOccupiedSlotDrop"/> accepts, the entry is routed
    /// straight to the handler and the target's drop/placement rules are bypassed, because the drop
    /// targets a different destination (the container) rather than the slot itself.
    /// Transfer-start/global vetoes are still applied earlier in the pipeline.
    /// </summary>
    public interface IPreRuleOccupiedSlotDropHandler : IOccupiedSlotDropHandler { }

    /// <summary>
    /// Occupied-slot drop handler evaluated AFTER the target inventory's drop rules pass.
    /// Use this when the drop must still satisfy the target's normal drop validation before the
    /// custom occupied-slot behavior runs. This matches the legacy occupied-handler timing.
    /// </summary>
    public interface IPostRuleOccupiedSlotDropHandler : IOccupiedSlotDropHandler { }

    public interface IDynamicSlotLifecycle
    {
        void HandleSlotEmptied(BaseSlot baseSlot);

        /// <summary>
        /// Creates a new slot if the inventory's slot management settings allow it.
        /// Returns true and the new slot on success; false if creation is not permitted.
        /// </summary>
        bool TryCreateSlot(out BaseSlot newSlot);
    }

    public interface IInventoryEventSink
    {
        void EmitItemAdded(
            ItemStack stack,
            int slotIndex,
            IInventory sourceInventory,
            BaseSlot sourceBaseSlot,
            BaseSlot targetBaseSlot,
            PlacementSnapshot placementSnapshot = null);

        void EmitItemRemoved(
            ItemStack stack,
            int slotIndex,
            IInventory targetInventory,
            BaseSlot sourceBaseSlot,
            BaseSlot targetBaseSlot,
            PlacementSnapshot placementSnapshot = null);

        void EmitSwapAttempting(InventorySwapContext context);
        void EmitSwapCompleted(InventorySwapContext context);
    }
}
