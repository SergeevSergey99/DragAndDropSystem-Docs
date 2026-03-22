using System.Collections.Generic;
using DragAndDropSystem.Core;
using DragAndDropSystem.DataBinding;
using DragAndDropSystem.Inspector;
using DragAndDropSystem.Inventories;
using DragAndDropSystem.Rules;
using Plugins.DragAndDropSystem.Examples.Trading.Data;
using TMPro;
using UnityEngine;

namespace DragAndDropSystem.Examples.Trading
{
    /// <summary>
    /// DataBinding для инвентаря игрока в системе торговли.
    /// Использует ListInventoryDataBinding для автоматической синхронизации списка предметов.
    /// Conversion вынесен в отдельный inventory-side converter.
    /// </summary>
    public class PlayerInventoryDataBinding : ListInventoryDataBinding<TradableItemModel, TradableItemModelAdapter>, ITransferDomainHandler
    {
        [FoldoutGroup("UI References")]
        [SerializeField, Tooltip("Текст для отображения денег игрока")]
        private TextMeshProUGUI _moneyText;

        [FoldoutGroup("Settings")]
        [SerializeField, Tooltip("Префикс для отображения денег")]
        private string _moneyPrefix = "Gold: ";

        [FoldoutGroup("Settings")]
        [SerializeField, Tooltip("Суффикс для отображения денег")]
        private string _moneySuffix = "g";

        private PlayerData PlayerData => TradingEconomyManager.Instance.PlayerData;

        // --- ListInventoryDataBinding примитивы ---

        protected override IReadOnlyList<TradableItemModel> GetItems() => PlayerData?.Inventory;
        protected override TradableItemModelAdapter CreateAdapter(TradableItemModel item) => new(item);
        protected override TradableItemModel ExtractData(TradableItemModelAdapter adapter) => adapter.Item;
        protected override IInventoryItemConverter CreateItemConverter() => new ModelInventoryItemConverter();
        protected override RuleResult CanDrop(DragContext context, DragEntry entry) => RuleResult.Success();

        protected override void AddToData(InventoryItemEventContext context, TradableItemModel item)
        {
            PlayerData.AddItem(item);
        }

        protected override void RemoveFromData(InventoryItemEventContext context, TradableItemModel item)
        {
            PlayerData.TryRemoveItem(item);
        }

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
