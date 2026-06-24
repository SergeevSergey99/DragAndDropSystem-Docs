# Item Conversion

This page explains how to move items between inventories that use different data models.

Example: a merchant stores goods as `ScriptableObject`, the player stores purchased
items as runtime models, and equipment uses fixed slots with its own checks.

## When A Converter Is Needed

A converter is needed when an item must move from one adapter type to another.

Typical cases:

- merchant and player store items in different models
- equipment inventory accepts only special adapters
- a container inside an item stores runtime instances
- the same item should look different in different inventories

If both inventories use the same adapter type, a converter is usually not needed.

## Where To Configure It

The converter is configured in the binding:

```csharp
protected override IItemAdapterConverter CreateItemConverter()
{
    return new MyItemAdapterConverter();
}
```

The binding tells the system:

- how items from this inventory look when they leave it
- how items should look when they enter this inventory

If no converter is configured, the item is left as-is.

## Simple Model

When an item moves from one inventory to another, the system needs an adapter that the
target inventory understands.

```text
source inventory adapter
  -> converter
  -> target inventory adapter
```

The converter's main job is to keep the meaning of the item when it crosses from one data model to another.

## What A Converter Must Preserve

If items are unique, the converter must preserve more than icon and name.

Check that it preserves:

- `ItemId`, if it affects stacking
- item count in the stack
- unique runtime state
- reference to a domain model, if the item is not just a `ScriptableObject`
- data needed by `CanDrop`, tooltip, price, rarity, or equipment checks

If after transfer “the item looks correct, but can no longer be dragged”, the target slot most likely received the wrong adapter type or lost required data.

## Swap Between Different Inventories

Swap between different inventory types is not a simple exchange of two stacks.

Each item must be converted to the model of the inventory it is entering:

```text
item A -> inventory B model
item B -> inventory A model
```

If you simply swap two adapters, the next drag/drop may break because a slot stores an item in the wrong format.

## When To Return `null`

A converter may return `null` if the item cannot be safely converted to the required model.

That is appropriate when:

- the item must not enter this inventory
- the target adapter type cannot be created
- conversion would lose important data

Do not use `null` for temporary blocks such as “not enough money” or “shop is closed”.
Use rules or `ITransferDomainHandler` for that.

## Common Mistakes

### `Wrong Item Type` Appears After Transfer

Check:

- whether `CreateItemConverter()` is implemented
- whether the converter returns the target inventory adapter
- whether the source inventory adapter remained in the slot

### First Swap Works, Second Swap Breaks

Check:

- whether conversion exists in both directions
- whether two stacks are being swapped directly without converter
- which adapter is stored in each slot after the first swap

### Item Data Was Lost

Check:

- whether runtime instance state is transferred
- whether all items in a stack are created from one adapter
- whether price, rarity, durability, owner, or other model fields are lost

## Checklist For A New Converter

- source and target inventories really use different adapter types
- binding overrides `CreateItemConverter()`
- converter creates the adapter type expected by the target inventory
- each unique item keeps its own state
- swap is tested in both directions
- after transfer, the next drag/drop from the target slot works

See also:

- [Demo4 Trading](../examples/demo4-trading.md)
- [Troubleshooting](../reference/troubleshooting.md)
- [Transfer Pipeline](transfer-pipeline.md)
