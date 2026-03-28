using System;
using DragAndDropSystem.Core;
using DragAndDropSystem.Inventories;
using UnityEngine;
using UnityEngine.EventSystems;

namespace DragAndDropSystem.Interaction
{
    /// <summary>
    /// Привязать к Down фазе. Начинает подсчёт удержания на слоте.
    /// Работает в паре с StartHoldDragAction (BeginDrag фаза).
    /// Настройки берутся из HoldDragSettings SO, указанного в InputEventRouter.
    /// </summary>
    [Serializable]
    public sealed class StartHoldCountAction : AssetSafeSlotInteractionAction
    {
        public override bool CanExecute(UniversalInventory inventory, SlotInputAdapter adapter, PointerEventData eventData)
        {
            if (DragAndDropManager.AutoCreateInstance.IsDragging)
                return false;

            var slot = adapter?.Slot;
            return slot != null && !slot.IsEmpty && slot.IsInteractable;
        }

        public override ActionResult Execute(UniversalInventory inventory, SlotInputAdapter adapter, PointerEventData eventData)
        {
            var slot = adapter?.Slot;
            if (slot == null || slot.IsEmpty || !slot.IsInteractable)
                return ActionResult.Failed("Slot is empty or not interactable");

            InputEventRouter.AutoCreateInstance.BeginHoldCount(inventory, slot);
            return ActionResult.Succeeded();
        }
    }

    /// <summary>
    /// Привязать к BeginDrag ��азе. Стартует драг с количеством, накопленным за время удер��ания.
    /// Работает в паре с StartHoldCountAction (Down фаза).
    /// Настройки берутся из HoldDragSettings SO, указанного в InputEventRouter.
    /// </summary>
    [Serializable]
    public sealed class StartHoldDragAction : AssetSafeSlotInteractionAction
    {
        public override bool IsDragBinding() => true;

        public override bool CanExecute(UniversalInventory inventory, SlotInputAdapter adapter, PointerEventData eventData)
        {
            if (DragAndDropManager.AutoCreateInstance.IsDragging)
                return false;

            var slot = adapter?.Slot;
            return slot != null && !slot.IsEmpty && slot.IsInteractable;
        }

        public override ActionResult Execute(UniversalInventory inventory, SlotInputAdapter adapter, PointerEventData eventData)
        {
            var slot = adapter?.Slot;
            if (slot == null || slot.IsEmpty || !slot.IsInteractable)
                return ActionResult.Failed("Slot is empty or not interactable");

            int amount = InputEventRouter.AutoCreateInstance.GetHoldDragAmount(slot);
            var policy = new DragRequestPolicy(DragAmount.Custom, amount);

            return DragAndDropManager.AutoCreateInstance.StartDrag(slot, policy)
                ? ActionResult.Succeeded()
                : ActionResult.Failed("Start drag failed");
        }
    }
}
