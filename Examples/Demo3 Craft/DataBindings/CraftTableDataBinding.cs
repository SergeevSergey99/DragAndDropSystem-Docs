using System.Collections.Generic;
using System.Linq;
using UDND.DataBinding;

namespace UDND.Examples.Craft
{
    public class CraftTableDataBinding : SlotIndexedInventoryDataBinding<CraftItemSO, CraftItemAdapterAdapter>
    {
        protected override CraftItemAdapterAdapter CreateAdapter(CraftItemSO item) => new(item);

        protected override IEnumerable<(int index, IReadOnlyList<CraftItemSO> items)> GetOccupiedSlots()
        {
            for (int i = 0; i < CraftingManager.AutoCreateInstance.CraftTableItems.Count; i++)
            {
                var item = CraftingManager.AutoCreateInstance.CraftTableItems[i];
                if (item != null)
                    yield return (i, Enumerable.Repeat(item.ItemSO, item.Count).ToList());
            }
        }

        protected override void AddToSlotData(int index, IReadOnlyList<CraftItemAdapterAdapter> adapters)
        {
            CraftingManager.AutoCreateInstance.TryAddCraftTableItem(adapters[0].ItemSO, adapters.Count, index);
        }

        protected override void RemoveFromSlotData(int index, IReadOnlyList<CraftItemAdapterAdapter> adapters)
        {
            CraftingManager.AutoCreateInstance.TryRemoveCraftTableItem(adapters[0].ItemSO, adapters.Count, index);
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
