# Unified Placement Planning - Design Plan

**Цель:** перестать различать single-cell и shaped в transfer semantics. Single-cell должен быть
вырожденным случаем footprint из одной ячейки, а планирование, acceptance и выполнение должны
использовать одну placement-модель.

При этом доступность места определяется не одним `CanPlace`, а сочетанием трех независимых проверок:

1. стратегия определяет eligibility create/merge, capacity и фазы кандидатов; итоговый выбор —
   за selection policy (4.4);
2. rules проверяют конкретную операцию и целевой anchor;
3. placement state проверяет топологию, границы и occupancy.

> Статус: **план, runtime-код не меняется.** Реализация выполняется отдельными компилируемыми
> этапами после ревью документа.
>
> Решение по scope (2026-06-11): целевой scope — **полная консолидация** (этапы 0-9), включая
> batch+shaped и миграцию single-cell пути. Публичного релиза еще не было — это единственный
> момент, когда двойной путь можно убрать без compatibility-обязательств. Контрольные точки —
> ревью-гейты (пауза, characterization, перфоманс), а не точки вероятной остановки.
>
> Ревью-итерация 2 (2026-06-11): добавлены per-entry транзакции planning state,
> multi-inventory `PlacementPlanningSession`, planning placement handles, уточнено владение
> candidate order (фазовый контракт), переходная маршрутизация по **топологии** вместо моста
> `VirtualSlotState -> PlacementPlanningState`, общий `PlacementPlanningRequest` для acceptance.
>
> Ревью-итерация 3 (2026-06-11): семантика зависимостей entries в BestEffort (transitive skip),
> `AllocationId`/`TargetReference` для всех Create, участие slot-инвентаря в session при
> cross-topology swap.

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
- Виртуальное состояние не транзакционно: при отклонении entry (например,
  `AllowPartial=false` после частичной аллокации) `Apply`-мутации `VirtualSlotState` не
  откатываются — отклоненный entry оставляет фантомные резервы для последующих entries batch.
- Shaped-путь проверяет feasibility через runtime `CanPlace`, поэтому batch+shaped сегодня
  запрещен жестким guard в `PlanEntry`, а не capability.

### 2.2 Стратегии и acceptance

- `GetSlotCandidates`, `GetAcceptableCount` и `CanUseAlternativeSlot` в основном рассуждают через
  `slot.IsEmpty`.
- Эти методы корректно выражают stack/unique semantics, но не доказывают, что shape помещается в
  топологию.
- `CanAcceptShape` отдельно отсекает multi-cell для grid, потому что общий acceptance-путь пока не
  умеет планировать placement без заранее заданного anchor.
- `GetAcceptableCount` не получает полный набор planner inputs: policy, global rules, конкретный
  hint и blocked-target semantics в acceptance-контракт не входят.

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
- владение порядком размазано: стратегия возвращает кандидатов в порядке слотов, а
  `StackFirstSlotSelectionPolicy` сама переупорядочивает, предпочитая занятый stack пустому слоту.

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

**Инвариант переходного периода: маршрутизация по топологии, а не по форме.** Каждый инвентарь
обслуживается ровно одним pipeline:

- grid-топология переходит на унифицированный путь целиком — включая single-cell как footprint
  1x1 — на этапе 4;
- slot-топология остается на старом `VirtualSlotState`-пути до этапа 6;
- два виртуальных состояния одного инвентаря в одном плане запрещены. Смешанный batch
  (single-cell + shaped) на grid не требует моста между состояниями, потому что все его entries
  идут через один pipeline;
- маршрутизация выбирает pipeline планирования для **target**-инвентаря. Slot-инвентарь может
  получить `PlacementPlanningState` как участник плана с grid-целью (reverse-сторона
  cross-topology swap) уже на этапе 4: в таком плане он не обслуживается старым pipeline,
  поэтому инвариант не нарушается.

---

## 4. Целевая архитектура

### 4.1 `PlacementPlanningSession` и `PlacementPlanningState`

План затрагивает больше одного инвентаря: swap размещает предметы в обе стороны (текущий
`TryPlanSwap` уже проверяет геометрию обеих сторон), batch может содержать разные source
inventories, а same-inventory release живет в инвентаре источника. Поэтому виртуальное состояние
ведется per inventory и собирается в session:

