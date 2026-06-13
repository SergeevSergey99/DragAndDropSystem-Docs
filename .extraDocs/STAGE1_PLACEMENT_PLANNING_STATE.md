# Этап 1 — `PlacementPlanningState` (детальная спека)

Companion к [UNIFIED_PLACEMENT_PLANNING_PLAN.md](./UNIFIED_PLACEMENT_PLANNING_PLAN.md), раздел 4.1 и
этап 1. Здесь — конкретные типы, сигнатуры, инварианты и тест-матрица, достаточные чтобы начать
кодить, не меняя публичные планы и executor.

> Scope этапа 1: ввести тип + покрыть тестами. **Не** проводить через planner/executor/acceptance
> (это этапы 2+). На этом этапе тип существует и тестируется изолированно.

---

## 1. Назначение и границы

`PlacementPlanningState` — это **planning-time, немутирующее, topology-aware зеркало occupancy**
одного инвентаря. Оно:

- инициализируется из реальных `Placement` инвентаря;
- позволяет резервировать запланированные footprint (create) и merge-capacity **в пределах одного
  `TransferPlan`**, чтобы решить проблему из раздела 3 плана (несколько ещё не выполненных операций);
- считает footprint **той же топологией**, что и runtime, поэтому feasibility совпадает с тем, что
  потом сделает executor через `TryPlace`;
- никогда не трогает inventory, slots и runtime `PlacementStore`.

### Чем НЕ является и связь с `VirtualSlotState`

| | `VirtualSlotState` (существует) | `PlacementPlanningState` (этап 1) |
|---|---|---|
| Единица | один слот = один stack | occupancy всего инвентаря по ячейкам |
| Знает геометрию | нет | да (footprint, bounds, occupancy) |
| Отвечает за | stack/unique/rule reasoning per slot | проекция shape, bounds, occupancy, резервирование |
| Не отвечает за | геометрию | stacking/unique/max-stack/rules |

Они **сосуществуют**: VSS остаётся для stack/rule-совместимости, PPS добавляет геометрию там, где её
не хватает. Слияние single-cell пути на PPS — опциональный этап 5, не сейчас.

---

## 2. Модель данных

### `PlannedPlacement` (токен занятости)

Лёгкий planning-side аналог `Placement`. **Не** держит и не мутирует runtime `Placement`.

```csharp
internal enum PlannedPlacementOrigin { Real, Reserved }

internal sealed class PlannedPlacement
{
    public int AnchorIndex { get; }
    public int Orientation { get; }
    public IPlacementShape Shape { get; }                 // value-like, безопасно шарить ссылку
    public IReadOnlyList<int> CoveredIndices { get; }     // зафиксировано при создании токена
    public IItemAdapter ItemAdapter { get; }             // для merge-eligibility (решает caller)
    public PlannedPlacementOrigin Origin { get; }

    public int ReservedAmount { get; internal set; }      // виртуальный текущий count (для merge)
    public bool IsReleased { get; internal set; }         // освобождён source-release'ом
}
```

### Внутреннее состояние `PlacementPlanningState`

```csharp
private readonly IPlacementInventory _inventory;            // источник топологии и реальных placement
private readonly Dictionary<int, PlannedPlacement> _cellToPlacement;  // covered cell → occupant
private readonly Dictionary<int, PlannedPlacement> _byAnchor;         // anchorIndex → occupant
private readonly HashSet<PlannedPlacement> _released;       // для exactly-once source release
```

`_cellToPlacement` зеркалит структуру `PlacementStore._cellToPlacement` — это намеренно: одинаковая
модель occupancy → одинаковые ответы.

---

## 3. Публичный API

