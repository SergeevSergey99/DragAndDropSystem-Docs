using System.Collections.Generic;
using UDND.Core;
using UDND.Examples;
using UDND.DataBinding;

namespace UDND.Examples.Loot
{
    /// <summary>
    /// DataBinding for the chest inventory.
    /// Connects Chest (data) with UniversalInventory (UI).
    /// Bound dynamically through BindToChest().
    /// </summary>
    public class ChestInventoryDataBinding : ListInventoryDataBinding<ItemExampleWith3DSO, ItemAdapterSoWith3DAdapter>
    {
        private Chest _chest;

        /// <summary>
        /// Bind this data binding to a specific chest.
        /// Called from LootUIController when a chest is opened.
        /// </summary>
        public void BindToChest(Chest chest)
        {
            _chest = chest;

            if (_chest != null)
                ReloadUI();
            else
                ClearUI();
        }

        protected override IReadOnlyList<ItemExampleWith3DSO> GetItems() => _chest?.GetItems();
        protected override ItemAdapterSoWith3DAdapter CreateAdapter(ItemExampleWith3DSO item) => new(item);
        protected override void AddToData(ItemAdapterSoWith3DAdapter adapter) => _chest?.AddItem(adapter.item);
        protected override void RemoveFromData(ItemAdapterSoWith3DAdapter adapter) => _chest?.RemoveItem(adapter.item);
    }
}