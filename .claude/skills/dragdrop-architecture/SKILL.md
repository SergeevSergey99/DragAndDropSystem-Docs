---
name: dragdrop-architecture
description: Architecture reference for DragAndDropSystem with policy-driven planner/executor transfer pipeline.
---
# DragDrop Architecture

**Version**: 2.0
**Last Updated**: 2026-02-28

## Architectural Baseline

Transfer architecture is centered on:
- `DropPolicy` (behavior contract)
- `TransferPlanner` (pure planning)
- `TransferPlanExecutor` (state mutation + rollback)

This replaces fragmented decision logic in manager-level transfer handling.

## Core Documents

- [COMPONENTS.md](./COMPONENTS.md) - responsibilities and boundaries
- [DATA_FLOW.md](./DATA_FLOW.md) - operation pipelines
- [STRATEGIES.md](./STRATEGIES.md) - inventory behavior strategies
- [PERFORMANCE.md](./PERFORMANCE.md) - optimization guidance

## Current Design Rules

1. Keep planning and mutation separated.
2. Keep drop behavior fully policy-driven.
3. Keep swap execution in the same pipeline as regular transfers.
4. Keep event emission rollback-safe in atomic mode.
5. Keep UI target code thin (`InventoryDropHandler` as boundary).
