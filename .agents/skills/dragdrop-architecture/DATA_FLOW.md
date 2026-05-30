# Data Flow

**Last Updated**: 2026-05-30

## Manual Drop Flow

1. `DragAndDropManager` finalizes drag and resolves active drop target.
2. Target provides `IDropProcessor` (typically `InventoryDropProcessor`).
3. `InventoryDropProcessor` resolves effective policy and validation context.
4. For area drops, `InventoryDropArea` resolves target-side preview item and builds `InventoryAcceptanceRequest`.
5. `TransferPlanner.BuildPlan(...)` returns `TransferPlan`.
6. `TransferPlanExecutor.Execute(...)` applies plan.
7. Deferred events are emitted after successful completion via `DispatchTransferEvents()`.

Current stack note:
- drag entries now carry `ItemStack` copies built from concrete adapter lists, not just `(item, count)`
- executor split/merge paths move real adapter lists between stacks via `Split()` / `TryAddToStack()`
- event payloads still expose representative `ItemAdapter + Count`, so external consumers remain aggregate-facing for now

## Planner Flow

Input:
- `DragContext`
- target inventory/slot hint
- `ResolvedDropPolicy`
- global rules

Pre-planning preview:
- source item is converted to target preview item via `TransferItemConversionUtility`
- `InventoryAcceptanceRequest` is created once per entry
- `targetInventory.GetAcceptableCount(request)` evaluates capacity in real drag context

Output:
- `TransferPlan` with entries of type:
  - allocation entry (slot allocations)
  - occupied handler entry (`RequiresOccupiedHandler`) — checked before swap
  - swap entry (`RequiresSwap`)
- same-inventory area drops exclude the source slot from candidate search

Internal helper objects:
- `EntryPlanningOperation`
- `VirtualSlotState`

## Executor Flow

For each planned entry:
- if allocation entry: execute transfer allocations through executor internal transfer helpers
- if occupied handler entry: call `DataBinding.ExecuteOccupiedSlotDrop(entry, slot)` — handler owns full mutation
- if swap entry: validate + execute swap

Batch policy:
- `Atomic`: rollback all on first failure
- `BestEffort`: continue and report partial failures

Normal transfer execution uses:
- `InventoryTransferRequest`
- `TargetPlacementOperation`
- `AlternativeSlotSearchOperation`
- `IDynamicSlotLifecycle.TryCreateSlot(...)` when a same-inventory area drop needs a new dynamic target slot

`InventoryTransferResult` carries both:
- `SourceItem`
- `TargetItem`

This keeps event payloads correct for cross-inventory adapter conversion.

## Event Dispatch Flow

`TransferPlanExecutor.DispatchTransferEvents()` is the sole event emitter for transfers:

```text
DispatchTransferEvents()
  → UniversalInventory.EmitItemRemoved(sourceItem)
      → DataBinding.HandleItemRemoved(context)
      → OnItemRemoved?.Invoke(context)
  → UniversalInventory.EmitItemAdded(targetItem)
      → DataBinding.HandleItemAdded(context)
      → OnItemAdded?.Invoke(context)
```

Key design:
- `TryAddToSlot` is pure mutation, no events
- DataBinding is notified directly (1:1 relationship), not via events
- external subscribers still use inventory events
- `IsSyncing` guard prevents re-entrant callbacks during `ReloadUI()`

## Item Conversion Flow

During preview/planning/execution, target-side item is resolved without mutating source stack:

```text
TransferItemConversionUtility.TryResolveTargetItem(...)
  → sourceUniversal.TryPreviewOutgoingItem(item)
  → targetUniversal.TryPreviewIncomingItem(convertedItem)
  → planner/executor work with target preview item
```

Actual stack mutation later uses the same conversion chain inside `UniversalInventory`.

Important current caveat:
- conversion APIs are still representative-adapter-based; `ReplaceItem()` updates all adapters inside the stack uniformly

## Occupied Slot Handler Flow

When the planner finds that allocation = 0 for a drop on an occupied slot, it checks the DataBinding hook **before** swap/findAlternative:

```
TransferPlanner.PlanEntry()
  → plannedAmount == 0, targetSlot occupied
  → targetInventory.CheckOccupiedSlotDrop(entry, slot)
      → DataBinding.CanHandleOccupiedSlotDrop()
          → true:  plan RequiresOccupiedHandler entry
          → false: continue to normal BlockedTargetBehavior logic

TransferPlanExecutor.ExecuteCore()
  → RequiresOccupiedHandler == true
  → targetInventory.ExecuteOccupiedSlotDrop(entry, slot)
      → DataBinding.ExecuteOccupiedSlotDrop()
          → handler owns full mutation: add to target, clear source slot, update visuals
```

Virtual hooks in `InventoryDataBindingBase`:
- `CanHandleOccupiedSlotDrop(DragEntry, BaseSlot)` — pure check, no mutation
- `ExecuteOccupiedSlotDrop(DragEntry, BaseSlot)` — performs mutation, returns success

## Swap Flow

1. Planner marks swap candidate if allocation failed, occupied handler not matched, and `BlockedTargetBehavior = Swap`.
2. Executor validates reverse and forward rule compatibility.
3. `SwapAttempting` callback can cancel (`InventorySwapContext.Cancel = true`).
4. Executor calls `UniversalInventory.TrySwapSlots(...)`.
5. `SwapCompleted` event and inventory events are emitted after successful plan completion.

Note: swap uses event subscriptions because two inventories participate.

## Same-Inventory Area Drop

For an area drop with no explicit target slot and the same source/target inventory:

```text
InventoryDropArea.TryBuildValidationContext()
  → if suggested slot == source slot, clear target slot hint

TransferPlanner.PlanEntry()
  → exclude source slot from same-inventory area-drop candidates
  → if existing slots cannot accept, allow deferred placement when capacity exists

TransferPlanExecutor.TryAddToTargetInventory()
  → TryAddToNewDynamicSlotForSameInventoryAreaDrop()
      → IDynamicSlotLifecycle.TryCreateSlot(out targetSlot)
      → targetInventory.TryAddToSlot(..., targetSlot, ...)
      → normal result/event dispatch

FreeFormSlotLayout
  → observes OnSlotCreated
  → positions the newly created slot at the pending drop point
```

This keeps layout code out of item mutation while still allowing free-form same-inventory split/move into a new slot.

## Event Safety Principle

In atomic mode, transfer and swap events are deferred until success to avoid false-positive subscriber side effects.
