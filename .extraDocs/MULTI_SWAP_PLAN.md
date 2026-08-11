---
Last Updated: 2026-08-11
---

# Multi Swap Plan

Поддержка свопа «один входящий предмет вытесняет несколько» (например, 3×1 переносят на три 1×1) в существующем swap-пути, без параллельной ветки кода и без изменения поведения по умолчанию.

## Текущее состояние

Своп строго 1↔1. `TryExecuteSwap` (`Scripts/Inventories/InventoryTransferEngine.cs:1198`) берёт ровно одно размещение под слотом-целью:

```csharp
var sourcePlacement = sourcePlacementInventory.GetPlacementAt(sourceSlot);
var targetPlacement = targetPlacementInventory.GetPlacementAt(targetSlot);
```

Про остальные клетки, которые накроет footprint входящего предмета, движок ничего не знает.

Что происходит при 3×1 на три 1×1:

1. берётся только тот 1×1, который лежит под слотом-целью;
2. освобождается его клетка, два соседних 1×1 остаются на месте;
3. `TryPlace` для 3×1 падает — footprint упирается в оставшиеся два предмета;
4. `RestoreSwapSnapshots` откатывает всё, запись падает с `"Swap: cannot place source item in target"`.

Фолбэка на `FindAlternative` нет: ветка `Swap` в `InventoryTransferEngine.cs:446` и `:511` терминальная.

Дополнительно probe в swap-ветке (`InventoryTransferEngine.cs:193-226`) возвращает `coveredSlots: new[] { entryTargetSlot }` — подсвечивается одна клетка, а не весь след предмета.

## Ведущее решение: зеркальное смещение

Якорь входящего предмета в цели — `A_t`, якорь источника — `A_s`. Вытесненное размещение с якорем `P` едет на `A_s + (P − A_t)` в клеточных координатах, со своей формой и ориентацией.

Почему так:

- **Это обобщение текущего поведения, а не замена.** Сейчас при 1↔1 входящий садится на `targetPlacement.AnchorIndex`, а вытесненный — на `sourcePlacement.AnchorIndex`, то есть смещение 0. Если для случая «вытеснен ровно один» определить `A_t = P`, формула даёт ровно текущий результат.
- **Геометрия предсказуема для игрока.** Три 1×1 переезжают в освобождённый след 3×1 в том же взаимном расположении.
- **Probe остаётся дешёвым.** Нет перебора кандидатов на каждый кадр ховера.

Альтернатива — «каждый вытесненный ищет любой свободный слот в источнике через orderer» — даёт больше успешных свопов, но телепортирует предметы в непредсказуемые места и стоит N прогонов кандидатного цикла на ховер. Она добавляется вторым режимом, а не поведением по умолчанию.

```csharp
public enum MultiSwapMode : byte
{
    Single = 0,            // текущее поведение: >1 вытесненного = отказ
    MirrorOffset = 1,      // зеркальное смещение в освобождённый след
    SourceInventoryFit = 2 // fallback на кандидатный цикл в источнике
}
```

`Single` по умолчанию — изменение не трогает существующие проекты.

## Правило разрешения якоря входящего предмета

| Вытеснено | `A_t` |
|---|---|
| ровно одно размещение | `displaced[0].AnchorIndex` — сохраняется текущий снап к вытесняемому предмету |
| больше одного | `geometry.TryResolveAnchor(targetSlot, request, out anchor)` — тот же grab-offset путь, что использует `TryCreatePlacementCandidate` |

Разделение нужно, чтобы 1↔1 своп не поменял поведение: сейчас входящий предмет снапится к якорю вытесняемого, а не к grab-offset позиции.

`TryResolveAnchor` требует `InventoryAcceptanceRequest`, которого на swap-пути сейчас нет — его нужно собрать так же, как это делает `CreateAcceptanceRequest` для обычного пути (`InventoryTransferEngine.cs:496`).

## Этап 1. Расширить `CanPlace` до множества игнорируемых

Сейчас потолок — два игнора: `Scripts/Inventories/IPlacementInventory.cs:25-28`, реализация в `Scripts/Inventories/PlacementStore.cs:38`:

```csharp
if (_cellToPlacement.TryGetValue(coveredIndices[i], out var existing) &&
    !ReferenceEquals(existing, ignoredA) &&
    !ReferenceEquals(existing, ignoredB))
    return false;
```

При N вытесненных нужно игнорировать весь набор — иначе вытесняемые предметы блокируют друг друга при проверке.

