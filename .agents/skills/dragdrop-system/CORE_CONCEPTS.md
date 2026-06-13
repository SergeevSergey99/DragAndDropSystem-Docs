# Core Concepts

**Last Updated**: 2026-06-14

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

`RuleEvaluationService` is used by the transfer service to validate candidates and swap directions.

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

## 4. JIT Transfer Service

`Scripts/Inventories/InventoryTransferEngine.cs`

- validates and executes against current inventory state
- checks an explicit target directly or enumerates ordered automatic candidates
- requests fresh candidates after each mutation
- processes batch entries sequentially with per-entry rollback
- creates dynamic targets through `IDynamicSlotLifecycle`
- emits events only after the current entry commits
- has no materialized plan, virtual occupancy, or batch-wide `Atomic` mode

Important current detail:
- transfer outcomes distinguish `SourceItem` and `TargetItem`
- this keeps event payloads correct for cross-inventory adapter conversion

## 5. Swap Is First-Class in Pipeline

Current flow:
- transfer service enters swap only for a blocked explicit target and a single full entry
- service validates reverse and forward drop legality
- `SwapAttempting` callback can cancel
- service mutates both placements inside the current entry transaction
- `SwapCompleted` callback runs after successful commit

## 6. Event Architecture

`UniversalInventory.TryAddToSlot()` is pure mutation, no events emitted internally.

Events are emitted only after an entry commits:
- `EmitItemAdded()` → direct `DataBinding.HandleItemAdded()` call, then `OnItemAdded`
- `EmitItemRemoved()` → direct `DataBinding.HandleItemRemoved()` call, then `OnItemRemoved`

The system prefers deferred event dispatch for consistency:
- no false-positive events on entry rollback
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

## 8. Conversion Is Previewed Before Execution

`Scripts/Inventories/TransferItemConversionUtility.cs`

- source inventory preview-converts outgoing item
- target inventory preview-converts incoming item
- probe, drop area, and execution work with the target-side preview item

Current note:
- conversion now lives on inventory-side `ItemConverter`
- `DataBinding` only provides wiring plus legacy fallback when needed

## 9. Handler Boundary

`InventoryDropProcessor` is the adapter between UI/manager targets and pipeline internals.

Responsibilities:
- resolve effective target and policy
- expose advisory probe data
- execute the JIT transfer with options
- return `DropResult`

## 10. Asynchronous Transfer-Wide Veto

`IAsyncTransferDomainHandler.CanStartTransferAsync(...)` is optional. The asynchronous execution
path invokes it once before the first mutation, after synchronous transfer-wide validation.
Implementations may perform remote validation or their own simulation; simulation is not required
or supplied by the core.

The synchronous execution API rejects transfers when an async handler is attached, preventing the
check from being silently bypassed. Preview remains synchronous and does not invoke this handler.

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
