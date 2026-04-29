---
Last Updated: 2026-04-29
---

# Shaped Items Architecture Plan

Поддержка предметов произвольной формы (Diablo / Tarkov-style) в существующей системе drag & drop без параллельной кодовой ветки для grid-режима.

## Ведущие принципы

1. **Без альтернативных путей.** Не плодить `IGridInventoryStrategy` рядом с `IInventoryStrategy`, `IPlacementRule` рядом с `IDragRule`, optional grid-поля рядом с обычными. Расширяем общие контракты так, чтобы slot-инвентарь стал вырожденным случаем placement-инвентаря.
2. **Footprint — свойство предмета.** Форма едет вместе с предметом между инвентарями. Реализуется опциональным интерфейсом, без правки `IInventoryItem`.
3. **Occupancy interpretation — свойство инвентаря.** Каждый инвентарь сам решает, как трактовать placement request. Slot-инвентарь занимает 1 слот вне зависимости от footprint, grid — все covered cells.
4. **Источник истины — placement.** Stack хранится на `Placement`, а не на каком-либо слоте. Слот ничего не знает про anchor/follower-роли — он лишь спрашивает инвентарь, какой placement его накрывает.
5. **Cross-inventory transfer — первоклассный сценарий.** Перетаскивание shaped item между grid- и slot-инвентарями должно работать без специального кода: footprint сохраняется как метаданные предмета, целевой инвентарь интерпретирует placement по своим правилам.

## Целевая модель данных

```
IShapedItem : IInventoryItem
    int FootprintWidth  { get; }
    int FootprintHeight { get; }
    // Phase 4: bool[,] ShapeMask { get; }

Placement
    int          Id
    Vector2Int   AnchorCell        // координата, не «специальный слот»
    Orientation  Orientation       // 0 / 90 в Phase 1
    Footprint    Footprint         // (W, H), для не-IShapedItem = (1, 1)
    ItemStack    Stack             // источник истины
    IReadOnlyList<int> CoveredIndices  // производное от anchor + footprint + orientation

UniversalInventory
    IReadOnlyList<Placement>     Placements          // источник истины
    Dictionary<int, int>         cellToPlacementId   // производное (occupancy map)
    GridTopology?                Grid                // null → линейный slot inventory
```

`AnchorCell` — это геометрическая координата placement-а (нужна для расчёта bounds, сериализации и `candidateAnchor = pointerCell - grabOffset`), а не привилегированный слот. Все слоты в `CoveredIndices` идентичны по роли.

`slot.ItemStack` становится тонким акцессором: `inventory.GetPlacementAt(this)?.Stack`. Это устраняет дублирование источника истины и делает 1×1 в slot-инвентаре тривиальным частным случаем placement-модели.

### Инварианты

- Один `Placement` ↔ один `ItemStack`. Один `ItemStack` живёт ровно в одном placement-е.
- Shaped items (`IShapedItem` с footprint > 1×1) всегда `Stack.Count == 1` в Phase 1–3. Стекуемые shaped items — Phase 4.
- Grid inventory работает только с `SlotManagementType.Fixed`. Dynamic несовместим с фиксированной NxM-топологией. Динамический рост grid — Phase 4+.
- Координаты grid — row-major: `index = y * columns + x`.

## Хранение и сериализация

Сериализуется список `PlacementData`, occupancy перестраивается при загрузке.

```
PlacementData
    int    AnchorIndex
    string ItemId
    int    Count          // Phase 1–3: всегда 1
    int    Orientation    // 0 или 90
```

Состояние follower-cells не сериализуется — оно полностью производно от placement-списка. Это снимает риск рассинхронизации.

## Контракты, которые расширяются (а не дублируются)

### `DragContext`

Всегда несёт placement-метаданные. Для 1×1 они тривиальны, поэтому существующие сценарии не ломаются.

```
DragContext
    BaseSlot     SourceSlot
    Placement    SourcePlacement     // включая Stack, footprint, orientation
    Vector2Int   GrabOffset          // (0,0) для 1×1
    BaseSlot     TargetSlot          // слот под курсором
```

