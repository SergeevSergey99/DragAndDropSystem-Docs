---
Last Updated: 2026-04-30
---

# Shaped Items Architecture Plan

Поддержка предметов произвольной формы (Diablo / Tarkov-style) в существующей системе drag & drop без параллельной кодовой ветки для grid-режима.

## Ведущие принципы

1. **Без альтернативных путей.** Не плодить `IGridInventoryStrategy` рядом с `IInventoryStrategy`, `IPlacementRule` рядом с `IDragRule`, optional grid-поля рядом с обычными. Расширяем общие контракты так, чтобы slot-инвентарь стал вырожденным случаем placement-инвентаря.
2. **Footprint — свойство item adapter-а.** Форма едет вместе с предметом между инвентарями. Реализуется опциональным интерфейсом на `IItemAdapter`-совместимом адаптере, без правки базового `IItemAdapter`.
3. **Occupancy interpretation — свойство инвентаря.** Каждый инвентарь сам решает, как трактовать placement request. Slot-инвентарь занимает 1 слот вне зависимости от footprint, grid — все covered cells.
4. **Источник истины — placement.** Stack хранится на `Placement`, а не на каком-либо слоте. Слот ничего не знает про anchor/follower-роли — он лишь спрашивает инвентарь, какой placement его накрывает.
5. **Cross-inventory transfer — первоклассный сценарий.** Перетаскивание shaped item между grid- и slot-инвентарями должно работать без специального кода: footprint сохраняется как метаданные предмета, целевой инвентарь интерпретирует placement по своим правилам.

## Целевая модель данных

```
IItemFootprintProvider
    Footprint Footprint { get; }
    // Footprint — struct, в Phase 1 хранит (W, H);
    // в Phase 4 расширяется shape mask / covered offsets без правки интерфейса.

Placement
    int          Id
    Vector2Int   AnchorCell        // координата, не «специальный слот»
    Orientation  Orientation       // 0 / 90 в Phase 1
    Footprint    Footprint         // (W, H), для adapter-а без IItemFootprintProvider = (1, 1)
    ItemStack    Stack             // источник истины
    IReadOnlyList<int> CoveredIndices  // производное от anchor + footprint + orientation

UniversalInventory
    IReadOnlyList<Placement>     Placements          // источник истины
    Dictionary<int, int>         cellToPlacementId   // производное (occupancy map)
    GridTopology?                Grid                // null → линейный slot inventory
```

`IItemFootprintProvider` — optional interface, который обычно реализует тот же object, что и `IItemAdapter`. Footprint резолвится из `stack.PrimaryAdapter`; если адаптер не реализует интерфейс, используется `(1, 1)`. В проекте нет `IInventoryItem`, поэтому shaped-контракт не должен ссылаться на него.

`AnchorCell` — это геометрическая координата placement-а (нужна для расчёта bounds, сериализации и `candidateAnchor = pointerCell - grabOffset`), а не привилегированный слот. Все слоты в `CoveredIndices` идентичны по роли. Для slot-инвентаря `AnchorCell` синтезируется тривиально (`(slotIndex, 0)`) и фактически не используется в логике, так как `GrabOffset` для slot-инвентаря всегда `(0, 0)` и highlight всегда `[anchor]`. Это сохраняет единый Placement-контракт для обоих режимов без специальных полей.

`slot.Stack` становится compatibility facade: `inventory.GetPlacementAt(this)?.Stack ?? ItemStack.Empty()`. Публичная семантика остаётся близкой к текущей: пустой слот даёт empty stack, а различие «нет placement» vs «placement есть, но stack пуст» проверяется явно через `inventory.GetPlacementAt(slot)` / `TryGetPlacementAt`. Phase 1 всё равно включает аудит `slot.Stack == null` / `slot.Stack?.IsEmpty` сайтов и нормализацию проверок, потому что после миграции source of truth меняется. `slot.SetStack` / `slot.Clear` должны перейти на inventory placement API, а не напрямую менять поле в слоте. Это самая большая миграция: сейчас drag, стратегии, snapshots, UI, filter/sort, context menu и tests активно читают/пишут `BaseSlot.Stack`.

### Инварианты

- Один `Placement` ↔ один `ItemStack`. Один `ItemStack` живёт ровно в одном placement-е.
- Shaped items (`IItemFootprintProvider` с footprint > 1×1) всегда `Stack.Count == 1` в Phase 1–3. Стекуемые shaped items — Phase 4.
- Grid inventory работает только с `SlotManagementType.Fixed`. Dynamic несовместим с фиксированной NxM-топологией. Динамический рост grid — Phase 4+.
- Координаты grid — row-major: `index = y * columns + x`.

## Хранение и сериализация

Сериализуется список `PlacementData`, occupancy перестраивается при загрузке. Это runtime/UI persistence, а не универсальная замена внешнему data model.

