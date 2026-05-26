---
Last Updated: 2026-05-26
---

# Placement Shape Refactor Plan

## Scope

This epic replaces rectangle-only placement sizing as the core placement shape model with an offsets-based shape API.

In scope:

- Add shape/offsets model for rectangular shapes, masks, and future topology-specific shapes.
- Add a single covered-cell calculation path.
- Remove the old rectangle-size compatibility API before release.
- Prepare placement geometry for future hex-like topologies while continuing to use `Vector2Int`.

Out of scope:

- Shaped stack support.
- Shaped batch placement solver.
- Removing shaped placement methods from inventory strategies.
- Splitting `IPlacementInventory` from drag target resolving.
- Extracting `PlacementStore`.
- Removing hardcoded `UniversalInventory` casts.

## Target API

```csharp
public interface IPlacementShape
{
    IReadOnlyList<Vector2Int> GetOffsets(PlacementOrientation orientation);
    bool SupportsOrientation(PlacementOrientation orientation);
}

public interface IItemPlacementShapeProvider
{
    IPlacementShape PlacementShape { get; }
}
```

Shape bounding is intentionally not part of `IPlacementShape`.
Use `PlacementShapeUtility.GetBoundingSize(shape, orientation)` so non-rectangular shapes do not expose a misleading rectangular contract.

## Shape Rules

- Offsets are relative to the placement anchor cell.
- Offsets use `Vector2Int`.
- For rectangular grids, `x/y` are row-major grid coordinates.
- Future hex layouts may interpret `x/y` as logical axial-like coordinates inside their topology.
- `GetOffsets(orientation)` should return a cached immutable/read-only list for the same shape and orientation.
- Unsupported orientation means placement is invalid for that orientation.

## Shape Resolution

Resolution order:

1. `IItemPlacementShapeProvider.PlacementShape`
2. `RectPlacementShape.One`

## Snapshot Rule

`PlacementSnapshot` must not store `IPlacementShape` references.
Snapshots should store POCO/runtime-safe data:

- anchor index
- orientation
- bounding size
- covered offsets
- covered indices
- resolved slots where needed by event consumers

## Migration Order

1. Add `IPlacementShape`, `IItemPlacementShapeProvider`, `RectPlacementShape`, and `PlacementShapeUtility`.
2. Add `PlacementCellUtility` with `PlacementBoundsMode`.
3. Add golden parity tests comparing old rectangle coverage with the new helper.
4. Replace `UniversalInventory` covered-cell builders with `PlacementCellUtility`.
5. Thread `IPlacementShape` through `PlacementRequest`, `Placement`, `DragEntry`, `PlannedPlacementAllocation`, and shaped contexts.
6. Update planner, executor, data binding, overlay, and Demo6 to resolve shapes through `PlacementShapeUtility`.
7. Update `PlacementSnapshot` with covered offsets and bounding size.
8. Add tests for provider priority, fallback, unsupported orientation, cached offsets, and snapshot metadata.
9. Remove the old rectangle-size API and expose bounding data through `Vector2Int BoundingSize`.

## Expected Final Flow

```text
ItemAdapter
  -> IItemPlacementShapeProvider.PlacementShape
        |
        v
PlacementRequest(shape, anchor, orientation)
        |
        v
PlacementCellUtility
  shape.GetOffsets(orientation)
  topology anchor + offsets -> covered indices
        |
        v
IPlacementInventory.CanPlace / TryPlace
        |
        v
Placement(stack, anchor, orientation, shape, coveredIndices)
```
