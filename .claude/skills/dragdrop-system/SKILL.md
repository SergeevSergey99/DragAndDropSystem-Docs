---
name: dragdrop-system
description: Quick reference for Unity Drag & Drop Inventory System with policy/planner/executor pipeline, batch transfer, and swap integration.
---
# Unity Drag & Drop Inventory System - Quick Reference

**Version**: 2.3
**Last Updated**: 2026-03-26

## System Overview

Core drag & drop works through a single transfer pipeline:

1. `DropPolicy` (`Scripts/Core/DropPolicy.cs`) - resolves runtime request + inventory defaults into final behavior.
2. `TransferPlanner` (`Scripts/Inventories/TransferPlanner.cs`) - builds immutable plan.
3. `TransferPlanExecutor` (`Scripts/Inventories/TransferPlanExecutor.cs`) - executes plan with rollback options.
4. `InventoryAcceptanceRequest` (`Scripts/Inventories/InventoryAcceptanceRequest.cs`) - carries context-aware preview data.

Main benefits:
- unified behavior for single and batch drag
- atomic mode with snapshot rollback
- swap integrated into the same execution pipeline
- slot-specific preview works for area-drops and mapped inventories

## Main Components

- `DragAndDropManager` (`Scripts/DragAndDropManager.cs`)
  - coordinates drag lifecycle and drop targets
  - exposes swap events
- `InventoryDropProcessor` (`Scripts/Inventories/InventoryDropProcessor.cs`)
  - entry point for plan+execute flow for inventory drops
- `TransferPlanner` (`Scripts/Inventories/TransferPlanner.cs`)
  - produces `TransferPlan` and per-entry actions
- `TransferPlanExecutor` (`Scripts/Inventories/TransferPlanExecutor.cs`)
  - applies transfers/swaps in `Atomic` or `BestEffort` mode
- `InventoryTransferService` (`Scripts/Inventories/InventoryTransferService.cs`)
  - transfer request/result models used by executor
- `TransferItemConversionUtility` (`Scripts/Inventories/TransferItemConversionUtility.cs`)
  - resolves target preview item before planning/execution

## Policy Model

`DropPolicy` has three layers:
- `DropRequestPolicy` - temporary nullable overrides for a single operation
- `DropPolicySettings` - inventory-level defaults in `UniversalInventory`
- `ResolvedDropPolicy` - final planner-facing policy

Main fields:
- `BlockedTargetBehavior`: `Reject`, `Swap`, `FindAlternative`
- `AllowPartial`
- `BatchMode`: `Atomic`, `BestEffort`
- `AlternativePlacementMode`: `MergeFirst`, `EmptyFirst`, `MergeOnly`, `EmptyOnly`

## Preview Model

- area-drops and planner preview resolve target-side item before capacity checks
- `InventoryAcceptanceRequest` lets strategies validate concrete candidate slots
- mapped-slot bindings no longer need ad-hoc preview guards in feature code

## Operation References

- detailed flows: `OPERATIONS.md`
- concepts and constraints: `CORE_CONCEPTS.md`
- advanced capabilities: `ADVANCED_FEATURES.md`
- demo mappings: `EXAMPLES.md`
