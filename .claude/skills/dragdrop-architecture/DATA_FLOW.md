# Data Flow

**Last Updated**: 2026-03-29

## Manual Drop Flow

1. `DragAndDropManager` finalizes drag and resolves active drop target.
2. Target provides `IDropProcessor` (typically `InventoryDropProcessor`).
3. `InventoryDropProcessor` resolves effective policy and validation context.
4. For area drops, `InventoryDropArea` resolves target-side preview item and builds `InventoryAcceptanceRequest`.
5. `TransferPlanner.BuildPlan(...)` returns `TransferPlan`.
6. `TransferPlanExecutor.Execute(...)` applies plan.
7. Deferred events are emitted after successful completion via `DispatchTransferEvents()`.

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
- `CanHandleOccupiedSlotDrop(DragEntry, ISlot)` — pure check, no mutation
- `ExecuteOccupiedSlotDrop(DragEntry, ISlot)` — performs mutation, returns success

## Swap Flow

1. Planner marks swap candidate if allocation failed, occupied handler not matched, and `BlockedTargetBehavior = Swap`.
2. Executor validates reverse and forward rule compatibility.
3. `SwapAttempting` callback can cancel (`InventorySwapContext.Cancel = true`).
4. Executor calls `UniversalInventory.TrySwapSlots(...)`.
5. `SwapCompleted` event and inventory events are emitted after successful plan completion.

Note: swap uses event subscriptions because two inventories participate.

## Same-Inventory FindAlternative Rule

If source and target inventory are the same, `FindAlternative` does not redistribute items across other slots of that inventory. The operation either uses the explicit target slot or leaves the item in place.

## Event Safety Principle

In atomic mode, transfer and swap events are deferred until success to avoid false-positive subscriber side effects.
