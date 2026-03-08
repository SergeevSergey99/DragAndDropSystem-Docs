# Shaped Items Architecture Plan

**Last Updated**: 2026-03-08

Документ описывает рекомендуемую архитектуру для поддержки предметов разного размера и формы, занимающих несколько ячеек инвентаря.

## Цель

Добавить поддержку grid-based inventory, в котором предмет:
- может занимать несколько ячеек;
- может иметь произвольный footprint;
- может поворачиваться;
- должен переноситься, валидироваться и визуализироваться как единое целое.

Цель при этом — не ломать текущую slot-based систему, а расширить её.

## Ключевой принцип

Не пытаться внедрить shaped items как особый случай текущего `ISlot`.

Правильная модель:
- `ISlot` остается минимальной ячейкой UI/инвентаря;
- поверх набора слотов вводится слой layout/occupancy;
- предмет может знать свой footprint через опциональный интерфейс;
- planner работает не с одиночным slot, а с набором занимаемых ячеек.

Иными словами: базовая сущность размещения становится не `slot`, а `placement`.

## Выбранная модель данных

Для этого проекта рекомендуемая модель:
- один `ItemStack` принадлежит одному placement;
- placement имеет один anchor slot;
- только anchor slot хранит реальный stack;
- остальные клетки хранят только occupancy-состояние и ссылку на anchor / placement id.

Это означает явный отказ от модели, где один и тот же `ItemStack` лежит сразу в нескольких слотах.

### Почему не shared `ItemStack` в нескольких слотах

Теоретически это возможно, но для текущей архитектуры это плохой компромисс.

Проблемы такого подхода:
- ломается простой инвариант `one slot -> zero or one stack`;
- `slot.ItemStack` перестает быть однозначным источником истины;
- в drag, selection, sort, context menu и save/load приходится везде проверять, является ли слот owner или follower;
- растет риск двойного учета одного и того же stack;
- сериализация и восстановление shared references усложняются;
- почти весь текущий код начнет обрастать special-case логикой.

Для текущего проекта `anchor + occupancy` дает ту же пользовательскую функциональность, но с существенно более чистыми инвариантами.

## Что нужно ввести

### 1. Item footprint model

`IInventoryItem` — минимальный интерфейс, добавлять footprint напрямую в него нельзя: это сломает все существующие реализации.

**Выбранный подход**: отдельный опциональный интерфейс `IShapedItem : IInventoryItem`.

```
IShapedItem
  int FootprintWidth  { get; }
  int FootprintHeight { get; }
  // Phase 4: bool[,] ShapeMask { get; }
```

Occupancy layer проверяет `item as IShapedItem`. Если `null` — предмет считается 1x1, и поведение не отличается от текущего. Это обеспечивает полную совместимость без изменения существующих адаптеров.

Минимальный практичный старт:
- прямоугольный footprint;
- поворот на 90 градусов;
- anchor = верхний левый slot.

Расширенный вариант позже:
- произвольная shape mask через `bool[,]` или битмаску.

### 2. Стекуемость shaped items — зафиксированное ограничение

**Решение для Phase 1–3: shaped items всегда имеют `Count = 1`.**

`ItemStack.Count > 1` для shaped item не поддерживается. Occupancy layer при размещении обязан проверять это как precondition. Попытка положить стак из 3 единиц предмета с footprint 2x1 должна явно отклоняться.

Это упрощает planner, executor, сериализацию и все edge-case логики. Стекуемые shaped items можно рассмотреть в Phase 4 как отдельную фичу.

### 3. Grid inventory topology

У инвентаря должен появиться grid layout descriptor:
- число колонок;
- число строк;
- mapping `index <-> x,y`;
- проверки выхода за границы.

Текущий `UniversalInventory` уже хранит список слотов, но не знает топологию grid на уровне данных. Это нужно добавить отдельно, а не выводить каждый раз из UI.

### 4. Occupancy layer

Нужен runtime-слой занятости ячеек.

Он должен отвечать на вопросы:
- можно ли разместить предмет с footprint в anchor cell;
- какие ячейки будут заняты;
- какой existing item блокирует placement;
- какие placements конфликтуют.

**Ключевое решение: occupancy layer — производное состояние.**

Occupancy не является источником истины. Источник истины — список `Placement` объектов в инвентаре. Occupancy-карта (mapping `slotIndex -> placementId`) строится из этого списка при инициализации и перестраивается при любом изменении placement.

Это означает:
- сериализуется только список placements;
- occupancy пересчитывается при загрузке сцены / десериализации;
- нет риска рассинхронизации между слотами и occupancy.

Occupancy layer реализуется как сервис внутри grid inventory (не отдельный `MonoBehaviour`), принадлежащий инвентарю и управляемый им.

### 5. Placement model

Нужна отдельная сущность размещения предмета, содержащая:
- item;
- count (для Phase 1–3 всегда = 1);
- anchor slot/index;
- orientation;
- covered cells.

Текущая модель `ItemStack` хранится на уровне одного slot. Для shaped items придется различать:
- логический owner placement;
- визуальные / occupancy cells.

