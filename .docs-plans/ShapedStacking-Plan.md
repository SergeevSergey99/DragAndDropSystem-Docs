# План: Shaped Stacking — стек count>1 внутри одного Placement

> Самодостаточный план. Реализацию можно вести, имея в контексте **только этот файл** + перечисленные исходники.
> Статус: **согласована модель и решения, режем на коммиты.** Релиз один — после всех коммитов; каждый коммит
> **компилируется**. Часть существующих тестов намеренно кодирует старый инвариант (count==1) и будет переписана —
> это отмечено по коммитам.
>
> Продолжение линии `SlotSelectionPolicy-Plan.md` (policy/planner/executor пайплайн уже внедрён). Здесь —
> распространение strategy-семантики на предметы сложной формы (shaped/placement) с поддержкой стека.

---

## 0. Контекст и принятые решения

Сейчас в движке действует **жёсткий инвариант: многоклеточное (shaped) размещение содержит ровно 1 предмет**
(`Count == 1`). Пользователь хочет распространить strategy-семантику на shaped так же, как на одноклеточные:

| Стратегия | Текущее поведение (shaped) | Целевое поведение |
|---|---|---|
| **Unique** | каждый предмет — своё размещение, count 1 | без изменений (каждый предмет занимает свои ячейки, count 1) |
| **SeparableStacks** | несколько отдельных размещений, **каждое count 1** | несколько размещений; **внутри каждого можно копить стек count>1**; слияние только при явном дропе на это размещение |
| **Stackable** | grid: плодит дубликаты (баг); slot: one-per-ID, но count 1 | **один Placement на ID**; **внутри стек count>1**; **авто-слияние** дубликатов в это размещение |

**Решения (подтверждены пользователем):**
1. **Снять инвариант count==1 для shaped** — внутри одного Placement может лежать стек `count>1`.
2. **Целевая модель — grid.** Идея на будущее: рассматривать slot-инвентари как grid, где каждый предмет занимает
   одну ячейку, чтобы shaped/stack логика была единой веткой (см. C7, опционально).
3. **Авто-слияние на старте**, но у стратегии «по-хорошему» должна быть **переключаемая опция** merge-режима,
   работающая и для shaped, и для обычных одноклеточных предметов (см. C8).
4. **Split — делаем сразу** (C5): это обычное разбиение стека (отделить N предметов в новое размещение).
5. **Унификация slot/grid — в работу** (C7), но **через общую топологию-абстракцию `IInventoryTopology`**
   (а не частный «slot как 1×1»), чтобы оставить расширяемость под будущие раскладки (напр. **гексагональную**
   сетку слотов). slot и grid — частные реализации одного интерфейса топологии.

---

## 1. Где зашит инвариант count==1 (проверено по коду; reference)

- **`PlacementStore.CanPlace`** (`Scripts/Inventories/PlacementStore.cs:53`):
  ```csharp
  if (!PlacementShapeUtility.IsSingleCell(request.Shape, request.Orientation) && request.Stack.Count > 1)
      return false;
  ```
  Геометрический отказ любому multi-cell стеку с `Count>1`. Рядом — `IsRejectedShapedSlotPlacement`
  (`SlotShapedItemPolicy.Reject`, оставляем) и `ShouldCollapseToAnchor` (slot-топология сводит covered к anchor).
- **`InventoryStrategyBase.GetMaxStackSize`** (static, `Scripts/Inventories/Strategies/InventoryStrategyBase.cs:132-141`):
  ```csharp
  if (!PlacementShapeUtility.IsSingleCell(PlacementShapeUtility.Resolve(itemAdapter), PlacementOrientation.Rot0))
      return 1;                       // ← форсит shaped → 1
  ```
  Используется `StackBasedInventoryStrategyBase.GetMaxStackSizeForItem` (`StackBasedInventoryStrategyBase.cs:26`) и
  всеми stack-based стратегиями. **`UniqueItemStrategy` сюда НЕ ходит** — оперирует литералом `1`
  (`UniqueItemStrategy.cs:121,122,140,144`), поэтому снятие форса его не затронет.
- **`TransferPlanner`** охранники single-item для shaped:
  - `:339` `"Shaped item transfer requires a single item"` (ветка not-single-cell, `requested != 1`).
  - `:519` `"Shaped grid placement requires a single item"` внутри `TryPlanShapedPlacement` (`:497-602`).
