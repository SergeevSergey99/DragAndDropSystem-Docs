# Trading

This example shows a case where two inventories use different item models and a transfer also changes gold.

---

## What participates in the operation

```mermaid
flowchart TB
    Merchant["Merchant inventory<br/>ScriptableObject items"]
    Player["Player inventory<br/>Runtime model items"]
    Equipment["Equipment slots"]
    Economy["Economy<br/>gold and prices"]

    Merchant <-->|buy / sell| Player
    Player <-->|equip| Equipment
    Economy --- Merchant
    Economy --- Player
```

---

## The important split

There are three separate concerns:

1. inventory and slot mechanics
2. item conversion between two data models
3. business logic for the operation: enough gold, what to do after purchase/sale

These should not be mixed together.

---

## Purchase flow

```mermaid
sequenceDiagram
    participant U as Player
    participant M as Merchant Inventory
    participant P as Player Inventory
    participant D as Domain Hook
    participant Data as Player / Merchant Data

    U->>P: Drops merchant item
    P->>P: CanDrop (mechanics, compatibility)
    M->>P: Convert item into player format
    P->>D: CanCommitTransfer (enough gold?)
    P->>P: Execute transfer
    P->>D: OnTransferSucceeded (update gold)
    P->>Data: AddToData / RemoveFromData
```

---

## Where each piece belongs

| Task | Where it belongs |
|---|---|
| Only valid item type in equipment slot | `canAccept` or `CanDrop` |
| Check available gold | `CanCommitTransfer` |
| Apply gold changes | `OnTransferSucceeded` |
| Convert `SO <-> Model` | `CreateItemConverter()` |
| Sync lists and fields | `AddToData` / `RemoveFromData` |
