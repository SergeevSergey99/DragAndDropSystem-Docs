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
- input pipeline for mouse, navigation, legacy Input Manager buttons, and `InputAction`
- world loot / drop support
- item conversion between different inventory models
- nested container example
- shaped items example

## Included Demos

- `Demo1 Inventories`: basic list-based inventories
- `Demo2 Loot`: chest interaction and world-to-UI flow
- `Demo3 Craft`: slot-indexed inventory and crafting grid
- `Demo4 Trading`: trading, equipment slots, converters, and money checks
- `Demo5 Containers`: nested inventories and container items
- `Demo6 Shaped Items`: items that take up more than one slot

## Package Layout

- `Scripts/`: runtime and editor code
- `Prefabs/`: ready-to-use scene prefabs
- `Settings/`: presets and default assets
- `Examples/`: demo scenes and integration samples

## Dependencies

- Required: `Unity.ugui`
- Optional: `com.unity.inputsystem`

## Without Input System

The core package is designed to compile and work without the new Input System package.

Available without `com.unity.inputsystem`:

- pointer-based drag and drop
- stacking, swapping, split/merge, and transfer pipeline
- context menu
- tooltip system
- selection through pointer interactions
- legacy `KeyCode` bindings
- legacy Input Manager button-name bindings such as `Submit` and `Cancel`

Requires `com.unity.inputsystem`:

- `InputAction` bindings
- `InputActionSelectionTrigger`
- navigation modality tracking based on `Gamepad` / `Keyboard.current`

Legacy input setup notes:

- the scene still needs a working `EventSystem`
- if you are not using the new Input System, the `EventSystem` must have `StandaloneInputModule`
- demos created in Unity 6 may serialize `InputSystemUIInputModule`; in older or legacy-input projects this can leave the `EventSystem` without a usable input module
- the runtime now auto-adds `StandaloneInputModule` when it detects legacy input mode and no usable UI input module is present
- mixed setups are supported: a project may include the new Input System while a specific scene still uses `StandaloneInputModule`
- in a mixed setup, pointer and UI navigation follow the scene's active `EventSystem` module
- old Input Manager button-name bindings require Legacy Input Manager support to be enabled in Player Settings
- `InputAction`-driven features still depend on the new Input System being configured for that workflow, so they may not work as expected in scenes intentionally using the legacy UI input path

## Notes

- This asset is intentionally flexible, so integrating your own data types usually requires a small adapter and one binding.
- `UniversalInventory` authors inventory strategy and slot management directly through `[SerializeReference]` pickers.
- Add a custom inventory behavior by inheriting from `InventoryStrategyBase`.
- Add a custom slot lifecycle mode by inheriting from `SlotManagementSettingsBase`.
- Select blocked-target behavior with `BlockedTargetResolutionKind`.
- Customize automatic alternative placement by inheriting from `PlacementCandidateOrderer`.
- Runtime code is split into asmdefs for cleaner integration.
- Example scenes are meant to show integration patterns, not the only valid architecture.

## Full Documentation

Full documentation can be found at https://sergeevsergey99.github.io/UniversalDragAndDrop-Docs/
Discord server for questions, bug reports and feedback https://discord.gg/HXf6Wv6UTx