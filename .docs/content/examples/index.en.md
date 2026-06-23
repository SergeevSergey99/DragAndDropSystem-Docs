# Examples

This section describes the **demo scenes shipped in `Examples/`**: how they are composed, which patterns they demonstrate, and which files are worth reading first.

Use this page as a chooser. If you already know what problem you are solving, jump straight to the matching demo; otherwise start with the table below.

## Quick chooser

| Task | Start with | Key systems | Complexity |
|---|---|---|---|
| Basic inventory list, simple rules, and drop areas | [Demo1 Inventories](demo1-inventories.md) | `ListInventoryDataBinding`, local hooks, rules | Low |
| Chest UI, pickup/drop, and world object integration | [Demo2 Loot](demo2-loot.md) | world interaction, chest binding, filters | Medium |
| Hotbar, inventory, craft grid, and result slot | [Demo3 Craft](demo3-Craft.md) | `SlotIndexedInventoryDataBinding`, `CraftingManager`, craft result | Medium |
| Trading, gold, equipment, and data model conversion | [Demo4 Trading](demo4-trading.md) | converters, domain checks, fixed equipment slots | High |
| An item that contains its own nested inventory | [Demo5 Containers](demo5-containers.md) | context menu, nested binding, occupied-slot handler | High |
| Multi-cell items, shapes, anchors, rotation, and cell preview | [Demo6 Shaped Items](demo6-shaped-items.md) | placement topology, shapes, rotation actions | High |

## What each demo shows

| Demo | What it shows | Start with these files |
|---|---|---|
| [Demo1 Inventories](demo1-inventories.md) | The simplest item list, basic binding, and local drag/drop checks. | `Examples/Demo1 Inventories/BasicListDataBinding.cs`, `Examples/Demo1 Inventories/ItemAdapterSoAdapter.cs` |
| [Demo2 Loot](demo2-loot.md) | A "world -> event -> UI -> inventory" flow, chests, pickup/drop, and filters. | `Examples/Demo2 Loot/ChestInventoryController.cs`, `Examples/Demo2 Loot/WorldDropManager.cs` |
| [Demo3 Craft](demo3-Craft.md) | Slot-indexed data, craft grid, hotbar, and a dedicated result inventory. | `Examples/Demo3 Craft/CraftingManager.cs`, `Examples/Demo3 Craft/Data/CraftResultDataBinding.cs` |
| [Demo4 Trading](demo4-trading.md) | Transfers across different data models, prices, gold, and equipment slots. | `Examples/Demo4 Trading/Domain/TradeDomainHandler.cs`, `Examples/Demo4 Trading/Converters/*` |
| [Demo5 Containers](demo5-containers.md) | A container as both an item and a data source, nested inventory, and cycle protection. | `Examples/Demo5 Containers/ContainerItemData.cs`, `Examples/Demo5 Containers/ContainerUIController.cs` |
| [Demo6 Shaped Items](demo6-shaped-items.md) | Shaped grid items: footprint, anchor, orientation, rotation, and covered-cell preview. | `Examples/Demo6 Shaped Items/ShapedItemSO.cs`, `Examples/Demo6 Shaped Items/ShapedItemAdapter.cs` |

## How to read demo pages

Each demo page answers four questions:

- what the demo shows
- which runtime objects participate
- how the main scenario flows
- which files to inspect if you want to reuse the pattern in your own project

---

## Important note

- Examples show **integration patterns**, not the only valid architecture.
- A real project may combine pieces from several demos.
- If your domain model differs, you usually adapt bindings, adapters, and converters first, not the transfer pipeline itself.

---

## Where to go next

- [Quick Start](../getting-started/quick-start.md) — for a basic setup from scratch
- [Data Binding](../architecture/data-binding.md) — for the full lifecycle and hooks
- [Transfer Pipeline](../architecture/transfer-pipeline.md) — for transfer order and rollback details
- [File Map](../reference/file-map.md) — for quickly finding a concrete runtime or example type
