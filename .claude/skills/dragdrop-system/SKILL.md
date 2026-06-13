---
name: dragdrop-system
description: Quick reference for the UniversalDragAndDrop JIT transfer pipeline, placement candidates, batch transfer, and swap integration.
---
# Unity Drag & Drop Inventory System - Quick Reference

**Version**: 3.0
**Last Updated**: 2026-06-14

## System Overview

Core drag & drop uses one just-in-time transfer pipeline:

1. `DropRequestPolicy` / `DropPolicySettings` / `ResolvedDropPolicy` (`Scripts/Core/Drop/DropPolicy.cs`, `Scripts/Core/Drop/DropPolicySettings.cs`) - resolves runtime request + inventory defaults into final behavior.
2. `InventoryDropProcessor` (`Scripts/Inventories/InventoryDropProcessor.cs`) - resolves policy and starts the transfer.
3. `InventoryTransferService` (`Scripts/Inventories/InventoryTransferService.cs`) - processes entries sequentially against current inventory state.
4. `IStrategy` (`Scripts/Inventories/Strategies/IStrategy.cs`) - validates explicit targets and enumerates placement candidates.
5. `PlacementCandidateOrderer` (`Scripts/Inventories/PlacementCandidateOrderer.cs`) - orders candidates only for automatic distribution.
6. `InventoryAcceptanceRequest` and `TransferItemConversionUtility` - provide target-aware, non-mutating preview data.
7. `IAsyncTransferDomainHandler` - optional transfer-wide asynchronous veto before any mutation.

Main benefits:
- unified behavior for single and batch drag
- no precomputed transfer plan or virtual inventory state
- batch is sequential best-effort and each entry sees mutations from previous entries
- explicit target validation does not enumerate or order all candidates
- topology-owned footprints work for single-cell and shaped items
- topology-owned orientation steps support four-direction rect grids and future six-direction hex grids
- swap and alternative placement remain in the same transfer service
- asynchronous domain checks cannot be bypassed by synchronous execution

## Main Components

- `DragAndDropManager` (`Scripts/DragAndDropManager.cs`)
  - coordinates drag lifecycle and drop targets
  - exposes swap events
- `InventoryDropProcessor` (`Scripts/Inventories/InventoryDropProcessor.cs`)
  - inventory-drop entry point and policy boundary
  - exposes the last advisory `TransferProbe` used by `CanAcceptDrop`
- `InventoryTransferService` (`Scripts/Inventories/InventoryTransferService.cs`)
  - performs conversion, validation, placement, swap, rollback of a failed entry, and result aggregation
- `IStrategy`
  - `TryGetCandidate(...)` validates a selected slot or placement area
  - `GetCandidates(...)` lazily enumerates automatic destinations
- `PlacementCandidateOrderer`
  - orders automatic candidates; it is not used for an explicit target
- `TransferItemConversionUtility` (`Scripts/Inventories/TransferItemConversionUtility.cs`)
  - resolves target preview item before mutation
- `InputEventRouter` / `InputModalityTracker` (`Scripts/Interaction/`)
  - input routing and modality state
- `InventoryDropArea` (`Scripts/UI/InventoryDropArea.cs`)
  - area-drop entry point that builds preview requests
- `IDynamicSlotLifecycle` (`Scripts/Inventories/InventoryRuntimeCapabilities.cs`)
  - capability for runtime slot creation/removal used by transfer execution
- `IAsyncTransferDomainHandler`
  - optional server-backed or user-defined transfer-wide check invoked once before mutation
- `IInventoryTopology`
  - owns orientation count, rotation, visual angle, grab-offset transform, and footprint projection

## Policy Model

`DropPolicy` has three layers:
- `DropRequestPolicy` - temporary nullable overrides for a single operation
- `DropPolicySettings` - inventory-level defaults in `UniversalInventory`
- `ResolvedDropPolicy` - final transfer-facing policy

Main fields:
- `BlockedTargetResolutionKind`: `Reject`, `AlternativeSlots`, `Swap`
- `AlternativeCandidateOrderer`
- `AllowSameInventoryAlternative`
- `PartialTransferMode`

## Preview Model

- `TransferProbe` describes the first currently viable entry, candidate, anchor, orientation, and
  covered slots; it does not reserve state or predict the whole batch
- area-drops and transfer preview resolve target-side item before capacity checks
- `InventoryAcceptanceRequest` lets strategies validate concrete candidate slots
- mapped-slot bindings no longer need ad-hoc preview guards in feature code
- same-inventory moves exclude the source placement while checking destinations
- dynamic inventories expose `NewDynamicSlot` as a placement candidate
- mixed single-cell/shaped batches are allowed and evaluated entry-by-entry against current state

## Operation References

- detailed flows: `OPERATIONS.md`
- concepts and constraints: `CORE_CONCEPTS.md`
- advanced capabilities: `ADVANCED_FEATURES.md`
- demo mappings: `EXAMPLES.md`
- mandatory compilation and Unity test procedure: `../VERIFICATION.md`
