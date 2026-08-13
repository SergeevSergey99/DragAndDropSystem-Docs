---
name: dragdrop-architecture
description: Architecture reference for UniversalDragAndDrop with a policy-driven JIT transfer and topology-aware placement pipeline.
---
# DragDrop Architecture

**Version**: 3.1
**Last Updated**: 2026-08-13

## Architectural Baseline

Transfer architecture is centered on:
- `DropRequestPolicy`, `DropPolicySettings`, `ResolvedDropPolicy` (behavior resolution)
- `InventoryDropProcessor` (drop boundary and the only owner of the effective policy)
- `InventoryTransferService` (sequential JIT validation and mutation)
- `RuleEvaluationService` (adapter-domain boundary: converts, then validates drop rules)
- `IStrategy` (explicit-target checks and lazy placement candidates)
- `PlacementCandidateOrderer` (automatic candidate ordering only)
- `IPlacementGeometry` and inventory topology (footprint projection and occupancy)
- `InventoryAcceptanceRequest` (context-aware preview request)
- `TransferItemConversionUtility` (conversion helper) and `TransferConversionSession`
  (drag-scoped conversion identity)
- `DropVerdict` (single drop decision shared by preview and feedback visuals)
- runtime capabilities such as `IDynamicSlotLifecycle` (dynamic slot creation/removal without UI coupling)

## Core Documents

- [UNIFIED_PLACEMENT_PLANNING_PLAN.md](../../../.extraDocs/UNIFIED_PLACEMENT_PLANNING_PLAN.md) - current refactor contract and edge cases
- [VERIFICATION.md](../VERIFICATION.md) - mandatory compilation and Unity test procedure
- [PERFORMANCE.md](./PERFORMANCE.md) - optimization guidance
- [FUTURE_REFACTORING_ROADMAP.md](./FUTURE_REFACTORING_ROADMAP.md) - planned architectural changes

## Current Design Rules

1. Do not precompute a transfer-wide plan; process entries sequentially against current state.
   Atomic multi-swap is the bounded exception and resolves only its displacement set before mutation.
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
13. Keep `IPlacementInventory` topology-neutral: expose `IInventoryTopology`, never a grid-specific
    flag or nullable `GridTopology` through the shared contract.
14. Treat orientation as a topology-defined discrete step. Shared code must not assume four
    directions or multiply orientation values by 90 degrees.
15. Keep the adapter-domain boundary in one place: start rules see the source domain, drop rules see
    the target domain, and `RuleEvaluationService.ValidateEntryDrop` is the only converter caller in
    the rule path. A failed conversion is a rule failure with a reason, not a late mutation error.
16. Keep conversion identity drag-scoped. Resolve adapters through `TransferConversionSession` so
    probe, preview, and mutation share one object; require converters to be pure factories; consume
    cache entries on commit.
17. Keep the tail-slice convention. `ItemStack.Split`/`CreateCopy` take the last N adapters, so every
    predictor of a transfer (drag start, auto-transfer, preview slices) must slice from the tail —
    preview slices from the tail of the *remainder* (`[DesiredCount - count, DesiredCount)`).
18. Keep one probe per hover. Only `InventoryDropProcessor` knows the effective policy; drop feedback
    reads the resulting `DropVerdict` and never probes on its own.
19. Validate both directions of a swap. The counterpart must be allowed to leave its slot and to land
    in the source slot, in `Probe` as well as in execution.
