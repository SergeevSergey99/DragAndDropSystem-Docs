using DragAndDropSystem.Core;
using DragAndDropSystem.Inventories;

namespace DragAndDropSystem.Examples.Trading
{
    /// <summary>
    /// Converts incoming tradable items to the merchant's SO adapter.
    /// </summary>
    public sealed class MerchantItemAdapterConverter : IItemAdapterConverter
    {
        public IItemAdapter TryConvertIncoming(IItemAdapter itemAdapter)
        {
            switch (itemAdapter)
            {
                case TradableSoAdapter:
                    return itemAdapter;
                case ITradableItem tradable:
                    return new TradableSoAdapter(tradable.OriginalSO);
                default:
                    return null;
            }
        }

        public IItemAdapter TryConvertOutgoing(IItemAdapter itemAdapter) => itemAdapter;
    }
}