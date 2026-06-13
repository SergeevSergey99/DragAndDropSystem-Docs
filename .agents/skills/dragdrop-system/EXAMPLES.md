# Examples

**Last Updated**: 2026-05-01

## Demo1 - Basic Inventory

Location:
- `Examples/Demo1 Inventories/`

Focus:
- basic drag/drop
- slot and inventory rules
- adapters for item models / SOs

## Demo4 - Trading

Location:
- `Examples/Demo4 Trading/`

Focus:
- economy constraints via template DataBindings
- merchant/player transfer restrictions with `TradingHelper`
- equipment slots with `MappedSlotInventoryDataBinding` and declarative `canAccept`
- item conversion pipeline (SO-adapter ↔ Model-adapter) through target-side preview before planning

Key DataBindings:
- `PlayerInventoryDataBinding`
- `MerchantInventoryDataBinding`
- `EquipmentInventoryDataBinding`
- `TradingHelper`

Current note:
- mapped-slot preview now relies on shared acceptance infrastructure, not per-binding preview guards
- `TryGetTargetBinding()` / `TryGetSourceBinding()` are the preferred access pattern in mapped bindings

## Demo2 - Loot / World Interaction

Location:
- `Examples/Demo2 Loot/`

Focus:
- event-driven UI open/close
- inventory sync with player/chest state
- optional world item workflows

## Cross-Demo Checklist

- verify `DropPolicy` preset per target type
- verify sequential best-effort batch behavior and per-entry rollback
- verify swap callbacks and cancellation path
- verify direct DataBinding notifications
- verify cross-inventory adapter conversion produces correct target payloads
