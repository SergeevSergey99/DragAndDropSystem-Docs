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
3. `PlacementCandidateOrderer` отвечает только за предпочтительный порядок;
4. rules валидируют drag/drop request и конкретный target;
5. transfer service владеет mutation, rollback, conversion и событиями;
6. UI и acceptance используют тот же candidate-resolution, но не получают исполняемый план.

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
- повторяет часть rules и strategy logic;
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
- `IInventoryTopology` проецирует shape и orientation в covered cells.
- `SlotTopology` всегда возвращает anchor-only footprint.
- Grid topology использует реальные shape offsets.
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
    Phase
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
IPlacementStrategy
    EnumerateCandidates(context, geometry) -> PlacementCandidateSource
    GetCapacity(candidate, item)
    ResolveDragAmount(...)
    DefaultOrderer
    capabilities
```

Strategy определяет:

- можно ли merge/create;
- max stack и candidate capacity;
- one-per-ID и separable stack semantics;
- допустимость dynamic slot;
- базовый deterministic порядок кандидатов;
- capability ограничения для batch или конкретной topology.

Strategy не:

- добавляет и не удаляет stacks;
- создает slots;
- вызывает rules;
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

Защита от custom strategy остается в transfer service: перед mutation каждый create candidate
повторно проходит topology/bounds/occupancy validation.

### 3.4 Candidate source и orderer

`PlacementCandidateSource` является ленивым и повторно перечисляемым. После каждого успешного
placement transfer service запрашивает кандидатов заново, потому что реальное состояние уже
изменилось.

```text
PlacementCandidateSource
    Enumerate(kindMask)

PlacementCandidateOrderer
    Order(source, context) -> IEnumerable<PlacementCandidate>
```

Orderer:

- не расширяет eligibility;
- не вызывает rules;
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
- global/inventory rule scope;
- optional orderer override.

`TransferProbe` является advisory UI result, а не исполняемым объектом.

### 4.2 Batch boundary

Batch всегда обрабатывается последовательно:

```text
ExecuteAsync(request):
    validate common request
    validate global/inventory rules at context scope once
    await request-level domain veto        // может запретить весь request; до любых mutations

    for each entry in stable DragContext order:
        result = TryTransferEntry(entry, current real inventory state)
        append result

    return report
```

Async veto существует ровно в двух точках: request-level (выше) и entry-level (4.3), обе — до
mutations своего scope. Post-mutation veto не существует: после commit entry внешняя логика
реагирует через DataBinding/events, но не откатывает. Существующие
`ITransferDomainHandler`/`IAsyncTransferDomainHandler` мигрируют на эти две точки. Поскольку
`await` уступает кадры, кандидаты резолвятся строго после veto своего scope — состояние,
изменившееся за время ожидания, увидится обычным candidate-resolution.

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
- batch + `Swap` отклоняется целиком до начала loop (7.2).

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
    validate source/start rules
    resolve target-side conversion preview
    await entry-level domain veto          // может запретить этот entry; до его mutations

    capture source/target checkpoints
    create working transfer stack

    attempt explicit hint
    if blocked:
        apply BlockedTargetResolutionKind
    else:
        continue candidate loop while remainder exists

    before each candidate mutation:
        validate inventory/slot rules against concrete target
        validate topology and capacity again

    return unplaced remainder to source

    if RequireFull and remainder exists:
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
обработки следующего entry. Иначе rules и external model следующего entry увидят состояние,
отличающееся от runtime inventory.

**Контракт количеств в событиях:** каждое add/remove событие несет точный перенесенный
sub-stack своего outcome (как сегодняшний `CreateCopy(actuallyAdded)`), DataBinding никогда не
получает количество из `DragEntry.Stack`. Entry из 10 предметов, разложенный 6+4, дает два
сбалансированных события со стеками 6 и 4. Поэтому разбиение `DragEntry` на несколько entries
не требуется и запрещено: его нельзя выполнить заранее (размеры кусков выясняются в процессе
размещения), оно ломает per-entry семантику `RequireFull` и entry-level veto и раздувает report
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

## 5. Rules

Отдельной системы `IPlanRule` нет.

Существующая rule-система получает две явные drop-фазы:

```text
IDragRule
    CanStartDrag(context, entry)
    CanDropContext(context)       // один раз на request; default Success
    CanDrop(context, entry)       // concrete entry/target
