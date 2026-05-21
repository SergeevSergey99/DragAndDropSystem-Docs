using System;
using UDND.Core;

namespace UDND.Interaction
{
    /// <summary>
    /// Bind to the Down phase. Starts hold counting on the slot.
    /// Works together with StartHoldDragAction (BeginDrag phase).
    /// Settings are taken from the HoldDragSettings SO assigned in InputEventRouter.
    /// </summary>
    [Serializable]
    public sealed class StartHoldCountAction : AssetSafeSlotInteractionAction
    {
        public override bool CanExecute(RuntimeInteractionSnapshot snapshot)
        {
            if (snapshot?.Inventory == null || DragAndDropManager.AutoCreateInstance.IsDragging)
                return false;

            var slot = snapshot.ActiveSlot;
            return slot != null && !slot.IsEmpty && slot.IsInteractable;
        }

        public override ActionResult Execute(RuntimeInteractionSnapshot snapshot)
        {
            var slot = snapshot?.ActiveSlot;
            if (slot == null || slot.IsEmpty || !slot.IsInteractable)
                return ActionResult.Failed("Slot is empty or not interactable");

            InputEventRouter.AutoCreateInstance.BeginHoldCount(snapshot.Inventory, slot);
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
        public override bool IsDragOnlyBinding() => true;

        public override bool CanExecute(RuntimeInteractionSnapshot snapshot)
        {
            if (snapshot?.Inventory == null || DragAndDropManager.AutoCreateInstance.IsDragging)
                return false;

            var slot = snapshot.ActiveSlot;
            return slot != null && !slot.IsEmpty && slot.IsInteractable;
        }

        public override ActionResult Execute(RuntimeInteractionSnapshot snapshot)
        {
            var slot = snapshot?.ActiveSlot;
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
