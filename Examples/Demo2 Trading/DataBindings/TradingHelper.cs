using DragAndDropSystem.Core;
using DragAndDropSystem.Examples.Trading.Data;
using DragAndDropSystem.Rules;
using Plugins.DragAndDropSystem.Examples.Trading.Data;

namespace DragAndDropSystem.Examples.Trading
{
    /// <summary>
    /// Статические хелперы для торговых операций.
    /// Используются в DataBinding'ах игрока и экипировки для проверки и обработки покупки/продажи.
    /// </summary>
    public static class TradingHelper
    {
        /// <summary>
        /// Проверка возможности покупки предмета у торговца.
        /// Возвращает null если источник не торговец, иначе результат проверки денег.
        /// </summary>
        public static RuleResult ValidatePurchaseFromMerchant(DragEntry entry, PlayerData playerData)
        {
            if (entry.SourceInventory?.DataBinding is not IMerchantInventory)
                return RuleResult.Success(); // Не торговец - не наша забота

            if (entry.Stack.Item is not ITradableItem tradable)
                return RuleResult.Failure("Неверный тип предмета");

            int totalPrice = tradable.BuyPrice * entry.Stack.Count;

            if (!TradingEconomyManager.Instance.CanPlayerAfford(totalPrice))
                return RuleResult.Failure($"Недостаточно денег! Нужно {totalPrice}g, у вас {playerData.Money}g");

            return RuleResult.Success();
        }

        /// <summary>
        /// Проверка возможности сбросить предмет в инвентарь торговца.
        /// Проверяем что предмет идет от игрока и у торговца достаточно денег.
        /// </summary>
        public static RuleResult ValidateSellToMerchant(DragEntry entry, MerchantData merchantData)
        {
            // Запрещаем торговлю между торговцами
            if (entry.SourceInventory?.DataBinding is IMerchantInventory)
                return RuleResult.Failure("Нельзя торговать между торговцами!");
            
            if (entry.Stack.Item is not ITradableItem tradable)
                return RuleResult.Failure("Неверный тип предмета");

            int totalPrice = tradable.SellPrice * entry.Stack.Count;

            if (merchantData.Money <= totalPrice)
                return RuleResult.Failure($"У торговца недостаточно денег! Нужно {totalPrice}g, у него {merchantData.Money}g");
            
            return RuleResult.Success();
        }

        /// <summary>
        /// Обработка покупки у торговца, если применимо.
        /// Возвращает true если покупка была обработана.
        /// </summary>
        public static void TryHandlePurchaseFromMerchant(InventoryItemEventContext context, PlayerData playerData)
        {
            if (context.SourceInventory?.DataBinding is not IMerchantInventory)
                return;

            if (context.Item is ITradableItem tradable)
            {
                int totalPrice = tradable.BuyPrice * context.Count;
                playerData.TrySpendMoney(totalPrice);
            }
        }

        /// <summary>
        /// Обработка продажи торговцу, если применимо.
        /// Возвращает true если продажа была обработана.
        /// </summary>
        public static void TryHandleSellToMerchant(InventoryItemEventContext context, PlayerData playerData)
        {
            if (context.TargetInventory?.DataBinding is not IMerchantInventory)
                return;

            if (context.Item is ITradableItem tradable)
            {
                int totalPrice = tradable.SellPrice * context.Count;
                playerData.AddMoney(totalPrice);
            }
        }
    }
}
