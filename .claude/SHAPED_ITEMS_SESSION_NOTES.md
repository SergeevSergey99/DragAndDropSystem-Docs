# Shaped Items Architecture Session Notes

Date: 2026-05-11

## Implemented During This Session

- Fixed shaped occupied-slot rendering regressions:
  - Occupied cells under shaped items now keep slot-filled visual state after drag/drop.
  - `PlacementOverlay` uses a dedicated item component so filled/dragged-from/dragged-to states can be customized separately from `BaseSlot`.

- Added shaped rotation support:
  - Drag entries carry `Orientation`.
  - Rotation actions can switch between `Rot0`, `Rot90`, `Rot180`, and `Rot270`.
  - Drop planning/execution respects rotated footprint size and rotated grab offset.
  - Fixed edge cases where rotated items failed to drop over their own source cells.

- Introduced configurable shaped placement anchoring:
  - Added anchor strategy API.
  - Current strategies include rotated grab offset, source grab offset, and target-slot anchoring.
  - This makes cursor-to-footprint behavior project-configurable instead of hardcoded around one anchor slot.

- Completed placement metadata propagation:
  - Events and transfer results now carry `PlacementSnapshot`.
  - Metadata includes anchor index, anchor slot, covered indices, covered slots, orientation, and footprint.
  - Added target/source resolved slot helpers for data binding semantics.

- Improved snapshot safety:
  - `InventorySnapshot` placement collections are immutable copies.
  - `RestoreSnapshot` uses atomic preflight and reports failures instead of silently dropping placements.
  - Rollback now restores via placement state, not legacy per-slot fallback.

- Closed live stack mutation boundary:
  - Added `IReadOnlyItemStack`.
  - `BaseSlot.Stack` and `Placement.Stack` expose read-only stack views.
  - Live stack mutations now go through `ISlotStackStore`.
  - Removed built-in fallback direct mutations from strategy helpers.
  - `Placement` now copies incoming `ItemStack` ownership, so caller-owned stacks cannot invalidate live placement state.
  - Removed `PruneEmptyPlacements` from hot-path reads.

- Hardened placement read models:
  - `Placement.CoveredIndices` no longer exposes a live mutable `List<int>`.
  - Added read-only stack enumeration via `UniversalInventory.GetAllStacksReadOnly()`.

- Added placement-aware inventory contract:
  - Introduced `IPlacementInventory`.
  - `UniversalInventory` implements `IPlacementInventory`.
  - Shaped planner/executor/strategy paths now depend on `IPlacementInventory` instead of directly requiring `UniversalInventory` where possible.

## Important Current Design Rules

- `Placement` is the source of truth for shaped item occupancy.
- `BaseSlot.Stack` is a read-only facade over inventory-owned storage.
- Live inventory stacks must not be mutated directly.
- Runtime/storage mutations should go through `ISlotStackStore`.
- Strategy transfer buffers may still be mutable `ItemStack` instances.
- Shaped placement logic should target `IPlacementInventory`, not concrete `UniversalInventory`, unless it needs UniversalInventory-specific events, rules, UI, or slot management.

## Remaining Work / Recommended Next Steps

1. Run Unity compile and EditMode tests.
   - This environment cannot run Unity or `dotnet`.
   - Pay special attention to API consumers affected by `BaseSlot.Stack` returning `IReadOnlyItemStack`.

2. Continue removing concrete `UniversalInventory` dependencies where appropriate.
   - Keep concrete references for UI/MonoBehaviour serialized fields, interaction routing, inventory actions, and UniversalInventory-specific events.
   - Prefer `IPlacementInventory` for grid/placement semantics.

3. Split `UniversalInventory` into smaller collaborators.
   - Suggested first extraction: `PlacementStore`.
   - Then consider `DropPreviewResolver` and `ShapedAnchorResolver`.

4. Simplify `TransferPlanExecutor`.
   - Current executor still has separate paths for slot allocation, placement allocation, swap, and occupied handlers.
   - After mutation boundary cleanup, it is safer to unify allocation execution.

5. Formalize custom strategy API documentation.
   - Document that strategies can mutate transfer buffers.
   - Document that live slot stacks must be changed through `ISlotStackStore`.
   - Add examples for custom shaped anchor strategy and custom shaped placement strategy.

6. Decide future support for full shape masks.
   - Current footprint occupancy is rectangular.
   - `Rot0`/`Rot180` and `Rot90`/`Rot270` are equivalent for rectangular occupancy, though visuals can differ.
   - Non-rectangular masks will require 4-way rotation-aware covered-cell generation.