- `PlacementStore.CanPlace(PlacementRequest, IReadOnlyCollection<Placement> ignored)` — заменить два `ReferenceEquals` на проверку принадлежности набору. Старые перегрузки оставить обёртками: они часть публичного API.
- Такую же перегрузку добавить в `IPlacementInventory`, `UniversalInventory` (`:520`), `IPlacementGeometry` и `InventoryPlacementGeometry` (`:60`).
- Набор передавать как `HashSet<Placement>`. При 1-2 элементах аллокация заметна на ховере, поэтому буфер переиспользуется в `InventoryTransferEngine`, а не создаётся на каждый вызов.

## Этап 2. Сбор вытесняемого набора

Новый метод в `InventoryTransferEngine`, в секции `// ──── Swap ───`:

```csharp
private static bool TryCollectDisplacedPlacements(
    InventoryPlacementGeometry geometry,
    BaseSlot anchorSlot,
    IPlacementShape shape,
    int orientation,
    Placement ignoredSourcePlacement,
    out List<Placement> displaced)
```

Логика:

1. `geometry.GetCoveredSlots(anchor, shape, orientation)`;
2. по каждому слоту `geometry.GetPlacementAt(slot)`;
3. дедупликация через `HashSet<Placement>` — одно размещение накрывает несколько клеток;
4. пропуск `ignoredSourcePlacement` для перемещения внутри одного инвентаря;
5. `false`, если хотя бы одна клетка footprint вне границ — это уже не своп, а отказ.

## Этап 3. Переписать `TryExecuteSwap`

`InventoryTransferEngine.cs:1198` работает со списком вместо одного `targetPlacement`. Порядок операций сохраняется, меняется кратность.

1. Ранние проверки как есть (snapshot-capable, placement-capable, вся стопка источника).
2. `TryCollectDisplacedPlacements`. Если `count > 1` и режим `Single` → `Failed("Swap: target footprint covers multiple items")`.
3. **Правила.** `ValidateSwapCounterpart` (`:1378`) прогоняется по каждому вытесненному отдельно — каждый едет в источник и должен пройти его правила. Первый отказ валит весь своп с указанием, какой предмет отказан.
4. **Конверсия.** Forward — один вызов как сейчас. Reverse — цикл `TryConvertStackToTargetDomain` по каждому вытесненному стеку. Сессия конверсии (`request.Context.ConversionSession`) одна на всех.
5. **Домен.** `forwardDomain` один, `reverseDomain` — по одному на вытесненный.
6. **Мутация.** Снапшоты обоих инвентарей → `RemovePlacement` для источника и всех вытесненных → `TryPlace` входящего на `A_t` → `TryPlace` каждого вытесненного на `A_s + (P − A_t)` через `topology.TryToIndex`. Любой сбой → `RestoreSwapSnapshots` (`:1412`) и отказ. Атомарность уже обеспечена снапшотами, новой машинерии не нужно.
7. **Результат.** `EntryTransferResult.Committed` сейчас возвращает один `PlacementTransferOutcome`; нужен массив — outcome входящего плюс по одному на каждый вытесненный.

### Уступка в `CounterpartContext`

`TransferDomainContext.CounterpartContext` — двусторонняя ссылка, построенная на предположении «своп это пара». При N вытесненных она определяется как «forward ↔ первый reverse», остальным reverse проставляется `CounterpartContext = forwardDomain` односторонне.

Обработчики, которые идут от reverse к counterpart, продолжат работать. Те, кто идёт от forward к counterpart, увидят только первый вытесненный. Это надо зафиксировать в xml-doc.

## Этап 4. События и контекст

`Scripts/Core/Models/InventoryEvents.cs:76` — `InventorySwapContext` несёт ровно одну пару стеков, и на нём завязан `CanSwap` в биндинге (`Scripts/DataBinding/InventoryDataBindingBase.cs:114`).

Не ломая API, добавить:

```csharp
public IReadOnlyList<ItemStack> DisplacedStacks { get; }
public IReadOnlyList<BaseSlot> DisplacedSlots { get; }
```

`TargetStack` / `TargetBaseSlot` продолжают указывать на предмет под курсором — первый вытесненный. Существующие подписчики `CanSwap` работают дальше, но видят только его; это надо явно написать в xml-doc, иначе биндинг, разрешающий своп по `TargetStack`, молча пропустит остальные. Кому нужна строгость — читает `DisplacedStacks`.

`OnSwapAttempting` / `OnSwapCompleted` вызываются один раз с полным контекстом, не N раз.

`DispatchSwapEvents` (`:1429`) — цикл `EmitItemRemoved` / `EmitItemAdded` по вытесненным вместо одиночной пары.

## Этап 5. Probe и превью

