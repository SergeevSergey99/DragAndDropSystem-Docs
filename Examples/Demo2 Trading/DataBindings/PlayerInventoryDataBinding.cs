using System.Collections.Generic;
using DragAndDropSystem.Core;
using DragAndDropSystem.DataBinding;
using DragAndDropSystem.Inspector;
using DragAndDropSystem.Rules;
using Plugins.DragAndDropSystem.Examples.Trading.Data;
using TMPro;
using UnityEngine;

namespace DragAndDropSystem.Examples.Trading
{
    /// <summary>
    /// DataBinding для инвентаря игрока в системе торговли.
    /// Использует ListInventoryDataBinding для автоматической синхронизации списка предметов.
    /// ConvertIncomingItem конвертирует SO-адаптеры торговца в Model-адаптеры игрока.
    /// </summary>
    public class PlayerInventoryDataBinding : ListInventoryDataBinding<TradableItemModel, TradableItemModelAdapter>
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
        protected override void AddToData(TradableItemModel item) => PlayerData.AddItem(item);
        protected override void RemoveFromData(TradableItemModel item) => PlayerData.TryRemoveItem(item);

        // --- Конвертация: SO-адаптер торговца → Model-адаптер игрока ---

        internal override IInventoryItem ConvertIncomingItem(IInventoryItem item)
        {
            if (item is TradableItemModelAdapter) return item;
            if (item is ITradableItem tradable)
                return new TradableItemModelAdapter(new TradableItemModel(tradable.OriginalSO));
            return item;
        }

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

        // --- Торговая логика поверх базовой синхронизации ---

        protected override void OnItemAddedToUI(InventoryItemEventContext context)
        {
            TradingHelper.TryHandlePurchaseFromMerchant(context, PlayerData);
            base.OnItemAddedToUI(context);
        }

        protected override void OnItemRemovedFromUI(InventoryItemEventContext context)
        {
            TradingHelper.TryHandleSellToMerchant(context, PlayerData);
            base.OnItemRemovedFromUI(context);
        }

        protected override void OnReloadUI()
        {
            base.OnReloadUI();
            UpdateMoneyUI();
        }

        protected override RuleResult CanDrop(DragContext context, DragEntry entry)
        {
            return TradingHelper.ValidatePurchaseFromMerchant(entry, PlayerData);
        }

        private void UpdateMoneyUI()
        {
            if (_moneyText != null && PlayerData != null)
                _moneyText.text = $"{_moneyPrefix}{PlayerData.Money}{_moneySuffix}";
        }
    }
}
