using System.Collections.Generic;
using DragAndDropSystem.Core;
using DragAndDropSystem.DataBinding;
using DragAndDropSystem.Examples.Trading.Data;
using DragAndDropSystem.Inspector;
using DragAndDropSystem.Inventories;
using DragAndDropSystem.Rules;
using Plugins.DragAndDropSystem.Examples.Trading.Data;
using TMPro;
using UnityEngine;

namespace DragAndDropSystem.Examples.Trading
{
    /// <summary>
    /// DataBinding для инвентаря торговца в системе торговли.
    /// Использует ListInventoryDataBinding для автоматической синхронизации списка предметов.
    /// Conversion вынесен в отдельный inventory-side converter.
    /// </summary>
    public class MerchantInventoryDataBinding : ListInventoryDataBinding<TradableItemSO, TradableSoAdapter>,
        IMerchantInventory,
        ITransferDomainHandler
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

        protected override IInventoryItemConverter CreateItemConverter() => new MerchantInventoryItemConverter();

        // --- ListInventoryDataBinding примитивы ---

        protected override IReadOnlyList<TradableItemSO> GetItems() => MerchantData?.Inventory;
        protected override TradableSoAdapter CreateAdapter(TradableItemSO item) => new(item);
        protected override TradableItemSO ExtractData(TradableSoAdapter adapter) => adapter.Item;

        // --- Правила ---
        protected override RuleResult CanStartDrag(DragContext context, DragEntry entry) => RuleResult.Success();
        protected override RuleResult CanDrop(DragContext context, DragEntry entry) => TradingHelper.ValidateMerchantDrop(entry);

        protected override void AddToData(InventoryItemEventContext context, TradableItemSO item)
        {
            MerchantData.AddItem(item);
        }

        protected override void RemoveFromData(InventoryItemEventContext context, TradableItemSO item)
        {
            MerchantData.TryRemoveItem(item);
        }

        public RuleResult Validate(TransferDomainContext context) => TradingHelper.ValidateMerchantTransfer(context, MerchantData);

        public void OnTransferSucceeded(TransferDomainContext context) => TradingHelper.ApplyMerchantTransferEffects(context, MerchantData);

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
    }
}
