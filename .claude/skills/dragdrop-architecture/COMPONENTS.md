# Components

**Last Updated**: 2026-02-28

## DragAndDropManager

Location: `Scripts/DragAndDropManager.cs`

Responsibilities:
- drag lifecycle orchestration
- active drop target resolution
- global rules access
- routing to `IItemDropHandler`
- public swap events (`OnSwapAttempting`, `OnSwapCompleted`)

Note: manager no longer owns all transfer branching logic directly.

## InventoryDropHandler

Location: `Scripts/Inventories/InventoryDropHandler.cs`

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
