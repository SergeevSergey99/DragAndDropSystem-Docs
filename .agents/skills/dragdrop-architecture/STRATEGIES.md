# Strategy Pattern - Deep Dive

Detailed documentation of current inventory strategies.

**Last Updated**: 2026-06-14

## Strategy Hierarchy

```text
IStrategy
  └─ InventoryStrategyBase
      ├─ UniqueItemStrategy
      ├─ StackableItemStrategy
      └─ SeparableStacksStrategy
```

Key files:
- `Scripts/Inventories/Strategies/IStrategy.cs`
- `Scripts/Inventories/Strategies/InventoryStrategyBase.cs`
- `Scripts/Inventories/Strategies/UniqueItemStrategy.cs`
- `Scripts/Inventories/Strategies/StackableItemStrategy.cs`
- `Scripts/Inventories/Strategies/SeparableStacksStrategy.cs`

Strategies are read-only policies. They validate an explicit destination with
`TryGetCandidate(...)`, lazily enumerate automatic destinations with `GetCandidates(...)`, and
calculate acceptance with `GetAcceptableCount(geometry, request)`. Mutation and dynamic slot
lifecycle belong to the transfer service and inventory capabilities.

## UniqueItemStrategy

Behavior:
- one item per slot
- drag amount always resolves to `1`
- each candidate has capacity `1`

Location:
- `Scripts/Inventories/Strategies/UniqueItemStrategy.cs`

How it works:
- creates single-item stacks only
- tries target slot first when provided
- otherwise fills empty valid slots one by one
- candidate resolution validates each slot with `PassesRules(..., request)`
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
1. item present → candidate is its logical placement in auto-merge mode
2. item absent → empty placement or `NewDynamicSlot` candidate
3. explicit-only mode accepts a duplicate only on its existing placement
4. each candidate is validated through rules and topology-neutral geometry

Shaped placements use the same candidate API. Existing logical placements produce merge
candidates; empty valid footprints produce create candidates.

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
- shaped items merge only on an explicit same-item placement; otherwise a new valid footprint is used
- merge/split preserve actual adapter lists inside stacks

Current preview behavior:
- uses `InventoryAcceptanceRequest`
- checks empty slots first
- optionally checks mergeable occupied slots if `_allowMergeOnDrop` is enabled

Use cases:
- Heroes-style army stacks
- tactical RPG inventories
- systems where stack separation is gameplay-relevant

## Dynamic Slots

Behavior:
- strategies may expose `NewDynamicSlot` candidates
- execution creates the slot through `IDynamicSlotLifecycle`

Dynamic capacity is read through `IInventorySlotCreationCapacity`. Strategies do not create or
remove slots themselves.

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
- explicit target → `TryGetCandidate(...)`
- automatic distribution → `GetCandidates(...)`
- acceptance count → `GetAcceptableCount(geometry, request)`
- dynamic slot lifecycle → `TryCreateSlot(...)` / `HandleSlotEmptied(...)`

## Candidate Selection

Selection is split into eligibility and ordering:

- `TryGetCandidate(...)` validates one explicit slot/placement without enumeration or ordering.
- `GetCandidates(...)` lazily returns eligible merge, empty-placement, and dynamic-slot candidates.
- `PlacementCandidateOrderer` is used only for automatic distribution and alternative placement.
- execution requests fresh candidates after each mutation, so later entries and stack remainders see real state.

No materialized plan, virtual slot state, or strategy mutation API is involved.

## Custom Strategy Development

Recommended steps:
1. inherit from `InventoryStrategyBase`
2. override `TryGetCandidate(...)` for explicit-target semantics
3. override `GetCandidates(...)` for automatic eligibility and natural order
4. override `GetAcceptableCount(...)` when aggregate capacity differs
5. override `ResolveDragAmount(...)` when drag amount semantics differ
6. use `PassesRules(...)` for candidate validation

Typical use cases:
- weight/volume inventories
- class-restricted placement
- custom stacking rules
- durability-aware behavior

Base class:
- `Scripts/Inventories/Strategies/InventoryStrategyBase.cs`
