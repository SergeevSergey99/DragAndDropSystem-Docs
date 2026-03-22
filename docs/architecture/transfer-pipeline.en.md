# Transfer Pipeline

Every item movement --- drag-and-drop, auto-transfer, swap --- goes through the same pipeline:
**Policy** &rarr; **Planning** &rarr; **Execution** &rarr; **Events**.

---

## Overview

The transfer system is built as a pipeline: first the behavior rules are determined (policy), then a plan is constructed without modifying data, after which the plan is executed and events are dispatched. This approach allows verifying an operation before executing it and rolling back on error.

---

## Item Path During Drag

```mermaid
sequenceDiagram
    participant Player
    participant System
    participant Inventory

    Player->>System: Releases item
    System->>System: Determines target (slot / area)
    System->>System: Validates rules (3 levels)
    System->>System: Plans transfer
    System->>Inventory: Executes transfer
    Inventory-->>System: Result
    System->>System: Dispatches events
```

---

## Drop Policy

Policy is an immutable set of 4 dimensions that define transfer behavior.

| Dimension | Options | What it controls |
|---|---|---|
| **What to do with an occupied slot** | Reject / Swap / Find another | Reaction to a non-empty target slot |
| **Capacity** | All or nothing / As many as fit | Whether partial transfer is allowed |
| **Batch execution** | Atomic / Partial | Roll back everything on error or keep successful |
| **Target usage** | Strictly to target / Target as hint | Exact drop or flexible search |

```mermaid
flowchart TD
    DROP["Item dropped into occupied slot"] --> CHECK{"Occupied slot policy?"}
    CHECK -->|Reject| REJ["Transfer rejected"]
    CHECK -->|Swap| SWAP["Attempt swap"]
    CHECK -->|Find another| ALT["Search for free slot"]
    ALT --> FOUND{"Free slot found?"}
    FOUND -->|Yes| OK["Place in free slot"]
    FOUND -->|No| REJ
```

---

## Planning

During the planning phase the system builds a transfer plan without modifying inventory state. This allows pre-validating the feasibility of the operation.

```mermaid
flowchart TD
    A["Get drag context"] --> B["Convert item for target inventory"]
    B --> C["Check how many will fit"]
    C --> D["Distribute across slots (virtually)"]
    D --> E["Plan ready"]
```

!!! info "Item Conversion"
    Before planning, the item is converted to the target inventory's format.
    For example: a merchant's `TradableItemSO` becomes a player's `TradableItemModel`.

---

## Execution

During the execution phase the completed plan is applied to real inventories. The execution mode is determined by the policy.

```mermaid
flowchart TD
    PLAN["Completed plan"] --> MODE{"Execution mode?"}

    MODE -->|Atomic| SNAP["Save state (snapshot)"]
    SNAP --> EXEC_A["Execute all operations"]
    EXEC_A --> FAIL{"Any errors?"}
    FAIL -->|Yes| ROLL["Roll back everything"]
    FAIL -->|No| EVT["Dispatch events"]

    MODE -->|Partial| EXEC_B["Execute all operations"]
    EXEC_B --> SKIP["Skip failed, continue"]
    SKIP --> EVT
```

- **Atomic** --- before execution, a snapshot of all affected inventories is saved. If any operation fails, everything is rolled back.
- **Partial (BestEffort)** --- executes everything possible. Failed operations are skipped.

---

## Swap

Swap is a built-in pipeline operation, not a separate path.

```mermaid
sequenceDiagram
    participant System
    participant Rules
    participant Callback
    participant Inventory

    System->>System: Planner marks swap
    System->>Rules: Validate rules (forward direction)
    System->>Rules: Validate rules (reverse direction)
    Rules-->>System: Allowed
    System->>Callback: Can swap? (can be cancelled)
    Callback-->>System: Allowed
    System->>Inventory: Items are swapped
    System->>System: Events dispatched
```

!!! warning "Cancelling a Swap"
    Any subscriber can set `Cancel = true` in the swap context to prevent it.

---

## When Events Are Dispatched

Events are deferred until the operation is fully complete:

```mermaid
flowchart LR
    DONE["Operation complete"] --> REM["Item removed from source"]
    DONE --> ADD["Item added to target"]

    REM --> DB_R["DataBinding updates data"]
    REM --> EVT_R["External subscribers notified"]

    ADD --> DB_A["DataBinding updates data"]
    ADD --> EVT_A["External subscribers notified"]
```

- In **atomic** mode events are not dispatched if something went wrong (rollback).
- DataBinding is notified directly --- it has a 1:1 relationship with the inventory.
- External subscribers use `OnItemAdded` / `OnItemRemoved` events.

---

## Auto-Transfer

Quick-click uses the same transfer mechanism. Auto-transfer programmatically creates a drag context and passes it into the same planning and execution pipeline.

```mermaid
flowchart LR
    CLICK["Quick click"] --> CTX["Create transfer context"]
    CTX --> PLAN["Planning"]
    PLAN --> EXEC["Execution"]
    EXEC --> INV["Target inventory"]
```

---

## Key Classes

| Concept | Class | Description |
|---|---|---|
| Policy | `DropPolicy` | Immutable set of 4 behavior dimensions |
| Planner | `TransferPlanner` | Builds a plan without mutating data |
| Executor | `TransferPlanExecutor` | Applies the plan, supports rollback |
| Transfer service | `InventoryTransferService` | Performs transactional transfer between slots |
| Drop processor | `InventoryDropProcessor` | Entry point: policy + plan + execution |
| Conversion | `TransferItemConversionUtility` | Converts an item for the target inventory |
| Auto-transfer | `AutoTransferService` | Programmatic transfer on click |
