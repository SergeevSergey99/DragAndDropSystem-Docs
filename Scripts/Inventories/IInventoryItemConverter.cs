using DragAndDropSystem.Core;

namespace DragAndDropSystem.Inventories
{
    /// <summary>
    /// Конвертирует предметы при выходе из инвентаря и при входе в него.
    /// </summary>
    public interface IInventoryItemConverter
    {
        bool TryConvertIncoming(IInventoryItem item, out IInventoryItem converted);
        bool TryConvertOutgoing(IInventoryItem item, out IInventoryItem converted);
    }
}
