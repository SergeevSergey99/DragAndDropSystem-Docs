using DragAndDropSystem.Core;
using DragAndDropSystem.Inventories;
using UnityEngine;

namespace DragAndDropSystem.ContextMenu.BuiltInEntries
{
    /// <summary>
    /// Встроенный пункт меню: сортировка предметов в инвентаре.
    /// </summary>
    [CreateAssetMenu(fileName = "SortEntry", menuName = "DragAndDrop/ContextMenu/Built-in/Sort", order = 0)]
    public class SortContextMenuEntrySO : ContextMenuEntryDefinitionSO
    {
        [SerializeField] private SortInventoryAction.SortType _sortType = SortInventoryAction.SortType.ByName;
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

            _ = SortInventoryAction.TrySortInventory(inventory, _sortType, _reverse);
        }
    }
}
