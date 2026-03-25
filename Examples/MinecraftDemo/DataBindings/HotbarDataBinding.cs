using System.Collections.Generic;
using System.Linq;
using DragAndDropSystem.Core;
using DragAndDropSystem.DataBinding;
using DragAndDropSystem.Rules;

namespace DragAndDropSystem.Examples.Minecraft
{
    public class HotbarDataBinding : SlotIndexedInventoryDataBinding<MinecraftItemSO, MinecraftItemAdapter>
    {
        protected override IEnumerable<(int index, MinecraftItemSO item, int count)> GetOccupiedSlots()
        {
            for (int i = 0; i < CraftingManager.Instance.HotbarItems.Count; i++)
            {
                var item = CraftingManager.Instance.HotbarItems[i];
                if (item != null)
                    yield return (i, item.ItemSO, item.Count);
            }
        }

        protected override MinecraftItemAdapter CreateAdapter(MinecraftItemSO item) => new(item);

        protected override void AddToSlotData(int index, MinecraftItemAdapter adapter, int count)
        {
            CraftingManager.Instance.TryAddHotbarItem(adapter.ItemSO, count, index);
        }

        protected override void RemoveFromSlotData(int index, MinecraftItemAdapter adapter, int count)
        {
            CraftingManager.Instance.TryRemoveHotbarItem(adapter.ItemSO, count, index);
        }

        protected override RuleResult CanDrop(DragContext context, DragEntry entry)
        {
            if (entry.Stack.Item is not MinecraftItemAdapter adapter)
                return RuleResult.Failure("Неверный тип предмета!");
            if (!CraftingManager.Instance.CanAddHotbarItem(adapter.ItemSO, entry.Stack.Count, context.TargetSlot.Index))
                return RuleResult.Failure("Невозможно положить этот предмет в хотбар!");
            return base.CanDrop(context, entry);
        }
    }
}