```text
PlacementPlanningSession
    GetState(inventory) -> PlacementPlanningState     // лениво, по требованию
    BeginEntry() -> checkpoint                        // охватывает все states session
    Commit(checkpoint)
    Rollback(checkpoint)

PlacementPlanningState
    GetPlacementAt(cellOrAnchor)                      // реальный или planned
    CanPlace(request, ignoredPlacements)
    TryReserveCreate(allocation) -> AllocationId      // у ЛЮБОГО Create, вкл. известный anchor
    TryReserveMerge(target, amount)                   // target: TargetReference (4.2)
    ReleaseSourcePlacement(placement)
    GetReservedAmount(target)                         // target: TargetReference (4.2)
```

**Per-entry транзакции.** Source placement освобождается до завершения планирования entry, а
аллокации резервируются по ходу подбора кандидатов. Если entry в итоге отклоняется
(`AllowPartial=false`, нет кандидатов, swap не сложился), `Rollback(checkpoint)` обязан убрать все
следы: восстановить released source и снять резервы. Это заодно чинит существующую утечку
`VirtualSlotState` (см. 2.1), при которой отклоненный entry оставляет фантомные резервы.

**Allocation handles.** `TryReserveCreate` возвращает стабильный `AllocationId` — для **любого**
`Create`, включая `CreateAt` на известном grid-anchor, а не только для `NewDynamicSlot`: иначе
последующий entry не сможет сослаться на созданный placement. Handle нужен, потому что
последующий entry того же плана должен уметь:

- увидеть placement, запланированный предыдущим entry (`GetPlacementAt`);
- merge-иться в него (`TryReserveMerge` через `TargetReference.PlannedAllocation`);
- соблюдать one-per-ID с учетом planned placements;
- ссылаться на еще не созданный dynamic slot, у которого нет anchor index до выполнения.

`AllocationId` уникален в пределах session; принадлежность инвентарю отслеживает session,
составной ключ `(inventory, id)` не нужен. Executor получает соответствие
`AllocationId -> реальный placement/slot` по мере исполнения (для `NewDynamicSlot` — после
создания слота). Handles заменяют сегодняшние synthetic virtual slots.

`GetReservedAmount` сознательно заменяет `GetRemainingMergeCapacity`: planning state не знает про
max stack и stacking semantics — это зона стратегии. Remaining merge capacity вычисляет аллокатор
(см. 4.5).

Требования:

- использует ту же `IInventoryTopology`, что и runtime `PlacementStore`;
- видит реальные placement и все ранее закоммиченные изменения session;
- не мутирует inventory, slots и runtime `PlacementStore`;
- для same-inventory move освобождает source footprint ровно один раз (повторный release внутри
  одного entry — ошибка);
- поддерживает последовательный deterministic greedy planning для batch;
- позволяет planner и acceptance использовать один и тот же алгоритм dry-run;
- swap-планирование читает planning states обеих сторон через session, а не runtime occupancy.

### 4.2 Универсальная аллокация

У entry должен быть список placement-аллокаций, а не одна shaped-аллокация или отдельный список
slot-аллокаций:

```text
PlannedPlacementAllocation
    AllocationId         // есть у каждого Create (включая CreateAt на известном anchor)
    OperationKind: Create | Merge
    Target: TargetReference
    Orientation
    Shape
    Amount

TargetReference
    ExistingPlacement(anchorIndex)     // реальный placement / свободный anchor
    PlannedAllocation(allocationId)    // placement, создаваемый ранее в этом плане
    NewDynamicSlot                     // identity — AllocationId создающей аллокации
```

Правила:

- single-cell - обычный `Create`/`Merge` с footprint из одной ячейки;
- shaped stack может иметь несколько аллокаций, если стратегия допускает несколько placement;
- merge в placement, запланированный предыдущим entry того же плана, ссылается на него через
  `TargetReference.PlannedAllocation`, а не по anchor;
- `NewDynamicSlot` хранит намерение создать слот, потому что его реальный index появится только в
  executor; идентичность до выполнения — `AllocationId`;
