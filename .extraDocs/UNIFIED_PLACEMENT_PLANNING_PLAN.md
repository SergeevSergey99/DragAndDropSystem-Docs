# Unified Placement Transfer - Design Plan

**Цель:** перестать различать single-cell и shaped в transfer semantics, но не строить для этого
отдельную planning-систему. Single-cell является footprint из одной ячейки; выбор места,
валидация и mutation используют общие topology-aware контракты.

Главное упрощение: предварительно материализованного `TransferPlan` больше нет. Transfer service
последовательно обрабатывает entries по реальному состоянию inventory:

```text
validate request
for each entry:
    resolve candidates
    try concrete placements immediately
    commit or rollback this entry
return execution report
```

Сохраняются полезные границы:

1. topology отвечает за footprint, bounds и occupancy;
2. strategy отвечает за eligibility, capacity и базовый поток кандидатов;
3. `PlacementCandidateOrderer` сортирует кандидатов только при автоматическом выборе места;
4. `ITransferDomainHandler` выполняет domain validation и может отменить transfer;
5. transfer service владеет mutation, rollback, conversion и событиями;
6. UI и acceptance используют те же strategy/topology проверки, но не получают исполняемый план.

> Статус: **план, runtime-код не меняется.**
> Scope: полная консолидация slot/grid transfer pipeline, включая batch и shaped items.
> Публичного релиза не было: старые planner/resolver contracts удаляются без compatibility layer.
> Batch после рефактора всегда sequential best-effort. Режима `Atomic` для всего batch нет.

---

## 1. Почему planning-слой удаляется

Текущий `TransferPlanner` решает сразу несколько задач:

- выбирает target;
- считает capacity;
- строит виртуальное состояние batch;
- кодирует create/merge/swap/occupied-handler в разных моделях результата;
- повторяет часть domain validation и strategy logic;
- готовит данные, которые executor затем обязан интерпретировать точно так же.

Это создает второй runtime:

- `VirtualSlotState` параллелен реальному `PlacementStore`;
- `PlannedSlotAllocation` и `PlannedPlacementAllocation` параллельны mutation API;
- preview и execution строят разные планы в разное время;
- новый transfer case требует менять planner model и executor dispatch;
- корректность зависит от синхронизации двух сложных алгоритмов.

Для sequential batch виртуальное состояние не требуется. После успешного entry реальный
inventory уже содержит результат, и следующий entry видит его обычным candidate-resolution.
При ошибке откатывается только текущий entry.

Planning остается только как локальная read-only операция:

```text
resolve current candidates -> choose next candidate -> attempt mutation
```

Она не создает долгоживущий объект, не резервирует виртуальную occupancy и не отделена от
execution отдельным публичным этапом.

---

## 2. Что уже унифицировано

- `PlacementStore` хранит все logical placements.
- `IPlacementInventory.Topology` предоставляет активную `IInventoryTopology` без привязки
  общего placement-контракта к grid.
- `IInventoryTopology` проецирует shape и orientation в covered cells.
- `SlotTopology` всегда возвращает anchor-only footprint.
- `RectGridTopology` использует реальные shape offsets; custom topology может задать собственную
  проекцию через тот же контракт.
- `Placement` является logical location и может содержать stack.
- Placement-based swap уже умеет освобождать обе стороны и размещать их заново.

Следовательно, single-cell/shaped различаются только данными footprint, а не transfer
pipeline. Семантические ветки по `IsSingleCell` должны исчезнуть из placement, acceptance,
swap и batch execution. Визуальные ветки по форме остаются допустимыми.

---

## 3. Целевая модель

### 3.1 `PlacementCandidate`

Strategy возвращает topology-neutral кандидатов:

```text
PlacementCandidate
    Kind: Merge | Create | NewDynamicSlot
    Target: Placement | BaseSlot | null
    Anchor: BaseSlot | null
    Orientation
    Shape
    Capacity
```

- `Merge` ссылается на существующий logical `Placement`.
- `Create` содержит anchor и footprint intent.
- `NewDynamicSlot` означает создание нового slot через lifecycle capability.
- raw index не является identity target; индекс читается из `BaseSlot` только для topology API.

Кандидат не хранит mutation callback и не является частью исполняемого плана. Взаимовлияние
кандидатов (one-per-ID: использовать можно только один из пустых слотов) не кодируется в
кандидате — execution разрешает его повторным перечислением после mutation: следующий проход
вернет единственный placement с фактическим остатком capacity.

### 3.2 Strategy

Strategy после миграции является строго read-only policy:

```text
IStrategy
    TryGetCandidate(context, geometry, targetSlot, out candidate)
    GetCandidates(context, geometry) -> PlacementCandidateSource
    ResolveDragAmount(...)
    GetAcceptableCount(geometry, request)
```

