using DragAndDropSystem.Core;
using DragAndDropSystem.DataBinding;
using DragAndDropSystem.Rules;
using DragAndDropSystem.Slots;
using Plugins.DragAndDropSystem.Examples.Trading.Data;
using UnityEngine;

namespace DragAndDropSystem.Examples.Trading
{
    /// <summary>
    /// Базовый класс для инвентарей в системе торговли
    /// Содержит общую логику для проверки и обработки покупки/продажи у торговцев
    /// </summary>
    public abstract class TradingInventoryDataBinding : InventoryDataBindingBase
    {
        protected PlayerData PlayerData => TradingEconomyManager.Instance.PlayerData;

        /// <summary>
        /// Проверка возможности покупки предмета у торговца
        /// Возвращает null если источник не торговец, иначе результат проверки денег
        /// </summary>
        protected RuleResult? ValidatePurchaseFromMerchant(DragContext context)
        {
            // Если источник не торговец - возвращаем null (не наша ответственность)
            if (context.SourceInventory?.DataBinding is not MerchantInventoryDataBinding)
                return null;

            // Проверяем тип адаптера
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
        /// Обработка покупки у торговца, если применимо
        /// Возвращает true если покупка была обработана
        /// </summary>
        protected bool TryHandlePurchaseFromMerchant(InventoryItemEventArgs args)
        {
            if (args.SourceInventory?.DataBinding is not MerchantInventoryDataBinding)
                return false;

            if (args.Item is TradableSoAdapter adapter)
            {
                int totalPrice = adapter.BuyPrice * args.Count;
                PlayerData.TrySpendMoney(totalPrice);
                return true;
            }

            return false;
        }

        /// <summary>
        /// Обработка продажи торговцу, если применимо
        /// Возвращает true если продажа была обработана
        /// </summary>
        protected bool TryHandleSellToMerchant(InventoryItemEventArgs args)
        {
            if (args.TargetInventory?.DataBinding is not MerchantInventoryDataBinding)
                return false;

            if (args.Item is TradableItemModelAdapter adapter)
            {
                int totalPrice = adapter.SellPrice * args.Count;
                PlayerData.AddMoney(totalPrice);
                return true;
            }

            return false;
        }

        /// <summary>
        /// Конвертирует SO адаптер в Model адаптер и заменяет в слоте
        /// Это важно, чтобы в UI игрока/экипировки всегда были Model адаптеры
        /// </summary>
        protected TradableItemModel ConvertAndReplaceSOAdapter(ISlot slot, TradableSoAdapter soAdapter)
        {
            var itemModel = new TradableItemModel(soAdapter.Item);
            var modelAdapter = new TradableItemModelAdapter(itemModel);
            slot.ReplaceItem(modelAdapter);
            return itemModel;
        }

        /// <summary>
        /// Проверка что операция является программным добавлением (из SyncToUI)
        /// Такие операции всегда разрешаем без дополнительных проверок
        /// </summary>
        protected bool IsProgrammaticOperation(DragContext context)
        {
            return context.SourceInventory == null;
        }
    }
}
