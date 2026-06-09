# Unified Placement Planning - Design Plan

**Цель:** перестать различать single-cell и shaped в transfer semantics. Single-cell должен быть
вырожденным случаем footprint из одной ячейки, а планирование, acceptance и выполнение должны
использовать одну placement-модель.

При этом доступность места определяется не одним `CanPlace`, а сочетанием трех независимых проверок:

1. стратегия определяет допустимость create/merge, capacity и порядок кандидатов;
2. rules проверяют конкретную операцию и целевой anchor;
3. placement state проверяет топологию, границы и occupancy.

> Статус: **план, runtime-код не меняется.** Реализация выполняется отдельными компилируемыми
> этапами после ревью документа.

---

## 1. Почему унификация возможна

Хранилище уже унифицировано:

- `PlacementStore` является источником правды для всех стеков, включая footprint из одной ячейки.
- Footprint принадлежит топологии через
  `IInventoryTopology.GetPlacementOffsets(shape, orientation)`.
- `SlotTopology` всегда возвращает anchor-only, поэтому пространственная форма предмета в слотном
  инвентаре занимает один слот без отдельной policy.
- Grid-топология использует реальные ориентированные offsets формы.
- Основной swap-путь уже placement-based: обе исходные placement освобождаются, после чего предметы
  размещаются через topology-aware `TryPlace`.

Следовательно, различие single-cell/shaped уже не является свойством storage. Оно осталось в
планировщике, acceptance-стратегиях и executor.

---

## 2. Где сейчас живет расхождение

### 2.1 Планировщик

- Single-cell проходит через `AllocateForStrategyInventory` / `AllocateForUniqueInventory` и
  возвращает `PlannedSlotAllocation`.
- Multi-cell grid placement проходит через `TryPlanShapedPlacement` и возвращает
  `PlannedPlacementAllocation`.
- В нескольких местах `IsSingleCell` выбирает не оптимизацию, а разные transfer semantics.
- `VirtualSlotState` хранит только stack одного слота и не моделирует занятые footprint.

### 2.2 Стратегии и acceptance

- `GetSlotCandidates`, `GetAcceptableCount` и `CanUseAlternativeSlot` в основном рассуждают через
  `slot.IsEmpty`.
- Эти методы корректно выражают stack/unique semantics, но не доказывают, что shape помещается в
  топологию.
- `CanAcceptShape` отдельно отсекает multi-cell для grid, потому что общий acceptance-путь пока не
  умеет планировать placement без заранее заданного anchor.

### 2.3 Executor

- Slot allocation исполняется через `TryAddToSlot` / dynamic-slot path.
- Shaped allocation исполняется через отдельный `TryAddToTargetPlacement`.
- `PlannedPlacementAllocation` сейчас только один на entry, поэтому не покрывает распределение
  стека по нескольким placement.
- Публичный `UniversalInventory.TrySwapSlots` все еще меняет `BaseSlot.Stack` напрямую и не должен
  использоваться для shaped placement. Основной planner/executor pipeline этот метод уже не вызывает.

---

## 3. Главный инвариант

`PlacementStore.CanPlace` проверяет **реальное текущее состояние** инвентаря. Планировщик не может
использовать его как единственный источник feasibility, потому что один план может содержать
несколько еще не выполненных операций.

Пример:

1. первый batch-entry планирует footprint `{0, 1}`;
2. runtime `PlacementStore` пока не изменен;
3. второй entry вызывает обычный `CanPlace` и тоже получает разрешение на `{0, 1}`;
4. план выглядит валидным, но executor не сможет его применить.

Поэтому перед общей миграцией нужен виртуальный topology-aware state, который резервирует
запланированные footprint и merge capacity.

---

## 4. Целевая архитектура

### 4.1 `PlacementPlanningState`

Внутренняя модель планировщика создается из текущих placement инвентаря и живет на протяжении
построения всего `TransferPlan`.

Минимальные операции:

```text
GetPlacementAt(cellOrAnchor)
CanPlace(request, ignoredPlacements)
TryReserveCreate(allocation)
TryReserveMerge(allocation)
ReleaseSourcePlacement(placement)
GetRemainingMergeCapacity(placement, item)
```

Требования:

- использует ту же `IInventoryTopology`, что и runtime `PlacementStore`;
- видит реальные placement и все ранее запланированные изменения;
- не мутирует inventory, slots и runtime `PlacementStore`;
- для same-inventory move освобождает source footprint ровно один раз;
- поддерживает последовательный deterministic greedy planning для batch;
- позволяет planner и acceptance использовать один и тот же алгоритм dry-run.

Необязательно сразу делать публичный интерфейс. На первом этапе это internal-модель, заменяющая
`VirtualSlotState` только там, где нужна геометрия. Старый virtual-slot view может временно
сосуществовать с ней для stack/rule compatibility.