```
PlacementData
    int    AnchorIndex
    string ItemKey       // opaque key, выдаёт DataBinding / adapter persistence layer
    int    Count          // Phase 1–3: всегда 1
    int    Orientation    // 0 или 90
```

Состояние follower-cells не сериализуется — оно полностью производно от placement-списка. Это снимает риск рассинхронизации.

Важно: одного `ItemId` недостаточно для текущего проекта. `ItemStack` хранит adapter instances, а демо и bindings могут работать с ScriptableObject, model-adapter, container instances, торговыми item model-ами и converter-ами. Для slot-only binding-ов старый путь остаётся рабочим; для grid/shaped persistence нужен placement-aware binding contract:

```
IPlacementDataBinding
    IEnumerable<PlacementData<TData>> GetPlacements()
    IItemAdapter CreateAdapter(TData data)
    string GetPersistenceKey(IItemAdapter adapter)
    void AddPlacementData(PlacementCommitContext context)
    void RemovePlacementData(PlacementCommitContext context)
```

Конкретная форма generic/non-generic API — открытый вопрос, который нужно закрыть до старта реализации Phase 1, потому что Phase 1 уже включает reload/snapshot/persistence tests. Сейчас набросок несимметричен (save идёт через `IItemAdapter → string`, load через `TData → IItemAdapter`), что заставит binding-имплементации держать две внутренние карты. Допустимые варианты: (a) типизировать обе стороны через `TData` и сделать contract generic; (b) обе стороны через opaque persistence key; (c) явно разделить `IPlacementSaveBinding` и `IPlacementLoadBinding`. Главное, что план признаёт: внешний data layer, а не `UniversalInventory`, отвечает за восстановление доменных item instances.

## Контракты, которые расширяются (а не дублируются)

### `DragEntry` / `DragContext`

Placement-метаданные живут на `DragEntry`, потому что текущая модель поддерживает batch drag через `DragContext.Entries`. Для 1×1 они тривиальны, поэтому существующие сценарии не ломаются.

```
DragEntry
    ItemStack    Stack
    BaseSlot     SourceSlot        // pressed / source covered cell
    IInventory   SourceInventory
    Placement    SourcePlacement     // включая Stack, footprint, orientation
    Vector2Int   GrabOffset          // (0,0) для 1×1

DragContext
    BaseSlot     TargetSlot          // слот под курсором
    IInventory   TargetInventory
```

`GrabOffset` — смещение от `AnchorCell` placement-а до cell, за который пользователь схватил предмет. Без него shaped item «прыгает» при захвате не за угловую ячейку.

### `IInventoryStrategy`

Единый контракт, принимающий `PlacementRequest`, а не одиночный `targetIndex`. Существующие стратегии (`UniqueItemStrategy`, `StackableItemStrategy`) продолжают работать: они трактуют любой запрос как 1-cell, footprint игнорируют для занятости (предмет любого размера занимает 1 слот в slot-инвентаре).

```
PlacementRequest
    ItemStack    Stack
    int          AnchorIndex
    Orientation  Orientation
    Footprint    Footprint   // из IItemFootprintProvider, или (1,1)
```

Для миграции не обязательно одномоментно ломать все strategy interfaces. Можно ввести placement API на уровне `UniversalInventory` (`CanPlace`, `TryPlace`, `RemovePlacement`) и адаптировать существующие стратегии через compatibility layer, пока `StackableItemStrategy` / `UniqueItemStrategy` не будут переписаны на `PlacementRequest`.

### `IDragRule`

Контекст правила обогащается placement-полями (anchor, covered, orientation), доступными через `DragEntry` и target placement query. Старые правила, не читающие новые поля, продолжают работать на 1×1. Никакого отдельного `IPlacementRule`.

### `InventoryTransferResult`

`AnchorSlot` и `CoveredSlots` — первичные поля placement commit-а. Для 1×1: `AnchorSlot == TargetBaseSlot`, `CoveredSlots == [TargetBaseSlot]`. `TargetBaseSlot` сохраняется как backward-compatible alias на anchor/resolved target для существующих подписчиков.

`InventoryItemEventContext` тоже должен получить placement metadata (`PlacementId`, `AnchorIndex`, `CoveredIndices`, `Orientation`). Иначе `SlotIndexedInventoryDataBinding` сможет обновить только anchor slot и потеряет информацию о footprint.

### `InventorySnapshot`

Snapshot/rollback всегда работает в терминах placement-транзакций. Для 1×1 — транзакция с одной ячейкой. Это убирает развилку «обычный rollback vs grid rollback».

## Cross-inventory сценарии

Переносы между grid- и slot-инвентарями работают по тем же контрактам:

