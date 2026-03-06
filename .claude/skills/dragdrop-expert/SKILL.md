---
name: dragdrop-expert
description: Expert guidance for reviewing and extending DragAndDropSystem with policy/planner/executor architecture.
---
# DragAndDrop Expert Guide

**Last Updated**: 2026-02-28
**Version**: 2.0

## Expert Baseline

1. Keep drag state in `DragContext`; avoid ad-hoc lock flags.
2. Keep behavior policy-driven (`DropPolicy`).
3. Keep planning/mutation separated (`TransferPlanner` vs `TransferPlanExecutor`).
4. Keep swap in main execution pipeline, not parallel legacy branches.
5. Preserve rollback safety and deferred events in atomic mode.

## Review Priorities

- Correct policy resolution (`override -> inventory/context default`).
- Planner determinism and no state mutation.
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
- `Scripts/DragAndDropManager.cs`

## Checklists and Deep Dives

- Anti-patterns: `ANTIPATTERNS.md`
- Best practices: `BEST_PRACTICES.md`
- Testing matrix: `TESTING.md`
