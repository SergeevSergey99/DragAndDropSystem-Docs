# Binding Templates

This page describes the ready-made templates your DataBinding inherits from. Each template automatically implements UI loading, add/remove event handling, and data synchronization — you only need to define a few primitive methods.

For a general overview of DataBinding and its role in the pipeline, see [Data Binding](data-binding.md).

---

## Which template to choose

```mermaid
flowchart TD
    A["What data structure\nbacks your inventory?"] --> B{"Dynamic list?"}
    B -->|Yes| C["ListInventoryDataBinding\nBackpack, chest, loot"]
    B -->|No| D{"Slots with numeric index?"}
    D -->|Yes| E["SlotIndexedInventoryDataBinding\nHotbar, slot array"]
    D -->|No| F["MappedSlotInventoryDataBinding\nEquipment, named slots"]
```

| Template | Data structure | Sync pattern | When to use |
|---|---|---|---|
| `ListInventoryDataBinding` | Dynamic list | Per-adapter (each adapter separately) | Backpack, chest, loot, merchant |
| `SlotIndexedInventoryDataBinding` | Array/dict by index | Per-slot (single call per slot) | Hotbar, equipment slot array |
| `MappedSlotInventoryDataBinding` | Named properties | Per-slot + per-slot validation | Character equipment (head, body, weapon) |

---

## ListInventoryDataBinding

For inventories where data is stored as a **list**: `List<T>`, array, database collection.

### What to implement

```csharp
public class BackpackBinding : ListInventoryDataBinding<ItemSO, ItemSOAdapter>
{
    [SerializeField] private List<ItemSO> _items;

    // 1. Where to read data from when loading UI
    protected override IReadOnlyList<ItemSO> GetItems() => _items;

    // 2. How to create an adapter from a data element
    protected override ItemSOAdapter CreateAdapter(ItemSO item) => new(item);

    // 3. How to add to data (called for EACH adapter in the stack)
    protected override void AddToData(ItemSOAdapter adapter) => _items.Add(adapter.Data);

    // 4. How to remove from data (called for EACH adapter in the stack)
    protected override void RemoveFromData(ItemSOAdapter adapter) => _items.Remove(adapter.Data);
}
```

### How it works automatically

**On load** (`ReloadUI`): iterates `GetItems()`, creates an adapter for each element, adds to UI.

**On item added** (drag & drop): iterates all adapters in the stack and calls `AddToData` for each:

```
Stack of 3 items → AddToData(adapter[0]), AddToData(adapter[1]), AddToData(adapter[2])
```

**On item removed**: same pattern, `RemoveFromData` for each adapter.

!!! note "Why per-adapter?"
    Each adapter in a stack can hold unique runtime data (serial number, purchase timestamp). Per-adapter processing guarantees the exact instances are added to and removed from your data.

---

## SlotIndexedInventoryDataBinding

For inventories where data is tied to **slots by numeric index**: fixed-size array, `int → Item` dictionary.

### What to implement

```csharp
public class HotbarBinding : SlotIndexedInventoryDataBinding<ItemSO, ItemSOAdapter>
{
    [SerializeField] private ItemSO[] _slots = new ItemSO[8];

    // 1. Which slots are occupied (skip empties)
    protected override IEnumerable<(int index, ItemSO item, int count)> GetOccupiedSlots()
    {
        for (int i = 0; i < _slots.Length; i++)
            if (_slots[i] != null)
                yield return (i, _slots[i], 1);
    }

    // 2. How to create an adapter from a data element
    protected override ItemSOAdapter CreateAdapter(ItemSO item) => new(item);

    // 3. How to write data to a slot (single call for the whole stack)
    protected override void AddToSlotData(int index, ItemSOAdapter adapter, int count)
        => _slots[index] = adapter.Data;

    // 4. How to clear a slot's data (single call for the whole stack)
    protected override void RemoveFromSlotData(int index, ItemSOAdapter adapter, int count)
        => _slots[index] = null;
}
```

### How it works automatically

**On load**: iterates `GetOccupiedSlots()`, creates an adapter for each, adds to UI at the slot index.

**On add/remove**: called **once** for the entire stack, passing `PrimaryAdapter` and total `count`:

```
Stack of 3 items into slot #2 → AddToSlotData(2, primaryAdapter, 3)
```

### Difference from List template

| | List | SlotIndexed |
|---|---|---|
| Sync | Per adapter | Single call per slot |
| Slot identity | None | Numeric index |
| Size | Dynamic | Usually fixed |

---

## MappedSlotInventoryDataBinding

For inventories where each slot is a **separate named property** with its own read, write, and validation logic. Supports both single items and stacks — both slot types can be mixed in the same `CreateBindingMap()`.

### What to implement

