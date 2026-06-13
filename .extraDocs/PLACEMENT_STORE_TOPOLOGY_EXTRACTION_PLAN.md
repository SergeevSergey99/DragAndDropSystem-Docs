---
Last Updated: 2026-05-27
---

# Placement Store and Topology Extraction Plan

## Goal

Split placement/occupancy logic out of `UniversalInventory` into reusable runtime collaborators so the same placement engine can power:

- current `UniversalInventory`;
- future focused `SlotInventory`;
- future `GridInventory`;
- future `HexInventory`.

The extraction must keep current public behavior compatible while reducing duplication and avoiding another parallel inventory implementation.

## Non-goals

This plan does not immediately:

- replace `UniversalInventory` in scenes;
- move serialized Unity fields into runtime classes;
- rewrite transfer planner/executor;
- redesign stack strategies;
- implement full hex UI/drop preview.
- split drag-target resolution out of `IPlacementInventory`;
- remove shaped placement methods from `IInventoryStrategy`;
- extract snapshot codec out of `UniversalInventory`;
- solve `ISlotStackStore` compatibility for external inventory implementations.

## Core Principle

Serialized configuration stays on Unity components. Runtime state and pure placement logic move into plain C# classes.

```csharp
public sealed class UniversalInventory : MonoBehaviour
{
    [SerializeField] private bool _useGridTopology;
    [SerializeField] private GridTopology _gridTopology;

    private PlacementStore _placementStore;
    private IInventoryTopology _topology;
}
```

`PlacementStore` is not a `MonoBehaviour`, not a `ScriptableObject`, and should not own serialized fields.

## Target Architecture

```text
Unity component
  UniversalInventory / SlotInventory / GridInventory / HexInventory
        |
        | owns serialized fields, slots, events, visuals, data binding
        v
Runtime placement collaborator
  PlacementStore
        |
        | uses shape offsets and topology
        v
Topology abstraction
  SlotTopology / RectGridTopology / HexTopology
```

## Topology Contract

Topology describes coordinate/index conversion and valid cells.

```csharp
public interface IInventoryTopology
{
    int CellCount { get; }
    bool Contains(Vector2Int cell);
    bool TryToIndex(Vector2Int cell, out int index);
    Vector2Int ToCell(int index);
    bool IsValidIndex(int index);
    IReadOnlyList<Vector2Int> GetPlacementOffsets(
        IPlacementShape shape,
        int orientation);
}
```

`TryToIndex` owns the bounds check. Callers should not compute an index for cells
outside the topology's valid domain. This keeps rectangular and future hex
topologies on the same contract and avoids relying on undefined sentinel indices.

Initial implementations:

- `SlotTopology`: one-dimensional slot layout. Every item occupies exactly one
  logical slot, so placement offsets always resolve to the anchor.
- `RectGridTopology`: current row-major rectangular grid.
- `HexTopology`: future implementation using `Vector2Int` as axial-like logical coordinates.

Do not put Unity UI concerns in topology. It is geometry/indexing only.

Footprint projection belongs to topology semantics:

- `SlotTopology` returns only `(0,0)` for any item shape;
- `RectGridTopology` returns the real oriented shape offsets.

`PlacementStore` consumes that projection uniformly and only checks bounds and
occupancy. It does not branch on concrete topology types or apply a separate
shaped-item policy.

## PlacementStore Scope

Move these runtime fields from `UniversalInventory`:

- `_cellToPlacement`;
- `_placements`.

Move these responsibilities:

- placement reset/clear;
- `CanPlace`;
- `TryPlace`;
- `GetPlacementAt`;
- `RemovePlacement`;
- register/unregister placement;
- covered-cell calculation through `PlacementCellUtility`;
- shifting placements after slot removal by recreating placements, not mutating them.

Do not move:

- serialized fields;
- slot prefab/container creation;
- `BaseSlot` lifecycle;
- Unity events;
- visual refresh calls;
- drag/drop policies;
- data binding;
- transfer planner/executor orchestration.

## Proposed PlacementStore API

```csharp
public sealed class PlacementStore
{
    public IReadOnlyCollection<Placement> Placements { get; }

    public void Reset();

    public bool CanPlace(PlacementRequest request, Placement ignoredPlacement = null);
    public bool TryPlace(PlacementRequest request, out Placement placement);

    public Placement GetAt(int cellIndex);
    public bool Remove(Placement placement);
    public bool RemoveAt(int cellIndex);

    public IReadOnlyList<int> GetCoveredIndices(
        int anchorIndex,
        IPlacementShape shape,
        int orientation,
        PlacementBoundsMode boundsMode);

    public IReadOnlyList<int> GetCoveredIndices(
        Vector2Int anchorCell,
        IPlacementShape shape,
        int orientation,
        PlacementBoundsMode boundsMode);

    public void ShiftAfterSlotRemoved(int removedIndex);
}
```

The store receives its topology directly:

```csharp
var store = new PlacementStore(topology);
```

`PlacementStore` is plain C# and should be valid immediately after construction.
Do not copy the existing lazy-init pattern (`EnsurePlacementStateInitialized`,
`ClearInitializedPlacementState`) into the store. If topology changes,
construct a new store or call `Reset()` with explicit replacement wiring in the
owning component.

## Compatibility Strategy

Phase 1 keeps `UniversalInventory` as the only scene-facing component.

`UniversalInventory` delegates to `PlacementStore` but keeps the same public API:

- `Placements`;
- `CanPlace`;
- `TryPlace`;
- `GetPlacementAt`;
- `RemovePlacement`;
- snapshot capture/restore behavior.

