---
name: dragdrop-system
description: Quick reference for Unity Drag & Drop Inventory System with policy/planner/executor pipeline, batch transfer, and swap integration.
---
# Unity Drag & Drop Inventory System - Quick Reference

**Version**: 2.5
**Last Updated**: 2026-05-30

## System Overview

Core drag & drop works through a single transfer pipeline:

1. `DropRequestPolicy` / `DropPolicySettings` / `ResolvedDropPolicy` (`Scripts/Core/Drop/DropPolicy.cs`, `Scripts/Core/Drop/DropPolicySettings.cs`) - resolves runtime request + inventory defaults into final behavior.
2. `TransferPlanner` (`Scripts/Inventories/TransferPlanner.cs`) - builds immutable plan.
3. `TransferPlanExecutor` (`Scripts/Inventories/TransferPlanExecutor.cs`) - executes plan with rollback options.
4. `InventoryAcceptanceRequest` (`Scripts/Inventories/InventoryAcceptanceRequest.cs`) - carries context-aware preview data.
5. `TransferItemConversionUtility` (`Scripts/Inventories/TransferItemConversionUtility.cs`) - resolves target-side preview conversion.

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
- `InputEventRouter` / `InputModalityTracker` (`Scripts/Interaction/`)
  - input routing and modality state
- `InventoryDropArea` (`Scripts/UI/InventoryDropArea.cs`)
  - area-drop entry point that builds preview requests
- `IDynamicSlotLifecycle` (`Scripts/Inventories/InventoryRuntimeCapabilities.cs`)
  - capability for runtime slot creation/removal used by transfer execution

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
- same-inventory area drops exclude the source slot as a candidate; dynamic inventories may create a new target slot during execution

## Operation References

- detailed flows: `OPERATIONS.md`
- concepts and constraints: `CORE_CONCEPTS.md`
- advanced capabilities: `ADVANCED_FEATURES.md`
- demo mappings: `EXAMPLES.md`
