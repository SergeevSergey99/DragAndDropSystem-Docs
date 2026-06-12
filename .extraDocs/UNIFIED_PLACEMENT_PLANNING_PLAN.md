# Unified Placement Planning - Design Plan

**Цель:** перестать различать single-cell и shaped в transfer semantics. Single-cell должен быть
вырожденным случаем footprint из одной ячейки, а планирование, acceptance и выполнение должны
использовать одну placement-модель.

При этом доступность места определяется не одним `CanPlace`, а сочетанием трех независимых проверок:

1. стратегия определяет eligibility create/merge, capacity, естественный порядок и фазовые метки
   кандидатов; итоговый способ обхода и выбор — за selection policy (4.4);
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
> Ревью-итерация 2 (2026-06-11): per-entry транзакции planning state, multi-inventory
> `PlacementPlanningSession`, переходная маршрутизация по **топологии** вместо моста
> `VirtualSlotState -> PlacementPlanningState`, общий `PlacementPlanningRequest` для acceptance.
>
> Ревью-итерация 3 (2026-06-11): семантика зависимостей entries в BestEffort (transitive skip),
> участие slot-инвентаря в session при cross-topology swap.
>
> Ревью-итерация 4 (2026-06-11): per-entry execution transaction для BestEffort, условный
> source-release для partial split, повторно перечисляемый candidate source для bit-for-bit
> совместимости policies.
>
> Ревью-итерация 5 (2026-06-12, линза расширяемости): **типизированные planned operations вместо
> закрытого union** (swap/occupied-флаги); контракт «каждая операция регистрирует свои эффекты в
> session» (swap резервирует результаты); **ссылки вместо id** (`PlanTarget`: `Placement` /
> `BaseSlot` / `PlannedPlacement` — id-слой `AllocationId`/`ExistingPlacementId`/`ExistingSlotId`/
> `KnownAnchor` удален); геометрический фасад `IPlanningGeometry` (топология владеет перечислением
> анкоров, стратегия не знает ни топологию, ни форму); `SelectionContext` + `Enumerate(kindMask)`
> для policies; поглощение `IAlternativePlacementStrategy` selection policies. Открытый вопрос:
> агрегатные правила (раздел 9).

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
- `Placement` — объект с reference identity (store хранит `HashSet<Placement>` и сравнивает через
  `ReferenceEquals`); merge мутирует stack на месте, ссылка переживает и merge, и реиндексацию
  слотов. План может адресовать цели ссылками, без id-слоя.

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

### 2.3 Executor и модель плана

- Slot allocation исполняется через `TryAddToSlot` / dynamic-slot path.
- Shaped allocation исполняется через отдельный `TryAddToTargetPlacement`.
- `PlannedPlacementAllocation` сейчас только один на entry, поэтому не покрывает распределение
  стека по нескольким placement.
- Результат планирования — **закрытый union**: swap и occupied-handler выражены bool-флагами
  (`RequiresSwap`, `RequiresOccupiedHandler`) с прибитыми данными на `PlannedEntryTransfer`.
  Любой новый вид операции (craft-on-drop, replace, выгрузка в связанный контейнер) требует
  одновременной правки модели плана, планировщика и executor.
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
  `StackFirstSlotSelectionPolicy` сама переупорядочивает, предпочитая занятый stack пустому слоту;
- параллельно существует **второй ordering-механизм** для другого контекста:
  `IAlternativePlacementStrategy` (MergeFirst/EmptyFirst/MergeOnly/EmptyOnly) перечисляет
  альтернативные слоты при заблокированном hinted-target. Это то же понятие «в каком порядке
  перебирать места», но с отдельным слотовым контрактом. Контексты вызова сегодня не пересекаются
  (blocked-hint vs area-drop/auto-transfer), однако два словаря для одного понятия — налог на
  каждую будущую стратегию и топологию.

Унификация обязана обобщить оба механизма в один (см. 4.4), иначе единый аллокатор упрется в
слотовые контракты на полпути.

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
запланированные footprint и merge capacity. Это касается **всех** операций, меняющих occupancy,
включая swap (см. 4.2): операция, которая только «читает» состояние, делает невидимыми свои
результаты для последующих entries того же плана.

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
    GetPlacementAt(cellOrAnchor) -> Placement | PlannedPlacement | null
    CanPlace(footprint, anchorSlot, ignored...)
    TryReserveCreate(anchorSlot | newDynamicSlot, footprint, item, amount) -> PlannedPlacement
    TryReserveMerge(target, amount)                   // target: Placement | PlannedPlacement
    TryReleaseSourcePlacement(placement)              // tentative; только кандидат на full move
    GetReservedAmount(target)                         // target: Placement | PlannedPlacement