- grid + dynamic slots пока запрещен существующей конфигурацией, поэтому unknown anchor нужен только
  slot topology;
- swap и occupied-handler остаются отдельными типами planned operation, а не маскируются под
  обычную аллокацию.

Перед удалением `PlannedSlotAllocation` нужно проверить его публичную доступность и внешних
потребителей. Поскольку публичного релиза еще не было, внешних потребителей нет и compatibility
wrapper не требуется — тип удаляется сразу. Если к моменту этапа 6 релиз уже состоится, тогда
оставить obsolete wrapper на один релиз.

### 4.3 Роли strategy, rules и geometry

Стратегия отвечает за:

- разрешение create/merge/reject (eligibility);
- max stack и remaining capacity (caps; учет уже запланированного — через аллокатор и
  `GetReservedAmount`);
- one-per-ID и separable-stack semantics, включая учет planned placements;
- фазовую структуру потока кандидатов (4.4) и default selection policy;
- допустимость создания dynamic slot.

Rules отвечают за конкретный item, amount и target anchor. Rules проверяет **только аллокатор,
один раз** — из кандидатов стратегии проверка rules убирается (сейчас она задублирована, см. 2.4).
Это сознательное изменение контракта `IAcceptanceStrategy`.

`PlacementPlanningState` отвечает только за:

- проекцию shape через topology;
- bounds;
- occupancy;
- резервирование planned footprint и учет reserved amounts;
- транзакционность (checkpoint/rollback).

Стратегия не должна самостоятельно реализовывать геометрию, а `CanPlace` не должен принимать
решения о stacking или unique semantics.

### 4.4 Placement-candidate модель и selection policy

Существующий цикл `GetSlotCandidates -> Select -> Apply -> повторить` структурно совпадает с
целевым алгоритмом 4.5 — меняются типы, а не схема. Кандидат обобщается со слота до placement:

```text
PlacementCandidate
    Kind: Merge | CreateAt | NewDynamicSlot
    Target: TargetReference   (Merge: existing или PlannedAllocation; CreateAt: anchor)
    Orientation
    Capacity             (cap стратегии минус текущее содержимое placement;
                          planned-резервы НЕ учтены — их вычитает аллокатор)
```

Владение порядком (разрешение текущей размазанности, см. 2.4):

- **стратегия** определяет eligibility, capacity и **фазовую структуру** потока: кандидаты
  отдаются ленивыми фазами (`Merge`, затем `CreateAt`, затем `NewDynamicSlot`); внутри
  фазы порядок — deterministic enumeration (topology scan / порядок слотов);
- **selection policy** определяет итоговый выбор: предпочтение между фазами и выбор внутри фазы.
  Policy может short-circuit, но не может требовать полной материализации потока — буферизация
  всех anchors запрещена;
- текущее поведение выражается без потерь: `FirstSlotSelectionPolicy` = первый кандидат первой
  непустой фазы; `StackFirstSlotSelectionPolicy` = предпочесть фазу `Merge` фазе
  `CreateAt`. Бит-в-бит совместимость фиксируется contract-тестами;
- стратегия предоставляет default policy (как сейчас `DefaultSlotSelectionPolicy`);
  request-level override (`InventoryAcceptanceRequest.SelectionPolicy`) сохраняется;
- rules в кандидатах не проверяются (4.3);
- геометрия проверяется лениво аллокатором через `PlacementPlanningState` только для реально
  рассмотренных кандидатов;
- для slot-топологии кандидаты вырождаются в текущее поведение (anchor == slot index,
  единственная ориентация), что позволяет мигрировать стратегии без изменения semantics.

### 4.5 Один аллокатор

Целевой алгоритм:

```text
PlanEntry(entry, policy, target, hint, session):
    checkpoint = session.BeginEntry()
    1. resolve converted item, shape, orientation and requested amount
    2. release source placement in its inventory's state for a same-inventory move
    3. if hinted target is occupied and an occupied-slot handler claims the drop:
         - plan OccupiedHandler operation (priority over swap and alternative search)
    4. ask strategy for phased placement candidates (lazy stream, 4.4)
    5. for each candidate chosen by selection policy:
         - capacity = candidate.Capacity - state.GetReservedAmount(target)
         - validate rules for concrete anchor and amount
         - validate geometry through the planning state
         - reserve merge or footprint (new AllocationId for created placements)
         - append PlannedPlacementAllocation
    6. if hinted target is blocked and policy requests Swap:
         - TryPlanSwap reading planning states of BOTH inventories via session
    7. apply AllowPartial, BatchMode and DragAmountStep semantics
       (trim уменьшает резервы до Commit — остаточная бронь не утекает)
    8. entry accepted -> session.Commit(checkpoint)
       entry rejected -> session.Rollback(checkpoint)
```

