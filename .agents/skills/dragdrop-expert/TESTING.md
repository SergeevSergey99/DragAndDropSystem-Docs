# Testing Scenarios

**Last Updated**: 2026-06-13

Follow the mandatory commands and reporting rules in
[Compilation And Test Verification](../VERIFICATION.md).

## Core Transfer

- [ ] explicit target is checked through `IStrategy.TryGetCandidate(...)`
- [ ] explicit target does not enumerate or order automatic candidates
- [ ] area drop and auto-transfer use `GetCandidates(...)` plus an orderer
- [ ] a successful partial candidate triggers fresh candidate enumeration
- [ ] only the amount that cannot fit remains in the source
- [ ] `PartialTransferMode.RequireFull` restores the current entry
- [ ] a failed entry does not revert earlier committed batch entries
- [ ] later batch entries see earlier committed state and DataBinding updates

## Topology

- [ ] `IPlacementInventory.Topology` is the shared topology source
- [ ] `SlotTopology` collapses every shape to one anchor slot
- [ ] spatial and custom topologies project their own oriented footprints
- [ ] shared placement code does not branch on `GridTopology` or `is grid`
- [ ] preview, execution, snapshots, and DataBinding agree on covered cells
- [ ] shaped auto-transfer uses the same candidate loop as single-cell auto-transfer

## Blocked Targets And Swap

- [ ] `Reject` fails the current entry without fallback
- [ ] `AlternativeSlots` uses the configured orderer only after the explicit target fails
- [ ] same-inventory alternatives respect their policy flag
- [ ] swap requires one full entry
- [ ] batch swap is rejected before mutation
- [ ] forward and reverse conversion/rules/domain checks run
- [ ] failed swap restores both inventories and emits no success events

## Stack Semantics

- [ ] `UniqueItemStrategy` distributes count across distinct placements with capacity one
- [ ] stackable one-per-ID behavior does not create duplicate logical locations
- [ ] separable stacks can create multiple logical locations
- [ ] explicit merge and automatic merge policies remain distinct
- [ ] shaped stacks can split across placements
- [ ] adapter identity and source order are preserved in transferred and remainder stacks

## Events And DataBinding

- [ ] notifications are emitted only after the current entry commits
- [ ] partial transfer events contain only transferred adapters
- [ ] rollback emits no false add/remove notifications
- [ ] source and target conversion payloads are correct
- [ ] occupied-target handlers remain snapshot protected
- [ ] placement reload uses the active topology without grid-specific branching

## Suggested Fixtures

- `UDND.Tests.Inventories.InventoryTransferServiceTests`
- `UDND.Tests.Inventories.StackableItemStrategyTests`
- `UDND.Tests.Inventories.SeparableStacksStrategyTests`
- `UDND.Tests.Inventories.ShapedItemPlacementTests`
- `UDND.Tests.Inventories.InventoryDropProcessorTests`
- `UDND.Tests.Core.DropRequestPolicyTests`

For architecture-wide transfer changes, run the complete
`DragAndDropSystem.Tests.Editor` assembly after the focused fixtures.
