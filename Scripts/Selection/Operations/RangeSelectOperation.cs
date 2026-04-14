using DragAndDropSystem.Slots;

namespace DragAndDropSystem.Selection
{
    /// <summary>
    /// Selects a range of slots from the last selected one to contextSlot (Shift+Click).
    /// If there is no last selected slot or it belongs to another inventory, only contextSlot is selected.
    /// </summary>
    [System.Serializable]
    public class RangeSelectOperation : SelectionOperationBase
    {
        public override string DisplayName => "Range Select";

        public override void Execute(SelectionManager manager, BaseSlot contextBaseSlot = null)
        {
            if (contextBaseSlot != null)
                manager.SelectRange(contextBaseSlot);
        }

        public override bool CanExecute(SelectionManager manager, BaseSlot contextBaseSlot = null)
            => base.CanExecute(manager, contextBaseSlot) && contextBaseSlot != null;
    }
}