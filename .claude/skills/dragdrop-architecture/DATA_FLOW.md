# Data Flow

**Last Updated**: 2026-03-21

## Manual Drop Flow

1. `DragAndDropManager` finalizes drag and resolves active drop target.
2. Target provides `IDropProcessor` (typically `InventoryDropProcessor`).
3. Handler builds context with effective policy.
4. `TransferPlanner.Plan(...)` returns `TransferPlan`.
5. `TransferPlanExecutor.Execute(...)` applies plan.
6. Deferred events are emitted after successful completion via `DispatchTransferEvents()`.

## Planner Flow

Input:
- `DragContext`
- target inventory/slot hint
- `DropPolicy`
- global rules

Output:
- `TransferPlan` with entries of type:
  - allocation entry (slot allocations)
  - swap entry (`RequiresSwap`)

## Executor Flow

For each planned entry:
- if allocation entry: execute transfer allocations
- if swap entry: validate + execute swap

Batch policy:
- `Atomic`: rollback all on first failure
- `BestEffort`: continue and report partial failures

## Event Dispatch Flow

`TransferPlanExecutor.DispatchTransferEvents()` is the sole event emitter for transfers:

```
DispatchTransferEvents()
  → UniversalInventory.EmitItemRemoved(context)
      → DataBinding.HandleItemRemoved(context)  [direct call]
      → OnItemRemoved?.Invoke(context)           [event for external subscribers]
  → UniversalInventory.EmitItemAdded(context)
      → DataBinding.HandleItemAdded(context)     [direct call]
      → OnItemAdded?.Invoke(context)             [event for external subscribers]
```

Key design:
- `TryAddToSlot` is pure mutation — no events
- DataBinding notified directly (1:1 relationship), not via events
- External subscribers (FilterSortController etc.) still use events
- `IsSyncing` guard prevents re-entrant callbacks during `ReloadUI()`

## Item Conversion Flow

During transfer, items may be converted through DataBinding pipeline:

```
source.DataBinding.ConvertOutgoingItem(item)
  → target.DataBinding.ConvertIncomingItem(convertedItem)
    → placed in target inventory
```

## Swap Flow

1. Planner marks swap candidate if allocation failed and policy allows `TrySwap`.
2. Executor validates reverse and forward rule compatibility.
3. `SwapAttempting` event can cancel (`InventorySwapContext.Cancel = true`).
4. Executor calls `UniversalInventory.TrySwapSlots(...)`.
5. `SwapCompleted` event and inventory events are emitted after successful plan completion.

Note: Swap uses event subscriptions (not direct calls) because two inventories participate.

## Event Safety Principle

In atomic mode, transfer and swap events are deferred until success to avoid false-positive subscriber side effects.
