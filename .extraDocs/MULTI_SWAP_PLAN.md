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

Это параллельный перенос: взаимное расположение вытесненных предметов сохраняется.

## Ограничения MVP

- Геометрия вычисляется только через `IInventoryTopology`; grid-specific ветки запрещены.
- Если destination cell отсутствует в топологии источника, swap отклоняется.
- Если reverse placements пересекаются с посторонними placements или друг с другом, swap отклоняется.
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
4. Сначала разрешает hover anchor через тот же `InventoryAcceptanceRequest` и `TryResolveAnchor`,
   что обычный shaped explicit drop, и собирает его displaced set. Если набор содержит только
   primary placement под курсором, переключается на anchor этого placement и собирает набор заново —
   это сохраняет legacy 1↔1 snap. При нескольких placements остаётся hover anchor.
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
`PlacementStore` публичной перегрузкой с коллекцией ignored placements.

## Этап 3. Probe и выполнение

### Probe

Swap-ветка `Probe` вызывает общий resolver.

- При успехе возвращает `TransferProbe.Accepted` с окончательным forward anchor и полным
  `CoveredSlots` входящего предмета.
- При отказе возвращает ту же причину, которую получил бы execution.
- `TransferProbe.DisplacedPlacements` и новый `DropVerdictKind` в MVP не добавляются.

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

- `TargetStack` и `TargetBaseSlot` всегда относятся к placement непосредственно под курсором.
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
| reverse destination вне bounds или занят | полный отказ и rollback |
| один reverse destination отклонён slot rule | полный отказ; правило получает фактический слот |
| target-domain converter меняет forward shape | displaced set рассчитан по converted shape |
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
поддерживаемых языках. Производительность probe сначала измерить; кеш добавлять только по
результатам профилирования.

## Не входит в MVP

- поиск альтернативных слотов для вытесненных предметов;
- `SourceInventoryFit`;
- отдельный multi-swap planner;
- batch из нескольких входящих entries;
- кеш probe;
- отдельный визуальный статус displaced slots;
- новые публичные collection-based overloads placement API.
