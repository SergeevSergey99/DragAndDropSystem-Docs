# Operations

**Last Updated**: 2026-02-28

## Manual Drag & Drop (Pipeline)

1. `DragDropEventListener` reports drag/drop target to `DragAndDropManager`.
2. `DragAndDropManager.CompleteDrag()` chooses target handler (`IItemDropHandler`).
3. `InventoryDropHandler.CanAcceptDrop()` validates drop possibility.
4. `InventoryDropHandler.HandleDrop()`:
   - resolves effective `DropPolicy`
   - builds `TransferPlan` via `TransferPlanner`
   - executes plan via `TransferPlanExecutor`
5. Manager emits final completion/cancel events.

## Transfer Plan Execution Modes

`BatchExecutionPolicy`:
- `Atomic`: capture snapshots, rollback whole operation on first failure.
- `BestEffort`: execute each planned entry independently.

## Occupied Target Behaviors

`OccupiedTargetPolicy`:
- `Reject`: fail on occupied target.
- `TryAlternativeSlots`: search valid alternatives.
- `TrySwap`: allow swap planning if allocation fails.

## Swap Flow (Current)

1. Planner marks entry as `RequiresSwap` when policy+conditions allow.
2. Executor validates:
   - reverse start/drop (target item -> source slot)
   - forward drop (source item -> target slot)
3. `SwapAttempting(InventorySwapContext)` callback can cancel.
4. Executor invokes `UniversalInventory.TrySwapSlots(...)`.
5. Swap events are dispatched after successful execution completion.

## Auto-Transfer (Quick Click / Actions)

Auto-transfer still uses `InventoryTransferService` for concrete movement,
but manual drop semantics are now centralized through plan/executor pipeline.

## Key Files

- `Scripts/DragAndDropManager.cs`
- `Scripts/Inventories/InventoryDropHandler.cs`
- `Scripts/Inventories/TransferPlanner.cs`
- `Scripts/Inventories/TransferPlanExecutor.cs`
- `Scripts/Inventories/InventoryTransferService.cs`
