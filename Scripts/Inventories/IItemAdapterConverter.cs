using DragAndDropSystem.Core;

namespace DragAndDropSystem.Inventories
{
    /// <summary>
    /// Конвертирует предметы при выходе из инвентаря и при входе в него.
    /// </summary>
    public interface IItemAdapterConverter
    {
        bool TryConvertIncoming(IItemAdapter itemAdapter, out IItemAdapter converted);
        bool TryConvertOutgoing(IItemAdapter itemAdapter, out IItemAdapter converted);
    }
}
