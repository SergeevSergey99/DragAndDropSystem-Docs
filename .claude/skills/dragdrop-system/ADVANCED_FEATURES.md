# Advanced Features

**Last Updated**: 2026-03-13

## Quick Click Auto-Transfer

- Implemented through `SlotInputAdapter` -> `InputEventRouter`, pointer phases, and bound actions.
- Distinguishes click vs drag by time and distance thresholds.

## Input Modality Tracking

- `InputModalityTracker` is the source of truth for `Mouse` vs `Navigation` mode.
- `InputEventRouter` no longer guesses modality on its own.
- Device-level keyboard/gamepad polling is only a minimal fallback for cold-start navigation.

## Global Input Actions

- `DefaultBindingsProfile` can execute some `InputAction` bindings without active inventory/slot.
- This is intended for global UI behaviors such as closing an already open context menu.

## Atomic Batch Execution

- Implemented via `TransferPlanExecutor` + snapshot providers.
- Prevents partial side effects for atomic policy.

## Swap Callbacks for Integrations

Swap is integrated via callbacks in execution options:
- `SwapAttempting(InventorySwapContext)` (cancelable)
- `SwapCompleted(InventorySwapContext)`

Used by DataBinding and gameplay systems to apply business rules.

## DataBinding Integration

`InventoryDataBindingBase` can react to:
- regular transfer events (`OnItemAddedToUI` / `OnItemRemovedFromUI`)
- swap callbacks (custom `CanSwapInternal` and post-swap handling)

## World 3D and Optional UI Systems

Optional modules remain independent from transfer core:
- `Scripts/World3D/*`
- `Scripts/Slots/SlotHoverEventListener.cs`
- `Scripts/UI/TooltipManager.cs`

Core transfer pipeline does not require these systems.
