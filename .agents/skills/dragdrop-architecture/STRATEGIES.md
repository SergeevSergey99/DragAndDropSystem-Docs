# Strategy Pattern - Deep Dive

Detailed documentation of current inventory strategies.

**Last Updated**: 2026-05-01

## Strategy Hierarchy

```text
IPlacementStrategy
IAcceptanceStrategy
IDragPolicy
IInventoryQueryStrategy
  └─ IInventoryStrategy
      ├─ UniqueItemStrategy
      ├─ StackableItemStrategy
      ├─ SeparableStacksStrategy
      └─ DynamicSlotDecorator
```

Key files:
- `Scripts/Inventories/Strategies/IPlacementStrategy.cs`
- `Scripts/Inventories/Strategies/IAcceptanceStrategy.cs`
- `Scripts/Inventories/Strategies/IDragPolicy.cs`
- `Scripts/Inventories/Strategies/IInventoryQueryStrategy.cs`
- `Scripts/Inventories/Strategies/IInventoryStrategy.cs`
- `Scripts/Inventories/Strategies/InventoryStrategyBase.cs`
- `Scripts/Inventories/Strategies/UniqueItemStrategy.cs`
- `Scripts/Inventories/Strategies/StackableItemStrategy.cs`
- `Scripts/Inventories/Strategies/SeparableStacksStrategy.cs`
- `Scripts/Inventories/Strategies/DynamicSlotDecorator.cs`

Current note:
- capability interfaces already exist and are used by `UniversalInventory`
- acceptance preview already uses `InventoryAcceptanceRequest`
- `IInventoryStrategy` is kept as an aggregate compatibility layer for now
- current stack mutation paths preserve concrete adapter lists through `ItemStack.Split(...)`
  and `TryAddToStack(...)` instead of recreating stacks only from representative adapter + count

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
- if source stack contains instance-aware adapters, each single-item stack keeps the moved adapter instance

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
4. merge/split preserve actual adapter lists inside stacks

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
- merge/split preserve actual adapter lists inside stacks

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
- read-only queries → `Contains(...)` / `GetItemCount(...)`

## TryAdd and skipRules

`IPlacementStrategy.TryAdd` has a `skipRules` flag:

```csharp
bool TryAdd(List<ISlot> slots, ItemStack stack, int targetIndex, bool skipRules = false);
bool TryAddQuite(List<BaseSlot> slots, ItemStack stack, int targetIndex); // shorthand: skipRules = true
```

When `skipRules = true`, `PassesRules()` calls are bypassed for all candidate slots.

Use case: `UniversalInventory.TryAddStackQuiet()` calls `TryAddQuite()` so that
`ReloadUI()` / `OnReloadUI()` never triggers drop-rule validation — items are placed
purely according to strategy layout logic.

Important: all concrete strategies (`UniqueItemStrategy`, `StackableItemStrategy`,
`SeparableStacksStrategy`) and `DynamicSlotDecorator` respect this flag.

## Custom Strategy Development

Recommended steps:
1. inherit from `InventoryStrategyBase`
2. override `TryAdd(slots, stack, targetIndex, skipRules = false)` — respect `skipRules` flag
3. override `TryRemove(slots, item, count, sourceIndex)` as needed
4. override `TryAddToSlot(...)` if slot-target semantics differ
5. override `CanAcceptItem(...)` / `GetAcceptableCount(...)` if preview logic differs
6. use `PassesRules(slot, item, count, request)` for validation — skip it when `skipRules` is true

Typical use cases:
- weight/volume inventories
- class-restricted placement
- custom stacking rules
- durability-aware behavior

Base class:
- `Scripts/Inventories/Strategies/InventoryStrategyBase.cs`
