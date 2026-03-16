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
        protected RuleResult? ValidatePurchaseFromMerchant(DragContext context, DragEntry entry)
        {
            // Если источник не торговец - возвращаем null (не наша ответственность)
            if (entry.SourceInventory?.DataBinding is not IMerchantInventory)
                return null;

            // Проверяем тип адаптера
            if (entry.Stack.Item is not ITradableItem tradable)
            {
                return RuleResult.Failure("Неверный тип предмета");
            }

            // Вычисляем стоимость покупки
            int totalPrice = tradable.BuyPrice * entry.Stack.Count;

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
        protected bool TryHandlePurchaseFromMerchant(InventoryItemEventContext context)
        {
            if (context.SourceInventory?.DataBinding is not IMerchantInventory)
                return false;

            if (context.Item is ITradableItem tradable)
            {
                int totalPrice = tradable.BuyPrice * context.Count;
                PlayerData.TrySpendMoney(totalPrice);
                return true;
            }

            return false;
        }

        /// <summary>
        /// Обработка продажи торговцу, если применимо
        /// Возвращает true если продажа была обработана
        /// </summary>
        protected bool TryHandleSellToMerchant(InventoryItemEventContext context)
        {
            if (context.TargetInventory?.DataBinding is not IMerchantInventory)
                return false;

            if (context.Item is ITradableItem tradable)
            {
                int totalPrice = tradable.SellPrice * context.Count;
                PlayerData.AddMoney(totalPrice);
                return true;
            }

            return false;
        }

        /// <summary>
        /// Конвертирует SO адаптер в Model адаптер и заменяет в слоте
        /// Это важно, чтобы в UI игрока/экипировки всегда были Model адаптеры
        /// </summary>
        protected TradableItemModel ConvertToModelAdapter(ISlot slot, ITradableItem tradable)
        {
            var itemModel = new TradableItemModel(tradable.OriginalSO);
            var modelAdapter = new TradableItemModelAdapter(itemModel);
            slot.ReplaceItem(modelAdapter);
            return itemModel;
        }

        /// <summary>
        /// Проверка что операция является программным добавлением (из SyncToUI)
        /// Такие операции всегда разрешаем без дополнительных проверок
        /// </summary>
        protected bool IsProgrammaticOperation(DragContext context, DragEntry entry)
        {
            return entry.SourceInventory == null;
        }
    }
}