Аллокатор не ветвится по `IsSingleCell`. Отличия выражаются topology, candidate policy и strategy
capabilities.

Шаг 3 фиксирует сознательное решение: occupied-handler распространяется и на shaped items с тем же
приоритетом, что сегодня на single-cell пути (сейчас shaped grid items до него не доходят — это
behavioral change, его покрывает characterization этапа 0).

### 4.6 Зависимости между entries и BestEffort

Entry B может зависеть от entry A двумя способами:

- **явно**: аллокация B ссылается на `PlannedAllocation(A.AllocationId)` — merge в placement,
  создаваемый A;
- **геометрически**: резерв B использует cells, освобожденные source-release A
  (same-inventory move внутри того же плана).

Оба вида известны на этапе планирования. Session строит граф зависимостей автоматически: явные —
из `TargetReference`, геометрические — фиксацией пересечения резерва с released-регионом
конкретного entry. План хранит для каждого entry набор entries, от которых он зависит.

Семантика выполнения:

- **Atomic**: граф не используется — любой сбой откатывает весь план (как сейчас).
- **BestEffort**: если entry A падает на выполнении (domain validation, изменившееся
  runtime-состояние), все транзитивно зависимые от него entries **пропускаются**, а не
  исполняются на невалидных предпосылках (отсутствующий handle, занятая область).
- Пропущенные entries попадают в `TransferExecutionSummary` с отдельной причиной
  («dependency failed»), отличимой от собственного сбоя; deferred events для них не эмитятся.
- Перепланирование остатка плана на лету не входит в первую версию (как и backtracking, §5.5):
  пропуск детерминирован и дешев, replan-on-failure требует прогона планировщика посреди
  выполнения и отдельной модели консистентности.

### 4.7 Поведения, которые единый аллокатор обязан поглотить

Главный риск миграции существующих путей — не геометрия, а накопленные специальные поведения.
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
8. one-per-ID учет placements, запланированных предыдущими entries того же плана
   (сегодня — synthetic virtual slots; в целевой модели — planned placements c handle);
9. исключение source slot/placement при same-inventory move;
10. `EnsureFreeSlots` / potentialNewSlots accounting для dynamic инвентарей.

Пункты 4-7 и 9 актуальны уже для grid vertical slice (этап 4); пункты 1-3, 8, 10 в полном объеме
всплывают при миграции slot-топологии (этап 6).

Single-cell fast path после миграции допустим **только как early-out внутри единого аллокатора**
(footprint из одной ячейки), а не как параллельная семантическая ветка.

### 4.8 Acceptance через тот же dry-run

`GetAcceptableCount` не должен иметь отдельный упрощенный алгоритм упаковки shaped items.
Иначе preview и реальный planner со временем разойдутся.

Сегодня acceptance не получает полный набор planner inputs (см. 2.2). «Тот же dry-run» требует
общего запроса с явным режимом:

```text
PlacementPlanningRequest
    Mode: Plan | AcceptancePreview | CountOnly | FirstTargetOnly
    item, amount, context/entry, target hint
    policy (resolved), selection policy override
    RulesScope: какие слои rules участвуют (slot rules, inventory rules, global rules)
```

- `Plan` — полный результат с аллокациями;
- `AcceptancePreview` — та же логика, облегченный результат;
- `CountOnly` — только суммарный `Amount`, ранний выход по `DesiredCount`;
- `FirstTargetOnly` — первый допустимый existing anchor либо возможность `NewDynamicSlot`
  (контракт `CanAcceptItem`);
- `RulesScope` — обязательная часть контракта: без зафиксированного набора слоев rules preview и
  planner дадут разные результаты по построению. Текущие расхождения (acceptance не видит global
  rules) фиксируются characterization и устраняются сознательно.

