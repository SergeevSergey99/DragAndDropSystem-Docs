using DragAndDropSystem.Slots;

namespace DragAndDropSystem.Selection
{
    /// <summary>
    /// Переключает выделение одного слота (Ctrl+Click)
    /// </summary>
    [System.Serializable]
    public class ToggleSlotOperation : SelectionOperationBase
    {
        public override string DisplayName => "Toggle Slot";

        public override void Execute(SelectionManager manager, ISlot contextSlot = null)
        {
            if (contextSlot != null)
                manager.Toggle(contextSlot);
        }

        public override bool CanExecute(SelectionManager manager, ISlot contextSlot = null)
            => base.CanExecute(manager, contextSlot) && contextSlot != null;
    }
}
