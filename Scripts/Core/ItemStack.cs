using System;
using UnityEngine;

namespace DragAndDropSystem.Core
{
    /// <summary>
    /// Универсальная обертка для предмета с количеством
    /// Работает с любым типом, реализующим IItemAdapter
    /// Лимиты стака задаются через настройку Max Stack Size в UniversalInventory
    /// или через IStackSizeLimitable на конкретном предмете
    /// </summary>
    [Serializable]
    public class ItemStack
    {
        public IItemAdapter ItemAdapter { get; private set; }
        public int Count { get; private set; }
        
        public string ID { get; private set; }
        public Sprite Icon { get; private set; }
        public string DisplayName;

        public bool IsEmpty => ItemAdapter == null || Count <= 0;

        public ItemStack(IItemAdapter itemAdapter, int count = 1)
        {
            ItemAdapter = itemAdapter;
            ID = itemAdapter.ItemId;
            Icon = itemAdapter.Icon;
            DisplayName = itemAdapter.DisplayName;
            Count = Math.Max(0, count);
        }

        public static ItemStack Empty() => new ItemStack(null, 0);

        /// <summary>
        /// Проверить, можно ли стакнуть с другим предметом (одинаковый ItemId)
        /// </summary>
        public bool CanStack(IItemAdapter otherItemAdapter)
        {
            if (ItemAdapter == null || otherItemAdapter == null) return false;
            return ItemAdapter.ItemId == otherItemAdapter.ItemId;
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
                ItemAdapter = null;
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
            return taken > 0 ? new ItemStack(ItemAdapter, taken) : Empty();
        }

        /// <summary>
        /// Заменить предмет в стеке, сохранив количество
        /// Полезно для замены адаптеров (например, при торговле)
        /// </summary>
        /// <param name="newItemAdapter">Новый предмет</param>
        public void ReplaceItem(IItemAdapter newItemAdapter)
        {
            if (newItemAdapter == null)
            {
                Clear();
                return;
            }

            ItemAdapter = newItemAdapter;
            
            ID = newItemAdapter.ItemId;
            DisplayName = newItemAdapter.DisplayName;
            Icon = newItemAdapter.Icon;
            // Count остается тем же
        }

        /// <summary>
        /// Очистить стак
        /// </summary>
        public void Clear()
        {
            ItemAdapter = null;
            Count = 0;
        }
    }
}