Existing scenes and data bindings should not need migration in Phase 1.

## Future Inventory Components

After `UniversalInventory` delegates to `PlacementStore`, thin specialized components become possible:

```text
SlotInventory
  - serialized slot settings
  - SlotTopology
  - stack/merge rules

GridInventory
  - serialized columns/rows
  - RectGridTopology
  - PlacementStore

HexInventory
  - serialized hex dimensions/layout mode
  - HexTopology
  - PlacementStore
```

Avoid inheritance-heavy design initially. Prefer composition so components can share `PlacementStore`, snapshot codec, and transfer-facing adapters.

`SlotInventory` would differ from `UniversalInventory` primarily by serialized
surface and inspector UX, not by runtime placement behavior. Do not create a
second slot placement implementation.

`GridInventory` is optional. Decide whether it is useful only after
`UniversalInventory` is fully delegated to `PlacementStore`. If it only mirrors
`UniversalInventory` with fewer fields, defer it.

## Shape Compatibility

Item geometry remains `IPlacementShape`:

```csharp
IReadOnlyList<Vector2Int> GetOffsets(int orientation);
bool SupportsOrientation(int orientation);
```

Topology decides how `anchor + offset` maps to a cell index.

For rectangular grids:

```text
Vector2Int(x, y) -> row-major cell
```

For hex inventories:

```text
Vector2Int(q, r) -> axial-like logical cell
```

No item-side API change should be required for hex support, as long as item shapes are authored in the topology's logical coordinate system.

## Migration Order

### 1. Add topology abstractions

Add:

- `IInventoryTopology`;
- `SlotTopology`;
- `RectGridTopology`.

Keep current `GridTopology` as the serialized value type for rectangular grids, but adapt it into `RectGridTopology` at runtime.

### 2. Add PlacementStore skeleton

Create `PlacementStore` with fields and no behavioral change.

Initially move logic from `UniversalInventory`, then wire tests directly against store for:

- single-cell placement;
- rectangular grid placement;
- L-shape placement;
- overlap rejection;
- out-of-bounds rejection;
- unsupported orientation rejection.

### 3. Delegate UniversalInventory placement methods

Replace internal `UniversalInventory` placement fields/methods with calls into `PlacementStore`.

Keep method names/public API stable:

- `CanPlace(...)`;
- `TryPlace(...)`;
- `GetPlacementAt(...)`;
- `RemovePlacement(...)`;
- `RemovePlacementAt(...)`;
- `Placements`.

### 4. Move snapshot geometry restore through PlacementStore

Snapshot codec can stay in `UniversalInventory` initially, but covered-cell validation should call the store/topology path.

Preserve the current POCO snapshot rule:

- anchor index;
- orientation;
- bounding size;
- covered offsets;
- covered indices;
- adapter list.

No `IPlacementShape` references in snapshots.

### 5. Move drop preview covered-cell calculation

Keep preview ownership in `UniversalInventory`, but route geometry through `PlacementStore.GetCoveredIndices(..., IncludeOnlyInBounds)`.

Do not move UI highlight state into store.

### 6. Introduce optional specialized GridInventory

Decision point only after `UniversalInventory` is green with `PlacementStore`.

`GridInventory` should initially be a thin component/facade, not a second implementation of placement logic.

Skip this step if `UniversalInventory + RectGridTopology` already gives a clean
scene-facing API.

### 7. Add HexTopology prototype

Add only after `RectGridTopology` and `GridInventory` are stable.

Focus first on:

- coordinate/index conversion;
- placement validation;
- snapshot restore.

UI/drop-preview for hex can be a separate step.

## Tests Required

PlacementStore unit tests:

- single-cell slot placement;
- rect grid placement;
- non-rect offsets placement;
- overlap rejection;
- out-of-bounds rejection;
- unsupported orientation rejection;
- remove placement unregisters all cells;
- shift after slot removed recreates placements and does not mutate old references.

UniversalInventory integration tests:

- existing shaped placement tests remain green;
- snapshot restore still atomic;
- dynamic slot removal still shifts placements;
- active drag during dynamic slot removal does not crash and either re-resolves
  placement state or cancels cleanly;
- drop preview still reports in-bounds cells;
- overlay still renders rectangular and non-rect placements.

Future topology tests:

- `RectGridTopology` parity with existing `GridTopology`;
- `SlotTopology` one-dimensional index/cell conversion;
- `SlotTopology` projects every item to its anchor slot;
- `HexTopology` coordinate/index round-trip.

## Risk Controls

- Keep `UniversalInventory` public API stable during extraction.
- Move one responsibility at a time.
- Add direct tests for `PlacementStore` before deleting old code.
- Do not introduce `GridInventory` until `UniversalInventory` delegates to the store.
- Do not move Unity object lifecycle into store.
- Do not serialize runtime store state.
- Treat `Placement` references as short-lived runtime references. After slot
  removal and placement recreation, old references are orphaned and must not be
  used as stable identity.
- UI/drag hot paths that compare `Placement` references should re-resolve from
  inventory state when possible, or tolerate orphaned references.

## Expected End State

```text
UniversalInventory
  - owns Unity fields, slots, events, binding, visuals
  - delegates placement state to PlacementStore

PlacementStore
  - owns placements and occupancy map
  - validates and mutates placement state
  - uses IInventoryTopology and IPlacementShape

IInventoryTopology
  - defines coordinate/index rules
  - enables RectGrid and future Hex without duplicating placement logic
```

This keeps shaped inventory logic reusable while avoiding parallel `GridInventory` and `HexInventory` implementations that duplicate core placement behavior.
