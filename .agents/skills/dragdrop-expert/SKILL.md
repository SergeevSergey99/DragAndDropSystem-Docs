---
name: dragdrop-expert
description: Expert guidance for reviewing and extending UniversalDragAndDrop with its policy-driven JIT transfer architecture.
---
# DragAndDrop Expert Guide

**Last Updated**: 2026-06-13
**Version**: 3.0

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

## Review Priorities

- Correct policy resolution (`override -> inventory/context default`).
- Candidate validity against current inventory state.
- Preview correctness (`InventoryAcceptanceRequest`, target-side conversion).
- Per-entry rollback and best-effort batch continuation.
- Bidirectional rule checks for swap.
- Same-inventory area-drop behavior: source slot is excluded, dynamic inventories create a new target slot during execution.
- No duplicate or premature event emission.

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
- `Scripts/DragAndDropManager.cs`

## Checklists and Deep Dives

- Anti-patterns: `ANTIPATTERNS.md`
- Best practices: `BEST_PRACTICES.md`
- Testing matrix: `TESTING.md`
