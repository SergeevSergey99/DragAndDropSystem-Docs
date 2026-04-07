# Trading

This example shows a case where two inventories use different item models and a transfer also changes gold.

Real project location:
- `Examples/Demo4 Trading/*`

This is the main example for cases where:
- source and target use different domain models
- an item is converted while crossing the inventory boundary
- transfer success depends on business rules, not only slot mechanics

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

## How the example is structured

```mermaid
flowchart LR
    subgraph Merchant Side
        MInv["Merchant Inventory UI"]
        MDB["MerchantInventoryDataBinding"]
        MData["MerchantData"]
    end

    subgraph Player Side
        PInv["Player Inventory UI"]
        PDB["PlayerInventoryDataBinding"]
        PData["PlayerData"]
    end

    Conv["Item Converters"]
    Trade["TradingHelper / domain hooks"]

    MInv <--> MDB
    MDB <--> MData
    PInv <--> PDB
    PDB <--> PData
    MDB --- Conv
    PDB --- Conv
    MDB --- Trade
    PDB --- Trade
```

Responsibility split:
- inventory and strategies handle placement mechanics
- converters handle model transitions
- domain hooks handle gold, prices, and side effects
- bindings synchronize UI with domain data

---

## Purchase flow

```mermaid
flowchart TD
    A["Player drops merchant item"] --> B["Player Inventory: CanDrop\nmechanics and compatibility"]
    B --> C["Merchant Inventory → Player Inventory\nconvert item into player format"]
    C --> D["Domain Hook: CanCommitTransfer\nenough gold?"]
    D --> E["Execute transfer"]
    E --> F["Domain Hook: OnTransferSucceeded\nupdate gold"]
    F --> G["Player / Merchant Data\nAddToData / RemoveFromData"]
```

---

## Where each piece belongs

| Task | Where it belongs |
|---|---|
| Only valid item type in equipment slot | `canDrop` or `CanDrop` |
| Check available gold | `CanCommitTransfer` |
| Apply gold changes | `OnTransferSucceeded` |
| Convert `SO <-> Model` | `CreateItemConverter()` |
| Sync lists and fields | `AddToData` / `RemoveFromData` |

---

## Conversion note

- merchant side stores a more static item representation
- player side stores a runtime model
- crossing the inventory boundary triggers conversion automatically
- runtime `ItemStack` may contain a list of concrete adapters, but rules/bindings/converters still use the representative adapter via `PrimaryAdapter`

---

## What to inspect in code

| File | Role |
|---|---|
| `TradingHelper.cs` | trading checks and side effects |
| `PlayerInventoryDataBinding.cs` | player sync and player-side domain hooks |
| `MerchantInventoryDataBinding.cs` | merchant sync and merchant-side domain hooks |
| `EquipmentInventoryDataBinding.cs` | fixed-slot equipment |
| `ModelInventoryItemConverter.cs` | converter into player format |
| `MerchantInventoryItemConverter.cs` | converter into merchant format |

---

## Successful purchase flow

1. The player starts a drag from merchant inventory.
2. The planner builds a target-side preview item through the converter.
3. Player-side rules validate mechanical compatibility.
4. A domain hook checks whether there is enough gold to commit.
5. The executor performs the transfer and only then triggers side effects.
6. DataBinding updates the player and merchant data lists.

---

## Where to go next

- [Data Binding](../architecture/data-binding.md) — full lifecycle hooks
- [Equipment](equipment.md) — if you only need fixed slots without trading
- [Examples Overview](index.md) — to compare other scenarios
