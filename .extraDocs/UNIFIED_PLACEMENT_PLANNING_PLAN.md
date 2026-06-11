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
>
> Решение по scope (2026-06-11): целевой scope — **полная консолидация** (этапы 0-9), включая
> batch+shaped и миграцию single-cell пути. Публичного релиза еще не было — это единственный
> момент, когда двойной путь можно убрать без compatibility-обязательств. Контрольные точки —
> ревью-гейты (пауза, characterization, перфоманс), а не точки вероятной остановки.

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
- Shaped-путь проверяет feasibility через runtime `CanPlace`, поэтому batch+shaped сегодня
  запрещен жестким guard в `PlanEntry`, а не capability.

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
- `UniversalInventory.TrySwapSlots` все еще меняет `BaseSlot.Stack` напрямую, но основной
  planner/executor pipeline этот метод уже не вызывает и внутренних потребителей у него нет.
  Поскольку публичного релиза еще не было, метод следует удалить без deprecation-периода.

### 2.4 Candidate/selection механика

Механизм «порядок кандидатов + выбор» уже существует и является публичным extension point, но его
контракт целиком слотовый:

- `IAcceptanceStrategy.GetSlotCandidates` возвращает eager `SlotAcceptanceCandidates`
  (список `(ISlot, RemainingCapacity)` + `CanCreateNewSlot`), и `AllocateViaCandidatesLoop`
  перестраивает этот список заново на каждой итерации аллокации;
- `SlotSelectionPolicyBase.Select` умеет выразить только `Existing(slot)` / `New()` / `None` —
  кандидата «создать placement с anchor A в ориентации O» в модели нет;
- rules проверяются дважды: внутри стратегии (`PassesRules` в `GetSlotCandidates`) и в
  планировщике (`IsCandidateAllowedByRules`);
- merge-first семантика размазана между порядком кандидатов стратегии и
  `StackFirstSlotSelectionPolicy` — два источника порядка.

Унификация обязана обобщить этот механизм (см. 4.4), иначе единый аллокатор упрется в слотовый
контракт на полпути.

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

**Инвариант переходного периода:** пока `VirtualSlotState` и `PlacementPlanningState` сосуществуют
(этапы 1-7), один план никогда не ведет два несинхронизированных виртуальных состояния одного
инвентаря. Конкретно:

- на slot-инвентарях до этапа 7 работает только `VirtualSlotState`;
- на grid-инвентарях начиная с этапа 3 `VirtualSlotState` становится view поверх
  `PlacementPlanningState`: чтение occupancy и `Apply` проксируются в резервы planning state,
  поэтому single-cell и shaped entries одного batch видят одни и те же брони;
- запрещено состояние, в котором slot-аллокация зарезервирована в одном представлении, а
  shaped-поиск читает другое.

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
GetReservedAmount(placement)
```

`GetReservedAmount` сознательно заменяет `GetRemainingMergeCapacity`: planning state не знает про
max stack и stacking semantics — это зона стратегии. Remaining merge capacity вычисляет аллокатор:
`strategyCap(item) - placement.Stack.Count - planningState.GetReservedAmount(placement)`.

Требования:

- использует ту же `IInventoryTopology`, что и runtime `PlacementStore`;
- видит реальные placement и все ранее запланированные изменения;
- не мутирует inventory, slots и runtime `PlacementStore`;
- для same-inventory move освобождает source footprint ровно один раз;
- поддерживает последовательный deterministic greedy planning для batch;
- позволяет planner и acceptance использовать один и тот же алгоритм dry-run;
- доступен на чтение swap-планированию: `TryPlanSwap` для entry N обязан учитывать резервы
  entries 1..N-1, а не runtime-состояние.

Необязательно сразу делать публичный интерфейс. На первом этапе это internal-модель. Сосуществование
со старым virtual-slot view регулируется инвариантом из раздела 3.

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
потребителей. Поскольку публичного релиза еще не было, внешних потребителей нет и compatibility
wrapper не требуется — тип удаляется сразу. Если к моменту этапа 7 релиз уже состоится, тогда
оставить obsolete wrapper на один релиз.

### 4.3 Роли strategy, rules и geometry

Стратегия отвечает за:

- разрешение create/merge/reject;
- max stack и remaining capacity (caps; учет уже запланированного — через аллокатор и
  `GetReservedAmount`);
- one-per-ID и separable-stack semantics;
- порядок кандидатов: hinted, merge-first, empty-first и другие policy;
- допустимость создания dynamic slot.

Rules отвечают за конкретный item, amount и target anchor. Rules проверяет **только аллокатор,
один раз** — из кандидатов стратегии проверка rules убирается (сейчас она задублирована, см. 2.4).
Это сознательное изменение контракта `IAcceptanceStrategy`.

`PlacementPlanningState` отвечает только за:

- проекцию shape через topology;
- bounds;
- occupancy;
- резервирование planned footprint и учет reserved amounts.

Стратегия не должна самостоятельно реализовывать геометрию, а `CanPlace` не должен принимать
решения о stacking или unique semantics.

### 4.4 Placement-candidate модель и selection policy

Существующий цикл `GetSlotCandidates -> Select -> Apply -> повторить` структурно совпадает с
целевым алгоритмом 4.5 — меняются типы, а не схема. Кандидат обобщается со слота до placement:

```text
PlacementCandidate
    Kind: MergeExisting | CreateAt | NewDynamicSlot
    AnchorIndex          (для NewDynamicSlot отсутствует)
    Orientation
    Capacity             (cap стратегии без учета planned-резервов)
