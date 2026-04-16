using System.Collections.Generic;
using UniversalDragAndDrop.DataBinding;

namespace UniversalDragAndDrop.Examples.Minecraft
{
    public class HotbarDataBinding : SlotIndexedInventoryDataBinding<MinecraftItemSO, MinecraftItemAdapterAdapter>
    {
        // Define adapter creation from item data
        protected override MinecraftItemAdapterAdapter CreateAdapter(MinecraftItemSO item) => new(item);
        // Get data for rendering in UI slots
        protected override IEnumerable<(int index, MinecraftItemSO item, int count)> GetOccupiedSlots()
        {
            for (int i = 0; i < CraftingManager.AutoCreateInstance.HotbarItems.Count; i++)
            {
                var item = CraftingManager.AutoCreateInstance.HotbarItems[i];
                if (item != null)
                    yield return (i, item.ItemSO, item.Count);
            }
        }

        // Add the item dragged into the slot to CraftingManager data
        protected override void AddToSlotData(int index, MinecraftItemAdapterAdapter adapterAdapter, int count)
        {
            CraftingManager.AutoCreateInstance.TryAddHotbarItem(adapterAdapter.ItemSO, count, index);
        }

        // Remove the item dragged out of the slot from data
        protected override void RemoveFromSlotData(int index, MinecraftItemAdapterAdapter adapterAdapter, int count)
        {
            CraftingManager.AutoCreateInstance.TryRemoveHotbarItem(adapterAdapter.ItemSO, count, index);
        }

        protected override void Awake()
        {
            // Specify the maximum item count in the slot
            _inventory.SetMaxStackSize(CraftingManager.MaxItemsPerSlot);
            base.Awake();
        }
    }
}