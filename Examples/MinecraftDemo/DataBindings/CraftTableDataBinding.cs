using System.Collections.Generic;
using DragAndDropSystem.DataBinding;

namespace DragAndDropSystem.Examples.Minecraft
{
    public class CraftTableDataBinding : SlotIndexedInventoryDataBinding<MinecraftItemSO, MinecraftItemAdapter>
    {
        // Определяем создание адаптера из данных предмета
        protected override MinecraftItemAdapter CreateAdapter(MinecraftItemSO item) => new(item);
        // Получаем данные для отрисовки в слотах UI
        protected override IEnumerable<(int index, MinecraftItemSO item, int count)> GetOccupiedSlots()
        {
            for (int i = 0; i < CraftingManager.Instance.CraftTableItems.Count; i++)
            {
                var item = CraftingManager.Instance.CraftTableItems[i];
                if (item != null)
                    yield return (i, item.ItemSO, item.Count);
            }
        }

        // Добавляем предмет перетащенный в слот в данные CraftingManager
        protected override void AddToSlotData(int index, MinecraftItemAdapter adapter, int count)
        {
            CraftingManager.Instance.TryAddCraftTableItem(adapter.ItemSO, count, index);
        }

        // Удаляем предмет вытащенный из слота из данных
        protected override void RemoveFromSlotData(int index, MinecraftItemAdapter adapter, int count)
        {
            CraftingManager.Instance.TryRemoveCraftTableItem(adapter.ItemSO, count, index);
        }

        protected override void Awake()
        {
            // Указываем максимальное число предметов в слоте
            _inventory.SetMaxStackSize(CraftingManager.MaxItemsPerSlot);
            base.Awake();
        }
        
    }
}