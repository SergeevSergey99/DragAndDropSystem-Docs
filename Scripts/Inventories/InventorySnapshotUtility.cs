using System.Collections.Generic;
using UniversalDragAndDrop.Core;
using UniversalDragAndDrop.Slots;

namespace UniversalDragAndDrop.Inventories
{
    /// <summary>
    /// Helper methods for working with inventory and slot snapshots.
    /// </summary>
    public static class InventorySnapshotUtility
    {
        public static void RestoreInventorySnapshot(
            IInventory inventory,
            IInventorySnapshotProvider provider,
            InventorySnapshot snapshot)
        {
            if (inventory == null)
                return;

            if (provider != null && snapshot != null)
            {
                provider.RestoreSnapshot(snapshot);
                inventory.UpdateAllVisuals();
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
            int previousCount = snapshot.SlotCount;

            if (slots.Count > previousCount)
            {
                changedBaseSlot = inventory.GetSlot(slots.Count - 1);
                wasEmptyBefore = true;
                return true;
            }

            // Reconstruct "before" cell occupancy from the placement snapshot.
            // For shaped placements every covered cell maps to the placement's stack info,
            // so a 1×1 placement at slot i is indistinguishable from the slot[i] view.
            var beforeOccupancy = BuildCellOccupancy(snapshot.Placements);

            int limit = previousCount < slots.Count ? previousCount : slots.Count;
            for (int i = 0; i < limit; i++)
            {
                var slot = slots[i];
                bool prevEmpty = !beforeOccupancy.TryGetValue(i, out var previous);
                bool nowEmpty = slot == null || slot.IsEmpty;

                bool changed = prevEmpty != nowEmpty;
                if (!changed && !prevEmpty && !nowEmpty)
                {
                    var stack = slot.Stack;
                    if (stack.PrimaryAdapter != previous.adapter || stack.Count != previous.count)
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

        private static Dictionary<int, (IItemAdapter adapter, int count)> BuildCellOccupancy(
            IReadOnlyList<InventoryPlacementState> placements)
        {
            var map = new Dictionary<int, (IItemAdapter adapter, int count)>();
            if (placements == null)
                return map;

            for (int p = 0; p < placements.Count; p++)
            {
                var placement = placements[p];
                if (placement.IsEmpty)
                    continue;

                var primary = placement.Adapters[0];
                var entry = (primary, placement.Adapters.Count);

                var covered = placement.CoveredIndices;
                if (covered != null && covered.Count > 0)
                {
                    for (int c = 0; c < covered.Count; c++)
                        map[covered[c]] = entry;
                }
                else if (placement.AnchorIndex >= 0)
                {
                    map[placement.AnchorIndex] = entry;
                }
            }

            return map;
        }
    }
}
