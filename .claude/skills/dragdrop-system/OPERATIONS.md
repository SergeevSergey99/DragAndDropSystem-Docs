# Operations

**Last Updated**: 2026-05-30

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

### Temporary action override

`CompleteDragAction` can override drop behavior for a single transfer:
- default binding: `CompleteDrag(null)`
- ctrl binding: `CompleteDrag(DropRequestPolicy.WithSwap())`
- shift binding: `CompleteDrag(DropRequestPolicy.WithFindAlternative())`
- action settings can also override `AllowPartial` and `AlternativePlacementMode`

This is operation-scoped. It does not mutate inventory defaults.

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
5. Same-inventory slot-target fallback does not reshuffle unrelated slots
6. Same-inventory area drop excludes the source slot; if the target inventory is dynamic, execution can create a new target slot

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

## Same-Inventory Area Drop

For drops onto an inventory area with no explicit target slot:

1. `InventoryDropArea` still builds normal preview context.
2. If preview suggests the source slot for a same-inventory drop, the slot hint is cleared.
3. `TransferPlanner` excludes the source slot from same-inventory area-drop candidates.
4. `TransferPlanExecutor` can ask `IDynamicSlotLifecycle.TryCreateSlot(...)` for a new target slot.
5. Normal `TryAddToSlot` / split / event dispatch handles the mutation.

This keeps layout components out of transfer semantics. `FreeFormSlotLayout` only positions dynamically created slots through `OnSlotCreated`.

## Auto-Transfer (Quick Click / Actions)

Auto-transfer uses the same planner/executor pipeline as manual drag & drop.

## Key Files

- `Scripts/DragAndDropManager.cs`
- `Scripts/Inventories/InventoryDropProcessor.cs`
- `Scripts/Inventories/TransferPlanner.cs`
- `Scripts/Inventories/TransferPlanExecutor.cs`
- `Scripts/Inventories/InventoryTransferService.cs`
- `Scripts/Inventories/InventoryAcceptanceRequest.cs`
- `Scripts/Interaction/InputEventRouter.cs`
- `Scripts/UI/InventoryDropArea.cs`
