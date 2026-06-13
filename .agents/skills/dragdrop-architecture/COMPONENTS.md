# Components

**Last Updated**: 2026-06-14

## InventoryDropProcessor

UI-facing boundary that resolves target inventory, target slot, request overrides, and
`ResolvedDropPolicy`, then delegates to `InventoryTransferService`.
`CanAcceptDrop` stores an advisory `TransferProbe`; execution always revalidates.

## InventoryTransferService

Implemented in `Scripts/Inventories/InventoryTransferEngine.cs`.

Responsibilities:
- sequential best-effort batch processing
- per-entry snapshots and rollback
- explicit-target and automatic candidate paths
- exact stack splitting and conversion
- placement mutation, occupied-target handling, and swap
- deferred entry outcome notifications
- advisory first-candidate probing without reservation or full-batch prediction
- optional asynchronous transfer-wide veto before the first mutation

## IStrategy

Read-only inventory behavior policy:
- validates a concrete selected slot with `TryGetCandidate(...)`
- lazily enumerates automatic destinations with `GetCandidates(...)`
- defines capacity, stacking, drag amount, and acceptance semantics

It does not mutate inventories or create slots.

## PlacementCandidateOrderer

Orders candidates only during automatic distribution. It is not used for the initial explicit
target attempt.

## IPlacementInventory

Topology-neutral placement inventory contract. It exposes:
- `IInventoryTopology Topology`
- `IStrategy Strategy`
- logical placements and placement mutation primitives

It must not expose a nullable grid flag through the common contract.

## IInventoryTopology / PlacementStore

`IInventoryTopology` projects shape and orientation into cells. `PlacementStore` owns occupancy
and bounds checks without knowing concrete topology types.

Built-in topologies:
- `SlotTopology`
- `RectGridTopology`
- `SlotCountLimitedTopology`

Custom topologies implement the same interface.

## InventoryPlacementGeometry

Read-only strategy facade over inventory slots, placements, anchor resolution, topology validation,
and covered-slot lookup.

## InventoryAcceptanceRequest

Carries target inventory, target-side adapter, requested amount, and optional drag context/source
entry for candidate and rule validation.

## TransferProbe

Read-only snapshot of the first currently viable batch entry and candidate. It exposes the entry,
candidate, resolved anchor, orientation, and covered slots. It is UI guidance only: it neither
reserves inventory state nor replaces execution-time validation.

## TransferItemConversionUtility

Resolves non-mutating preview adapters and performs outgoing/incoming stack conversion immediately
before placement mutation.

## Runtime Capabilities

- `IDynamicSlotLifecycle`: creates/removes dynamic slots during execution.
- `IInventorySnapshotProvider`: captures entry rollback checkpoints.
- `ITransferDomainHandler`: transfer-wide veto, concrete candidate validation, and success hook.
- `IAsyncTransferDomainHandler`: optional transfer-wide asynchronous veto before mutation.
- `IOccupiedSlotDropHandler`: domain-owned occupied-target operation.
- `IInventoryEventSink`: commits transfer outcomes to DataBinding and subscribers.

## UI Components

- `DropPreviewController` projects preview footprints through `IPlacementInventory.Topology`.
- `PlacementOverlay` renders recorded covered slots and does not decide placement semantics.
- Layout components react to slot lifecycle and never mutate transfer contents.
