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

    public interface IOccupiedSlotDropHandler
    {
        bool CheckOccupiedSlotDrop(DragEntry entry, BaseSlot occupiedBaseSlot);
        bool ExecuteOccupiedSlotDrop(DragEntry entry, BaseSlot occupiedBaseSlot);
    }

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
