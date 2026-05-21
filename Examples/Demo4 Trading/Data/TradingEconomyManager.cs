using System;
using System.Collections.Generic;
using CodeUtils;
using UDND.Examples.Trading.Data;
using UnityEngine;
using UDND.Tools.Inspector;

namespace UDND.Examples.Trading
{
    /// <summary>
    /// Centralized economy manager for the trading system
    /// Manages money and goods for the player and merchants
    ///
    /// EXAMPLE: Demonstrates a centralized data model with transactions between different actors
    /// </summary>
    public class TradingEconomyManager : MonoSingleton<TradingEconomyManager>
    {
        [TitleGroup("Player Data")]
        [SerializeField]
        private PlayerData _playerData;

        [TitleGroup("Merchants"), SerializeField]
        private List<Merchant> _merchants = new ();

        public PlayerData PlayerData => _playerData;

        /// <summary>
        /// Get merchant data by ID
        /// </summary>
        public MerchantData GetMerchant(string merchantId)
        {
            var merchant = _merchants.Find(x => x.id.Equals(merchantId));
            if (merchant != null)
            {
                return merchant.data;
            }
            
            Debug.LogError($"[TradingEconomyManager] Merchant '{merchantId}' not found!");
            return null;
        }

        /// <summary>
        /// Check whether the player has enough money to buy
        /// </summary>
        public bool CanPlayerAfford(int price) => _playerData.Money >= price;

        /// <summary>
        /// Player buys an item from a merchant
        /// </summary>
        public bool TryBuyFromMerchant(string merchantId, TradableItemSO item, int count)
        {
            var merchant = GetMerchant(merchantId);
            if (merchant == null)
                return false;

            int totalPrice = item.BuyPrice * count;

            // Check whether the player has enough money
            if (!CanPlayerAfford(totalPrice))
            {
                Debug.LogWarning($"[TradingEconomyManager] Player cannot afford {item.DisplayName} x{count} (need {totalPrice}g, has {_playerData.Money}g)");
                return false;
            }

            // Check whether the merchant has the item
            if (merchant.GetItemCount(item) < count)
            {
                Debug.LogWarning($"[TradingEconomyManager] Merchant doesn't have enough {item.DisplayName} (need {count}, has {merchant.GetItemCount(item)})");
                return false;
            }

            // Execute the transaction
            _playerData.TrySpendMoney(totalPrice);
            merchant.AddMoney(totalPrice);
            merchant.TryRemoveItem(item);
            _playerData.AddItem(new TradableItemModel(item));
            return true;
        }

        [Serializable]
        public class Merchant
        { 
            [field: SerializeField]
            public string id  { get; private set; }
            [field: SerializeField]
            public MerchantData  data { get; private set; }
        }
    }
}