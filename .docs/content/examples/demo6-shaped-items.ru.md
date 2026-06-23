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

Этот пример показывает предметы, которые занимают не один слот, а footprint из нескольких клеток grid-инвентаря.

## Что показывает демо

- прямоугольные предметы вроде меча и щита
- сложные формы через маску занятых клеток - топор и коса
- `PlacementInventoryDataBinding` для данных вида item + anchor + orientation
- `IItemPlacementShapeProvider` на adapter-е предмета
- поворот предмета во время drag через `RotateDragAction`
- preview занятых клеток и проверку размещения по топологии
- рисовка предметов поверх слотов
- совместимость с обычными инвентарями и стратегиями
- бонус: Editor для настройки маски предметов

## Как устроено

Данные предметов:

- `ShapedItemExampleSO.cs`
- `ComplexShapedItemExampleSO.cs`
- `SO/RectShapes/*`
- `SO/ComplexShapes/*`

Binding и adapter:

- `Adapters/ShapedItemAdapter.cs`
- `DataBindings/ShapedItemsInventoryDataBinding.cs`

Editor утилита:

- `Editor/ComplexShapedItemExampleSOEditor.cs`

## Как работает

Перенос и поворот:

1. Во время переноса система хранит форму, якорьный слот и поворот в `DragEntry`.
2. Клавиши `Q` и `E` в demo-профиле вызывают `RotateDragAction` с шагами поворота `-1` и `1` что в сетке соответсвует -90 и +90.
3. После успешного переноса binding записывает предмет, якорь и поворот в список.

## Прямоугольные и сложные формы

`ShapedItemExampleSO` описывает обычный прямоугольник через `Width` и `Height`. Его `GetOccupiedCells()` возвращает все клетки внутри bounding box.

`ComplexShapedItemExampleSO` добавляет bool-маску `_cells`. Она позволяет сделать L-, T-, cross- и другие формы, где часть клеток bounding box пустая. Если маска случайно стала пустой, предмет fallback-ится к базовому прямоугольнику, чтобы не получить предмет без footprint-а.

`ComplexShapedItemExampleSOEditor` рисует кликабельную сетку поверх иконки предмета: включённые клетки считаются занятыми, выключенные - пустыми.

## Когда брать этот пример за основу

- предметы должны занимать несколько клеток в inventory grid
- нужно хранить позицию и ориентацию предмета в своих данных
- нужен поворот предметов во время drag
- нужны нестандартные формы, а не только прямоугольники
