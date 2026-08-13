---
Last Updated: 2026-08-12
---

# Multi Swap MVP Plan

## Цель

Добавить opt-in своп «один входящий предмет вытесняет несколько размещений» внутри существующего
JIT swap-пути. Основной кейс: предмет `3×1` переносится на три предмета `1×1`, а три вытесненных
предмета занимают соответствующие позиции в области источника.

MVP не добавляет поиск свободных мест, новый planner, виртуальное состояние или отдельную ветку
transfer pipeline.

## Поведение

```csharp
/// <summary>
/// How many placements one incoming swap entry may displace.
/// Only meaningful where an item can cover more than one cell.
/// </summary>
public enum SwapDisplacementMode : byte
{
    SinglePlacement = 0,
    AllCoveredPlacements = 1
}

/// <summary>What a swap does when the incoming footprint covers an item only partly.</summary>
public enum PartialOverlapSwapMode : byte
{
    Reject = 0,
    WithDragOffset = 1,
    VacatedArea = 2
}
```

Оси независимы: своп, вытесняющий **один** предмет, упирается в вопрос частичного накрытия ровно
так же, как вытесняющий несколько, поэтому это отдельная настройка, а не значение первого enum'а.

- `SinglePlacement` сохраняет текущее поведение и остаётся значением по умолчанию.
- `AllCoveredPlacements` разрешает одному входящему placement вытеснить несколько placements.
- Batch drag + Swap остаётся запрещённым: MVP поддерживает один входящий `DragEntry`.
- Вытесненные предметы ищут место только в режиме `VacatedArea`.
- Любая неудача откатывает весь текущий swap через существующие snapshots.

Для входящего target anchor `A_t`, исходного source anchor `A_s` и вытесненного anchor `P`
назначение вытесненного предмета вычисляется как:

```text
A_reverse = A_s + (P - A_t)
```

Это параллельный перенос: взаимное расположение вытесненных предметов сохраняется — кроме случая
перекрытия внутри одного инвентаря, описанного ниже.

### Перекрытие внутри одного инвентаря

При same-inventory swap освобождаемая область источника может пересекаться со следом, который
занимает входящий предмет. Тогда `A_reverse` указывает на клетку, где уже стоит въехавший предмет,
и параллельного переноса не существует. Вместо отказа resolver сдвигает такое назначение на вектор
самого свопа `A_s - A_t`, повторяя сдвиг, пока назначение не выйдет из следа входящего предмета.

Сдвинутое назначение дополнительно обязано целиком лежать внутри исходного следа источника — то
есть внутри области, которая действительно освобождается. Иначе swap отклоняется.

Кросс-инвентарный swap этого шага не делает: входящий след находится в другом инвентаре и
пересечься с назначениями не может.

**Следствие, которое надо знать.** Сдвиг вычисляется для каждого вытесненного предмета
независимо, поэтому предметы, которым сдвиг понадобился, и предметы, которым он не понадобился,
смещаются на разную величину. Их взаимный порядок при этом не сохраняется.

Сетка `6×1`, предмет `3×1` на `{3,4,5}`, предметы `a`@1 и `b`@2, перенос `3×1` на якорь 1
(след `{1,2,3}`, вектор свопа `+2`):

- `a` по смещению попадает в клетку 3 — она под входящим следом, поэтому сдвиг: клетка **5**;
- `b` по смещению попадает в клетку 4 — она уже свободна, сдвига нет: клетка **4**.

`a` был левее `b`, стал правее. Это принятое поведение, а не дефект; тест
`ProcessDrop_MultiSwap_SameInventoryOverlap_ShiftsOnlyTheBlockedDisplacement` фиксирует его.

### Частичное накрытие

Предметы разной формы никогда не накрывают друг друга точно, поэтому своп, лишь задевший соседа, —
обычный случай, а не краевой. `PartialOverlapSwapMode` решает, что с ним делать.