`GrabOffset` — смещение от `AnchorCell` placement-а до cell, за который пользователь схватил предмет. Без него shaped item «прыгает» при захвате не за угловую ячейку.

### `IInventoryStrategy`

Единый контракт, принимающий `PlacementRequest`, а не одиночный `targetIndex`. Существующие стратегии (`UniqueItemStrategy`, `StackableItemStrategy`) продолжают работать: они трактуют любой запрос как 1-cell, footprint игнорируют для занятости (предмет любого размера занимает 1 слот в slot-инвентаре).

```
PlacementRequest
    ItemStack    Stack
    int          AnchorIndex
    Orientation  Orientation
    Footprint    Footprint   // из IShapedItem, или (1,1)
```

### `IDragRule`

Контекст правила обогащается placement-полями (anchor, covered, orientation). Старые правила, не читающие новые поля, продолжают работать на 1×1. Никакого отдельного `IPlacementRule`.

### `InventoryTransferResult`

`AnchorSlot` и `CoveredSlots` — первичные поля, не optional-расширения. Для 1×1: `AnchorSlot == TargetSlot`, `CoveredSlots == [TargetSlot]`. Существующие подписчики не видят разницы.

### `InventorySnapshot`

Snapshot/rollback всегда работает в терминах placement-транзакций. Для 1×1 — транзакция с одной ячейкой. Это убирает развилку «обычный rollback vs grid rollback».

## Cross-inventory сценарии

Переносы между grid- и slot-инвентарями работают по тем же контрактам:

- **grid → slot.** `DragContext` несёт footprint предмета. Целевой slot-инвентарь при `CanAccept`/`TryAdd` интерпретирует placement как 1-cell (своя политика occupancy). Footprint сохраняется как item-метаданные; если предмет позже перенесут обратно в grid, он снова развернётся.
- **slot → grid.** Footprint предмета равен (1,1), grid занимает одну ячейку.
- **grid → grid (разные размеры).** Тот же планировщик, та же валидация по occupancy целевого инвентаря.

Highlight рассчитывается как **view-query к target-инвентарю**, без глобального `if (shaped)`:

```
candidateAnchor = pointerCell - dragContext.GrabOffset
highlightSlots  = targetInventory.GetCoveredCells(candidateAnchor, footprint, orientation)
```

Slot-инвентарь возвращает `[pointerSlot]`. Grid возвращает covered cells. Source-инвентарь подсвечивает свой `SourcePlacement`. Один и тот же код для обоих случаев.

**Политика slot-инвентаря для shaped items** — параметр инвентаря:

- `Accept` (по умолчанию) — принимает с collapse в 1 слот, footprint сохраняется в предмете.
- `Reject` — отклоняет shaped items явной политикой.
- Альтернатива «занимать N последовательных слотов» намеренно не поддерживается; такой инвентарь надо моделировать как grid с шириной 1.

## Drag за любой covered cell

Пайплайн при ЛКМ на слоте:

```
1. pressedSlot = слот под курсором
2. placement   = inventory.GetPlacementAt(pressedSlot)   // null → нет драга
3. grabOffset  = pressedSlot.cell - placement.AnchorCell
                  // (0,0) для slot inventory автоматически
4. DragContext { SourceSlot=pressedSlot, SourcePlacement=placement,
                 GrabOffset=grabOffset, ... }
```

`ItemStack` достаётся из `placement.Stack`. Никаких `ResolveAnchorSlot`/`IsFollower`/`IsAnchor` API в слоте — слот не знает про роли. 1×1 проходит через ту же ветку (placement тривиальный).

## Визуал размещённого предмета

**Иконка рендерится отдельной overlay-панелью**, не дочерним `Image` slot-а. Slot-RectTransform может перекрывать соседей, поэтому растянутый Image внутри slot-а — путь к багам с clipping/masking/sibling order.

