# Troubleshooting

This page helps diagnose problems by symptom: what you see, what it usually means, and what to check.

## Item Cannot Be Picked Up

Common causes:

- source slot is empty
- `CanStartDrag` returned failure
- data was not loaded into UI
- slot contains an adapter of the wrong type

Check:

- whether `DataBinding` is assigned to the correct `UniversalInventory`
- whether `ReloadUI()` is called
- what `GetItems()` / `GetOccupiedSlots()` returns
- whether a rule blocks drag from this slot or inventory

## Item Cannot Be Dropped Into A Slot

Common causes:

- the slot forbids this item type
- the target stack is already full
- `CanDrop` returned failure
- `DropPolicySettings` is set to `Reject`
- the item was converted to the wrong adapter type

Check:

- rules on the slot and inventory
- `CanDrop` in your binding
- `DropPolicySettings`
- converter, if transfer goes between different inventory types

## Item Is Placed In A Different Slot

This is usually not a bug, but policy behavior.

Check:

- whether `FindAlternative` is enabled
- whether alternative placement inside the same inventory is enabled
- whether the item was dropped on the inventory area instead of a concrete slot
- which `PlacementCandidateOrderer` is selected

If you need strict “only this slot” behavior, use `Reject` for blocked target.

## Data Did Not Update After Transfer

If UI changed but your game data did not, the problem is almost always in the binding.

Check:

- whether the binding is assigned to the correct inventory
- whether `AddToData` / `RemoveFromData` are implemented
- whether those methods modify the exact list or object you expect
- whether you change data directly and forget to call `ReloadUI()`

## `Wrong Item Type` Appears

This usually means an inventory received an adapter that belongs to another data model.

Common causes:

- `CreateItemConverter()` is not configured
- converter returns the wrong adapter
- swap left another inventory's adapter in the slot
- data after transfer is synchronized in one format, while the slot stores another

Check:

- converter on source and target bindings
- `CanDrop` / `CanStartDrag` in fixed-slot or equipment binding
- which adapter is actually stored in the slot after transfer

## First Swap Works, Second Swap Breaks

Almost always, after the first swap one of the slots stores an item with the wrong adapter type.

Check:

- whether conversion exists in both directions
- whether you are exchanging two stacks directly without conversion
- whether data and UI update with the same adapter type

## `CanDrop` Is Called Many Times

This is normal when the system is searching for a suitable place:

- drop was on an inventory area
- `FindAlternative` is enabled
- swap is being checked
- the system iterates slots for auto placement

It is suspicious if you are definitely dropping into a concrete slot and policy does not allow alternative search.

In that case, check:

- whether the pointer hits the slot
- whether `InventoryDropArea` is placed over slots
- which `BlockedTargetResolutionKind` is used

## Stack Behaves Like The Same Item

This happens when several instances in a stack reuse the same adapter object, even though they should be different items.

Check:

- whether a separate adapter is created for each unique instance
- whether converter preserves runtime item state
- whether `ItemId` matches your stacking logic

## Where To Start Debugging

1. Name the symptom: cannot pick up, cannot drop, data did not update, swap breaks.
2. Check Inspector settings: strategy, slot management, drop policy, rules.
3. Check binding: data loading, `CanStartDrag`, `CanDrop`, add/remove methods.
4. If inventories use different data models, check converter.
5. If the issue is only with trading, server validation, or gold, check domain handler.

See also:

- [Logs and Debugging](logs-and-debugging.md)
- [Drop Policy](../architecture/drop-policy-matrix.md)
- [Item Conversion](../architecture/item-conversion-cookbook.md)
- [Transfer Pipeline](../architecture/transfer-pipeline.md)
