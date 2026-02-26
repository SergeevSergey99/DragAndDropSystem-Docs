using DragAndDropSystem.Slots;

namespace DragAndDropSystem.Selection
{
    /// <summary>
    /// Добавляет один слот к выделению без снятия других (без модификатора).
    /// Если нужно сначала сбросить выделение — используйте ClearAndSelectOperation.
    /// </summary>
    [System.Serializable]
    public class SelectSlotOperation : SelectionOperationBase
    {
        public override string DisplayName => "Select Slot";

        public override void Execute(SelectionManager manager, ISlot contextSlot = null)
        {
            if (contextSlot != null)
                manager.Select(contextSlot);
        }

        public override bool CanExecute(SelectionManager manager, ISlot contextSlot = null)
            => base.CanExecute(manager, contextSlot) && contextSlot != null;
    }
}