Strategy определяет:

- допустимость конкретного выбранного slot/placement;
- transferable amount для конкретного target через `candidate.Capacity`;
- можно ли merge/create;
- max stack и candidate capacity;
- one-per-ID и separable stack semantics;
- допустимость dynamic slot;
- естественный deterministic порядок кандидатов.

Strategy не:

- добавляет и не удаляет stacks;
- создает slots;
- вызывает domain handlers;
- мутирует `PlacementStore`;
- выполняет fallback relocation.

Удаляются mutation-методы `TryAdd*`, `TryRemove`, `TryAddToSlot` из strategy contract, а также
флаги, существовавшие только для маршрутизации старого executor.

### 3.3 Geometry

Strategy не должна знать concrete topology:

```text
IPlacementGeometry
    EnumerateAnchors(order)
    GetPlacementAt(cellOrAnchor)
    CanPlace(shape, orientation, anchor, ignoredPlacement = null)
    GetCoveredCells(shape, orientation, anchor)
```

Реализация является read-only facade над реальным `PlacementStore` и `IInventoryTopology`.
Ни strategy, ни DataBinding, ни preview не определяют возможности placement через
`GridTopology`/`is grid`: они используют topology projection и общие placement primitives.

Защита от custom strategy остается в transfer service: перед mutation каждый create candidate
повторно проходит topology/bounds/occupancy validation.

### 3.4 Candidate source и orderer

`PlacementCandidateSource` является ленивым и повторно перечисляемым. После каждого успешного
placement transfer service запрашивает кандидатов заново, потому что реальное состояние уже
изменилось.

Он используется только для автоматического выбора места:

- area drop;
- auto-transfer;
- `AlternativeSlots` после blocked explicit target;
- продолжение распределения remainder по другим placements.

Если пользователь указал конкретный slot, transfer service сначала вызывает
`IStrategy.TryGetCandidate`. Успешный результат уже содержит kind, resolved placement/anchor и
доступное количество в `candidate.Capacity`, ограниченное requested amount. Для shaped inventory
выбранная covered cell сначала резолвится geometry в logical placement/anchor. Enumeration и
orderer для этой первой попытки не используются.

`false` означает, что выбранный target недоступен и mutation не было. Причиной может быть
incompatible item, zero capacity, blocked footprint или запрет strategy semantics. После этого
только `BlockedTargetResolutionKind` определяет, завершить entry, искать alternatives или делать
swap.

```text
PlacementCandidateSource
    Enumerate(kindMask)

PlacementCandidateOrderer
    Order(source, context) -> IEnumerable<PlacementCandidate>
```

Orderer:

- не расширяет eligibility;
- не вызывает domain handlers;
- не мутирует inventory;
- только фильтрует фазы и задает порядок;
- должен быть deterministic.

Built-in orderers:

- `Natural`;
- `MergeFirst`;
- `EmptyFirst`;
- `MergeOnly`;
- `EmptyOnly`.

`SlotSelectionPolicyBase` и `IAlternativePlacementStrategy` заменяются одним
`PlacementCandidateOrderer`.

### 3.5 Blocked target policy

Полиморфная resolver-иерархия не нужна:

```text
BlockedTargetResolutionKind
    Reject
    AlternativeSlots
    Swap
```

`DropPolicySettings` хранит data-only настройки:

```text
BlockedTargetResolutionKind
AlternativeOrderer              // показывается только для AlternativeSlots
AllowSameInventoryAlternative   // показывается только для AlternativeSlots
PartialTransferMode
```

Inspector defaults сохраняют текущее ожидаемое поведение:

- `BlockedTargetResolutionKind.AlternativeSlots`;
- `MergeFirst` alternative orderer;
- same-inventory alternative разрешен;
- partial transfer разрешен.

`AlternativeOrderer` является `[SerializeReference]` field и показывается только для
`AlternativeSlots`. Для area drop без blocked hint используется strategy default orderer либо
request override, а не alternative-specific field.

Orderer никогда не применяется к выбранному пользователем slot. Он участвует только там, где
transfer service должен выбрать один target из нескольких автоматических candidates.

Поведение:

- `Reject`: explicit blocked hint завершает entry отказом;
- `AlternativeSlots`: hinted target остается нетронутым, transfer service перечисляет остальные
  candidates через configured orderer;
- `Swap`: выполняется swap с конкретным occupied hinted placement.

Удаляются:

- `BlockedTargetResolverBase`;
- `BlockedTargetResolution`;
- `BlockedTargetResolutionContext`;
- `RejectBlockedTargetResolver`;
- `FindAlternativeBlockedTargetResolver`;
- `SwapBlockedTargetResolver`;
- `ISwapStrategy`;
- `HintedTargetSwapStrategy`;
- `SwapSearchContext`.

