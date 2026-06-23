# Demo3 Craft

<div class="showcase-video">
    <iframe
    src="https://www.youtube.com/embed/lbcTdQGJTIs"
    title="Project showcase video"
    allow="accelerometer; autoplay; clipboard-write; encrypted-media; gyroscope; picture-in-picture; web-share"
    allowfullscreen>
    </iframe>
</div>

`Examples/Demo3 Craft/CraftDemo.unity`

This is a slot-indexed inventory and crafting example where UI synchronizes with
fixed data arrays instead of a list.

## What The Demo Shows

- `SlotIndexedInventoryDataBinding`
- crafting table and result slot
- maximum stack limit per slot
- `CraftResultDataBinding` as a custom inventory type for taking the crafting result

## How It Is Structured

The central logic point is:

- `Crafting/CraftingManager.cs`

It stores:

- `_hotbarItems`
- `_inventoryItems`
- `_craftTableItems`
- recipe list
- current result and crafting multiplier

UI is split into four independent bindings:

- `MainInventoryDataBinding`
- `HotbarDataBinding`
- `CraftTableDataBinding`
- `CraftResultDataBinding`

## How It Works

Regular slots:

1. Binding enumerates occupied indices through `GetOccupiedSlots()`.
2. `AddToSlotData(...)` and `RemoveFromSlotData(...)` call `CraftingManager` methods.
3. On `Awake()`, inventories call `SetMaxStackSize(CraftingManager.MaxItemsPerSlot)` to limit item count per slot.

Crafting:

1. Changing the craft table updates `_craftTableItems`.
2. `CraftingManager.RefreshCraftResult()` searches for a matching recipe.
3. `CraftResultDataBinding.OnReloadUI()` shows the result and configures the drag step.
4. When the user takes the result, `OnItemRemovedFromUI(...)` calls `ConsumeCraftIngredients(...)`.

This is a useful example of a read-only slot that does not accept incoming drop, but
creates an item based on external logic that can be taken out.

## When To Use This Example

- you need an inventory with fixed indices
- you need crafting
- you need an output slot with an effect after extraction
