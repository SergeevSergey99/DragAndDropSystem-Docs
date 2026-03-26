using DragAndDropSystem.Core;

namespace DragAndDropSystem.Inventories
{
    /// <summary>
    /// Отвечает за вычисление количества предметов, захватываемых при drag операции.
    /// </summary>
    public interface IDragPolicy
    {
        int ResolveDragAmount(int stackCount, DragAmount dragAmount, int customDragAmount);
    }
}