Если в будущем появится другой вид реакции, он добавляется как новый enum case с отдельным
явным execution path. Абстрактный resolver заранее не создается.

---

## 4. Unified transfer pipeline

### 4.1 Public boundary

Основной API:

```text
InventoryTransferService
    ExecuteAsync(TransferRequest, CancellationToken) -> TransferExecutionReport
    Probe(TransferRequest) -> TransferProbe
```

`ExecuteAsync` не принимает `TransferPlan`.

`TransferRequest` содержит:

- `DragContext`;
- target inventory;
- optional target hint;
- resolved blocked-target policy;
- partial transfer mode;
- optional orderer override.

`TransferProbe` является advisory UI result, а не исполняемым объектом.

### 4.2 Batch boundary

Batch всегда обрабатывается последовательно:

```text
ExecuteAsync(request):
    validate common request
    validate ITransferDomainHandler at request scope once
    validate optional IAsyncTransferDomainHandler at request scope once

    for each entry in stable DragContext order:
        result = TryTransferEntry(entry, current real inventory state)
        append result

    return report
```

`ITransferDomainHandler` остается синхронной domain extension point. Перед началом transfer он
получает полный контекст операции и может отменить обработку всего request.

Опциональный `IAsyncTransferDomainHandler.CanStartTransferAsync(...)` решает ту же задачу для
server-backed и других асинхронных проверок. Он вызывается ровно один раз асинхронным execution
path до первой mutation. Наличие handler не является обязательным, core не требует и не
предоставляет симуляцию. Пользователь при желании может выполнить внутри handler собственную
симуляцию. Синхронный execution при наличии async handler отклоняет request вместо обхода
проверки.

Успешный результат transfer-wide handler означает только «можно начать best-effort обработку»,
а не гарантирует успешность всех entries.

Тот же handler lifecycle может проверять уже выбранный concrete candidate перед mutation.
Strategy отвечает за inventory semantics и capacity; domain handler — за внешние бизнес-условия.

Гарантии:

- успешные ранние entries не откатываются из-за позднего отказа;
- failed entry не оставляет частичных mutation;
- следующий entry видит реальный результат предыдущих;
- successful entry commit-ит events/DataBinding до перехода к следующему entry;
- report содержит отдельный status и reason для каждого entry;
- success batch означает, что хотя бы один entry перенесен;
- `IsPartial` означает, что не все requested entries/amount были перенесены.

Target hint в batch применяется только к первому entry:

- первый entry сначала пытается использовать hinted placement;
- если hint blocked, к нему применяется configured blocked policy;
- последующие entries работают как area drop и перечисляют обычные candidates;
- hint не переиспользуется после failure первого entry;
- batch + `Swap` отклоняется целиком до начала loop (6.2).

`BatchMode` и `Atomic` удаляются из:

- `DropPolicySettings`;
- `DropRequestPolicy`;
- `ResolvedDropPolicy`;
- planner/executor;
- UI.

Full-batch rollback, projected views и plan-all отсутствуют.

### 4.3 Entry transaction

Каждый entry имеет локальную transaction boundary:

```text
TryTransferEntry(entry):
    resolve target-side conversion preview

    capture source/target checkpoints
    create working transfer stack

    if explicit target exists:
        candidate = strategy.TryGetCandidate(target)
        if candidate is available:
            try candidate directly without orderer
        else:
            apply BlockedTargetResolutionKind

    while remainder exists and automatic placement is allowed:
        candidates = strategy.EnumerateCandidates(current state)
        candidate = orderer.Select(candidates)
        try candidate

    before each candidate mutation:
        validate topology and capacity again
        validate ITransferDomainHandler for concrete candidate
        apply candidate mutation

    if RequireFull and remainder exists:
        rollback entry
        return failed

    restore unplaced remainder to source
    if remainder restore failed:
        rollback entry
        return failed

    commit entry
    dispatch deferred entry events/DataBinding notifications
    return EntryTransferResult
```

Для inventory checkpoints используется существующий snapshot contract либо его узкая замена.
Общая plugin-style transaction framework не вводится до появления реального внешнего
participant, который невозможно выразить inventory snapshot.

События и DataBinding notifications отправляются только после успешного commit entry и до
обработки следующего entry. Иначе domain handlers и external model следующего entry увидят
состояние, отличающееся от runtime inventory.

