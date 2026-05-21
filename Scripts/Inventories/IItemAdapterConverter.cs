using UDND.Core;

namespace UDND.Inventories
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
