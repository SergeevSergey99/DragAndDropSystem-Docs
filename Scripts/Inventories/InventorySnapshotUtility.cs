using DragAndDropSystem.Core;
using DragAndDropSystem.Slots;

namespace DragAndDropSystem.Inventories
{
    /// <summary>
    /// Вспомогательные методы для работы со снапшотами инвентарей и слотов.
    /// </summary>
    public static class InventorySnapshotUtility
    {
        public static InventorySlotState CaptureSlotState(ISlot slot)
        {
            if (slot == null || slot.IsEmpty)
            {
                return new InventorySlotState(null, 0);
            }

            return new InventorySlotState(slot.Stack.ItemAdapter, slot.Stack.Count);
        }

        public static void RestoreSlotState(ISlot slot, InventorySlotState state)
        {
            if (slot == null)
                return;

            if (state.IsEmpty)
            {
                slot.Clear();
            }
            else
            {
                slot.SetStack(new ItemStack(state.ItemAdapter, state.Count));
            }

            slot.UpdateVisuals();
        }

        public static void RestoreInventorySnapshot(
            IInventory inventory,
            IInventorySnapshotProvider provider,
            InventorySnapshot snapshot,
            ISlot fallbackSlot,
            InventorySlotState fallbackState)
        {
            if (inventory == null)
                return;

            if (provider != null && snapshot != null)
            {
                provider.RestoreSnapshot(snapshot);
                inventory.UpdateAllVisuals();
            }
            else if (fallbackSlot != null)
            {
                RestoreSlotState(fallbackSlot, fallbackState);
            }
        }

        public static bool TryResolveSlotChange(
            IInventory inventory,
            InventorySnapshot snapshot,
            out ISlot changedSlot,
            out bool wasEmptyBefore)
        {
            changedSlot = null;
            wasEmptyBefore = false;

            if (inventory == null || snapshot == null)
                return false;

            var slots = inventory.Slots;
            int previousCount = snapshot.Slots.Count;

            if (slots.Count > previousCount)
            {
                changedSlot = inventory.GetSlot(slots.Count - 1);
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
                    if (stack.ItemAdapter != previous.ItemAdapter || stack.Count != previous.Count)
                    {
                        changed = true;
                    }
                }

                if (changed)
                {
                    changedSlot = slot;
                    wasEmptyBefore = prevEmpty;
                    return true;
                }
            }

            return false;
        }
    }
}
