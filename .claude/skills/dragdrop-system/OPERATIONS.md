# Operations

**Last Updated**: 2026-03-23

## Manual Drag & Drop (Pipeline)

1. Active `IDropTarget` is tracked by `DragAndDropManager`.
2. `DragAndDropManager.CompleteDrag()` gets `IDropProcessor` from the active target.
3. `InventoryDropProcessor.CanAcceptDrop()` validates drop possibility.
4. `InventoryDropProcessor.ProcessDrop()`:
   - resolves effective `DropPolicy`
   - resolves target-side preview item where needed
   - builds `TransferPlan` via `TransferPlanner`
   - executes plan via `TransferPlanExecutor`
5. Manager emits final completion/cancel events.

## Transfer Plan Execution Modes

`BatchExecutionPolicy`:
- `Atomic`: capture snapshots, rollback whole operation on first failure
- `BestEffort`: execute each planned entry independently

## Occupied Target Behaviors

`OccupiedTargetPolicy`:
- `Reject`
- `TryAlternativeSlots`
- `TrySwap`

## Swap Flow (Current)

1. Planner marks entry as `RequiresSwap` when policy+conditions allow.
2. Executor validates reverse and forward directions.
3. `SwapAttempting(InventorySwapContext)` callback can cancel.
4. Executor invokes `UniversalInventory.TrySwapSlots(...)`.
5. Swap events are dispatched after successful execution completion.

## Preview Acceptance

Area-drop and planning preview use:
- `TransferItemConversionUtility`
- `InventoryAcceptanceRequest`

This keeps slot-specific rules and mapped-slot bindings consistent between hover preview and final execution.

## Auto-Transfer (Quick Click / Actions)

Auto-transfer uses the same planner/executor pipeline as manual drag & drop.

## Key Files

- `Scripts/DragAndDropManager.cs`
- `Scripts/Inventories/InventoryDropProcessor.cs`
- `Scripts/Inventories/TransferPlanner.cs`
- `Scripts/Inventories/TransferPlanExecutor.cs`
- `Scripts/Inventories/InventoryTransferService.cs`
- `Scripts/Inventories/InventoryAcceptanceRequest.cs`
