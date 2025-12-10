using DragAndDropSystem.Core;
using DragAndDropSystem.DataBinding;
using DragAndDropSystem.Examples.Trading.Data;
using DragAndDropSystem.Rules;
using DragAndDropSystem.Tools;
using Plugins.DragAndDropSystem.Examples.Trading.Data;
using Sirenix.OdinInspector;
using TMPro;
using UnityEngine;

namespace DragAndDropSystem.Examples.Trading
{
    /// <summary>
    /// DataBinding для инвентаря торговца в системе торговли
    /// Синхронизирует UI инвентаря с MerchantData в TradingEconomyManager
    ///
    /// ПРИМЕР: Демонстрирует работу с централизованной моделью данных, проверку разных условий в CanStartDrag/CanDrop,
    /// и выполнение транзакций через централизованный менеджер
    /// </summary>
    public class MerchantInventoryDataBinding : InventoryDataBindingBase
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
        private MerchantData MerchantData
        {
            get
            {
                if (_merchantData == null)
                    _merchantData = TradingEconomyManager.Instance.GetMerchant(_merchantId);
                return _merchantData;
            }
        }
        private PlayerData PlayerData => TradingEconomyManager.Instance.PlayerData;

        protected override void OnEnable()
        {
            base.OnEnable();
            
            if (MerchantData != null)
            {
                MerchantData.OnMoneyChanged += UpdateMoneyUI;
                _merchantNameText.text = MerchantData.DisplayName;
            }

            // Обновляем UI сразу
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

        protected override void OnItemAddedToUI(InventoryItemEventArgs args)
        {
            // Если мы сейчас синхронизируем UI - не обновляем данные
            if (_isSyncing) return;

            var sourceBinding = args.SourceInventory?.DataBinding;

            // Если предмет пришел от игрока или из экипировки - покупаем у него
            if ((sourceBinding is PlayerInventoryDataBinding || sourceBinding is EquipmentInventoryDataBinding)
                && args.Item is TradableItemModelAdapter adapter)
            {
                int totalPrice = adapter.SellPrice * args.Count;
                MerchantData.TrySpendMoney(totalPrice);
                MerchantData.AddItem(adapter.Item.originalSO);

                // Заменяем адаптер модели на адаптер SO в слоте торговца
                // Это важно, чтобы в UI торговца всегда были SO адаптеры
                if (args.TargetSlot != null)
                {
                    var soAdapter = new TradableSoAdapter(adapter.Item.originalSO);
                    args.TargetSlot.ReplaceItem(soAdapter);
                }
            }
        }

        protected override void OnItemRemovedFromUI(InventoryItemEventArgs args)
        {
            // Если мы сейчас синхронизируем UI - не обновляем данные
            if (_isSyncing) return;

            // Проверяем куда ушел предмет
            var targetBinding = args.TargetInventory?.DataBinding;

            // Если предмет ушел к игроку или в экипировку - продаем ему
            if ((targetBinding is PlayerInventoryDataBinding || targetBinding is EquipmentInventoryDataBinding)
                && args.Item is TradableSoAdapter adapter)
            {
                int totalPrice = adapter.BuyPrice * args.Count;
                MerchantData.AddMoney(totalPrice);
                MerchantData.TryRemoveItem(adapter.Item);
            }
        }

        public override void ReloadUI()
        {
            if (_inventory == null || MerchantData == null) return;

            // Устанавливаем флаг синхронизации
            _isSyncing = true;

            try
            {
                _inventory.ClearAll();

                foreach (var itemSo in MerchantData.Inventory)
                {
                    if (itemSo == null)
                        continue;

                    var adapter = new TradableSoAdapter(itemSo);
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
            if (_moneyText != null && MerchantData != null)
            {
                _moneyText.text = $"{_moneyPrefix}{MerchantData.Money}{_moneySuffix}";
            }
        }

        /// <summary>
        /// Проверка возможности начать перетаскивание из инвентаря торговца
        /// Проверяем что у игрока достаточно денег для покупки
        /// </summary>
        protected override RuleResult CanStartDragInternal(DragContext context)
        {
            if (context.DraggedStack.Item is not TradableSoAdapter adapter)
            {
                return RuleResult.Failure("Неверный тип предмета");
            }

            // Вычисляем стоимость покупки
            int totalPrice = adapter.BuyPrice * context.DraggedStack.Count;

            // Проверяем достаточно ли денег у игрока
            if (!TradingEconomyManager.Instance.CanPlayerAfford(totalPrice))
            {
                return RuleResult.Failure($"Недостаточно денег! Нужно {totalPrice}g, у вас {PlayerData.Money}g");
            }

            return RuleResult.Success();
        }

        /// <summary>
        /// Проверка возможности сбросить предмет в инвентарь торговца
        /// Проверяем что предмет идет от игрока и у торговца достаточно денег
        /// </summary>
        protected override RuleResult CanDropInternal(DragContext context)
        {
            // Если SourceInventory == null, то это программное добавление (SyncToUI)
            // Разрешаем такие операции
            if (context.SourceInventory == null)
            {
                return RuleResult.Success();
            }

            // ВАЖНО: Запрещаем торговлю между торговцами
            var sourceIsMerchant = context.SourceInventory.DataBinding as MerchantInventoryDataBinding;
            if (sourceIsMerchant != null)
            {
                return RuleResult.Failure("Нельзя торговать между торговцами!");
            }
            // Получаем предмет как TradableItemAdapter
            if (context.DraggedStack.Item is not TradableItemModelAdapter adapter)
            {
                return RuleResult.Failure("Неверный тип предмета");
            }

            // Проверяем что источник - это игрок
            var sourceIsPlayer = context.SourceInventory.DataBinding as TradingInventoryDataBinding;
            if (sourceIsPlayer == null)
            {
                return RuleResult.Failure("Можно продавать только предметы из инвентаря игрока");
            }


            // Вычисляем стоимость продажи
            int totalPrice = adapter.SellPrice * context.DraggedStack.Count;

            // Проверяем достаточно ли денег у торговца
            if (MerchantData.Money < totalPrice)
            {
                return RuleResult.Failure($"У торговца недостаточно денег! Нужно {totalPrice}g, у него {MerchantData.Money}g");
            }

            return RuleResult.Success();
        }
    }
}
