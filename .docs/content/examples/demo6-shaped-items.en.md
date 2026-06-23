# Demo6 Shaped Items

<div class="showcase-video">
    <iframe
    src="https://www.youtube.com/embed/r6hML-y5wLg"
    title="Project showcase video"
    allow="accelerometer; autoplay; clipboard-write; encrypted-media; gyroscope; picture-in-picture; web-share"
    allowfullscreen>
    </iframe>
</div>

`Examples/Demo6 Shaped Items/Shaped Items.unity`

This example shows items that occupy not one slot, but a footprint of several cells in
a grid inventory.

## What The Demo Shows

- rectangular items such as sword and shield
- complex shapes through an occupied-cell mask, such as axe and scythe
- `PlacementInventoryDataBinding` for item + anchor + orientation data
- `IItemPlacementShapeProvider` on the item adapter
- item rotation during drag through `RotateDragAction`
- occupied-cell preview and placement checks through topology
- drawing items over slots
- compatibility with regular inventories and strategies
- bonus: an Editor for configuring item masks

## How It Is Structured

Item data:

- `ShapedItemExampleSO.cs`
- `ComplexShapedItemExampleSO.cs`
- `SO/RectShapes/*`
- `SO/ComplexShapes/*`

Binding and adapter:

- `Adapters/ShapedItemAdapter.cs`
- `DataBindings/ShapedItemsInventoryDataBinding.cs`

Editor utility:

- `Editor/ComplexShapedItemExampleSOEditor.cs`

## How It Works

Transfer and rotation:

1. During transfer, the system stores shape, anchor slot, and rotation in `DragEntry`.
2. `Q` and `E` in the demo profile call `RotateDragAction` with rotation steps `-1` and `1`, which correspond to -90 and +90 in the grid.
3. After a successful transfer, the binding writes item, anchor, and rotation back into the list.

## Rectangular And Complex Shapes

`ShapedItemExampleSO` describes a regular rectangle through `Width` and `Height`. Its
`GetOccupiedCells()` returns all cells inside the bounding box.

`ComplexShapedItemExampleSO` adds the `_cells` bool mask. It allows L-, T-, cross- and
other shapes where some cells inside the bounding box are empty. If the mask becomes
empty by mistake, the item falls back to the base rectangle to avoid an item with no
footprint.

`ComplexShapedItemExampleSOEditor` draws a clickable grid over the item icon: enabled
cells are occupied, disabled cells are empty.

## When To Use This Example

- items must occupy several cells in an inventory grid
- you need to store item position and orientation in your data
- you need item rotation during drag
- you need custom shapes, not only rectangles