### 4.2 Универсальная аллокация

У entry должен быть список placement-аллокаций, а не одна shaped-аллокация или отдельный список
slot-аллокаций:

```text
PlannedPlacementAllocation
    TargetKind: ExistingAnchor | NewDynamicSlot
    OperationKind: Create | Merge
    AnchorIndex
    Orientation
    Shape
    Amount
```

Правила:

- single-cell - обычный `Create`/`Merge` с footprint из одной ячейки;
- shaped stack может иметь несколько аллокаций, если стратегия допускает несколько placement;
- `NewDynamicSlot` хранит намерение создать слот, потому что его реальный index появится только в
  executor;
- grid + dynamic slots пока запрещен существующей конфигурацией, поэтому unknown anchor нужен только
  slot topology;
- swap и occupied-handler остаются отдельными типами planned operation, а не маскируются под
  обычную аллокацию.

Перед удалением `PlannedSlotAllocation` нужно проверить его публичную доступность и внешних
потребителей. При необходимости оставить obsolete compatibility wrapper на один релиз.

### 4.3 Роли strategy, rules и geometry

Стратегия отвечает за:

- разрешение create/merge/reject;
- max stack и remaining capacity;
- one-per-ID и separable-stack semantics;
- порядок кандидатов: hinted, merge-first, empty-first и другие policy;
- допустимость создания dynamic slot.

Rules отвечают за конкретный item, amount и target anchor.

`PlacementPlanningState` отвечает только за:

- проекцию shape через topology;
- bounds;
- occupancy;
- резервирование planned footprint.

Стратегия не должна самостоятельно реализовывать геометрию, а `CanPlace` не должен принимать
решения о stacking или unique semantics.

### 4.4 Один аллокатор

Целевой алгоритм:

```text
PlanEntry(entry, policy, target, hint, planningState):
    1. resolve converted item, shape, orientation and requested amount
    2. release source placement in planningState for a same-inventory move
    3. ask strategy for ordered create/merge candidates
    4. for each candidate:
         - calculate strategy capacity
         - validate rules for concrete anchor and amount
         - validate geometry through planningState
         - reserve merge or footprint in planningState
         - append PlannedPlacementAllocation
    5. if hinted target is blocked and policy requests Swap:
         - TryPlanSwap using the same virtual state assumptions
    6. apply AllowPartial and BatchMode semantics
```

Аллокатор не ветвится по `IsSingleCell`. Отличия выражаются topology, candidate policy и strategy
capabilities.

### 4.5 Acceptance через тот же dry-run

`GetAcceptableCount` не должен иметь отдельный упрощенный алгоритм упаковки shaped items.
Иначе preview и реальный planner со временем разойдутся.

Нужен общий internal allocation service:

- planner запрашивает аллокации и получает полный результат;
- acceptance запускает тот же dry-run и возвращает сумму `Amount`;
- `CanAcceptItem` берет первый реально допустимый existing anchor либо сообщает возможность
  `NewDynamicSlot`.

Для вызова без target hint используется deterministic scan anchors в strategy order. Это greedy
оценка, а не поиск оптимальной упаковки. Такой контракт должен быть явно задокументирован.

---

## 5. Что не требуется унифицировать

1. **UI и rendering.** Drag visual, overlay и подсветка legitimately используют размер и форму.
2. **Доменные rules.** Правило может сознательно запрещать multi-cell item.
3. **Оптимизации.** Быстрый путь для footprint из одной ячейки допустим, если результат совпадает с
   общей placement semantics.
4. **Поддержка сложного batch.** Strategy capability может запретить multi-entry spatial planning.
   Это должно быть явным решением capability, а не скрытым `IsShaped` guard в planner.
5. **Оптимальная упаковка.** Первая версия использует deterministic greedy reservation. Полный
   bin-packing/backtracking не входит в scope.

Цель - убрать разные бизнес-правила переноса, а не добиться буквального отсутствия всех проверок
формы в кодовой базе.

---

## 6. Этапы реализации

### Этап 0. Characterization и API audit

- Зафиксировать существующее поведение slot/grid для stackable, separable, unique.
- Добавить тесты для partial, same-inventory move, conversion и dynamic slot.
- Добавить swap event snapshot test и тест direct API `TrySwapSlots`.
- Проверить внешних потребителей `PlannedSlotAllocation` и `PlannedPlacementAllocation`.

### Этап 1. Виртуальное placement-состояние

- Ввести `PlacementPlanningState`.
- Инициализировать его реальными placement.
- Добавить тесты резервирования пересекающихся footprint, освобождения source и merge capacity.
- Пока не менять публичные планы и executor.

### Этап 2. Shaped alternative search

