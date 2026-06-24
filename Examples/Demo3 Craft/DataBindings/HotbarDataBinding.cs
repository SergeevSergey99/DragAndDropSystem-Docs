using System.Collections.Generic;
using System.Linq;
using UDND.DataBinding;

namespace UDND.Examples.Craft
{
    public class HotbarDataBinding : SlotIndexedInventoryDataBinding<CraftItemSO, CraftItemAdapterAdapter>
    {
        // Define adapter creation from item data
        protected override CraftItemAdapterAdapter CreateAdapter(CraftItemSO item) => new(item);
        // Get data for rendering in UI slots
        protected override IEnumerable<(int index, IReadOnlyList<CraftItemSO> items)> GetOccupiedSlots()
        {
            for (int i = 0; i < CraftingManager.AutoCreateInstance.HotbarItems.Count; i++)
            {
                var item = CraftingManager.AutoCreateInstance.HotbarItems[i];
                if (item != null)
                    yield return (i, Enumerable.Repeat(item.ItemSO, item.Count).ToList());
            }
        }

        // Add the item dragged into the slot to CraftingManager data
        protected override void AddToSlotData(int index, IReadOnlyList<CraftItemAdapterAdapter> adapters)
        {
            CraftingManager.AutoCreateInstance.TryAddHotbarItem(adapters[0].ItemSO, adapters.Count, index);
        }

        // Remove the item dragged out of the slot from data
        protected override void RemoveFromSlotData(int index, IReadOnlyList<CraftItemAdapterAdapter> adapters)
        {
            CraftingManager.AutoCreateInstance.TryRemoveHotbarItem(adapters[0].ItemSO, adapters.Count, index);
        }

        protected override void Awake()
        {
            // Specify the maximum item count in the slot
            _inventory.SetMaxStackSize(CraftingManager.MaxItemsPerSlot);
            base.Awake();
        }
    }
}