# Data Flow

**Last Updated**: 2026-06-13

## Transfer Flow

```text
DragContext
  -> InventoryDropProcessor resolves policy
  -> InventoryTransferService validates transfer-wide veto
  -> process DragEntry values in order
      -> resolve target-side preview adapter
      -> validate rules
      -> explicit TryGetCandidate OR ordered GetCandidates
      -> validate concrete capacity/topology/domain context
      -> split exact adapter instances from source
      -> convert outgoing/incoming stack
      -> mutate one target placement
      -> enumerate again while remainder exists
      -> rollback current entry or commit outcomes
      -> DataBinding + inventory events
```

No `TransferPlan`, projected occupancy, or batch-wide transaction is created.
Mixed single-cell and shaped entries use the same loop and observe committed mutations from earlier
entries.

## Probe Flow

```text
DragContext
  -> transfer-wide domain veto
  -> first entry with a viable explicit or automatic candidate
  -> TransferProbe(candidate, anchor, orientation, covered slots)
```

The probe is advisory. It does not reserve state, calculate exact batch packing, or replace
execution-time validation.

## Topology Flow

```text
IPlacementInventory.Topology
  -> IInventoryTopology.GetPlacementOffsets(shape, orientation)
  -> PlacementCellUtility maps offsets to indices
  -> PlacementStore validates bounds and occupancy
  -> Placement records anchor and covered indices
```

Shared code does not branch on `GridTopology`. `SlotTopology`, `RectGridTopology`, and custom
topologies participate through `IInventoryTopology`.

## Explicit And Automatic Paths

- Explicit slot: `IStrategy.TryGetCandidate(...)` only.
- Area drop / auto-transfer: `IStrategy.GetCandidates(...)` plus an orderer.
- Blocked explicit target with `AlternativeSlots`: switch to automatic candidates.
- Successful partial candidate: request fresh candidates for the remaining amount.

## Event Flow

Events are deferred until the current entry commits:

```text
Committed outcome
  -> ITransferDomainHandler success hook
  -> IInventoryEventSink.EmitItemRemoved
  -> source DataBinding + external subscribers
  -> IInventoryEventSink.EmitItemAdded
  -> target DataBinding + external subscribers
```

A failed entry restores its source/target snapshots and emits no outcome notifications.

## Swap Flow

Swap resolves both converted stacks, validates forward and reverse domain contexts, removes both
placements, checks both incoming footprints against the vacated state, and places both stacks.
Any failure restores snapshots.
