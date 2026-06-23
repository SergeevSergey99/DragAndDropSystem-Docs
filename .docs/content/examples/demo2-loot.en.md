# Demo2 Loot

<div class="showcase-video">
    <iframe
    src="https://www.youtube.com/embed/Q1B4ef6LyTU"
    title="Project showcase video"
    allow="accelerometer; autoplay; clipboard-write; encrypted-media; gyroscope; picture-in-picture; web-share"
    allowfullscreen>
    </iframe>
</div>

`Examples/Demo2 Loot/LootDemo.unity`

This is an example of a slot-indexed inventory and a connection between the game world,
events, and UI.

## What The Demo Shows

- chests as interactive world objects
- separate bindings for player and chest
- switching different data sources for DataBinding
- pickup/drop scenarios connected to the world
- the world does not know about UI directly

## How It Is Structured

The key point is that `Chest` and `PlayerInteraction` live in the game world and do not
know about concrete UI panels. UI opening and binding are handled by `LootUIController`.

## How It Works

Opening a chest:

1. `PlayerInteraction` finds an `IInteractable`.
2. The player action calls `Interact(...)`.
3. `Chest` changes state and publishes an event.
4. `LootUIController` receives the event.
5. The controller activates the panel and calls `BindToChest(...)`.
6. `ChestInventoryDataBinding` loads chest contents into UI.

Moving items:

1. The player drags items between player inventory and chest inventory.
2. The standard transfer pipeline performs the transfer.
3. After successful commit, bindings update `Chest` and `PlayerInventoryData`.

## When To Use This Example

- you need chests opened through interaction
- you need an example of separating game-world logic from inventory UI