```

**Ссылки, а не id.** План нигде не адресует цели сырыми индексами или session-id:

- существующий placement — это сам `Placement` (reference identity переживает merge и
  реиндексацию слотов: store работает через `ReferenceEquals`, merge мутирует stack на месте);
- anchor для Create — это `BaseSlot`-ссылка (на grid и slot топологиях ячейка = слот); индекс
  читается из слота в момент использования и служит только payload геометрии;
- запланированный placement — это объект `PlannedPlacement`, который вернул `TryReserveCreate`.
  Для `NewDynamicSlot` слот не существует до выполнения: executor создает слот и **привязывает**
  его к этому же объекту (`PlannedPlacement.Bind(...)`). Никаких словарей `id -> placement` нет —
  привязку несет сам объект;
- слот/placement, удаленный из инвентаря между планированием и выполнением, обнаруживается
  проверкой принадлежности **по ссылке** в начале entry execution transaction → entry завершается
  fail-before-mutation, а не попадает в «слот с тем же индексом».

`PlannedPlacement` заменяет сегодняшние synthetic virtual slots: последующий entry того же плана
видит его через `GetPlacementAt`, merge-ится в него (`TryReserveMerge`), one-per-ID учитывает его
наравне с реальными placement.

**Per-entry транзакции.** Source placement освобождается до завершения планирования entry, а
резервы создаются по ходу подбора кандидатов. Если entry в итоге отклоняется
(`AllowPartial=false`, нет кандидатов, swap не сложился), `Rollback(checkpoint)` обязан убрать все
следы: восстановить released source и снять резервы. Это заодно чинит существующую утечку
`VirtualSlotState` (см. 2.1), при которой отклоненный entry оставляет фантомные резервы.

`GetReservedAmount` сознательно заменяет `GetRemainingMergeCapacity`: planning state не знает про
max stack и stacking semantics — это зона стратегии. Remaining merge capacity вычисляет аллокатор
(см. 4.5).

Требования:

- использует ту же `IInventoryTopology`, что и runtime `PlacementStore`;
- видит реальные placement и все ранее закоммиченные изменения session;
- не мутирует inventory, slots и runtime `PlacementStore`;
- для same-inventory move может tentative-освободить source footprint ровно один раз, только если
  dragged stack покрывает весь текущий source placement; повторный release внутри entry — ошибка;
- перед `Commit` проверяет, что успешный entry действительно удалит source placement целиком.
  Если после partial/`DragAmountStep` остается исходный stack, tentative release откатывается и
  entry перепланируется один раз без освобождения source footprint;
- поддерживает последовательный deterministic greedy planning для batch;
- позволяет planner и acceptance использовать один и тот же алгоритм dry-run;
- swap-планирование работает через session со states обеих сторон, а не runtime occupancy.

### 4.2 Модель planned operations

Результат планирования entry — **список типизированных операций**, а не аллокации плюс bool-флаги:

```text
PlannedEntryTransfer
    Entry, RequestedAmount, PlannedAmount, FailureReason
    Operations: IReadOnlyList<PlannedOperation>

PlannedOperation (контракт)
    RegisterEffects(session)     // планирование: release/reserve в states затронутых инвентарей
    Execute(executionContext)    // внутри entry execution transaction
    Rollback(executionContext)   // откат в entry execution transaction

Встроенные операции первой версии:
    PlacementOperation
        Kind: Create | Merge
        Target: PlanTarget
        Orientation, Shape, Amount
    SwapOperation
        forward/reverse пары (placement -> результат), stacks before/after
    OccupiedHandlerOperation
        доменное действие; эффектов в session не регистрирует

PlanTarget — всегда ссылка на объект, никогда сырой индекс:
    Placement          // существующий placement (merge)
    BaseSlot           // anchor для Create в существующем слоте/ячейке
    PlannedPlacement   // placement, создаваемый ранее в этом плане; для NewDynamicSlot
                       //  executor привязывает созданный слот к этому объекту
