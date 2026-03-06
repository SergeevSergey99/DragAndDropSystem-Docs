# Core Concepts

**Last Updated**: 2026-02-28

## 1. DragContext Is Runtime Source of Truth

`Scripts/Core/DragContext.cs`

- Contains drag entry data (source, stack, target hints).
- Must be treated as ephemeral runtime state.
- Do not duplicate drag state with extra lock flags unless strictly required.

## 2. Rule Validation Is Layered

Validation order stays:
1. Global rules (`DragAndDropManager.GlobalRules`)
2. Inventory-level rules (`UniversalInventory` / DataBinding)
3. Slot-level rules (`ISlot.SlotRuleValidator`)

`RuleEvaluationService` is used by planner/executor to validate candidates and swap directions.

## 3. Policy-Driven Transfer Behavior

`Scripts/Core/DropPolicy.cs`

Policy is no longer an optional side behavior; it is the contract for transfer semantics:
- occupied target handling
- partial vs strict capacity
- atomic vs best effort batch
- strict target vs hint target

## 4. Planner/Executor Split

### Planner
`Scripts/Inventories/TransferPlanner.cs`

- Pure planning layer (no mutations).
- Builds `TransferPlan` with `PlannedEntryTransfer` entries.
- Uses virtual slot state to avoid overbooking in batch planning.
- Can mark entry as `RequiresSwap`.

### Executor
`Scripts/Inventories/TransferPlanExecutor.cs`

- Mutation layer.
- Executes normal allocations through `InventoryTransferService`.
- Executes swap branch when planned.
- Supports rollback in `Atomic` mode.
- Emits transfer/swap events only after successful completion.

## 5. Swap Is First-Class in Pipeline

Swap is not a legacy side branch anymore.

Current flow:
- planner marks swap candidate (`RequiresSwap`)
- executor validates reverse and forward drop legality
- `SwapAttempting` callback can cancel
- `UniversalInventory.TrySwapSlots` performs swap
- `SwapCompleted` callback runs after successful plan completion

## 6. Event Consistency

The system prefers deferred event dispatch for consistency:
- no false-positive events on atomic rollback
- predictable order for DataBinding consumers

## 7. Handler Boundary

`InventoryDropProcessor` is the adapter between UI/manager targets and pipeline internals.

Responsibilities:
- resolve effective target+policy
- request plan
- execute plan with options (global rules, swap callbacks)
- return `DropResult`
