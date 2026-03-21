using System.Collections.Generic;
using DragAndDropSystem.Core;
using DragAndDropSystem.Slots;

namespace DragAndDropSystem.Inventories
{

    /// <summary>
    /// Стратегия управления слотами инвентаря
    /// </summary>
    public interface IInventoryStrategy
    {
        /// <summary>
        /// Попытаться добавить стак в инвентарь
        /// </summary>
        bool TryAdd(List<ISlot> slots, ItemStack stack, int targetIndex);

        /// <summary>
        /// Попытаться удалить предмет из инвентаря
        /// </summary>
        bool TryRemove(List<ISlot> slots, IInventoryItem item, int count, int sourceIndex);

        /// <summary>
        /// Получить количество предмета в инвентаре
        /// </summary>
        int GetItemCount(List<ISlot> slots, IInventoryItem item);

        /// <summary>
        /// Проверить наличие предмета
        /// </summary>
        bool Contains(List<ISlot> slots, IInventoryItem item);

        /// <summary>
        /// Рассчитать количество предметов для drag операции.
        /// </summary>
        int ResolveDragAmount(int stackCount, UniversalInventory.DragAmountType dragAmount, int customDragAmount);

        /// <summary>
        /// Требуется ли разместить стак через стратегию, а не в один конкретный слот.
        /// </summary>
        bool RequiresStrategyPlacement(ItemStack stack);

        /// <summary>
        /// Использует ли стратегия поштучное распределение по слотам при планировании.
        /// </summary>
        bool UsesPerItemSlotPlanning { get; }

        /// <summary>
        /// Проверить, может ли слот использоваться как альтернативный target.
        /// </summary>
        bool CanUseAlternativeSlot(ISlot slot, IInventoryItem item);

        /// <summary>
        /// Попытаться добавить стак в конкретный слот по правилам стратегии.
        /// </summary>
        bool TryAddToSlot(List<ISlot> slots, ItemStack stack, ISlot targetSlot, System.Action ensureFreeSlots, SlotOperationContext operationContext);

        /// <summary>
        /// Проверить, может ли инвентарь принять предмет и вернуть suggested slot.
        /// </summary>
        bool CanAcceptItem(List<ISlot> slots, IInventoryItem item, int desiredCount, bool canCreateNewSlot, int potentialNewSlots, ISlot slotPrefab, out ISlot suggestedSlot);

        /// <summary>
        /// Получить количество предметов, которое стратегия может принять.
        /// </summary>
        int GetAcceptableCount(List<ISlot> slots, IInventoryItem item, int desiredCount, bool canCreateNewSlot, int potentialNewSlots, ISlot slotPrefab);
    }

}