using System.Collections.Generic;
using UDND.DataBinding;

namespace UDND.Examples.Craft
{
    public class CraftTableDataBinding : SlotIndexedInventoryDataBinding<CraftItemSO, CraftItemAdapterAdapter>
    {
        protected override CraftItemAdapterAdapter CreateAdapter(CraftItemSO item) => new(item);

        protected override IEnumerable<(int index, CraftItemSO item, int count)> GetOccupiedSlots()
        {
            for (int i = 0; i < CraftingManager.AutoCreateInstance.CraftTableItems.Count; i++)
            {
                var item = CraftingManager.AutoCreateInstance.CraftTableItems[i];
                if (item != null)
                    yield return (i, item.ItemSO, item.Count);
            }
        }

        protected override void AddToSlotData(int index, CraftItemAdapterAdapter adapterAdapter, int count)
        {
            CraftingManager.AutoCreateInstance.TryAddCraftTableItem(adapterAdapter.ItemSO, count, index);
        }

        protected override void RemoveFromSlotData(int index, CraftItemAdapterAdapter adapterAdapter, int count)
        {
            CraftingManager.AutoCreateInstance.TryRemoveCraftTableItem(adapterAdapter.ItemSO, count, index);
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