```

Контракт:

- стратегия отдает **ленивый упорядоченный поток** кандидатов (struct enumerator или callback),
  а не eager `List`: текущая перестройка полного списка на каждой итерации недопустима на grid
  (`O(anchors * footprint)` на каждую аллокацию и каждый hover);
- источник порядка один — стратегия; selection policy (`SlotSelectionPolicyBase` ->
  placement-selection policy) остается pluggable extension point, но работает как выбор/фильтр
  поверх потока, а не второй источник сортировки;
- rules в кандидатах не проверяются (см. 4.3);
- геометрия проверяется лениво аллокатором через `PlacementPlanningState` только для реально
  рассмотренных кандидатов;
- `SlotSelection.New()` отображается в `NewDynamicSlot`;
- для slot-топологии кандидаты вырождаются в текущее поведение (anchor == slot index,
  единственная ориентация), что позволяет мигрировать стратегии без изменения semantics.

### 4.5 Один аллокатор

Целевой алгоритм:

```text
PlanEntry(entry, policy, target, hint, planningState):
    1. resolve converted item, shape, orientation and requested amount
    2. release source placement in planningState for a same-inventory move
    3. if hinted target is occupied and an occupied-slot handler claims the drop:
         - plan OccupiedHandler operation (priority over swap and alternative search)
    4. ask strategy for ordered placement candidates (lazy stream)
    5. for each candidate:
         - capacity = strategy cap - existing count - planningState reserved amount
         - validate rules for concrete anchor and amount
         - validate geometry through planningState
         - reserve merge or footprint in planningState
         - append PlannedPlacementAllocation
    6. if hinted target is blocked and policy requests Swap:
         - TryPlanSwap reading planningState (not runtime occupancy)
    7. apply AllowPartial, BatchMode and DragAmountStep semantics