- **grid → slot.** `DragEntry` несёт footprint предмета через `SourcePlacement` / item adapter metadata. Целевой slot-инвентарь при `CanAccept`/`TryAdd` интерпретирует placement как 1-cell (своя политика occupancy). Footprint сохраняется как item-метаданные; если предмет позже перенесут обратно в grid, он снова развернётся.
- **slot → grid.** Footprint резолвится из item adapter-а. Если предмет был collapsed в slot-инвентаре, при переносе обратно в grid он снова занимает свой реальный footprint. Только обычные non-shaped items имеют footprint `(1,1)`.
- **grid → grid (разные размеры).** Тот же планировщик, та же валидация по occupancy целевого инвентаря.

Highlight рассчитывается как **view-query к target-инвентарю**, без глобального `if (shaped)`:

```
candidateAnchor = pointerCell - entry.GrabOffset
highlightSlots  = targetInventory.GetCoveredCells(candidateAnchor, footprint, orientation)
```

Slot-инвентарь возвращает `[pointerSlot]`. Grid возвращает covered cells. Source-инвентарь подсвечивает `entry.SourcePlacement`. Один и тот же код для обоих случаев.

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
4. DragEntry { SourceSlot=pressedSlot, SourcePlacement=placement,
               GrabOffset=grabOffset, ... }
