---
name: dragdrop-architecture
description: Architecture reference for UniversalDragAndDrop with a policy-driven JIT transfer and topology-aware placement pipeline.
---
# DragDrop Architecture

**Version**: 3.0
**Last Updated**: 2026-06-13

## Architectural Baseline

Transfer architecture is centered on:
- `DropRequestPolicy`, `DropPolicySettings`, `ResolvedDropPolicy` (behavior resolution)
- `InventoryDropProcessor` (drop boundary)
- `InventoryTransferService` (sequential JIT validation and mutation)
- `IStrategy` (explicit-target checks and lazy placement candidates)
- `PlacementCandidateOrderer` (automatic candidate ordering only)
- `IPlacementGeometry` and inventory topology (footprint projection and occupancy)
- `InventoryAcceptanceRequest` (context-aware preview request)
- `TransferItemConversionUtility` (preview conversion helper)
- runtime capabilities such as `IDynamicSlotLifecycle` (dynamic slot creation/removal without UI coupling)

## Core Documents

- [UNIFIED_PLACEMENT_PLANNING_PLAN.md](../../../.extraDocs/UNIFIED_PLACEMENT_PLANNING_PLAN.md) - current refactor contract and edge cases
- [PERFORMANCE.md](./PERFORMANCE.md) - optimization guidance
- [FUTURE_REFACTORING_ROADMAP.md](./FUTURE_REFACTORING_ROADMAP.md) - planned architectural changes

## Current Design Rules

1. Do not precompute a transfer plan; process entries sequentially against current state.
2. Keep drop behavior policy-driven.
3. Keep swap execution in the same pipeline as regular transfers.
4. Keep rollback scoped to the current failed entry; batch is best-effort.
5. Keep UI target code thin (`InventoryDropProcessor` as boundary).
6. Keep DataBinding notification direct for add/remove; events only for external subscribers and swap.
7. Keep `TryAddToSlot` as pure mutation with no internal event emission.
8. Keep preview conversion target-aware and non-mutating before execution.
9. Keep slot-domain code on `BaseSlot`; `ISlot` is only for filter/sorter contracts.
10. Keep layout components out of transfer semantics; layouts may react to slot lifecycle events but must not own item mutation.
11. Keep footprint projection topology-owned: `SlotTopology` maps every item to one anchor slot,
    spatial topologies use oriented shape offsets, and `PlacementStore` only validates bounds/occupancy.
12. Validate an explicit target directly; enumerate and order candidates only for automatic placement.
