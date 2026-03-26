# Operations

**Last Updated**: 2026-03-26

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

`BatchMode`:
- `Atomic`: capture snapshots, rollback whole operation on first failure
- `BestEffort`: execute each planned entry independently

## Occupied Target Behaviors

`BlockedTargetBehavior`:
- `Reject`
- `FindAlternative`
- `Swap`

## Drop Policy Resolution

1. action/drop target can provide `DropRequestPolicy`
2. `InventoryDropProcessor` merges it with a bound target override when present
3. `IDropPolicyProvider` on the target inventory resolves `ResolvedDropPolicy`
4. planner receives only the resolved non-nullable policy

## Single Entry Decision Order

1. Try target slot if one exists
2. If full placement succeeds -> success
3. If partial placement succeeds:
   - `AllowPartial = false` -> fail
   - `AllowPartial = true` -> partial success
4. If zero placement:
   - `Reject` -> fail
   - `Swap` -> plan swap
   - `FindAlternative` -> ask strategy for alternative slots
5. Same-inventory `FindAlternative` does not reshuffle items across other slots

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
