# Components

**Last Updated**: 2026-03-23

## DragAndDropManager

Location: `Scripts/DragAndDropManager.cs`

Responsibilities:
- drag lifecycle orchestration
- active drop target resolution
- global rules access
- routing to `IDropProcessor`
- public swap events (`OnSwapAttempting`, `OnSwapCompleted`)

Note: manager no longer owns transfer branching logic directly.

## InputModalityTracker / InputEventRouter

Locations:
- `Scripts/Interaction/InputModalityTracker.cs`
- `Scripts/Interaction/InputEventRouter.cs`

Responsibilities:
- `InputModalityTracker`: scene-level `Mouse` vs `Navigation` state and modality-change events
- `InputEventRouter`: binding resolution, inventory runtime-state, pointer phase classification, global/default `InputAction` routing

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
- target-side preview conversion via `TransferItemConversionUtility`
- inventory acceptance preview via `InventoryAcceptanceRequest`
- virtual-slot allocation for batch operations
- rule-aware target candidate selection
- planning swap entries (`RequiresSwap` + `SwapTargetSlot`)

Key helper objects:
- `EntryPlanningOperation`
- `VirtualSlotState`

## TransferPlanExecutor

Location: `Scripts/Inventories/TransferPlanExecutor.cs`

Responsibilities:
- execute plan entries in sequence
- run normal transfers through internal execution helpers
- run swap branch with bidirectional rule validation
- support atomic rollback through snapshots
- defer transfer/swap event dispatch until operation success
- dispatch remove/add with final transfer outcomes

## InventoryTransfer Models

Location: `Scripts/Inventories/InventoryTransferService.cs`

Responsibilities:
- define `InventoryTransferRequest`
- define `InventoryTransferResult`
- carry concrete transfer payload between executor helpers and event dispatch

Related execution helpers:
- `TargetPlacementOperation`
- `AlternativeSlotSearchOperation`
- `InventoryAcceptanceRequest`

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
- preview and apply item conversion via `TryPreviewIncomingItem()` / `TryPreviewOutgoingItem()`
- evaluate slot rules for acceptance preview using `InventoryAcceptanceRequest`

Notes:
- `TryAddToSlot` is pure mutation, no events emitted internally
- events are emitted only by `TransferPlanExecutor.DispatchTransferEvents()`
- dynamic slot creation is still split between strategy wrapping (`DynamicSlotDecorator`)
  and inventory-level slot lifecycle methods such as `EnsureFreeSlots()` / `HandleSlotEmptied()`

## Acceptance Preview

Key classes:
- `InventoryAcceptanceRequest`
- `TransferItemConversionUtility`

Responsibilities:
- keep slot-specific preview validation out of feature bindings
- let strategies ask "can this inventory accept this transfer in this context?"
- support area-drop and planner preview with the same request model

## DataBinding System

Location: `Scripts/DataBinding/`

Key classes:
- `InventoryDataBindingBase` — base class with direct notification (`HandleItemAdded`/`HandleItemRemoved`),
  swap event subscriptions, sync scope, rule integration, and item conversion pipeline
- `ListInventoryDataBinding<TData, TAdapter>` — template for list-based data sources
- `MappedSlotInventoryDataBinding<TData, TAdapter>` — template for slot-mapped data with `Dictionary<ISlot, SlotBinding<TData>>`
  plus `TryGetTargetBinding()` / `TryGetSourceBinding()` helpers

Responsibilities:
- bidirectional sync between UI (`UniversalInventory`) and external data
- converter wiring during inventory initialization
- rule integration (`CanStartDrag`, `CanDrop`, `CanSwap`)
- swap handling via event subscriptions (`OnSwapAttempting` / `OnSwapCompleted`)

Current note:
- conversion now lives on inventory-side `ItemConverter`
- `DataBinding` only wires converter in and provides a legacy fallback path
