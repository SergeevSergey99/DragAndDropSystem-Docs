# Examples

This section describes the **demo scenes shipped in `Examples/`**: how they are composed, which patterns they demonstrate, and which files are worth reading first.

The Examples section answers questions:

- how each demo is structured by layers
- where data, adapters, bindings, and UI live
- how the main interaction flow works
- which files to inspect if you want to reuse the pattern in your own game

---

## How to read this section

Each demo page explains:

1. **What the demo shows**
2. **How it is structured**
3. **How the main scenario works**
4. **Which files to inspect**

The goal is to help you understand the architectural shape quickly, not just reproduce a scene.

---

## Included demos

### [Demo1 Inventories](demo1-inventories.md)

Use it for:

- the simplest list-based inventory example
- understanding `ListInventoryDataBinding` without extra domain complexity
- local `CanStartDrag` and `CanDrop` overrides

Shows:

- `ListInventoryDataBinding<ItemExampleSO, ItemAdapterSoAdapter>`
- loading a list into UI
- drop areas
- basic inventory rules

`Examples/Demo1 Inventaries/*`

### [Demo2 Loot](demo2-loot.md)

Use it for:

- world -> event -> UI -> inventory flow
- chests and interaction-driven UI opening
- pickup / drop flows connected to world objects

Shows:

- a mediator between world layer and UI
- changing the chest's data binding data source
- world drop / pickup integration
- item filters

`Examples/Demo2 Loot/*`

### [Demo3 Minecraft](demo3-minecraft.md)

Use it for:

- slot-indexed inventories
- a crafting grid plus a dedicated result slot
- per-slot max stack rules

Shows:

- `SlotIndexedInventoryDataBinding`
- separate hotbar / inventory / craft table bindings
- `CraftingManager` as the domain source of truth
- `CraftResultDataBinding` as a custom read-only output inventory

`Examples/Demo3 Minecraft/*`

### [Demo4 Trading](demo4-trading.md)

Use it for:

- data conversions between inventories are needed 
- the operation depends on money, prices and checks at the time of transfer

Shows:

- player / merchants / equipment inventories
- `ListInventoryDataBinding` and `MappedSlotInventoryDataBinding`
- data models changes during cross-inventory transfers

`Examples/Demo4 Trading/*`

### [Demo5 Containers](demo5-containers.md)

Use it for:

- items that contain their own inventory
- opening a nested container from a context menu
- the item must drop into the slot occupied by the container item and must be protected from cycles

Shows:

- item instances instead of plain ScriptableObject rows
- a container acting as both an item and a data source
- `ContainerUIController` and active-container switching
- safeguards such as "a container cannot be placed into itself"

`Examples/Demo5 Containers/*`

---

## How to choose a demo

| If you need | Start with |
|---|---|
| Basic inventory list + simple hooks | [Demo1 Inventories](demo1-inventories.md) |
| Chest UI and world interaction | [Demo2 Loot](demo2-loot.md) |
| Crafting grid and slot-indexed data | [Demo3 Minecraft](demo3-minecraft.md) |
| Trading, conversion, and gold logic | [Demo4 Trading](demo4-trading.md) |
| Nested containers and context menu | [Demo5 Containers](demo5-containers.md) |

---

## Important note

- Examples show **integration patterns**, not the only valid architecture.
- A real project may combine pieces from several demos.
- If your domain model differs, you usually adapt bindings, adapters, and converters first, not the transfer pipeline itself.

---

## Where to go next

- [Quick Start](../getting-started/quick-start.md) — for a basic setup from scratch
- [Data Binding](../architecture/data-binding.md) — for the full lifecycle and hooks
- [Transfer Pipeline](../architecture/transfer-pipeline.md) — for planning / execution / rollback details
