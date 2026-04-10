using DragAndDropSystem.Slots;

namespace DragAndDropSystem.Selection
{
    /// <summary>
    /// Clears selection and selects only contextSlot (regular click without modifiers).
    /// If the clicked slot is already the only selected one, it deselects it.
    /// </summary>
    [System.Serializable]
    public class ClearAndSelectOperation : SelectionOperationBase
    {
        public override string DisplayName => "Clear And Select";

        public override void Execute(SelectionManager manager, BaseSlot contextBaseSlot = null)
        {
            if (contextBaseSlot == null)
            {
                manager.Clear();
                return;
            }

            // If this slot is already the only selected one, deselect it
            bool isOnlySelected = manager.CurrentContext.TotalSlotsCount == 1
                                  && manager.IsSelected(contextBaseSlot);
            if (isOnlySelected)
            {
                manager.Clear();
                return;
            }

            manager.Clear();
            manager.Select(contextBaseSlot);
        }

        public override bool CanExecute(SelectionManager manager, BaseSlot contextBaseSlot = null)
            => base.CanExecute(manager, contextBaseSlot);
    }
}
