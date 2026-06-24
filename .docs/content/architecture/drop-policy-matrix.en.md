# Drop Policy

Drop policy answers a simple question: **what should happen when the player drops an item somewhere it cannot be placed directly**.

For example:

- the slot is occupied
- the slot is blocked by rules
- the target stack is already full
- the item was dropped on an inventory area instead of a slot

## Main Options

| Setting | What it does |
|---|---|
| `Reject` | Rejects the transfer. The item returns to where it came from. |
| `FindAlternative` | Looks for another suitable slot. |
| `Swap` | Tries to swap the dragged item with the item in the occupied slot. |

## If The Slot Is Occupied

| Policy | Behavior |
|---|---|
| `Reject` | Nothing moves. |
| `FindAlternative` | The system searches for another slot where the item can fit. |
| `Swap` | The system tries to exchange the two items. |

If the selected slot is valid, policy does not interfere: the item is placed into that slot.

## If The Item Was Dropped On An Inventory Area

When the player drops an item on the inventory area instead of a concrete slot, the system searches for a suitable place.

This uses `PlacementCandidateOrderer`:

| Orderer | Behavior |
|---|---|
| `Natural` | Uses the natural slot order. |
| `MergeFirst` | Tries to merge with existing stacks first. |
| `EmptyFirst` | Tries empty slots first. |
| `MergeOnly` | Tries only stack merging. |
| `EmptyOnly` | Tries only empty slots. |

If the player drops directly on a concrete slot, sorting is not used: the selected slot is checked first.

## Partial Transfer

`PartialTransferMode` controls whether only part of a stack may be transferred.

| Value | Behavior |
|---|---|
| `Allow` | Transfers as much as fits. The rest returns back. |
| `RequireFull` | Cancels the transfer if the whole stack cannot fit. |

This applies to one transfer entry. If the player drags several selected items, failure of one item does not cancel items that were already transferred before it.

## Alternative Slot Inside The Same Inventory

`AllowSameInventoryAlternativePlacement` matters when moving an item inside the same inventory.

If enabled, dropping onto an occupied slot can make the system look for another free slot in the same inventory.

If disabled, that drop does not become “put it somewhere else”. The item stays where it was if the selected slot is not valid.

## Swap

`Swap` exchanges two items.

It works only for one dragged item and one concrete occupied slot. If the player drags several items onto one occupied slot, batch swap is rejected before anything changes.

## What To Choose

| Desired behavior | Choose |
|---|---|
| Place only into the selected slot | `Reject` |
| Allow “put it somewhere suitable” | `FindAlternative` |
| Allow exchange with an occupied slot | `Swap` |
| Allow moving part of a stack | `PartialTransferMode.Allow` |
| Forbid partial transfers | `PartialTransferMode.RequireFull` |

See also:

- [Transfer Pipeline](transfer-pipeline.md)
- [Placement Strategies](strategies.md)
