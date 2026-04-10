using System.Collections.Generic;
using DragAndDropSystem.Core;
using DragAndDropSystem.DataBinding;
using DragAndDropSystem.Examples.Trading.Data;
using DragAndDropSystem.Tools.Inspector;
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
        [SerializeField, Tooltip("Merchant ID (must match the ID in TradingEconomyManager)")]
        private string _merchantId;

        [FoldoutGroup("UI References")] [SerializeField, Tooltip("Text for displaying the merchant's name")]
        private TextMeshProUGUI _merchantNameText;

        [FoldoutGroup("UI References")] [SerializeField, Tooltip("Text for displaying the merchant's money")]
        private TextMeshProUGUI _moneyText;

        [FoldoutGroup("Settings")] [SerializeField, Tooltip("Prefix for displaying money (e.g. 'Gold: ')")]
        private string _moneyPrefix = "Gold: ";

        [FoldoutGroup("Settings")] [SerializeField, Tooltip("Suffix for displaying money (e.g. 'g')")]
        private string _moneySuffix = "g";

        private MerchantData _merchantData;
        private MerchantData MerchantData => _merchantData ??= TradingEconomyManager.AutoCreateInstance.GetMerchant(_merchantId);

        protected override IItemAdapterConverter CreateItemConverter() => new MerchantItemAdapterConverter();

        // --- ListInventoryDataBinding примитивы ---

        protected override IReadOnlyList<TradableItemSO> GetItems() => MerchantData?.Inventory;
        protected override TradableSoAdapter CreateAdapter(TradableItemSO item) => new(item);

        // --- Правила ---
        protected override RuleResult CanStartDrag(DragContext context, DragEntry entry) => RuleResult.Success();
        protected override RuleResult CanDrop(DragContext context, DragEntry entry) => TradingHelper.ValidateMerchantDrop(entry);

        protected override void AddToData(TradableSoAdapter adapter) => MerchantData.AddItem(adapter.Item);
        protected override void RemoveFromData(TradableSoAdapter adapter) => MerchantData.TryRemoveItem(adapter.Item);

        public RuleResult CanCommitTransfer(TransferDomainContext context) => TradingHelper.ValidateMerchantTransfer(context, MerchantData);

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
