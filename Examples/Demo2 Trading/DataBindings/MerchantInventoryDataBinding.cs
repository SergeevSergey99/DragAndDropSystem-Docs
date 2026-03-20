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
    public class MerchantInventoryDataBinding : ListInventoryDataBinding<TradableItemSO, TradableSoAdapter>, IMerchantInventory
    {
        [FoldoutGroup("Merchant Settings")]
        [SerializeField, Tooltip("ID торговца (должен совпадать с ID в TradingEconomyManager)")]
        private string _merchantId;

        [FoldoutGroup("UI References")]
        [SerializeField, Tooltip("Текст для отображения имени торговца")]
        private TextMeshProUGUI _merchantNameText;

        [FoldoutGroup("UI References")]
        [SerializeField, Tooltip("Текст для отображения денег торговца")]
        private TextMeshProUGUI _moneyText;

        [FoldoutGroup("Settings")]
        [SerializeField, Tooltip("Префикс для отображения денег (например, 'Gold: ')")]
        private string _moneyPrefix = "Gold: ";

        [FoldoutGroup("Settings")]
        [SerializeField, Tooltip("Суффикс для отображения денег (например, 'g')")]
        private string _moneySuffix = "g";

        private MerchantData _merchantData;
        private MerchantData MerchantData => _merchantData ??= TradingEconomyManager.Instance.GetMerchant(_merchantId);
        private PlayerData PlayerData => TradingEconomyManager.Instance.PlayerData;

        // --- ListInventoryDataBinding примитивы ---

        protected override IReadOnlyList<TradableItemSO> GetItems() => MerchantData?.Inventory;
        protected override TradableSoAdapter CreateAdapter(TradableItemSO item) => new(item);
        protected override TradableItemSO ExtractData(TradableSoAdapter adapter) => adapter.Item;
        protected override void AddToData(TradableItemSO item) => MerchantData.AddItem(item);
        protected override void RemoveFromData(TradableItemSO item) => MerchantData.TryRemoveItem(item);

        // --- Конвертация: Model-адаптер игрока → SO-адаптер торговца ---

        internal override IInventoryItem ConvertIncomingItem(IInventoryItem item)
        {
            if (item is TradableSoAdapter) return item;
            if (item is ITradableItem tradable)
                return new TradableSoAdapter(tradable.OriginalSO);
            return null; // Неизвестный тип предмета, не конвертируем, запрещаем
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

        // --- Торговая логика поверх базовой синхронизации ---

        protected override void OnItemAddedToUI(InventoryItemEventContext context)
        {
            var sourceBinding = context.SourceInventory?.DataBinding;

            // Если предмет пришел от игрока — торговец платит за выкуп
            if (sourceBinding != null && sourceBinding is not IMerchantInventory
                && context.Item is ITradableItem tradable)
            {
                int totalPrice = tradable.SellPrice * context.Count;
                MerchantData.TrySpendMoney(totalPrice);
            }

            base.OnItemAddedToUI(context);
        }

        protected override void OnItemRemovedFromUI(InventoryItemEventContext context)
        {
            var targetBinding = context.TargetInventory?.DataBinding;

            // Если предмет ушел к игроку — торговец получает деньги за продажу
            if (targetBinding != null && targetBinding is not IMerchantInventory
                && context.Item is ITradableItem tradable)
            {
                int totalPrice = tradable.BuyPrice * context.Count;
                MerchantData.AddMoney(totalPrice);
            }

            base.OnItemRemovedFromUI(context);
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

        // --- Правила ---

        /// <summary>
        /// Проверка возможности начать перетаскивание из инвентаря торговца.
        /// Проверяем что у игрока достаточно денег для покупки.
        /// </summary>
        protected override RuleResult CanStartDrag(DragContext context, DragEntry entry)
        {
            if (entry.Stack.Item is not ITradableItem tradable)
                return RuleResult.Failure("Неверный тип предмета");
            
            int totalPrice = tradable.BuyPrice * entry.Stack.Count;

            if (!TradingEconomyManager.Instance.CanPlayerAfford(totalPrice))
                return RuleResult.Failure($"Недостаточно денег! Нужно {totalPrice}g, у вас {PlayerData.Money}g");
            
            return RuleResult.Success();
        }

        /// <summary>
        /// Проверка возможности сбросить предмет в инвентарь торговца.
        /// Проверяем что предмет идет от игрока и у торговца достаточно денег.
        /// </summary>
        protected override RuleResult CanDrop(DragContext context, DragEntry entry)
        {
            // Запрещаем торговлю между торговцами
            if (entry.SourceInventory.DataBinding is IMerchantInventory)
                return RuleResult.Failure("Нельзя торговать между торговцами!");
            
            if (entry.Stack.Item is not ITradableItem tradable)
                return RuleResult.Failure("Неверный тип предмета");

            int totalPrice = tradable.SellPrice * entry.Stack.Count;

            if (MerchantData.Money < totalPrice)
                return RuleResult.Failure($"У торговца недостаточно денег! Нужно {totalPrice}g, у него {MerchantData.Money}g");
            
            return RuleResult.Success();
        }
    }
}
