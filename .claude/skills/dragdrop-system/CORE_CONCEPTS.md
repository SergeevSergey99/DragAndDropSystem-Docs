# Core Concepts

**Last Updated**: 2026-03-22

## 1. DragContext Is Runtime Source of Truth

`Scripts/Core/DragContext.cs`

- contains drag entry data, source and target hints
- must be treated as ephemeral runtime state
- should not be duplicated with extra lock flags unless strictly required

## 2. Rule Validation Is Layered

Validation order:
1. global rules
2. inventory-level rules
3. slot-level rules

`RuleEvaluationService` is used by planner/executor to validate candidates and swap directions.

## 3. Policy-Driven Transfer Behavior

`Scripts/Core/DropPolicy.cs`

Policy defines:
- occupied target handling
- partial vs strict capacity
- atomic vs best effort batch
- strict target vs hint target

## 4. Planner/Executor Split

### Planner

`Scripts/Inventories/TransferPlanner.cs`

- pure planning layer
- uses target-side preview conversion and `InventoryAcceptanceRequest`
- builds `TransferPlan` with `PlannedEntryTransfer` entries
- uses virtual slot state to avoid overbooking in batch planning
- can mark entry as `RequiresSwap`

### Executor

`Scripts/Inventories/TransferPlanExecutor.cs`

- mutation layer
- executes normal allocations through `InventoryTransferService`
- executes swap branch when planned
- supports rollback in `Atomic` mode
- emits transfer/swap events only after successful completion

Important current detail:
- transfer outcomes distinguish `SourceItem` and `TargetItem`
- this keeps event payloads correct for cross-inventory adapter conversion

## 5. Swap Is First-Class in Pipeline

Current flow:
- planner marks swap candidate
- executor validates reverse and forward drop legality
- `SwapAttempting` callback can cancel
- `UniversalInventory.TrySwapSlots` performs swap
- `SwapCompleted` callback runs after successful plan completion

## 6. Event Architecture

`UniversalInventory.TryAddToSlot()` is pure mutation, no events emitted internally.

Events are emitted only by `TransferPlanExecutor.DispatchTransferEvents()`:
- `EmitItemAdded()` → direct `DataBinding.HandleItemAdded()` call, then `OnItemAdded`
- `EmitItemRemoved()` → direct `DataBinding.HandleItemRemoved()` call, then `OnItemRemoved`

The system prefers deferred event dispatch for consistency:
- no false-positive events on atomic rollback
- predictable order for DataBinding consumers

## 7. Preview Acceptance Is Context-Aware

`Scripts/Inventories/InventoryAcceptanceRequest.cs`

- acceptance preview is no longer just `(item, count)`
- request can carry source inventory, source slot, target inventory and original drag context
- strategies validate actual candidate slots through `UniversalInventory.CanAcceptByRules(...)`

This matters for:
- area drops
- mapped-slot inventories
- cross-inventory adapter conversion

## 8. Conversion Is Previewed Before Planning

`Scripts/Inventories/TransferItemConversionUtility.cs`

- source inventory preview-converts outgoing item
- target inventory preview-converts incoming item
- planner, drop area, and transfer service all work with target-side preview item

Current note:
- conversion still lives in `DataBinding`
- roadmap proposes moving it to dedicated converters later

## 9. Handler Boundary

`InventoryDropProcessor` is the adapter between UI/manager targets and pipeline internals.

Responsibilities:
- resolve effective target and policy
- request plan
- execute plan with options
- return `DropResult`

## 10. Input Layers Are Separated

Input responsibilities are split:
- `InputModalityTracker`
- `InputEventRouter`
- `SlotInputAdapter` / `InventoryDropArea`

This keeps modality detection out of transfer and slot-domain logic.
