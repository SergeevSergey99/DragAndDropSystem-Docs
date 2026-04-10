using DragAndDropSystem.Examples.Trading.Data;
using DragAndDropSystem.Tools.Inspector;
using UnityEngine;

namespace DragAndDropSystem.Examples.Trading
{
    /// <summary>
    /// ScriptableObject for a tradable item with buy/sell prices
    /// Used in the trading system example with merchants
    /// </summary>
    [CreateAssetMenu(fileName = "TradableItem", menuName = "DragAndDrop/Examples/Trading/TradableItemSO", order = 200)]
    public class TradableItemSO : ScriptableObject
    {
        [Header("Basic Info")]
        [SerializeField] private string _displayName;
        [SerializeField, PreviewField(100)] private Sprite _icon;

        [SerializeField, Tooltip("Item type (weapon, armor, artifact, etc.)")]
        private ItemType _itemType = ItemType.Other;

        [Header("Trading")]
        [SerializeField, Tooltip("Buy price from the merchant (paid by the player)")]
        private int _buyPrice = 100;

        [SerializeField, Tooltip("Sell price to the merchant (paid by the merchant)")]
        private int _sellPrice = 50;

        [Header("Description")]
        [SerializeField, TextArea(3, 5)]
        private string _description;

        public string DisplayName => _displayName;
        public Sprite Icon => _icon;
        public ItemType ItemType => _itemType;
        public int BuyPrice => _buyPrice;
        public int SellPrice => _sellPrice;
        public string Description => _description;

        private void OnValidate()
        {
            // Check that sell price is not higher than buy price
            if (_sellPrice > _buyPrice)
            {
                Debug.LogWarning($"[{name}] Sell price ({_sellPrice}) is higher than buy price ({_buyPrice})! This may be illogical.");
            }
        }
    }
}
