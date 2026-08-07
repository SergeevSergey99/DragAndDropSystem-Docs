# Components

**Last Updated**: 2026-08-06

## InventoryDropProcessor

UI-facing boundary that resolves target inventory, target slot, request overrides, and
`ResolvedDropPolicy`, then delegates to `InventoryTransferService`.

`ProbeDrop(context, requested)` runs the acceptance probe with the effective policy and stores it as
`LastProbe`; `CanAcceptDrop` is a thin wrapper over it. Because the bound override
(`DropRequestPolicy.Merge(_boundRequestOverride, requested)`) lives here, this is the only correct
source of a probe for drop feedback. `TargetBaseSlot` exposes the slot the processor drops into, so
callers can confirm a probe belongs to the slot they are previewing.

Execution always revalidates; the probe is advisory.

## RuleEvaluationService

`Scripts/Rules/RuleEvaluationService.cs`

The adapter-domain boundary of the pipeline.

- `ValidateEntryStart(...)` validates in the **source** domain.
- `ValidateEntryDrop(...)` resolves the entry into the **target** domain through the drag's
  `TransferConversionSession`, then runs global rules, the target inventory's `RuleValidator`, the
  target DataBinding's `CanDrop`, and the target slot's rules.

Details that matter when editing it:
- every adapter of the stack is resolved, not just the primary one — a converter may pass some items
  through unchanged and rebuild others
- when nothing changed, the original entry is reused, so same-domain drags stay allocation-free
- only the validated entry is replaced in the context copy; the rest of a batch keeps its position
  and its source domain until its own turn
- a conversion failure returns `RuleResult.Failure`, so the preview can render it

## TransferConversionSession

`Scripts/Inventories/TransferConversionSession.cs`

Drag-scoped memo of converted adapters, owned by `DragContext` and shared by every derived context
(`WithTarget`, `WithEntries`, `CreateDerived`).

- key: `(source inventory, target inventory, source adapter reference)`, reference identity on all
  three components
- `TryResolve(...)` converts on a miss; failures are not cached
- `Consume(...)` drops an entry once its transfer commits
- absence of a session is a supported mode, not a degraded one

Requires `IItemAdapterConverter` implementations to be pure factories for the duration of a drag.

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

Orientation is stored as an integer step index, not an angle. A topology owns:
- orientation count and normalization
- rotating an orientation by N steps
- visual angle for a step
- grab-offset transformation
- oriented footprint projection

`RectGridTopology` uses four 90-degree steps. A hex topology can use six 60-degree steps without
changing placement, strategy, or transfer contracts. `Placement` records the projected offsets
that were actually committed so snapshots do not have to reconstruct topology-specific rotation.

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

Resolves non-mutating preview adapters and performs source-to-target stack conversion immediately
before placement mutation. All conversion goes through the drag's `TransferConversionSession` when
one is available.

Key members:
- `TryResolveTargetItem(source, target, adapter, session, out converted)` - one adapter across the
  boundary
- `TryCreatePreviewStack(request, count, ...)` - target-domain view of the items the next split will
  take; slices `[DesiredCount - count, DesiredCount)`
- `TryCreateSourceStack(request, count, out stack)` - the same slice without conversion, used to
  build drop-rule input
- `TryConvertStackToTargetDomain(source, target, stack, session)` - in-place conversion at mutation
  time, reusing the previewed objects
- `ConsumeCommitted(...)` - releases cache entries owned by the target after commit

The synthetic fallback path (requests with no concrete source instances) deliberately bypasses the
session: it must produce N distinct instances, and a reference-keyed cache would return one object N
times.

## DropVerdict

`Scripts/Inventories/DropVerdict.cs`

Whether the active drop preview would land on a given slot, plus a `FailureReason` when it would
not. Produced once per hover by `DropPreviewController` from the processor's probe, exposed through
`IInventoryInteraction.TryGetActiveDropVerdict(slot, out verdict)`.

`TryGetActiveDropVerdict` returning false means "no opinion", which is not a refusal.

## Runtime Capabilities

- `IDynamicSlotLifecycle`: creates/removes dynamic slots during execution.
- `IInventorySnapshotProvider`: captures entry rollback checkpoints.
- `ITransferDomainHandler`: transfer-wide veto, concrete candidate validation, and success hook.
- `IAsyncTransferDomainHandler`: optional transfer-wide asynchronous veto before mutation.
- `IOccupiedSlotDropHandler` + timing variants `IPreRuleOccupiedSlotDropHandler` / `IPostRuleOccupiedSlotDropHandler`: DataBinding-owned occupied-target operation.
- `IInventoryEventSink`: commits transfer outcomes to DataBinding and subscribers.

## UI Components

- `DropPreviewController` projects preview footprints through `IPlacementInventory.Topology`, and
  owns the `DropVerdict` of the preview currently on screen. It accepts a caller-supplied
  `TransferProbe`; it only resolves its own (with default policy) when no processor is involved.
- `PlacementOverlay` renders recorded covered slots and does not decide placement semantics.
- `CrossFeedbackSlot` renders a refusal read from `TryGetActiveDropVerdict` inside `Highlight(bool)`;
  it holds no state of its own and never probes.
- Layout components react to slot lifecycle and never mutate transfer contents.
