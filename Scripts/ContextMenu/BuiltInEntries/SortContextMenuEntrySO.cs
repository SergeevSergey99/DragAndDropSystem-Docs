using System;
using System.Collections.Generic;
using DragAndDropSystem.Core;
using UnityEngine;

namespace DragAndDropSystem.ContextMenu.BuiltInEntries
{
    /// <summary>
    /// Встроенный пункт меню: сортировка предметов в инвентаре.
    /// </summary>
    [CreateAssetMenu(fileName = "SortEntry", menuName = "DragAndDrop/ContextMenu/Built-in/Sort", order = 0)]
    public class SortContextMenuEntrySO : ContextMenuEntryDefinitionSO
    {
        public enum SortType
        {
            ByName,
            ByItemId,
            ByStackSize,
        }

        [SerializeField] private SortType _sortType = SortType.ByName;
        [SerializeField] private bool _reverse = false;

        public override bool CanShow(ContextMenuContext ctx)
        {
            if (ctx.Inventory == null)
                return false;

            for (int i = 0; i < ctx.Inventory.SlotCount; i++)
            {
                var slot = ctx.Inventory.GetSlot(i);
                if (slot != null && !slot.IsEmpty)
                    return true;
            }

            return false;
        }

        public override void Execute(ContextMenuContext ctx)
        {
            var inventory = ctx.Inventory;
            if (inventory == null)
                return;

            var stacks = new List<(IInventoryItem item, int count)>();
            for (int i = 0; i < inventory.SlotCount; i++)
            {
                var slot = inventory.GetSlot(i);
                if (slot != null && !slot.IsEmpty)
                    stacks.Add((slot.Stack.Item, slot.Stack.Count));
            }

            Sort(stacks);

            for (int i = 0; i < inventory.SlotCount; i++)
                inventory.GetSlot(i)?.Clear();

            for (int i = 0; i < stacks.Count && i < inventory.SlotCount; i++)
            {
                var slot = inventory.GetSlot(i);
                if (slot != null)
                    slot.SetStack(new ItemStack(stacks[i].item, stacks[i].count));
            }

            inventory.UpdateAllVisuals();
        }

        private void Sort(List<(IInventoryItem item, int count)> stacks)
        {
            switch (_sortType)
            {
                case SortType.ByName:
                    stacks.Sort((a, b) =>
                    {
                        int r = string.Compare(a.item.DisplayName, b.item.DisplayName, StringComparison.Ordinal);
                        return _reverse ? -r : r;
                    });
                    break;
                case SortType.ByItemId:
                    stacks.Sort((a, b) =>
                    {
                        int r = string.Compare(a.item.ItemId, b.item.ItemId, StringComparison.Ordinal);
                        return _reverse ? -r : r;
                    });
                    break;
                case SortType.ByStackSize:
                    stacks.Sort((a, b) =>
                    {
                        int r = b.count.CompareTo(a.count);
                        return _reverse ? -r : r;
                    });
                    break;
            }
        }
    }
}