`Reject` (по умолчанию) отклоняет своп, если вытесненный предмет накрыт не целиком — **но только
когда полное накрытие было достижимо**. Предмет, который меньше или другой формы и не смог бы
накрыть лежащего под ним ни при каком совмещении, меняется местами как обычно; иначе запрет убил бы
весь класс свопов «маленький предмет на большой». Достижимость проверяется по формам, а не по числу
клеток: у `3×1` столько же клеток, сколько у L-образной тройки, но вместить её он не может.

`WithDragOffset` разрешает частичное накрытие и ставит вытесненный предмет по смещению захвата.
Не влезло — отказ.

`VacatedArea` разрешает частичное накрытие и ищет позицию перебором якорей: след проецируется через
`GetCoveredCells` и проверяется поклеточно, ровно как при любом размещении. Ранжирование —
позиции целиком внутри освобождаемой области, затем опирающиеся на неё бо́льшим числом клеток, и
только потом близость к позиции по смещению захвата. Освобождаемая область — след перетаскиваемого
предмета плюс, при свопе внутри одного инвентаря, следы всех вытесненных, минус клетки под входящим
предметом.

Позиции, которые отклоняют правила слота, пропускаются, а не валят весь своп. Разрешение жадное и
попредметное, без бэктрекинга: предмет, обработанный раньше, забирает клетки у следующих.

Пример (`ProcessDrop_MultiSwap_PartialOverlapModeDecidesTheOutcome`): сетка `3×3`, `2×1` стоит на
`{0,3}`, `2×2` на `{1,2,4,5}`, тащим `2×2` на `{3,4,6,7}`. `2×1` задет одной клеткой из двух, а
позиция по смещению захвата уходит выше поля. `Reject` отказывает из-за неполного накрытия,
`WithDragOffset` — из-за недостижимой позиции, `VacatedArea` находит `{2,5}`.

## Ограничения MVP

- Геометрия вычисляется только через `IInventoryTopology`; grid-specific ветки запрещены.
- Если позиция по смещению захвата недостижима, а поиск не включён, swap отклоняется.
- Если reverse placements пересекаются с посторонними placements или друг с другом, swap отклоняется.
  Пересечение с самим входящим следом — единственный случай, который разбирается сдвигом, а не отказом.
- Поиск не делает бэктрекинга.
- Каждый вытесненный предмет сохраняет свою shape и orientation, нормализованную топологией источника.
- Кеш probe, третий цвет подсветки и candidate fallback не входят в MVP.

## Этап 1. Policy

Добавить `SwapDisplacementMode` и `PartialOverlapSwapMode` в:

- `DropPolicySettings`;
- `DropRequestPolicy` как nullable override;
- `ResolvedDropPolicy`;
- `DropRequestPolicy.Merge`;
- `DropRequestPolicy.WithSwap(SwapDisplacementMode mode, PartialOverlapSwapMode partialOverlap)`.

Поле в inspector показывается только при `BlockedTargetResolutionKind.Swap`.

## Этап 2. Общий read-only resolver

В swap-секции `InventoryTransferService` добавить внутренний resolver, который вызывается и из
`Probe`, и из `TryExecuteSwap`. Он ничего не мутирует и возвращает либо готовое описание операции,
либо причину отказа.

Минимальные внутренние модели:

```csharp
private sealed class ResolvedSwap
{
    public Placement SourcePlacement;
    public BaseSlot ForwardAnchor;
    public ItemStack ForwardStack;
    public IPlacementShape ForwardShape;
    public int ForwardOrientation;
    public List<ResolvedSwapDisplacement> Displacements;
}

private sealed class ResolvedSwapDisplacement
{
    public Placement Placement;
    public BaseSlot DestinationAnchor;
    public ItemStack ConvertedStack;
    public TransferDomainContext DomainContext;
}
```

Resolver выполняет шаги строго в таком порядке:

1. Проверяет существующие swap-инварианты: один полный entry, непустой source,
   snapshot-capable и placement-capable inventories.
2. Через `TransferConversionSession` получает target-domain adapter входящего предмета.
3. По target-domain adapter определяет forward shape; orientation нормализует target topology.
4. Определяет target anchor через тот же `InventoryAcceptanceRequest` и `TryResolveAnchor`, что и
   обычный shaped explicit drop — всегда, независимо от режима.

   Якорь отвечает только на вопросы «в какие слоты положить», «с каким поворотом» и «что отправить
   в данные». Решение о свопе он не принимает: что именно вытесняется, читается с клеток, которые
   этот якорь фактически накрывает. Поэтому никакого снапа на якорь вытесняемого предмета нет —
   иначе точно позиционированный shaped-предмет молча переезжал бы в чужую позицию.
