---
name: dragdrop-expert
description: Expert guidance for reviewing and extending UniversalDragAndDrop with its policy-driven JIT transfer architecture.
---
# DragAndDrop Expert Guide

**Last Updated**: 2026-08-06
**Version**: 3.1

## Expert Baseline

1. Keep drag state in `DragContext`; avoid ad-hoc lock flags.
2. Keep behavior policy-driven (`DropRequestPolicy` / `DropPolicySettings` / `ResolvedDropPolicy`).
3. Keep transfer decisions just-in-time; do not introduce a materialized plan or virtual inventory state.
4. Keep explicit-target validation in `IStrategy.TryGetCandidate(...)`.
5. Use `IStrategy.GetCandidates(...)` plus an orderer only for automatic distribution.
6. Keep swap in the main transfer service, not parallel resolver/strategy branches.
7. Preserve per-entry rollback and partial-stack return semantics.
8. Preserve target-aware preview conversion and acceptance requests before mutation.
9. Keep UI layout concerns separate from transfer semantics.
10. Treat `IPlacementInventory.Topology` as authoritative; do not branch shared placement code on
    `GridTopology` or an `is grid` flag.
11. Keep `IAsyncTransferDomainHandler` optional and transfer-wide; invoke it once before any
    mutation through the asynchronous execution path.
12. Keep orientation topology-defined; manager, UI, snapshots, and placement code must not assume
    four 90-degree rotations.
13. Let `RuleEvaluationService.ValidateEntryDrop` own the adapter-domain boundary. Drop rules receive
    the target-domain entry; never call a converter from a rule or a binding check.
14. Resolve adapters through the drag's `TransferConversionSession` so preview and commit share one
    object; keep `IItemAdapterConverter` implementations pure.
15. Slice stacks from the tail, matching `ItemStack.Split`; preview slices the tail of the remainder.
16. Keep drop feedback on one probe: read `DropVerdict`, never probe from a slot.

## Review Priorities

- Correct policy resolution (`override -> inventory/context default`).
- Candidate validity against current inventory state.
- Preview correctness (`InventoryAcceptanceRequest`, target-side conversion).
- Per-entry rollback and best-effort batch continuation.
- Bidirectional rule checks for swap, in `Probe` as well as execution.
- Adapter-domain boundary: start rules source-side, drop rules target-side, one conversion point.
- Instance identity: the adapter the preview validated is the adapter that gets committed.
- Stack slicing follows the tail convention on every partial-transfer path.
- Same-inventory area-drop behavior: source slot is excluded, dynamic inventories create a new target slot during execution.
- No duplicate or premature event emission.
- Async transfer-wide veto is not bypassed by a synchronous execution path.
- Orientation is normalized and projected by the active topology.

## Critical Files

- `Scripts/Core/Drop/DropPolicy.cs`
- `Scripts/Core/DragContext.cs`
- `Scripts/Inventories/InventoryDropProcessor.cs`
- `Scripts/Inventories/InventoryTransferService.cs`
- `Scripts/Inventories/Strategies/IStrategy.cs`
- `Scripts/Inventories/PlacementCandidate.cs`
- `Scripts/Inventories/PlacementCandidateOrderer.cs`
- `Scripts/Inventories/InventoryAcceptanceRequest.cs`
- `Scripts/Inventories/TransferItemConversionUtility.cs`
- `Scripts/Inventories/TransferConversionSession.cs`
- `Scripts/Inventories/DropVerdict.cs`
- `Scripts/Inventories/DropPreviewController.cs`
- `Scripts/Rules/RuleEvaluationService.cs`
- `Scripts/Inventories/IAsyncTransferDomainHandler.cs`
- `Scripts/DragAndDropManager.cs`

## Checklists and Deep Dives

- Anti-patterns: `ANTIPATTERNS.md`
- Best practices: `BEST_PRACTICES.md`
- Testing matrix: `TESTING.md`
- Mandatory compilation and Unity test commands: `../VERIFICATION.md`
