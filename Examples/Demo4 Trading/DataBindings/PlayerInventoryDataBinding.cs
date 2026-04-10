using System.Collections.Generic;
using DragAndDropSystem.DataBinding;
using DragAndDropSystem.Tools.Inspector;
using DragAndDropSystem.Inventories;
using DragAndDropSystem.Rules;
using Plugins.DragAndDropSystem.Examples.Trading.Data;
using TMPro;
using UnityEngine;

namespace DragAndDropSystem.Examples.Trading
{
    /// <summary>
    /// DataBinding for the player inventory in the trading system.
    /// Uses ListInventoryDataBinding for automatic synchronization of the item list.
    /// Conversion is delegated to a separate inventory-side converter.
    /// </summary>
    public class PlayerInventoryDataBinding : ListInventoryDataBinding<TradableItemModel, TradableItemAdapterModelAdapter>, ITransferDomainHandler
    {
        [FoldoutGroup("UI References")]
        [SerializeField, Tooltip("Text for displaying the player's money")]
        private TextMeshProUGUI _moneyText;

        [FoldoutGroup("Settings")]
        [SerializeField, Tooltip("Prefix for displaying money")]
        private string _moneyPrefix = "Gold: ";

        [FoldoutGroup("Settings")]
        [SerializeField, Tooltip("Suffix for displaying money")]
        private string _moneySuffix = "g";

        private PlayerData PlayerData => TradingEconomyManager.AutoCreateInstance.PlayerData;

        // --- ListInventoryDataBinding primitives ---

        protected override IReadOnlyList<TradableItemModel> GetItems() => PlayerData?.Inventory;
        protected override TradableItemAdapterModelAdapter CreateAdapter(TradableItemModel item) => new(item);
        protected override IItemAdapterConverter CreateItemConverter() => new ModelItemAdapterConverter();

        protected override void AddToData(TradableItemAdapterModelAdapter adapter) => PlayerData.AddItem(adapter.Item);
        protected override void RemoveFromData(TradableItemAdapterModelAdapter adapter) => PlayerData.TryRemoveItem(adapter.Item);

        public RuleResult CanCommitTransfer(TransferDomainContext context) => TradingHelper.ValidatePlayerTransfer(context, PlayerData);

        public void OnTransferSucceeded(TransferDomainContext context) => TradingHelper.ApplyPlayerTransferEffects(context, PlayerData);

        // --- Lifecycle ---

        protected override void OnEnable()
        {
            base.OnEnable();
            if (PlayerData != null)
                PlayerData.OnMoneyChanged += UpdateMoneyUI;
            UpdateMoneyUI();
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            if (TradingEconomyManager.IsInstanceExist && PlayerData != null)
                PlayerData.OnMoneyChanged -= UpdateMoneyUI;
        }

        protected override void OnReloadUI()
        {
            base.OnReloadUI();
            UpdateMoneyUI();
        }

        private void UpdateMoneyUI()
        {
            if (_moneyText != null && PlayerData != null)
                _moneyText.text = $"{_moneyPrefix}{PlayerData.Money}{_moneySuffix}";
        }
    }
}