5. Получает все covered target slots и собирает уникальные placements в порядке обхода footprint.
   `HashSet` используется только для дедупликации, не как источник порядка.
6. Placement непосредственно под `TargetBaseSlot` сохраняется как primary displaced placement для
   обратной совместимости `TargetStack`/`TargetBaseSlot`. Если под курсором пусто, или там лежит
   сам перетаскиваемый предмет, или его размещение не попало в displaced set — primary становится
   первый вытесненный в порядке обхода следа.

   Клетка под курсором больше ни на что не влияет. Ни её пустота, ни отсутствие в ней размещения
   не отменяют своп: у многоклеточного предмета курсорная клетка зависит от того, за какую ячейку
   его взяли, и раньше один и тот же дроп проходил или падал в зависимости только от этого.
7. При `SinglePlacement` и количестве displaced placements больше одного возвращает отказ.
   При `Reject` возвращает отказ, если вытесненный предмет накрыт не целиком, хотя форма входящего
   могла бы его вместить.
8. Для каждого displaced placement вычисляет фактический destination anchor по формуле
   `A_s + (P - A_t)` через topology source inventory.
9. Проверяет всю итоговую геометрию без мутаций:
   - forward footprint может игнорировать source placement при same-inventory swap и весь displaced set;
   - reverse footprints могут игнорировать source placement и displaced set;
   - reverse footprints не пересекаются друг с другом;
   - остальные placements продолжают блокировать размещение.
10. Конвертирует каждый displaced stack в source domain через ту же conversion session.
11. Проверяет правила по **всем клеткам, которые предмет займёт**, а не по клетке под курсором и не
    по одному якорю:
    - входящий предмет — по каждому слоту своего разрешённого следа;
    - каждый вытесненный — по каждому слоту следа в своём реальном месте назначения.

    Клетка, запрещающая предмет, запрещает его и тогда, когда предмет приходит на неё хвостом,
    поэтому от выбора якоря результат не зависит. То же правило действует и в обычном размещении
    (`PassesRulesOnFootprint`), чтобы своп не расходился с остальной системой.
12. Создаёт forward domain context и по одному reverse domain context на displacement; каждый
    context содержит фактический planned target slot.

Проверку occupancy выполнить внутренним swap-helper через topology, covered slots и
`GetPlacementAt`. Для MVP не расширять `IPlacementInventory`, `IPlacementGeometry` и
`PlacementStore` публичной перегрузкой с коллекцией ignored placements: проверка «reverse footprints
не пересекаются друг с другом» требует инкрементального учёта уже запланированных позиций и через
`CanPlace(request, ignored)` не выражается, поэтому перегрузка почти ничего не давала бы.
Bounds и covered cells resolver получает через существующий topology-aware placement API; helper
добавляет только проверку текущей и уже зарезервированной occupancy.

## Этап 3. Probe и выполнение

### Probe

Swap-ветка `Probe` вызывает общий resolver.

- При успехе возвращает `TransferProbe.Accepted` с окончательным forward anchor и полным
  `CoveredSlots` входящего предмета.
- При отказе возвращает ту же причину, которую получил бы execution.
- `TransferProbe.DisplacedPlacements` и новый `DropVerdictKind` в MVP не добавляются. UI использует
  только полный forward footprint и итоговый verdict; внутренние placement-ссылки наружу не выходят.

### Execution

`TryExecuteSwap` повторно вызывает resolver против актуального состояния, затем:

1. Вызывает `SwapAttempting` один раз.
2. Снимает snapshots обоих inventories; для same-inventory — один snapshot.
3. Удаляет source placement и все displaced placements.
4. Размещает forward stack в разрешённом target anchor.
5. Размещает каждый converted displaced stack в его `DestinationAnchor`.
6. При любой ошибке восстанавливает snapshots и не испускает success events.
7. После полного успеха consume-ит committed conversion entries, вызывает domain success hooks,
   публикует inventory events и один `SwapCompleted`.

