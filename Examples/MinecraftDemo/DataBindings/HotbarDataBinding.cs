using System.Collections.Generic;
using DragAndDropSystem.DataBinding;

namespace DragAndDropSystem.Examples.Minecraft
{
    public class HotbarDataBinding : SlotIndexedInventoryDataBinding<MinecraftItemSO, MinecraftItemAdapter>
    {
        protected override IEnumerable<(int index, MinecraftItemSO item, int count)> GetOccupiedSlots()
        {
            throw new System.NotImplementedException();
        }

        protected override MinecraftItemAdapter CreateAdapter(MinecraftItemSO item)
        {
            throw new System.NotImplementedException();
        }

        protected override MinecraftItemSO ExtractData(MinecraftItemAdapter adapter)
        {
            throw new System.NotImplementedException();
        }

        protected override void AddToSlotData(int index, MinecraftItemSO item, int count)
        {
            throw new System.NotImplementedException();
        }

        protected override void RemoveFromSlotData(int index, MinecraftItemSO item, int count)
        {
            throw new System.NotImplementedException();
        }
    }
}