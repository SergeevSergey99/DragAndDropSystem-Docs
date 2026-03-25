using System;

namespace DragAndDropSystem.Core
{
    /// <summary>
    /// Универсальная обертка для предмета с количеством
    /// Работает с любым типом, реализующим IInventoryItem
    /// Лимиты стака задаются через настройку Max Stack Size в UniversalInventory
    /// или через IStackSizeLimitable на конкретном предмете
    /// </summary>
    [Serializable]
    public class ItemStack
    {
        public IInventoryItem Item { get; private set; }
        public int Count { get; private set; }

        public bool IsEmpty => Item == null || Count <= 0;

        public ItemStack(IInventoryItem item, int count = 1)
        {
            Item = item;
            Count = Math.Max(0, count);
        }

        public static ItemStack Empty() => new ItemStack(null, 0);

        /// <summary>
        /// Проверить, можно ли стакнуть с другим предметом (одинаковый ItemId)
        /// </summary>
        public bool CanStack(IInventoryItem otherItem)
        {
            if (Item == null || otherItem == null) return false;
            return Item.ItemId == otherItem.ItemId;
        }

        /// <summary>
        /// Добавить предметы к стаку без ограничений
        /// </summary>
        public void AddToStack(int amount)
        {
            Count += amount;
        }

        /// <summary>
        /// Удалить предметы из стака
        /// </summary>
        public int RemoveFromStack(int amount)
        {
            int toRemove = Math.Min(amount, Count);
            Count -= toRemove;
            if (Count <= 0)
            {
                Item = null;
                Count = 0;
            }
            return toRemove;
        }

        /// <summary>
        /// Разделить стак на две части
        /// </summary>
        public ItemStack Split(int amount)
        {
            int taken = RemoveFromStack(amount);
            return taken > 0 ? new ItemStack(Item, taken) : Empty();
        }

        /// <summary>
        /// Заменить предмет в стеке, сохранив количество
        /// Полезно для замены адаптеров (например, при торговле)
        /// </summary>
        /// <param name="newItem">Новый предмет</param>
        public void ReplaceItem(IInventoryItem newItem)
        {
            if (newItem == null)
            {
                Clear();
                return;
            }

            Item = newItem;
            // Count остается тем же
        }

        /// <summary>
        /// Очистить стак
        /// </summary>
        public void Clear()
        {
            Item = null;
            Count = 0;
        }
    }
}
