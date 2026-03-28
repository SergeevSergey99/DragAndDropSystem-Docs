using System.Collections.Generic;
using DragAndDropSystem.DataBinding;

namespace DragAndDropSystem.Examples.Minecraft
{
    public class CraftTableDataBinding : SlotIndexedInventoryDataBinding<MinecraftItemSO, MinecraftItemAdapter>
    {
        protected override MinecraftItemAdapter CreateAdapter(MinecraftItemSO item) => new(item);

        protected override IEnumerable<(int index, MinecraftItemSO item, int count)> GetOccupiedSlots()
        {
            for (int i = 0; i < CraftingManager.AutoCreateInstance.CraftTableItems.Count; i++)
            {
                var item = CraftingManager.AutoCreateInstance.CraftTableItems[i];
                if (item != null)
                    yield return (i, item.ItemSO, item.Count);
            }
        }

        protected override void AddToSlotData(int index, MinecraftItemAdapter adapter, int count)
        {
            CraftingManager.AutoCreateInstance.TryAddCraftTableItem(adapter.ItemSO, count, index);
        }

        protected override void RemoveFromSlotData(int index, MinecraftItemAdapter adapter, int count)
        {
            CraftingManager.AutoCreateInstance.TryRemoveCraftTableItem(adapter.ItemSO, count, index);
        }

        protected override void Awake()
        {
            _inventory.SetMaxStackSize(CraftingManager.MaxItemsPerSlot);
            base.Awake();
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            CraftingManager.AutoCreateInstance.OnCraftTableChanged += ReloadUI;
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            if (CraftingManager.IsInstanceExist)
                CraftingManager.Instance.OnCraftTableChanged -= ReloadUI;
        }
    }
}
