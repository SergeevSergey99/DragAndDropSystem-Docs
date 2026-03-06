# Data Flow

**Last Updated**: 2026-02-28

## Manual Drop Flow

1. `DragAndDropManager` finalizes drag and resolves active drop target.
2. Target provides `IDropProcessor` (typically `InventoryDropProcessor`).
3. Handler builds context with effective policy.
4. `TransferPlanner.Plan(...)` returns `TransferPlan`.
5. `TransferPlanExecutor.Execute(...)` applies plan.
6. Deferred events are emitted after successful completion.

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

## Swap Flow

1. Planner marks swap candidate if allocation failed and policy allows `TrySwap`.
2. Executor validates reverse and forward rule compatibility.
3. `SwapAttempting` callback can cancel (`InventorySwapContext.Cancel = true`).
4. Executor calls `UniversalInventory.TrySwapSlots(...)`.
5. `SwapCompleted` callback and inventory events are emitted after successful plan completion.

## Event Safety Principle

In atomic mode, transfer and swap events are deferred until success to avoid false-positive subscriber side effects.