- **`TransferPlanExecutor.TryAddToTargetPlacement`** (`Scripts/Inventories/TransferPlanExecutor.cs:1045-1105`):
  - `:1066` отказ `TransferStack.Count > 1 || TransferAmount != TransferStack.Count`;
  - всегда создаёт **новое** размещение через `targetPlacementInventory.TryPlace` (`:1096`) — **слияния нет**.
- **`DragContext.HasStackedShapedEntries`** (`Scripts/Core/Models/DragContext.cs:106-119`, условие `:113`
  `entry.IsShaped && entry.Stack.Count > 1`) — флаг, по которому stacked-shaped drag отвергается.

**Что УЖЕ готово к count>1:**
- `Placement` хранит полноценный `ItemStack MutableStack` (`Scripts/Core/Models/Placement.cs:121,131-132`),
  count произволен.
- `UniversalInventory.TryAddToSlotStack` (`Scripts/Inventories/UniversalInventory.cs:814-827`) доливает в
  `MutableStack` существующего размещения (без лимита — лимит = задача стратегии). Это и есть примитив слияния.
- `StackableItemStrategy.GetSlotCandidates` (`Scripts/Inventories/Strategies/StackableItemStrategy.cs:196-249`)
  уже one-per-ID, дедуплицирует covered-ячейки (`ShouldSkipDuplicatePlacementLocation`,
  `InventoryStrategyBase.cs:304`) и резолвит anchor-слот размещения (`ResolveLogicalStackSlot`,
  `InventoryStrategyBase.cs:313`). `IsSourceSlot` исключает весь source-placement (`:279-302`).
- `PlannedPlacementAllocation` несёт `AnchorIndex/Orientation/Shape/Amount` (см. использование
  `TransferPlanExecutor.cs:1052-1057`, `TransferPlanner.cs:590-600`).

**Ключевой архитектурный факт:** shaped идёт **отдельной веткой планирования** `TryPlanShapedPlacement`
(`TransferPlanner.cs:351`), **до** `GetAcceptableCount` (`:395`) и candidate-loop. Поэтому сейчас shaped полностью
**минует** strategy-семантику (one-per-ID, ёмкость, merge). Главная задача плана — вернуть shaped под стратегию для
решения «merge vs new / есть ли место», сохранив геометрию (anchor, covered, CanPlace) на стороне placement-инвентаря.

---

## 2. Целевая модель

**Разделение «геометрия ↔ количество».**
- `PlacementStore`/`IPlacementInventory` отвечают за **геометрию**: какие ячейки заняты, можно ли разместить
  футпринт, пересечения, границы. **Количество в стеке их не волнует.**
- **Лимит стека (`maxStack`) и решение merge-vs-new — задача стратегии/планировщика**, как и для одноклеточных.

**Семантика по стратегиям (единая для shaped и single-cell):**
- **Unique:** один предмет на размещение, `count 1`, отдельные размещения, без слияния. (Готово литералом `1`.)
- **SeparableStacks:** допускает несколько размещений одного ID; внутри размещения стек растёт до `maxStack`;
  слияние **только при явном дропе на ячейки этого размещения**; дроп в пустую область → новое размещение.
- **Stackable:** **один Placement на ID**; стек растёт до `maxStack`; **авто-слияние** дубликата в существующее
  размещение; если оно полно — reject (второго размещения не открываем).

**Merge-режим как опция (C8):** сериализуемый флаг у stack-based стратегий
(напр. `AutoMergeStacks`) — авто-слияние vs только-явное; defaults: Stackable=auto, SeparableStacks=explicit.
Работает одинаково для shaped и одноклеточных.

---

## 3. Разбивка по коммитам

Каждый коммит компилируется. Тесты, кодирующие старый инвариант, переписываются в указанном коммите.

### C1 — Модель: размещение хранит стек (count≥1)
- `PlacementStore.CanPlace` (`PlacementStore.cs:53`): **убрать** отказ `!IsSingleCell && Count>1`. Оставить
  `IsRejectedShapedSlotPlacement` и проверки границ/пересечений.
- Проверить `PlacementStore.TryPlace` (`:78-94`) — создаёт `Placement` с переданным стеком (count уже произвольный,
  `Placement.cs:121` делает copy). Доп. изменений в модели не нужно.
