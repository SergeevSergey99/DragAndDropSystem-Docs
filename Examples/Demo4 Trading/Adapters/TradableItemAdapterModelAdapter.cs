using DragAndDropSystem.Core;
using DragAndDropSystem.Examples.Trading.Data;
using UnityEngine;

namespace DragAndDropSystem.Examples.Trading
{
    /// <summary>
    /// Adapter for TradableItemSO implementing IItemAdapter
    /// Used to integrate tradable items with the drag-and-drop system
    /// </summary>
    public class TradableItemAdapterModelAdapter : IItemAdapter, IDescribable, ITradableItem
    {
        public TradableItemModel Item { get; }
        public TradableItemSO OriginalSO => Item.originalSO;

        public TradableItemAdapterModelAdapter(TradableItemModel item)
        {
            Item = item;
        }

        // IItemAdapter implementation
        public string ItemId => Item.originalSO.GetInstanceID().ToString();
        public string DisplayName => Item.originalSO.DisplayName;
        public Sprite Icon => Item.originalSO.Icon;

        // Additional properties for trading
        public ItemType ItemType => Item.originalSO.ItemType;
        public int BuyPrice => Item.originalSO.BuyPrice;
        public int SellPrice => Item.originalSO.SellPrice;
        public string Description => Item.originalSO.Description + $"\n\nPrice: {SellPrice}";

        public override string ToString()
        {
            return $"{DisplayName} (Buy: {BuyPrice}g, Sell: {SellPrice}g)";
        }
    }
}