Для вызова без target hint используется deterministic scan anchors в порядке фаз стратегии. Это
greedy оценка, а не поиск оптимальной упаковки. Такой контракт должен быть явно задокументирован.

Acceptance является горячим preview-путем, поэтому общий алгоритм не означает обязательное создание
полного `TransferPlan` и списков аллокаций. Требования к облегченным режимам:

- работают через reusable buffers/value types и не создают GC-нагрузку пропорционально числу
  anchors на каждый вызов;
- кэшируют shape/orientation offsets; **result-кэш не входит в scope.** Acceptance вызывается
  событийно (вход в зону, смена наведенной ячейки/ориентации, drop), а не per-frame, поэтому
  пересчет на каждое событие — норма. Надежная инвалидация result-кэша невозможна в принципе:
  occupancy версионируется тривиально, но rules — произвольный пользовательский код с внешними
  входами, его не версионировать;
- цена устаревшего acceptance-результата — неверная подсветка до следующего события: acceptance
  advisory, на drop план строится заново и executor перевалидирует через `TryPlace` с atomic
  rollback. Поэтому строгая инвалидация не является требованием корректности;
- fallback, если baseline после этапа 7 покажет проблему на больших grid: occupancy-only counter
  плюс задокументированное допущение «rules стабильны в пределах drag». Это сознательное решение
  по результатам профайлера, не заранее.

До миграции acceptance нужны baseline-профили на типичных и больших grid. Совпадение semantics с
planner обязательно, но конкретная внутренняя форма результата и объем вычислений могут отличаться.

---

## 5. Что не требуется унифицировать

1. **UI и rendering.** Drag visual, overlay и подсветка legitimately используют размер и форму.
2. **Доменные rules.** Правило может сознательно запрещать multi-cell item.
3. **Оптимизации.** Быстрый путь для footprint из одной ячейки допустим как early-out внутри
   общего аллокатора, если результат совпадает с общей placement semantics.
4. **Безусловный multi-entry spatial planning.** Batch+shaped входит в scope (этап 5), но strategy
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

Порядок построен по правилу «контракты до потребителей»: session и candidate-модель появляются
раньше первого vertical slice, чтобы shaped-поиск, batch и миграции не строились на временных
конструкциях и не переделывались.

### Этап 0. Characterization и API audit

- Зафиксировать существующее поведение slot/grid для stackable, separable, unique.
- Добавить тесты для partial, same-inventory move, conversion и dynamic slot.
- Добавить swap event snapshot test.
- Покрыть тестами каждый пункт чеклиста 4.7.
- Зафиксировать тестом существующую утечку виртуального состояния при отклоненном entry (2.1) —
  она исчезнет с транзакциями, и это сознательное изменение.
- Зафиксировать observable `FailureReason` строки `PlannedEntryTransfer` для shaped и single-cell
  путей: унификация их изменит, и это должно быть сознательно.
- Зафиксировать фактический rules scope acceptance-пути (какие слои rules сегодня участвуют в
  `CanAcceptItem` / `GetAcceptableCount`, а какие — только в planner).
- Удалить неиспользуемый `UniversalInventory.TrySwapSlots`: до первого релиза compatibility и
  migration path для этого API не требуются.
- Проверить внешних потребителей `PlannedSlotAllocation` и `PlannedPlacementAllocation`.
- Зафиксировать точные observable результаты `CanAcceptItem` / `GetAcceptableCount`, включая shaped,
  greedy order и suggested slot, чтобы изменение контракта было сознательным.
- Снять baseline allocations/time для acceptance preview на малых и больших grid, включая
  batch-сценарии.

### Этап 1. `PlacementPlanningSession` и planning state

- Ввести `PlacementPlanningSession` (`Dictionary<IInventory, PlacementPlanningState>`, ленивое
  создание) и `PlacementPlanningState` с API из 4.1.
- Per-entry транзакции: `BeginEntry`/`Commit`/`Rollback` на уровне session.
- Allocation handles: `TryReserveCreate -> AllocationId` для любого Create, merge и
  `GetReservedAmount` через `TargetReference`; id уникален в пределах session.
