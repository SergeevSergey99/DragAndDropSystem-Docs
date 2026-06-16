using System.Collections.Generic;
using UDND.Core;
using UDND.Examples.Trading.Data;
using UDND.DataBinding;
using UDND.Inventories;
using UDND.Rules;

namespace UDND.Examples.Trading
{
    /// <summary>
    /// DataBinding for the player inventory in the trading system.
    /// Uses ListInventoryDataBinding for automatic synchronization of the item list.
    /// Conversion is delegated to a separate inventory-side converter.
    /// </summary>
    public class PlayerInventoryDataBinding : ListInventoryDataBinding<TradableItemModel, TradableItemAdapterModelAdapter>, ITransferDomainHandler
    {
        private PlayerData PlayerData => TradingEconomyManager.AutoCreateInstance.PlayerData;

        // --- ListInventoryDataBinding primitives ---

        protected override IReadOnlyList<TradableItemModel> GetItems() => PlayerData?.Inventory;
        protected override TradableItemAdapterModelAdapter CreateAdapter(TradableItemModel item) => new(item);
        protected override IItemAdapterConverter CreateItemConverter() => new ModelItemAdapterConverter();

        protected override void AddToData(TradableItemAdapterModelAdapter adapter) => PlayerData.AddItem(adapter.Item);
        protected override void RemoveFromData(TradableItemAdapterModelAdapter adapter) => PlayerData.TryRemoveItem(adapter.Item);

        public RuleResult CanStartTransfer(DragContext context, IInventory targetInventory) => RuleResult.Success();

        public RuleResult CanCommitTransfer(TransferDomainContext context) => TradingHelper.ValidatePlayerTransfer(context, PlayerData);

        public void OnTransferSucceeded(TransferDomainContext context) => TradingHelper.ApplyPlayerTransferEffects(context, PlayerData);
    }
}