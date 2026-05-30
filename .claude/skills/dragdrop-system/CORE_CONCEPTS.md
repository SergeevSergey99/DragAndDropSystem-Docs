# Core Concepts

**Last Updated**: 2026-05-30

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

`Scripts/Core/Drop/DropPolicy.cs`

Policy defines:
- blocked target handling
- partial transfer permission
- batch mode
- alternative placement order

Current policy layers:
- `DropRequestPolicy`
- `DropPolicySettings`
- `ResolvedDropPolicy`

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
- executes normal allocations through internal placement helpers
- can create a new dynamic target slot for same-inventory area drops via `IDynamicSlotLifecycle.TryCreateSlot(...)`
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
- planner, drop area, and executor all work with target-side preview item

Current note:
- conversion now lives on inventory-side `ItemConverter`
- `DataBinding` only provides wiring plus legacy fallback when needed

## 9. Handler Boundary

`InventoryDropProcessor` is the adapter between UI/manager targets and pipeline internals.

Responsibilities:
- resolve effective target and policy
- request plan
- execute plan with options
- return `DropResult`

Current note:
- same-inventory slot-target fallback still avoids reshuffling unrelated slots
- same-inventory area drops exclude the source slot; dynamic inventories can create a new target slot so split/move operations do not fall back into the source

## 10. Input Layers Are Separated

Input responsibilities are split:
- `InputModalityTracker`
- `InputEventRouter`
- `SlotInputAdapter` / `InventoryDropArea`

This keeps modality detection out of transfer and slot-domain logic.

## 11. Dynamic Slot Lifecycle

`Scripts/Inventories/InventoryRuntimeCapabilities.cs`

`IDynamicSlotLifecycle` is the runtime capability for dynamic slot creation/removal:
- `TryCreateSlot(out BaseSlot)` lets the executor create an explicit target slot when a plan needs a new slot.
- `HandleSlotEmptied(BaseSlot)` trims excess empty slots after committed removals.

Layout components such as `FreeFormSlotLayout` do not mutate stacks. They react to `OnSlotCreated` and position newly created slot transforms.
