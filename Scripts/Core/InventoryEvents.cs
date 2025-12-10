using System;
using DragAndDropSystem.Inventories;
using DragAndDropSystem.Slots;

namespace DragAndDropSystem.Core
{
    /// <summary>
    /// Аргументы событий инвентаря (добавление/удаление предметов)
    /// </summary>
    public class InventoryItemEventArgs : EventArgs
    {
        public IInventoryItem Item { get; }
        public int Count { get; }
        public int SlotIndex { get; }

        /// <summary>
        /// Инвентарь-источник (откуда взяли предмет)
        /// Null если предмет добавлен не из другого инвентаря
        /// </summary>
        public IInventory SourceInventory { get; }

        /// <summary>
        /// Инвентарь-назначение (куда положили предмет)
        /// Null если предмет удалён не в другой инвентарь
        /// </summary>
        public IInventory TargetInventory { get; }

        /// <summary>
        /// Слот-источник (откуда взяли предмет)
        /// Null если слот неизвестен или недоступен
        /// </summary>
        public ISlot SourceSlot { get; }

        /// <summary>
        /// Слот-назначение (куда положили предмет)
        /// Null если слот неизвестен или недоступен
        /// </summary>
        public ISlot TargetSlot { get; }

        public InventoryItemEventArgs(
            IInventoryItem item,
            int count,
            int slotIndex = -1,
            IInventory sourceInventory = null,
            IInventory targetInventory = null,
            ISlot sourceSlot = null,
            ISlot targetSlot = null)
        {
            Item = item;
            Count = count;
            SlotIndex = slotIndex;
            SourceInventory = sourceInventory;
            TargetInventory = targetInventory;
            SourceSlot = sourceSlot;
            TargetSlot = targetSlot;
        }
    }

    /// <summary>
    /// Аргументы событий обмена предметов (swap)
    /// </summary>
    public class InventorySwapEventArgs : EventArgs
    {
        /// <summary>
        /// Стак из исходного слота (будет перемещен в целевой)
        /// </summary>
        public ItemStack SourceStack { get; }

        /// <summary>
        /// Стак из целевого слота (будет перемещен в исходный)
        /// </summary>
        public ItemStack TargetStack { get; }

        /// <summary>
        /// Исходный слот (откуда начали перетаскивание)
        /// </summary>
        public ISlot SourceSlot { get; }

        /// <summary>
        /// Целевой слот (куда хотим бросить)
        /// </summary>
        public ISlot TargetSlot { get; }

        /// <summary>
        /// Исходный инвентарь
        /// </summary>
        public IInventory SourceInventory { get; }

        /// <summary>
        /// Целевой инвентарь
        /// </summary>
        public IInventory TargetInventory { get; }

        /// <summary>
        /// Можно установить в true чтобы отменить swap
        /// </summary>
        public bool Cancel { get; set; }

        public InventorySwapEventArgs(
            ItemStack sourceStack,
            ItemStack targetStack,
            ISlot sourceSlot,
            ISlot targetSlot,
            IInventory sourceInventory,
            IInventory targetInventory)
        {
            SourceStack = sourceStack;
            TargetStack = targetStack;
            SourceSlot = sourceSlot;
            TargetSlot = targetSlot;
            SourceInventory = sourceInventory;
            TargetInventory = targetInventory;
            Cancel = false;
        }
    }
}
