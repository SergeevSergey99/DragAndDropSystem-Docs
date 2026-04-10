using DragAndDropSystem.Core;
using DragAndDropSystem.Slots;

namespace DragAndDropSystem.Inventories
{
    /// <summary>
    /// Helper methods for working with inventory and slot snapshots.
    /// </summary>
    public static class InventorySnapshotUtility
    {
        public static InventorySlotState CaptureSlotState(BaseSlot baseSlot)
        {
            if (baseSlot == null || baseSlot.IsEmpty)
                return new InventorySlotState(null);

            return new InventorySlotState(baseSlot.Stack.Adapters);
        }

        public static void RestoreSlotState(BaseSlot baseSlot, InventorySlotState state)
        {
            if (baseSlot == null)
                return;

            if (state.IsEmpty)
            {
                baseSlot.Clear();
            }
            else
            {
                if (ItemStack.TryCreate(state.Adapters, out var restoredStack))
                    baseSlot.SetStack(restoredStack);
            }

            baseSlot.UpdateVisuals();
        }

        public static void RestoreInventorySnapshot(
            IInventory inventory,
            IInventorySnapshotProvider provider,
            InventorySnapshot snapshot,
            BaseSlot fallbackBaseSlot,
            InventorySlotState fallbackState)
        {
            if (inventory == null)
                return;

            if (provider != null && snapshot != null)
            {
                provider.RestoreSnapshot(snapshot);
                inventory.UpdateAllVisuals();
            }
            else if (fallbackBaseSlot != null)
            {
                RestoreSlotState(fallbackBaseSlot, fallbackState);
            }
        }

        public static bool TryResolveSlotChange(
            IInventory inventory,
            InventorySnapshot snapshot,
            out BaseSlot changedBaseSlot,
            out bool wasEmptyBefore)
        {
            changedBaseSlot = null;
            wasEmptyBefore = false;

            if (inventory == null || snapshot == null)
                return false;

            var slots = inventory.Slots;
            int previousCount = snapshot.Slots.Count;

            if (slots.Count > previousCount)
            {
                changedBaseSlot = inventory.GetSlot(slots.Count - 1);
                wasEmptyBefore = true;
                return true;
            }

            int limit = previousCount < slots.Count ? previousCount : slots.Count;
            for (int i = 0; i < limit; i++)
            {
                var slot = slots[i];
                var previous = snapshot.Slots[i];

                bool prevEmpty = previous.IsEmpty;
                bool nowEmpty = slot == null || slot.IsEmpty;

                bool changed = prevEmpty != nowEmpty;
                if (!changed && !prevEmpty && !nowEmpty)
                {
                    var stack = slot.Stack;
                    if (stack.PrimaryAdapter != previous.ItemAdapter || stack.Count != previous.Count)
                    {
                        changed = true;
                    }
                }

                if (changed)
                {
                    changedBaseSlot = slot;
                    wasEmptyBefore = prevEmpty;
                    return true;
                }
            }

            return false;
        }
    }
}