- Граф зависимостей (4.6): session фиксирует явные ссылки на `PlannedAllocation` и
  геометрические зависимости (резерв поверх released-региона другого entry).
- Инициализировать состояния реальными placement.
- Тесты: пересекающиеся footprint, rollback восстанавливает released source и снимает резервы,
  reserved amounts по real и planned targets, merge в planned placement, one-per-ID поверх
  planned placements, граф зависимостей (явных и геометрических).
- Пока не менять публичные планы и executor.

### Этап 2. Placement-candidate contract

- Ввести `PlacementCandidate` и ленивый фазовый поток (4.4).
- Закрепить владение порядком: стратегия — eligibility/capacity/фазы, selection policy — выбор;
  default policy у стратегии, request-level override сохраняется.
- Убрать проверку rules из кандидатов стратегий; rules проверяет только аллокатор.
- Мигрировать built-in стратегии и policies; для slot-топологии поведение бит-в-бит прежнее.
- Contract-тесты: порядок и выбор кандидатов прежние для всех built-in стратегий и policies.

### Этап 3. Универсальная модель аллокаций

- Расширить `PlannedPlacementAllocation`: `AllocationId`, `OperationKind`, `TargetReference`.
- Перевести `PlannedEntryTransfer` на список аллокаций.
- Добавить `NewDynamicSlot`.
- Временно адаптировать старые slot allocations в новый формат.
- Выполнить этап как отдельную структурную миграцию без одновременного изменения selection
  semantics.
- Отдельно проверить partial accounting, `TransferExecutionSummary`, `ExecutedTransferEntry`,
  domain contexts и deferred add/remove events для нескольких аллокаций одного entry.

### Этап 4. Grid vertical slice: единый planner + executor для grid-топологии

- Все entries с target grid-топологии идут через единый аллокатор (4.5) — включая single-cell
  как footprint 1x1. Slot-инвентари не затронуты (инвариант раздела 3).
- Shaped alternative search реализуется здесь, сразу на strategy-ordered фазовых кандидатах.
- Executor для grid: `Create` через `TryPlace`, `Merge` через placement stack; маппинг
  `AllocationId -> placement`.
- Cross-topology swap (grid-цель, slot-источник) планируется через session обеих сторон:
  slot-инвентарь получает `PlacementPlanningState` как участник (раздел 3); обычное планирование
  со slot-целью остается на старом pipeline до этапа 6.
- Occupied-handler, swap fallback (через session), `DragAmountStep`, hint-only — для grid через
  единый путь (чеклист 4.7, пункты 4-7, 9).
- Тесты: occupied hint, свободный регион, отсутствие региона, same-inventory source release,
  смена ориентации, covered-cell drop.

### Контрольная точка A (ревью-гейт)

- grid-топология полностью на едином пути; preview и execution не расходятся;
- shaped alternative search использует strategy order (фазы), rules и виртуальную topology;
- производительность в пределах baseline;
- зафиксированы решения по behavioral changes (occupied-handler для shaped, `FailureReason`,
  устранение утечки отклоненного entry).

### Этап 5. Batch на grid

- Снять `IsBatchDrag`-guard для grid-целей: все entries одного плана разделяют одну
  `PlacementPlanningSession`.
- Смешанный batch (single-cell + shaped) работает без моста — один pipeline (раздел 3).
- Ввести strategy capability для явного запрета multi-entry spatial planning (вместо guard).
- Семантика зависимостей в BestEffort (4.6): execution-сбой entry транзитивно пропускает
  зависимые entries; `TransferExecutionSummary` и события различают failed и skipped.
- Тесты: смешанный batch, atomic и best-effort при частичной геометрической невместимости,
  отклоненный entry не влияет на последующие (транзакции), execution-сбой entry пропускает
  транзитивно зависимые, partial-учет по нескольким entries, ориентации per entry, swap внутри
  batch с учетом резервов предыдущих entries.

### Этап 6. Миграция slot-топологии

- Перевести slot-инвентари (strategy/unique allocation) на единый аллокатор и session.
- `NewDynamicSlot`: создание слота в executor, затем anchor и `TryPlace`; rollback удаляет слот
  через восстановление snapshot.
