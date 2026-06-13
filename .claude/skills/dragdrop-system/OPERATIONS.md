# Operations

**Last Updated**: 2026-06-13

## Manual Drag And Drop

1. `DragAndDropManager` resolves the active `IDropTarget`.
2. `InventoryDropProcessor` resolves `ResolvedDropPolicy`.
3. `InventoryTransferService.CanAttempt(...)` performs an advisory read-only probe.
4. `InventoryTransferService.ExecuteBatch(...)` processes entries sequentially.
5. Each entry resolves conversion, validates the explicit target or automatic candidates,
   mutates current inventory state, and commits notifications.

There is no materialized plan or virtual inventory state.

## Explicit Target

1. Resolve the selected slot to a placement anchor.
2. Call `IStrategy.TryGetCandidate(...)` directly.
3. Do not enumerate candidates and do not invoke an orderer for this first attempt.
4. If blocked, apply `BlockedTargetResolutionKind`:
   - `Reject`
   - `AlternativeSlots`
   - `Swap`
5. If part of the entry remains after a successful placement, continue through automatic
   candidate enumeration.

## Automatic Placement

Area drop, auto-transfer, blocked-target alternatives, and entry remainder distribution use:

1. `IStrategy.GetCandidates(...)`
2. `PlacementCandidateOrderer`
3. JIT candidate validation against current state
4. one mutation
5. fresh enumeration while a remainder exists

Shaped and single-cell items use the same loop. `IPlacementInventory.Topology` determines
the projected footprint.

## Batch

- Batch is sequential best-effort.
- Each entry has its own snapshot boundary.
- A failed entry rolls back without reverting earlier committed entries.
- Later entries see earlier mutations and DataBinding updates.
- `PartialTransferMode.Allow` leaves only the amount that did not fit in the source.
- Batch swap is rejected.

## Swap

- Swap requires one full entry and two placement-capable, snapshot-capable inventories.
- Both directions run conversion, rules, topology, and domain validation.
- Both placements are removed before the incoming footprints are checked.
- Failure restores both inventories.

## Auto-Transfer

Auto-transfer builds a normal `DragContext` and enters the same automatic candidate loop.
It supports shaped items; topology decides whether and where their footprints fit.

## Key Files

- `Scripts/DragAndDropManager.cs`
- `Scripts/Inventories/InventoryDropProcessor.cs`
- `Scripts/Inventories/InventoryTransferEngine.cs`
- `Scripts/Inventories/Strategies/IStrategy.cs`
- `Scripts/Inventories/IPlacementInventory.cs`
- `Scripts/Inventories/PlacementCandidateOrderer.cs`
- `Scripts/Inventories/AutoTransferService.cs`
