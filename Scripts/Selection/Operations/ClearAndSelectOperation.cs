using DragAndDropSystem.Slots;

namespace DragAndDropSystem.Selection
{
    /// <summary>
    /// Сбрасывает выделение и выделяет только contextSlot (обычный клик без модификаторов).
    /// Если кликнутый слот уже является единственным выделенным — снимает выделение с него.
    /// </summary>
    [System.Serializable]
    public class ClearAndSelectOperation : SelectionOperationBase
    {
        public override string DisplayName => "Clear And Select";

        public override void Execute(SelectionManager manager, ISlot contextSlot = null)
        {
            if (contextSlot == null)
            {
                manager.Clear();
                return;
            }

            // Если этот слот уже единственный выделенный — снимаем
            bool isOnlySelected = manager.CurrentContext.TotalSlotsCount == 1
                                  && manager.IsSelected(contextSlot);
            if (isOnlySelected)
            {
                manager.Clear();
                return;
            }

            manager.Clear();
            manager.Select(contextSlot);
        }

        public override bool CanExecute(SelectionManager manager, ISlot contextSlot = null)
            => base.CanExecute(manager, contextSlot);
    }
}
