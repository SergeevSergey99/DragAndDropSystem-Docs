# Unified Placement Planning - Design Plan

**Цель:** перестать различать single-cell и shaped в transfer semantics. Single-cell — вырожденный
случай footprint из одной ячейки; планирование, acceptance и выполнение используют одну
placement-модель.

Доступность места определяется не одним `CanPlace`, а сочетанием трех независимых проверок:

1. стратегия определяет eligibility create/merge, capacity, естественный порядок и фазовые метки
   кандидатов; способ обхода и итоговый выбор — за selection policy (4.4);
2. rules проверяют конкретную операцию и целевой anchor;
3. placement state проверяет топологию, границы и occupancy.

> Статус: **план, runtime-код не меняется.** Реализация — отдельными компилируемыми этапами.
> Scope: **полная консолидация** (этапы 0-9), включая batch+shaped и миграцию single-cell пути.
> Публичного релиза не было — двойной путь убирается без compatibility-обязательств.
> Контрольные точки — ревью-гейты, а не точки вероятной остановки.
> Документ — результат нескольких ревью-циклов (история в git). Ключевое упрощение финальной
> версии: **BestEffort исполняется через JIT-replan** (план каждого entry строится по реальному
> состоянию непосредственно перед его исполнением), поэтому граф зависимостей entries,
> transitive skip и связанная транзакционная механика исключены из дизайна.

---

## 1. Почему унификация возможна

Хранилище уже унифицировано:

- `PlacementStore` — источник правды для всех стеков, включая footprint из одной ячейки.
- Footprint принадлежит топологии через `IInventoryTopology.GetPlacementOffsets(shape, orientation)`.
- `SlotTopology` всегда возвращает anchor-only: форма предмета в слотном инвентаре занимает один
  слот без отдельной policy.
- Grid-топология использует реальные ориентированные offsets формы.
- Основной swap-путь уже placement-based: обе исходные placement освобождаются, предметы
  размещаются через topology-aware `TryPlace`.
- `Placement` — объект, store сравнивает его через `ReferenceEquals`, merge мутирует stack на
  месте. Identity сегодня нарушается в одном месте — `ShiftAfterSlotRemoved` пересоздает объекты
  при удалении dynamic slot; это чинится точечно (4.1).

Различие single-cell/shaped — не свойство storage. Оно осталось в планировщике,
acceptance-стратегиях и executor.

---

## 2. Где сейчас живет расхождение

### 2.1 Планировщик

- Single-cell идет через `AllocateForStrategyInventory` / `AllocateForUniqueInventory`
  (`PlannedSlotAllocation`), multi-cell grid — через `TryPlanShapedPlacement`
  (`PlannedPlacementAllocation`). Две модели результата.
- В нескольких местах `IsSingleCell` выбирает не оптимизацию, а разные transfer semantics.
- `VirtualSlotState` хранит только stack одного слота и не моделирует footprint.
- Виртуальное состояние не транзакционно: при отклонении entry (`AllowPartial=false` после
  частичной аллокации) `Apply`-мутации не откатываются — отклоненный entry оставляет фантомные
  резервы последующим entries batch.
- Shaped-путь проверяет feasibility через runtime `CanPlace`, поэтому batch+shaped запрещен
  жестким guard в `PlanEntry`, а не capability.

### 2.2 Стратегии и acceptance

- `GetSlotCandidates`, `GetAcceptableCount`, `CanUseAlternativeSlot` рассуждают через
  `slot.IsEmpty` и не доказывают, что shape помещается в топологию.
- `CanAcceptShape` отсекает multi-cell для grid, потому что acceptance-путь не умеет планировать
  placement без заранее заданного anchor.
- `GetAcceptableCount` не получает полный набор planner inputs: policy, global rules, hint и
  blocked-target semantics в контракт не входят.

### 2.3 Executor и модель плана

- Slot allocation исполняется через `TryAddToSlot` / dynamic-slot path, shaped — через отдельный
  `TryAddToTargetPlacement`.
- `PlannedPlacementAllocation` один на entry — не покрывает распределение стека по нескольким
  placement.
- Результат планирования — **закрытый union**: swap и occupied-handler выражены bool-флагами
  (`RequiresSwap`, `RequiresOccupiedHandler`) на `PlannedEntryTransfer`. Новый вид операции
  требует одновременной правки модели плана, планировщика и executor.
- `UniversalInventory.TrySwapSlots` мутирует `BaseSlot.Stack` напрямую; потребителей нет —
  удалить без deprecation.

### 2.4 Candidate/selection механика

Механизм «порядок кандидатов + выбор» существует, но контракт целиком слотовый:

- `GetSlotCandidates` возвращает eager список `(ISlot, RemainingCapacity)`, и
  `AllocateViaCandidatesLoop` перестраивает его заново на каждой итерации аллокации;
- `SlotSelection` выражает только `Existing(slot)` / `New()` / `None` — кандидата «создать
  placement с anchor A в ориентации O» в модели нет;
- rules проверяются дважды: в стратегии (`PassesRules`) и в планировщике
  (`IsCandidateAllowedByRules`);
- владение порядком размазано: стратегия отдает кандидатов в порядке слотов, а
  `StackFirstSlotSelectionPolicy` переупорядочивает;
- параллельно живет второй ordering-механизм для blocked-hint контекста:
  `IAlternativePlacementStrategy` (MergeFirst/EmptyFirst/MergeOnly/EmptyOnly) — то же понятие
  «порядок мест» с отдельным слотовым контрактом. Контексты вызова не пересекаются
  (blocked-hint vs area-drop/auto-transfer), но два словаря для одного понятия — налог на каждую
  будущую стратегию и топологию.

---

## 3. Главный инвариант

