using UniversalDragAndDrop.Examples.Trading.Data;
using UniversalDragAndDrop.Core;
using UniversalDragAndDrop.Examples.Trading.Data;
using UniversalDragAndDrop.Inventories;
using UniversalDragAndDrop.Rules;

namespace UniversalDragAndDrop.Examples.Trading
{
    /// <summary>
    /// Static helpers for trading operations.
    /// Used by player and equipment DataBindings to validate and process buy/sell actions.
    /// </summary>
    public static class TradingHelper
    {
        /// <summary>
        /// Mechanical validation for dropping into a merchant inventory.
        /// Money/business validation is handled through domain hooks.
        /// </summary>
        public static RuleResult ValidateMerchantDrop(DragEntry entry)
        {
            if (entry.SourceInventory?.DataBinding is IMerchantInventory)
                return RuleResult.Failure("Trading between merchants is not allowed.");
            
            if (entry.Stack.PrimaryAdapter is not ITradableItem)
                return RuleResult.Failure("Invalid item type");
            
            return RuleResult.Success();
        }

        public static RuleResult ValidatePlayerTransfer(TransferDomainContext context, PlayerData playerData)
        {
            if (context.SourceInventory?.DataBinding is not IMerchantInventory)
                return RuleResult.Success();

            if (context.SourceItemAdapter is not ITradableItem tradable)
                return RuleResult.Failure("Invalid item type");

            int cost = tradable.BuyPrice * context.RequestedAmount;

            if (context.Kind == TransferKind.Swap &&
                context.CounterpartContext?.SourceItemAdapter is ITradableItem sellItem)
            {
                cost -= sellItem.SellPrice * context.CounterpartContext.RequestedAmount;
            }

            if (cost > 0 && !TradingEconomyManager.AutoCreateInstance.CanPlayerAfford(cost))
                return RuleResult.Failure($"Not enough money. Required: {cost}g, available: {playerData.Money}g");

            return RuleResult.Success();
        }

        public static RuleResult ValidateMerchantTransfer(TransferDomainContext context, MerchantData merchantData)
        {
            if (context.TargetInventory?.DataBinding is not IMerchantInventory)
                return RuleResult.Success();

            if (context.SourceInventory?.DataBinding is IMerchantInventory)
                return RuleResult.Failure("Trading between merchants is not allowed.");

            if (context.SourceItemAdapter is not ITradableItem tradable)
                return RuleResult.Failure("Invalid item type");

            int cost = tradable.SellPrice * context.RequestedAmount;

            if (context.Kind == TransferKind.Swap &&
                context.CounterpartContext?.SourceItemAdapter is ITradableItem buyItem)
            {
                cost -= buyItem.BuyPrice * context.CounterpartContext.RequestedAmount;
            }

            if (cost > 0 && merchantData.Money < cost)
                return RuleResult.Failure($"The merchant does not have enough money. Required: {cost}g, available: {merchantData.Money}g");

            return RuleResult.Success();
        }

        public static void ApplyPlayerTransferEffects(TransferDomainContext context, PlayerData playerData)
        {
            if (context.SourceInventory?.DataBinding is IMerchantInventory &&
                context.SourceItemAdapter is ITradableItem buyItem)
            {
                playerData.TrySpendMoney(buyItem.BuyPrice * context.CommittedAmount);
                return;
            }

            if (context.TargetInventory?.DataBinding is IMerchantInventory &&
                context.SourceItemAdapter is ITradableItem sellItem)
            {
                playerData.AddMoney(sellItem.SellPrice * context.CommittedAmount);
            }
        }

        public static void ApplyMerchantTransferEffects(TransferDomainContext context, MerchantData merchantData)
        {
            if (context.SourceInventory?.DataBinding is IMerchantInventory &&
                context.SourceItemAdapter is ITradableItem soldByMerchant)
            {
                merchantData.AddMoney(soldByMerchant.BuyPrice * context.CommittedAmount);
                return;
            }

            if (context.TargetInventory?.DataBinding is IMerchantInventory &&
                context.SourceItemAdapter is ITradableItem boughtByMerchant)
            {
                merchantData.TrySpendMoney(boughtByMerchant.SellPrice * context.CommittedAmount);
            }
        }
    }
}