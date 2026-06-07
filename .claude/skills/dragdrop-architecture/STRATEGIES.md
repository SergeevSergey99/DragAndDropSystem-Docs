# Strategy Pattern - Deep Dive

Detailed documentation of current inventory strategies.

**Last Updated**: 2026-05-30

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

Behavior (one-per-ID):
- at most ONE logical stack location per item ID (a slot, or a placement for shaped items)
- that stack may hold count > 1 (including shaped placements), capped by the strategy / per-item limit
- auto-merge (default): a duplicate dropped anywhere consolidates into the existing stack
- explicit-merge-only (private `_explicitMergeOnly`, inverted serialized field so default = auto-merge):
  the stack grows only on an explicit drop onto it; a duplicate dropped elsewhere is rejected
- for multiple separate stacks of the same item, use `SeparableStacksStrategy`

Location:
- `Scripts/Inventories/Strategies/StackableItemStrategy.cs`

How it works:
1. `GetSlotCandidates`: item present → only its logical location (auto-merge); explicit-only → None
2. item absent → empty slot (+ new slot capability)
3. `TryAddToSlot` into an empty slot while the item exists: auto-merge → consolidate into the existing
   stack; explicit-only → reject (this is what stops a duplicate from bouncing back on an empty-slot drop)
4. validate each candidate through rules; merge/split preserve actual adapter lists inside stacks

Shaped placements:
- the merge-vs-new/reject decision is owned by the strategy via `IAcceptanceStrategy.ResolveShapedMerge(...)`
  (auto → merge into the single existing placement anywhere; explicit-only → merge only on footprint overlap,
  else reject). The planner only asks and acts — it never reads strategy flags or sniffs strategy types.

Use cases:
- resources
- consumables
- crafting materials

## SeparableStacksStrategy

Behavior:
- multiple stacks/placements of the same item are allowed
- each stack (including a shaped placement) may hold count > 1, capped by the limit
- merge only on explicit drop onto the same item; otherwise a new separate stack/placement is created

Location:
- `Scripts/Inventories/Strategies/SeparableStacksStrategy.cs`

How it works:
- explicit drop to empty slot creates a new stack
- explicit drop to a same-item occupied slot merges into it
- shaped: `ResolveShapedMerge` merges only when the dropped footprint overlaps a same-item placement, else new
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
- generic add still creates slots while items remain
- same-inventory area drops can create an explicit target slot through `IDynamicSlotLifecycle.TryCreateSlot(...)`
- trimming/removing excess empty slots still happens through `IDynamicSlotLifecycle.HandleSlotEmptied(...)`

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
- dynamic slot lifecycle → `TryCreateSlot(...)` / `HandleSlotEmptied(...)`

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
