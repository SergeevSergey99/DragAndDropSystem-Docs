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

            // Собираем все непустые стаки
            var stacks = new List<ItemStackData>();
            for (int i = 0; i < inventory.SlotCount; i++)
            {
                var slot = inventory.GetSlot(i);
                if (slot != null && !slot.IsEmpty)
                {
                    stacks.Add(new ItemStackData
                    {
                        Item = slot.Stack.Item,
                        Count = slot.Stack.Count,
                        OriginalSlotIndex = i
                    });
                }
            }

            if (stacks.Count == 0)
            {
                return ActionResult.Failed("No items to sort");
            }

            // Сортируем
            SortStacks(stacks);

            // Очищаем все слоты
            for (int i = 0; i < inventory.SlotCount; i++)
            {
                var slot = inventory.GetSlot(i);
                if (slot != null && !slot.IsEmpty)
                {
                    slot.Clear();
                }
            }

            // Размещаем отсортированные стаки обратно
            for (int i = 0; i < stacks.Count && i < inventory.SlotCount; i++)
            {
                var slot = inventory.GetSlot(i);
                if (slot != null)
                {
                    var stackData = stacks[i];
                    var newStack = new ItemStack(stackData.Item, stackData.Count);
                    slot.SetStack(newStack);
                }
            }

            inventory.UpdateAllVisuals();

            return ActionResult.Succeeded();
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

        private void SortStacks(List<ItemStackData> stacks)
        {
            switch (_sortType)
            {
                case SortType.ByName:
                    stacks.Sort((a, b) =>
                    {
                        int result = string.Compare(a.Item.DisplayName, b.Item.DisplayName, StringComparison.Ordinal);
                        return _reverse ? -result : result;
                    });
                    break;

                case SortType.ByItemId:
                    stacks.Sort((a, b) =>
                    {
                        int result = string.Compare(a.Item.ItemId, b.Item.ItemId, StringComparison.Ordinal);
                        return _reverse ? -result : result;
                    });
                    break;

                case SortType.ByStackSize:
                    stacks.Sort((a, b) =>
                    {
                        int result = b.Count.CompareTo(a.Count); // Больше -> меньше
                        return _reverse ? -result : result;
                    });
                    break;
            }
        }

        private class ItemStackData
        {
            public IInventoryItem Item;
            public int Count;
            public int OriginalSlotIndex;
        }
    }
}