`PlacementStore.CanPlace` проверяет **реальное текущее состояние**. Планировщик не может опираться
только на него, когда один планируемый результат содержит несколько еще не выполненных операций:

1. первый entry атомарного batch планирует footprint `{0, 1}`;
2. runtime store не изменен;
3. второй entry вызывает `CanPlace` и тоже получает разрешение на `{0, 1}`;
4. план выглядит валидным, executor не сможет его применить.

Поэтому **atomic-планирование и preview** работают через виртуальный topology-aware state,
резервирующий запланированные footprint и merge capacity. Это касается всех операций, меняющих
occupancy, включая swap (4.2). BestEffort-выполнение виртуального межзаписного состояния не
требует — оно планирует каждый entry по реальному состоянию (4.6).

**Инвариант переходного периода: маршрутизация по топологии, а не по форме.** Каждый инвентарь
обслуживается ровно одним pipeline:

- grid-топология переходит на унифицированный путь целиком — включая single-cell как footprint
  1x1 — на этапе 4;
- slot-топология остается на старом `VirtualSlotState`-пути до этапа 6;
- два виртуальных состояния одного инвентаря в одном планировании запрещены; смешанный batch на
  grid не требует моста, потому что все его entries идут через один pipeline;
- маршрутизация выбирает pipeline для **target**-инвентаря. Slot-инвентарь может получить
  `PlacementPlanningState` как участник плана с grid-целью (reverse-сторона cross-topology swap)
  уже на этапе 4 — в таком плане он не обслуживается старым pipeline, инвариант не нарушается.

---

## 4. Целевая архитектура

### 4.1 `PlacementPlanningSession` и `PlacementPlanningState`

Планирование затрагивает больше одного инвентаря: swap размещает в обе стороны, same-inventory
release живет в инвентаре источника. Виртуальное состояние ведется per inventory и собирается в
session:

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

**Зачем session как отдельная сущность.** Это не слой поверх виртуального планирования, а
владелец per-plan вещей, которые иначе живут россыпью полей в `TransferPlanner` (как сегодня
`BuildVirtualSlots` + список `VirtualSlotState`): словарь `inventory -> state` (один инвентарь в
одном планировании обязан давать одно состояние — source entry 1 может быть стороной swap
entry 3) и entry-чекпоинт, охватывающий несколько states сразу (один entry трогает до трех:
source release, target резервы, обе стороны swap; per-state чекпоинты с ручным учетом «кого я
трогал» воспроизводят класс утечки 2.1). Извлечение в объект делает транзакционность тестируемой
без планировщика. **Session — внутренняя инфраструктура**: стратегии и policies видят только
`IPlanningGeometry` (4.3).

**Время жизни session = один проход планирования.** Atomic batch и preview: одна session на все
entries. BestEffort-выполнение: свежая session на каждый entry, инициализированная реальным
состоянием (4.6); внутри entry она по-прежнему нужна — несколько аллокаций одного entry
резервируют друг относительно друга.

**Ссылки, а не id.** План нигде не адресует цели сырыми индексами или session-id:

- существующий placement — сам `Placement` (merge мутирует stack на месте, ссылка переживает
  доклад в стек);
- anchor для Create — `BaseSlot`-ссылка (на обеих топологиях ячейка = слот); индекс читается из
  слота в момент использования, как payload геометрии;
- запланированный placement — объект `PlannedPlacement` из `TryReserveCreate`. Для
  `NewDynamicSlot` executor создает слот и привязывает его к этому объекту
  (`PlannedPlacement.Bind`). Словарей `id -> placement` нет — привязку несет сам объект;
- слот/placement, удаленный между планированием и выполнением, обнаруживается проверкой
  принадлежности **по ссылке** до первой мутации entry → fail-before-mutation, а не «слот с тем
  же текущим индексом».

`PlannedPlacement` заменяет сегодняшние synthetic virtual slots: последующая аллокация видит его
через `GetPlacementAt`, merge-ится в него, one-per-ID учитывает наравне с реальными placement.

**Точечная правка identity.** `ShiftAfterSlotRemoved` сегодня пересоздает `Placement`-объекты
(задокументировано как «short-lived»). Поскольку план может пережить мутации собственного
выполнения (atomic batch, мульти-операционный entry, где ранняя операция способна вызвать
реиндексацию), реиндексация переводится на in-place rebind (мутация anchor/covered indices того
же объекта). Полная стабильность через rollback-пути не требуется: после полного atomic-отката
план мертв, а после отката entry в BestEffort следующий entry перепланируется по реальному
состоянию. Побочная выгода — лечится класс багов index-keyed DataBindings при реиндексации.

**Per-entry транзакции планирования.** Source placement освобождается до завершения планирования
entry, резервы создаются по ходу подбора кандидатов. Отклоненный entry (`AllowPartial=false`,
нет кандидатов, swap не сложился) откатывается `Rollback(checkpoint)` без следов: восстановленный
source, снятые резервы. Это чинит существующую утечку 2.1.

`GetReservedAmount` сознательно заменяет `GetRemainingMergeCapacity`: planning state не знает про
max stack и stacking semantics — это зона стратегии; remaining capacity вычисляет аллокатор (4.5).

Требования:

- использует ту же `IInventoryTopology`, что и runtime `PlacementStore`;
- видит реальные placement и все ранее закоммиченные изменения session;
- не мутирует inventory, slots и runtime `PlacementStore`;
- tentative-release source footprint — ровно один раз и только если dragged stack покрывает весь
  source placement; перед `Commit` проверяется, что entry действительно удалит source целиком,
  иначе tentative release откатывается и entry перепланируется один раз без освобождения
  source footprint (partial split / `DragAmountStep`);
