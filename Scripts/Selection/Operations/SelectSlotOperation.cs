using DragAndDropSystem.Slots;

namespace DragAndDropSystem.Selection
{
    /// <summary>
    /// Adds one slot to selection without clearing others (no modifier).
    /// If selection should be cleared first, use ClearAndSelectOperation.
    /// </summary>
    [System.Serializable]
    public class SelectSlotOperation : SelectionOperationBase
    {
        public override string DisplayName => "Select Slot";

        public override void Execute(SelectionManager manager, BaseSlot contextBaseSlot = null)
        {
            if (contextBaseSlot != null)
                manager.Select(contextBaseSlot);
        }

        public override bool CanExecute(SelectionManager manager, BaseSlot contextBaseSlot = null)
            => base.CanExecute(manager, contextBaseSlot) && contextBaseSlot != null;
    }
}