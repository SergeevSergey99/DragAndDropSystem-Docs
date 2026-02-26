using DragAndDropSystem.Slots;

namespace DragAndDropSystem.Selection
{
    /// <summary>
    /// Выделяет диапазон слотов от последнего выделенного до contextSlot (Shift+Click).
    /// Если последнего выделенного нет или он из другого инвентаря — выделяет только contextSlot.
    /// </summary>
    [System.Serializable]
    public class RangeSelectOperation : SelectionOperationBase
    {
        public override string DisplayName => "Range Select";

        public override void Execute(SelectionManager manager, ISlot contextSlot = null)
        {
            if (contextSlot != null)
                manager.SelectRange(contextSlot);
        }

        public override bool CanExecute(SelectionManager manager, ISlot contextSlot = null)
            => base.CanExecute(manager, contextSlot) && contextSlot != null;
    }
}
