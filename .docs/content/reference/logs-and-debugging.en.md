# Logs And Debugging

This page helps you find where to look when a transfer does not work.

Start from what you see in the game, not from an internal phase name:

- the item cannot be picked up
- the item cannot be dropped
- the item returns back
- data did not change after transfer
- swap behaves incorrectly
- Console shows many warnings

## Quick Map

| What you see | Start with |
|---|---|
| Item cannot be picked up | `CanStartDrag`, rules on the source slot, data loading into UI |
| Item cannot be dropped | `CanDrop`, rules on the target slot, `DropPolicySettings` |
| Item goes to another slot | `FindAlternative`, `PlacementCandidateOrderer`, drop on inventory area |
| UI changed, data did not | `AddToData`, `RemoveFromData`, correct binding |
| Wrong item type error | `CreateItemConverter()`, adapter type in source and target |
| Swap breaks after the first time | conversion in both directions and adapter type in slots after swap |

## What Logs Usually Mean

| Log or class | Usually means |
|---|---|
| `RuleResult` | A rule blocked drag or drop. |
| `CanStartDrag` | Binding blocked drag start. |
| `CanDrop` | Binding blocked drop into target inventory or slot. |
| `InventoryDropProcessor` | Drop was rejected before the actual transfer. Often policy or target slot is the reason. |
| `InventoryTransferService` | The problem happened during transfer: placement, swap, conversion, or rollback. |
| `GetAcceptableCount` | The system is iterating slots and searching for a place to put the item. |
| `CanCommitTransfer` | Business logic blocked the transfer right before commit, for example not enough gold. |

## If Warnings Mention Other Slots

This is often normal. The system may check more than the selected slot when:

- the item was dropped on an inventory area
- `FindAlternative` is enabled
- a large stack needs space
- swap is being checked

If you expected only one slot to be checked, verify:

- the pointer really hits the slot, not `InventoryDropArea`
- policy does not allow `FindAlternative`
- the selected slot is not covered by another UI element

## If The Log Mentions A Rule

A rule log does not always mean a bug. Sometimes the system checks several options and
one of them is correctly rejected.

Look at the context:

- which slot was checked
- which item was checked
- whether it was the selected slot or an alternative one
- whether the transfer still succeeded afterward

## If The Problem Is Data

When UI and game data differ after a transfer, check the binding:

- does `GetItems()` / `GetOccupiedSlots()` load the correct data?
- does `AddToData(...)` add to the correct list?
- does `RemoveFromData(...)` remove from the correct list?
- do external data changes call `ReloadUI()`?

## If The Problem Is Between Different Inventories

For example: merchant, player, equipment, or container use different data models.

Check:

- whether the right binding has `CreateItemConverter()`
- which adapter is in the source slot
- which adapter should be in the target slot
- whether the converter preserves unique item data

## If The Problem Is Trading Or External Validation

Check the domain handler:

- `CanStartTransfer` — whether transfer may start at all
- `CanStartTransferAsync` — whether a server or another external check rejected transfer
- `CanCommitTransfer` — whether a specific placement may be committed
- `OnTransferSucceeded` — whether a side effect breaks data after successful transfer

## Useful Check Order

1. Make sure the item is actually loaded into UI.
2. Check rules and `CanStartDrag`.
3. Check target slot, `CanDrop`, and drop policy.
4. If transfer goes between different models, check converter.
5. If there is money, server, access rights, or ownership logic, check domain handler.
6. If UI and data diverged, check binding add/remove methods.

See also:

- [Troubleshooting](troubleshooting.md)
- [Drop Policy](../architecture/drop-policy-matrix.md)
- [Item Conversion](../architecture/item-conversion-cookbook.md)
