# Examples

This section describes the **demo scenes from `Examples/`**: what they are made of,
which patterns they show, and which files to inspect in code.

Use this page as a map. If you already know what task you are solving, jump straight to
the matching demo; otherwise start with the table below.

## What Each Demo Shows

| Demo | Contents |
|---|---|
| [Demo1 Inventories](demo1-inventories.md) | Basic inventory list, simple rules, and drop areas. The simplest item list, basic binding, and local drag/drop checks. |
| [Demo2 Loot](demo2-loot.md) | Slot synchronization with data. Chest, pickup/drop, and connecting UI to world objects. A "world -> event -> UI -> inventory" flow with chests, pickup/drop, and filters. |
| [Demo3 Craft](demo3-Craft.md) | Crafting grid and result slot. |
| [Demo4 Trading](demo4-trading.md) | Transfer between different data models, prices, buying and selling, and equipment inventory. |
| [Demo5 Containers](demo5-containers.md) | Container as item and data source, nested inventory, and cycle protection. |
| [Demo6 Shaped Items](demo6-shaped-items.md) | Complex-shaped items occupying several slots in a rectangular grid: rotation and drawing the item over slots. Compatible with regular inventories and strategies. |

## How To Read Demo Pages

Each demo page answers four questions:

- what the demo shows
- which runtime objects participate
- how the main scenario works
- which files to inspect if you want to move the pattern into your own project

## Important Notes

- Demos show **integration patterns**, not the only correct architecture.
- You can combine parts from different demos, such as containers from Demo5 and fixed slots from Demo4.
- If your domain model is different, you usually change bindings, adapters, and converters, not the base transfer pipeline.

See also:

- [Quick Start](../getting-started/quick-start.md) — for a basic setup from scratch
- [Data Binding](../architecture/data-binding.md) — for the full lifecycle and hooks
- [Transfer Pipeline](../architecture/transfer-pipeline.md) — for transfer order and rollback
- [File Map](../reference/file-map.md) — for quickly finding a concrete runtime or example type