```

**Контракт эффектов.** Каждая операция при планировании обязана зарегистрировать свои изменения
occupancy в session (`RegisterEffects`), внутри entry-транзакции:

- `PlacementOperation.Create` -> `TryReserveCreate`; `Merge` -> `TryReserveMerge`;
- move-часть entry -> tentative `TryReleaseSourcePlacement` (см. 4.1);
- `SwapOperation` -> в state target-инвентаря: `Release(targetPlacement)` + reserve
  forward-результата; в state source-инвентаря: `Release(sourcePlacement)` + reserve
  reverse-результата. Swap, который «только читает», делает свои результаты невидимыми для
  последующих entries batch: они либо double-book-ают ячейки результата, либо не видят
  освобожденные ячейки (раздел 3);
- `OccupiedHandlerOperation` — ноль эффектов.

Благодаря этому новый тип операции — это новый класс с `RegisterEffects`/`Execute`/`Rollback`,
без правки модели плана, планировщика и executor dispatch. Рёбра зависимостей (4.6) возникают
из эффектов автоматически и для будущих операций тоже.

Правила:

- single-cell — обычный `Create`/`Merge` с footprint из одной ячейки;
- shaped stack может иметь несколько `PlacementOperation`, если стратегия допускает несколько
  placement;
- merge в placement, запланированный предыдущим entry того же плана, ссылается на его
  `PlannedPlacement`-объект, а не на anchor;
- grid + dynamic slots пока запрещен существующей конфигурацией, поэтому `PlannedPlacement` без
  anchor-слота (`NewDynamicSlot`) нужен только slot topology.

Перед удалением `PlannedSlotAllocation` нужно проверить его публичную доступность и внешних
потребителей. Поскольку публичного релиза еще не было, внешних потребителей нет и compatibility
wrapper не требуется — тип удаляется сразу. Если к моменту этапа 6 релиз уже состоится, тогда
оставить obsolete wrapper на один релиз.

### 4.3 Роли strategy, rules и geometry

Стратегия отвечает за:

- разрешение create/merge/reject (eligibility);
- max stack и remaining capacity (caps; учет уже запланированного — через аллокатор и
  `GetReservedAmount`);
- one-per-ID и separable-stack semantics, включая учет `PlannedPlacement`;
- естественный deterministic order, фазовые метки кандидатов (4.4) и default selection policy;
- допустимость создания dynamic slot.

Стратегия **не знает ни топологию, ни форму предмета**. Геометрические вопросы она задает через
узкий read-only фасад:

```text
IPlanningGeometry (view поверх PlacementPlanningState + topology)
    EnumerateAnchorSlots(order)        // порядок перечисления отдает ТОПОЛОГИЯ;
                                       //  order: Natural | FromHint(slot)
    CanFit(footprint, anchorSlot)      // footprint — опаковый токен, резолвится аллокатором
                                       //  по converted item; стратегия его не интерпретирует
    GetPlacementAt(slot)               // реальный или planned
    GetReservedAmount(target)
```

Новая топология реализует свое перечисление анкоров (и проекцию форм) — стратегии не меняются.
Новая стратегия пишет semantics поверх фасада — топологии не меняются. Защита от кривых
расширений: даже если стратегия не проверила `CanFit`, `TryReserve*` перевалидирует геометрию —
некорректная стратегия не может испортить planning state, ее кандидат просто не зарезервируется.

Rules отвечают за конкретный item, amount и target anchor. Rules проверяет **только аллокатор,
один раз** — из кандидатов стратегии проверка rules убирается (сейчас она задублирована, см. 2.4).
Это сознательное изменение контракта `IAcceptanceStrategy`.

`PlacementPlanningState` отвечает только за: проекцию shape через topology, bounds, occupancy,
резервирование planned footprint, учет reserved amounts, транзакционность (checkpoint/rollback).

### 4.4 Candidate source, selection policy и единый владелец порядка

Существующий цикл `GetSlotCandidates -> Select -> Apply -> повторить` структурно совпадает с
целевым алгоритмом 4.5 — меняются типы, а не схема. Кандидат обобщается со слота до placement:

```text
PlacementCandidate
    Kind: Merge | Create | NewDynamicSlot
    Target: PlanTarget        (Merge: Placement | PlannedPlacement; Create: BaseSlot)
    Orientation
    Capacity                  (cap стратегии минус текущее содержимое placement;
                               planned-резервы НЕ учтены — их вычитает аллокатор)

PlacementCandidateSource     (отдает стратегия)
    Enumerate(kindMask)      // ленивое, повторно перечисляемое; маска фаз обязательна,
                             //  чтобы merge-проход не оплачивал геометрию Create-кандидатов

SelectionContext             (получает policy)
    Request, Hint
    Reason: AreaDrop | BlockedHint | AutoTransfer | Preview
    Geometry: IPlanningGeometry   // для metric-policies: ближайший к hint, фрагментация и т.п.
