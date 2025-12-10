using DragAndDropSystem.Core;
using DragAndDropSystem.Examples.Trading.Data;
using UnityEngine;

namespace DragAndDropSystem.Examples.Trading
{
    /// <summary>
    /// Адаптер для TradableItemSO, реализующий интерфейс IInventoryItem
    /// Используется для интеграции торговых предметов с системой drag and drop
    /// </summary>
    public class TradableItemModelAdapter : IInventoryItem, IDescribable
    {
        public TradableItemModel Item { get; }

        public TradableItemModelAdapter(TradableItemModel item)
        {
            Item = item;
        }

        // IInventoryItem implementation
        public string ItemId => Item.originalSO.GetInstanceID().ToString();
        public string DisplayName => Item.originalSO.DisplayName;
        public Sprite Icon => Item.originalSO.Icon;

        // Дополнительные свойства для торговли
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