```

Это расширение существующего `IDragRule`/`RuleValidator`, а не новая коллекция правил.
`DragRuleBase.CanDropContext` по умолчанию возвращает success, поэтому обычным item/slot rules
не требуется дополнительная реализация.

Global/inventory rules получают полный `DragContext` и могут видеть весь requested batch. Они
остаются read-only и проверяют:

- возможность start drag;
- aggregate допустимость request до первой mutation;
- допустимость item для target inventory;
- конкретный target candidate;
- aggregate свойства request: суммарный вес, число entries, source inventories, item types.

Rules не проверяют projected или будущий inventory state. Фактическая вместимость определяется
последовательными placement attempts.

Разделение:

- request-level aggregate rule реализует `CanDropContext` и читает `context.Entries`; failure
  отклоняет request до первой mutation;
- entry-level rule читает текущий `entry`;
- slot/target rule вызывается только после выбора concrete anchor через context copy с этим
  target;
- topology validation выполняется transfer service, не rule.

`RuleEvaluationService` вызывает `CanDropContext` только у global и target-inventory
validators. Slot validators участвуют только в concrete candidate validation.

Rule не может мутировать inventory. Результат позднего capacity exhaustion является штатным
partial/failure result, а не нарушением rule.

Если требуется правило вида «после переноса inventory должен весить не больше N», оно может
вычислить это из live inventory + requested entry до mutation. Для сложного best-effort batch
правило либо оценивает весь request консервативно, либо проверяется перед каждым entry по
актуальному state.

---

## 6. Partial transfer и большие stacks

### 6.1 Один stack, несколько placements

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

### 6.2 Partial policy

```text
PartialTransferMode
    Allow
    RequireFull
```

- `Allow`: успешные outcomes commit, remainder возвращается в source.
- `RequireFull`: любой remainder откатывает весь текущий entry.

`RequireFull` не откатывает предыдущие entries batch.

### 6.3 Возврат remainder

Возврат остатка является частью entry transaction:

- partial split возвращается в исходный placement/stack;
- concrete adapter instances сохраняются;
- outgoing/incoming conversion не должна подменять adapters, возвращаемые source;
- если remainder невозможно вернуть, откатываются source и target checkpoints;
- events отражают только фактически committed amount.

### 6.4 `DragAmountStep`

`DragAmountStep` применяется к фактически transferable amount без materialized plan:

1. выполнить entry attempt под checkpoint;
2. вычислить допустимый rounded amount;
3. если transfer должен быть уменьшен, rollback;
4. повторить entry один раз с rounded cap.

Это медленнее только для редкого trim case, но удаляет отдельную reservation/trim model.

### 6.5 Source footprint при same-inventory move

Source footprint нельзя безусловно освобождать при partial transfer:

- partial attempt сохраняет source placement занятым;
- candidate source исключает source placement;
- full relocation может выполнить отдельную попытку с временно удаленным source placement;
- такая попытка обязана перенести весь entry;
- при remainder выполняется полный rollback.

Так target placements не смогут занять source cells, после чего remainder уже нельзя было бы
вернуть обратно.

---

## 7. Swap

### 7.1 Single-entry swap

Swap является специальным entry path внутри transfer service, а не strategy и не planned
operation.

Условия v1:

- `DragContext` содержит ровно один entry;
- задан concrete occupied target placement;
- переносится весь source placement/stack;
- обе стороны поддерживают snapshot rollback;
- forward и reverse conversion успешны;
- rules проверены в обе стороны;
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

### 7.2 Batch drop с `Swap`

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

### 7.3 Future group exchange

Кейс `9 x 1x1` из grid `3x3` против одного `3x3` placement полезен, но является не batch swap,
а отдельным group exchange с собственной all-or-nothing boundary:

- освобождаются два набора placements;
- большой placement размещается в области source group;
- source group упаковывается в область target placement;
- операция commit/rollback целиком.

Такой `GroupExchange` требует отдельного контракта областей и packing semantics. Он оставляется
на будущее и не удерживает planner/resolver infrastructure в v1.

---

## 8. Occupied handlers и dynamic slots

### 8.1 Occupied handler

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

### 8.2 Dynamic slots

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

### 8.3 Relocation

`SlotRelocationService` и implicit repacking удаляются.

Transfer использует deterministic greedy candidates. Он не перемещает посторонние stacks, чтобы
освободить место. Если relocation потребуется в будущем, это отдельная пользовательская
операция, а не скрытый retry.

---

## 9. Acceptance и preview

Acceptance не строит `TransferPlan`.

Общий read-only resolver предоставляет:

```text
TransferProbe
    CanAttempt
    FailureReason
    PrimaryCandidate
    SuggestedAmount
    Orientation
    CoveredSlots
