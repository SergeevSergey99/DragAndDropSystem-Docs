# Unified Placement Planning — Design Plan

**Цель:** перестать различать single-cell и shaped в логике (планировщик, стратегии, acceptance).
Single-cell — это вырожденный случай **footprint из 1 ячейки**. Везде должна работать одна
placement-машинерия; доступность слотов проверяется по топологии через `CanPlace`, а не по
`slot.IsEmpty`.

> Статус: **план, код не меняется.** Реализация — отдельными этапами после ревью этого документа.

---

## 1. Почему это вообще возможно

Хранилище уже унифицировано:
- `PlacementStore` — единый источник правды для **всех** стеков (и 1×1, и shaped).
  `BaseSlot.Stack` → `IInventory.TryGetStackForSlot` → `PlacementStore.GetAt(index).Stack`
  (`UniversalInventory.cs:660-678`, `BaseSlot.cs:23-31`).
- Footprint принадлежит топологии: `IInventoryTopology.GetPlacementOffsets(shape, orientation)`.
  `SlotTopology` всегда отдаёт anchor-only → multi-cell автоматически схлопывается в 1 ячейку
  (`InventoryTopology.cs`).
- Swap уже placement-based и топология-агностичен (`TransferPlanExecutor.TryCommitSwapViaPlacement`,
  `TransferPlanner.TryPlanSwapAgainstTarget`).

То есть «single-cell как footprint=1» — это уже истина на уровне storage. Осталось довести до неё
**планировщик и стратегии**, которые пока форкаются.

---

## 2. Где сейчас живёт расхождение

### 2.1 Планировщик — два параллельных пути
- **Single-cell:** `PlanEntry` → `AllocateForStrategyInventory` / `AllocateForUniqueInventory`
  (`TransferPlanner.cs:866`, `:939`) → `TryAllocateIntoSlot`. Тип результата — `PlannedSlotAllocation`
  (per-slot, без footprint).
- **Shaped:** `TryPlanShapedPlacement` (`TransferPlanner.cs:523`) — резолв якоря, merge/`CanPlace`,
  свап. Тип результата — `PlannedPlacementAllocation` (anchor + shape + orientation).
- **Разводящие guard'ы:** `TransferPlanner.cs:368` (batch), `:537` (вход shaped-пути),
  `:726` (`TryPlanOccupiedSlotHandler`).

### 2.2 Стратегии/acceptance — доступность по `slot.IsEmpty`
- `InventoryStrategyBase` (`:63, :79, :128, :190, :200`), `SeparableStacksStrategy`,
  `CanUseAlternativeSlot`, `GetAcceptableCount`, `GetSlotCandidates`.
- Для 1 клетки корректно, но не footprint/топология-aware.
- `CanAcceptShape` (`UniversalInventory.cs:1422`) для multi-cell на гриде без якоря просто пасует.

### 2.3 Инвентарь форков (16 шт. в 10 файлах)
| Категория | Файлы | Действие |
|---|---|---|
| **Ядро планировщика/acceptance** | `TransferPlanner` (368/537/726), `UniversalInventory:1422`, `TransferPlanExecutor:1081` | **Унифицировать** |
| Доступность в стратегиях | `InventoryStrategyBase`, `SeparableStacksStrategy` (`IsEmpty`-проверки) | **Перевести на `CanPlace`** |
| Batch | `DragContext.HasShapedEntries/HasStackedShapedEntries`, `AutoTransferService` | Переоформить как решение стратегии |
| Доменные правила | `BuiltInRules:31` | Оставить (легитимно shape-aware) |
| UI / рендер | `DropPreviewController:56`, `SourceSizedDragVisual:91`, `DragAndDropManager:257` | **Оставить** (забота рендера) |

---

## 3. Целевая архитектура

### 3.1 Один тип аллокации
`PlannedSlotAllocation` упраздняется. Везде — `PlannedPlacementAllocation`:
`(anchorIndex, orientation, shape, amount, mergeIntoExisting)`.
Single-cell = `shape` из 1 ячейки, `anchorIndex` = индекс слота, `orientation = Rot0`.

### 3.2 Один аллокатор
Единый алгоритм для любого entry (форма роли не играет):

```
PlanEntry(entry, policy, target, hint):
    1. anchors = ResolveAnchors(entry, target, hint)         # для slot-топологии anchor = слот
    2. на hinted anchor:
         - merge?  → если на anchor лежит совместимый placement с запасом → merge-аллокация
         - place?  → target.CanPlace(shape@anchor, ignore=sourcePlacementIfSame) → new-аллокация
    3. остаток количества и policy=AlternativeSlots:
         for cand in EnumerateAnchors(strategy-order):        # empty-first / merge-first и т.п.
             if target.CanPlace(shape@cand) → аллокация
    4. hinted anchor занят и policy=Swap:
         → TryPlanSwap (уже placement-based, топология-агностичен)
    5. иначе → fail
```

