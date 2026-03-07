# Shaped Items Architecture Plan

**Last Updated**: 2026-03-06

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
- предмет знает свой footprint;
- planner работает не с одиночным slot, а с набором занимаемых ячеек.

Иными словами: базовая сущность размещения становится не `slot`, а `placement`.

## Что нужно ввести

### 1. Item footprint model

Нужен новый контракт, например концептуально:
- размер (`width`, `height`) для прямоугольных предметов;
- или матрица/битмаска для произвольной формы;
- ориентация (`rotation`);
- anchor point.

Минимальный практичный старт:
- прямоугольный footprint;
- поворот на 90 градусов;
- anchor = верхний левый или нижний левый slot.

Расширенный вариант позже:
- произвольная shape mask.

### 2. Grid inventory topology

У инвентаря должен появиться grid layout descriptor:
- число колонок;
- число строк;
- mapping `index <-> x,y`;
- проверки выхода за границы.

Текущий `UniversalInventory` уже хранит список слотов, но не знает топологию grid на уровне данных. Это нужно добавить отдельно, а не выводить каждый раз из UI.

### 3. Occupancy layer

Нужен runtime-слой занятости ячеек.

Он должен отвечать на вопросы:
- можно ли разместить предмет с footprint в anchor cell;
- какие ячейки будут заняты;
- какой existing item блокирует placement;
- какие placements конфликтуют.

Это должен быть отдельный компонент/сервис, а не логика внутри `UniversalSlot`.

### 4. Placement model

Нужна отдельная сущность размещения предмета, содержащая:
- item;
- count;
- anchor slot/index;
- orientation;
- covered cells.

Текущая модель `ItemStack` хранится на уровне одного slot. Для shaped items придется различать:
- логический owner placement;
- визуальные / occupancy cells.

На практике это означает: один slot должен быть anchor/owner, остальные — occupied followers.

## Изменения в текущей архитектуре

### UniversalInventory

Нужно расширить:
- grid metadata;
- API поиска placement по slot;
- API проверки placement;
- операции установки/очистки multi-cell placement.

Нельзя оставлять модель, где каждый slot считается независимым владельцем собственного `ItemStack`.

Лучший путь:
- только anchor slot хранит реальный stack/item;
- остальные занятые слоты знают, что они являются частью placement и ссылаются на anchor.

### ISlot / UniversalSlot

Желательно добавить состояние вида:
- `IsAnchorSlot`;
- `AnchorSlot` или `AnchorIndex`;
- `IsOccupiedByCompositeItem`;
- возможность визуализировать occupied follower cells без собственного stack.

Слот должен оставаться легкой единицей UI, не центром правил размещения.

### TransferPlanner

Это главный модуль, который придется переработать.

Сейчас planner планирует allocation по одиночным slot target candidates. Для shaped items planner должен:
- искать набор anchor candidates;
- проверять footprint целиком для каждого кандидата;
- резервировать virtual occupancy не по slot, а по всем covered cells;
- строить placement plan, а не simple slot allocation.

Если этого не сделать именно в planner, вся логика начнет расползаться по UI и inventory mutation code.

### TransferPlanExecutor

Executor должен применять plan как placement transaction:
- освободить старый placement целиком;
- занять новый placement целиком;
- откатить все covered cells в atomic mode;
- корректно эмитить события только один раз на placement, а не на каждую ячейку.

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

## Этапы реализации

### Phase 1. Foundation

Сначала добавить только основу:
- grid metadata;
- footprint contract;
- anchor + occupied cells;
- placement queries.

Без drag UI, без rotation, без fancy preview.

### Phase 2. Planner/Executor integration

Потом внедрить shaped placement в transfer pipeline:
- planner строит placement-aware allocations;
- executor применяет placement transaction;
- rollback работает на multi-cell state.

### Phase 3. UI/UX

После этого:
- ghost preview;
- hover/selection/context menu by placement;
- rotation input;
- visual overlays on occupied cells.

### Phase 4. Advanced

Позже можно добавлять:
- произвольные shape masks;
- auto-rotation on placement;
- packing heuristics;
- shape-aware auto-sort;
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
- обычный inventory;
- grid/shaped inventory.

## Практическая рекомендация

Самый безопасный путь:

1. Ввести placement model рядом с текущей slot model.
2. Добавить grid inventory как отдельный режим инвентаря.
3. Адаптировать planner/executor так, чтобы они умели оба режима.
4. Только потом переносить selection/context menu/preview на placement-aware поведение.

Это дольше, но существенно безопаснее, чем пытаться сразу переписать всю систему под shaped items.

## Минимальный MVP shaped items

Если нужен не идеальный, а реалистичный первый релиз фичи, то MVP может быть таким:
- только прямоугольные предметы;
- только grid inventory;
- rotation 0/90;
- один anchor slot хранит stack;
- planner проверяет footprint по occupied cells;
- selection и context menu работают по anchor placement;
- auto-sort и batch swap можно временно ограничить.

Это уже даст сильную пользовательскую ценность и не потребует сразу решать все сложные edge-case для произвольных форм.
