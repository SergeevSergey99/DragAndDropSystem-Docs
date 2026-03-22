namespace DragAndDropSystem.Inventories
{
    /// <summary>
    /// Отвечает за вычисление количества предметов, захватываемых при drag операции.
    /// </summary>
    public interface IDragPolicy
    {
        int ResolveDragAmount(int stackCount, UniversalInventory.DragAmountType dragAmount, int customDragAmount);
    }
}