На практике это означает: один slot должен быть anchor/owner, остальные — occupied followers.

Рекомендуемый инвариант:
- один `ItemStack` <-> один placement;
- один placement <-> один anchor slot;
- follower-cells не являются владельцами stack.

### 6. Сериализация placement state

Сериализуется список `PlacementData`:
```
PlacementData
  int   anchorIndex
  string itemId
  int   count        // Phase 1–3: всегда 1
  int   orientation  // 0 или 90
```

Occupancy пересчитывается при загрузке из этого списка. Follower-состояние слотов не сериализуется — оно восстанавливается из `PlacementData`.

Это влияет на то, как `UniversalInventory` хранит и восстанавливает state в grid-режиме.

## Изменения в текущей архитектуре

### IInventoryStrategy

**Это самое болезненное место.** Текущий `IInventoryStrategy.TryAdd(List<ISlot> slots, ItemStack stack, int targetIndex)` принимает один `targetIndex`. Для shaped item нужно передать anchor + orientation — это не расширяемо без смены сигнатуры.

**Решение**: для grid inventory вводится отдельный `IGridInventoryStrategy`, не наследующий от `IInventoryStrategy`. Grid inventory использует его вместо базовой стратегии. Существующие стратегии (`UniqueItemStrategy`, `StackableItemStrategy` и др.) не трогаются.

Это решение нужно принять на Phase 1, иначе Phase 2 потребует переписывать уже написанный код.

### UniversalInventory

Нужно расширить:
- grid metadata;
- список placements и API управления ими;
- API поиска placement по slot;
- API проверки placement;
- операции установки/очистки multi-cell placement.

Нельзя оставлять модель, где каждый slot считается независимым владельцем собственного `ItemStack`.

Лучший путь:
- только anchor slot хранит реальный stack/item;
- остальные занятые слоты знают, что они являются частью placement и ссылаются на anchor;
- публичные методы, принимающие slot, должны уметь резолвить placement через anchor.

### ISlot / UniversalSlot

Желательно добавить состояние вида:
- `IsAnchorSlot`;
- `AnchorSlot` или `AnchorIndex`;
- `IsOccupiedByCompositeItem`;
- возможность визуализировать occupied follower cells без собственного stack.

Слот должен оставаться легкой единицей UI, не центром правил размещения.

Важно: follower-slot не должен притворяться полноценным владельцем `ItemStack`.

### DragContext

Текущий `DragContext` несёт source slot и target slot. Для shaped items drag context должен дополнительно нести:
- footprint перетаскиваемого предмета (из `IShapedItem`);
- текущую orientation;
- какая ячейка является anchor под курсором (не обязательно верхний левый угол footprint).

Без этого planner не сможет валидировать placement по всем covered cells. Расширение `DragContext` нужно сделать обратно совместимым: для 1x1 предметов anchor = target slot, footprint = 1x1.

### TransferPlanner

Это главный модуль, который придется переработать.

Сейчас planner планирует allocation по одиночным slot target candidates. Для shaped items planner должен:
- извлекать footprint из `DragContext` (через `IShapedItem`);
- искать набор anchor candidates;
- проверять footprint целиком для каждого кандидата через occupancy layer;
- резервировать virtual occupancy не по slot, а по всем covered cells;
- строить placement plan, а не simple slot allocation.

Если этого не сделать именно в planner, вся логика начнет расползаться по UI и inventory mutation code.

### TransferPlanExecutor и snapshot/rollback

Executor должен применять plan как placement transaction:
- освободить старый placement целиком;
- занять новый placement целиком;
- откатить все covered cells в atomic mode;
- корректно эмитить события только один раз на placement, а не на каждую ячейку.

**Snapshot/rollback требует расширения.** Текущий `InventorySnapshotUtility` делает снапшоты на уровне одного слота. При откате multi-cell placement нужно откатить все covered cells атомарно. На Phase 2 `InventorySnapshot` должен уметь захватывать и восстанавливать placement state (список placements + occupancy), а не только содержимое отдельных слотов.

### InventoryTransferResult

Текущий `InventoryTransferResult` возвращает один `ISlot TargetSlot`. После placement multi-cell предмета это поле теряет смысл или становится двусмысленным.

**Решение**: добавить в `InventoryTransferResult` опциональные поля:
- `ISlot AnchorSlot` — anchor slot размещения (для shaped items);
- `IReadOnlyList<int> CoveredSlotIndices` — все занятые ячейки.

Для 1x1 переносов `AnchorSlot == TargetSlot`, `CoveredSlotIndices` содержит один элемент. Все существующие подписчики события трансфера продолжают работать без изменений.

### RuleEvaluationService / Rules

Rules должны начать получать расширенный контекст placement.

Примеры новых проверок:
- item fits by shape;
- rotation allowed / forbidden;
- item cannot overlap blocked cells;
- anchor slot constraints;
- placement must remain inside inventory bounds.

При этом старые slot rules должны не исчезнуть, а стать частным случаем placement validation.

## Drag and UI

### Drag preview

