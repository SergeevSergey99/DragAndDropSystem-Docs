using System.Collections.Generic;
using UniversalDragAndDrop.Core;

namespace UniversalDragAndDrop.DataBinding
{
    /// <summary>
    /// Template DataBinding for inventories with fixed slots.
    /// Automatically handles ReloadUI, OnItemAdded, and OnItemRemoved.
    /// Derived classes only need to implement 5 primitive methods.
    ///
    /// TData is the data item type (for example, ItemSO or ItemModel)
    /// TAdapter is the adapter type implementing IItemAdapter (for example, ItemAdapterSoAdapter)
    ///
    /// Usage example:
    /// <code>
    /// public class MyBinding : SlotIndexedInventoryDataBinding&lt;ItemSO, ItemAdapterSoAdapter&gt;
    /// {
    ///     [SerializeField] private PlayerInventoryData _playerData;
    ///
    ///     protected override IEnumerable&lt;(int index, ItemSO itemAdapter, int count)&gt; GetOccupiedSlots()
    ///     {
    ///         var slots = _playerData.Slots;
    ///         for (int i = 0; i &lt; slots.Count; i++)
    ///             if (slots[i] != null) yield return (i, slots[i], 1);
    ///     }
    ///     protected override ItemAdapterSoAdapter CreateAdapter(ItemSO itemAdapter) => new ItemAdapterSoAdapter(itemAdapter);
    ///     protected override ItemSO ExtractData(ItemAdapterSoAdapter adapter) => adapter.itemAdapter;
    ///     protected override void AddToSlotData(int index, ItemSO itemAdapter, int count) => _playerData.SetItem(index, itemAdapter);
    ///     protected override void RemoveFromSlotData(int index, ItemSO itemAdapter, int count) => _playerData.ClearSlot(index);
    /// }
    /// </code>
    /// </summary>
    public abstract class SlotIndexedInventoryDataBinding<TData, TAdapter> : InventoryDataBindingBase
        where TAdapter : class, IItemAdapter
    {
        /// <summary>
        /// Get occupied slots with their indices, data, and counts.
        /// Empty slots may be omitted.
        /// </summary>
        protected abstract IEnumerable<(int index, TData item, int count)> GetOccupiedSlots();

        /// <summary>
        /// Create an adapter (IItemAdapter) from a data item.
        /// Called when data is loaded into the UI (ReloadUI).
        /// </summary>
        protected abstract TAdapter CreateAdapter(TData item);

        /// <summary>
        /// Update slot data when an item is added.
        /// Called when an item is added to the slot via drag&amp;drop.
        /// </summary>
        protected abstract void AddToSlotData(int index, TAdapter adapter, int count);

        /// <summary>
        /// Update slot data when an item is removed.
        /// Called when an item is removed from the slot via drag&amp;drop.
        /// </summary>
        protected abstract void RemoveFromSlotData(int index, TAdapter item, int count);

        protected override void OnReloadUI()
        {
            foreach (var (index, item, count) in GetOccupiedSlots())
            {
                if (item == null) continue;

                AddToUIQuiet(() => CreateAdapter(item), count, index);
            }
        }

        protected override void OnItemAddedToUI(InventoryItemEventContext context)
        {
            if (context.Stack.PrimaryAdapter is not TAdapter adapter) return;

            int index = context.AnchorIndex;
            if (index < 0) return;

            AddToSlotData(index, adapter, context.Stack.Count);
        }

        protected override void OnItemRemovedFromUI(InventoryItemEventContext context)
        {
            if (context.Stack.PrimaryAdapter is not TAdapter adapter) return;

            int index = context.AnchorIndex;
            if (index < 0) return;

            RemoveFromSlotData(index, adapter, context.Stack.Count);
        }
    }
}
