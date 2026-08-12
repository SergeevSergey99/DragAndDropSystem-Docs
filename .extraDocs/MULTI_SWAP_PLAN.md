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
public enum MultiSwapMode : byte
{
    Single = 0,
    PreserveOffsets = 1
}
```

- `Single` сохраняет текущее поведение и остаётся значением по умолчанию.
- `PreserveOffsets` разрешает одному входящему placement вытеснить несколько placements.
- Batch drag + Swap остаётся запрещённым: MVP поддерживает один входящий `DragEntry`.
- Вытесненные предметы не ищут альтернативные места.
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

- `a` зеркалится в клетку 3 — она под входящим следом, поэтому сдвиг: клетка **5**;
- `b` зеркалится в клетку 4 — она уже свободна, сдвига нет: клетка **4**.

`a` был левее `b`, стал правее. Это принятое поведение, а не дефект; тест
`ProcessDrop_MultiSwap_SameInventoryOverlap_ShiftsOnlyTheBlockedDisplacement` фиксирует его.

## Ограничения MVP

- Геометрия вычисляется только через `IInventoryTopology`; grid-specific ветки запрещены.
- Если destination cell отсутствует в топологии источника, swap отклоняется.
- Если reverse placements пересекаются с посторонними placements или друг с другом, swap отклоняется.
  Пересечение с самим входящим следом — единственный случай, который разбирается сдвигом, а не отказом.
- Каждый вытесненный предмет сохраняет свою shape и orientation, нормализованную топологией источника.
- Кеш probe, третий цвет подсветки и candidate fallback не входят в MVP.

## Этап 1. Policy

Добавить `MultiSwapMode` в:

- `DropPolicySettings`;
- `DropRequestPolicy` как nullable override;
- `ResolvedDropPolicy`;
- `DropRequestPolicy.Merge`;
- `DropRequestPolicy.WithSwap(MultiSwapMode mode = MultiSwapMode.Single)`.

Поле в inspector показывается только при `BlockedTargetResolutionKind.Swap`.

## Этап 2. Общий read-only resolver

В swap-секции `InventoryTransferService` добавить внутренний resolver, который вызывается и из
`Probe`, и из `TryExecuteSwap`. Он ничего не мутирует и возвращает либо готовое описание операции,
либо причину отказа.

Минимальные внутренние модели:

```csharp
private sealed class ResolvedMultiSwap
{
    public Placement SourcePlacement;
    public BaseSlot ForwardAnchor;
    public ItemStack ForwardStack;
    public IPlacementShape ForwardShape;
    public int ForwardOrientation;
    public List<ResolvedDisplacement> Displacements;
}

private sealed class ResolvedDisplacement
{
    public Placement Placement;
    public BaseSlot DestinationAnchor;
    public ItemStack ConvertedStack;
    public TransferDomainContext DomainContext;
}
```

Resolver выполняет шаги строго в таком порядке:

1. Проверяет существующие swap-инварианты: один полный entry, непустые source/target,
   snapshot-capable и placement-capable inventories.
2. Через `TransferConversionSession` получает target-domain adapter входящего предмета.
3. По target-domain adapter определяет forward shape; orientation нормализует target topology.
4. Определяет target anchor по режиму:
   - `Single` всегда использует anchor primary placement под курсором — ровно как текущий swap;
   - `PreserveOffsets` разрешает hover anchor через тот же `InventoryAcceptanceRequest` и
     `TryResolveAnchor`, что обычный shaped explicit drop;
   - если в `PreserveOffsets` hover footprint содержит только primary placement, resolver пробует
     legacy anchor. Он принимается только когда повторная проекция даёт тот же displaced set;
     иначе сохраняются hover anchor и исходный набор. Правило терминально, без новых итераций.
5. Получает все covered target slots и собирает уникальные placements в порядке обхода footprint.
   `HashSet` используется только для дедупликации, не как источник порядка.
6. Placement непосредственно под `TargetBaseSlot` сохраняется как primary displaced placement для
   обратной совместимости `TargetStack`/`TargetBaseSlot`.
7. При `Single` и количестве displaced placements больше одного возвращает отказ.
8. Для каждого displaced placement вычисляет фактический destination anchor по формуле
   `A_s + (P - A_t)` через topology source inventory.
9. Проверяет всю итоговую геометрию без мутаций:
   - forward footprint может игнорировать source placement при same-inventory swap и весь displaced set;
   - reverse footprints могут игнорировать source placement и displaced set;
   - reverse footprints не пересекаются друг с другом;
   - остальные placements продолжают блокировать размещение.
10. Конвертирует каждый displaced stack в source domain через ту же conversion session.
11. Для каждого displaced item проверяет start/drop rules против его реального
    `DestinationAnchor`, а не общего source slot.
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
| `3×1` на три `1×1`, `PreserveOffsets` | полный swap, порядок `1×1` сохранён |
| тот же кейс, `Single` | отказ без мутаций |
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
