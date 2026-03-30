using System.Collections.Generic;
using System.Linq;
using DragAndDropSystem.Core;
using DragAndDropSystem.DataBinding;
using DragAndDropSystem.Rules;

namespace DragAndDropSystem.Examples.Minecraft
{
    public class HotbarDataBinding : SlotIndexedInventoryDataBinding<MinecraftItemSO, MinecraftItemAdapterAdapter>
    {
        // Определяем создание адаптера из данных предмета
        protected override MinecraftItemAdapterAdapter CreateAdapter(MinecraftItemSO item) => new(item);
        // Получаем данные для отрисовки в слотах UI
        protected override IEnumerable<(int index, MinecraftItemSO item, int count)> GetOccupiedSlots()
        {
            for (int i = 0; i < CraftingManager.AutoCreateInstance.HotbarItems.Count; i++)
            {
                var item = CraftingManager.AutoCreateInstance.HotbarItems[i];
                if (item != null)
                    yield return (i, item.ItemSO, item.Count);
            }
        }

        // Добавляем предмет перетащенный в слот в данные CraftingManager
        protected override void AddToSlotData(int index, MinecraftItemAdapterAdapter adapterAdapter, int count)
        {
            CraftingManager.AutoCreateInstance.TryAddHotbarItem(adapterAdapter.ItemSO, count, index);
        }

        // Удаляем предмет вытащенный из слота из данных
        protected override void RemoveFromSlotData(int index, MinecraftItemAdapterAdapter adapterAdapter, int count)
        {
            CraftingManager.AutoCreateInstance.TryRemoveHotbarItem(adapterAdapter.ItemSO, count, index);
        }

        protected override void Awake()
        {
            // Указываем максимальное число предметов в слоте
            _inventory.SetMaxStackSize(CraftingManager.MaxItemsPerSlot);
            base.Awake();
        }
    }
}