- Прогнать чеклист 4.7 целиком (пункты 1-3, 8, 10 — основная нагрузка этого этапа).
- После стабилизации удалить `PlannedSlotAllocation`, `VirtualSlotState`, старые
  planner/executor branches и `TryAddToTargetPlacement`-guard.

### Контрольная точка B (ревью-гейт)

- единый execution contract работает для slot и grid topology;
- весь чеклист 4.7 покрыт contract-тестами и проходит;
- перфоманс planner/executor в пределах baseline.

### Этап 7. Унификация acceptance

- Ввести `PlacementPlanningRequest` с режимами (4.8); зафиксировать `RulesScope`.
- Перевести `GetAcceptableCount` и `CanAcceptItem` на dry-run общего аллокатора
  (`CountOnly` / `FirstTargetOnly`).
- Удалить `CanAcceptShape` как routing guard.
- Проверить, что preview capacity совпадает с реально построенным планом.
- Подтвердить отсутствие регрессии по baseline allocations/time; если общий dry-run слишком
  дорог, допускается специализированный preview fast path при общей contract-тестовой матрице
  с planner.

### Этап 8. Зачистка

- Удалить оставшиеся semantic `IsSingleCell` branches из planner/executor/acceptance и
  `AutoTransferService` (shaped-guard в выборе режима auto-transfer). Визуальные проверки формы
  (`DragAndDropManager.TrySetPlacementDraggedState`, `DropPreviewController`, drag visuals)
  остаются — они UI по §5.1.
- Убедиться, что batch ограничивается только явной capability, без shape-hardcode.
- Оставить UI, rules и доказанные early-out fast paths внутри общего аллокатора.

### Этап 9. Документация

- Обновить `.agents/skills/dragdrop-*` и зеркальные `.claude/skills/dragdrop-*`.
- Исправить устаревшее описание swap в `DATA_FLOW.md` и `COMPONENTS.md`.
- Обновить публичную документацию по strategy/acceptance extension points, включая новый
  placement-candidate контракт `IAcceptanceStrategy`, selection policies и
  `PlacementPlanningRequest`.

Каждый этап должен отдельно компилироваться и проходить соответствующий test subset.

---

## 7. Риски и проверки

### Корректность

- planned footprint должен учитывать все предыдущие закоммиченные аллокации текущего плана;
- `Rollback(checkpoint)` обязан восстанавливать released source placement и снимать все резервы
  entry — частичный откат недопустим;
- source placement нельзя освобождать повторно при нескольких allocations одного entry;
- covered-cell interaction должен резолвиться в логический placement/anchor;
- conversion может изменить shape, поэтому geometry проверяется по converted adapter;
- merge не создает новый footprint, но меняет reserved amount в planning state;
- `DragAmountStep`-trim выполняется до `Commit` внутри entry-транзакции — остаточная бронь не
  утекает (текущее консервативное поведение `VirtualSlotState` зафиксировать в characterization
  как известную утечку, см. 2.1);
- same-inventory swap должен проверять совместимость двух результирующих footprint;
- swap-планирование читает planning states обеих сторон через session, а не runtime occupancy;
- executor обязан детерминированно сопоставлять `AllocationId` реальным placement/slot;
  несколько `NewDynamicSlot` в одном плане сопоставляются по handle, а не по порядку создания;
- граф зависимостей обязан покрывать оба вида (явные `PlannedAllocation`-ссылки и геометрические
  через released-регион); пропуск зависимых в BestEffort — транзитивный, без ложных пропусков
  независимых entries;
- atomic rollback и события должны сохранять placement snapshots;
- маршрутизация переходного периода — по топологии (раздел 3): два виртуальных состояния одного
  инвентаря в одном плане запрещены.

### Изменение контракта стратегий

- удаление rules-проверок из кандидатов (4.3/4.4) меняет контракт `IAcceptanceStrategy`:
  кастомные стратегии, полагавшиеся на двойную проверку, должны быть найдены в audit этапа 0;
- фазовый контракт (4.4) перераспределяет владение порядком между стратегией и policy;
  бит-в-бит совместимость built-in комбинаций фиксируется contract-тестами на этапе 2;
