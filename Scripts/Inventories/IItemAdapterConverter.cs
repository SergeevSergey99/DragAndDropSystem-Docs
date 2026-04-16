using UniversalDragAndDrop.Core;

namespace UniversalDragAndDrop.Inventories
{
    /// <summary>
    /// Converts items when they leave an inventory and when they enter it.
    /// </summary>
    public interface IItemAdapterConverter
    {
        IItemAdapter TryConvertIncoming(IItemAdapter itemAdapter);
        IItemAdapter TryConvertOutgoing(IItemAdapter itemAdapter);
    }
}