Ключевое: шаги 2–4 **не знают** про форму — всё решает `CanPlace` + топология. Это поглощает
сегодняшние `AllocateForStrategyInventory`, `AllocateForUniqueInventory` и `TryPlanShapedPlacement`.

### 3.3 Стратегии — доступность через топологию (#2)
- `CanUseAlternativeSlot(slot, item)` → семантически `CanPlaceAt(anchor, shape, orientation)`.
- Перечисление кандидатов (`IAlternativePlacementStrategy`, `GetSlotCandidates`) возвращает
  **якоря-кандидаты**; финальная проверка вписывания — через `CanPlace` (пустой anchor ≠ «влезет
  2×2»; `CanPlace` это ловит).
- `GetAcceptableCount` для shaped — скан якорей с `CanPlace` (оценка упаковки) вместо `return false`.

---

## 4. Что НЕ унифицируется (реальные расхождения)

Они не исчезают, но переоформляются как **решения стратегии/политики**, а не `IsSingleCell`-хардкод:

1. **Batch из нескольких shaped** — упаковка нескольких footprint'ов это bin-packing. Оставляем как
   явное «стратегия не поддерживает multi-shaped batch» (метод-решение), а не проверку формы в
   планировщике.
2. **«Сколько влезет» без якоря** для shaped — дороже (скан + `CanPlace`). Допустима консервативная
   оценка; вынести в стратегию.
3. **UI-визуалы** (размер drag-preview/overlay) — это рендер, 1×1 и так вырожденный footprint.
   Трогать не нужно.

---

## 5. Этапы (каждый компилируется и проходит тесты)

1. **#2a — `CanUseAlternativeSlot` → `CanPlace`.** В стратегиях заменить `slot.IsEmpty`-проверки
   доступности на topology/`CanPlace`. Single-cell поведение идентично (эквивалентно). Тесты: зелёные
   без изменений.
2. **#2b — shaped-aware alternative search.** В placement-пути при занятом якоре и policy=Alternative
   перебирать якоря через `CanPlace` (закрывает дыру: 2×1 на занятую ячейку находит свободный регион).
   Новый тест.
3. **Унификация типа аллокации.** `PlannedSlotAllocation` → `PlannedPlacementAllocation`
   (single-cell = footprint 1). Executor: единый commit через `TryPlace`. Убрать `:1081`.
4. **Слияние путей планировщика.** Один аллокатор (раздел 3.2); удалить `TryPlanShapedPlacement`
   как отдельный путь и guard'ы `368/537/726`. Самый рискованный шаг.
5. **Зачистка.** Удалить оставшиеся ядровые `IsSingleCell`; batch переоформить через стратегию;
   оставить только UI + `BuiltInRules`.

Порядок выбран так, чтобы риск нарастал постепенно и каждый шаг был обратимым.

---

## 6. Риски и страховка

- **Blast radius — самый сложный код:** batch, partial, stackable, unique, dynamic-slot,
  cross-inventory, conversion, swap. Любая регрессия здесь дорогая.
- **Страховка:** существующий тест-сьют (`ShapedItemPlacementTests`, `InventoryDropProcessorTests`,
  `DropPolicySettingsTests`, `DropRequestPolicyTests`) — прогон после каждого этапа.
- **Производительность:** `CanPlace` пересчитывает covered-cells; для горячих путей (скан кандидатов
  на больших гридах) при необходимости — кэш offsets по (shape, orientation).
- **Совместимость:** `PlannedSlotAllocation` — публичный? Проверить внешних потребителей до удаления
  (этап 3).
- **Anchor + grab-offset:** единый путь обязан всегда проходить резолв якоря; для slot-топологии он
  вырожден (anchor = слот). Убедиться, что single-cell drag без grab-offset даёт anchor = слот.

---

## 7. Критерий готовности

- Ноль `IsSingleCell`/`IsShaped` в `TransferPlanner`, `TransferPlanExecutor`,
  acceptance-слое `UniversalInventory` (кроме явных стратегических решений по batch).
- Один аллокатор, один тип аллокации.
- Стратегии проверяют доступность через `CanPlace`.
- Все существующие тесты зелёные + новые на: shaped alternative search, cross-topology alternative,
  unified single-cell через placement-путь.

---

*Основано на трассировке кода: `TransferPlanner.cs`, `TransferPlanExecutor.cs`, `PlacementStore.cs`,
`InventoryTopology.cs`, `UniversalInventory.cs`, `InventoryStrategyBase.cs`,
`Core/Drop/*AlternativePlacementStrategy.cs`.*
