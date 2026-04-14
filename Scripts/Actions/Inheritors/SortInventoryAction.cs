using System;
using System.Collections.Generic;
using DragAndDropSystem.Core;
using DragAndDropSystem.Slots;
using UnityEngine;

namespace DragAndDropSystem.Inventories
{
    /// <summary>
    /// Inventory item sorting action
    /// </summary>
    [Serializable]
    public class SortInventoryAction : InventoryActionBase
    {
        public enum SortType
        {
            ByName,         // By item name
            ByItemId,       // By item ID
            ByStackSize,    // By stack size (larger -> smaller)
        }

        [SerializeField, Tooltip("Sort type")]
        private SortType _sortType = SortType.ByName;

        [SerializeField, Tooltip("Sort in reverse order")]
        private bool _reverse = false;

        public override string DisplayName => "Sort Inventory";

        public override ActionResult Execute(UniversalInventory inventory, BaseSlot activeBaseSlot)
        {
            if (inventory == null)
            {
                return ActionResult.Failed("Inventory is null");
            }

            return TrySortInventory(inventory, _sortType, _reverse)
                ? ActionResult.Succeeded()
                : ActionResult.Failed("No items to sort");
        }

        public override bool CanExecute(UniversalInventory inventory, BaseSlot activeBaseSlot)
        {
            if (!base.CanExecute(inventory, activeBaseSlot))
                return false;

            // Check whether there is at least one non-empty slot
            for (int i = 0; i < inventory.SlotCount; i++)
            {
                var slot = inventory.GetSlot(i);
                if (slot != null && !slot.IsEmpty)
                {
                    return true;
                }
            }

            return false;
        }

        public static bool TrySortInventory(UniversalInventory inventory, SortType sortType, bool reverse)
        {
            if (inventory == null)
                return false;

            var stacks = new List<ItemStackData>();
            for (int i = 0; i < inventory.SlotCount; i++)
            {
                var slot = inventory.GetSlot(i);
                if (slot != null && !slot.IsEmpty)
                {
                    stacks.Add(new ItemStackData
                    {
                        ItemAdapter = slot.Stack.PrimaryAdapter,
                        Adapters = slot.Stack.Adapters,
                        Count = slot.Stack.Count,
                        OriginalSlotIndex = i
                    });
                }
            }

            if (stacks.Count == 0)
                return false;

            SortStacks(stacks, sortType, reverse);

            for (int i = 0; i < inventory.SlotCount; i++)
            {
                var slot = inventory.GetSlot(i);
                if (slot != null && !slot.IsEmpty)
                    slot.Clear();
            }

            for (int i = 0; i < stacks.Count && i < inventory.SlotCount; i++)
            {
                var slot = inventory.GetSlot(i);
                if (slot != null)
                {
                    var stackData = stacks[i];
                    if (ItemStack.TryCreate(stackData.Adapters, out var sortedStack))
                        slot.SetStack(sortedStack);
                }
            }

            inventory.UpdateAllVisuals();
            return true;
        }

        private static void SortStacks(List<ItemStackData> stacks, SortType sortType, bool reverse)
        {
            switch (sortType)
            {
                case SortType.ByName:
                    stacks.Sort((a, b) =>
                    {
                        int result = string.Compare(a.ItemAdapter.DisplayName, b.ItemAdapter.DisplayName, StringComparison.Ordinal);
                        return reverse ? -result : result;
                    });
                    break;

                case SortType.ByItemId:
                    stacks.Sort((a, b) =>
                    {
                        int result = string.Compare(a.ItemAdapter.ItemId, b.ItemAdapter.ItemId, StringComparison.Ordinal);
                        return reverse ? -result : result;
                    });
                    break;

                case SortType.ByStackSize:
                    stacks.Sort((a, b) =>
                    {
                        int result = b.Count.CompareTo(a.Count); // Larger -> smaller
                        return reverse ? -result : result;
                    });
                    break;
            }
        }

        private class ItemStackData
        {
            public IItemAdapter ItemAdapter;
            public IReadOnlyList<IItemAdapter> Adapters;
            public int Count;
            public int OriginalSlotIndex;
        }
    }
}