**Контракт количеств в событиях:** каждое add/remove событие несет точный перенесенный
sub-stack своего outcome (как сегодняшний `CreateCopy(actuallyAdded)`), DataBinding никогда не
получает количество из `DragEntry.Stack`. Entry из 10 предметов, полностью разложенный двумя
outcomes `6 + 4`, дает события суммарно на 10 адаптеров. Entry из 10 предметов, у которого
перенеслось 6, а 4 вернулись source как remainder, дает события только на 6 адаптеров. Поэтому
разбиение `DragEntry` на несколько entries не требуется и запрещено: размеры outcome выясняются
в процессе размещения, а искусственное разбиение сломает per-entry `RequireFull` и report
относительно жеста пользователя.

### 4.4 Concrete placement attempt

Mutation выполняет transfer service через узкие inventory primitives:

```text
TryApplyCandidate(workingStack, candidate) -> CandidateTransferResult

CandidateTransferResult
    TransferredAmount
    RemainingAmount
    Outcome
    FailureReason
```

Инварианты:

- `TransferredAmount == 0` означает отсутствие mutation;
- успешная операция переносит точно заранее вычисленный amount;
- create/merge mutation является all-or-nothing для этого amount;
- при локальном отказе отделенный sub-stack возвращается в `workingStack`;
- transfer service не вызывает strategy mutation API;
- после успеха candidates перечисляются заново по актуальному state;
- публичные convenience-API инвентаря (`TryAddStack` и подобные) сохраняются для геймплейного
  кода и реализуются поверх тех же narrow mutation primitives.

Это позволяет одному entry иметь несколько outcomes без отдельной planned-operation model.

### 4.5 Результаты

Каноническая result chain:

```text
PlacementTransferOutcome
    Kind: Create | Merge | Swap | OccupiedHandler
    SourcePlacementSnapshot
    TargetPlacementSnapshot
    SourceItem
    TargetItem
    Amount

EntryTransferResult
    Status: Succeeded | Partial | Failed
    RequestedAmount
    TransferredAmount
    RemainingAmount
    FailureReason
    Outcomes[]

TransferExecutionReport
    EntryResults[]
    RequestedAmount
    TransferredAmount
    SucceededEntries
    FailedEntries
    IsPartial

DropResult
    compatibility projection of TransferExecutionReport
```

Удаляются параллельные DTO:

- `InventoryTransferResult`;
- `ExecutedTransferEntry`;
- `SwapOperationResult`;
- `PendingSwapOutcome`;
- `TransferExecutionSummary`.

`TransferKind` задается concrete execution path, а не выводится из уже измененного slot state.

---

## 5. Partial transfer и большие stacks

### 5.1 Один stack, несколько placements

Большой stack является одним entry, даже если target strategy распределяет его по нескольким
placements.

Пример: stack из 10 предметов переносится в Unique inventory с шестью свободными slots.

```text
RequestedAmount = 10
TransferredAmount = 6
RemainingAmount = 4
Outcomes = 6 create outcomes по одному предмету
```

Алгоритм:

1. Создать `workingStack` запрошенного размера.
2. Strategy возвращает candidates с capacity.
3. Unique candidate принимает `1`; separable stack принимает до своего max; merge принимает
   remaining capacity placement.
4. Transfer service отделяет exact amount и применяет candidate.
5. После успеха candidates строятся заново.
6. Когда candidates закончились, остаток возвращается в source.

### 5.2 Partial policy

```text
PartialTransferMode
    Allow
    RequireFull
```

- `Allow`: успешные outcomes commit, remainder возвращается в source.
- `RequireFull`: любой remainder откатывает весь текущий entry.

`RequireFull` не откатывает предыдущие entries batch.

### 5.3 Возврат remainder

Возврат остатка является частью entry transaction:

- partial split возвращается в исходный placement/stack;
- concrete adapter instances сохраняются;
- outgoing/incoming conversion не должна подменять adapters, возвращаемые source;
- если remainder невозможно вернуть, откатываются source и target checkpoints;
- events отражают только фактически committed amount.

### 5.4 `DragAmountStep`

`DragAmountStep` применяется к фактически transferable amount без materialized plan:

1. выполнить entry attempt под checkpoint;
2. вычислить допустимый rounded amount;
3. если transfer должен быть уменьшен, rollback;
4. повторить entry один раз с rounded cap.

Это медленнее только для редкого trim case, но удаляет отдельную reservation/trim model.

### 5.5 Source footprint при same-inventory move

Source footprint нельзя безусловно освобождать при partial transfer:

- partial attempt сохраняет source placement занятым;
- candidate source исключает source placement;
- full relocation может выполнить отдельную попытку с временно удаленным source placement;
- такая попытка обязана перенести весь entry;
- при remainder выполняется полный rollback.

Так target placements не смогут занять source cells, после чего remainder уже нельзя было бы
вернуть обратно.

---

## 6. Swap

### 6.1 Single-entry swap

Swap является специальным entry path внутри transfer service, а не strategy и не planned
operation.