- Тест: переписать `ShapedItemPlacementTests.CanPlace_RejectsMultiCellStackWithMultipleItems`
  (`Tests/Editor/Inventories/ShapedItemPlacementTests.cs:1419`) → теперь `CanPlace` принимает multi-cell стек count>1.
- **Эффект:** появляется *возможность*; наблюдаемое поведение почти не меняется (лимит ещё держит стратегия в C2,
  слияния в пайплайне ещё нет).

### C2 — Ёмкость стратегии для shaped
- `InventoryStrategyBase.GetMaxStackSize` (`:132-141`): **убрать** ветку `!IsSingleCell → return 1`. Shaped получает
  обычный лимит (`defaultMaxStackSize` / `IStackSizeLimitable`), как одноклеточные. `UniqueItemStrategy` не затронут.
- Следствие: `StackableItemStrategy`/`SeparableStacksStrategy` в `GetSlotCandidates`/`GetAcceptableCount` начнут
  отдавать **реальную ёмкость** существующего shaped-размещения (anchor-слот уже резолвится через
  `ResolveLogicalStackSlot`).
- Тесты: добавить strategy-level проверки ёмкости shaped (Stackable/Separable). Меняются только результаты
  query/preview; пайплайн ещё создаёт новые размещения (merge в C3/C4).

> **Статус C3+C4: СДЕЛАНЫ вместе** (один сквозной grid-merge — раздельно дают нерабочий промежуток).
> Охват — **grid**. Не-grid drop-merge (дроп shaped на занятый слот slot-инвентаря) **отложен в C7**
> (унификация топологии), поэтому `ProcessDrop_ShapedSlotToOccupiedSlot_Rejects` пока остаётся валиден и НЕ
> переписывается. Ослабление охранника `requested != 1` перенесено в **C5** (там же включается drag count>1).
> Реализация: флаг `PlannedPlacementAllocation.MergeIntoExisting`; `TransferPlanner.TryResolveShapedMergeTarget`
> (Stackable=авто по `Placements`, Separable=по перекрытию `GetCoveredCells`, Unique=нет);
> `TransferPlanExecutor.TryMergeIntoExistingPlacement` (через `TryAddToSlotStack` в anchor).
> Тесты: 4 новых grid-кейса в `ShapedItemPlacementTests` (auto-merge away, full→reject, separable onto/away).

### C3 — Планирование shaped: merge vs new (one-per-ID & capacity aware)
- Переписать `TransferPlanner.TryPlanShapedPlacement` (`:497-602`): перед геометрией спросить стратегию через
  candidate-механизм (или прямой query), **существует ли размещение этого ID и есть ли в нём место**:
  - **Stackable (one-per-ID, авто-merge):** размещение есть и есть место → план **merge** в его anchor; полно →
    reject; нет → **new** (текущая геометрия + anchor-резолв).
  - **SeparableStacks:** дроп пришёлся на ячейки существующего размещения того же ID → **merge**; иначе → **new**.
  - **Unique:** как сейчас (new, count 1).
- Расширить `PlannedPlacementAllocation` признаком **merge-в-существующий-anchor** (или семантикой
  `AnchorIndex == anchor существующего размещения` + `Amount`). Сохранить `Shape/Orientation/Amount`.
- Ослабить охранники `requested != 1` (`:339`, `:519`) до **ёмкости** (`min(requested, maxStack - currentCount)` для
  merge; для new — `min(requested, maxStack)`). Сохранить запрет batch для shaped (`:343`).
- **Эффект:** план содержит merge-аллокации; исполнение — в C4.

### C4 — Исполнитель: слияние в существующее размещение
- `TransferPlanExecutor.TryAddToTargetPlacement` (`:1045-1105`):
  - **снять** запрет count>1 (`:1066`);
  - если аллокация — merge (anchor существующего размещения того же ID): доливать через
    `UniversalInventory.TryAddToSlotStack` (`UniversalInventory.cs:814-827`) на anchor-слот до `Amount`/ёмкости;
  - иначе — создавать новое размещение через `TryPlace` (как сейчас, `:1096`).
- Откат: проверить, что `PlacementSnapshot` (`ResolvePlacementSnapshot`, `:1107-1128`) при merge восстанавливает
  **прежний count**, а не удаляет всё размещение (atomic-режим); best-effort — корректный декремент.
