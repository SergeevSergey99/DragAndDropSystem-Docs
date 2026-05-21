using System;
using System.Collections.Generic;
using UnityEngine;
using UDND.Core;
using UDND.Filter;
using UDND.Slots;
using UDND.Tools.Inspector;

namespace UDND.Inventories
{
    /// <summary>
    /// Inventory sorting action. Physically rearranges items in slots.
    /// Uses <see cref="ISlotSorter"/> for sort logic.
    /// </summary>
    [Serializable]
    public class SortInventoryAction : InventoryActionBase
    {
        [SerializeReference, ManagedReferencePicker, Tooltip("Sort strategy")]
        private ISlotSorter _sorter;

        [SerializeField, Tooltip("Sort in ascending order")]
        private bool _ascending = true;

        public override string DisplayName => "Sort Inventory";

        public override ActionResult Execute(UniversalInventory inventory, BaseSlot activeBaseSlot)
        {
            if (inventory == null)
                return ActionResult.Failed("Inventory is null");

            return TrySortInventory(inventory, _sorter, _ascending)
                ? ActionResult.Succeeded()
                : ActionResult.Failed("No items to sort");
        }

        public override bool CanExecute(UniversalInventory inventory, BaseSlot activeBaseSlot)
        {
            if (!base.CanExecute(inventory, activeBaseSlot))
                return false;

            for (int i = 0; i < inventory.SlotCount; i++)
            {
                var slot = inventory.GetSlot(i);
                if (slot != null && !slot.IsEmpty)
                    return true;
            }

            return false;
        }

        /// <summary>
        /// Sort the inventory using an <see cref="ISlotSorter"/>.
        /// </summary>
        public static bool TrySortInventory(UniversalInventory inventory, ISlotSorter sorter, bool ascending)
        {
            if (inventory == null || sorter == null)
                return false;

            var slots = inventory.Slots;
            var nonEmptyIndices = new List<int>();

            for (int i = 0; i < slots.Count; i++)
            {
                var slot = slots[i];
                if (slot != null && !slot.IsEmpty)
                    nonEmptyIndices.Add(i);
            }

            if (nonEmptyIndices.Count == 0)
                return false;

            int direction = ascending ? 1 : -1;

            nonEmptyIndices.Sort((idxA, idxB) =>
            {
                var ctxA = new FilterContext(slots[idxA], inventory, slots, idxA);
                var ctxB = new FilterContext(slots[idxB], inventory, slots, idxB);
                return sorter.Compare(in ctxA, in ctxB) * direction;
            });

            // Collect stacks in sorted order
            var sortedStacks = new List<ItemStack>(nonEmptyIndices.Count);
            foreach (int idx in nonEmptyIndices)
            {
                var slot = slots[idx];
                var copy = slot.Stack.CreateCopy();
                sortedStacks.Add(copy);
            }

            // Clear all non-empty slots
            foreach (int idx in nonEmptyIndices)
                slots[idx].Clear();

            // Place sorted stacks starting from slot 0 (compact to front)
            for (int i = 0; i < sortedStacks.Count && i < slots.Count; i++)
            {
                var slot = inventory.GetSlot(i);
                if (slot != null)
                    slot.SetStack(sortedStacks[i]);
            }

            inventory.UpdateAllVisuals();
            return true;
        }
    }
}