```

Аллокатор не ветвится по `IsSingleCell`. Отличия выражаются topology, candidate policy и strategy
capabilities.

Шаг 3 фиксирует сознательное решение: occupied-handler распространяется и на shaped items с тем же
приоритетом, что сегодня на single-cell пути (сейчас shaped grid items до него не доходят — это
behavioral change, его покрывает characterization этапа 0).

Шаг 6 — не «та же строка, что сейчас»: swap-планирование получает read-доступ к
`PlacementPlanningState`, иначе swap для entry N в batch проверяет устаревшее состояние.

### 4.6 Поведения, которые единый аллокатор обязан поглотить

Главный риск миграции single-cell пути — не геометрия, а накопленные специальные поведения.
Чеклист (каждое — отдельный characterization-тест до миграции и contract-тест после):

1. deferred placement через `TryAddStack(-1)` для динамического инвентаря с нулем слотов
   (inventory-area drop без target hint);
2. deferred-fallback, когда virtual allocation ничего не нашла, но инвентарь сообщает capacity
   через `potentialNewSlots`;
3. различие legacy deferred (`TryAddStack(-1)`) и policy-driven `RequiresNewSlot`;
4. `DragAmountStep`: округление и per-allocation подгонка (`ApplySourceDragAmountStep`);
5. hint-only entries (`PlanHintOnlyEntry`), когда альтернативы недостижимы;
6. приоритет occupied-slot handler над blocked-target поведением;
7. swap fallback при `plannedAmount == 0`;
8. синтетические virtual slots при планировании создания слотов (one-per-ID не должен
   запланировать второй новый слот для того же item);
9. исключение source slot/placement при same-inventory move;
10. `EnsureFreeSlots` / potentialNewSlots accounting для dynamic инвентарей.

Single-cell fast path после миграции допустим **только как early-out внутри единого аллокатора**
(footprint из одной ячейки), а не как параллельная семантическая ветка.

### 4.7 Acceptance через тот же dry-run

`GetAcceptableCount` не должен иметь отдельный упрощенный алгоритм упаковки shaped items.
Иначе preview и реальный planner со временем разойдутся.

Нужен общий internal allocation service:

- planner запрашивает аллокации и получает полный результат;
- acceptance запускает тот же dry-run и возвращает сумму `Amount`;
- `CanAcceptItem` берет первый реально допустимый existing anchor либо сообщает возможность
  `NewDynamicSlot`.

Для вызова без target hint используется deterministic scan anchors в strategy order. Это greedy
оценка, а не поиск оптимальной упаковки. Такой контракт должен быть явно задокументирован.

Acceptance является горячим preview-путем, поэтому общий алгоритм не означает обязательное создание
полного `TransferPlan` и списков аллокаций. Нужен облегченный режим того же allocation service:

- работает через reusable buffers/value types и не создает GC-нагрузку пропорционально числу
  anchors на каждый hover;
- может остановиться после достижения `DesiredCount`;
- возвращает только count и первый допустимый target, если полные аллокации не запрошены;
- кэширует как минимум shape/orientation offsets;
- кэшировать **результат** на время drag можно только при наличии надежной версии occupancy/rules
  (version counter на `PlacementStore`/инвентаре). Такой версии сейчас нет, и ее введение —
  отдельная подзадача; до нее кэш ограничивается shape/offsets, а результат пересчитывается.

До миграции acceptance нужны baseline-профили на типичных и больших grid. Совпадение semantics с
planner обязательно, но конкретная внутренняя форма результата и объем вычислений могут отличаться.

---

## 5. Что не требуется унифицировать

1. **UI и rendering.** Drag visual, overlay и подсветка legitimately используют размер и форму.
2. **Доменные rules.** Правило может сознательно запрещать multi-cell item.
3. **Оптимизации.** Быстрый путь для footprint из одной ячейки допустим как early-out внутри
   общего аллокатора, если результат совпадает с общей placement semantics.
4. **Безусловный multi-entry spatial planning.** Batch+shaped входит в scope (этап 3), но strategy
   capability может его явно запретить для конкретной стратегии. Это должно быть явным решением
   capability, а не скрытым `IsShaped` guard в planner.
5. **Оптимальная упаковка.** Первая версия использует deterministic greedy reservation. Полный
   bin-packing/backtracking не входит в scope.

Цель - убрать разные бизнес-правила переноса, а не добиться буквального отсутствия всех проверок
формы в кодовой базе.

---

## 6. Этапы реализации

Контрольные точки A и B — **ревью-гейты**: пауза, прогон characterization, сравнение перфоманса с
baseline, фиксация решений. Они не означают «вероятно, остановимся здесь»: целевой scope — полная
консолидация.

### Этап 0. Characterization и API audit

- Зафиксировать существующее поведение slot/grid для stackable, separable, unique.
- Добавить тесты для partial, same-inventory move, conversion и dynamic slot.
- Добавить swap event snapshot test.
- Покрыть тестами каждый пункт чеклиста 4.6.
- Зафиксировать observable `FailureReason` строки `PlannedEntryTransfer` для shaped и single-cell
  путей: унификация их изменит, и это должно быть сознательно.
- Удалить неиспользуемый `UniversalInventory.TrySwapSlots`: до первого релиза compatibility и
  migration path для этого API не требуются.
- Проверить внешних потребителей `PlannedSlotAllocation` и `PlannedPlacementAllocation`.
- Зафиксировать точные observable результаты `CanAcceptItem` / `GetAcceptableCount`, включая shaped,
  greedy order и suggested slot, чтобы изменение контракта было сознательным.
- Снять baseline allocations/time для acceptance preview на малых и больших grid, включая
  batch-сценарии.

### Этап 1. Виртуальное placement-состояние

- Ввести `PlacementPlanningState` с API из 4.1 (включая `GetReservedAmount` вместо
  capacity-методов).
- Инициализировать его реальными placement.
- Добавить тесты резервирования пересекающихся footprint, освобождения source и reserved amounts.
- Пока не менять публичные планы и executor.

### Этап 2. Shaped alternative search

- Перевести поиск альтернативного anchor для shaped на `PlacementPlanningState`
  (вместо runtime `CanPlace`).
- Дать `TryPlanSwap` read-доступ к planning state для shaped-пути.
- Сохранить strategy ordering и rules.
- Добавить тесты: occupied hint, свободный регион, отсутствие региона, same-inventory source release.

### Этап 3. Batch + shaped

- Снять `IsBatchDrag`-guard для shaped в `PlanEntry`; все entries одного плана разделяют один
  `PlacementPlanningState` per target inventory.
- На grid-инвентарях перевести `VirtualSlotState` в view поверх `PlacementPlanningState`
  (инвариант раздела 3): single-cell и shaped entries одного batch видят одни брони.
- Ввести strategy capability для явного запрета multi-entry spatial planning (вместо guard).
- Тесты: смешанный batch (single-cell + shaped) на grid, atomic и best-effort при частичной
  геометрической невместимости, partial-учет по нескольким entries, ориентации per entry.

### Контрольная точка A (ревью-гейт)

- shaped alternative placement и batch reservation работают и покрыты тестами;
- preview и execution не расходятся;
- производительность в пределах baseline;
- зафиксированы решения по behavioral changes (occupied-handler для shaped, FailureReason).

### Этап 4. Placement-candidate модель

- Ввести `PlacementCandidate` и ленивый поток кандидатов (4.4).
- Перевести `SlotSelectionPolicyBase` на выбор поверх placement-кандидатов; для slot-топологии
  поведение бит-в-бит прежнее.
- Убрать проверку rules из кандидатов стратегий; rules проверяет только аллокатор.
- Зафиксировать единственный источник порядка (стратегия) и роль policy как выбора/фильтра.
- Contract-тесты: порядок кандидатов прежний для всех built-in стратегий и policies.

### Этап 5. Универсальная модель аллокаций

- Расширить `PlannedPlacementAllocation` полями target/operation kind.
- Перевести `PlannedEntryTransfer` на список аллокаций.
- Добавить `NewDynamicSlot`.
- Временно адаптировать старые slot allocations в новый формат.
- Выполнить этап как отдельную структурную миграцию без одновременного изменения selection
  semantics.
- Отдельно проверить partial accounting, `TransferExecutionSummary`, `ExecutedTransferEntry`,
  domain contexts и deferred add/remove events для нескольких аллокаций одного entry.

### Этап 6. Унифицированный executor

- Исполнять `Create` через `TryPlace` для любой topology.
- Исполнять `Merge` через placement stack.
- Для `NewDynamicSlot` сначала создавать слот, затем строить anchor и выполнять `TryPlace`.
- Сохранить snapshots, domain validation, conversion и deferred events.
- Удалить shaped-only guard из `TryAddToTargetPlacement`.

### Контрольная точка B (ревью-гейт)

- единый execution contract работает для slot и grid topology;
- весь чеклист 4.6 покрыт contract-тестами и проходит;
- перфоманс executor в пределах baseline.

### Этап 7. Миграция single-cell planner

- Перевести strategy/unique allocation на общий аллокатор (4.5) с placement-кандидатами (4.4).
- Использовать один planning state для всех entries одного плана; slot-инвентари переходят с
  `VirtualSlotState` на `PlacementPlanningState`.
- Прогнать чеклист 4.6 целиком.
- После стабилизации удалить `PlannedSlotAllocation`, `VirtualSlotState` и старые executor
  branches.

### Этап 8. Унификация acceptance

- Перевести `GetAcceptableCount` и `CanAcceptItem` на dry-run общего аллокатора.
- Удалить `CanAcceptShape` как routing guard.
- Проверить, что preview capacity совпадает с реально построенным планом.
- Использовать облегченный no-plan/no-list режим allocation service и подтвердить отсутствие
  регрессии по baseline allocations/time.
- Если общий dry-run слишком дорог, допускается специализированный preview fast path при общей
  contract-тестовой матрице с planner.

### Этап 9. Зачистка

- Удалить оставшиеся semantic `IsSingleCell` branches из planner/executor/acceptance.
- Убедиться, что batch ограничивается только явной capability, без shape-hardcode.
- Оставить UI, rules и доказанные early-out fast paths внутри общего аллокатора.

### Этап 10. Документация

- Обновить `.agents/skills/dragdrop-*` и зеркальные `.claude/skills/dragdrop-*`.
- Исправить устаревшее описание swap в `DATA_FLOW.md` и `COMPONENTS.md`.
- Обновить публичную документацию по strategy/acceptance extension points, включая новый
  placement-candidate контракт `IAcceptanceStrategy` и selection policies.

Каждый этап должен отдельно компилироваться и проходить соответствующий test subset.

---

## 7. Риски и проверки

### Корректность

- planned footprint должен учитывать все предыдущие аллокации текущего плана;
- source placement нельзя освобождать повторно при нескольких allocations одного entry;
- covered-cell interaction должен резолвиться в логический placement/anchor;
- conversion может изменить shape, поэтому geometry проверяется по converted adapter;
- merge не создает новый footprint, но меняет reserved amount в planning state;
- same-inventory swap должен проверять совместимость двух результирующих footprint;
- swap-планирование читает planning state, а не runtime occupancy;
- atomic rollback и события должны сохранять placement snapshots;
- два виртуальных состояния одного инвентаря в одном плане запрещены (инвариант раздела 3);
  на этапах 3-6 grid-инвентари работают через planning-state-backed view.

### Изменение контракта стратегий

- удаление rules-проверок из кандидатов (4.3/4.4) меняет контракт `IAcceptanceStrategy`:
  кастомные стратегии, полагавшиеся на двойную проверку, должны быть найдены в audit этапа 0;
- порядок кандидатов built-in стратегий фиксируется contract-тестами до миграции (этап 4);
- occupied-handler для shaped — сознательный behavioral change, фиксируется на гейте A.

### Dynamic slots

- unknown anchor допустим только как `NewDynamicSlot`;
- после создания слота executor обязан проверить topology и выполнить `TryPlace`;
- rollback должен удалить созданный слот через восстановление snapshot;
- создание/удаление слота может сдвигать индексы; rollback не должен оставлять index-keyed
  DataBinding с ключами на уже другие logical slots;
- characterization должен покрывать создание слота, rollback, повторное использование source slot
  и последующую реиндексацию;
- grid inventory продолжает запрещать dynamic slot management, пока не появится отдельная модель
  расширяемой spatial topology.

### Производительность

- anchor scan имеет стоимость `O(anchorCount * footprintSize)`;
- offsets shape/orientation можно кэшировать;
- planning state должен обновлять occupancy инкрементально;
- кандидаты перечисляются лениво; eager-список на каждую итерацию аллокации запрещен (4.4);
- acceptance не должен строить Unity-объекты, мутировать inventory или аллоцировать коллекции на
  каждый anchor/hover после прогрева;
- result-кэш acceptance требует version counter occupancy/rules — до его появления только
  offsets-кэш (4.7);
- до и после этапа 8 обязательны benchmark/Profiler сравнения на representative grid sizes;
- backtracking не требуется в первой версии.

### Тестовая матрица

- slot и grid topology;
- single-cell и multi-cell;
- stackable, separable и unique;
- create, explicit merge, auto merge, alternative, swap;
- full и partial stack;
- same-inventory и cross-inventory;
- conversion с неизменной и измененной shape;
- batch best-effort и atomic, включая смешанный single-cell + shaped batch;
- dynamic slot create/rollback;
- drop на anchor и covered cell;
- placement snapshots в add/remove/swap events;
- каждый пункт чеклиста 4.6.

---

## 8. Критерии готовности

### Гейт A: этапы 0-3

- `PlacementPlanningState` корректно резервирует footprint нескольких planned operations.
- Shaped alternative search использует strategy order, rules и виртуальную topology.
- Batch+shaped работает: смешанные entries разделяют один planning state, atomic/best-effort
  корректны.
- Preview, planner и executor согласованы для добавленных shaped-сценариев.
- Нет регрессии acceptance/hover по baseline performance.

### Полная консолидация: этапы 4-10

- Planner использует один topology-aware allocation service.
- Все entries одного плана разделяют `PlacementPlanningState`.
- Single-cell и shaped создают одинаковый тип списка аллокаций.
- Strategy, rules и geometry имеют раздельные обязанности; rules проверяются один раз.
- Кандидаты — placement-кандидаты, отдаются лениво, selection policies работают поверх них.
- Acceptance capacity вычисляется dry-run того же аллокатора.
- Executor выполняет create/merge через placement API для slot и grid topology.
- Нет `IsSingleCell` branches, меняющих transfer semantics; UI/rules/early-out fast paths
  разрешены.
- `VirtualSlotState`, `PlannedSlotAllocation` и `CanAcceptShape` удалены.
- Неиспользуемый direct swap API удален; swap доступен только через placement-safe pipeline.
- Batch ограничивается только явной strategy capability.
- Все characterization и новые topology/batch/event тесты проходят, чеклист 4.6 покрыт
  contract-тестами.
- Architecture skills и публичная документация соответствуют реализации.

---

*Основные затрагиваемые области: `TransferPlanner`, `TransferPlanExecutor`, `PlacementStore`,
`InventoryTopology`, `UniversalInventory`, `InventoryAcceptanceRequest`, `VirtualSlotState`,
`InventoryStrategyBase`, `IAcceptanceStrategy`, `SlotAcceptanceCandidate`, `SlotSelectionPolicy`,
concrete strategies и `Core/Drop/*AlternativePlacementStrategy`.*
