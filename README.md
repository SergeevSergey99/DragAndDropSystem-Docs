# Universal Drag & Drop System

Flexible Unity data-binding driven inventory system with drag and drop, stacking, swapping, quick transfer, rules, input actions, context menu and multi-selection integration.

This package is built around a simple idea:

- `UniversalInventory` handles UI state and transfer mechanics
- `IItemAdapter` lets the system represent your item data in slots
- `DataBinding` syncs UI changes back into your game data

Because of that separation, the asset can work with:

- `ScriptableObject` items
- runtime item models
- list-based inventories
- indexed or fixed-slot inventories
- different item representations in different inventories

## Included Features

- drag and drop between inventories
- stack, split, merge, and swap flows
- transfer planning and rollback-safe execution
- inventory, slot, and global rules
- domain hooks before commit and after success
- context menu, tooltip, and selection systems
- input pipeline for mouse, navigation, and `InputAction`
- world loot / drop support
- item conversion between different inventory models
- nested container example

## Included Demos

- `Demo1 Inventories`: basic list-based inventories
- `Demo2 Loot`: chest interaction and world-to-UI flow
- `Demo3 Minecraft`: slot-indexed inventory and crafting grid
- `Demo4 Trading`: trading, equipment slots, converters, and money checks
- `Demo5 Containers`: nested inventories and container items

## Package Layout

- `Scripts/`: runtime and editor code
- `Prefabs/`: ready-to-use scene prefabs
- `Settings/`: presets and default assets
- `Examples/`: demo scenes and integration samples

## Notes

- This asset is intentionally flexible, so integrating your own data types usually requires a small adapter and one binding.
- Runtime code is split into asmdefs for cleaner integration.
- Example scenes are meant to show integration patterns, not the only valid architecture.

## Full Documentation

Full documentation can be found at https://sergeevsergey99.github.io/DragAndDropSystem-Docs/
