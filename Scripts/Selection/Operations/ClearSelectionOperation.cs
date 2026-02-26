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

        public override void Execute(SelectionManager manager, ISlot contextSlot = null)
            => manager.Clear();

        public override bool CanExecute(SelectionManager manager, ISlot contextSlot = null)
            => base.CanExecute(manager, contextSlot) && manager.CurrentContext.HasSelection;
    }
}