- selection policy не может требовать материализации полного потока кандидатов — это часть
  контракта, иначе grid-перфоманс деградирует;
- occupied-handler для shaped — сознательный behavioral change, фиксируется на гейте A;
- `RulesScope` acceptance — сознательное выравнивание с planner, текущие расхождения фиксируются
  characterization (этап 0).

### Dynamic slots

- unknown anchor допустим только как `NewDynamicSlot` с `AllocationId`;
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
- planning state должен обновлять occupancy инкрементально; checkpoint/rollback не должны
  копировать полное состояние (журнал операций или undo-стек);
- кандидаты перечисляются лениво фазами; eager-список на каждую итерацию аллокации запрещен (4.4);
- acceptance не должен строить Unity-объекты, мутировать inventory или аллоцировать коллекции на
  каждый anchor/вызов после прогрева;
- result-кэш acceptance вне scope: только offsets-кэш и пересчет на UI-событие (4.8);
- до и после этапа 7 обязательны benchmark/Profiler сравнения на representative grid sizes;
- backtracking не требуется в первой версии.

### Тестовая матрица

- slot и grid topology;
- single-cell и multi-cell;
- stackable, separable и unique;
- create, explicit merge, auto merge, merge в planned placement, alternative, swap;
- full и partial stack;
- same-inventory и cross-inventory, включая разные source inventories в одном batch;
- conversion с неизменной и измененной shape;
- batch best-effort и atomic, включая смешанный single-cell + shaped batch;
- отклоненный entry не оставляет следов в session (транзакции);
- BestEffort: execution-сбой entry транзитивно пропускает зависимые, независимые выполняются,
  summary различает failed/skipped;
- dynamic slot create/rollback;
- drop на anchor и covered cell;
- placement snapshots в add/remove/swap events;
- каждый пункт чеклиста 4.7.

---

## 8. Критерии готовности

### Гейт A: этапы 0-4

- `PlacementPlanningSession` корректно резервирует footprint нескольких planned operations,
  per-entry rollback не оставляет следов.
- Candidate contract: built-in стратегии и policies дают прежние результаты бит-в-бит на
  slot-топологии.
- Grid-топология полностью на едином пути: single-cell и shaped создают одинаковый тип списка
  аллокаций.
- Shaped alternative search использует фазовый strategy order, rules и виртуальную topology.
- Preview, planner и executor согласованы для grid-сценариев.
- Нет регрессии acceptance/hover по baseline performance.

### Полная консолидация: этапы 5-9

- Planner использует один topology-aware allocation service; все entries одного плана разделяют
  `PlacementPlanningSession`.
- Strategy, rules и geometry имеют раздельные обязанности; rules проверяются один раз.
- Кандидаты — placement-кандидаты, отдаются ленивыми фазами; selection policies работают поверх
  них; владение порядком однозначно.
- Acceptance capacity вычисляется dry-run того же аллокатора через `PlacementPlanningRequest`
  с зафиксированным `RulesScope`.
- Executor выполняет create/merge через placement API для slot и grid topology; planning handles
  детерминированно сопоставлены реальным placement.
- Нет `IsSingleCell` branches, меняющих transfer semantics; UI/rules/early-out fast paths
  разрешены.
- `VirtualSlotState`, `PlannedSlotAllocation` и `CanAcceptShape` удалены.
- Неиспользуемый direct swap API удален; swap доступен только через placement-safe pipeline.
- Batch ограничивается только явной strategy capability.
- BestEffort выполняет независимые entries и детерминированно пропускает транзитивно зависимые
  от упавших (4.6).
- Все characterization и новые topology/batch/event тесты проходят, чеклист 4.7 покрыт
  contract-тестами.
- Architecture skills и публичная документация соответствуют реализации.

---

*Основные затрагиваемые области: `TransferPlanner`, `TransferPlanExecutor`, `PlacementStore`,
`InventoryTopology`, `UniversalInventory`, `InventoryAcceptanceRequest`, `VirtualSlotState`,
`InventoryStrategyBase`, `IAcceptanceStrategy`, `SlotAcceptanceCandidate`, `SlotSelectionPolicy`,
`AutoTransferService`, concrete strategies и `Core/Drop/*AlternativePlacementStrategy`.*
