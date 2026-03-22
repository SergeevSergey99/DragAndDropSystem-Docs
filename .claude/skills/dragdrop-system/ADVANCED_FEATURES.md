# Advanced Features

**Last Updated**: 2026-03-22

## Quick Click Auto-Transfer

- implemented through `SlotInputAdapter` -> `InputEventRouter`, pointer phases, and bound actions
- distinguishes click vs drag by time and distance thresholds

## Input Modality Tracking

- `InputModalityTracker` is the source of truth for `Mouse` vs `Navigation` mode
- `InputEventRouter` no longer guesses modality on its own

## Atomic Batch Execution

- implemented via `TransferPlanExecutor` + snapshot providers
- prevents partial side effects for atomic policy
- uses deferred event dispatch so DataBinding and subscribers see only committed outcomes

## Swap Callbacks for Integrations

Swap remains inventory-scoped and event-based:
- `OnSwapAttempting(InventorySwapContext)` (cancelable)
- `OnSwapCompleted(InventorySwapContext)`

## DataBinding Integration

### Direct Notification (Add/Remove)

`UniversalInventory` calls DataBinding directly:
- `DataBinding.HandleItemAdded(context)`
- `DataBinding.HandleItemRemoved(context)`

This is possible because DataBinding has a 1:1 relationship with `UniversalInventory`.

### Swap (Event-Based)

DataBinding subscribes to swap events via `OnEnable` / `OnDisable`:
- `OnSwapAttempting` → virtual `CanSwap()`
- `OnSwapCompleted` → virtual `OnSwapCompleted()`

### Item Conversion Pipeline

Current implementation keeps item conversion in `DataBinding`, but preview/execution now goes through
shared transfer helpers:
- `ConvertIncomingItem(item)`
- `ConvertOutgoingItem(item)`
- preview chain: `TransferItemConversionUtility` → `TryPreviewOutgoingItem` → `TryPreviewIncomingItem`
- mutation chain: source inventory applies outgoing conversion, target inventory applies incoming conversion

### Template DataBindings

`ListInventoryDataBinding<TData, TAdapter>`:
- list-based data source template
- automates reload/add/remove sync

`MappedSlotInventoryDataBinding<TData, TAdapter>`:
- slot-mapped data template
- uses `Dictionary<ISlot, SlotBinding<TData>>`
- `SlotBinding` contains `Get`, `Set`, `Clear`, `CanAccept`
- `TryGetTargetBinding()` / `TryGetSourceBinding()` centralize slot lookup safety

### Context-Aware Acceptance Preview

`InventoryAcceptanceRequest` allows:
- area-drop preview without hand-written guards in feature bindings
- strategy-side slot iteration with real drag context
- consistent validation for planner and `InventoryDropArea`

## Optional UI / World Systems

Optional modules remain independent from transfer core:
- `Scripts/World3D/*`
- `Scripts/UI/TooltipManager.cs`
- hover/listener helpers