```

`ItemStack` достаётся из `placement.Stack`, а metadata записывается в `DragEntry`. Никаких `ResolveAnchorSlot`/`IsFollower`/`IsAnchor` API в слоте — слот не знает про роли. 1×1 проходит через ту же ветку (placement тривиальный).

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

Filter/sort для grid должен быть ограничен. Текущий `FilterSortController` может скрывать слоты, выключать interactivity и менять sibling order отдельных cells; для фиксированной grid-топологии это ломает соответствие `index = y * columns + x`. В Phase 1–3 для grid разрешены только режимы, которые не меняют геометрию ячеек: dim/covered overlay/filter marker. Hide и MoveToEnd должны быть disabled или заменены grid-aware представлением.

## Зафиксированные ограничения Phase 1–3

Эти ограничения — не временные «todo», а часть scope-а первого релиза. Каждое описано в плане явно, чтобы planner/executor не пытались поддержать их «частично».

- **Shaped items не стекуются.** `Stack.Count > 1` для shaped item отвергается на этапе валидации placement и drag-start.
- **Swap shaped items отключён.** При дропе shaped item на occupied cell (или при пересечении footprint-а с существующими placement-ами) — reject через стандартный `BlockedTargetResolution.Reject()`.
- **Batch drag с shaped items отключён.** Поиск нескольких shaped placements — комбинаторная задача, отдельная фича.
- **Auto-transfer для shaped items отключён.** `AutoTransferAnimationStrategy` работает на 1-cell уровне; для placement нужна отдельная анимационная подсистема.
- **Auto-sort для grid inventory отключён.** 2D bin packing — отдельная задача, в Phase 1–3 не входит. Фильтрация (hide/show) работает: скрытый предмет всё ещё занимает ячейки.
- **Произвольные shape masks не поддерживаются.** Только прямоугольные footprint, ротация 0/90.

Все эти отказы должны выглядеть для внешнего API как стандартный `Reject` через существующий drop policy / `BlockedTargetResolverBase`. Внутри planner-а допустима явная placement-aware проверка capability, если она нужна для корректного failure reason и чтобы не допустить частичную поддержку shaped cases.

## Этапы реализации

### Phase 1. Foundation (без shaped drag)

Цель: построить placement-модель и убедиться, что 1×1 в slot-инвентаре через неё работает идентично текущему коду.

1. Ввести `IItemFootprintProvider` как optional adapter interface.
2. Ввести `Placement` с инвариантом «stack живёт здесь, не на слоте».
3. Перевести `slot.Stack` на compatibility facade через `inventory.GetPlacementAt(slot)?.Stack ?? ItemStack.Empty()`. Это самая болезненная миграция — выполнять её первой, с сохранением публичных `SetStack` / `Clear` как thin wrappers поверх placement API.
4. Расширить `UniversalInventory` API: `GetPlacementAt`, `GetCoveredCells`, `CanPlace(PlacementRequest)`, `TryPlace`, `RemovePlacement`. Slot-инвентарь реализует их тривиально.
5. Добавить `GridTopology` (columns, rows, row-major mapping) как опциональное поле инвентаря. `null` → линейный slot inventory.
6. Реализовать occupancy map как производное состояние от `Placements`.
7. Снимки rollback (`InventorySnapshot`) перевести на placement-транзакции как единственное внутреннее представление. Никакой второй ветки «slot snapshot для обратной совместимости» — для 1×1 placement-транзакция возвращает тот же observable результат, что и старый slot snapshot. Snapshot payload при этом расширяется placement state (anchor/orientation/covered cells), а старое slot-observable поведение сохраняется для публичных 1×1 сценариев.
8. DataBinding: оставить текущие slot/list bindings рабочими через facade; добавить отдельный placement-aware binding contract для grid persistence.
9. Валидация: grid + Dynamic = ошибка инициализации; shaped item + Count > 1 = отказ при placement.
10. Unit/play-mode тесты на API: создание grid, программное размещение, occupancy, snapshot restore, binding reload для 1×1, serialization/persistence hook для placement.

Phase 1 не включает полноценный shaped drag/overlay, но не является «без изменений drag/UI pipeline» в буквальном смысле: нужно сохранить текущий slot-only UX поверх новой storage model. Существующие игры не должны заметить разницы.

### Phase 2. Drag pipeline integration

Цель: shaped placement становится first-class в transfer pipeline.

1. Расширить `DragEntry` полями `SourcePlacement`, `GrabOffset`; `DragContext` остаётся контейнером entries + target state. Инициализаторы выставляют тривиальные значения для 1×1.
2. Перевести placement planning с `PlannedSlotAllocation(BaseSlot, amount)` на `PlannedPlacementAllocation(PlacementRequest, amount/resolvedPlacement)`. Для 1×1 это даёт тот же результат, но не привязывает core к одному target slot.
3. Перевести `IInventoryStrategy.TryAdd` / `CanAccept` на `PlacementRequest` или закрыть старые стратегии compatibility adapter-ом. Существующие стратегии трактуют любой запрос как 1-cell.
4. `TransferPlanner` строит placement-aware allocations через `inventory.CanPlace(PlacementRequest)`. Допустимы internal capability checks для reject случаев shaped/swap/batch/auto-transfer, но не отдельный публичный grid pipeline.
5. `TransferPlanExecutor` применяет plan как placement-транзакцию (освободить старый placement целиком, занять новый целиком, события — раз на placement).
6. `InventorySnapshot` захватывает/восстанавливает placement state.
7. `InventoryTransferResult.AnchorSlot`/`CoveredSlots` — первичные поля.
8. `IDragRule` контекст обогащается placement-полями.
9. Reject-политики для swap/batch/auto-transfer shaped items через стандартный drop policy.

Cross-inventory grid ↔ slot должен работать после Phase 2 на уровне core-логики, до UI-визуализации.

### Phase 3. UI / UX

1. `PlacementOverlay` как sibling-панель сетки. Pooling, пересчёт по `RectTransformDimensionsChange`.
2. Covered-state визуал на самих слотах (dim/tint).
3. Drag preview через тот же overlay-механизм с учётом `GrabOffset`.
4. Hover/selection/context menu/tooltip делегируют через `inventory.GetPlacementAt`. Tooltip над любой covered cell показывает данные placement-а.
5. Rotation input (поворот предмета во время drag).
6. Highlight на target-инвентаре через `targetInventory.GetCoveredCells(candidateAnchor, footprint, orientation)`.

### Phase 4. Advanced (вне scope текущего плана)

- Произвольные shape masks / covered offsets (конкретное хранение выбрать отдельно; не фиксировать `bool[,]` как API).
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

Способ обеспечить это: миграция `slot.Stack` на facade делается в Phase 1 как совместимый слой под капотом, публичный API сохраняется. Все остальные расширения контрактов (`PlacementRequest`, `DragEntry`, `DragContext`, `InventoryTransferResult`) делаются обратно совместимо за счёт тривиальных значений по умолчанию для 1×1.

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
| `slot.Stack` | Compatibility facade через `inventory.GetPlacementAt(slot)?.Stack ?? ItemStack.Empty()` |
| Footprint | Опциональный `IItemFootprintProvider` на item adapter-е, отдаёт `Footprint` struct (Phase 4 расширяет mask/covered offsets без правки интерфейса) |
| Стратегии | Один `IInventoryStrategy` с `PlacementRequest`, никакого `IGridInventoryStrategy` |
| Rules | Один `IDragRule` с placement-context, никакого `IPlacementRule` |
| `DragEntry` | Всегда несёт `SourcePlacement` + `GrabOffset` |
| `InventoryTransferResult` | `AnchorSlot` + `CoveredSlots` — первичные поля |
| Координаты grid | row-major (`index = y * columns + x`) |
| Slot management | Grid → только Fixed |
| Shaped Count | Всегда 1 в Phase 1–3 |
| Cross-inventory | Footprint — свойство предмета, occupancy — свойство инвентаря |
| Slot inventory + shaped | Default Accept (collapse в 1 слот), опционально Reject |
| Highlight | View-query `targetInventory.GetCoveredCells(...)` |
| Visual | Отдельная `PlacementOverlay` панель, `raycastTarget=false` |
| Swap / Batch / Auto-transfer для shaped | Reject через стандартный drop policy в Phase 1–3 |
