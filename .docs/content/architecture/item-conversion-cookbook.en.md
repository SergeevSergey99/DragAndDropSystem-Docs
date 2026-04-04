# Cookbook: Item Conversion

This page answers a practical question:
"how should I configure conversion between two inventories that use different adapter models?"

The architectural overview already exists in [Transfer Pipeline](transfer-pipeline.md).
This page focuses on working rules and common mistakes.

---

## When a converter is needed

A converter is needed when two inventories use different representations of the same item.

Typical examples:

- a merchant stores `ScriptableObject`s while the player uses runtime models
- a UI-facing inventory uses lightweight adapters while the domain model uses rich instances
- an inventory boundary inside a container item uses another adapter model

If both sides already use the same adapter type, a converter is usually unnecessary.

---

## Where the converter lives

The converter is declared on the inventory binding side:

```csharp
protected override IItemAdapterConverter CreateItemConverter()
{
    return new MyItemAdapterConverter();
}
```

So the binding defines:

- how this inventory exports an item
- how this inventory imports an item

By default, the system uses an identity converter.

---

## Who calls conversion

### Preview / planning

During preview and planning, conversion is orchestrated by `TransferItemConversionUtility`.

This is required so target rules and binding hooks see a target-side adapter rather than the original source-side adapter.

### Normal execution

For a regular transfer, the chain is:

1. the item is taken from the source slot
2. `source outgoing` runs
3. then `target incoming` runs
4. only after that is the item placed into the target inventory

### Swap

Swap is not one symmetric conversion.
It is two separate chains:

- `A -> B`
- `B -> A`

Each one runs through its own `outgoing -> incoming`.

---

## Correct mental model

Do not think in terms of "the source inventory gives the final target object".

Think instead:

```text
source adapter
  -> source outgoing
  -> intermediate representation
  -> target incoming
  -> target adapter
```

The intermediate representation does not need to be a dedicated type.
What matters is that source and target boundaries stay independent.

---

## What an adapter must preserve

If your items have instance state, the adapter must carry it safely through conversion:

- a stable `ItemId`, if stacking semantics depend on it
- runtime fields of the specific instance
- a reference to the domain entity, if the item is unique
- data later used by `CanStartDrag`, `CanDrop`, tooltips, and side effects

If the item "looks right after transfer but drag breaks later", the usual cause is that the target received the wrong adapter type or the wrong instance state.

---

## What not to do

### Do not use one adapter as a representative for the entire stack

If a stack contains different runtime instances, do not clone one adapter through `Repeat`.

That causes:

- loss of instance state
- preview and execution diverging
- distorted remove/add payloads

### Do not implement cross-inventory swap as a raw stack exchange

If swap merely swaps two `ItemStack` objects:

- the target slot receives a foreign adapter type
- the next `CanStartDrag` or `CanDrop` starts failing on type checks

### Do not rely on preview and execution sharing the same object reference

The preview stack and the execution stack may be different objects.
Stability must come from data and conversion semantics, not reference equality.

---

## When a converter should return `null`

`null` means not "I do not want to do it right now", but "this boundary cannot export/import this item".

That is appropriate when:

- the item must never cross this boundary
- the binding cannot materialize the required target adapter
- data loss would be unacceptable

If the operation is only temporarily forbidden by business logic, that is not the converter's job.
Use:

- rules
- `CanCommitTransfer`
- `CanCommitTransferAsync`

---

## How to diagnose conversion errors

### Symptom: `Wrong item type`

Usually means:

- the target binding received the source adapter type
- swap was committed as a raw exchange
- preview converted correctly, but execution did not

### Symptom: first swap works, second swap breaks

Usually means:

- commit succeeded with the wrong adapter type stored in the slot
- after the first swap, the slot physically contains an object from the other inventory boundary

### Symptom: preview passes, commit fails

Usually means:

- the preview stack was assembled correctly
- but execution used a different conversion route

---

## Mini checklist for new converters

- the binding really overrides `CreateItemConverter()`
- outgoing and incoming are as symmetric as your domain model requires
- each adapter in a stack is converted individually
- the target stores its own inventory-specific adapter type after commit
- swap runs as two independent conversion chains

---

## Where to continue

- [Transfer Pipeline](transfer-pipeline.md) — full planning/execution order
- [Demo4 Trading](../examples/demo4-trading.md) — working example of merchant/player/equipment conversion
- [Troubleshooting](../reference/troubleshooting.md) — symptoms and common causes
