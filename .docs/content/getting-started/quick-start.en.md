# Quick Start

This guide creates two regular inventories that support dragging items between each other.

The same setup is already implemented in the first example, where you can see the final
result.

Even in a basic scenario, you usually need a little integration code for your own data.
In this guide that means:

- `ItemSO` as your item data
- `ItemSOAdapter` as the representation for the inventory system
- `SimpleBinding` as the bridge between UI and your data list

## Step 1. Prepare The Scene

1. Create a `Canvas` for the inventories if the scene does not have one yet.
2. Add `DragAndDropManager` to the scene. You can drag the prefab from
   `Prefabs/DragCanvas.prefab`.
> The scene needs one `DragAndDropManager`. It manages all transfer operations and
> controls the dragged object.
3. Make sure the scene has an `EventSystem`.
   If the project uses legacy input, the `EventSystem` should have `StandaloneInputModule`.
   The asset also supports the New Input System. `DragAndDropManager` has a minimal
   working action config by default. You can edit it or create your own copy, then assign
   it in the scene.

---

## Step 2. Create Two Inventories

1. Create an object named `Backpack` inside `Canvas`.
2. Add `UniversalInventory` to it.
3. Set:

    | Field | Value |
    |---|---|
    | `Slot Container` | Parent for slots, preferably with `GridLayout` or another component controlling child layout |
    | `Slot Prefab` | Slot prefab you want to use, for example `Prefabs/Slot.prefab` |
    | `Initial Slot Count` | For example `10`. On startup, slots are created in `Slot Container`. If you already created them manually, you can cache them with the button |
    | `Inventory Strategy` | `UniqueItemStrategy` for the simplest start, so each item occupies its own slot |
    | `Slot Management` | `FixedSlotManagementSettings` for a fixed number of slots |

4. Duplicate the object and create a second inventory, for example `Chest`.

!!! info Initialization
    On startup, the inventory tries to find already created slots under `Slot Container`
    and cache them. If there are not enough slots, it creates slots from `Slot Prefab`
    up to `Initial Slot Count`. You can also create all slots manually in edit mode and
    cache them with `Cache Slots`, so this operation does not happen during play.

---

## Step 3. Define An Item Type

Suppose you have an item type:

```csharp
[CreateAssetMenu(menuName = "Game/Item")]
public class ItemSO : ScriptableObject
{
    [field: SerializeField] public string ItemName { get; private set; }
    [field: SerializeField] public Sprite Icon { get; private set; }
}
```

To display this type in slots, create an adapter that implements `IItemAdapter`:

```csharp
using UDND.Core;
using UnityEngine;

public class ItemSOAdapter : IItemAdapter
{
    // Reference to your data type.
    public readonly ItemSO Data;

    // Constructor.
    public ItemSOAdapter(ItemSO data) => Data = data;

    // Required interface fields.
    public string ItemId => Data.GetInstanceID().ToString();
    public Sprite Icon => Data.Icon;
    public string DisplayName => Data.ItemName;
}
```

Required field meanings:

- `ItemId` — used to decide whether items can be merged into one slot for `Stackable`
  and `SeparableStacks` strategies.
- `Icon` — used to display the item image in the slot.
- `DisplayName` — used mainly in logs and some optional systems, such as the tooltip
  example. It can be an empty string.

---

## Step 4. Add A Simple Binding

To show your data in slots, you need to give the system access to that data and define
how it can interact with it. In real projects, these data sets usually live in player,
character, or world-object scripts. In this example, the list is stored directly in the
component.

```csharp
using UDND.Core;
using UDND.DataBinding;
using System.Collections.Generic;
using UnityEngine;

public class SimpleBinding : ListInventoryDataBinding<ItemSO, ItemSOAdapter>
{
    // Your data list.
    [SerializeField] private List<ItemSO> _items;

    // Required overrides.
    protected override IReadOnlyList<ItemSO> GetItems() => _items;
    protected override ItemSOAdapter CreateAdapter(ItemSO item) => new(item);
    protected override void AddToData(ItemSOAdapter adapter) => _items.Add(adapter.Data);
    protected override void RemoveFromData(ItemSOAdapter adapter) => _items.Remove(adapter.Data);
}
```

`ListInventoryDataBinding` is a DataBinding template for data stored as a list. When
inheriting from it, you specify your item type and its adapter type.

Required method meanings:

- `GetItems` — used for initial data rendering.
- `CreateAdapter` — creates an adapter and passes it the required data.
- `AddToData` — called when an item is transferred **into this** inventory.
- `RemoveFromData` — called when an item is transferred **out of this** inventory.

Add this binding to both inventories or other objects in the scene.
Assign references to the corresponding inventories.
Each binding has its own `_items` list.

!!! info Data
    In real projects, instead of a simple `_items` list, you will most likely interact
    with your own scripts that store data. See examples for implementations of this pattern.

---

## Step 5. Fill Starting Data

1. Create a few `ItemSO` assets.
2. Add them to `_items` in your `SimpleBinding` components.
3. Run the scene.

If everything is configured correctly:

- both inventories show items
- an item can be dragged from one inventory to another
- backing data lists update automatically during transfer

---

## What Happens Under The Hood

```mermaid
flowchart LR
    A["Player drags an item"] --> B["<b>UniversalInventory</b> handles transfer"]
    B --> C["Methods are called in <b>DataBinding</b>"]
    C --> D["Your List<ItemSO> is updated"]
```

## Common First-Project Mistakes

- `ItemId` does not match your stacking logic, so items unexpectedly merge or do not merge
- there is no `DragAndDropManager` in the scene
- there is no `EventSystem` in the scene
- the project uses legacy input, but `EventSystem` has no `StandaloneInputModule`
- binding is connected to the wrong `UniversalInventory`
- `InventoryDataBinding` has no assigned inventory reference
- data changes outside the pipeline, but `ReloadUI()` is not called. Add a `ReloadUI`
  call in your DataBinding when data changes

See also:

- [Examples](../examples/index.md) — if you want to choose from all 6 demos
- [Data Binding](../architecture/data-binding.md) — if you need to understand lifecycle and extension points
- [Placement Strategies](../architecture/strategies.md) — if you want to add your own strategy or slot management mode
- [Troubleshooting](../reference/troubleshooting.md) — if the basic scene does not work on the first try
