# Quick Start

This walkthrough creates two regular inventories that support drag & drop between each other.

```mermaid
flowchart LR
    A["Backpack"] <-->|drag & drop| B["Chest"]
```

---

## What you need

- `DragAndDropManager` in the scene
- `EventSystem`
- `Canvas`
- two objects with `UniversalInventory`
- one item type
- one `ListInventoryDataBinding`

---

## Step 1. Prepare the scene

1. Add `DragAndDropManager` from `Prefabs/DragingObj`.
2. Make sure there is an `EventSystem`.
3. Create a `Canvas` if needed.

---

## Step 2. Create two inventories

For the first working setup, use:

| Field | Value |
|---|---|
| `Item Behavior` | `Unique` |
| `Slot Management` | `Fixed` |
| `Initial Slot Count` | e.g. `10` |
| `Slot Prefab` | `Prefabs/Slot` |
| `Slot Container` | parent for generated slots |

Duplicate the object to create a second inventory.

---

## Step 3. Define an item type

```csharp
[CreateAssetMenu(menuName = "Game/Item")]
public class ItemSO : ScriptableObject
{
    [field: SerializeField] public string ItemName { get; private set; }
    [field: SerializeField] public Sprite Icon { get; private set; }
}
```

Then add an adapter:

```csharp
using DragAndDropSystem.Core;
using UnityEngine;

public class ItemSOAdapter : IInventoryItem
{
    public readonly ItemSO Data;

    public ItemSOAdapter(ItemSO data) => Data = data;

    public string ItemId => Data.GetInstanceID().ToString();
    public Sprite Icon => Data.Icon;
    public string DisplayName => Data.ItemName;
}
```

---

## Step 4. Add a simple binding

```csharp
public class BackpackBinding : ListInventoryDataBinding<ItemSO, ItemSOAdapter>
{
    [SerializeField] private List<ItemSO> _items;

    protected override IReadOnlyList<ItemSO> GetItems() => _items;
    protected override ItemSOAdapter CreateAdapter(ItemSO item) => new(item);
    protected override ItemSO ExtractData(ItemSOAdapter adapter) => adapter.Data;
    protected override void AddToData(InventoryItemEventContext ctx, ItemSO item) => _items.Add(item);
    protected override void RemoveFromData(InventoryItemEventContext ctx, ItemSO item) => _items.Remove(item);
}
```

Attach one binding per inventory.

---

## Step 5. Fill starting data

1. Create a few `ItemSO` assets.
2. Put them into the `_items` list.
3. Run the scene.

If everything is wired correctly:

- both inventories show items
- items can be dragged between them
- your backing list updates automatically

---

## What happens behind the scenes

```mermaid
flowchart LR
    A["Player drags an item"] --> B["UniversalInventory executes transfer"]
    B --> C["DataBinding receives event"]
    C --> D["Your List<ItemSO> is updated"]
```

---

## Next

- [Equipment Example](../examples/equipment.md)
- [Trading Example](../examples/trading.md)
- [Data Binding](../architecture/data-binding.md)