Для shaped items нужен ghost preview, который показывает:
- footprint предмета;
- валидность текущего placement;
- anchor cell;
- цветовое различие valid/invalid.

Без этого UX будет слабым, даже если core-логика будет правильной.

### Pointer targeting

При наведении на follower-cell система должна уметь:
- резолвить anchor placement;
- работать с предметом как с целым объектом;
- открывать контекстное меню по placement, а не по отдельной follower-cell.

### Selection

Selection должна работать на уровне placement, а не отдельных occupied cells.

Если выделяется один квадрат предмета 2x3, выделенным должен считаться весь placement.

### Context menu

`ContextMenuContext` в будущем лучше расширить placement-данными:
- anchor slot;
- covered cells;
- orientation;
- maybe logical item instance / placement id.

### AutoTransferAnimationStrategy — явное ограничение

Существующий `AutoTransferContext` / `TweenAutoTransferAnimation` работают на уровне одного слота. Для auto-transfer shaped item нужно освободить весь старый placement и занять весь новый — текущая анимация этого не поддерживает.

**Решение для Phase 1–3: auto-transfer для shaped items отключён явно.** При попытке auto-transfer shaped item система должна либо отклонить операцию, либо выполнить перенос без анимации. Полноценная поддержка анимации — Phase 4.

## Этапы реализации

### Phase 1. Foundation

Сначала добавить только основу:
- `IShapedItem` интерфейс;
- grid metadata и topology;
- `PlacementData` и список placements в инвентаре;
- occupancy layer как производное состояние;
- anchor + follower state на слотах;
- placement queries (CanPlace, GetPlacement, GetCoveredSlots);
- `IGridInventoryStrategy` как отдельная стратегия;
- сериализация `PlacementData`.

Без drag UI, без rotation, без fancy preview. Без изменений в `DragContext` и transfer pipeline.

### Phase 2. Planner/Executor integration

Потом внедрить shaped placement в transfer pipeline:
- расширить `DragContext` footprint + orientation;
- planner строит placement-aware allocations через occupancy layer;
- executor применяет placement transaction;
- rollback работает на multi-cell state через расширенный `InventorySnapshot`;
- расширить `InventoryTransferResult` полями anchor/covered.

### Phase 3. UI/UX

После этого:
- ghost preview;
- hover/selection/context menu by placement;
- rotation input;
- visual overlays on occupied cells.

### Phase 4. Advanced

Позже можно добавлять:
- произвольные shape masks (`bool[,]`);
- auto-rotation on placement;
- packing heuristics;
- shape-aware auto-sort (2D bin packing — нетривиальная задача, отдельный scope);
- полноценная auto-transfer анимация для shaped items;
- стекуемые shaped items (если нужно);
- rule presets for equipment-like grid inventories.

## Что важно не сломать

При внедрении shaped items нужно сохранить:
- текущие slot-only inventory сценарии как supported baseline;
- совместимость с context menu;
- batch drag behavior;
- world drop integration;
- rollback-safe transfer execution;
- current rule pipeline.

Лучше всего добиться этого через feature flag / inventory mode:
- обычный inventory (текущий режим, без изменений);
- grid/shaped inventory (новый режим).

Проверка режима должна быть в одном месте (на уровне инвентаря), а не размазана по planner/executor/UI.

## Практическая рекомендация

Самый безопасный путь:

1. Ввести `IShapedItem` и `PlacementData` рядом с текущей slot model.
2. Добавить grid inventory как отдельный режим с `IGridInventoryStrategy`.
3. Адаптировать `DragContext`, planner и executor так, чтобы они умели оба режима.
4. Только потом переносить selection/context menu/preview на placement-aware поведение.

Это дольше, но существенно безопаснее, чем пытаться сразу переписать всю систему под shaped items.

## Минимальный MVP shaped items

Если нужен не идеальный, а реалистичный первый релиз фичи, то MVP может быть таким:
- только прямоугольные предметы (`IShapedItem` с width/height);
- только grid inventory;
- rotation 0/90;
- shaped items всегда Count = 1;
- один anchor slot хранит stack;
- occupancy layer как производное состояние из placement list;
- planner проверяет footprint по occupied cells;
- selection и context menu работают по anchor placement;
- auto-sort явно отключён для grid inventory (2D bin packing — отдельная задача);
- auto-transfer для shaped items явно отключён.

Это уже даст сильную пользовательскую ценность и не потребует сразу решать все сложные edge-case для произвольных форм.

## Итоговое решение

Для этого проекта следует считать выбранной моделью:
- `anchor placement + occupancy layer`;
- без shared `ItemStack` across multiple slots;
- footprint через опциональный `IShapedItem`, без изменения `IInventoryItem`;
- occupancy как производное состояние от списка placements;
- `IGridInventoryStrategy` отдельно от существующих стратегий;
- shaped items Count = 1 (Phase 1–3);
- с резолвом любых slot-driven действий через anchor placement.

Если когда-либо понадобится модель со shared runtime entity на несколько ячеек, ее лучше делать как отдельную placement-сущность, а не как один `ItemStack`, напрямую лежащий в нескольких слотах.