```csharp
internal sealed class PlacementPlanningState
{
    public PlacementPlanningState(IPlacementInventory inventory);   // seed из inventory.Placements

    public IInventory Inventory { get; }

    // --- Lookups (зеркало PlacementStore) ---
    public PlannedPlacement GetPlacementAt(int cellIndex);                 // covered ИЛИ anchor → токен / null
    public bool TryResolve(Placement realPlacement, out PlannedPlacement planned); // real → planning-токен

    // --- Geometry / feasibility (только topology + bounds + occupancy) ---
    public IReadOnlyList<int> GetCoveredCells(
        int anchorIndex, IPlacementShape shape, int orientation);
    public bool CanPlace(
        PlacementRequest request,
        PlannedPlacement ignoredA = null,
        PlannedPlacement ignoredB = null);

    // --- Reservation ---
    public bool TryReserveCreate(PlacementRequest request, out PlannedPlacement reserved);
    public bool TryReserveMerge(PlannedPlacement target, int amount);

    // --- Source release для same-inventory move (идемпотентно, exactly-once) ---
    public bool ReleaseSourcePlacement(Placement realSourcePlacement);
    public bool ReleaseSourcePlacement(PlannedPlacement planned);

    // --- Capacity bookkeeping (только арифметика; политику max-stack даёт strategy) ---
    public int GetReservedAmount(PlannedPlacement target);
    public int GetRemainingMergeCapacity(PlannedPlacement target, int strategyMaxStack);
}
```

### Уточнение к плану (важно)

В разделе 4.1 плана операция значится как `GetRemainingMergeCapacity(placement, item)`. Это нарушало
бы границу ролей из 4.3: `item` тянет за собой знание max-stack, а max-stack принадлежит **стратегии**,
не геометрии. Поэтому сигнатура уточнена:

- состояние хранит только `ReservedAmount` и считает `remaining = max(0, strategyMaxStack − reserved)`;
- `strategyMaxStack` **передаёт caller** (allocator, спросив у стратегии);
- **eligibility** мержа (CanStack/one-per-ID/separable) тоже решает caller до вызова — состояние не
  знает про stacking semantics.

Так геометрия не принимает решений о stacking, а стратегия не лезет в occupancy — ровно как требует
раздел 4.3.

---

## 4. Алгоритмы (семантика операций)

**Конструктор (seed).** Для каждого `p` из `_inventory.Placements`:
создать `PlannedPlacement(Origin.Real)` с `AnchorIndex/Orientation/Shape = p.*`,
`CoveredIndices = p.CoveredIndices` (скопировать в свой список), `ReservedAmount = p.Stack.Count`,
`ItemAdapter = p.Stack.PrimaryAdapter`. Зарегистрировать в `_byAnchor[anchor]` и во всех
`_cellToPlacement[cell]`. После конструктора — никаких обращений к inventory/store.

**`GetCoveredCells(anchor, shape, orientation)`** → делегирует
`_inventory.GetCoveredCells(...)` (уже `RequireAllInBounds`, проверено) — единый источник проекции
footprint, без дублирования геометрии. Пустой результат = не помещается/вне границ.

**`CanPlace(request, ignoredA, ignoredB)`:**
1. `cells = GetCoveredCells(request.AnchorIndex, request.Shape, request.Orientation)`;
2. если `cells` пуст → `false`;
3. для каждой `cell`: если `_cellToPlacement[cell]` существует, и это **не** `ignoredA`/`ignoredB`,
   и не `IsReleased` → `false`;
4. иначе `true`.
(Тот же two-ignore контракт, что мы добавили в runtime `PlacementStore.CanPlace` для swap.)

**`TryReserveCreate(request, out reserved)`:**
`if (!CanPlace(request)) return false;` иначе создать `PlannedPlacement(Origin.Reserved)` с
`ReservedAmount = request.Stack.Count`, зарегистрировать его cells/anchor, вернуть токен. С этого
момента зарезервированный footprint **виден** последующим `CanPlace` — это и закрывает баг батча из
раздела 3.

**`TryReserveMerge(target, amount)`:** `target` зарегистрирован и не released →
`target.ReservedAmount += amount; return true`. Footprint (CoveredIndices) **не меняется**. Проверку
вместимости делает caller через `GetRemainingMergeCapacity`.

**`ReleaseSourcePlacement(real|token)`:** разрезолвить (real → `_byAnchor[real.AnchorIndex]`); если
токен найден и ещё не в `_released` → снять его cells из `_cellToPlacement`, пометить `IsReleased`,
добавить в `_released`, `return true`. Повторный вызов на том же токене → `return false`
(**exactly-once** для same-inventory move с несколькими аллокациями одного entry).

**`GetRemainingMergeCapacity(target, strategyMaxStack)`** → `target==null||IsReleased ? 0 :
max(0, strategyMaxStack − target.ReservedAmount)`.

---

## 5. Инварианты

1. **Немутабельность runtime.** После конструктора PPS не вызывает inventory/slots/`PlacementStore`.
   `inventory.Placements` и stacks слотов после любых операций PPS **не изменены**.
