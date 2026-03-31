using System;
using System.Collections.Generic;
using DragAndDropSystem.Core;
using DragAndDropSystem.Slots;
using UnityEngine;

namespace DragAndDropSystem.Inventories
{
    /// <summary>
    /// Действие сортировки предметов в инвентаре
    /// </summary>
    [Serializable]
    public class SortInventoryAction : InventoryActionBase
    {
        public enum SortType
        {
            ByName,         // По имени предмета
            ByItemId,       // По ID предмета
            ByStackSize,    // По размеру стака (больше -> меньше)
        }

        [SerializeField, Tooltip("Тип сортировки")]
        private SortType _sortType = SortType.ByName;

        [SerializeField, Tooltip("Сортировать в обратном порядке")]
        private bool _reverse = false;

        public override string DisplayName => "Sort Inventory";

        public override ActionResult Execute(UniversalInventory inventory, UniversalSlot activeSlot)
        {
            if (inventory == null)
            {
                return ActionResult.Failed("Inventory is null");
            }

            return TrySortInventory(inventory, _sortType, _reverse)
                ? ActionResult.Succeeded()
                : ActionResult.Failed("No items to sort");
        }

        public override bool CanExecute(UniversalInventory inventory, UniversalSlot activeSlot)
        {
            if (!base.CanExecute(inventory, activeSlot))
                return false;

            // Проверяем, есть ли хотя бы один непустой слот
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
                    slot.SetStack(new ItemStack(stackData.ItemAdapter, stackData.Count));
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
                        int result = b.Count.CompareTo(a.Count); // Больше -> меньше
                        return reverse ? -result : result;
                    });
                    break;
            }
        }

        private class ItemStackData
        {
            public IItemAdapter ItemAdapter;
            public int Count;
            public int OriginalSlotIndex;
        }
    }
}
