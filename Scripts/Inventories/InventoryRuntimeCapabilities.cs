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
    /// Outcome of <see cref="IOccupiedSlotDropHandler.ExecuteOccupiedSlotDrop"/>. It tells the
    /// transfer pipeline whether the handler took over the drop and, if so, whether it succeeded —
    /// i.e. whether the rest of the pipeline (the target slot's drop rules and placement) is
    /// interrupted or still runs.
    /// </summary>
    public enum OccupiedSlotDropResult
    {
        /// <summary>
        /// The handler fully performed the drop into its own destination (for example, inserted the
        /// dragged item into the container sitting in the occupied slot).
        /// Pipeline: INTERRUPTED. The entry is reported as committed/success and the target slot's
        /// normal drop rules and placement are NOT run. Use this when you took ownership of the item
        /// and placed or consumed it yourself.
        /// </summary>
        Handled = 0,

        /// <summary>
        /// The handler refused the drop (for example, the destination is full, or moving the item
        /// would be a no-op).
        /// Pipeline: INTERRUPTED. The entry fails, anything the handler mutated is rolled back to the
        /// pre-handler snapshot, and the dragged item stays in / returns to its source slot. Normal
        /// placement is NOT attempted. Use this when the drop must simply not happen.
        /// </summary>
        Rejected = 1,

        /// <summary>
        /// The handler chose not to intercept this drop after all.
        /// Pipeline: NOT interrupted. Anything the handler touched is rolled back, and the entry
        /// continues through the normal transfer pipeline (drop rules + placement) exactly as if no
        /// occupied-slot handler were present. Use this to defer to the default behaviour when you
        /// only discover at execution time that you don't want to handle the drop — equivalent to
        /// <see cref="IOccupiedSlotDropHandler.CheckOccupiedSlotDrop"/> having returned false, but
        /// decided later.
        /// </summary>
        Fallthrough = 2
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
        /// <summary>
        /// Cheap, side-effect-free probe: should <see cref="ExecuteOccupiedSlotDrop"/> be consulted
        /// for this entry/slot at all? Returning false leaves the entry to the normal pipeline.
        /// </summary>
        bool CheckOccupiedSlotDrop(DragEntry entry, BaseSlot occupiedBaseSlot);

        /// <summary>
        /// Performs (or declines) the occupied-slot drop. The returned
        /// <see cref="OccupiedSlotDropResult"/> decides what the pipeline does next — see each value
        /// for whether it interrupts the pipeline and whether it counts as success.
        /// </summary>
        OccupiedSlotDropResult ExecuteOccupiedSlotDrop(DragEntry entry, BaseSlot occupiedBaseSlot);
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