- последовательный deterministic greedy planning;
- planner и acceptance используют один алгоритм dry-run;
- swap-планирование работает через session со states обеих сторон.

### 4.2 Модель planned operations

Результат планирования entry — **список типизированных операций**, а не аллокации плюс bool-флаги:

```text
PlannedEntryTransfer
    Entry, RequestedAmount, PlannedAmount, FailureReason
    Operations: IReadOnlyList<PlannedOperation>

PlannedOperation (контракт)
    CollectParticipants(collector)   // ДО выполнения: затронутые inventories и внешние участники
                                     //  -> executor знает, что snapshot-ить и у кого требовать
                                     //  rollback-capability
    RegisterEffects(registrar)       // планирование: release/reserve затронутых states;
                                     //  registrar — публичный IPlanningEffectRegistrar,
                                     //  session остается internal (C# accessibility)
    Execute(ctx) -> OperationExecutionResult
                                     // стандартизированный результат: outcomes, domain contexts,
                                     //  вклад в deferred events, привязки PlannedPlacement
    Rollback(ctx)                    // откат, включая снятие привязок и provisional outcomes

Встроенные операции первой версии:
    PlacementOperation       Kind: Create | Merge; Target: PlanTarget; Orientation, Shape, Amount
    SwapOperation            forward/reverse пары (placement -> результат), stacks before/after
    OccupiedHandlerOperation доменное действие

PlanTarget — всегда ссылка на объект, никогда сырой индекс:
    Placement | BaseSlot | PlannedPlacement
```

Стандартизированный `OperationExecutionResult` обязателен: без него executor не соберет summary,
domain contexts и deferred events для неизвестной ему операции, и обещание «новая операция без
правки executor» не выполняется.

**Контракт эффектов.** Каждая операция при планировании регистрирует свои изменения occupancy
через `IPlanningEffectRegistrar`, внутри entry-чекпоинта:

- `PlacementOperation`: `Create` -> `TryReserveCreate`, `Merge` -> `TryReserveMerge`;
- move-часть entry -> tentative `TryReleaseSourcePlacement` (4.1);
- `SwapOperation`: в state target-инвентаря — release целевой placement + reserve
  forward-результата; в state source-инвентаря — release исходной placement + reserve
  reverse-результата. «Читающий» swap делает свои результаты невидимыми для последующих entries
  atomic-плана: они либо double-book-ают ячейки результата, либо не видят освобожденные;
