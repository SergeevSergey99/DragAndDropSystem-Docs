# Examples

**Last Updated**: 2026-03-21

## Demo1 - Basic Inventory

Location: `Examples/Demo1/`

Focus:
- basic drag/drop
- slot/inventory rules
- adapters for item models/SO

## Demo2 - Trading

Location: `Examples/Demo2 Trading/`

Focus:
- economy constraints via template DataBindings
- merchant/player transfer restrictions with `TradingHelper`
- equipment slots with `MappedSlotInventoryDataBinding` and declarative `canAccept`
- item conversion pipeline (SO-adapter ↔ Model-adapter)

Key DataBindings:
- `PlayerInventoryDataBinding` extends `ListInventoryDataBinding<TradableItemModel, TradableItemModelAdapter>`
- `MerchantInventoryDataBinding` extends `ListInventoryDataBinding<TradableItemSO, TradableSoAdapter>`
- `EquipmentInventoryDataBinding` extends `MappedSlotInventoryDataBinding<TradableItemModel, TradableItemModelAdapter>`
- `TradingHelper` — static class for shared validation/transaction logic

Important: swap behavior should be configured by policy (`DropPolicy`) and/or binding rules,
not by legacy boolean flags.

## Demo3 - Loot / World Interaction

Location: `Examples/Demo3 Loot/`

Focus:
- event-driven UI open/close
- inventory sync with player/chest state
- optional world item workflows

## Cross-Demo Checklist

- verify `DropPolicy` preset per target type
- verify batch behavior (`Atomic` vs `BestEffort`)
- verify swap callbacks and cancellation path
- verify DataBinding direct notifications work (HandleItemAdded/HandleItemRemoved)
