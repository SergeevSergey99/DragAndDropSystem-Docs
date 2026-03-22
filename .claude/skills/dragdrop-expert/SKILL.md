---
name: dragdrop-expert
description: Expert guidance for reviewing and extending DragAndDropSystem with policy/planner/executor architecture.
---
# DragAndDrop Expert Guide

**Last Updated**: 2026-03-22
**Version**: 2.2

## Expert Baseline

1. Keep drag state in `DragContext`; avoid ad-hoc lock flags.
2. Keep behavior policy-driven (`DropPolicy`).
3. Keep planning/mutation separated (`TransferPlanner` vs `TransferPlanExecutor`).
4. Keep swap in main execution pipeline, not parallel legacy branches.
5. Preserve rollback safety and deferred events in atomic mode.
6. Preserve target-aware preview conversion and acceptance requests before execution.

## Review Priorities

- Correct policy resolution (`override -> inventory/context default`).
- Planner determinism and no state mutation.
- Preview correctness (`InventoryAcceptanceRequest`, target-side conversion).
- Executor atomic rollback correctness.
- Bidirectional rule checks for swap.
- No duplicate or premature event emission.

## Critical Files

- `Scripts/Core/DropPolicy.cs`
- `Scripts/Core/DragContext.cs`
- `Scripts/Inventories/InventoryDropProcessor.cs`
- `Scripts/Inventories/TransferPlanner.cs`
- `Scripts/Inventories/TransferPlanExecutor.cs`
- `Scripts/Inventories/InventoryTransferService.cs`
- `Scripts/Inventories/InventoryAcceptanceRequest.cs`
- `Scripts/Inventories/TransferItemConversionUtility.cs`
- `Scripts/DragAndDropManager.cs`

## Checklists and Deep Dives

- Anti-patterns: `ANTIPATTERNS.md`
- Best practices: `BEST_PRACTICES.md`
- Testing matrix: `TESTING.md`
