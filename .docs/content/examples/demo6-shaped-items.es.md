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

Este ejemplo muestra items que no ocupan un solo slot, sino un footprint de varias
celdas en un inventario de grid.

## Qué muestra la demo

- items rectangulares como espada y escudo
- formas complejas mediante una máscara de celdas ocupadas, como hacha y guadaña
- `PlacementInventoryDataBinding` para datos item + anchor + orientation
- `IItemPlacementShapeProvider` en el adapter del item
- rotación del item durante drag mediante `RotateDragAction`
- preview de celdas ocupadas y comprobación de placement mediante topología
- dibujo de items sobre los slots
- compatibilidad con inventarios y estrategias normales
- bonus: Editor para configurar máscaras de items

## Cómo está estructurada

Datos de item:

- `ShapedItemExampleSO.cs`
- `ComplexShapedItemExampleSO.cs`
- `SO/RectShapes/*`
- `SO/ComplexShapes/*`

Binding y adapter:

- `Adapters/ShapedItemAdapter.cs`
- `DataBindings/ShapedItemsInventoryDataBinding.cs`

Utilidad de Editor:

- `Editor/ComplexShapedItemExampleSOEditor.cs`

## Cómo funciona

Transferencia y rotación:

1. Durante la transferencia, el sistema guarda forma, slot ancla y rotación en `DragEntry`.
2. `Q` y `E` en el perfil de la demo llaman a `RotateDragAction` con pasos de rotación `-1` y `1`, que en la grid corresponden a -90 y +90.
3. Tras una transferencia exitosa, el binding escribe item, anchor y rotación de vuelta a la lista.

## Formas rectangulares y complejas

`ShapedItemExampleSO` describe un rectángulo normal mediante `Width` y `Height`. Su
`GetOccupiedCells()` devuelve todas las celdas dentro del bounding box.

`ComplexShapedItemExampleSO` añade la máscara bool `_cells`. Permite formas L, T, cruz y
otras donde algunas celdas dentro del bounding box están vacías. Si la máscara queda
vacía por error, el item vuelve al rectángulo base para evitar un item sin footprint.

`ComplexShapedItemExampleSOEditor` dibuja una grid clicable sobre el icono del item: las
celdas activadas cuentan como ocupadas, las desactivadas como vacías.

## Cuándo usar este ejemplo como base

- los items deben ocupar varias celdas en un inventory grid
- necesitas guardar posición y orientación del item en tus datos
- necesitas rotación de items durante drag
- necesitas formas personalizadas, no solo rectángulos
