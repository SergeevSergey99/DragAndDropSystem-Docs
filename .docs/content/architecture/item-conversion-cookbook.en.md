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

## Rules See The Converted Item

The target inventory's checks run **after** conversion. `CanDrop`, the inventory's rules and the
slot's rules all receive the item as it will exist once it is inside — already in the target
inventory's adapter type.

This is what makes typed bindings work across data models. A binding declared as
`MappedSlotInventoryDataBinding<TradableItemModel, TradableItemAdapterModelAdapter>` receives its own
adapter type even when the item is dragged in from a merchant that stores `ScriptableObject`s:

```csharp
canDrop: adapter => adapter.Item.originalSO.ItemType == ItemType.Weapon
    ? RuleResult.Success()
    : RuleResult.Failure("Only weapons can be placed in this slot")
```

You never call the converter yourself inside a rule. If you do, you create a second object that is
not the one the transfer will store, and you pay for it on every hover frame.

`CanStartDrag`, by contrast, runs on the source side and sees the source inventory's adapter.

## Conversion Runs During Preview

A converter is called while the player is only hovering, long before anything is dropped. Two
consequences:

**The converter must be a pure factory.** No registering the new item anywhere, no taking an id from
a counter, no spawning objects. A hover the player abandons must leave no trace. Do that work in
`ITransferDomainHandler.OnTransferSucceeded`, which only runs after a committed transfer.

**The object you build during preview is the object that gets stored.** Conversions are resolved
once per drag and reused, so the adapter validated by `CanDrop` is the same instance that ends up in
the target slot. You do not need to make the conversion cheap by cutting corners — it happens once.

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

Both directions are also checked against the rules of the inventory they land in. The item coming
back from the target must be allowed to leave its slot and to enter the source slot, exactly as if
you had dropped it there. A swap is not a way around a rule that would refuse a plain drop.

## When To Return `null`

A converter may return `null` if the item cannot be safely converted to the required model.

That is appropriate when:

- the item must not enter this inventory
- the target adapter type cannot be created
- conversion would lose important data

Do not use `null` for temporary blocks such as “not enough money” or “shop is closed”.
Use rules or `ITransferDomainHandler` for that.

Returning `null` refuses the drop the same way a rule does: the slot shows the refusal while the
player is still hovering, instead of the drag ending with nothing happening.

## Common Mistakes

### `Wrong Item Type` Appears After Transfer

Check:

- whether `CreateItemConverter()` is implemented
- whether the converter returns the target inventory adapter
- whether the source inventory adapter remained in the slot

### The Target Refuses Everything From Another Inventory

If a slot with a typed check rejects every item coming from an inventory with a different data
model, the converter is the thing to look at — not the rule. The rule already receives the converted
item, so a rejection means the conversion did not produce the expected adapter type.

Check:

- whether the target binding overrides `CreateItemConverter()`
- whether `TryConvertIncoming` handles the source adapter type at all (a `switch` with no matching
  case returning `null` refuses the drop)
- whether the converter preserves the fields the rule reads

### A Partial Transfer Behaves Oddly

Dragging part of a stack moves the **last** items of that stack. If your own code builds a stack to
predict what will move, build it the same way (`stack.CreateCopy(count)`), otherwise your prediction
and the actual transfer refer to different item instances — which only shows up on partial moves,
never on full ones.

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
- converter is a pure factory: no registration, no id counters, no spawning
- converter preserves the fields the target's rules read (`ItemId`, type, price, …)
- each unique item keeps its own state
- swap is tested in both directions
- partial transfer is tested, not only full-stack moves
- after transfer, the next drag/drop from the target slot works

See also:

- [Demo4 Trading](../examples/demo4-trading.md)
- [Troubleshooting](../reference/troubleshooting.md)
- [Transfer Pipeline](transfer-pipeline.md)
