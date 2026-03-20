using System.Collections.Generic;
using DragAndDropSystem.Core;
using DragAndDropSystem.DataBinding;
using DragAndDropSystem.Examples.Trading.Data;
using DragAndDropSystem.Inspector;
using DragAndDropSystem.Rules;
using Plugins.DragAndDropSystem.Examples.Trading.Data;
using TMPro;
using UnityEngine;

namespace DragAndDropSystem.Examples.Trading
{
    /// <summary>
    /// DataBinding для инвентаря торговца в системе торговли.
    /// Использует ListInventoryDataBinding для автоматической синхронизации списка предметов.
    /// ConvertIncomingItem конвертирует Model-адаптеры игрока в SO-адаптеры торговца.
    /// </summary>
    public class MerchantInventoryDataBinding : ListInventoryDataBinding<TradableItemSO, TradableSoAdapter>,
        IMerchantInventory
    {
        [FoldoutGroup("Merchant Settings")]
        [SerializeField, Tooltip("ID торговца (должен совпадать с ID в TradingEconomyManager)")]
        private string _merchantId;

        [FoldoutGroup("UI References")] [SerializeField, Tooltip("Текст для отображения имени торговца")]
        private TextMeshProUGUI _merchantNameText;

        [FoldoutGroup("UI References")] [SerializeField, Tooltip("Текст для отображения денег торговца")]
        private TextMeshProUGUI _moneyText;

        [FoldoutGroup("Settings")] [SerializeField, Tooltip("Префикс для отображения денег (например, 'Gold: ')")]
        private string _moneyPrefix = "Gold: ";

        [FoldoutGroup("Settings")] [SerializeField, Tooltip("Суффикс для отображения денег (например, 'g')")]
        private string _moneySuffix = "g";

        private MerchantData _merchantData;
        private MerchantData MerchantData => _merchantData ??= TradingEconomyManager.Instance.GetMerchant(_merchantId);
        private PlayerData PlayerData => TradingEconomyManager.Instance.PlayerData;

        // --- ListInventoryDataBinding примитивы ---

        protected override IReadOnlyList<TradableItemSO> GetItems() => MerchantData?.Inventory;
        protected override TradableSoAdapter CreateAdapter(TradableItemSO item) => new(item);
        protected override TradableItemSO ExtractData(TradableSoAdapter adapter) => adapter.Item;

        // --- Правила ---
        protected override RuleResult CanStartDrag(DragContext context, DragEntry entry) => TradingHelper.ValidatePurchaseFromMerchant(entry, PlayerData);
        protected override RuleResult CanDrop(DragContext context, DragEntry entry) => TradingHelper.ValidateSellToMerchant(entry, MerchantData);

        protected override void AddToData(InventoryItemEventContext context, TradableItemSO item)
        {
            // Если предмет пришел от игрока — торговец платит за выкуп
            if (context.Item is ITradableItem tradable)
            {
                MerchantData.TrySpendMoney(tradable.SellPrice * context.Count);
                MerchantData.AddItem(item);
            }
        }

        protected override void RemoveFromData(InventoryItemEventContext context, TradableItemSO item)
        {
            // Если предмет ушел к игроку — торговец получает деньги за продажу
            if (context.Item is ITradableItem tradable)
            {
                MerchantData.AddMoney(tradable.BuyPrice * context.Count);
                MerchantData.TryRemoveItem(item);
            }
        }

        // --- Lifecycle ---

        protected override void OnEnable()
        {
            base.OnEnable();

            if (MerchantData != null)
            {
                MerchantData.OnMoneyChanged += UpdateMoneyUI;
                _merchantNameText.text = MerchantData.DisplayName;
            }

            UpdateMoneyUI();
        }

        protected override void OnDisable()
        {
            base.OnDisable();

            if (MerchantData != null)
            {
                MerchantData.OnMoneyChanged -= UpdateMoneyUI;
            }
        }

        protected override void OnReloadUI()
        {
            base.OnReloadUI();
            UpdateMoneyUI();
        }

        private void UpdateMoneyUI()
        {
            if (_moneyText != null && MerchantData != null)
            {
                _moneyText.text = $"{_moneyPrefix}{MerchantData.Money}{_moneySuffix}";
            }
        }

        // --- Конвертация: Model-адаптер игрока → SO-адаптер торговца ---
        internal override IInventoryItem ConvertIncomingItem(IInventoryItem item)
        {
            if (item is TradableSoAdapter) return item;
            if (item is ITradableItem tradable)
                return new TradableSoAdapter(tradable.OriginalSO);
            return null; // Неизвестный тип предмета, не конвертируем, запрещаем
        }
    }
}
