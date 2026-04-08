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

        public override void Execute(SelectionManager manager, BaseSlot contextBaseSlot = null)
        {
            if (contextBaseSlot != null)
                manager.Select(contextBaseSlot);
        }

        public override bool CanExecute(SelectionManager manager, BaseSlot contextBaseSlot = null)
            => base.CanExecute(manager, contextBaseSlot) && contextBaseSlot != null;
    }
}