```
PlacementOverlay (sibling сетки слотов)
    raycastTarget = false
    pickingMode   = Ignore
    Image      (иконка предмета)
    bounds     (рассчитываются от corner-слотов через RectTransformUtility,
               пересчёт по RectTransformDimensionsChange — не каждый кадр)
```

Один overlay на placement, пулится. Drag preview — тот же overlay, отвязанный от инвентаря и привязанный к курсору с учётом `GrabOffset`.

Сами слоты получают независимый covered-визуал (dim/tint/border) — полезно для accessibility и empty-state, когда иконка временно скрыта.

Hover/selection/context menu, попавшие на любую covered cell, делегируют запрос инвентарю и работают по placement-у целиком.

## Зафиксированные ограничения Phase 1–3

Эти ограничения — не временные «todo», а часть scope-а первого релиза. Каждое описано в плане явно, чтобы planner/executor не пытались поддержать их «частично».

- **Shaped items не стекуются.** `Stack.Count > 1` для shaped item отвергается на этапе валидации placement.
- **Swap shaped items отключён.** При дропе shaped item на occupied cell (или при пересечении footprint-а с существующими placement-ами) — reject через стандартный `BlockedTargetResolution.Reject()`.
- **Batch drag с shaped items отключён.** Поиск нескольких shaped placements — комбинаторная задача, отдельная фича.
- **Auto-transfer для shaped items отключён.** `AutoTransferAnimationStrategy` работает на 1-cell уровне; для placement нужна отдельная анимационная подсистема.
- **Auto-sort для grid inventory отключён.** 2D bin packing — отдельная задача, в Phase 1–3 не входит. Фильтрация (hide/show) работает: скрытый предмет всё ещё занимает ячейки.
- **Произвольные shape masks не поддерживаются.** Только прямоугольные footprint, ротация 0/90.

Все эти отказы — стандартный `Reject` через существующий drop policy / `BlockedTargetResolverBase`. Никаких новых ветвей в `TransferPlanner` для них не вводится.

## Этапы реализации

### Phase 1. Foundation (без drag-pipeline)

Цель: построить placement-модель и убедиться, что 1×1 в slot-инвентаре через неё работает идентично текущему коду.

1. Ввести `IShapedItem`.
2. Ввести `Placement` с инвариантом «stack живёт здесь, не на слоте».
3. Перевести `slot.ItemStack` на акцессор `inventory.GetPlacementAt(slot)?.Stack`. Это самая болезненная миграция — выполнять её первой.
4. Расширить `UniversalInventory` API: `GetPlacementAt`, `GetCoveredCells`, `CanPlace(PlacementRequest)`, `TryPlace`, `RemovePlacement`. Slot-инвентарь реализует их тривиально.
5. Добавить `GridTopology` (columns, rows, row-major mapping) как опциональное поле инвентаря. `null` → линейный slot inventory.
6. Реализовать occupancy map как производное состояние от `Placements`.
7. Сериализация: `List<PlacementData>` вместо «slot stores stack».
8. Валидация: grid + Dynamic = ошибка инициализации; shaped item + Count > 1 = отказ при placement.
9. Unit/play-mode тесты на API: создание grid, программное размещение, occupancy, сериализация → загрузка.

Без визуальных изменений и без изменений в drag pipeline. Существующие игры не должны заметить разницы.

### Phase 2. Drag pipeline integration

Цель: shaped placement становится first-class в transfer pipeline.

1. Расширить `DragContext` полями `SourcePlacement`, `GrabOffset`. Инициализаторы DragContext выставляют тривиальные значения для 1×1 — старые сценарии работают.
2. Перевести `IInventoryStrategy.TryAdd` / `CanAccept` на `PlacementRequest`. Существующие стратегии адаптируются с тривиальной интерпретацией.
3. `TransferPlanner` строит placement-aware allocations через `inventory.CanPlace(PlacementRequest)`. Никаких grid-specific ветвей.
4. `TransferPlanExecutor` применяет plan как placement-транзакцию (освободить старый placement целиком, занять новый целиком, события — раз на placement).
5. `InventorySnapshot` захватывает/восстанавливает placement state.
6. `InventoryTransferResult.AnchorSlot`/`CoveredSlots` — первичные поля.
7. `IDragRule` контекст обогащается placement-полями.
8. Reject-политики для swap/batch/auto-transfer shaped items через стандартный drop policy.