```csharp
public class EquipmentBinding : MappedSlotInventoryDataBinding<ItemModel, ItemModelAdapter>
{
    [SerializeField] private UniversalSlot _weaponSlot;
    [SerializeField] private UniversalSlot _armorSlot;
    [SerializeField] private UniversalSlot _potionSlot;
    [SerializeField] private CharacterData _data;

    // 1. Declarative map: slot → how to read, write, clear, validate
    protected override Dictionary<ISlot, SlotBinding<ItemModel, ItemModelAdapter>> CreateBindingMap() => new()
    {
        // Single item (simple constructor)
        [_weaponSlot] = new(
            get: () => _data.Weapon,
            set: adapter => _data.Weapon = adapter.Model,
            clear: () => _data.Weapon = null,
            canDrop: adapter => adapter.Model.Type == ItemType.Weapon
                ? RuleResult.Success()
                : RuleResult.Failure("Weapons only")),

        [_armorSlot] = new(
            get: () => _data.Armor,
            set: adapter => _data.Armor = adapter.Model,
            clear: () => _data.Armor = null),

        // Stacking slot (list constructor)
        [_potionSlot] = new(
            getAll: () => _data.Potions,
            add: adapters => _data.AddPotions(adapters),
            remove: adapters => _data.RemovePotions(adapters),
            clear: () => _data.ClearPotions(),
            canDrop: adapter => adapter.Model.Type == ItemType.Potion
                ? RuleResult.Success()
                : RuleResult.Failure("Potions only")),
    };

    // 2. How to create an adapter from a data element (for ReloadUI)
    protected override ItemModelAdapter CreateAdapter(ItemModel item) => new(item);
}
```

### How it works automatically

**On load**: iterates all entries in `BindingMap`, calls `GetAll()` for each slot. For each item in the list, creates a separate adapter and assembles them into an `ItemStack` via `ItemStack.TryCreate()`.

**On item added**: filters adapters of type `TAdapter` from the stack, finds the binding for the target slot, calls `Add(adapter_list)`.

**On item removed**: similarly filters adapters, finds the binding for the source slot, calls `Remove(adapter_list)`.

**On CanDrop/CanStartDrag check**: automatically calls `CanDrop(adapter)` / `CanStartDrag(adapter)` for the target/source slot using `PrimaryAdapter`, if a validator is provided.

### Two SlotBinding constructors

**Simple** — for single items (one item per slot):

```csharp
new SlotBinding<TData, TAdapter>(
    get: () => ...,              // read current value (TData)
    set: adapter => ...,         // write via adapter (TAdapter)
    clear: () => ...,            // clear
    canDrop: adapter => ...      // optional: validation on drop (TAdapter)
)
```

Internally `get/set/clear` are wrapped into the list-based API: `GetAll` returns a single-element array, `Add` calls `set(adapters[0])`, `Remove` calls `clear()`.

**Stacking** — for multiple identical items in a slot:

```csharp
new SlotBinding<TData, TAdapter>(
    getAll: () => ...,           // all items in the slot (IReadOnlyList<TData>)
    add: adapters => ...,        // add adapters (IReadOnlyList<TAdapter>)
    remove: adapters => ...,     // remove adapters (IReadOnlyList<TAdapter>)
    clear: () => ...,            // full clear
    canDrop: adapter => ...      // optional: validation on drop (TAdapter)
)
```

`add`/`remove` receive the concrete adapter instances from the stack. Like `ListInventoryDataBinding`, this lets you work with individual instance data without an intermediate `ExtractData` step.

!!! note "Both types in one BindingMap"
    Both constructors produce the same `SlotBinding<TData, TAdapter>`. They can be freely mixed in a single dictionary — the base class always works through the unified list-based API.

`canDrop` and `canStartDrag` are optional in both constructors. If not set, the slot accepts anything that passes other rules.

### Difference from SlotIndexed template

| | SlotIndexed | MappedSlot |
|---|---|---|
| Slot identity | Numeric index | `ISlot` object reference |
| Slot data | Same pattern for all | Individual get/set/clear per slot |
| Validation | Shared via `CanDrop` override | Individual `CanAccept` per slot |
| Stacks | Via count parameter | Via list-based API (each adapter individually) |
| Slot count | Can be many | Usually < 10 |

---

## Base class: InventoryDataBindingBase

All three templates inherit from `InventoryDataBindingBase`. You normally don't inherit from it directly, but it's useful to know which virtual methods are available for override:

| Method | Default | When to override |
|---|---|---|
| `CanStartDrag(context, entry)` | `Success` | Block taking items from this inventory |
| `CanDrop(context, entry)` | `Success` | Add drop checks (MappedSlot overrides this automatically) |
| `CanSwap(args)` | `Success` | Extra swap validation |
| `OnSwapCompleted(args)` | No-op | React to completed swap |
| `CreateItemConverter()` | `null` | Item conversion between inventories with different formats |
| `CanHandleOccupiedSlotDrop(entry, slot)` | `false` | Custom handling for drops onto occupied slots |
| `ExecuteOccupiedSlotDrop(entry, slot)` | `false` | Execute the custom occupied slot drop |

Helper methods are also available:

- `ReloadUI()` — full resync of UI with data
- `ClearUI()` — clear all slots
- `AddToUIQuiet(adapter, count, slotIndex)` — add item without generating events
- `BeginSync()` — start sync scope (suppresses events, prevents feedback loops)