- `OccupiedHandlerOperation` — ноль planning-эффектов, но **не ноль runtime-эффектов**:
  существующие handlers мутируют внешние модели (Demo5: `container.AddItem` + `RemoveFromData` +
  очистка source slot) и эмитят события немедленно — inventory snapshot этого не откатывает.
  Через `CollectParticipants` операция декларирует внешнего участника; если он не предоставляет
  rollback (опциональный transaction-aware интерфейс handler'а), entry с такой операцией допустим,
  только если она — единственная мутирующая операция entry и исполняется последней (откатывать
  нечего), иначе fail-before-mutation. Сегодняшний одиночный drop-on-container подпадает под
  исключение и работает без изменений.

Новый тип операции — новый класс с этими четырьмя методами, без правки модели плана,
планировщика и executor dispatch.

Правила:

- single-cell — обычный `Create`/`Merge` с footprint из одной ячейки;
- shaped stack может иметь несколько `PlacementOperation`, если стратегия допускает несколько
  placement;
- merge в placement, запланированный ранее в этом же планировании, ссылается на его
  `PlannedPlacement`-объект;
- grid + dynamic slots запрещен конфигурацией, поэтому `PlannedPlacement` без anchor-слота
  (`NewDynamicSlot`) нужен только slot topology;
- `PlannedSlotAllocation` удаляется без compatibility wrapper (релиза не было); если к этапу 6
  релиз состоится — obsolete wrapper на один релиз.

### 4.3 Роли strategy, rules и geometry

Стратегия отвечает за: eligibility create/merge/reject; max stack и capacity (caps; учет
запланированного — через аллокатор и `GetReservedAmount`); one-per-ID и separable-stack
semantics, включая `PlannedPlacement`; естественный deterministic order и фазовые метки
кандидатов (4.4); default selection policy; допустимость создания dynamic slot.

Стратегия **не знает ни топологию, ни форму предмета**. Геометрию она спрашивает через узкий
read-only фасад:

```text
IPlanningGeometry (view поверх PlacementPlanningState + topology)
    EnumerateAnchorSlots(order)        // порядок перечисления отдает ТОПОЛОГИЯ;
                                       //  order: Natural | FromHint(slot)
    CanFit(footprint, anchorSlot)      // footprint — опаковый токен, резолвится аллокатором
                                       //  по converted item
    GetPlacementAt(slot)               // реальный или planned
    GetReservedAmount(target)
```

Новая топология реализует свое перечисление анкоров и проекцию форм — стратегии не меняются.
Новая стратегия пишет semantics поверх фасада — топологии не меняются. Защита от кривых
расширений: даже если стратегия не вызвала `CanFit`, `TryReserve*` перевалидирует геометрию —
испортить planning state стратегия не может.

Rules отвечают за конкретный item, amount и target anchor. Rules проверяет **только аллокатор,
один раз** — из кандидатов стратегий проверка убирается (сейчас задублирована, 2.4). Это
сознательное изменение контракта `IAcceptanceStrategy`.

`PlacementPlanningState` отвечает только за: проекцию shape через topology, bounds, occupancy,
резервирование, учет reserved amounts, транзакционность.

### 4.4 Candidate source, selection policy и единый владелец порядка

Существующий цикл `GetSlotCandidates -> Select -> Apply -> повторить` структурно совпадает с
целевым алгоритмом 4.5 — меняются типы:

```text
PlacementCandidate
    Kind: Merge | Create | NewDynamicSlot
    Target: PlanTarget        (Merge: Placement | PlannedPlacement; Create: BaseSlot)
    Orientation
    Capacity                  (cap стратегии минус текущее содержимое placement;
                               planned-резервы НЕ учтены — их вычитает аллокатор)

PlacementCandidateSource     (отдает стратегия)
    Enumerate(kindMask)      // ленивое, повторно перечисляемое; маска фаз обязательна, чтобы
                             //  merge-проход не оплачивал геометрию Create-кандидатов
```

Policy получает source, request (включая hint) и `IPlanningGeometry` — этого достаточно для
metric-policies (ближайший к hint, минимизация фрагментации). Терминология: `SlotSelectionPolicy`
(выбор целевого места) не связан с `SelectionManager` (`Scripts/Selection` — выделение предметов
в UI); имена новых типов не должны усиливать коллизию.

Владение порядком и выбором:

- **стратегия** предоставляет ленивый, повторно перечисляемый source поверх `IPlanningGeometry`;
  кандидаты имеют фазовую метку, естественный порядок сохраняет текущую strategy/topology
  semantics, включая interleave merge и empty slots;
- **selection policy** определяет способ обхода. `FirstSlotSelectionPolicy` берет первый кандидат
  естественного потока; `StackFirstSlotSelectionPolicy` перечисляет `Enumerate(Merge)`, при
  отсутствии — `Enumerate(Create | NewDynamicSlot)`. Пример `empty slot 0 + mergeable slot 1`
  остается bit-for-bit: First — slot 0, StackFirst — slot 1;
- повторное ленивое перечисление допустимо; полная материализация и сортировка всех anchors
  запрещены; source стабилен в пределах одной попытки allocation;
- rules в кандидатах не проверяются (4.3); геометрия проверяется лениво и перевалидируется при
  reserve;
- для slot-топологии кандидаты вырождаются в текущее поведение — миграция стратегий без
  изменения semantics.

**Один владелец порядка.** `IAlternativePlacementStrategy` поглощается selection policies —
его семантика и есть способ обхода кандидатов. Выбор активной policy — чистая конфигурация, без
context-enum:

- дефолтная policy инвентаря/стратегии (как сейчас `DefaultSlotSelectionPolicy`);
- per-operation override (`InventoryAcceptanceRequest.SelectionPolicy`) имеет приоритет;
- blocked-резолвер владеет своей policy и передает ее как такой override при поиске альтернатив —
  зеркало сегодняшнего `FindAlternativeBlockedTargetResolver`, владеющего
  `IAlternativePlacementStrategy` сериализованным полем.

`BlockedTargetResolver` сводится к выбору **вида реакции** (Alternative | Swap | Reject).
Исключение заблокированного hinted-слота — свойство вызова blocked-поиска, не policy.

Будущий auto-rotate входит аддитивно: source отдает кандидатов с разными `Orientation`. `Kind`
кандидата — словарь executor'а; новые виды операций добавляются как `PlannedOperation` (4.2),
а не расширением `Kind`.

### 4.5 Один аллокатор

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
           резервирует оба результата (4.2)
    7. apply AllowPartial, BatchMode and DragAmountStep semantics
       (trim уменьшает резервы до Commit — остаточная бронь не утекает)
    8. if final amount leaves items in source placement and step 2 released it:
         - session.Rollback(checkpoint); replan entry once with source footprint retained
    9. accepted -> session.Commit(checkpoint); rejected -> session.Rollback(checkpoint)
```

Аллокатор не ветвится по `IsSingleCell` — отличия выражаются topology, candidate source и
strategy capabilities. Шаг 3 — сознательный behavioral change: occupied-handler распространяется
на shaped items с тем же приоритетом, что на single-cell пути (фиксируется characterization).

**Детерминизм планировщика — несущее требование** (см. 4.6): одинаковые входы + одинаковое
состояние = одинаковый план. Кастомные стратегии/policies обязаны соблюдать это контрактом.

### 4.6 Выполнение batch: Atomic и BestEffort (JIT-replan)

```text
Atomic:
    одна session -> план всех entries -> выполнение всех -> любой сбой = полный rollback (как сейчас)

BestEffort:
    for each entry:
        план(entry) по РЕАЛЬНОМУ состоянию (свежая session)
        выполнить entry (атомарно на уровне entry)
        сбой entry -> откат только этого entry -> следующий entry планируется заново
```

Почему JIT-replan, а не «исполнить заранее построенный план с пропуском зависимых»:

- entry B планируется **после фактического исполнения A** — координация через реальное
  состояние; граф зависимостей, transitive skip и валидность ссылок устаревшего плана не нужны
  в принципе;
- это корректнее пропуска: если A упал, B не пропускается «за компанию», а перепланируется и
  может встать в другое место — либо честно откажет с настоящей причиной;
- пока все entries исполняются успешно, JIT-планы побитово совпадают с preview-планом
  (детерминизм 4.5 + реальное состояние совпадает с виртуальным прогнозом). Расхождение возможно
  только при сбое — ровно когда пересчет и нужен;
- цена — повторное планирование на drop (не на hover); планирование дешевое, drop — редкое
  событие.

**Атомарность отдельного entry** (нужна в обоих режимах — entry с несколькими операциями не
должен исполниться наполовину):

- перед entry executor снимает snapshots инвентарей, заявленных `CollectParticipants`;
- участник без rollback-capability (`IInventorySnapshotProvider` либо transaction adapter) —
  entry помечается failed **до первой мутации**, кроме исключения для occupied-handler (4.2);
- сбой любой операции откатывает весь entry: выполненные операции, созданные dynamic slots,
  привязки `PlannedPlacement`, provisional outcomes; deferred events откатившихся операций не
  эмитятся;
- `TransferExecutionSummary` содержит per-entry результат: `Succeeded | Failed` + причина.
  Статус «пропущен из-за зависимости» не существует — зависимостей между entries нет.

Preview для BestEffort batch остается оценкой по общей session (4.8): он может оказаться
оптимистичнее фактического исполнения, если часть entries упадет на доменной валидации — это
свойство best-effort по определению, drop всегда перевалидирует.

### 4.7 Поведения, которые единый аллокатор обязан поглотить

Главный риск миграции — не геометрия, а накопленные специальные поведения. Чеклист (каждое —
characterization-тест до миграции и contract-тест после):

1. deferred placement через `TryAddStack(-1)` для динамического инвентаря с нулем слотов
   (inventory-area drop без target hint);
2. deferred-fallback, когда virtual allocation ничего не нашла, но инвентарь сообщает capacity
   через `potentialNewSlots`;
3. различие legacy deferred (`TryAddStack(-1)`) и policy-driven `RequiresNewSlot`;
4. `DragAmountStep`: округление и per-allocation подгонка (`ApplySourceDragAmountStep`);
5. hint-only entries (`PlanHintOnlyEntry`), когда альтернативы недостижимы;
6. приоритет occupied-slot handler над blocked-target поведением;
7. swap fallback при `plannedAmount == 0`;
8. one-per-ID учет placements, запланированных ранее в том же планировании (сегодня — synthetic
   virtual slots; в целевой модели — `PlannedPlacement`);
9. исключение source slot/placement при same-inventory move;
10. `EnsureFreeSlots` / potentialNewSlots accounting для dynamic инвентарей.

Пункты 4-7 и 9 актуальны для grid vertical slice (этап 4); 1-3, 8, 10 — для миграции
slot-топологии (этап 6). Single-cell fast path после миграции допустим **только как early-out
внутри единого аллокатора**, не как параллельная семантическая ветка.

### 4.8 Acceptance через тот же dry-run

`GetAcceptableCount` не должен иметь отдельный алгоритм упаковки — иначе preview и planner
разойдутся. Нужен общий запрос с явным режимом:

```text
PlacementPlanningRequest
    Mode: Plan | AcceptancePreview | CountOnly | FirstTargetOnly
    item, amount, context/entry, target hint
    policy (resolved), selection policy override
    RulesScope: какие слои rules участвуют (slot rules, inventory rules, global rules)
```

- `Plan` — полный результат с операциями; `AcceptancePreview` — та же логика, облегченный
  результат; `CountOnly` — суммарный `Amount` с ранним выходом по `DesiredCount`;
  `FirstTargetOnly` — первый допустимый existing anchor либо возможность `NewDynamicSlot`
  (контракт `CanAcceptItem`);
- `RulesScope` — обязательная часть контракта: без зафиксированного набора слоев preview и
  planner дадут разные результаты. Текущие расхождения (acceptance не видит global rules)
  фиксируются characterization и устраняются сознательно;
- вызов без hint — deterministic scan anchors в порядке candidate source; greedy-оценка, не
  оптимальная упаковка (документируется).

Требования к облегченным режимам:

- reusable buffers/value types, без GC-нагрузки пропорционально числу anchors на вызов;
- кэшируются shape/orientation offsets; **result-кэш вне scope**: acceptance вызывается
  событийно (вход в зону, смена ячейки/ориентации, drop), а не per-frame; надежная инвалидация
  невозможна в принципе (occupancy версионируется, rules — произвольный пользовательский код);
- цена устаревшего результата — неверная подсветка до следующего события: acceptance advisory,
  drop планирует заново и executor перевалидирует с rollback;
- fallback при провале baseline после этапа 7: occupancy-only counter + задокументированное
  допущение «rules стабильны в пределах drag» — решение по профайлеру, не заранее.

До миграции acceptance — baseline-профили на типичных и больших grid.

---

## 5. Что не требуется унифицировать

1. **UI и rendering** — drag visual, overlay, подсветка legitimately используют размер и форму.
2. **Доменные rules** — правило может сознательно запрещать multi-cell item.
3. **Оптимизации** — single-cell early-out внутри общего аллокатора при совпадающей semantics.
4. **Безусловный multi-entry spatial planning** — batch+shaped в scope (этап 5), но strategy
   capability может его явно запретить; это решение capability, а не скрытый guard.
5. **Оптимальная упаковка** — deterministic greedy; bin-packing/backtracking вне scope.

---

## 6. Этапы реализации

Контрольные точки A и B — ревью-гейты (пауза, characterization, перфоманс), не точки остановки.
Порядок — «контракты до потребителей»: session, candidate-модель и модель операций появляются
раньше первого vertical slice.

### Этап 0. Characterization и API audit

- Зафиксировать поведение slot/grid для stackable, separable, unique; partial, same-inventory
  move, conversion, dynamic slot; swap event snapshot test.
- Покрыть тестами каждый пункт чеклиста 4.7.
- Зафиксировать утечку виртуального состояния при отклоненном entry (2.1) — исчезнет с
  транзакциями, сознательно.
- Зафиксировать observable `FailureReason` строки shaped/single-cell путей.
- Зафиксировать фактический rules scope acceptance-пути.
- Зафиксировать порядок `IAlternativePlacementStrategy`-реализаций для blocked-hint — мигрируют
  в policies.
- Зафиксировать детерминизм планировщика (одинаковые входы — одинаковый план) — несущее
  требование JIT-replan.
- Удалить `UniversalInventory.TrySwapSlots`.
- Проверить внешних потребителей `PlannedSlotAllocation` / `PlannedPlacementAllocation`.
- Зафиксировать observable результаты `CanAcceptItem` / `GetAcceptableCount` (greedy order,
  suggested slot).
- Снять baseline allocations/time acceptance preview на малых и больших grid, включая batch.

### Этап 1. `PlacementPlanningSession` и planning state

- `PlacementPlanningSession` (ленивый `inventory -> state`) и `PlacementPlanningState` (4.1).
- Per-entry транзакции `BeginEntry`/`Commit`/`Rollback` на уровне session.
- `PlannedPlacement`-объекты; merge и `GetReservedAmount` по `Placement | PlannedPlacement`;
  никаких id/индексных адресаций.
- In-place rebind в `ShiftAfterSlotRemoved` (identity, 4.1); characterization фиксирует текущее
  пересоздание до правки.
- Тесты: пересекающиеся footprint; rollback восстанавливает released source и снимает резервы;
  reserved amounts по real и planned targets; merge в planned placement; one-per-ID поверх
  planned; tentative source-release и replan partial split без release; identity при
  `ShiftAfterSlotRemoved`.
- Публичные планы и executor не меняются.

### Этап 2. Candidate contract и geometry facade

- `IPlanningGeometry`: перечисление анкоров у топологии (`Natural`, `FromHint`), `CanFit` по
  опаковому footprint-токену.
- `PlacementCandidate` + ленивый повторно перечисляемый `PlacementCandidateSource` с
  `Enumerate(kindMask)`.
- Policy получает source, request и `IPlanningGeometry`; активная policy — конфигурацией
  (дефолт + per-operation override).
- Поглотить `IAlternativePlacementStrategy` selection policies; blocked-резолвер передает свою
  policy как override, blocked-поиск исключает hinted-слот на уровне вызова;
  `BlockedTargetResolver` выбирает только вид реакции.
- Убрать rules из кандидатов стратегий.
- Мигрировать built-in стратегии и policies; slot-топология — бит-в-бит.
- Contract-тесты: порядок/выбор прежние для всех built-in комбинаций, включая
  `empty slot 0 + mergeable slot 1` (First/StackFirst) и blocked-hint сценарии бывших
  `*AlternativePlacementStrategy`.

### Этап 3. Модель planned operations

- `PlannedOperation` (`CollectParticipants`/`RegisterEffects`/`Execute`/`Rollback`),
  `OperationExecutionResult`, `IPlanningEffectRegistrar`, `PlanTarget`.
- `PlannedEntryTransfer` — список операций; swap/occupied-handler — операции вместо bool-флагов.
- `SwapOperation.RegisterEffects`: release обеих исходных + reserve обоих результатов.
- Переходный `LegacyPlacementOperation` для slot-pipeline (этапы 3-5): исполняет старую slot
  allocation, `RegisterEffects` — no-op (учет в `VirtualSlotState`); удаляется на этапе 6 — иначе
  два виртуальных состояния одного инвентаря (раздел 3).
- Проверить partial accounting, `TransferExecutionSummary` (per-entry статусы),
  `ExecutedTransferEntry`, domain contexts, deferred events для нескольких операций entry.
- Тесты эффектов swap: в atomic-планировании entry после swap-entry не может занять ячейки
  результата и видит освобожденные.

### Этап 4. Grid vertical slice: единый planner + executor для grid-топологии

- Все entries с target grid-топологии — через единый аллокатор (4.5), включая single-cell как
  1x1. Slot-инвентари не затронуты (раздел 3).
- Shaped alternative search — через общий candidate source и selection policies.
- Executor: `Create` через `TryPlace`, `Merge` через placement stack, привязка
  `PlannedPlacement.Bind`.
- Cross-topology swap (grid-цель, slot-источник) — через session обеих сторон; обычное
  планирование со slot-целью — на старом pipeline до этапа 6.
- Occupied-handler, swap fallback, `DragAmountStep`, hint-only — для grid через единый путь
  (чеклист 4.7, пункты 4-7, 9).
- Тесты: occupied hint; свободный регион; отсутствие региона; full same-inventory source
  release; partial split без release; смена ориентации; covered-cell drop.

### Контрольная точка A (ревью-гейт)

- grid-топология полностью на едином пути; preview и execution не расходятся;
- shaped alternative search через candidate source/policy, rules и виртуальную topology;
- перфоманс в пределах baseline;
- зафиксированы behavioral changes (occupied-handler для shaped, `FailureReason`, устранение
  утечки 2.1, миграция `*AlternativePlacementStrategy`).

### Этап 5. Batch на grid: Atomic через session, BestEffort через JIT-replan

- Снять `IsBatchDrag`-guard для grid-целей.
- Atomic: одна session на все entries; полный rollback при сбое.
- BestEffort: цикл «свежая session по реальному состоянию -> план entry -> атомарное выполнение
  entry» на уровне transfer service; сбой entry не прерывает цикл.
- Смешанный batch (single-cell + shaped) — один pipeline, без моста (раздел 3).
- Strategy capability для явного запрета multi-entry spatial planning (вместо guard).
- Тесты: смешанный batch; atomic при частичной геометрической невместимости (полный откат);
  BestEffort: сбой entry A -> entry B перепланируется и встает в другое место; сбой второй
  операции entry откатывает первую; отсутствие rollback-capability дает fail-before-mutation;
  **JIT-консистентность**: при отсутствии сбоев последовательность JIT-планов совпадает с
  preview-планом; ориентации per entry; swap внутри atomic batch с учетом резервов.

### Этап 6. Миграция slot-топологии

- Slot-инвентари — на единый аллокатор, session и JIT-цикл BestEffort.
- Цели — ссылки (`BaseSlot`/`Placement`/`PlannedPlacement`); принадлежность по ссылке до первой
  мутации, удаленный объект — fail-before-mutation.
- `NewDynamicSlot`: создание слота в executor, `Bind`, затем `TryPlace`; rollback удаляет слот
  через snapshot.
- Чеклист 4.7 целиком (пункты 1-3, 8, 10 — основная нагрузка).
- Удалить `PlannedSlotAllocation`, `VirtualSlotState`, `LegacyPlacementOperation`, старые
  branches и `TryAddToTargetPlacement`-guard.

### Контрольная точка B (ревью-гейт)

- единый execution contract для slot и grid topology;
- чеклист 4.7 покрыт contract-тестами и проходит;
- перфоманс planner/executor в пределах baseline.

### Этап 7. Унификация acceptance

- `PlacementPlanningRequest` с режимами (4.8); зафиксировать `RulesScope`.
- `GetAcceptableCount` / `CanAcceptItem` — на dry-run общего аллокатора
  (`CountOnly` / `FirstTargetOnly`).
- Удалить `CanAcceptShape` как routing guard.
- Preview capacity совпадает с реально построенным планом.
- Нет регрессии по baseline; при провале допускается специализированный preview fast path при
  общей contract-тестовой матрице с planner.

### Этап 8. Зачистка

- Удалить semantic `IsSingleCell` branches из planner/executor/acceptance и `AutoTransferService`
  (shaped-guard выбора режима). Визуальные проверки формы
  (`DragAndDropManager.TrySetPlacementDraggedState`, `DropPreviewController`, drag visuals)
  остаются — UI по §5.1.
- Удалить `IAlternativePlacementStrategy` и реализации (поглощены на этапе 2).
- Batch ограничивается только явной capability.

### Этап 9. Документация

- Обновить `.agents/skills/dragdrop-*` и зеркальные `.claude/skills/dragdrop-*`.
- Исправить описание swap в `DATA_FLOW.md` / `COMPONENTS.md`.
- Документация extension points: candidate source, selection policies, `IPlanningGeometry`,
  `PlannedOperation`, `PlacementPlanningRequest`; гайд «как добавить
  стратегию / топологию / policy / операцию»; контракт детерминизма для кастомных расширений.

Каждый этап отдельно компилируется и проходит свой test subset.

---

## 7. Риски и проверки

### Корректность

- planned footprint учитывает все закоммиченные эффекты текущего планирования, включая swap;
- `Rollback(checkpoint)` восстанавливает released source и снимает все резервы entry — частичный
  откат недопустим;
- committed source release — только если entry удаляет source placement целиком; partial split и
  `DragAmountStep` планируются с занятым source footprint (tentative release + однократный
  replan);
- covered-cell interaction резолвится в логический placement/anchor;
- conversion может изменить shape — geometry по converted adapter;
- merge не создает footprint, но меняет reserved amount;
- `DragAmountStep`-trim — до `Commit`, остаточная бронь не утекает;
- same-inventory swap проверяет совместимость двух результирующих footprint;
- `SwapOperation` регистрирует эффекты (release обеих сторон + reserve обоих результатов) —
  «читающий» swap в atomic-плане ведет к double-book/недопланированию;
- план не адресует цели индексами/id — только ссылки; принадлежность по ссылке до первой мутации;
- `PlannedPlacement.Bind` — единственный механизм связывания; несколько `NewDynamicSlot`
  различаются объектами, не порядком создания;
- `ShiftAfterSlotRemoved` ребиндит placement на месте — план не должен переживать пересоздание
  объектов внутри собственного выполнения;
- atomic rollback и события сохраняют placement snapshots;
- маршрутизация переходного периода — по топологии (раздел 3).

### Детерминизм и JIT

- детерминизм планировщика — несущее требование: JIT-план BestEffort совпадает с preview только
  при детерминированных стратегиях/policies; контракт фиксируется документацией и
  consistency-тестом (этап 5);
- BestEffort preview — оценка: может быть оптимистичнее исполнения при доменных отказах;
  свойство best-effort, документируется;
- повторное планирование на drop — приемлемая цена (drop — редкое событие); baseline этапа 0
  подтверждает.

### Изменение контракта стратегий и policies

- удаление rules из кандидатов меняет контракт `IAcceptanceStrategy`; кастомные стратегии ищутся
  в audit этапа 0;
- candidate-source контракт перераспределяет владение порядком; бит-в-бит совместимость built-in
  комбинаций — contract-тестами этапа 2;
- source обязан поддерживать стабильное повторное ленивое перечисление и `kindMask`; policy не
  может требовать материализации потока;
- поглощение `IAlternativePlacementStrategy` — изменение публичного extension point; конфигурация
  blocked-поиска сохраняется как policy-override резолвера;
- стратегия без `CanFit` не ломает planning state — `TryReserve*` перевалидирует;
- `CollectParticipants` обязан быть полным: незаявленный участник = незахваченный snapshot =
  неоткатываемая мутация;
- legacy occupied-handlers без rollback-capability: «единственная мутирующая операция entry,
  исполняется последней»; полноценное участие — через transaction-aware интерфейс;
- occupied-handler для shaped — сознательный behavioral change (гейт A);
- `RulesScope` acceptance — сознательное выравнивание с planner.

### Dynamic slots

- placement без anchor-слота — только `PlannedPlacement` с NewDynamicSlot-намерением;
- executor резолвит ссылочные цели entry до первой мутации; отсутствующий объект —
  fail-before-mutation;
- после создания слота — проверка topology, `TryPlace`, `Bind`;
- rollback удаляет созданный слот через snapshot; не оставляет index-keyed DataBinding с ключами
  на чужие logical slots;
- characterization: создание, rollback, повторное использование source slot, реиндексация;
- grid + dynamic slot management остается запрещенным до отдельной модели расширяемой spatial
  topology.

### Производительность

- anchor scan — `O(anchorCount * footprintSize)`; offsets кэшируются;
- planning state обновляет occupancy инкрементально; checkpoint/rollback — журнал/undo-стек, не
  копия состояния;
- кандидаты — лениво и повторно, `kindMask` исключает оплату чужих фаз; eager-список на итерацию
  запрещен;
- acceptance не строит Unity-объекты, не мутирует inventory, не аллоцирует на anchor/вызов после
  прогрева;
- result-кэш acceptance вне scope (4.8);
- per-entry snapshots BestEffort ограничены инвентарями entry (`CollectParticipants`);
- benchmark/Profiler до и после этапа 7 на representative grid sizes;
- backtracking не требуется.

### Тестовая матрица

- slot и grid topology; single-cell и multi-cell; stackable, separable, unique;
- create, explicit merge, auto merge, merge в planned placement, alternative, swap;
- full и partial stack; same-inventory и cross-inventory (включая разные sources в batch);
- conversion с неизменной и измененной shape;
- batch atomic и best-effort, включая смешанный single-cell + shaped;
- отклоненный entry не оставляет следов в session;
- full move освобождает source footprint; partial split / rounded-down `DragAmountStep` — нет;
- эффекты swap в atomic-плане: ячейки результата заняты, освобожденные доступны;
- BestEffort/JIT: сбой entry A -> B перепланируется (может встать в другое место); сбой второй
  операции откатывает первую; отсутствие snapshot — fail-before-mutation; при отсутствии сбоев
  JIT-планы совпадают с preview; summary различает per-entry Succeeded/Failed с причинами;
- identity placement при `ShiftAfterSlotRemoved`; реиндексация не меняет смысл ссылочных целей;
  удаленный slot/placement — fail-before-mutation;
- legacy occupied-handler: handler-only entry исполняется, смешанный entry —
  fail-before-mutation;
- blocked-hint policies (бывшие `*AlternativePlacementStrategy`) — прежний порядок;
- dynamic slot create/rollback; drop на anchor и covered cell;
- placement snapshots в add/remove/swap events;
- каждый пункт чеклиста 4.7.

---

## 8. Критерии готовности

### Гейт A: этапы 0-4

- `PlacementPlanningSession` корректно резервирует эффекты нескольких операций; per-entry
  rollback без следов.
- Candidate contract: built-in стратегии и policies — бит-в-бит на slot-топологии, включая
  blocked-hint.
- Grid-топология полностью на едином пути: single-cell и shaped дают одинаковый список операций.
- Preview, planner и executor согласованы для grid-сценариев.
- Нет регрессии acceptance/hover по baseline.

### Полная консолидация: этапы 5-9

- Один topology-aware allocation service; atomic — общая session, BestEffort — JIT-replan
  per entry.
- Strategy/rules/geometry разделены; rules один раз; стратегии — через `IPlanningGeometry`, без
  знания топологии.
- Кандидаты — из повторно перечисляемого ленивого source; порядок у стратегии, обход у policy;
  один владелец порядка (`IAlternativePlacementStrategy` поглощен).
- План — список `PlannedOperation`; новый вид операции — новый класс, без правки модели/executor.
- Цели плана — только ссылки; индексов и id в модели нет.
- Acceptance — dry-run того же аллокатора через `PlacementPlanningRequest` с `RulesScope`.
- При отсутствии сбоев JIT-исполнение BestEffort воспроизводит preview-план (consistency-тест).
- Нет `IsSingleCell` branches, меняющих semantics; UI/rules/early-out fast paths разрешены.
- `VirtualSlotState`, `PlannedSlotAllocation`, `CanAcceptShape`, `TrySwapSlots`,
  `IAlternativePlacementStrategy` удалены.
- Batch ограничивается только явной strategy capability.
- Все characterization и новые тесты проходят; чеклист 4.7 покрыт contract-тестами.
- Architecture skills и публичная документация соответствуют реализации.

---

## 9. Открытые вопросы

### Агрегатные правила (решение не принято)

Текущие rules — предикаты над одной операцией («можно ли N штук сюда»). Класс правил над
агрегатом («суммарный вес инвентаря <= 100», «не больше 3 типов предметов») per-anchor проверкой
не выражается: три аллокации по 5 веса проходят поодиночке (90+5), сумма 105 нарушает лимит
невидимо для всех хук-точек.

- **A. Шов сейчас:** `IPlanAggregateRule` валидирует накопленные дельты плана по инвентарю
  **инкрементально на pre-Commit каждого entry** — нарушение = обычное отклонение entry через
  checkpoint-rollback, без post-hoc удаления entries из готового плана. В JIT-цикле BestEffort
  работает естественно (каждый entry — свое планирование). Ограничение v1: lightweight-режимы
  acceptance агрегатные правила не учитывают (preview — greedy-оценка, drop перевалидирует).
- **B. Вне scope:** агрегатные ограничения — зона domain handlers на выполнении. Дешевле сейчас;
  preview систематически лжет для таких правил, ретрофит в выпущенный pipeline дороже.

---

*Основные затрагиваемые области: `TransferPlanner`, `TransferPlanExecutor`,
`InventoryTransferService`, `PlacementStore`, `InventoryTopology`, `UniversalInventory`,
`InventoryAcceptanceRequest`, `VirtualSlotState`, `InventoryStrategyBase`, `IAcceptanceStrategy`,
`SlotAcceptanceCandidate`, `SlotSelectionPolicy`, `AutoTransferService`,
`Core/Drop/*AlternativePlacementStrategy`, `BlockedTargetResolverBase` и concrete strategies.*
