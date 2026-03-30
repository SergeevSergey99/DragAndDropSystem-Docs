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
        bool TryAddQuite(List<ISlot> slots, ItemStack stack, int targetIndex);
        bool TryAdd(List<ISlot> slots, ItemStack stack, int targetIndex, bool skipRules = false);
        bool TryRemove(List<ISlot> slots, IItemAdapter itemAdapter, int count, int sourceIndex);
        bool TryAddToSlot(List<ISlot> slots, ItemStack stack, ISlot targetSlot, System.Action ensureFreeSlots, SlotOperationContext operationContext);
        bool RequiresStrategyPlacement(ItemStack stack);
        bool UsesPerItemSlotPlanning { get; }
        bool CanUseAlternativeSlot(ISlot slot, IItemAdapter itemAdapter);
        IEnumerable<ISlot> EnumerateAlternativeSlots(List<ISlot> slots, IItemAdapter itemAdapter, AlternativePlacementMode mode, ISlot excludeSlot);
    }
}
