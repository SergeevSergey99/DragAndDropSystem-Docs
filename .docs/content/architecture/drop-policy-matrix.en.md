# Drop Policy Matrix

This page answers a practical question:
"what exactly will the system do for my combination of target slot, policy, and slot state?"

The full architecture is described in [Transfer Pipeline](transfer-pipeline.md).
This page is the condensed behavior matrix.

---

## Short idea

There are three main switches:

- whether a concrete `target slot` exists
- whether that slot is empty or occupied
- which `BlockedTargetBehavior` is selected

Additional modifiers:

- `AllowPartial`
- `BatchMode`
- same-inventory vs cross-inventory transfer

---

## Basic matrix

| Scenario | `Reject` | `Swap` | `FindAlternative` |
|---|---|---|---|
| concrete `target slot`, slot is empty and valid | place into that slot | place into that slot | place into that slot |
| concrete `target slot`, slot is occupied but merge/placement is possible | place into that slot | place into that slot | place into that slot |
| concrete `target slot`, slot is occupied and normal placement is impossible | reject if `occupied handler` does not intercept | try `occupied handler`, then swap | try `occupied handler`, then search other slots |
| concrete `target slot`, slot fails rules | reject | reject or swap if this is an occupied target and both swap directions pass rules | may search for another slot |
| area-drop without a concrete slot | inventory-wide search for valid placement | usually behaves like normal inventory search, not slot-to-slot swap | inventory-wide search for valid placement |

---

## Direct slot drop: important rule

When the operation already has a concrete `target slot`:

- with `Reject` and `Swap`, planner must not scan the rest of the inventory
- inventory-wide search is only valid for `FindAlternative`
- executor for a concrete `targetSlot` must not repeat inventory-wide `GetAcceptableCount()`

This is especially important for fixed-slot inventories such as equipment.
Otherwise you get misleading validation logs for neighboring slots.

---

## Occupied slot: real order

When the target slot is occupied and normal placement into that slot failed:

1. planner first checks `DataBinding.CanHandleOccupiedSlotDrop(...)`
2. if the binding says "I can handle this myself" -> build `RequiresOccupiedHandler`
3. if the binding does not intercept:
   - `Reject` -> fail
   - `Swap` -> build `RequiresSwap`
   - `FindAlternative` -> enumerate other candidate slots

So the `occupied handler` has priority over both swap and alternative placement.

---

## Partial transfer

`AllowPartial` only changes the amount, not the branch selection:

- if everything fits -> normal success
- if only part fits and `AllowPartial = false` -> fail
- if only part fits and `AllowPartial = true`:
  - with `FindAlternative`, the remainder may search other slots
  - with `Reject` and `Swap`, the remainder must not trigger inventory-wide search

---

## Same-inventory vs cross-inventory

### Same-inventory

- `FindAlternative` is not a global reshuffle or sort
- if the target fails, the item stays in place
- swap is only supported for a single entry and a full source stack

### Cross-inventory

- conversion may change the adapter type across inventory boundaries
- swap must not be a raw stack exchange
- both directions must run through conversion independently

---

## Batch drag

For batch drag, keep these separate:

- `BlockedTargetBehavior`
- `BatchMode`

`BatchMode` answers "what happens if one entry fails":

- `Atomic` -> all or nothing
- `BestEffort` -> move what can be moved

Swap is not a general batch orchestration mode.
The current swap implementation is aimed at a single entry and a full source stack.

---

## Practical examples

### Fixed equipment slot

Conditions:

- a concrete slot exists
- that slot is occupied
- `BlockedTargetBehavior = Swap`

Expected behavior:

- planner works only with that slot
- it does not validate neighboring weapon/armor/artifact slots
- if normal placement fails and `occupied handler` does not intercept, planner builds a swap

### Inventory area

Conditions:

- no concrete target slot

Expected behavior:

- inventory-wide search is allowed
- `GetAcceptableCount()` is allowed
- the placement strategy decides merge-first vs empty-first

---

## If behavior looks wrong

- [Transfer Pipeline](transfer-pipeline.md) — overall phase order
- [Logs and Debugging](../reference/logs-and-debugging.md) — how to read planner/executor/rules logs
- [Troubleshooting](../reference/troubleshooting.md) — common symptoms and causes
