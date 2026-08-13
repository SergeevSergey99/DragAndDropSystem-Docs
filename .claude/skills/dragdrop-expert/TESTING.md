# Testing Scenarios

**Last Updated**: 2026-08-13

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
- [ ] the counterpart must be allowed to leave the target slot and to land in the source slot
- [ ] a counterpart refusal moves neither item
- [ ] `Probe` refuses a swap that execution would refuse
- [ ] failed swap restores both inventories and emits no success events
- [ ] `SwapDisplacementMode.SinglePlacement` rejects a shaped drop that would displace multiple placements
- [ ] `AllCoveredPlacements` moves every distinct displaced placement to its source-relative anchor
- [ ] each displaced item is checked against its actual reverse destination slot
- [ ] multi-swap callback lists are complete, ordered, and emitted once after atomic commit
- [ ] multi-swap probe reports the complete forward footprint
- [ ] `VacatedArea` resolution is independent of which covered cell was grabbed
- [ ] mixed-size displacements resolve larger footprints before one-cell items fragment the free area

## Adapter Domain And Conversion

- [ ] drop rules receive the item in the target domain; start rules receive the source domain
- [ ] a typed target binding accepts an item arriving from a different adapter domain
- [ ] a conversion failure is a rule failure with a reason, not a late mutation error
- [ ] the adapter validated by the preview is the exact instance committed to the target
- [ ] the conversion session is shared by derived contexts, including split drops
- [ ] committed entries are consumed and never handed out again during the same drag
- [ ] a converter that changes only some instances of a stack still yields a fully converted stack
- [ ] code-driven transfers work with no session at all
- [ ] regular explicit and automatic transfers commit the target-domain footprint after conversion

## Orientation Projection

- [ ] cross-topology transfers preserve visual angle rather than reusing a topology-local step number
- [ ] forward and reverse swap directions use the same angle-based projection rule

## Stack Slicing

- [ ] preview slices match the instances `ItemStack.Split` takes (tail)
- [ ] after a partial placement, the slice skips instances already in the target
- [ ] an entry spread over several placements commits exactly the validated instances
- [ ] partial auto-transfer carries the tail of the source stack

## Drop Feedback

- [ ] one probe per hover reaches both the footprint highlight and the feedback visual
- [ ] the preview uses the processor's effective policy, including a bound override
- [ ] a slot outside any preview reports no verdict rather than a refusal
- [ ] clearing the preview stops reporting a verdict for its slots

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
- `UDND.Tests.Inventories.TransferConversionTests` - domain boundary, instance identity, slicing
- `UDND.Tests.Inventories.SwapRuleValidationTests` - both swap directions, probe agreement
- `UDND.Tests.Inventories.StackableItemStrategyTests`
- `UDND.Tests.Inventories.SeparableStacksStrategyTests`
- `UDND.Tests.Inventories.ShapedItemPlacementTests`
- `UDND.Tests.Inventories.InventoryDropProcessorTests`
- `UDND.Tests.Inventories.TransferProbeTests`
- `UDND.Tests.Core.DropRequestPolicyTests`

For architecture-wide transfer changes, run the complete
`DragAndDropSystem.Tests.Editor` assembly after the focused fixtures.
