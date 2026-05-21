using System.Collections.Generic;
using UDND.DataBinding;

namespace UDND.Examples.Minecraft
{
    public class MainInventoryDataBinding : SlotIndexedInventoryDataBinding<MinecraftItemSO, MinecraftItemAdapterAdapter>
    {
        // Define adapter creation from item data
        protected override MinecraftItemAdapterAdapter CreateAdapter(MinecraftItemSO item) => new(item);
        // Get data for rendering in UI slots
        protected override IEnumerable<(int index, MinecraftItemSO item, int count)> GetOccupiedSlots()
        {
            for (int i = 0; i < CraftingManager.AutoCreateInstance.InventoryItems.Count; i++)
            {
                var item = CraftingManager.AutoCreateInstance.InventoryItems[i];
                if (item != null)
                    yield return (i, item.ItemSO, item.Count);
            }
        }

        // Add the item dragged into the slot to CraftingManager data
        protected override void AddToSlotData(int index, MinecraftItemAdapterAdapter adapterAdapter, int count)
        {
            CraftingManager.AutoCreateInstance.TryAddInventoryItem(adapterAdapter.ItemSO, count, index);
        }

        // Remove the item dragged out of the slot from data
        protected override void RemoveFromSlotData(int index, MinecraftItemAdapterAdapter adapterAdapter, int count)
        {
            CraftingManager.AutoCreateInstance.TryRemoveInventoryItem(adapterAdapter.ItemSO, count, index);
        }

        protected override void Awake()
        {
            // Specify the maximum item count in the slot
            _inventory.SetMaxStackSize(CraftingManager.MaxItemsPerSlot);
            base.Awake();
        }
    }
}