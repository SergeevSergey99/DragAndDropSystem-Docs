# Demo5 Containers

<div class="showcase-video">
    <iframe
    src="https://www.youtube.com/embed/3BFn4jt9xhM"
    title="Project showcase video"
    allow="accelerometer; autoplay; clipboard-write; encrypted-media; gyroscope; picture-in-picture; web-share"
    allowfullscreen>
    </iframe>
</div>

`Examples/Demo5 Containers/Containers Demo.unity`

This is an example of inventory-items that contain their own nested item set.

## What The Demo Shows

- item instances instead of simple list rows
- a container as a regular item inside the player inventory
- a separate UI panel for the active container contents
- context menu action for opening a container
- dropping an item directly onto a container icon to put it inside
- protection against cycles when nesting containers

## How It Is Structured

Data:

- `Scripts/Data/ItemInstance.cs`
- `Scripts/Data/ContainerItemInstance.cs`
- `Scripts/Data/IContainerizeItemInstance.cs`
- `Scripts/ContainerDemoManager.cs`

Bindings and UI:

- `Scripts/Bindings/PlayerContainerInventoryDataBinding.cs`
- `Scripts/Bindings/ContainerInventoryDataBinding.cs`
- `Scripts/UI/ContainerUIController.cs`
- `Scripts/ContextMenu/OpenContainerMenuEntrySO.cs`

## How It Works

Opening a container:

1. `ContainerItemInstance` is stored in the player inventory.
2. The context menu calls `OpenContainerMenuEntrySO`.
3. Through `Events.OnOpenClick`, the selected container is passed to `ContainerUIController`.
4. The controller sets the active container.
5. `ContainerInventoryDataBinding` rebuilds inventory size and loads container contents.

Moving items:

1. Regular drag/drop operations work between player inventory and container inventory.
2. Bindings synchronize transfer into the player list or `ContainerItemInstance.Items`.
3. If an item is dropped onto a slot that contains a container, `IPreRuleOccupiedSlotDropHandler` intercepts that attempt before regular drop rules.
4. The binding checks that the container has capacity and that the move would not place a container into itself or one of its children.
5. If the check passes, `ContainerViewRegistry.InsertIntoContainer(...)` puts the item inside the container and returns `OccupiedSlotDropResult.Handled`.
6. If the check fails, the handler returns `Rejected`, and the item stays in its original place.

## When To Use This Example

- an item must contain a nested inventory
- you need a context menu for item operations
- dropping onto an already occupied slot should mean a separate action, such as "put this inside the container"