Условия v1:

- `DragContext` содержит ровно один entry;
- задан concrete occupied target placement;
- переносится весь source placement/stack;
- обе стороны поддерживают snapshot rollback;
- forward и reverse conversion успешны;
- strategy и domain validation пройдены в обе стороны;
- обе resulting footprints помещаются после освобождения исходных placements;
- same-inventory resulting footprints не пересекаются.

Execution:

```text
capture both inventories
remove source placement
remove target placement
place converted source at target anchor
place converted target at source anchor
commit both or rollback both
```

### 6.2 Batch drop с `Swap`

В v1 batch swap не поддерживается.

Если несколько dragged entries dropped на один occupied target при
`BlockedTargetResolutionKind.Swap`, весь request отклоняется до mutation:

```text
Swap requires a single full entry
```

Нельзя silently:

- swap только первый entry;
- переместить остальные как alternatives;
- последовательно менять содержимое одного target.

Это были бы разные и неожиданные semantics.

### 6.3 Future group exchange

Кейс `9 x 1x1` из grid `3x3` против одного `3x3` placement полезен, но является не batch swap,
а отдельным group exchange с собственной all-or-nothing boundary:

- освобождаются два набора placements;
- большой placement размещается в области source group;
- source group упаковывается в область target placement;
- операция commit/rollback целиком.

Такой `GroupExchange` требует отдельного контракта областей и packing semantics. Он оставляется
на будущее и не удерживает planner/resolver infrastructure в v1.

---

## 7. Occupied handlers и dynamic slots

### 7.1 Occupied handler

Приоритет остается:

```text
explicit occupied hint:
    occupied handler -> normal merge/create attempt -> blocked policy
```

Handler path является отдельным entry execution:

- pure check до mutation;
- execution должен быть all-or-nothing;
- `false` означает, что mutation не было;
- handler сам не запускает alternative/swap;
- success events откладываются до завершения entry.

Если external model не поддерживает rollback, handler не смешивается с другими mutations одного
entry.

### 7.2 Dynamic slots

Dynamic inventory выражает возможность создать новый slot через lifecycle capability.
Strategy может вернуть `NewDynamicSlot` candidate, но не создает slot сама.

Transfer service:

```text
create slot
try exact placement
on failure remove created slot
on success record outcome
```

Удаляются:

- `DynamicSlotDecorator`;
- `SlotManagementSettingsBase.WrapRuntimeStrategy`;
- deferred executor fallback через `TryAddStack(-1)`.

`TryAddStackQuiet` не является transfer API. DataBinding restore получает отдельный
`ImportStackQuiet`/`HydrateStack` primitive.

### 7.3 Relocation

`SlotRelocationService` и implicit repacking удаляются.

Transfer использует deterministic greedy candidates. Он не перемещает посторонние stacks, чтобы
освободить место. Если relocation потребуется в будущем, это отдельная пользовательская
операция, а не скрытый retry.

---

## 8. Acceptance и preview

Acceptance не строит `TransferPlan`.

Общий read-only resolver предоставляет:

```text
TransferProbe
    CanAttempt
    FailureReason
    EntryIndex
    Entry
    Candidate
    AnchorSlot
    Orientation
    CoveredSlots
```

`Probe` использует те же:

- target-side conversion;
- `IStrategy.TryGetCandidate` для выбранного slot;
- strategy candidate source и orderer только для автоматического выбора;
- domain validation;
- topology validation.

При explicit target `Candidate` получается напрямую через `IStrategy.TryGetCandidate`, без
enumeration и orderer. Для area drop/auto-transfer он выбирается из ordered candidate source.
Probe не пытается предсказать общее transferable amount для multi-placement stack.

`GetAcceptableCount(geometry, request)` сохраняет exact read-only контракт и использует ту же
topology-neutral geometry, что candidate resolution. Probe не заменяет его и не является
execution guard.

Ограничения:

- probe не резервирует state;
- execution всегда перевалидирует;
- exact batch packing не обещается;
- batch preview показывает только общий target и возможность попытки;
- execution report является единственным authoritative result.

Single-entry preview может точно показать первый create/merge target и footprint. Для
multi-placement stack подсветка всех будущих targets не требуется.

`InventoryDropArea` не делает отдельный `CanAcceptItem -> CanAcceptDrop` chain.
`DropPreviewController` только отображает `TransferProbe`.
На release inventory pipeline сразу вызывает `ExecuteAsync`; предварительный `CanAcceptDrop`
не является execution guard.

---

## 9. Что удаляется

### Planning/execution

