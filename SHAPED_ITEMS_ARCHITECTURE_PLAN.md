# Shaped Items Architecture Plan

**Last Updated**: 2026-04-01

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

Актуальное примечание по текущей реализации `ItemStack`:
- сам `ItemStack` уже может хранить `PrimaryAdapter` + список concrete adapters;
- это не меняет placement-инвариант: placement по-прежнему должен владеть ровно одним stack;
- shaped inventory не должен дублировать один и тот же stack по нескольким слотам только потому,
  что внутри stack теперь может быть список адаптеров

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

**Система координат: row-major.** Индекс слота вычисляется как `index = y * columns + x`, где `x` — колонка (0..columns-1), `y` — строка (0..rows-1). Это решение фиксируется в Phase 1, т.к. от него зависят все вычисления covered cells, footprint bounds checking и сериализация.

Текущий `UniversalInventory` уже хранит список слотов, но не знает топологию grid на уровне данных. Это нужно добавить отдельно, а не выводить каждый раз из UI.

**Ограничение: grid inventory работает только с `Fixed` slot management.** `Dynamic` слоты несовместимы с фиксированной NxM топологией. При попытке включить grid mode с `SlotManagementType.Dynamic` — ошибка инициализации. Динамический рост grid (добавление строк/колонок) можно рассмотреть как отдельную фичу Phase 4+.

### 4. Occupancy layer

Нужен runtime-слой занятости ячеек.

Он должен отвечать на вопросы:
- можно ли разместить предмет с footprint в anchor cell;
- какие ячейки будут заняты;
- какие existing items блокируют placement (список, не один — covered cells могут перекрывать несколько разных placements);
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

**Важно: не добавлять placement-состояние в интерфейс `ISlot`.** `ISlot` — публичный интерфейс, изменение которого сломает все существующие реализации. Placement-aware запросы должны идти через occupancy layer / grid inventory:

- `gridInventory.GetPlacement(slot)` → `Placement?` (anchor, orientation, covered cells);
- `gridInventory.IsFollowerSlot(slot)` → `bool`;
- `gridInventory.ResolveAnchorSlot(slot)` → `ISlot` (для follower возвращает anchor, для anchor — себя).

Слот остаётся лёгкой единицей UI и не знает о placement. Код, которому нужна placement-информация, обращается к инвентарю, а не к слоту.

На уровне `UniversalSlot` может потребоваться визуальная поддержка:
- возможность визуализировать occupied follower cells без собственного stack;
- отключение hover/selection визуала для follower cells (делегирование anchor-у).

Важно: follower-slot не должен притворяться полноценным владельцем `ItemStack`.

### DragContext

Текущий `DragContext` несёт source slot и target slot. Для shaped items drag context должен дополнительно нести:
- footprint перетаскиваемого предмета (из `IShapedItem`);
- текущую orientation;
- какая ячейка является anchor под курсором (не обязательно верхний левый угол footprint);
- **grab offset** — смещение от anchor до точки захвата. Когда пользователь хватает предмет 3x2 за ячейку (2,1), drag preview должен сохранять этот offset, иначе предмет «прыгнет» так, что anchor окажется под курсором. Grab offset хранится как `Vector2Int` в координатах footprint.

Без этого planner не сможет валидировать placement по всем covered cells. Расширение `DragContext` нужно сделать обратно совместимым: для 1x1 предметов anchor = target slot, footprint = 1x1, grab offset = (0,0).

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

**Механизм расширения**: вводится отдельный интерфейс `IPlacementRule` (рядом с существующим `IDragRule`). Placement rule получает расширенный контекст: anchor index, orientation, covered cells, конфликтующие placements. Существующие `IDragRule` продолжают работать для 1x1 предметов и обычных инвентарей. Grid inventory при валидации вызывает оба набора правил: сначала placement rules, затем стандартные slot rules для anchor slot. Выбор этого подхода нужно зафиксировать в Phase 1, чтобы Phase 2 не потребовала переделки.

### Swap для shaped items — зафиксированное ограничение

**Решение для Phase 1–3: swap для shaped items отключён.**

Текущий `TrySwapSlots` обменивает стеки двух одиночных слотов. Для shaped items swap становится комбинаторно сложным:
- covered cells нового placement могут перекрывать несколько разных existing items (не один);
- нужно проверить, что каждый вытесненный предмет поместится на освободившееся место;
- порядок вытеснения и валидации зависит от формы всех участников.

При дропе shaped item на occupied cell — reject (через `BlockedTargetBehavior.Reject`). Shaped item swap — отдельная фича Phase 4.

### Batch drag + shaped items — зафиксированное ограничение

**Решение для Phase 1–3: batch drag для shaped items отключён.**

Поиск валидных placements для нескольких shaped items одновременно — комбинаторная задача (каждый следующий placement зависит от уже размещённых). При попытке batch drag, содержащего shaped item, система должна отклонить операцию на этапе planner.

### GetAcceptableCount — зависимость от позиции

Текущий `IInventory.GetAcceptableCount(item, count)` — flat API, не учитывающий позицию. Для grid inventory acceptance зависит от того, **куда** кладётся предмет: 2x3 предмет может поместиться в одном углу grid, но не в другом.

