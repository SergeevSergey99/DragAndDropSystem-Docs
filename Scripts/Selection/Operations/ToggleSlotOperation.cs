using DragAndDropSystem.Slots;

namespace DragAndDropSystem.Selection
{
    /// <summary>
    /// Toggles selection of a single slot (Ctrl+Click)
    /// </summary>
    [System.Serializable]
    public class ToggleSlotOperation : SelectionOperationBase
    {
        public override string DisplayName => "Toggle Slot";

        public override void Execute(SelectionManager manager, BaseSlot contextBaseSlot = null)
        {
            if (contextBaseSlot != null)
                manager.Toggle(contextBaseSlot);
        }

        public override bool CanExecute(SelectionManager manager, BaseSlot contextBaseSlot = null)
            => base.CanExecute(manager, contextBaseSlot) && contextBaseSlot != null;
    }
}