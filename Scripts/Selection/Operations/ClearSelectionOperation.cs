using DragAndDropSystem.Slots;

namespace DragAndDropSystem.Selection
{
    /// <summary>
    /// Снимает всё выделение
    /// </summary>
    [System.Serializable]
    public class ClearSelectionOperation : SelectionOperationBase
    {
        public override string DisplayName => "Clear Selection";

        public override void Execute(SelectionManager manager, BaseSlot contextBaseSlot = null)
            => manager.Clear();

        public override bool CanExecute(SelectionManager manager, BaseSlot contextBaseSlot = null)
            => base.CanExecute(manager, contextBaseSlot) && manager.CurrentContext.HasSelection;
    }
}
