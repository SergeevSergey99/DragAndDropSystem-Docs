---
name: dragdrop-architecture
description: Architecture reference for DragAndDropSystem with policy-driven planner/executor transfer pipeline.
---
# DragDrop Architecture

**Version**: 2.2
**Last Updated**: 2026-03-22

## Architectural Baseline

Transfer architecture is centered on:
- `DropPolicy` (behavior contract)
- `TransferPlanner` (pure planning)
- `TransferPlanExecutor` (state mutation + rollback)
- `InventoryAcceptanceRequest` (context-aware preview request)

## Core Documents

- [COMPONENTS.md](./COMPONENTS.md) - responsibilities and boundaries
- [DATA_FLOW.md](./DATA_FLOW.md) - operation pipelines
- [STRATEGIES.md](./STRATEGIES.md) - inventory behavior strategies
- [PERFORMANCE.md](./PERFORMANCE.md) - optimization guidance
- [FUTURE_REFACTORING_ROADMAP.md](./FUTURE_REFACTORING_ROADMAP.md) - planned architectural changes

## Current Design Rules

1. Keep planning and mutation separated.
2. Keep drop behavior policy-driven.
3. Keep swap execution in the same pipeline as regular transfers.
4. Keep event emission rollback-safe in atomic mode.
5. Keep UI target code thin (`InventoryDropProcessor` as boundary).
6. Keep DataBinding notification direct for add/remove; events only for external subscribers and swap.
7. Keep `TryAddToSlot` as pure mutation with no internal event emission.
8. Keep preview conversion target-aware and non-mutating before planning/execution.