`EntryTransferResult.Outcomes` продолжает содержать только forward swap outcome. Reverse
перемещения не добавляются туда, иначе `DropResult` и анимации начнут считать последним target
слот из исходного inventory.

## Этап 4. События и domain contexts

В `InventorySwapContext` добавить без удаления старых свойств:

```csharp
public IReadOnlyList<ItemStack> DisplacedStacks { get; }
public IReadOnlyList<BaseSlot> DisplacedSourceSlots { get; }
public IReadOnlyList<BaseSlot> DisplacedDestinationSlots { get; }
```

- `TargetStack` и `TargetBaseSlot` относятся к placement непосредственно под курсором. Исключение —
  same-inventory swap, где под курсором лежит сам перетаскиваемый предмет: тогда они указывают на
  первый вытесненный placement, потому что «встречный предмет» в этом сценарии не тот, что под
  курсором.
- Списки имеют детерминированный порядок footprint traversal, primary placement идёт первым.
- `OnSwapAttempting` и `OnSwapCompleted` вызываются один раз на весь multi-swap.
- `DispatchSwapEvents` испускает remove/add для каждого вытесненного placement после общего commit.

В `TransferDomainContext` добавить read-only список counterpart contexts. Старое
`CounterpartContext` оставить legacy-проекцией на primary counterpart:

```csharp
public IReadOnlyList<TransferDomainContext> CounterpartContexts { get; internal set; }
```

- forward context видит все reverse contexts;
- каждый reverse context видит только forward context;
- при обычном 1↔1 список содержит один элемент и старое поведение сохраняется.

## Этап 5. Тесты и документация

Обязательные тесты `MultiSwapTests`:

| Кейс | Ожидание |
|---|---|
| `3×1` на три `1×1`, `AllCoveredPlacements` | полный swap, порядок `1×1` сохранён |
| тот же кейс, `SinglePlacement` | отказ без мутаций |
| reverse destination вне bounds или занят | полный отказ и rollback (`ProcessDrop_MultiSwap_ReverseDestinationOutOfBounds_RejectsWithoutMutation`) |
| same-inventory перекрытие с разным сдвигом | сдвигается только заблокированное назначение; порядок не сохраняется (`ProcessDrop_MultiSwap_SameInventoryOverlap_ShiftsOnlyTheBlockedDisplacement`) |
| один reverse destination отклонён slot rule | полный отказ; правило получает фактический слот |
| target-domain converter меняет forward shape | displaced set рассчитан по converted shape (`MultiSwap_ResolvesDisplacedSetFromTheTargetDomainFootprint` в `TransferConversionTests`) |
| cross-inventory swap | каждый displaced stack конвертирован в source domain |
| same-inventory с пересекающимися областями | корректная проверка ignored set и rollback |
| курсор на неякорной клетке shaped placement | primary target и legacy `TargetStack` стабильны |
| probe | полный forward footprint и тот же verdict, что execution |
| события | один `SwapCompleted`, детерминированные displaced lists |
| результат | `EntryTransferResult` содержит только forward outcome |

Регрессии:

- `SwapRuleValidationTests`;
- `ShapedItemPlacementTests`;
- `InventoryTransferServiceTests`;
- `TransferProbeTests`;
- полная сборка `DragAndDropSystem.Tests.Editor`.

После реализации обновить `transfer-pipeline`, `rules`, file map и архитектурные skills на всех
поддерживаемых языках. Skills лежат в двух зеркальных деревьях — `.claude/skills/` и
`.agents/skills/`; правки нужны в обоих, иначе один из наборов начнёт описывать старое поведение.

Производительность probe сначала измерить; кеш добавлять только по результатам профилирования.

## Не входит в MVP

- поиск альтернативных слотов для вытесненных предметов;
- `SourceInventoryFit`;
- отдельный multi-swap planner;
- batch из нескольких входящих entries;
- кеш probe;
- отдельный визуальный статус displaced slots;
- новые публичные collection-based overloads placement API.