- `TransferPlanner`;
- `TransferPlan`;
- `PlannedEntryTransfer`;
- `PlannedSlotAllocation`;
- `PlannedPlacementAllocation`;
- `VirtualSlotState`;
- `EntryPlanningOperation`;
- `PlannedOperation`;
- `PlanningReservation`;
- `PlacementPlanningSession`;
- planner/executor split как runtime boundary.

`TransferPlanExecutor` заменяется единым `InventoryTransferService`, который владеет
candidate-resolution и mutation.

### Policy

- `BatchMode`;
- `Atomic`;
- `BlockedTargetResolverBase` и concrete resolvers;
- `BlockedTargetResolution`;
- `BlockedTargetResolutionContext`;
- `ISwapStrategy`;
- `HintedTargetSwapStrategy`;
- `SwapSearchContext`.

### Strategies

- strategy mutation API;
- `IAlternativePlacementStrategy`;
- `SlotSelectionPolicyBase`;
- `DynamicSlotDecorator`;
- `IInventoryQueryStrategy`;
- `RequiresStrategyPlacement`;
- `UsesPerItemSlotPlanning`;
- `CanUseAlternativeSlot`.

### Legacy helpers

- `TargetPlacementOperation`;
- `SlotOperationContext`;
- `SlotRelocationService`;
- `UniversalInventory.TrySwapSlots`;
- deferred `TryAddStack(-1)` transfer path;
- parallel execution result DTO.

---

## 10. Что остается отдельным

1. UI и rendering могут проверять форму и orientation.
2. `ITransferDomainHandler` может запрещать transfer по внешним бизнес-условиям.
3. `IAsyncTransferDomainHandler` может асинхронно запретить весь transfer до первой mutation.
4. Single-cell fast path допустим внутри topology/storage при той же semantics.
5. Optimal packing и backtracking вне scope.
5. Group exchange вне scope.
6. Sorting/repacking inventory является отдельной action.
7. Hydration/import не проходит через transfer pipeline.
8. Транзакционный all-or-nothing batch не предоставляется: events/DataBinding фиксируются после
   каждого entry. `ITransferDomainHandler` может отменить весь transfer до первой mutation. Core
   не выполняет для него симуляцию и не трактует успешный verdict как гарантию полного
   выполнения; пользователь при необходимости может реализовать собственную симуляцию внутри
   handler.

---

## 11. Этапы реализации

### Этап 0. Characterization

- Зафиксировать single-cell и shaped create/merge/move/swap.
- Зафиксировать occupied handler priority.
- Зафиксировать blocked target: reject/alternative/swap.
- Зафиксировать alternative order: Natural/MergeFirst/EmptyFirst/MergeOnly/EmptyOnly.
- Зафиксировать same-inventory source exclusion.
- Зафиксировать dynamic slot create/remove.
- Зафиксировать partial large stack:
  - Unique: один предмет на placement;
  - Separable: merge + spill;
  - Stackable one-per-ID;
  - remainder возвращается source.
- Зафиксировать `RequireFull`.
- Зафиксировать `DragAmountStep`.
- Зафиксировать batch best-effort.
- Зафиксировать swap geometry и bidirectional strategy/domain validation.
- Зафиксировать current double preview/execution calls.
- Зафиксировать request-level veto существующего `ITransferDomainHandler`; core simulation не
  вводится.
- Снять performance baseline acceptance и transfer.

### Этап 1. Policy simplification

- Ввести `BlockedTargetResolutionKind`.
- Перенести alternative orderer и same-inventory flag в data-only settings.
- Зафиксировать defaults: AlternativeSlots + MergeFirst + same-inventory alternative + partial.
- Перевести `DropRequestPolicy` и `ResolvedDropPolicy` на nullable scalar overrides.
- Удалить resolver hierarchy и swap strategy.
- Удалить `BatchMode`/`Atomic`; batch semantics становится фиксированной best-effort.
- Обновить сериализованные scenes/profiles. Нужна явная asset migration, потому что старые
  resolver values хранятся как `SerializeReference`.

### Этап 2. Candidate contract

- Ввести topology-neutral `PlacementCandidate`.
- Ввести lazy `PlacementCandidateSource`.
- Ввести `PlacementCandidateOrderer`.
- Переименовать `IPlacementStrategy` в `IStrategy`.
- Добавить `IStrategy.TryGetCandidate` для прямой проверки выбранного slot/placement и capacity.
- Переделать `SlotSelectionPolicyBase` и `IAlternativePlacementStrategy` implementations в
  orderers.
- Ввести `IPlacementGeometry`.
- Contract tests: explicit target не вызывает enumeration/orderer; eligibility не меняется от
  orderer; built-in automatic order сохраняется.

### Этап 3. Unified entry transfer для grid

