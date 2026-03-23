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
sequenceDiagram
    participant User as Player
    participant Rules as Rules / DataBinding
    participant Pipeline as Transfer Pipeline
    participant UI as UniversalInventory
    participant Data as Your data

    User->>Rules: Releases item over target
    Rules->>Rules: CanDrop and mechanical checks
    Pipeline->>Pipeline: Build plan
    Pipeline->>Rules: CanCommitTransfer
    opt binding implements IAsyncTransferDomainHandler
        Pipeline->>Rules: CanCommitTransferAsync
    end
    Pipeline->>UI: Execute transfer
    Pipeline->>Rules: OnTransferSucceeded
    UI->>Data: AddToData / RemoveFromData
```

---

## Why planning exists

Before any real mutation, the system decides:

- can the item fit
- is partial transfer needed
- is a swap required
- is there a valid target slot
- does the item need conversion for the target inventory

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

```mermaid
flowchart LR
    A["Merchant inventory<br/>ScriptableObject"] --> B["Converter"]
    B --> C["Player inventory<br/>Runtime model"]
```

The important user-facing detail is simple:

- the target inventory receives the item in its own format

---

## After success

```mermaid
flowchart TD
    A["Transfer completed successfully"] --> B["OnTransferSucceeded"]
    A --> C["OnItemRemoved / OnItemAdded"]
    C --> D["AddToData / RemoveFromData"]
```

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

## Batch operations and policies

When transferring multiple items at once, behavior is determined by `DropPolicy`:

```mermaid
flowchart TD
    A["Transferring multiple\nitems"] --> B{"Which policy?"}
    B -->|Atomic| C["All or nothing:\nif any item fails,\ncancel everything"]
    B -->|BestEffort| D["Transfer what fits,\nleave the rest\nin source"]
```

Defaults:

- single transfer — `StrictTarget`, `Partial`, `BestEffort`
- batch (from selection) — `TargetAsHint`, `RejectAll`, `Atomic`

The policy can be configured in the inspector on each `UniversalInventory` (Drop Policy section).

---

## Swap is part of the same pipeline

```mermaid
flowchart LR
    A["Target slot is occupied"] --> B{"Does policy allow swap?"}
    B -->|No| C["Reject"]
    B -->|Yes| D["Validate both directions"]
    D --> E["Execute swap"]
```
