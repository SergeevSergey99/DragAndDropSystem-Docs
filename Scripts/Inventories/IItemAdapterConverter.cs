using DragAndDropSystem.Core;

namespace DragAndDropSystem.Inventories
{
    /// <summary>
    /// Конвертирует предметы при выходе из инвентаря и при входе в него.
    /// </summary>
    public interface IItemAdapterConverter
    {
        IItemAdapter TryConvertIncoming(IItemAdapter itemAdapter);
        IItemAdapter TryConvertOutgoing(IItemAdapter itemAdapter);
    }
}