- Ввести `InventoryTransferService.TryTransferEntry`.
- Grid topology полностью перевести на candidate loop: single-cell и shaped.
- Create/merge выполняются через narrow placement mutation API.
- Реализовать `EntryTransferResult`, outcomes и deferred events.
- Реализовать entry rollback.
- Реализовать same-inventory full relocation и partial source-footprint rule.
- Реализовать single-entry swap без planner.

### Контрольная точка A

- Grid single-cell/shaped используют один pipeline.
- Нет materialized plan или virtual occupancy.
- Swap, partial stack и rollback проходят characterization tests.
- Performance не хуже baseline на значимую величину.

### Этап 4. Slot topology и strategies

- Slot topology перевести на тот же candidate loop.
- Strategy сделать read-only.
- Удалить strategy mutation flags/methods.
- `NewDynamicSlot` выполнять через lifecycle capability.
- Удалить `DynamicSlotDecorator`, relocation и deferred placement.
- Разделить public read-only inventory view и internal mutation surface.
- Удалить `IInventoryQueryStrategy`; queries читают logical placements.

### Этап 5. Sequential batch

- `ExecuteAsync` проходит entries в stable order.
- Per-entry rollback, без full-batch rollback.
- Failed entry не прерывает остальные.
- Расширить существующий `ITransferDomainHandler` request-level проверкой полного контекста
  операции до первой mutation; default handler отсутствует, поэтому обычный transfer идет сразу.
- Сохранить необязательный `IAsyncTransferDomainHandler` для transfer-wide асинхронного veto до
  первой mutation. Sync execution при его наличии не выполняет transfer.
- Target hint использует только первый entry; остальные идут как area drop.
- После successful entry DataBinding/events commit-ятся до следующего entry.
- Batch + `Swap` отклонять до mutation.
- Mixed single-cell/shaped batch работает по реальному state.
- Report различает failed entry и partial amount.

### Этап 6. Acceptance/UI

- Ввести `TransferProbe`.
- `CanAcceptItem`, `GetAcceptableCount`, hover и preview перевести на общие read-only
  strategy/domain/topology primitives.
- Удалить двойное planning/acceptance.
- `InventoryDropArea` больше не хранит отдельно выбранный slot.
- `DropPreviewController` становится renderer.
- Release вызывает execution напрямую.

### Этап 7. Cleanup и документация

- Удалить planner/executor models и parallel DTO.
- Удалить semantic `IsSingleCell` branches.
- Обновить `.agents/skills/dragdrop-*` и зеркальную документацию.
- Обновить `COMPONENTS.md`, `DATA_FLOW.md`, `STRATEGIES.md`.
- Документировать extension points:
  - strategy;
  - topology;
  - candidate orderer;
  - `ITransferDomainHandler`;
  - `IAsyncTransferDomainHandler`;
  - dynamic slot lifecycle.
- Документировать ограничения batch swap и advisory preview.

Каждый этап должен компилироваться и проходить свой test subset.

---

## 12. Риски и обязательные проверки

### Correctness

- failed candidate с amount `0` не мутирует state;
- failed entry полностью восстанавливает source и target;
- successful partial entry возвращает exact adapter remainder;
- events соответствуют только committed outcomes;
- события строятся из фактического outcome sub-stack, никогда из `DragEntry.Stack` —
  multi-placement entry не дает двойного счета в DataBinding;
- full relocation может использовать освобожденный source footprint;
- partial transfer не отдает source footprint target placements;
- covered-cell hint резолвится в logical placement;
- conversion меняет target shape до topology validation;
- dynamic slot удаляется при failed candidate/entry;
- same-inventory move не превращается в no-op с несбалансированными events;
- orderer не может вернуть ineligible candidate как допустимый;
- explicit target не проходит через orderer;
- swap валидирует обе стороны и resulting footprint overlap.

### Batch

- entries выполняются в стабильном порядке;
- следующий entry видит mutation предыдущего;
- следующий entry видит committed DataBinding/external state предыдущего;
- failed entry не откатывает успешные предыдущие;
- failed entry не блокирует последующие;
- request-level `ITransferDomainHandler` получает полный контекст операции;
- domain veto отклоняет batch до первой mutation;
- target hint применяется только к первому entry;
- batch + occupied target + Swap отклоняется до mutation;
- mixed topology sources не требуют virtual bridge.

### Large stack

- Unique распределяет по одному предмету;
- Separable merge-ит и создает несколько stacks до capacity;
- Stackable one-per-ID не открывает второй logical placement;
- `Allow` commit-ит transferable amount;
- `RequireFull` откатывает entry;
- remainder сохраняет concrete adapter instances и source order;
- `DragAmountStep` retry не эмитит события первой попытки.

### Performance

