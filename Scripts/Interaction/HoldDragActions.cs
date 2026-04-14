using System;
using DragAndDropSystem.Core;
using DragAndDropSystem.Inventories;
using UnityEngine.EventSystems;

namespace DragAndDropSystem.Interaction
{
    /// <summary>
    /// Bind to the Down phase. Starts hold counting on the slot.
    /// Works together with StartHoldDragAction (BeginDrag phase).
    /// Settings are taken from the HoldDragSettings SO assigned in InputEventRouter.
    /// </summary>
    [Serializable]
    public sealed class StartHoldCountAction : AssetSafeSlotInteractionAction
    {
        public override bool CanExecute(UniversalInventory inventory, SlotInputAdapter adapter, PointerEventData eventData)
        {
            if (DragAndDropManager.AutoCreateInstance.IsDragging)
                return false;

            var slot = adapter?.BaseSlot;
            return slot != null && !slot.IsEmpty && slot.IsInteractable;
        }

        public override ActionResult Execute(UniversalInventory inventory, SlotInputAdapter adapter, PointerEventData eventData)
        {
            var slot = adapter?.BaseSlot;
            if (slot == null || slot.IsEmpty || !slot.IsInteractable)
                return ActionResult.Failed("Slot is empty or not interactable");

            InputEventRouter.AutoCreateInstance.BeginHoldCount(inventory, slot);
            return ActionResult.Succeeded();
        }
    }

    /// <summary>
    /// Bind to the BeginDrag phase. Starts drag with the amount accumulated during hold.
    /// Works together with StartHoldCountAction (Down phase).
    /// Settings are taken from the HoldDragSettings SO assigned in InputEventRouter.
    /// </summary>
    [Serializable]
    public sealed class StartHoldDragAction : AssetSafeSlotInteractionAction
    {
        public override bool IsDragBinding() => true;

        public override bool CanExecute(UniversalInventory inventory, SlotInputAdapter adapter, PointerEventData eventData)
        {
            if (DragAndDropManager.AutoCreateInstance.IsDragging)
                return false;

            var slot = adapter?.BaseSlot;
            return slot != null && !slot.IsEmpty && slot.IsInteractable;
        }

        public override ActionResult Execute(UniversalInventory inventory, SlotInputAdapter adapter, PointerEventData eventData)
        {
            var slot = adapter?.BaseSlot;
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