Cross-inventory grid ↔ slot должен работать после Phase 2 на уровне core-логики, до UI-визуализации.

### Phase 3. UI / UX

1. `PlacementOverlay` как sibling-панель сетки. Pooling, пересчёт по `RectTransformDimensionsChange`.
2. Covered-state визуал на самих слотах (dim/tint).
3. Drag preview через тот же overlay-механизм с учётом `GrabOffset`.
4. Hover/selection/context menu/tooltip делегируют через `inventory.GetPlacementAt`. Tooltip над любой covered cell показывает данные placement-а.
5. Rotation input (поворот предмета во время drag).
6. Highlight на target-инвентаре через `targetInventory.GetCoveredCells(candidateAnchor, footprint, orientation)`.

### Phase 4. Advanced (вне scope текущего плана)

- Произвольные shape masks (`bool[,]`).
- Стекуемые shaped items.
- Swap shaped items с multi-item displacement.
- Batch drag с shaped items.
- Auto-rotation на placement.
- Auto-sort для grid (2D bin packing).
- Полноценная анимация auto-transfer для placement.
- Динамический рост grid (добавление строк/колонок).
- Rule presets для equipment-like grid инвентарей.

## Что нельзя сломать при миграции

- Существующие slot-only сценарии (демо-инвентари, equipment, hotbar) должны работать без изменений в их пользовательском коде.
- Совместимость context menu, batch drag (для не-shaped), world drop, rollback-safe transfer.
- Текущий rule pipeline.

Способ обеспечить это: миграция `slot.ItemStack` на акцессор делается в Phase 1 как механическая замена под капотом, публичный API сохраняется. Все остальные расширения контрактов (`PlacementRequest`, `DragContext`, `InventoryTransferResult`) делаются обратно совместимо за счёт тривиальных значений по умолчанию для 1×1.

## Naming

- `Placement` — единица размещения (с stack, footprint, orientation, anchor cell).
- `AnchorCell` — координата (не слот).
- `CoveredCell` / `IsCellCovered(slot)` — слот, попадающий под placement. В коде нет «anchor slot» как сущности.
- `Footprint` — (width, height).
- `Orientation` — `Rot0` / `Rot90` в Phase 1.

## Сводка ключевых решений

| Решение | Значение |
|---|---|
| Storage модель | Stack живёт на `Placement`, не на слоте |
| Slot роли | Нет anchor/follower; все covered cells симметричны |
| `slot.ItemStack` | Акцессор через `inventory.GetPlacementAt(slot)?.Stack` |
| Footprint | Опциональный `IShapedItem`, не трогает `IInventoryItem` |
| Стратегии | Один `IInventoryStrategy` с `PlacementRequest`, никакого `IGridInventoryStrategy` |
| Rules | Один `IDragRule` с placement-context, никакого `IPlacementRule` |
| `DragContext` | Всегда несёт `SourcePlacement` + `GrabOffset` |
| `InventoryTransferResult` | `AnchorSlot` + `CoveredSlots` — первичные поля |
| Координаты grid | row-major (`index = y * columns + x`) |
| Slot management | Grid → только Fixed |
| Shaped Count | Всегда 1 в Phase 1–3 |
| Cross-inventory | Footprint — свойство предмета, occupancy — свойство инвентаря |
| Slot inventory + shaped | Default Accept (collapse в 1 слот), опционально Reject |
| Highlight | View-query `targetInventory.GetCoveredCells(...)` |
| Visual | Отдельная `PlacementOverlay` панель, `raycastTarget=false` |
| Swap / Batch / Auto-transfer для shaped | Reject через стандартный drop policy в Phase 1–3 |