- Перевести поиск альтернативного anchor для shaped на `PlacementPlanningState`.
- Сохранить strategy ordering и rules.
- Добавить тесты: occupied hint, свободный регион, отсутствие региона, same-inventory source release.

### Этап 3. Универсальная модель аллокаций

- Расширить `PlannedPlacementAllocation` полями target/operation kind.
- Перевести `PlannedEntryTransfer` на список аллокаций.
- Добавить `NewDynamicSlot`.
- Временно адаптировать старые slot allocations в новый формат.

### Этап 4. Унифицированный executor

- Исполнять `Create` через `TryPlace` для любой topology.
- Исполнять `Merge` через placement stack.
- Для `NewDynamicSlot` сначала создавать слот, затем строить anchor и выполнять `TryPlace`.
- Сохранить snapshots, domain validation, conversion и deferred events.
- Удалить shaped-only guard из `TryAddToTargetPlacement`.

### Этап 5. Миграция single-cell planner

- Перевести strategy/unique allocation на общий аллокатор.
- Использовать один planning state для всех entries одного плана.
- После стабилизации удалить `PlannedSlotAllocation`, `VirtualSlotState` и старые executor branches,
  если они больше не нужны.

### Этап 6. Унификация acceptance

- Перевести `GetAcceptableCount` и `CanAcceptItem` на dry-run общего аллокатора.
- Удалить `CanAcceptShape` как routing guard.
- Проверить, что preview capacity совпадает с реально построенным планом.

### Этап 7. Batch capabilities и зачистка

- Заменить shape-hardcode для batch явной capability стратегии.
- Удалить оставшиеся semantic `IsSingleCell` branches из planner/executor/acceptance.
- Оставить UI, rules и доказанные fast paths.
- Либо удалить/сделать obsolete `UniversalInventory.TrySwapSlots`, либо направить его в безопасный
  placement-based service.

### Этап 8. Документация

- Обновить `.agents/skills/dragdrop-*` и зеркальные `.claude/skills/dragdrop-*`.
- Исправить устаревшее описание swap в `DATA_FLOW.md` и `COMPONENTS.md`.
- Обновить публичную документацию по strategy/acceptance extension points.

Каждый этап должен отдельно компилироваться и проходить соответствующий test subset.

---

## 7. Риски и проверки

### Корректность

- planned footprint должен учитывать все предыдущие аллокации текущего плана;
- source placement нельзя освобождать повторно при нескольких allocations одного entry;
- covered-cell interaction должен резолвиться в логический placement/anchor;
- conversion может изменить shape, поэтому geometry проверяется по converted adapter;
- merge не создает новый footprint, но меняет virtual remaining capacity;
- same-inventory swap должен проверять совместимость двух результирующих footprint;
- atomic rollback и события должны сохранять placement snapshots.

### Dynamic slots

- unknown anchor допустим только как `NewDynamicSlot`;
- после создания слота executor обязан проверить topology и выполнить `TryPlace`;
- rollback должен удалить созданный слот через восстановление snapshot;
- grid inventory продолжает запрещать dynamic slot management, пока не появится отдельная модель
  расширяемой spatial topology.

### Производительность

- anchor scan имеет стоимость `O(anchorCount * footprintSize)`;
- offsets shape/orientation можно кэшировать;
- planning state должен обновлять occupancy инкрементально;
- acceptance не должен строить Unity-объекты или мутировать inventory;
- backtracking не требуется в первой версии.

### Тестовая матрица

- slot и grid topology;
- single-cell и multi-cell;
- stackable, separable и unique;
- create, explicit merge, auto merge, alternative, swap;
- full и partial stack;
- same-inventory и cross-inventory;
- conversion с неизменной и измененной shape;
- batch best-effort и atomic;
- dynamic slot create/rollback;
- drop на anchor и covered cell;
- placement snapshots в add/remove/swap events.

---

## 8. Критерии готовности

- Planner использует один topology-aware allocation service.
- Все entries одного плана разделяют `PlacementPlanningState`.
- Single-cell и shaped создают одинаковый тип списка аллокаций.
- Strategy, rules и geometry имеют раздельные обязанности.
- Acceptance capacity вычисляется dry-run того же аллокатора.
- Executor выполняет create/merge через placement API для slot и grid topology.
- Нет `IsSingleCell` branches, меняющих transfer semantics; UI/rules/fast paths разрешены.
- Direct swap API не обходит placement invariants.
- Все characterization и новые topology/batch/event тесты проходят.
- Architecture skills и публичная документация соответствуют реализации.

---

*Основные затрагиваемые области: `TransferPlanner`, `TransferPlanExecutor`, `PlacementStore`,
`InventoryTopology`, `UniversalInventory`, `InventoryAcceptanceRequest`, `VirtualSlotState`,
`InventoryStrategyBase`, concrete strategies и `Core/Drop/*AlternativePlacementStrategy`.*
