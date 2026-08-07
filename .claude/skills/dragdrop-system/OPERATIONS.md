# Operations

**Last Updated**: 2026-08-06

## Manual Drag And Drop

1. `DragAndDropManager` resolves the active `IDropTarget`.
2. `InventoryDropProcessor` resolves `ResolvedDropPolicy`.
3. `InventoryDropProcessor.ProbeDrop(...)` performs an advisory read-only probe with that policy.
4. `InventoryTransferService.ExecuteBatch(...)` processes entries sequentially.
5. Each entry validates rules — `RuleEvaluationService.ValidateEntryDrop(...)` converts the item to
   the target domain first — then validates the explicit target or automatic candidates, mutates
   current inventory state, and commits notifications.

There is no materialized plan or virtual inventory state.

## Drop Preview And Feedback

1. `SlotInputAdapter.OnBecomeActiveTarget` takes the probe from
   `DragAndDropManager.CurrentProcessor` (already bound to this slot by `ActivateTopTarget`).
2. `DropPreviewController.ShowDropPreview(slot, context, probe)` resolves the covered footprint and
   one `DropVerdict`, then highlights the slots.
3. Feedback visuals read the verdict back through `TryGetActiveDropVerdict(...)`.

One probe per hover, resolved with the policy the drop itself will use. A slot that runs its own
probe can disagree with the drop — see `ANTIPATTERNS.md`.

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
- Both directions run conversion, rules, topology, and domain validation. The counterpart goes
  through `ValidateSwapCounterpart(...)`: `ValidateEntryStart` against the target slot it leaves,
  then `ValidateEntryDrop` against the source slot it lands in.
- Counterpart validation runs before any mutation, and also inside `Probe(...)` so the preview
  agrees with execution.
- Both placements are removed before the incoming footprints are checked.
- Failure restores both inventories; a counterpart refusal moves neither item.

## Auto-Transfer

Auto-transfer builds a normal `DragContext` and enters the same automatic candidate loop.
It supports shaped items; topology decides whether and where their footprints fit.

A partial auto-transfer takes the **tail** of the source stack (`CreateCopy(dragAmount)`), matching
`ItemStack.Split`, so rules, conversion and mutation all refer to the same adapter instances.

## Key Files

- `Scripts/DragAndDropManager.cs`
- `Scripts/Inventories/InventoryDropProcessor.cs`
- `Scripts/Inventories/InventoryTransferEngine.cs`
- `Scripts/Inventories/Strategies/IStrategy.cs`
- `Scripts/Inventories/IPlacementInventory.cs`
- `Scripts/Inventories/PlacementCandidateOrderer.cs`
- `Scripts/Inventories/AutoTransferService.cs`
