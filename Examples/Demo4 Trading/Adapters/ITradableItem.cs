using UniversalDragAndDrop.Examples.Trading.Data;

namespace UniversalDragAndDrop.Examples.Trading
{
    /// <summary>
    /// Common interface for tradable items.
    /// Implemented by both adapters (TradableSoAdapter and TradableItemAdapterModelAdapter),
    /// allowing code to work with price and item type without depending on a specific adapter.
    /// </summary>
    public interface ITradableItem
    {
        ItemType ItemType { get; }
        int BuyPrice { get; }
        int SellPrice { get; }
        TradableItemSO OriginalSO { get; }
    }
}