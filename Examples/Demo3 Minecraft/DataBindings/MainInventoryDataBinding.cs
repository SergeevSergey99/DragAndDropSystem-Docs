using System.Collections.Generic;
using DragAndDropSystem.DataBinding;

namespace DragAndDropSystem.Examples.Minecraft
{
    public class MainInventoryDataBinding : SlotIndexedInventoryDataBinding<MinecraftItemSO, MinecraftItemAdapterAdapter>
    {
        // Определяем создание адаптера из данных предмета
        protected override MinecraftItemAdapterAdapter CreateAdapter(MinecraftItemSO item) => new(item);
        // Получаем данные для отрисовки в слотах UI
        protected override IEnumerable<(int index, MinecraftItemSO item, int count)> GetOccupiedSlots()
        {
            for (int i = 0; i < CraftingManager.AutoCreateInstance.InventoryItems.Count; i++)
            {
                var item = CraftingManager.AutoCreateInstance.InventoryItems[i];
                if (item != null)
                    yield return (i, item.ItemSO, item.Count);
            }
        }

        // Добавляем предмет перетащенный в слот в данные CraftingManager
        protected override void AddToSlotData(int index, MinecraftItemAdapterAdapter adapterAdapter, int count)
        {
            CraftingManager.AutoCreateInstance.TryAddInventoryItem(adapterAdapter.ItemSO, count, index);
        }

        // Удаляем предмет вытащенный из слота из данных
        protected override void RemoveFromSlotData(int index, MinecraftItemAdapterAdapter adapterAdapter, int count)
        {
            CraftingManager.AutoCreateInstance.TryRemoveInventoryItem(adapterAdapter.ItemSO, count, index);
        }

        protected override void Awake()
        {
            // Указываем максимальное число предметов в слоте
            _inventory.SetMaxStackSize(CraftingManager.MaxItemsPerSlot);
            base.Awake();
        }
    }
}