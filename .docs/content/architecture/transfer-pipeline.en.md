# Transfer Pipeline

This page describes the current just-in-time transfer pipeline.

See also:

- [Drop Policy Matrix](drop-policy-matrix.md)
- [Cookbook: Item Conversion](item-conversion-cookbook.md)
- [Logs and Debugging](../reference/logs-and-debugging.md)

## Short version

```mermaid
flowchart LR
    A["Drop request"] --> B["Transfer-wide veto"]
    B --> C["Process entries sequentially"]
    C --> D["Validate current target state"]
    D --> E["Mutate one entry"]
    E --> F["Commit events and data sync"]
```

There is no materialized `TransferPlan`, virtual slot state, or batch-wide atomic
rollback. Each entry is evaluated against the real inventory state left by the
previous entry.

## Entry processing order

For every `DragEntry`, `InventoryTransferService`:

1. validates drag/drop rules;
2. resolves the target-side preview adapter without mutating the source;
3. builds an `InventoryAcceptanceRequest`;
4. checks a selected target directly through `IStrategy.TryGetCandidate(...)`;
5. when automatic placement is needed, enumerates `IStrategy.GetCandidates(...)`
   and applies a `PlacementCandidateOrderer`;
6. performs conversion, split, merge, placement, or swap;
7. restores the entry snapshots if the entry fails;
8. dispatches events and DataBinding notifications only after that entry commits.

## Explicit target vs automatic placement

A concrete target slot is always checked first. Candidate ordering is not involved
while that target remains valid.

If the target is blocked:

- `Reject` stops that entry;
- `Swap` attempts a single-entry swap;
- `AlternativeSlots` enumerates automatic candidates and orders them with the
  configured `PlacementCandidateOrderer`.

Area drops and auto-transfer start directly with automatic candidate enumeration.

## Strategy and topology responsibilities

`IStrategy` owns item semantics:

- unique, one-per-ID, or separable stacks;
- exact capacity of a concrete target;
- merge vs create candidates;
- candidate enumeration.

`IPlacementGeometry` and inventory topology own spatial semantics:

- resolving the anchor slot;
- projecting oriented footprints;
- checking bounds and occupancy;
- returning covered slots.

`IPlacementInventory` exposes the active `IInventoryTopology`; shared code never uses a
grid-specific flag to decide placement behavior. Single-cell and shaped items use the same path,
including auto-transfer.

## Batch semantics

Batch transfer is sequential best-effort:

- entries are processed in drag-context order;
- a failed entry is restored without reverting earlier committed entries;
- later entries see all earlier successful mutations;
- partial transfer may move the amount that fits and leave the remainder in the
  source when `PartialTransferMode.Allow` is active;
- batch swap is rejected. Swap currently requires one full entry.

## Extension points

`ITransferDomainHandler.CanStartTransfer(...)` runs before entry processing and may
cancel the whole transfer. It is a general veto point. A project may implement its
own simulation there, but simulation is not required by the pipeline.

Other extension points:

- inventory and global rules for mechanical validation;
- `IOccupiedSlotDropHandler` for domain-specific occupied-target behavior;
- `PlacementCandidateOrderer` for automatic placement preference;
- item converters for target-side representations;
- transfer success hooks and inventory events after commit.

## Failure guarantees

The pipeline does not promise batch-wide atomicity. It does guarantee that a failed
entry restores the source and target snapshots captured for that entry, and that
notifications are not emitted before the entry commits.
