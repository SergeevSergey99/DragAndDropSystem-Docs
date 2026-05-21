using System.Collections.Generic;
using UDND.Core;

namespace UDND.DataBinding
{
    /// <summary>
    /// Template DataBinding for list-based inventories.
    /// Automatically handles ReloadUI, OnItemAdded, and OnItemRemoved.
    /// Derived classes only need to implement 5 primitive methods.
    ///
    /// TData is the data item type (for example, ItemSO or ItemModel)
    /// TAdapter is the adapter type implementing IItemAdapter (for example, ItemAdapterSoAdapter)
    ///
    /// Usage example:
    /// <code>
    /// public class MyBinding : ListInventoryDataBinding&lt;ItemSO, ItemAdapterSoAdapter&gt;
    /// {
    ///     [SerializeField] private List&lt;ItemSO&gt; items;
    ///
    ///     protected override IReadOnlyList&lt;ItemSO&gt; GetItems() => items;
    ///     protected override ItemAdapterSoAdapter CreateAdapter(ItemSO itemAdapter) => new ItemAdapterSoAdapter(itemAdapter);
    ///     protected override ItemSO ExtractData(ItemAdapterSoAdapter adapter) => adapter.itemAdapter;
    ///     protected override void AddToData(ItemSO itemAdapter) => items.Add(itemAdapter);
    ///     protected override void RemoveFromData(ItemSO itemAdapter) => items.Remove(itemAdapter);
    /// }
    /// </code>
    /// </summary>
    public abstract class ListInventoryDataBinding<TData, TAdapter> : InventoryDataBindingBase
        where TAdapter : class, IItemAdapter
    {
        /// <summary>
        /// Get the current data list for display in the UI.
        /// May return null, in which case the UI stays empty.
        /// </summary>
        protected abstract IReadOnlyList<TData> GetItems();

        /// <summary>
        /// Create an adapter (IItemAdapter) from a data item.
        /// Called when data is loaded into the UI (ReloadUI).
        /// </summary>
        protected abstract TAdapter CreateAdapter(TData item);

        /// <summary>
        /// Add an item to the external data source.
        /// Called when an item is added to the UI via drag&amp;drop.
        /// </summary>
        protected abstract void AddToData(TAdapter adapter);

        /// <summary>
        /// Remove an item from the external data source.
        /// Called when an item is removed from the UI via drag&amp;drop.
        /// </summary>
        protected abstract void RemoveFromData(TAdapter adapter);

        protected override void OnReloadUI()
        {
            var items = GetItems();
            if (items == null) return;

            for (int i = 0; i < items.Count; i++)
            {
                var item = items[i];
                if (item == null) continue;

                AddToUIQuiet(new[] { CreateAdapter(item) });
            }
        }

        protected override void OnItemAddedToUI(InventoryItemEventContext context)
        {
            if (context.Stack.PrimaryAdapter is not TAdapter) return;
            for (int i = 0; i < context.Stack.Count; i++)
                AddToData(context.Stack.Adapters[i] as TAdapter);
        }

        protected override void OnItemRemovedFromUI(InventoryItemEventContext context)
        {
            if (context.Stack.PrimaryAdapter is not TAdapter) return;
            for (int i = 0; i < context.Stack.Count; i++)
                RemoveFromData(context.Stack.Adapters[i] as TAdapter);
        }
    }
}