`IGridInventoryStrategy` должен предоставлять position-aware API: `CanPlace(item, anchorIndex, orientation)`. Flat `GetAcceptableCount` для grid inventory возвращает ответ в смысле «есть ли хотя бы одна позиция, куда предмет поместится», без привязки к конкретному anchor.

## Визуал размещённого предмета

Документ описывает drag preview и ghost overlay, но не фиксирует базовый вопрос: **как рендерится уже размещённый shaped item на grid**.

### Выбранный подход: anchor renders full icon

Anchor slot отвечает за отрисовку иконки предмета. Иконка масштабируется на весь footprint (визуально перекрывает follower cells). Follower slots не рендерят собственную иконку — они отображают только «занятый» визуал (затемнение, рамка, или полностью прозрачны).

Это стиль Diablo / Escape from Tarkov и наиболее естественный для grid inventory.

Технически это означает:
- anchor slot рендерит `Image` с размером `cellSize * footprintSize` (выходит за пределы своего RectTransform);
- или используется отдельный UI-объект (overlay), порождаемый поверх grid при placement;
- follower cells подавляют hover highlight и tooltip — делегируют anchor-у.

Выбор конкретной реализации (растянутый Image vs overlay) — Phase 3, но принципиальное решение «anchor рендерит весь предмет» фиксируется сейчас, т.к. оно влияет на архитектуру `UniversalSlot.UpdateVisuals()`.

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
- grid metadata и topology (row-major координаты, Fixed slot management);
- `PlacementData` и список placements в инвентаре;
- occupancy layer как производное состояние;
- placement queries через grid inventory (CanPlace, GetPlacement, GetCoveredSlots, ResolveAnchorSlot);
- `IGridInventoryStrategy` как отдельная стратегия;
- `IPlacementRule` интерфейс (рядом с `IDragRule`);
- сериализация `PlacementData`;
- валидация: grid + Dynamic = ошибка; shaped item + Count > 1 = ошибка.

Без drag UI, без rotation, без fancy preview. Без изменений в `DragContext` и transfer pipeline.

**Тестируемость Phase 1**: фаза не даёт user-visible функциональности. Тестирование — через play-mode / unit тесты на API уровне: создание grid inventory, placement через код (`TryAddItem`), проверка occupancy, сериализация/десериализация. Это валидирует фундамент до интеграции с drag pipeline.

### Phase 2. Planner/Executor integration

Потом внедрить shaped placement в transfer pipeline:
- расширить `DragContext`: footprint, orientation, grab offset;
- planner строит placement-aware allocations через occupancy layer;
- executor применяет placement transaction;
- rollback работает на multi-cell state через расширенный `InventorySnapshot`;
- расширить `InventoryTransferResult` полями anchor/covered;
- swap для shaped items → reject;
- batch drag с shaped items → reject.

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
- rule presets for equipment-like grid inventories;
- swap для shaped items (multi-item displacement при дропе);
- batch drag с shaped items (комбинаторный поиск placements);
- динамический рост grid (добавление строк/колонок).

### Не покрытые аспекты (учесть при реализации)

- **FilterSortController**: auto-sort явно отключён для grid inventory в Phase 1–3, но фильтрация (hide/show) может быть полезна. Нужно определить, как фильтрация взаимодействует с occupancy — скрытый предмет всё ещё занимает ячейки.
- **DropPolicySettings**: `FindAlternative` для grid inventory означает поиск валидного anchor в 2D, а не просто «следующий слот». `DropPolicySettings.Resolve` должен корректно работать с grid-aware поиском альтернатив.
- **Tooltip/Describable**: для shaped items tooltip должен появляться при наведении на любую covered cell, но показывать данные anchor placement. Нужен проход через `ResolveAnchorSlot` в tooltip pipeline.

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
- только grid inventory с Fixed slot management;
- row-major coordinate system;
- rotation 0/90;
- shaped items всегда Count = 1;
- один anchor slot хранит stack;
- occupancy layer как производное состояние из placement list;
- planner проверяет footprint по occupied cells;
- selection и context menu работают по anchor placement;
- drag preview с grab offset;
- auto-sort явно отключён для grid inventory (2D bin packing — отдельная задача);
- auto-transfer для shaped items явно отключён;
- swap при дропе shaped item на occupied cell — reject;
- batch drag с shaped items — reject.

Это уже даст сильную пользовательскую ценность и не потребует сразу решать все сложные edge-case для произвольных форм.

## Итоговое решение

Для этого проекта следует считать выбранной моделью:
- `anchor placement + occupancy layer`;
- без shared `ItemStack` across multiple slots;
- footprint через опциональный `IShapedItem`, без изменения `IInventoryItem`;
- occupancy как производное состояние от списка placements;
- `IGridInventoryStrategy` отдельно от существующих стратегий;
- `IPlacementRule` рядом с `IDragRule` для placement-aware валидации;
- placement-запросы через grid inventory, не через `ISlot`;
- shaped items Count = 1 (Phase 1–3);
- swap и batch drag для shaped items отключены (Phase 1–3);
- grid inventory только с Fixed slot management;
- row-major coordinate system (`index = y * columns + x`);
- anchor renders full icon, followers — occupied overlay;
- drag с grab offset для корректного UX;
- с резолвом любых slot-driven действий через anchor placement.

Если когда-либо понадобится модель со shared runtime entity на несколько ячеек, ее лучше делать как отдельную placement-сущность, а не как один `ItemStack`, напрямую лежащий в нескольких слотах.