- candidate source ленивый;
- orderer не материализует поток без необходимости;
- после каждого success выполняется только нужный re-enumeration;
- topology offsets кэшируются;
- snapshots ограничены source/target текущего entry;
- probe не создает Unity objects;
- нет allocations per anchor после прогрева;
- benchmark: small/large slot inventory, grid, large Unique stack, separable spill, batch.

---

## 13. Тестовая матрица

- slot/grid topology;
- single-cell/multi-cell;
- stackable/separable/unique;
- explicit target/area drop/auto-transfer;
- explicit target:
  - available create/merge возвращает candidate с exact capacity;
  - unavailable target возвращает `false` без mutation;
  - covered cell резолвится в logical placement;
  - enumeration и orderer не вызываются;
- create/merge/alternative/reject;
- full/partial stack;
- same-inventory/cross-inventory;
- conversion с неизменной и измененной shape;
- covered-cell drop;
- dynamic slot;
- occupied handler;
- single-entry swap:
  - slot-slot;
  - grid-grid;
  - cross-topology;
  - shaped footprint;
  - overlapping result rejection;
  - partial source rejection;
- batch:
  - все entries успешны;
  - middle entry failed, later succeeds;
  - capacity заканчивается;
  - shaped + single-cell;
  - explicit hint используется только первым entry;
  - DataBinding первого entry обновлен до validation второго;
  - request-level domain veto не оставляет mutations;
  - Swap policy rejects whole request;
- large stack:
  - 10 -> Unique capacity 6 gives 6/4;
  - same case RequireFull gives 0/10;
  - merge + spill;
  - max stack cap;
  - remainder restore failure triggers rollback;
- same-inventory:
  - full relocation into old source footprint;
  - partial transfer cannot consume source footprint;
- preview:
  - explicit target использует тот же `IStrategy.TryGetCandidate`, что execution;
  - automatic placement использует тот же candidate source/orderer, что execution;
  - advisory result can become stale before execution without data loss;
- `ITransferDomainHandler`:
  - request-level veto отклоняет весь request до любых mutations;
  - отсутствие handler означает отсутствие дополнительной domain validation;
  - пользовательский handler может выполнить собственную симуляцию, но core ее не предоставляет;
  - concrete candidate может быть отклонен до mutation;
- events/DataBinding:
  - no events on rollback;
  - balanced remove/add per outcome;
  - entry 10 -> outcomes 6+4: remove/add notifications суммарно содержат ровно 10 адаптеров;
  - entry 10 -> transferred 6 + remainder 4: notifications содержат ровно 6, source сохраняет 4;
  - correct placement snapshots.

---

## 14. Критерии готовности

- Один topology-aware transfer service для slot и grid.
- `TransferPlan`, virtual planning state и planner/executor split отсутствуют.
- Batch всегда sequential best-effort; `Atomic` отсутствует в API и Inspector.
- Каждый entry rollback-safe.
- Strategy read-only и topology-neutral.
- `IStrategy.TryGetCandidate` проверяет выбранный slot/placement и возвращает его capacity.
- `IStrategy.EnumerateCandidates` используется только для автоматического распределения.
- `PlacementCandidateOrderer` не участвует в explicit-target path.
- `ITransferDomainHandler` является единственной domain validation extension point; обязательной
  симуляции нет.
- `BlockedTargetResolutionKind` заменяет resolver hierarchy.
- Swap strategy hierarchy отсутствует.
- Single-entry swap сохраняет placement geometry и rollback.
- Batch swap явно отклоняется и документирован как future group exchange.
- Большой stack может распределиться по нескольким placements.
- Partial remainder гарантированно возвращается source.
- `RequireFull` откатывает только текущий entry.
- Dynamic slots создаются transfer service, не strategy.
- Implicit relocation отсутствует.
- Acceptance/UI используют общий candidate resolver без executable plan.
- Result chain одна; parallel DTO отсутствуют.
- Characterization, contract и integration tests проходят.
- Architecture skills и публичная документация соответствуют реализации.

---

*Основные затрагиваемые области: `TransferPlanner`, `TransferPlanExecutor`,
`InventoryTransferService`, `InventoryDropProcessor`, `DropPolicy`, `DropPolicySettings`,
`DropRequestPolicySettings`, `PlacementStore`, `InventoryTopology`, `UniversalInventory`,
`InventoryAcceptanceRequest`, `InventoryStrategyBase`, `IStrategy`,
`IAcceptanceStrategy`, `IInventoryQueryStrategy`, `SlotSelectionPolicy`,
`Core/Drop/*AlternativePlacementStrategy`, `BlockedTargetResolverBase`, `ISwapStrategy`,
`DynamicSlotDecorator`, `SlotRelocationService`, `TargetPlacementOperation`,
`InventoryDropArea`, `DropPreviewController`, `AutoTransferService`, DataBinding и tests.*
