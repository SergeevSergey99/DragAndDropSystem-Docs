# Examples

**Last Updated**: 2026-02-28

## Demo1 - Basic Inventory

Location: `Examples/Demo1/`

Focus:
- basic drag/drop
- slot/inventory rules
- adapters for item models/SO

## Demo2 - Trading

Location: `Examples/Demo2 Trading/`

Focus:
- economy constraints in DataBinding
- merchant/player transfer restrictions
- swap validation hooks through `InventorySwapContext`

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
