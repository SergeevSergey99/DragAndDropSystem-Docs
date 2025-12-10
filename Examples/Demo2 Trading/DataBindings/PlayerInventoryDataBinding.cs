using System.Linq;
using DragAndDropSystem.Core;
using DragAndDropSystem.DataBinding;
using DragAndDropSystem.Rules;
using DragAndDropSystem.Tools;
using Plugins.DragAndDropSystem.Examples.Trading.Data;
using Sirenix.OdinInspector;
using TMPro;
using UnityEngine;

namespace DragAndDropSystem.Examples.Trading
{
    /// <summary>
    /// DataBinding для инвентаря игрока в системе торговли
    /// Синхронизирует UI инвентаря с PlayerEconomyData в TradingEconomyManager
    ///
    /// ПРИМЕР: Демонстрирует работу с централизованной моделью данных и проверку условий в CanDropInternal
    /// </summary>
    public class PlayerInventoryDataBinding : TradingInventoryDataBinding
    {
        [FoldoutGroup("UI References")]
        [SerializeField, Tooltip("Текст для отображения денег игрока")]
        private TextMeshProUGUI _moneyText;

        [FoldoutGroup("Settings")]
        [SerializeField, Tooltip("Префикс для отображения денег (например, 'Gold: ')")]
        private string _moneyPrefix = "Gold: ";

        [FoldoutGroup("Settings")]
        [SerializeField, Tooltip("Суффикс для отображения денег (например, 'g')")]
        private string _moneySuffix = "g";

        protected override void OnEnable()
        {
            base.OnEnable();
            if (PlayerData != null)
            {
                PlayerData.OnMoneyChanged += UpdateMoneyUI;
            }
            // Обновляем UI денег сразу
            UpdateMoneyUI();
        }

        protected override void OnDisable()
        {
            if (TradingEconomyManager.IsInstanceExist && PlayerData != null)
            {
                PlayerData.OnMoneyChanged -= UpdateMoneyUI;
            }
        }

        protected override void OnItemAddedToUI(InventoryItemEventArgs args)
        {
            // Если мы сейчас синхронизируем UI - не обновляем данные
            if (_isSyncing) return;

            // Если предмет пришел от торговца - покупаем у него
            if (TryHandlePurchaseFromMerchant(args))
            {
                // Заменяем адаптер SO на адаптер модели в слоте игрока
                // ConvertAndReplaceSOAdapter создает TradableItemModel, который мы добавляем в данные
                var soAdapter = args.Item as TradableSoAdapter;
                if (args.TargetSlot != null)
                {
                    var itemModel = ConvertAndReplaceSOAdapter(args.TargetSlot, soAdapter);
                    PlayerData.AddItem(itemModel);
                }
            }
            else if (args.Item is TradableItemModelAdapter adapter)
            {
                // Добавляем предмет в данные (включая внутренние перемещения для сохранения порядка)
                PlayerData.AddItem(adapter.Item);
            }
        }

        protected override void OnItemRemovedFromUI(InventoryItemEventArgs args)
        {
            // Если мы сейчас синхронизируем UI - не обновляем данные
            if (_isSyncing) return;

            // Если предмет ушел к торговцу - продаем ему
            TryHandleSellToMerchant(args);

            // Удаляем предмет из данных (включая внутренние перемещения для сохранения порядка)
            if (args.Item is TradableItemModelAdapter adapter)
            {
                PlayerData.TryRemoveItem(adapter.Item);
            }
        }

        public override void ReloadUI()
        {
            if (_inventory == null || PlayerData == null) return;

            // Устанавливаем флаг синхронизации
            _isSyncing = true;

            try
            {
                _inventory.ClearAll();

                foreach (var itemModel in PlayerData.Inventory)
                {
                    if (itemModel.originalSO == null)
                        continue;

                    var adapter = new TradableItemModelAdapter(itemModel);
                    AddToUIQuiet(adapter, 1);
                }

                UpdateMoneyUI();
            }
            finally
            {
                // Всегда сбрасываем флаг синхронизации
                _isSyncing = false;
            }
        }

        /// <summary>
        /// Обновить UI отображения денег
        /// </summary>
        private void UpdateMoneyUI()
        {
            if (_moneyText != null && PlayerData != null)
            {
                _moneyText.text = $"{_moneyPrefix}{PlayerData.Money}{_moneySuffix}";
            }
        }

        /// <summary>
        /// Проверка возможности начать перетаскивание
        /// Для игрока всегда разрешаем перетаскивание своих предметов
        /// </summary>
        protected override RuleResult CanStartDragInternal(DragContext context)
        {
            // Разрешаем игроку перетаскивать свои предметы
            return RuleResult.Success();
        }

        /// <summary>
        /// Проверка возможности сбросить предмет в инвентарь игрока
        /// Здесь проверяем что предмет идет от торговца и у игрока достаточно денег
        /// </summary>
        protected override RuleResult CanDropInternal(DragContext context)
        {
            // Если это программное добавление (SyncToUI) - разрешаем
            if (IsProgrammaticOperation(context))
            {
                return RuleResult.Success();
            }

            // Проверяем покупку у торговца (если применимо)
            var purchaseResult = ValidatePurchaseFromMerchant(context);
            if (purchaseResult.HasValue)
            {
                return purchaseResult.Value;
            }

            // Если это не торговец, значит просто перемещение внутри инвентаря игрока
            return RuleResult.Success();
        }
    }
}
