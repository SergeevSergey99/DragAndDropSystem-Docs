# Data Flow

**Last Updated**: 2026-08-12

## Transfer Flow

```text
DragContext (owns TransferConversionSession)
  -> InventoryDropProcessor resolves policy
  -> InventoryTransferService validates synchronous transfer-wide veto
  -> async execution validates optional asynchronous transfer-wide veto
  -> process DragEntry values in order
      -> validate rules
           RuleEvaluationService.ValidateEntryDrop:
             resolve entry into the target domain (via session)
             -> global -> inventory -> DataBinding.CanDrop -> slot rules
      -> resolve target-side preview adapter (session hit)
      -> explicit TryGetCandidate OR ordered GetCandidates
      -> validate concrete capacity/topology/domain context
      -> split exact adapter instances from source (tail of the remainder)
      -> convert stack to the target domain (session hit: same objects the rules saw)
      -> mutate one target placement
      -> enumerate again while remainder exists
      -> rollback current entry or commit outcomes (+ consume session entries)
      -> DataBinding + inventory events
```

Conversion sits **before** the drop rules, not after: the target must judge the item as it will
exist after the drop. Because the session memoizes by source adapter reference, the object the
rules validated is the object the mutation commits.

No `TransferPlan`, projected occupancy, or batch-wide transaction is created.
Mixed single-cell and shaped entries use the same loop and observe committed mutations from earlier
entries.

`IAsyncTransferDomainHandler.CanStartTransferAsync(...)` is optional and runs once before the
first mutation. It may perform a remote check or a user-defined simulation, but the core neither
requires nor supplies simulation. The synchronous execution API rejects a request when such a
handler is attached, so the check cannot be silently skipped.

## Probe Flow

```text
DragContext
  -> synchronous transfer-wide domain veto
  -> first entry with a viable explicit or automatic candidate
  -> TransferProbe(candidate, anchor, orientation, covered slots)
```

The probe is advisory. It does not reserve state, calculate exact batch packing, or replace
execution-time validation. It does not invoke asynchronous domain handlers.

## Drop Feedback Flow

```text
SlotInputAdapter.OnBecomeActiveTarget
  -> DragAndDropManager.CurrentProcessor (already bound to this slot)
  -> InventoryDropProcessor.ProbeDrop(context)      // effective policy, incl. bound override
  -> IInventoryInteraction.ShowDropPreview(slot, context, probe)
       -> DropPreviewController resolves covered slots + one DropVerdict
       -> highlights the covered slots
  -> CrossFeedbackSlot.Highlight reads TryGetActiveDropVerdict(this, out verdict)
```

One probe per hover. Slots render the verdict; they never derive it. A slot that probes on its own
resolves a different policy and can contradict the drop it previews.

## Topology Flow

```text
IPlacementInventory.Topology
  -> normalize topology-defined orientation step
  -> rotate grab offset using topology coordinates
  -> IInventoryTopology.GetPlacementOffsets(shape, orientation)
  -> PlacementCellUtility maps offsets to indices
  -> PlacementStore validates bounds and occupancy
  -> Placement records anchor, covered indices, and projected offsets
```

Shared code does not branch on `GridTopology`. `SlotTopology`, `RectGridTopology`, and custom
topologies participate through `IInventoryTopology`.

No shared layer assumes 90-degree rotation. Rect grids expose four steps and a future axial hex
topology may expose six.

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

`SwapDisplacementMode.SinglePlacement` keeps one-for-one behavior. With `AllCoveredPlacements`, one shaped entry may
displace every distinct placement covered by its destination footprint; this is not batch swap.

```text
forward direction validated by the caller's ValidateEntryDrop
  -> ValidateSwapCounterpart(target item -> source slot)
       ValidateEntryStart  (may it leave the target slot?)
       ValidateEntryDrop   (may it land in the source slot?  converts into the source domain)
  -> convert both stacks through the session
  -> forward + reverse domain handler validation
  -> remove both placements
  -> check both incoming footprints against the vacated state
  -> place both stacks, consume session entries, dispatch events
```

For plural displacement, each counterpart keeps its topology-coordinate offset from the target
anchor when mapped around the source anchor. Every counterpart is checked against that actual
reverse destination, and all incoming footprints are validated together against the vacated state.
The same resolver runs inside `Probe` and execution, so preview reports the complete forward
footprint and agrees with commit. Any failure restores snapshots and moves no item.
