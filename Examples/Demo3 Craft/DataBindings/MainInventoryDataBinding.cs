using System.Collections.Generic;
using System.Linq;
using UDND.DataBinding;

namespace UDND.Examples.Craft
{
    public class MainInventoryDataBinding : SlotIndexedInventoryDataBinding<CraftItemSO, CraftItemAdapterAdapter>
    {
        // Define adapter creation from item data
        protected override CraftItemAdapterAdapter CreateAdapter(CraftItemSO item) => new(item);
        // Get data for rendering in UI slots
        protected override IEnumerable<(int index, IReadOnlyList<CraftItemSO> items)> GetOccupiedSlots()
        {
            for (int i = 0; i < CraftingManager.AutoCreateInstance.InventoryItems.Count; i++)
            {
                var item = CraftingManager.AutoCreateInstance.InventoryItems[i];
                if (item != null)
                    yield return (i, Enumerable.Repeat(item.ItemSO, item.Count).ToList());
            }
        }

        // Add the item dragged into the slot to CraftingManager data
        protected override void AddToSlotData(int index, IReadOnlyList<CraftItemAdapterAdapter> adapters)
        {
            CraftingManager.AutoCreateInstance.TryAddInventoryItem(adapters[0].ItemSO, adapters.Count, index);
        }

        // Remove the item dragged out of the slot from data
        protected override void RemoveFromSlotData(int index, IReadOnlyList<CraftItemAdapterAdapter> adapters)
        {
            CraftingManager.AutoCreateInstance.TryRemoveInventoryItem(adapters[0].ItemSO, adapters.Count, index);
        }

        protected override void Awake()
        {
            // Specify the maximum item count in the slot
            _inventory.SetMaxStackSize(CraftingManager.MaxItemsPerSlot);
            base.Awake();
        }
    }
}