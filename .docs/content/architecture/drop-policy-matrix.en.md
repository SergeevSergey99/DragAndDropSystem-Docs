# Drop Policy Matrix

The full flow is described in [Transfer Pipeline](transfer-pipeline.md).

## Policy fields

- `BlockedTargetResolutionKind`: `Reject`, `FindAlternative`, or `Swap`
- `AlternativeOrderer`: ordering used only for automatic alternative placement
- `AllowSameInventoryAlternativePlacement`: whether a blocked same-inventory drop
  may use another placement
- `PartialTransferMode`: allow a partial entry or require the whole amount

## Behavior matrix

| Scenario | `Reject` | `FindAlternative` | `Swap` |
|---|---|---|---|
| explicit target is valid | use it | use it | use it |
| explicit target is blocked | reject entry | search ordered candidates | try single-entry swap |
| area drop, no target slot | automatic candidates | automatic candidates | automatic candidates |
| occupied handler accepts target | handler executes | handler executes | handler executes |
| batch with multiple entries | sequential best-effort | sequential best-effort | rejected before mutation |

## Candidate ordering

The configured orderer is used only when placement is automatic:

- `NaturalPlacementCandidateOrderer`
- `MergeFirstPlacementCandidateOrderer`
- `EmptyFirstPlacementCandidateOrderer`
- `MergeOnlyPlacementCandidateOrderer`
- `EmptyOnlyPlacementCandidateOrderer`

An explicit slot is validated directly and is never reordered.

## Partial transfer

`Allow` moves the amount that currently fits and leaves the remainder in the
source. `RequireFull` restores the entry when the full requested amount cannot be
placed.

This is per-entry behavior. There is no `Atomic` batch mode.

## Same-inventory alternatives

When `AllowSameInventoryAlternativePlacement` is disabled, a blocked explicit drop
inside the same inventory does not fall back to another slot. Area drops and valid
explicit targets are unaffected.

## Swap constraints

Swap currently requires:

- exactly one drag entry;
- the full dragged entry, not a partial split;
- bidirectional rule and placement validity;
- successful restoration if either direction fails.

Large shaped batch swaps are intentionally not supported by this policy.