```

`Probe` использует те же:

- target-side conversion;
- strategy candidate source;
- orderer;
- rules;
- topology validation.

`SuggestedAmount` вычисляется одним проходом по ordered candidates без mutation:
`accumulate min(remaining, candidate.Capacity)`. Известное ограничение: для one-per-ID при
desired > maxStack альтернативные пустые слоты дают оптимистичную оценку — execution перенесет
первую допустимую часть, remainder вернется в source. Это укладывается в advisory-контракт
probe и не требует метаданных на кандидате.

Ограничения:

- probe не резервирует state;
- execution всегда перевалидирует;
- exact batch packing не обещается;
- batch preview показывает только общий target и возможность попытки;
- `SuggestedAmount` является оценкой по текущему state;
- execution report является единственным authoritative result.

Single-entry preview может точно показать первый create/merge target и footprint. Для
multi-placement stack подсветка всех будущих targets не требуется.

`InventoryDropArea` не делает отдельный `CanAcceptItem -> CanAcceptDrop` chain.
`DropPreviewController` только отображает `TransferProbe`.
На release inventory pipeline сразу вызывает `ExecuteAsync`; предварительный `CanAcceptDrop`
не является execution guard.

---

## 10. Что удаляется

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
- `IPlanRule` и plan-rule contexts;
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

## 11. Что остается отдельным

1. UI и rendering могут проверять форму и orientation.
2. Rules могут запрещать конкретные shapes/items.
3. Single-cell fast path допустим внутри topology/storage при той же semantics.
4. Optimal packing и backtracking вне scope.
5. Group exchange вне scope.
6. Sorting/repacking inventory является отдельной action.
7. Hydration/import не проходит через transfer pipeline.
8. Транзакционный all-or-nothing batch не предоставляется и дешево не возвращается:
   events/DataBinding фиксируются после каждого entry. Atomic-подобный гейт реализуется
   request-level rule (`CanDropContext`) с консервативной симуляцией размещения; поскольку
   post-mutation veto-точек нет, после прохождения гейта исполнение фейлится только в
   исключительных случаях.

---

## 12. Этапы реализации

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
- Зафиксировать swap geometry и bidirectional rules.
- Зафиксировать current double preview/execution calls.
- Зафиксировать текущие точки `ITransferDomainHandler`/`IAsyncTransferDomainHandler` и их
  veto-семантику — мигрируют на request-level и entry-level veto (4.2/4.3).
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
- Переделать `SlotSelectionPolicyBase` и `IAlternativePlacementStrategy` implementations в
  orderers.
- Ввести `IPlacementGeometry`.
- Перенести rules из strategy candidate enumeration в transfer resolver.
- Contract tests: eligibility не меняется от orderer; built-in порядок сохраняется.

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
- Добавить `CanDropContext` в существующий rule lifecycle и batch-visible inventory/global rule
  tests.
- Target hint использует только первый entry; остальные идут как area drop.
- После successful entry DataBinding/events commit-ятся до следующего entry.
- Batch + `Swap` отклонять до mutation.
- Mixed single-cell/shaped batch работает по реальному state.
- Report различает failed entry и partial amount.

### Этап 6. Acceptance/UI

- Ввести `TransferProbe`.
- `CanAcceptItem`, `GetAcceptableCount`, hover и preview перевести на общий candidate resolver.
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
  - inventory/global rule;
  - dynamic slot lifecycle.
- Документировать ограничения batch swap и advisory preview.

Каждый этап должен компилироваться и проходить свой test subset.

---

## 13. Риски и обязательные проверки

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
- swap валидирует обе стороны и resulting footprint overlap.

### Batch

- entries выполняются в стабильном порядке;
- следующий entry видит mutation предыдущего;
- следующий entry видит committed DataBinding/external state предыдущего;
- failed entry не откатывает успешные предыдущие;
- failed entry не блокирует последующие;
- aggregate request rule получает полный `DragContext`;
- context-rule failure отклоняет batch до первой mutation;
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

## 14. Тестовая матрица

- slot/grid topology;
- single-cell/multi-cell;
- stackable/separable/unique;
- explicit target/area drop/auto-transfer;
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
  - `CanDropContext` failure не оставляет mutations;
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
- rules:
  - concrete target rejected;
  - aggregate batch request rejected;
  - rules do not mutate;
- preview:
  - same first candidate/orderer as execution;
  - advisory amount can become stale without data loss;
  - one-per-ID, desired > maxStack: probe оптимистичен, execution переносит первую допустимую
    часть, remainder возвращается (advisory-контракт);
- async veto:
  - request-level отклоняет до любых mutations;
  - entry-level отклоняет конкретный entry, предыдущие committed entries сохраняются;
  - состояние, изменившееся во время await, видится candidate-resolution после veto;
- events/DataBinding:
  - no events on rollback;
  - balanced remove/add per outcome;
  - entry 10 -> 6+4: binding получает суммарно ровно 10 адаптеров, без повторного счета;
  - correct placement snapshots.

---

## 15. Критерии готовности

- Один topology-aware transfer service для slot и grid.
- `TransferPlan`, virtual planning state и planner/executor split отсутствуют.
- Batch всегда sequential best-effort; `Atomic` отсутствует в API и Inspector.
- Каждый entry rollback-safe.
- Strategy read-only и topology-neutral.
- Strategy задает eligibility/capacity/base order.
- `PlacementCandidateOrderer` является единственной pluggable ролью порядка.
- Rules проверяются transfer service и не дублируются strategy.
- Existing rule lifecycle содержит однократный `CanDropContext`; отдельной plan-rule системы нет.
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
`InventoryAcceptanceRequest`, `InventoryStrategyBase`, `IPlacementStrategy`,
`IAcceptanceStrategy`, `IInventoryQueryStrategy`, `SlotSelectionPolicy`,
`Core/Drop/*AlternativePlacementStrategy`, `BlockedTargetResolverBase`, `ISwapStrategy`,
`DynamicSlotDecorator`, `SlotRelocationService`, `TargetPlacementOperation`,
`InventoryDropArea`, `DropPreviewController`, `AutoTransferService`, DataBinding и tests.*
