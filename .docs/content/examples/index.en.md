# Examples

This section describes the **actual demo scenes shipped in `Examples/`**: how they are composed, which patterns they demonstrate, and which files are worth reading first.

If Quick Start answers "how do I get my first inventory working?", the Examples section answers different questions:

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

### [Showcase](showcase.md)

Use it for:
- presenting real shipped or in-progress projects using the asset
- collecting screenshots, GIFs, videos, and external links
- maintaining a single gallery of user case studies

Shows:
- project cards
- screenshot and GIF galleries
- YouTube embeds and external links
- a recommended structure for each case study

Real file location:
- `.docs/content/examples/showcase.en.md`

### [Demo1 Inventories](demo1-inventories.md)

Use it for:
- the simplest list-based inventory example
- understanding `ListInventoryDataBinding` without extra domain complexity
- local `CanStartDrag` and `CanDrop` overrides

Shows:
- `ListInventoryDataBinding<ItemExampleSO, ItemAdapterSoAdapter>`
- loading a list into UI
- syncing the list back into data
- basic inventory rules and custom validation hooks

Real code location:
- `Examples/Demo1 Inventaries/*`

### [Demo2 Loot](demo2-loot.md)

Use it for:
- world -> event -> UI -> inventory flow
- chests and interaction-driven UI opening
- pickup / drop flows connected to world objects

Shows:
- a mediator between world layer and UI
- separate bindings for player and chest inventories
- `IInteractable`, `Chest`, and `PlayerInteraction`
- world drop / pickup integration

Real code location:
- `Examples/Demo2 Loot/*`

### [Demo3 Minecraft](demo3-minecraft.md)

Use it for:
- slot-indexed inventories
- a crafting grid plus a dedicated result slot
- per-slot max stack rules

Shows:
- `SlotIndexedInventoryDataBinding`
- separate hotbar / inventory / craft table bindings
- `CraftingManager` as the domain source of truth
- `CraftResultDataBinding` as a read-only output slot with side effects

Real code location:
- `Examples/Demo3 Minecraft/*`

### [Demo4 Trading](demo4-trading.md)

Use it for:
- cross-inventory transfers between different domain models
- converters
- gold, prices, and commit-time validation

Shows:
- player / merchant / equipment inventories
- `ListInventoryDataBinding` and `MappedSlotInventoryDataBinding`
- domain hooks and conversion pipeline
- separation of mechanical validation and business validation

Real code location:
- `Examples/Demo4 Trading/*`

### [Demo5 Containers](demo5-containers.md)

Use it for:
- items that contain their own inventory
- opening a container from a context menu
- custom occupied-slot drops and cycle prevention

Shows:
- item instances instead of plain ScriptableObject rows
- a container acting as both an item and a data source
- `ContainerUIController` and active-container switching
- safeguards such as "a container cannot be placed into itself"

Real code location:
- `Examples/Demo5 Containers/*`

---

## How to choose a demo

| If you need | Start with |
|---|---|
| A gallery of real projects and case studies | [Showcase](showcase.md) |
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
