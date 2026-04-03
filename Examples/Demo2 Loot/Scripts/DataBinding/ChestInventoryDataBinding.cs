using System.Collections.Generic;
using DragAndDropSystem.Core;
using DragAndDropSystem.DataBinding;
using Plugins.DragAndDropSystem.Examples;

namespace DragAndDropSystem.Examples.Loot
{
    /// <summary>
    /// DataBinding для инвентаря сундука.
    /// Связывает Chest (данные) ↔ UniversalInventory (UI).
    /// Привязывается динамически через BindToChest().
    /// </summary>
    public class ChestInventoryDataBinding : ListInventoryDataBinding<ItemExampleWith3DSO, ItemAdapterSoWith3DAdapter>
    {
        private Chest _chest;

        /// <summary>
        /// Привязать этот биндинг к конкретному сундуку.
        /// Вызывается из LootUIController когда открывается сундук.
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
