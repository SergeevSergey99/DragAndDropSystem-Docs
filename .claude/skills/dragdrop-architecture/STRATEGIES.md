# Strategy Pattern - Deep Dive

Detailed documentation of current inventory strategies.

**Last Updated**: 2026-03-22

## Strategy Hierarchy

```text
IInventoryStrategy
  ├─ UniqueItemStrategy
  ├─ StackableItemStrategy
  ├─ SeparableStacksStrategy
  └─ DynamicSlotDecorator
```

Key files:
- `Scripts/Inventories/Strategies/IInventoryStrategy.cs`
- `Scripts/Inventories/Strategies/InventoryStrategyBase.cs`
- `Scripts/Inventories/Strategies/UniqueItemStrategy.cs`
- `Scripts/Inventories/Strategies/StackableItemStrategy.cs`
- `Scripts/Inventories/Strategies/SeparableStacksStrategy.cs`
- `Scripts/Inventories/Strategies/DynamicSlotDecorator.cs`

Current note:
- strategies are still behind one interface
- acceptance preview already uses `InventoryAcceptanceRequest`
- roadmap proposes splitting this wide interface into smaller capabilities later

## UniqueItemStrategy

Behavior:
- one item per slot
- drag amount always resolves to `1`
- planning uses `UsesPerItemSlotPlanning = true`

Location:
- `Scripts/Inventories/Strategies/UniqueItemStrategy.cs`

How it works:
- creates single-item stacks only
- tries target slot first when provided
- otherwise fills empty valid slots one by one
- preview acceptance validates each slot with `PassesRules(..., request)`

Use cases:
- equipment slots
- artifacts
- truly unique items

## StackableItemStrategy

Behavior:
- automatic merging of same items
- can also place into empty slots

Location:
- `Scripts/Inventories/Strategies/StackableItemStrategy.cs`

How it works:
1. fill existing matching stacks
2. create new stack in empty slot if needed
3. validate each candidate through rules

Current preview behavior:
- uses `InventoryAcceptanceRequest`
- validates merge candidates and empty candidates through real slot rules
- when dynamic slots are allowed, prefab-slot rules are checked before reporting acceptable count

Use cases:
- resources
- consumables
- crafting materials

## SeparableStacksStrategy

Behavior:
- multiple stacks of the same item are allowed
- merge only on explicit drop when `_allowMergeOnDrop` is enabled

Location:
- `Scripts/Inventories/Strategies/SeparableStacksStrategy.cs`

How it works:
- explicit drop to empty slot creates a new stack
- explicit drop to same-item occupied slot merges only if `_allowMergeOnDrop`
- programmatic add prefers creating a new stack instead of auto-merging all the time

Current preview behavior:
- uses `InventoryAcceptanceRequest`
- checks empty slots first
- optionally checks mergeable occupied slots if `_allowMergeOnDrop` is enabled

Use cases:
- Heroes-style army stacks
- tactical RPG inventories
- systems where stack separation is gameplay-relevant

## DynamicSlotDecorator

Behavior:
- wraps any base strategy
- adds dynamic slot creation behavior

Location:
- `Scripts/Inventories/Strategies/DynamicSlotDecorator.cs`

How it works:
- during `TryAdd` with a specific target index, it can create slots up to that index
- during generic add, it creates new slots while items remain and limits allow it
- delegates drag amount, acceptance preview, and placement behavior to wrapped strategy

Important current detail:
- `DynamicSlotDecorator` is creation-oriented
- trimming/removing excess empty slots still happens in `UniversalInventory.HandleSlotEmptied()` and `TrimExcessFreeSlots()`

## Strategy Selection in UniversalInventory

Location:
- `Scripts/Inventories/UniversalInventory.cs`

Configuration enums:
- `ItemBehaviorType`
  - `Unique`
  - `Stackable`
  - `SeparableStacks`
- `SlotManagementType`
  - `Fixed`
  - `Dynamic`

Current runtime delegation from `UniversalInventory`:
- drag amount → `ResolveDragAmount(...)`
- target placement → `TryAddToSlot(...)`
- preview acceptance → `CanAcceptItem(...)` / `GetAcceptableCount(...)`
- planning hint → `UsesPerItemSlotPlanning`

## Custom Strategy Development

Recommended steps:
1. inherit from `InventoryStrategyBase`
2. override `TryAdd(slots, stack, targetIndex)`
3. override `TryRemove(slots, item, count, sourceIndex)` as needed
4. override `TryAddToSlot(...)` if slot-target semantics differ
5. override `CanAcceptItem(...)` / `GetAcceptableCount(...)` if preview logic differs
6. use `PassesRules(slot, item, count, request)` for validation

Typical use cases:
- weight/volume inventories
- class-restricted placement
- custom stacking rules
- durability-aware behavior

Base class:
- `Scripts/Inventories/Strategies/InventoryStrategyBase.cs`
