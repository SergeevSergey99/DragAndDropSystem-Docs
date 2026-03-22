using System.Collections.Generic;
using DragAndDropSystem.Core;
using DragAndDropSystem.Slots;

namespace DragAndDropSystem.Inventories
{
    /// <summary>
    /// Отвечает за размещение, удаление и slot-level особенности поведения стратегии.
    /// </summary>
    public interface IPlacementStrategy
    {
        bool TryAdd(List<ISlot> slots, ItemStack stack, int targetIndex);
        bool TryRemove(List<ISlot> slots, IInventoryItem item, int count, int sourceIndex);
        bool TryAddToSlot(List<ISlot> slots, ItemStack stack, ISlot targetSlot, System.Action ensureFreeSlots, SlotOperationContext operationContext);
        bool RequiresStrategyPlacement(ItemStack stack);
        bool UsesPerItemSlotPlanning { get; }
        bool CanUseAlternativeSlot(ISlot slot, IInventoryItem item);
    }
}
