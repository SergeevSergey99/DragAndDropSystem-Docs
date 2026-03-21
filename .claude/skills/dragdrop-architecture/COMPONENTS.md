# Components

**Last Updated**: 2026-03-21

## DragAndDropManager

Location: `Scripts/DragAndDropManager.cs`

Responsibilities:
- drag lifecycle orchestration
- active drop target resolution
- global rules access
- routing to `IDropProcessor`
- public swap events (`OnSwapAttempting`, `OnSwapCompleted`)

Note: manager no longer owns all transfer branching logic directly.

## InputModalityTracker / InputEventRouter

Locations:
- `Scripts/Interaction/InputModalityTracker.cs`
- `Scripts/Interaction/InputEventRouter.cs`

Responsibilities:
- `InputModalityTracker`: scene-level `Mouse` vs `Navigation` state and modality-change events
- `InputEventRouter`: binding resolution, inventory runtime-state, pointer phase classification, global/default `InputAction` routing

Note: modality detection is intentionally separated from inventory action routing.

## InventoryDropProcessor

Location: `Scripts/Inventories/InventoryDropProcessor.cs`

Responsibilities:
- resolve effective target inventory/slot
- resolve effective `DropPolicy`
- invoke planner and executor
- pass execution options (global rules, swap callbacks)

This is the adapter between UI target layer and transfer core.

## DropPolicy

Location: `Scripts/Core/DropPolicy.cs`

Defines 4 behavior dimensions:
- `OccupiedTargetPolicy`
- `CapacityPolicy`
- `BatchExecutionPolicy`
- `TargetUsagePolicy`

Supports defaults and inspector overrides via `DropPolicySettings`.

## TransferPlanner

Location: `Scripts/Inventories/TransferPlanner.cs`

Responsibilities:
- pure planning from drag context and policy
- virtual-slot allocation for batch operations
- rule-aware target candidate selection
- planning swap entries (`RequiresSwap` + `SwapTargetSlot`)

## TransferPlanExecutor

Location: `Scripts/Inventories/TransferPlanExecutor.cs`

Responsibilities:
- execute plan entries in sequence
- run normal transfers via `InventoryTransferService`
- run swap branch with bidirectional rule validation
- support atomic rollback through snapshots
- defer transfer/swap event dispatch until operation success

## InventoryTransferService

Location: `Scripts/Inventories/InventoryTransferService.cs`

Responsibilities:
- transactional source->target movement primitive
- acceptable count calculation
- target placement (direct/alternative paths)
- snapshot rollback on failure

## UniversalInventory / ISlot

Locations:
- `Scripts/Inventories/UniversalInventory.cs`
- `Scripts/Slots/ISlot.cs`

Responsibilities:
- store and mutate item stacks
- run inventory/slot rule checks
- provide concrete slot-level mutations and visuals
- support `TrySwapSlots` for swap execution
- notify DataBinding directly via `HandleItemAdded()`/`HandleItemRemoved()` (not events)
- apply item conversion pipeline via DataBinding's `ConvertIncomingItem()`/`ConvertOutgoingItem()`

Note: `TryAddToSlot` is pure mutation — no events emitted internally. Events are only emitted
by `TransferPlanExecutor.DispatchTransferEvents()` which calls `EmitItemAdded()`/`EmitItemRemoved()`.

## DataBinding System

Location: `Scripts/DataBinding/`

Key classes:
- `InventoryDataBindingBase` — base class with direct notification (`HandleItemAdded`/`HandleItemRemoved`),
  swap event subscriptions, sync scope, rule integration, and item conversion pipeline
- `ListInventoryDataBinding<TData, TAdapter>` — template for list-based data sources
- `MappedSlotInventoryDataBinding<TData, TAdapter>` — template for slot-mapped data with `Dictionary<ISlot, SlotBinding<TData>>`

Responsibilities:
- bidirectional sync between UI (UniversalInventory) and external data
- item conversion (ConvertIncomingItem / ConvertOutgoingItem)
- rule integration (virtual CanStartDrag, CanDrop, CanSwap)
- swap handling via event subscriptions (OnSwapAttempting / OnSwapCompleted)
