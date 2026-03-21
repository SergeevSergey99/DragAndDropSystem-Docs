# Advanced Features

**Last Updated**: 2026-03-21

## Quick Click Auto-Transfer

- Implemented through `SlotInputAdapter` -> `InputEventRouter`, pointer phases, and bound actions.
- Distinguishes click vs drag by time and distance thresholds.

## Input Modality Tracking

- `InputModalityTracker` is the source of truth for `Mouse` vs `Navigation` mode.
- `InputEventRouter` no longer guesses modality on its own.
- Device-level keyboard/gamepad polling is only a minimal fallback for cold-start navigation.

## Global Input Actions

- `DefaultBindingsProfile` can execute some `InputAction` bindings without active inventory/slot.
- This is intended for global UI behaviors such as closing an already open context menu.

## Atomic Batch Execution

- Implemented via `TransferPlanExecutor` + snapshot providers.
- Prevents partial side effects for atomic policy.

## Swap Callbacks for Integrations

Swap is integrated via inventory-scoped events (not direct calls):
- `OnSwapAttempting(InventorySwapContext)` (cancelable)
- `OnSwapCompleted(InventorySwapContext)`

Used by DataBinding (subscribed in `OnEnable`/`OnDisable`) and gameplay systems.
Swap remains event-based because two inventories participate and cancellation via `context.Cancel` is needed.

## DataBinding Integration

### Direct Notification (Add/Remove)

`UniversalInventory` calls DataBinding directly (not through events):
- `DataBinding.HandleItemAdded(context)` — called from `EmitItemAdded()`
- `DataBinding.HandleItemRemoved(context)` — called from `EmitItemRemoved()`

This is possible because DataBinding has a 1:1 relationship with UniversalInventory.
The `IsSyncing` guard in base class prevents re-entrant callbacks during `ReloadUI()`.

### Swap (Event-Based)

DataBinding subscribes to swap events via `OnEnable`/`OnDisable`:
- `OnSwapAttempting` — delegates to virtual `CanSwap()` method
- `OnSwapCompleted` — delegates to virtual `OnSwapCompleted()` method

### Item Conversion Pipeline

DataBinding supports item conversion during transfers:
- `ConvertIncomingItem(item)` — convert before placement (e.g., SO → Model adapter)
- `ConvertOutgoingItem(item)` — convert before removal to target
- Chain: source.ConvertOutgoing → target.ConvertIncoming

### Template DataBindings

Two template base classes automate common patterns:

**`ListInventoryDataBinding<TData, TAdapter>`** — for list-based data sources:
- 5 primitives: `GetItems()`, `CreateAdapter()`, `ExtractData()`, `AddToData()`, `RemoveFromData()`
- Automates `OnReloadUI`, `OnItemAddedToUI`, `OnItemRemovedFromUI`

**`MappedSlotInventoryDataBinding<TData, TAdapter>`** — for slot-mapped data:
- Uses `Dictionary<ISlot, SlotBinding<TData>>` via abstract `CreateBindingMap()`
- `SlotBinding` has: `Get`, `Set`, `Clear`, `CanAccept` (optional validation)
- CanAccept enables declarative slot-type validation co-located with binding definition

## World 3D and Optional UI Systems

Optional modules remain independent from transfer core:
- `Scripts/World3D/*`
- `Scripts/Slots/SlotHoverEventListener.cs`
- `Scripts/UI/TooltipManager.cs`

Core transfer pipeline does not require these systems.