2. **Геометрическая эквивалентность runtime.** Для исходного (нерезервированного) состояния
   `PPS.CanPlace(r) == PlacementStore.CanPlace(r)` для любого `r` (parity).
3. **Видимость резерва.** После `TryReserveCreate(A)` все `CanPlace`, чьи cells пересекают footprint
   A, возвращают `false` (если A не передан как ignored).
4. **Source release ровно один раз** на токен; повторный — no-op `false`.
5. **Merge не двигает footprint** — меняет только `ReservedAmount`.
6. **SlotTopology коллапсирует** — multi-cell shape резервирует одну ячейку (anchor-only),
   как и runtime.
7. **Disjointness резерва.** Cells успешно зарезервированного create-токена не пересекаются ни с
   одним живым (не released) токеном на момент резервирования.

---

## 6. Интеграция (готовится сейчас, включается на этапе 2)

- Планировщик держит реестр `Dictionary<IInventory, PlacementPlanningState>`, создаваемый лениво **на
  один `TransferPlan`**. Все entries, целящиеся в один инвентарь, делят его PPS → это и есть «общий
  planning state» из раздела 3.
- Этап 1 реестр **не** добавляет в planner; вводит только тип и его тесты. Реестр появляется на
  этапе 2, когда shaped alternative-search начнёт ходить через PPS.
- `VirtualSlotState` остаётся нетронутым; PPS его не заменяет на этом этапе.

---

## 7. Тест-матрица этапа 1 (изолированно, без planner)

**Seed / lookups**
- Инвентарь со смешанными single- и multi-cell placement → `GetPlacementAt` возвращает один и тот же
  токен и по anchor-, и по любой covered-ячейке multi-cell предмета.
- `TryResolve(realPlacement)` находит токен по реальному `Placement`.

**CanPlace**
- свободный регион → true; занятый → false; занятый, но переданный в `ignored` → true.
- вне границ → false; ориентация, не помещающаяся в сетку → false.
- two-ignore: `CanPlace(ignoredA=source, ignoredB=target)` = true там, где single-ignore = false.

**Reservation (ядро инварианта раздела 3)**
- `TryReserveCreate(A)`, затем `CanPlace` на пересекающийся регион → false.
- два `TryReserveCreate` на непересекающиеся регионы → оба true.
- `TryReserveCreate` на занятый регион → false, токен не создан, occupancy не изменён.

**Source release / same-inventory move**
- `ReleaseSourcePlacement` освобождает cells: `CanPlace` на освобождённый регион → true.
- повторный `ReleaseSourcePlacement` того же токена → false (exactly-once).
- release source, затем `TryReserveCreate` footprint, **пересекающего** старые cells источника →
  true (предмет «переезжает» на свои же ячейки).

**Merge / capacity**
- `GetRemainingMergeCapacity(t, maxStack=N)` == `N − t.ReservedAmount`.
- `TryReserveMerge(t, k)` уменьшает remaining на k; `CoveredIndices` токена не меняется.
- цепочка merge: сумма зарезервированного не превышает maxStack при корректной проверке caller'ом.

**Topology**
- SlotTopology: multi-cell shape резервирует ровно одну ячейку.
- GridTopology: 2×1/3×1 с ориентациями покрывают ожидаемые наборы ячеек.

**Parity / немутабельность**
- Матрица запросов: `PPS.CanPlace` (исходное состояние) == `PlacementStore.CanPlace` 1:1.
- После серии reserve/merge/release: `inventory.Placements` и stacks слотов идентичны исходным
  (PPS ничего не мутировал в runtime).

---

## 8. Non-goals этапа 1

- Не проводится через planner/executor/acceptance.
- `NewDynamicSlot` (резерв слота с неизвестным index) **не** моделируется — это этап 3.
- Нет мульти-инвентарной оркестрации сверх отдельных per-inventory инстансов.
- Нет оптимальной упаковки/backtracking.

---

## 9. Definition of Done (этап 1)

- `PlacementPlanningState` + `PlannedPlacement` реализованы как `internal`.
- Вся тест-матрица раздела 7 зелёная, включая parity с `PlacementStore` и проверку немутабельности.
- Тип компилируется и живёт изолированно; publicные планы, executor и acceptance не изменены.