- Тесты (переписать под новое поведение):
  - `StackableSlotInventory_ShapedItemsUseOnePlacementPerId` (`ShapedItemPlacementTests.cs:1117`): второй «bag» →
    **count 2** в одном размещении, второй слот пуст (а не reject).
  - `ProcessDrop_ShapedSlotToOccupiedSlot_Rejects` (`:1139`): развести на «другой ID → reject» (оставить) и новый
    кейс «тот же ID → merge».

### C5 — Перетаскивание стека shaped (move + split)
- Разрешить тащить размещение с `count>1`: `ResolveDragAmount` для shaped, `entry.Stack.Count>1`.
- **Move целиком:** перенос всего стека одним футпринтом, count сохраняется.
- **Split (делаем):** обычное разбиение стека — отделить N из shaped-стека в **новое** размещение (новый футпринт)
  на N предметов; остаток остаётся в исходном. Переиспользовать существующую механику split одноклеточных стеков
  (`ItemStack.Split`), наложив футпринт.
- Снять/ослабить охранники, отвергавшие stacked-shaped drag (`DragContext.HasStackedShapedEntries` и его
  использования). Batch для shaped по-прежнему запрещён.

### C6 — UI: отображение количества на shaped-размещении
- Показ count на anchor-ячейке размещения (`Scripts/UI/PlacementOverlay.cs`, отображение количества слота).
  Убедиться, что covered-ячейки не дублируют цифру.

### C7 — Унификация slot/grid через общую топологию (`IInventoryTopology`)
- Свести shaped/stack логику к **единой ветке поверх абстракции топологии** `IInventoryTopology` (уже есть:
  `RectGridTopology`, `SlotTopology`; см. `ShapedItemPlacementTests` — `RectGridTopology(GridTopology)`,
  `SlotTopology(n)`). slot и grid — частные реализации одного интерфейса; планировщик/стор работают через топологию,
  а не через `_useGridTopology`-ветвления.
- **Расширяемость:** интерфейс должен позволять добавить новую раскладку (напр. **гексагональную** сетку слотов)
  без правок планировщика/исполнителя — только новая реализация `IInventoryTopology` + `IPlacementShape`/offsets под
  гекс-координаты.
- Убрать частные ветвления `_useGridTopology`/collapse там, где их заменяет топология.

### C8 — Переключатель merge-режима у стратегии
- Сериализуемая опция (напр. `AutoMergeStacks`) у `StackBasedInventoryStrategyBase`: авто-слияние vs только-явное,
  одинаково для shaped и одноклеточных. Defaults: Stackable=auto, SeparableStacks=explicit (поведение C1-C6
  сохраняется).

### C9 — Тесты / демо / доки / скиллы
- Полный набор тестов shaped-стекинга по стратегиям; обновить доку классов стратегий, `dragdrop-architecture` и др.
  скиллы; проверить демо `Examples/Demo1 Inventories/InventoriesDemo.unity`.

Релиз — после C9.

---

## 4. Риски / заметки
- **C5 split shaped-стека** — самая неоднозначная часть (что значит «отделить N» геометрически). Если split на
  старте не нужен — урезать C5 до «двигать только весь стек».
- **Откат merge в atomic-режиме** — снапшот обязан вернуть прежний count, а не снести размещение целиком.
- **Существующие тесты старого инварианта** переписываются (C1: `CanPlace_RejectsMultiCellStackWithMultipleItems`;
  C4: `StackableSlotInventory_ShapedItemsUseOnePlacementPerId`, `ProcessDrop_ShapedSlotToOccupiedSlot_Rejects`).
- **Unique не трогаем** — он на литерале `1`, остаётся count 1.
- **Геометрия остаётся в placement-инвентаре** — стратегия решает только merge-vs-new и ёмкость, не футпринт.

## 5. Лог решений
1. Снять инвариант count==1 для shaped: один Placement может держать стек count>1.
2. Лимит стека и merge-vs-new — задача стратегии, не `PlacementStore` (разделение геометрия/количество).
3. Stackable = one-per-ID + авто-merge; SeparableStacks = много размещений + merge только по явному дропу;
   Unique = count 1 без слияния.
4. Целевая модель — grid; slot/grid унифицируются через `IInventoryTopology` (C7), с расширяемостью под гекс.
5. Авто-merge на старте; переключаемая опция merge-режима — C8 (для shaped и одноклеточных).
6. Split shaped-стека — делаем сразу (C5), на базе обычного `ItemStack.Split` + футпринт.
7. Релиз один, после C1..C9; каждый коммит компилируется. Git-операции — на стороне пользователя.
