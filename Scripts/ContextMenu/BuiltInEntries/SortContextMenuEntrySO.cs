using DragAndDropSystem.Filter;
using DragAndDropSystem.Inventories;
using DragAndDropSystem.Tools.Inspector;
using UnityEngine;

namespace DragAndDropSystem.ContextMenu.BuiltInEntries
{
    /// <summary>
    /// Built-in menu entry: sorts items in the inventory using an <see cref="ISlotSorter"/>.
    /// </summary>
    [CreateAssetMenu(fileName = "SortEntry", menuName = "DragAndDrop/ContextMenu/Built-in/Sort", order = 0)]
    public class SortContextMenuEntrySO : ContextMenuEntryDefinitionSO
    {
        [SerializeReference, ManagedReferencePicker] private ISlotSorter _sorter;
        [SerializeField] private bool _ascending = true;

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
            if (inventory == null || _sorter == null)
                return;

            SortInventoryAction.TrySortInventory(inventory, _sorter, _ascending);
        }
    }
}
