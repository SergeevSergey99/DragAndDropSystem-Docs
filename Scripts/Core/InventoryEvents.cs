using System;
using DragAndDropSystem.Inventories;
using DragAndDropSystem.Slots;

namespace DragAndDropSystem.Core
{
    /// <summary>
    /// Аргументы событий инвентаря (добавление/удаление предметов)
    /// </summary>
    public class InventoryItemEventContext
    {
        public ItemStack Stack { get; }
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
        public BaseSlot SourceBaseSlot { get; }

        /// <summary>
        /// Слот-назначение (куда положили предмет)
        /// Null если слот неизвестен или недоступен
        /// </summary>
        public BaseSlot TargetBaseSlot { get; }

        public InventoryItemEventContext(
            ItemStack stack,
            int slotIndex = -1,
            IInventory sourceInventory = null,
            IInventory targetInventory = null,
            BaseSlot sourceBaseSlot = null,
            BaseSlot targetBaseSlot = null)
        {
            Stack = stack ?? ItemStack.Empty();
            SlotIndex = slotIndex;
            SourceInventory = sourceInventory;
            TargetInventory = targetInventory;
            SourceBaseSlot = sourceBaseSlot;
            TargetBaseSlot = targetBaseSlot;
        }
    }

    /// <summary>
    /// Аргументы событий обмена предметов (swap)
    /// </summary>
    public class InventorySwapContext
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
        public BaseSlot SourceBaseSlot { get; }

        /// <summary>
        /// Целевой слот (куда хотим бросить)
        /// </summary>
        public BaseSlot TargetBaseSlot { get; }

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

        public InventorySwapContext(
            ItemStack sourceStack,
            ItemStack targetStack,
            BaseSlot sourceBaseSlot,
            BaseSlot targetBaseSlot,
            IInventory sourceInventory,
            IInventory targetInventory)
        {
            SourceStack = sourceStack;
            TargetStack = targetStack;
            SourceBaseSlot = sourceBaseSlot;
            TargetBaseSlot = targetBaseSlot;
            SourceInventory = sourceInventory;
            TargetInventory = targetInventory;
            Cancel = false;
        }
    }
}
