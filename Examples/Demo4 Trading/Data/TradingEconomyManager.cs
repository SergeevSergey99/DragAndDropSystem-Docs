using System;
using System.Collections.Generic;
using CodeUtils;
using DragAndDropSystem.Examples.Trading.Data;
using DragAndDropSystem.Inspector;
using DragAndDropSystem.Tools;
using Plugins.DragAndDropSystem.Examples.Trading.Data;
using UnityEngine;

namespace DragAndDropSystem.Examples.Trading
{
    /// <summary>
    /// Централизованный менеджер экономики для системы торговли
    /// Управляет деньгами и товарами игрока и торговцев
    ///
    /// ПРИМЕР: Демонстрирует централизованную модель данных с транзакциями между разными субъектами
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
        /// Получить данные торговца по ID
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
        /// Проверить, достаточно ли у игрока денег для покупки
        /// </summary>
        public bool CanPlayerAfford(int price) => _playerData.Money >= price;

        /// <summary>
        /// Игрок покупает предмет у торговца
        /// </summary>
        public bool TryBuyFromMerchant(string merchantId, TradableItemSO item, int count)
        {
            var merchant = GetMerchant(merchantId);
            if (merchant == null)
                return false;

            int totalPrice = item.BuyPrice * count;

            // Проверяем достаточно ли денег у игрока
            if (!CanPlayerAfford(totalPrice))
            {
                Debug.LogWarning($"[TradingEconomyManager] Player cannot afford {item.DisplayName} x{count} (need {totalPrice}g, has {_playerData.Money}g)");
                return false;
            }

            // Проверяем есть ли товар у торговца
            if (merchant.GetItemCount(item) < count)
            {
                Debug.LogWarning($"[TradingEconomyManager] Merchant doesn't have enough {item.DisplayName} (need {count}, has {merchant.GetItemCount(item)})");
                return false;
            }

            // Выполняем транзакцию
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
