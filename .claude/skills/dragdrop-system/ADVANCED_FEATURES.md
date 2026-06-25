# Advanced Features

**Last Updated**: 2026-05-30

## Quick Click Auto-Transfer

- implemented through `SlotInputAdapter` -> `InputEventRouter`, pointer phases, and bound actions
- distinguishes click vs drag by time and distance thresholds

## Input Modality Tracking

- `InputModalityTracker` is the source of truth for `Mouse` vs `Navigation` mode
- `InputEventRouter` no longer guesses modality on its own

## Quiet Add / skipRules

`TryAddStackQuiet()` on `UniversalInventory` bypasses rule validation during `ReloadUI()`:

```csharp
if (ItemStack.TryCreate(adapters, out var stack))
    inventory.TryAddStackQuiet(stack, slotIndex);
// internally calls strategy.TryAddQuite(...) which sets skipRules = true
```

Use this (via `AddToUIQuiet()` in DataBinding) when populating slots from data — not from a user drag. Normal `TryAdd` (used by transfer pipeline) always evaluates rules.

## ItemStack Runtime Model

Current `ItemStack` behavior:
- `PrimaryAdapter` is the representative adapter for UI, rule checks, type checks, and casts
- `ItemAdapter` is kept as a compatibility alias to `PrimaryAdapter`
- `Adapters` stores the concrete adapter instances currently contained in the stack
- `Count` is derived from `Adapters.Count`

Current helper APIs:
- `ItemStack.TryCreate(...)` — safe creation from adapter list
- `TryAddToStack(...)` — safe merge without runtime exceptions
- `Split(...)` / `CreateCopy(...)` — preserve concrete adapter instances

Important note:
- bindings and rules that only need adapter type or metadata should keep using `PrimaryAdapter`
- systems that need true instance identity must not reconstruct stacks only from representative adapter + count

## Occupied Slot Handler

Allows a DataBinding to intercept a drop on an occupied slot **before** normal blocked-target behavior (swap/findAlternative/reject) continues.

Implement one of the timing interfaces on the target inventory DataBinding:
```csharp
public class ContainerBinding : SlotIndexedInventoryDataBinding<ItemModel, ItemModelAdapter>,
    IPreRuleOccupiedSlotDropHandler
{
    public bool CheckOccupiedSlotDrop(DragEntry entry, BaseSlot occupiedSlot) => ...;
    public bool ExecuteOccupiedSlotDrop(DragEntry entry, BaseSlot occupiedSlot) => ...;
}
```

Pipeline integration:
1. The transfer service sees an occupied explicit target.
2. `IPreRuleOccupiedSlotDropHandler` runs before target drop rules and can bypass them when the real target is the object inside the slot.
3. `IPostRuleOccupiedSlotDropHandler` runs after target drop rules pass.
4. If a handler accepts, it invokes `ExecuteOccupiedSlotDrop(...)` in the current entry transaction and owns the custom mutation.
5. If rejected, normal blocked-target resolution continues.

Example: Demo5 Containers — `PlayerContainerInventoryDataBinding` uses this to insert a dragged item into a container when dropped on it. If the container is full or the drop would create a cycle, the hook returns false and swap/other behavior applies normally.

## Sequential Batch Execution

- entries execute sequentially against current inventory state
- each entry has its own rollback checkpoint
- failed entries do not undo earlier committed entries
- events are deferred until the current entry commits

## Free-Form Dynamic Slots

`FreeFormSlotLayout` is a layout component, not a transfer handler:
- it records the pending drop screen position
- it positions newly created slots through `OnSlotCreated`
- it should not split stacks, create items, or emit inventory events

Same-inventory area drops are handled by the core pipeline:
- candidate resolution excludes the source placement
- execution can create a dynamic target slot via `IDynamicSlotLifecycle.TryCreateSlot(...)`
- normal split/move/event dispatch then applies

## Swap Callbacks for Integrations

Swap remains inventory-scoped and event-based:
- `OnSwapAttempting(InventorySwapContext)` (cancelable)
- `OnSwapCompleted(InventorySwapContext)`

Transfer-level domain hooks now also run for swap path:
- domain validation executes before swap commit
- domain success hooks are deferred until the entry commits

## DataBinding Integration

### Direct Notification (Add/Remove)

`UniversalInventory` calls DataBinding directly:
- `DataBinding.HandleItemAdded(context)`
- `DataBinding.HandleItemRemoved(context)`

This is possible because DataBinding has a 1:1 relationship with `UniversalInventory`.

### Occupied Slot Drop Hooks

See [Occupied Slot Handler](#occupied-slot-handler) above. These are called by the transfer service — not via events:
- `CheckOccupiedSlotDrop(entry, BaseSlot)` — pure check
- `ExecuteOccupiedSlotDrop(entry, BaseSlot)` — full mutation
- choose `IPreRuleOccupiedSlotDropHandler` or `IPostRuleOccupiedSlotDropHandler` for timing

### Swap (Event-Based)

DataBinding subscribes to swap events via `OnEnable` / `OnDisable`:
- `OnSwapAttempting` → virtual `CanSwap()`
- `OnSwapCompleted` → virtual `OnSwapCompleted()`

### Item Conversion Pipeline

Current implementation keeps item conversion on `UniversalInventory` via `ItemConverter`, and preview/execution go through
shared transfer helpers:
- `IInventoryItemConverter`
- `IdentityInventoryItemConverter`
- preview chain: `TransferItemConversionUtility` → `TryPreviewOutgoingItem` → `TryPreviewIncomingItem`
- mutation chain: source inventory applies outgoing conversion, target inventory applies incoming conversion
- `InventoryDataBindingBase` configures converter via `CreateItemConverter()` (returns null → identity)

### Template DataBindings

`ListInventoryDataBinding<TData, TAdapter>`:
- list-based data source template
- automates reload/add/remove sync

`MappedSlotInventoryDataBinding<TData, TAdapter>`:
- slot-mapped data template
- uses `Dictionary<BaseSlot, SlotBinding<TData, TAdapter>>`
- `SlotBinding` internally list-based: `GetAll` (returns TData for reload), `Add`/`Remove` (receive TAdapter lists), `Clear`, `CanDrop`/`CanStartDrag` (receive TAdapter)
- two constructors: simple (single-item: `get/set/clear`) and stacking (`getAll/add/remove/clear`)
- both can be mixed in the same `CreateBindingMap()` dictionary
- no `ExtractData` needed — adapters passed directly (consistent with List/SlotIndexed templates)
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