```

Владение порядком и выбором:

- **стратегия** предоставляет ленивый, повторно перечисляемый `PlacementCandidateSource` поверх
  `IPlanningGeometry`. Кандидаты имеют фазовую метку, но естественный порядок источника сохраняет
  текущую strategy/topology semantics, включая interleave merge и empty slots;
- **selection policy** определяет способ обхода источника и итоговый выбор.
  `FirstSlotSelectionPolicy` берет первый кандидат естественного потока.
  `StackFirstSlotSelectionPolicy` сначала перечисляет `Enumerate(Merge)`, при отсутствии
  результата — `Enumerate(Create | NewDynamicSlot)`;
- повторное ленивое перечисление допустимо, полная материализация и сортировка всех anchors
  запрещены. Candidate source должен быть стабильным в пределах одной попытки allocation;
- пример `empty slot 0 + mergeable slot 1` остается bit-for-bit совместимым:
  `FirstSlotSelectionPolicy` выбирает slot 0, `StackFirstSlotSelectionPolicy` — slot 1;
- стратегия предоставляет default policy (как сейчас `DefaultSlotSelectionPolicy`);
  request-level override (`InventoryAcceptanceRequest.SelectionPolicy`) сохраняется;
- rules в кандидатах не проверяются (4.3); геометрия проверяется лениво и перевалидируется при
  reserve;
- для slot-топологии кандидаты вырождаются в текущее поведение (anchor == slot, единственная
  ориентация), что позволяет мигрировать стратегии без изменения semantics.

**Один владелец порядка вместо двух механизмов.** Семейство `IAlternativePlacementStrategy`
(MergeFirst/EmptyFirst/MergeOnly/EmptyOnly) поглощается selection policies: его семантика — это
буквально способ обхода кандидатов. Контексты вызова при этом сохраняются и становятся явными
через `SelectionContext.Reason`: blocked-hint поиск (сегодня — `FindAlternativeBlockedTargetResolver`)
и targetless выбор (area-drop, auto-transfer) — разные `Reason`, и инвентарь может конфигурировать
для них разные policy. `BlockedTargetResolver` сводится к выбору **вида реакции**
(Alternative | Swap | Reject) и не владеет порядком. Кандидат-источник для `Reason=BlockedHint`
исключает заблокированный hinted-слот (текущая семантика `excludeBaseSlot`).

Будущие orientation-расширения (auto-rotate) входят аддитивно: source отдает кандидатов с разными
`Orientation`, аллокатор и policy не меняются.

`Kind` кандидата — словарь **executor'а** (что он умеет исполнять), а не стратегии. Новые виды
операций добавляются как новые `PlannedOperation` (4.2), а не расширением `Kind`.

### 4.5 Один аллокатор

Целевой алгоритм:

```text
PlanEntry(entry, policy, target, hint, session):
    checkpoint = session.BeginEntry()
    1. resolve converted item, footprint (shape+orientation) and requested amount
    2. if dragged stack covers the full source placement:
         - tentatively release source placement in its inventory's state
    3. if hinted target is occupied and an occupied-slot handler claims the drop:
         - plan OccupiedHandlerOperation (priority over swap and alternative search)
    4. ask strategy for lazy candidate source over IPlanningGeometry (4.4)
    5. for each candidate chosen by selection policy:
         - capacity = candidate.Capacity - state.GetReservedAmount(candidate.Target)
         - validate rules for concrete anchor and amount
         - reserve: TryReserveMerge(target) | TryReserveCreate(...) -> PlannedPlacement
           (reserve перевалидирует геометрию)
         - append PlacementOperation
    6. if hinted target is blocked and resolver requests Swap:
         - plan SwapOperation; RegisterEffects освобождает обе исходные placement и
           резервирует оба результата в states своих инвентарей (4.2)
    7. apply AllowPartial, BatchMode and DragAmountStep semantics
       (trim уменьшает резервы до Commit — остаточная бронь не утекает)
    8. if final amount leaves items in source placement and step 2 released it:
         - session.Rollback(checkpoint)
         - replan entry once with source footprint retained
    9. entry accepted -> session.Commit(checkpoint)
       entry rejected -> session.Rollback(checkpoint)
