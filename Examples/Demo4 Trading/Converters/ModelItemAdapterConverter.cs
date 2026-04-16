using UniversalDragAndDrop.Core;
using UniversalDragAndDrop.Inventories;

namespace UniversalDragAndDrop.Examples.Trading
{
    /// <summary>
    /// Converts tradable items to the player's model adapter.
    /// </summary>
    public sealed class ModelItemAdapterConverter : IItemAdapterConverter
    {
        public IItemAdapter TryConvertIncoming(IItemAdapter itemAdapter)
        {
            switch (itemAdapter)
            {
                case TradableItemAdapterModelAdapter:
                    return itemAdapter;
                case TradableSoAdapter tradable:
                    return new TradableItemAdapterModelAdapter(new TradableItemModel(tradable.OriginalSO));
                default:
                    return null;
            }
        }

        public IItemAdapter TryConvertOutgoing(IItemAdapter itemAdapter) => itemAdapter;
    }
}