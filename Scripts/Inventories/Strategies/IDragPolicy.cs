using UniversalDragAndDrop.Core;

namespace UniversalDragAndDrop.Inventories
{
    /// <summary>
    /// Responsible for calculating how many items are picked up during a drag operation.
    /// </summary>
    public interface IDragPolicy
    {
        int ResolveDragAmount(int stackCount, DragAmount dragAmount, int customDragAmount);
    }
}