```

Аллокатор не ветвится по `IsSingleCell`. Отличия выражаются topology, candidate policy и strategy
capabilities.

Шаг 3 фиксирует сознательное решение: occupied-handler распространяется и на shaped items с тем же
приоритетом, что сегодня на single-cell пути (сейчас shaped grid items до него не доходят — это
behavioral change, его покрывает characterization этапа 0).

### 4.6 Зависимости между entries и BestEffort

Entry B может зависеть от entry A двумя способами:

- **явно**: операция B ссылается на `PlannedPlacement`, созданный операцией A (merge в создаваемый
  A placement, включая результаты `SwapOperation`);
- **геометрически**: резерв B использует cells, освобожденные эффектами A — source-release при
  same-inventory move или release сторон swap.

Оба вида известны на этапе планирования. Session строит граф зависимостей автоматически из
зарегистрированных эффектов (4.2): явные — из `PlanTarget`-ссылок, геометрические — фиксацией
пересечения резерва с released-регионом конкретного entry. Геометрическое ребро сохраняется только
если tentative source-release A пережил проверку full-consumption и был закоммичен; partial split
не освобождает регион и не создает такую зависимость. План хранит для каждого entry набор entries,
от которых он зависит.

Семантика выполнения:

- **Atomic**: граф не используется — любой сбой откатывает весь план (как сейчас).
- **BestEffort сохраняет атомарность отдельного entry.** Перед entry executor снимает snapshots
  всех затрагиваемых им inventories в состоянии после предыдущих успешных entries и начинает
  entry execution transaction. Domain validation и все операции entry относятся к этой transaction.
- Если хотя бы один участник entry не предоставляет rollback-capability
  (`IInventorySnapshotProvider` либо эквивалентный transaction adapter), entry помечается failed
  **до первой мутации**; его dependents затем пропускаются по обычному правилу. BestEffort не
  разрешает «частично атомарный» fallback.
- Если любая операция, conversion, domain validation или commit-операция entry завершается
  неуспешно, executor откатывает **весь entry**, включая уже выполненные операции, созданные
  dynamic slots, привязки `PlannedPlacement`, provisional outcomes/domain contexts и
  `ExecutedTransferEntry`. Частично выполненный failed entry запрещен.
- **BestEffort**: если entry A падает на выполнении (domain validation, изменившееся
  runtime-состояние), все транзитивно зависимые от него entries **пропускаются**, а не
  исполняются на невалидных предпосылках (непривязанный `PlannedPlacement`, занятая область).
- Пропущенные entries попадают в `TransferExecutionSummary` с отдельной причиной
  («dependency failed»), отличимой от собственного сбоя; deferred events не эмитятся ни для
  пропущенных, ни для откатившихся операций failed entry.
- Summary содержит per-entry результат, а не только aggregate counters:
  `EntryExecutionStatus = Succeeded | Failed | SkippedDependency`, failure reason и индексы
  непосредственных failed dependencies. Aggregate `SucceededEntries`/`FailedEntries` сохраняются,
  `SkippedEntries` добавляется отдельно и не маскируется под `FailedEntries`.
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
   (сегодня — synthetic virtual slots; в целевой модели — `PlannedPlacement`-объекты);
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

- `Plan` — полный результат с операциями;
- `AcceptancePreview` — та же логика, облегченный результат;
- `CountOnly` — только суммарный `Amount`, ранний выход по `DesiredCount`;
- `FirstTargetOnly` — первый допустимый existing anchor либо возможность `NewDynamicSlot`
  (контракт `CanAcceptItem`);
- `RulesScope` — обязательная часть контракта: без зафиксированного набора слоев rules preview и
  planner дадут разные результаты по построению. Текущие расхождения (acceptance не видит global
  rules) фиксируются characterization и устраняются сознательно.

Для вызова без target hint используется deterministic scan anchors в порядке candidate source.
Это greedy оценка, а не поиск оптимальной упаковки. Такой контракт должен быть явно задокументирован.

Acceptance является горячим preview-путем, поэтому общий алгоритм не означает обязательное создание
полного `TransferPlan` и списков операций. Требования к облегченным режимам:

- работают через reusable buffers/value types и не создают GC-нагрузку пропорционально числу
  anchors на каждый вызов; lightweight-режимы не ведут граф зависимостей и не создают
  `PlannedPlacement`-объекты сверх необходимого;
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

Порядок построен по правилу «контракты до потребителей»: session, candidate-модель и модель
операций появляются раньше первого vertical slice, чтобы shaped-поиск, batch и миграции не
строились на временных конструкциях и не переделывались.

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
- Зафиксировать порядок и результаты `IAlternativePlacementStrategy`-реализаций (MergeFirst,
  EmptyFirst, MergeOnly, EmptyOnly) для blocked-hint сценариев — они мигрируют в policies.
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
- `PlannedPlacement`-объекты: `TryReserveCreate` возвращает объект; merge и `GetReservedAmount`
  принимают `Placement | PlannedPlacement`; никаких id и индексных адресаций.
- Граф зависимостей (4.6): session фиксирует явные `PlanTarget`-ссылки на `PlannedPlacement`
  других entries и геометрические зависимости (резерв поверх released-региона другого entry).
- Инициализировать состояния реальными placement.
- Тесты: пересекающиеся footprint, rollback восстанавливает released source и снимает резервы,
  reserved amounts по real и planned targets, merge в planned placement, one-per-ID поверх
  planned placements, tentative source-release и replan partial split без release, граф
  зависимостей (явных и геометрических).
- Пока не менять публичные планы и executor.

### Этап 2. Candidate contract и geometry facade

- Ввести `IPlanningGeometry`: перечисление анкоров принадлежит топологии (`Natural`,
  `FromHint`), `CanFit` по опаковому footprint-токену.
- Ввести `PlacementCandidate` и ленивый повторно перечисляемый `PlacementCandidateSource`
  c `Enumerate(kindMask)` (4.4).
- Ввести `SelectionContext` (request, hint, `Reason`, geometry facade); policy получает контекст.
- Закрепить владение порядком: стратегия — eligibility/capacity/естественный порядок/фазовая
  метка, selection policy — способ обхода и выбор; default policy у стратегии, request-level
  override сохраняется.
- Поглотить `IAlternativePlacementStrategy` selection policies (`Reason=BlockedHint`, exclude
  hinted slot); `BlockedTargetResolver` выбирает только вид реакции (Alternative | Swap | Reject).
- Убрать проверку rules из кандидатов стратегий; rules проверяет только аллокатор.
- Мигрировать built-in стратегии и policies; для slot-топологии поведение бит-в-бит прежнее.
- Contract-тесты: порядок и выбор кандидатов прежние для всех built-in стратегий и policies,
  включая `empty slot 0 + mergeable slot 1` для First/StackFirst и blocked-hint сценарии
  бывших `*AlternativePlacementStrategy`.

### Этап 3. Модель planned operations

- Ввести `PlannedOperation` (`RegisterEffects`/`Execute`/`Rollback`) и `PlanTarget`.
- Перевести `PlannedEntryTransfer` на список операций; swap и occupied-handler становятся
  `SwapOperation`/`OccupiedHandlerOperation` вместо bool-флагов.
- `SwapOperation.RegisterEffects`: release обеих исходных placement + reserve обоих результатов
  в states своих инвентарей.
- Временно адаптировать старые slot allocations в `PlacementOperation`.
- Выполнить этап как отдельную структурную миграцию без одновременного изменения selection
  semantics.
- Отдельно проверить partial accounting, `TransferExecutionSummary`, `ExecutedTransferEntry`,
  domain contexts и deferred add/remove events для нескольких операций одного entry;
  добавить per-entry status и отдельный `SkippedEntries`.
- Тесты эффектов swap: entry после swap-entry не может занять ячейки результата swap; entry
  может занять ячейки, освобожденные swap'ом, и получает ребро зависимости.

### Этап 4. Grid vertical slice: единый planner + executor для grid-топологии

- Все entries с target grid-топологии идут через единый аллокатор (4.5) — включая single-cell
  как footprint 1x1. Slot-инвентари не затронуты (инвариант раздела 3).
- Shaped alternative search реализуется здесь через общий candidate source и selection policies.
- Executor для grid: `Create` через `TryPlace`, `Merge` через placement stack; созданные
  placement привязываются к `PlannedPlacement` (`Bind`), словарей соответствий нет.
- Cross-topology swap (grid-цель, slot-источник) планируется через session обеих сторон:
  slot-инвентарь получает `PlacementPlanningState` как участник (раздел 3); обычное планирование
  со slot-целью остается на старом pipeline до этапа 6.
- Occupied-handler, swap fallback (через session), `DragAmountStep`, hint-only — для grid через
  единый путь (чеклист 4.7, пункты 4-7, 9).
- Тесты: occupied hint, свободный регион, отсутствие региона, full same-inventory source release,
  partial split без release, смена ориентации, covered-cell drop.

### Контрольная точка A (ревью-гейт)

- grid-топология полностью на едином пути; preview и execution не расходятся;
- shaped alternative search использует candidate source/selection policy, rules и виртуальную
  topology;
- производительность в пределах baseline;
- зафиксированы решения по behavioral changes (occupied-handler для shaped, `FailureReason`,
  устранение утечки отклоненного entry, миграция `*AlternativePlacementStrategy`).

### Этап 5. Batch на grid

- Снять `IsBatchDrag`-guard для grid-целей: все entries одного плана разделяют одну
  `PlacementPlanningSession`.
- Смешанный batch (single-cell + shaped) работает без моста — один pipeline (раздел 3).
- Ввести strategy capability для явного запрета multi-entry spatial planning (вместо guard).
- Семантика зависимостей в BestEffort (4.6): каждый entry исполняется транзакционно; execution-сбой
  откатывает все его операции и транзитивно пропускает зависимые entries;
  `TransferExecutionSummary` и события различают succeeded/failed/skipped.
- Тесты: смешанный batch, atomic и best-effort при частичной геометрической невместимости,
  отклоненный entry не влияет на последующие planning-транзакции, сбой второй операции
  откатывает первую операцию того же entry, execution-сбой entry пропускает транзитивно
  зависимые, отсутствие rollback-capability дает fail-before-mutation, partial-учет по нескольким
  entries, ориентации per entry, swap внутри batch с учетом резервов предыдущих entries,
  entry зависящий от swap-entry пропускается при его сбое.

### Этап 6. Миграция slot-топологии

- Перевести slot-инвентари (strategy/unique allocation) на единый аллокатор и session.
- Цели адресуются ссылками (`BaseSlot`/`Placement`/`PlannedPlacement`); принадлежность
  проверяется по ссылке в начале entry transaction, удаленный слот дает fail-before-mutation.
- `NewDynamicSlot`: создание слота в executor, привязка к `PlannedPlacement`, затем `TryPlace`;
  rollback удаляет слот через восстановление snapshot.
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
- Удалить `IAlternativePlacementStrategy` и его реализации, если этап 2 подтвердил полное
  поглощение policies.
- Убедиться, что batch ограничивается только явной capability, без shape-hardcode.
- Оставить UI, rules и доказанные early-out fast paths внутри общего аллокатора.

### Этап 9. Документация

- Обновить `.agents/skills/dragdrop-*` и зеркальные `.claude/skills/dragdrop-*`.
- Исправить устаревшее описание swap в `DATA_FLOW.md` и `COMPONENTS.md`.
- Обновить публичную документацию по strategy/acceptance extension points: candidate source,
  selection policies + `SelectionContext`, `IPlanningGeometry`, `PlannedOperation`,
  `PlacementPlanningRequest`, гайд «как добавить стратегию / топологию / policy / операцию».

Каждый этап должен отдельно компилироваться и проходить соответствующий test subset.

---

## 7. Риски и проверки

### Корректность

- planned footprint должен учитывать все предыдущие закоммиченные эффекты текущего плана,
  включая эффекты swap;
- `Rollback(checkpoint)` обязан восстанавливать released source placement и снимать все резервы
  entry — частичный откат недопустим;
- source placement нельзя освобождать повторно при нескольких операциях одного entry;
  committed release допустим только если entry удаляет source placement целиком;
- partial split и `DragAmountStep`, оставляющие source stack, обязаны планироваться с занятым
  source footprint; tentative release требует rollback + однократный replan без release;
- covered-cell interaction должен резолвиться в логический placement/anchor;
- conversion может изменить shape, поэтому geometry проверяется по converted adapter;
- merge не создает новый footprint, но меняет reserved amount в planning state;
- `DragAmountStep`-trim выполняется до `Commit` внутри entry-транзакции — остаточная бронь не
  утекает (текущее консервативное поведение `VirtualSlotState` зафиксировать в characterization
  как известную утечку, см. 2.1);
- same-inventory swap должен проверять совместимость двух результирующих footprint;
- `SwapOperation` регистрирует эффекты в session (release обеих сторон + reserve обоих
  результатов); «читающий» swap в batch ведет к double-book/недопланированию;
- план не адресует цели сырыми индексами и id: только ссылки `Placement`/`BaseSlot`/
  `PlannedPlacement`; принадлежность проверяется по ссылке до первой мутации entry;
- `PlannedPlacement.Bind` — единственный механизм связывания планируемого placement с реальным;
  несколько `NewDynamicSlot` в одном плане различаются объектами, а не порядком создания;
- граф зависимостей обязан покрывать оба вида (явные `PlanTarget`-ссылки и геометрические
  через released-регион); пропуск зависимых в BestEffort — транзитивный, без ложных пропусков
  независимых entries;
- BestEffort entry transaction откатывает все уже выполненные операции и снимает привязки
  `PlannedPlacement` перед пропуском зависимых entries;
- отсутствие rollback-capability у участника BestEffort entry обнаруживается до мутации;
- atomic rollback и события должны сохранять placement snapshots;
- маршрутизация переходного периода — по топологии (раздел 3): два виртуальных состояния одного
  инвентаря в одном плане запрещены.

### Изменение контракта стратегий и policies

- удаление rules-проверок из кандидатов (4.3/4.4) меняет контракт `IAcceptanceStrategy`:
  кастомные стратегии, полагавшиеся на двойную проверку, должны быть найдены в audit этапа 0;
- candidate-source контракт (4.4) перераспределяет владение порядком между стратегией и policy;
  бит-в-бит совместимость built-in комбинаций фиксируется contract-тестами на этапе 2;
- candidate source обязан поддерживать стабильное повторное ленивое перечисление и `kindMask`;
  selection policy не может требовать материализации полного потока кандидатов, иначе
  grid-перфоманс деградирует;
- поглощение `IAlternativePlacementStrategy` — изменение публичного extension point; контексты
  вызова сохраняются через `SelectionContext.Reason`, прежние комбинации фиксируются
  characterization (этап 0) и contract-тестами (этап 2);
- стратегия, не проверившая `CanFit`, не должна ломать planning state: `TryReserve*`
  перевалидирует геометрию (защита от кривых кастомных стратегий);
- occupied-handler для shaped — сознательный behavioral change, фиксируется на гейте A;
- `RulesScope` acceptance — сознательное выравнивание с planner, текущие расхождения фиксируются
  characterization (этап 0).

### Dynamic slots

- placement без существующего anchor-слота допустим только как `PlannedPlacement` с
  NewDynamicSlot-намерением;
- executor резолвит все ссылочные цели entry (принадлежность slot/placement инвентарю) до первой
  мутации. Отсутствующий/удаленный объект дает fail-before-mutation, а не fallback на слот с тем
  же текущим индексом;
- после создания слота executor обязан проверить topology, выполнить `TryPlace` и привязать
  результат к `PlannedPlacement`;
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
- кандидаты перечисляются лениво и повторно, `kindMask` исключает оплату геометрии чужих фаз;
  eager-список на каждую итерацию аллокации запрещен (4.4);
- acceptance не должен строить Unity-объекты, мутировать inventory или аллоцировать коллекции на
  каждый anchor/вызов после прогрева; lightweight-режимы не ведут граф зависимостей;
- result-кэш acceptance вне scope: только offsets-кэш и пересчет на UI-событие (4.8);
- per-entry snapshots в BestEffort ограничены инвентарями, которые entry реально затрагивает;
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
- full same-inventory move освобождает source footprint; partial split и rounded-down
  `DragAmountStep` не освобождают;
- эффекты swap: ячейки результата заняты для последующих entries, освобожденные — доступны
  и создают ребро зависимости; сбой swap-entry пропускает зависимых;
- BestEffort: сбой второй операции откатывает первую операцию того же entry, затем транзитивно
  пропускает зависимые; независимые выполняются, отсутствие snapshot дает fail-before-mutation,
  summary различает succeeded/failed/skipped и содержит per-entry причины;
- dynamic slot create/rollback;
- реиндексация слотов между planning и execution не меняет смысл целей (ссылки); удаленный
  slot/placement дает fail-before-mutation;
- blocked-hint policies (бывшие `*AlternativePlacementStrategy`) дают прежний порядок;
- drop на anchor и covered cell;
- placement snapshots в add/remove/swap events;
- каждый пункт чеклиста 4.7.

---

## 8. Критерии готовности

### Гейт A: этапы 0-4

- `PlacementPlanningSession` корректно резервирует эффекты нескольких planned operations,
  per-entry rollback не оставляет следов.
- Candidate contract: built-in стратегии и policies дают прежние результаты бит-в-бит на
  slot-топологии, включая blocked-hint сценарии.
- Grid-топология полностью на едином пути: single-cell и shaped создают одинаковый список
  операций.
- Shaped alternative search использует общий candidate source/selection policy, rules и
  виртуальную topology.
- Preview, planner и executor согласованы для grid-сценариев.
- Нет регрессии acceptance/hover по baseline performance.

### Полная консолидация: этапы 5-9

- Planner использует один topology-aware allocation service; все entries одного плана разделяют
  `PlacementPlanningSession`.
- Strategy, rules и geometry имеют раздельные обязанности; rules проверяются один раз; стратегии
  работают через `IPlanningGeometry` и не знают конкретную топологию.
- Кандидаты — placement-кандидаты из повторно перечисляемого ленивого source; естественный порядок
  принадлежит стратегии, способ обхода — selection policy; один владелец порядка
  (`IAlternativePlacementStrategy` поглощен).
- План — список типизированных `PlannedOperation`; новый вид операции добавляется новым классом
  с `RegisterEffects`/`Execute`/`Rollback`, без правки модели плана и executor dispatch.
- Все цели плана — ссылки (`Placement`/`BaseSlot`/`PlannedPlacement`); сырых индексов и id в
  модели плана нет.
- Acceptance capacity вычисляется dry-run того же аллокатора через `PlacementPlanningRequest`
  с зафиксированным `RulesScope`.
- Executor выполняет create/merge через placement API для slot и grid topology; `PlannedPlacement`
  детерминированно привязываются к реальным placement.
- Нет `IsSingleCell` branches, меняющих transfer semantics; UI/rules/early-out fast paths
  разрешены.
- `VirtualSlotState`, `PlannedSlotAllocation` и `CanAcceptShape` удалены.
- Неиспользуемый direct swap API удален; swap доступен только через placement-safe pipeline.
- Batch ограничивается только явной strategy capability.
- BestEffort атомарно откатывает упавший entry, выполняет независимые entries и детерминированно
  пропускает транзитивно зависимые от упавших (4.6).
- Все characterization и новые topology/batch/event тесты проходят, чеклист 4.7 покрыт
  contract-тестами.
- Architecture skills и публичная документация соответствуют реализации.

---

## 9. Открытые вопросы

### Агрегатные правила (решение не принято)

Все текущие rules — предикаты над одной операцией: «можно ли N штук этого предмета в этот
anchor». Класс правил над **агрегатом плана** так не выражается. Пример: правило «суммарный вес
инвентаря <= 100» при текущем весе 90 и плане из трех аллокаций по весу 5 — каждая проверка по
отдельности проходит (90+5), сумма (105) нарушает лимит, и ни один per-anchor хук ее не видит.
Аналогично: «не больше 3 разных типов предметов», «максимум 2 placement квестовых предметов».

Варианты:

- **A. Шов сейчас:** после планирования всех entries session отдает агрегированные дельты по
  каждому инвентарю (добавлено/удалено по предметам, созданные/удаленные placement);
  `IPlanAggregateRule` (регистрируется как inventory rule) валидирует и может отклонить
  entry/план. Цена сейчас — интерфейс + одна точка вызова в `Plan`-режиме. Ограничение v1:
  lightweight-режимы acceptance (`CountOnly`/`FirstTargetOnly`) агрегатные правила не учитывают —
  preview остается greedy-оценкой, drop перевалидирует (документируется).
- **B. Вне scope:** агрегатные ограничения объявляются зоной domain handlers на выполнении.
  Дешевле сейчас, но preview систематически лжет для таких правил, и ретрофит шва в выпущенный
  pipeline дороже.

---

*Основные затрагиваемые области: `TransferPlanner`, `TransferPlanExecutor`, `PlacementStore`,
`InventoryTopology`, `UniversalInventory`, `InventoryAcceptanceRequest`, `VirtualSlotState`,
`InventoryStrategyBase`, `IAcceptanceStrategy`, `SlotAcceptanceCandidate`, `SlotSelectionPolicy`,
`AutoTransferService`, `Core/Drop/*AlternativePlacementStrategy`, `BlockedTargetResolverBase`
и concrete strategies.*