Swap-ветка probe (`InventoryTransferEngine.cs:193-226`) возвращает `coveredSlots: new[] { entryTargetSlot }`. Заменить на реальную проекцию `geometry.GetCoveredSlots(A_t, shape, orientation)` и прогнать тот же `TryCollectDisplacedPlacements` плюс проверку правил по всем вытесненным — иначе превью пообещает своп, который упадёт на релизе. Ровно эту рассинхронизацию probe и существует, чтобы предотвращать.

В `TransferProbe` (`Scripts/Inventories/InventoryTransferService.cs:12`) добавить `IReadOnlyList<Placement> DisplacedPlacements`.

Отдельной фазой, опционально: `DropVerdict` сейчас двоичный — `CanPlace` плюс причина. Чтобы подсветить вытесняемые предметы третьим цветом, нужен `DropVerdictKind { Accepted, Displaced, Rejected }` и правка `CrossFeedbackSlot`. Без этого фича работает, но игрок не видит, что именно он вытесняет — при мульти-свопе это важнее, чем при 1↔1.

## Этап 6. Настройка и точки входа

- `MultiSwapMode` в `DropPolicySettings` (с `ShowIf` на `Swap`, как сделано для `FindAlternative`), в `DropRequestPolicy` как `MultiSwapMode?`, в `ResolvedDropPolicy` и в `Merge` (`Scripts/Core/Drop/DropPolicy.cs:72`).
- `DropRequestPolicy.WithSwap(MultiSwapMode mode = MultiSwapMode.Single)`.
- Batch + Swap остаётся отклонённым (`InventoryTransferEngine.cs:94`, `:296`): мульти-своп — это «один входящий вытесняет много», а не «много входящих».

## Этап 7. Тесты

Новый фикстур `Tests/Editor/Inventories/MultiSwapTests.cs` в стиле `SwapRuleValidationTests` (`InventoryBuilder` + `DragContextBuilder` + `ResolvedDropPolicy`).

| Кейс | Ожидание |
|---|---|
| 3×1 на три 1×1, `MirrorOffset` | своп, три 1×1 в следе 3×1 в исходном порядке |
| то же, режим `Single` | отказ, оба инвентаря нетронуты |
| 3×1 накрывает 1×1 и половину 2×2 | отказ: 2×2 не влезает в след 3×1 |
| один из вытесненных отклонён правилом источника | отказ целиком, полный откат |
| 1×1 на 3×1 | как раньше — регресс на сохранение снапа якоря |
| 3×1 ↔ 3×1 | как раньше |
| мульти-своп между разными инвентарями | конверсия отработала на каждом вытесненном |
| probe при мульти-свопе | `CoveredSlots` = весь footprint, `DisplacedPlacements` = 3 |
| `OnSwapCompleted` | вызван один раз, `DisplacedStacks.Count == 3` |

Регресс: существующие `SwapRuleValidationTests` и `ShapedItemPlacementTests` должны пройти без правок. Если правка понадобилась — поведение по умолчанию поехало.

Верификация по `.claude/skills/VERIFICATION.md`: сборка `UDND.Runtime`, `DragAndDropSystem.Tests.Editor`, `UDND.Examples`, затем EditMode-фикстуры `MultiSwapTests`, `SwapRuleValidationTests`, `ShapedItemPlacementTests`, `InventoryTransferServiceTests`, `TransferProbeTests`. Изменение затрагивает `IPlacementInventory` — по регламенту это повод прогнать всю сборку `DragAndDropSystem.Tests.Editor`.

## Этап 8. Документация

- `transfer-pipeline.{ru,en,es}.md` — таблица `BlockedTargetResolutionKind`, строка `Swap` сейчас говорит «одиночный swap»;
- `rules.{ru,en,es}.md` — что правила источника прогоняются по каждому вытесненному;
- `file-map.*.md` — если появятся новые файлы;
- скиллы `dragdrop-system/{SKILL,CORE_CONCEPTS,ADVANCED_FEATURES}.md` и `dragdrop-architecture/DATA_FLOW.md` — зеркально в `.claude/skills/` и `.agents/skills/`, они дублируются.

## Риски

**Стоимость probe.** Мульти-своп добавляет N прогонов `ValidateSwapCounterpart` на каждый кадр ховера, а каждый строит `DragContext` и `RuleEvaluationService`. При 4-5 вытесненных и пользовательских правилах это заметно. Нужен кеш результата probe по ключу (якорь + ориентация) в пределах ховера — сейчас такого кеша нет.

**Односторонний `CounterpartContext`** при N вытесненных — см. этап 3.

**Объём.** Этапы 1-3 — ядро, ориентировочно 250-350 строк с учётом xml-doc. Этапы 4-6 — по 50-80. Тесты — 300+. Целиком: полный рабочий день с прогонами.
