---
name: dragdrop-system
description: Quick reference for Unity Drag & Drop Inventory System with policy/planner/executor pipeline, batch transfer, and swap integration.
---
# Unity Drag & Drop Inventory System - Quick Reference

**Version**: 2.0
**Last Updated**: 2026-02-28

## System Overview

Core drag & drop now works through a single transfer pipeline:

1. `DropPolicy` (`Scripts/Core/DropPolicy.cs`) - defines behavior.
2. `TransferPlanner` (`Scripts/Inventories/TransferPlanner.cs`) - builds immutable plan.
3. `TransferPlanExecutor` (`Scripts/Inventories/TransferPlanExecutor.cs`) - executes plan with rollback options.

Main benefits:
- Unified behavior for single and batch drag.
- Atomic mode with snapshot rollback.
- Swap integrated into the same execution pipeline.

## Main Components

- `DragAndDropManager` (`Scripts/DragAndDropManager.cs`)
  - Coordinates drag lifecycle and drop targets.
  - Exposes swap events: `OnSwapAttempting`, `OnSwapCompleted`.
- `InventoryDropProcessor` (`Scripts/Inventories/InventoryDropProcessor.cs`)
  - Entry point for plan+execute flow for inventory drops.
- `TransferPlanner` (`Scripts/Inventories/TransferPlanner.cs`)
  - Produces `TransferPlan` and per-entry actions.
- `TransferPlanExecutor` (`Scripts/Inventories/TransferPlanExecutor.cs`)
  - Applies transfers/swaps in `Atomic` or `BestEffort` mode.
- `InventoryTransferService` (`Scripts/Inventories/InventoryTransferService.cs`)
  - Transactional slot/inventory transfer primitive.

## Policy Model

`DropPolicy` controls four dimensions:
- `OccupiedTargetPolicy`: `Reject`, `TrySwap`, `TryAlternativeSlots`
- `CapacityPolicy`: `RejectAll`, `Partial`
- `BatchExecutionPolicy`: `Atomic`, `BestEffort`
- `TargetUsagePolicy`: `StrictTarget`, `TargetAsHint`

Presets:
- `DropPolicy.SingleDefault`
- `DropPolicy.BatchAtomic`
- `DropPolicy.BatchBestEffort`

## Swap Model (Current)

- Swap is planned when allocation is impossible and policy allows `TrySwap`.
- Executor validates both directions via rules before swap.
- Swap can be canceled by listeners through `InventorySwapContext.Cancel`.
- Swap events are dispatched only after successful execution completion.

Current scope:
- Single-entry full-stack swap is supported.
- Batch swap orchestration is not enabled as separate mode yet.

## Operation References

- Detailed flows: `OPERATIONS.md`
- Concepts and constraints: `CORE_CONCEPTS.md`
- Advanced capabilities: `ADVANCED_FEATURES.md`
- Demo mappings: `EXAMPLES.md`
