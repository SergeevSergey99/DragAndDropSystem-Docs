using System.Collections.Generic;
using UDND.Core;

namespace UDND.DataBinding
{
    /// <summary>
    /// Template DataBinding for inventories with fixed slots.
    /// Automatically handles ReloadUI, OnItemAdded, and OnItemRemoved.
    /// Derived classes only need to implement 4 primitive methods.
    ///
    /// Each slot is described by its full list of adapters, so stacks made of
    /// distinct adapter instances are preserved without data loss.
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
    ///     protected override IEnumerable&lt;(int index, IReadOnlyList&lt;ItemSO&gt; items)&gt; GetOccupiedSlots()
    ///     {
    ///         var slots = _playerData.Slots;
    ///         for (int i = 0; i &lt; slots.Count; i++)
    ///             if (slots[i] != null) yield return (i, new[] { slots[i] });
    ///     }
    ///     protected override ItemAdapterSoAdapter CreateAdapter(ItemSO itemAdapter) => new ItemAdapterSoAdapter(itemAdapter);
    ///     protected override void AddToSlotData(int index, IReadOnlyList&lt;ItemAdapterSoAdapter&gt; adapters) => _playerData.SetItem(index, adapters[0].itemAdapter);
    ///     protected override void RemoveFromSlotData(int index, IReadOnlyList&lt;ItemAdapterSoAdapter&gt; adapters) => _playerData.ClearSlot(index);
    /// }
    /// </code>
    /// </summary>
    public abstract class SlotIndexedInventoryDataBinding<TData, TAdapter> : InventoryDataBindingBase
        where TAdapter : class, IItemAdapter
    {
        /// <summary>
        /// Get occupied slots with their indices and the full list of items in each slot.
        /// Empty slots may be omitted.
        /// </summary>
        protected abstract IEnumerable<(int index, IReadOnlyList<TData> items)> GetOccupiedSlots();

        /// <summary>
        /// Create an adapter (IItemAdapter) from a data item.
        /// Called when data is loaded into the UI (ReloadUI).
        /// </summary>
        protected abstract TAdapter CreateAdapter(TData item);

        /// <summary>
        /// Update slot data when items are added.
        /// Called when a stack is added to the slot via drag&amp;drop.
        /// The list contains every adapter in the dropped stack.
        /// </summary>
        protected abstract void AddToSlotData(int index, IReadOnlyList<TAdapter> adapters);

        /// <summary>
        /// Update slot data when items are removed.
        /// Called when a stack is removed from the slot via drag&amp;drop.
        /// The list contains every adapter in the removed stack.
        /// </summary>
        protected abstract void RemoveFromSlotData(int index, IReadOnlyList<TAdapter> adapters);

        protected override void OnReloadUI()
        {
            foreach (var (index, items) in GetOccupiedSlots())
            {
                if (items == null || items.Count == 0) continue;

                var adapters = new List<IItemAdapter>(items.Count);
                foreach (var item in items)
                {
                    if (item == null) continue;
                    adapters.Add(CreateAdapter(item));
                }

                if (adapters.Count == 0) continue;

                AddToUIQuiet(adapters, index);
            }
        }

        protected override void OnItemAddedToUI(InventoryItemEventContext context)
        {
            int index = context.AnchorIndex;
            if (index < 0) return;

            var adapters = CollectAdapters(context.Stack);
            if (adapters.Count == 0) return;

            AddToSlotData(index, adapters);
        }

        protected override void OnItemRemovedFromUI(InventoryItemEventContext context)
        {
            int index = context.AnchorIndex;
            if (index < 0) return;

            var adapters = CollectAdapters(context.Stack);
            if (adapters.Count == 0) return;

            RemoveFromSlotData(index, adapters);
        }

        private static List<TAdapter> CollectAdapters(ItemStack stack)
        {
            var result = new List<TAdapter>();
            if (stack?.Adapters == null)
                return result;

            for (int i = 0; i < stack.Adapters.Count; i++)
            {
                if (stack.Adapters[i] is TAdapter adapter)
                    result.Add(adapter);
            }

            return result;
        }
    }
}
