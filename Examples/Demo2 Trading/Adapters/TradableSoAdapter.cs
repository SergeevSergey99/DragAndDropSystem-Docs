using DragAndDropSystem.Core;
using DragAndDropSystem.Examples.Trading.Data;
using UnityEngine;

namespace DragAndDropSystem.Examples.Trading
{
    /// <summary>
    /// Адаптер для TradableItemSO, реализующий интерфейс IInventoryItem
    /// Используется для интеграции торговых предметов с системой drag and drop
    /// </summary>
    public class TradableSoAdapter : IInventoryItem, IDescribable, ITradableItem
    {
        public TradableItemSO Item { get; }
        public TradableItemSO OriginalSO => Item;

        public TradableSoAdapter(TradableItemSO item)
        {
            Item = item;
        }

        // IInventoryItem implementation
        public string ItemId => Item.GetInstanceID().ToString();
        public string DisplayName => Item.DisplayName;
        public Sprite Icon => Item.Icon;

        // Дополнительные свойства для торговли
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
