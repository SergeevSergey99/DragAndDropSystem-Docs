# Transfer Pipeline

This page explains transfer from the asset user's perspective.

The question is not "which helper classes exist?" but "in what order does the system decide, and where can I hook into it?"

---

## Short version

```mermaid
flowchart LR
    A["Player drops an item"] --> B["Rule checks"]
    B --> C["Planning without mutation"]
    C --> D["Execution"]
    D --> E["Events and sync"]
```

---

## What happens on a normal drop

```mermaid
flowchart TD
    A["Player releases item\nover target slot"] --> B["CanDrop and\nmechanical rule checks"]
    B -->|Failed| X1["Item returns\nto source"]
    B -->|Passed| C["Build plan:\nwhere and how much to place"]
    C --> D["Business checks:\nCanCommitTransfer"]
    D -->|Failed| X2["Item returns\nto source"]
    D -->|Passed| E["Execute transfer:\nsplit → conversion → placement"]
    E --> F["OnTransferSucceeded\n(business side effects)"]
    F --> G["OnItemRemoved / OnItemAdded\n→ AddToData / RemoveFromData"]
```

---

## Why planning exists

Before any real mutation, the system decides:

- can the item fit
- is partial transfer needed
- is a swap required
- is there a valid target slot
- does the item need conversion for the target inventory

This enables:

- no state corruption on invalid operations
- atomic execution with rollback
- uniform handling of drag, quick transfer and swap

---

## Planning vs commit

- planning does not mutate inventory state
- commit applies real changes

That is why:

- `CanDrop` is good for preview and mechanics
- `CanCommitTransfer` is the right place for fast local pre-commit checks
- `CanCommitTransferAsync` is the right place for external async checks if the binding implements the interface

If both versions exist, the order is always:

1. `CanCommitTransfer`
2. `CanCommitTransferAsync`
3. commit

---

## Item conversion

When source and target use different item representations, conversion happens in two stages:

```mermaid
flowchart TD
    A["Item split from\nsource slot"] --> B["Outgoing conversion:\nsource releases item\nin its format"]
    B --> C["Incoming conversion:\ntarget accepts item\nin its format"]
    C --> D["Item placed\nin target slot"]
```

Example: a merchant stores items as ScriptableObjects, and the player uses runtime models. On purchase:

1. Item is split from the merchant's slot
2. **Outgoing**: merchant releases the item (SO → intermediate format)
3. **Incoming**: player inventory accepts the item (intermediate → runtime model)
4. Item is placed in the player's slot

Important for asset users:

- each adapter in the stack is converted individually (unique runtime data is preserved)
- if any adapter fails conversion, the entire operation rolls back
- the target inventory receives the item already in its own format

---

## Detailed execution order

For each planned entry, the following happens:

```mermaid
flowchart TD
    A["Capture snapshots\nof source and target"] --> B["Check CanCommitTransfer\non both inventories"]
    B -->|Rejected| X["Rollback: restore\nfrom snapshot"]
    B -->|OK| C["Take items from\nsource slot"]
    C --> D["Outgoing conversion\n(each adapter individually)"]
    D -->|Error| X
    D -->|OK| E["Place into target\ninventory (incoming\nconversion inside)"]
    E -->|Didn't fit| F["Return remainder\nto source"]
    E -->|Fits| G["Record outcome"]
    F --> G
```

After **all** entries are executed:

```mermaid
flowchart TD
    A["All entries\ncompleted"] --> B["OnTransferSucceeded\non each DataBinding"]
    B --> C["OnItemRemoved — source\n(with original adapters)"]
    C --> D["OnItemAdded — target\n(with final adapters)"]
    D --> E["OnDropCompleted"]
```

Important: events fire **after the entire operation completes**, not one per entry. This prevents false events when a later rollback occurs in Atomic mode.

---

## What happens on failure

If a check or commit fails, the behavior depends on the policy:

| Situation | What happens |
|---|---|
| `CanDrop` returns failure | transfer does not start, item returns to source |
| `CanCommitTransfer` returns failure | transfer is cancelled before commit, state unchanged |
| `CanCommitTransferAsync` returns failure | transfer is cancelled before commit, state unchanged |
| Atomic batch: one item fails | entire operation is cancelled, no items are moved |
| BestEffort batch: one item fails | remaining items are transferred, failed ones stay in source |

The key point: if a check fails, inventories remain in their original state. Planning builds a plan without mutations, and commit only applies after all checks pass.

---

## Drop Policy

The current model has three layers:

- `DropRequestPolicy`
  - temporary per-operation override
  - can override:
    - `BlockedTargetBehavior`
    - `AlternativePlacementMode`
    - `AllowPartial`
- `DropPolicySettings`
  - inventory-level defaults on `UniversalInventory`
  - defines:
    - blocked target behavior
    - allow merge on drop
    - allow partial
    - batch mode
    - alternative placement mode
- `ResolvedDropPolicy`
  - final non-nullable policy used by the planner

`BlockedTargetBehavior`:
- `Reject`
- `Swap`
- `FindAlternative`

## Temporary override through actions

Temporary drop policy override is done through action-level request policy, not by mutating `DragContext`.

Example:
- default `CompleteDragAction` calls `CompleteDrag(null)`
- `Ctrl` variant of `CompleteDragAction` calls `CompleteDrag(DropRequestPolicy.WithBlocked(BlockedTargetBehavior.Swap))`
- `Shift` variant of `CompleteDragAction` calls `CompleteDrag(DropRequestPolicy.WithFindAlternative())`
- actions can also override `AllowPartial` and `AlternativePlacementMode` for a single transfer

Important:
- the override only applies to the current transfer operation
- inventory-level `DropPolicySettings` stay unchanged
- final `ResolvedDropPolicy` is assembled in this order:
  1. action request override
  2. drop-target override
  3. inventory defaults

## Drop Policy Processing Order

For a single drag entry:

1. Resolve `ResolvedDropPolicy`
2. Try placing into the target slot if one exists
3. If everything fits, the entry succeeds
4. If only part fits:
   - `AllowPartial = false` -> fail
   - `AllowPartial = true` -> partial success
   - remainder can search for alternatives only when `BlockedTargetBehavior = FindAlternative`
5. If nothing fits into the target:
   - `Reject` -> fail
   - `Swap` -> planner creates a swap entry
   - `FindAlternative` -> placement strategy enumerates alternative slots
6. For same-inventory drops, `FindAlternative` does not reshuffle items across other slots. If the target fails, the item stays in place

## Batch operations

When transferring multiple items at once, behavior is determined by `BatchMode` inside `DropPolicy`:

```mermaid
flowchart TD
    A["Transferring multiple\nitems"] --> B{"Which policy?"}
    B -->|Atomic| C["All or nothing:\nif any item fails,\ncancel everything"]
    B -->|BestEffort| D["Transfer what fits,\nleave the rest\nin source"]
```

By default, batch mode comes from the target inventory's `DropPolicySettings`, while request overrides only affect temporary runtime behavior.

---

## Swap is part of the same pipeline

```mermaid
flowchart LR
    A["Target slot is occupied"] --> B{"BlockedTargetBehavior = Swap?"}
    B -->|No| C["Reject"]
    B -->|Yes| D["Validate both directions"]
    D --> E["Execute swap"]
```

---

## What you need to know vs what you don't

Usually an asset user needs to understand:

- where to write rules
- where to write business checks
- when data syncs
- why partial transfer and swap behave predictably

Usually not needed upfront:

- internal helper structures in the planning layer
- low-level execution layer steps
- map of all internal pipeline classes

If you're modifying the asset itself rather than just using it, see the [File Map](../reference/file-map.md).
