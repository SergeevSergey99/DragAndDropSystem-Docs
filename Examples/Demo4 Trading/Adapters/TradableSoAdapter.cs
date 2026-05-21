using UnityEngine;
using UDND.Core;
using UDND.Examples.Trading.Data;

namespace UDND.Examples.Trading
{
    /// <summary>
    /// Adapter for TradableItemSO implementing IItemAdapter
    /// Used to integrate tradable items with the drag-and-drop system
    /// </summary>
    public class TradableSoAdapter : IItemAdapter, IDescribable, ITradableItem
    {
        public TradableItemSO Item { get; }
        public TradableItemSO OriginalSO => Item;

        public TradableSoAdapter(TradableItemSO item)
        {
            Item = item;
        }

        // IItemAdapter implementation
        public string ItemId => Item.GetInstanceID().ToString();
        public string DisplayName => Item.DisplayName;
        public Sprite Icon => Item.Icon;

        // Additional properties for trading
        public ItemType ItemType => Item.ItemType;
        public int BuyPrice => Item.BuyPrice;
        public int SellPrice => Item.SellPrice;
        public string Description => Item.Description + $"\n\nPrice: {BuyPrice}";

        public override string ToString()
        {
            return $"{DisplayName} (Buy: {BuyPrice}g, Sell: {SellPrice}g)";
        }
    }
}