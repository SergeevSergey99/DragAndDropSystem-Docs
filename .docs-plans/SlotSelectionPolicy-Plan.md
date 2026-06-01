# План: SlotSelectionPolicy — eligibility (стратегия) vs selection (политика)

> Самодостаточный план. Реализацию можно начинать, имея в контексте **только этот файл** + перечисленные
> исходники. Статус: **согласован. Стартуем с Фазы 1.** Версия скилл-архитектуры: 2.4.

---

## 0. TL;DR

Расщепляем выбор слота на:
- **Стратегия = eligibility**: какие слоты годятся (список кандидатов) + capability «можно создать новый».
- **Политика (`SlotSelectionPolicy`) = selection**: какой из кандидатов взять. Полиморфный класс, переопределяется на `InventoryDropArea`.

Работа разбита на 3 фазы:
- **Фаза 1 (СТАРТ, ноль регрессий):** вводит модель candidates/policy. Стратегии сохраняют текущую eligibility. Политики, выбирающие **существующий** слот (First/Last/Random/PreferStack/PreferEmpty), исполняются планировщиком. `SlotSelection.New()` имеет **сильную** семантику (force create new slot) уже в контракте, и его intent протаскивается до `InventoryDropProcessor`, но **исполняется только после Фазы 2**.
- **Фаза 2 (forced-new):** planner+executor исполняют force-new intent через `TryCreateSlot` + `TryAddToSlot`. Нужна для `SeparableStacks` + политика «новый стек» при `maxFreeSlots = 0` (при `maxFreeSlots > 0` буфер пустых уже даёт эффект через выбор конкретного пустого слота в Фазе 1).
- **Фаза 3 (one-per-ID, отдельный проект):** новый класс стратегии с inventory-aware размещением по всему пайплайну.

> **Release gate = Фаза 1 + Фаза 2.** Фаза 1 в одиночку НЕ релизится как «кастомные политики выбора»: пока Фаза 2
> не приземлилась, `New()`-форсирующая политика доходит до процессора, но планировщик её не исполнит (положит в
> существующий слот). Фаза 1 — это модель + политики по существующим слотам; полноценная фича = 1+2.

---

## 1. Как выбор слота работает сейчас (проверено по коду)

Цепочка при наведении на область (нет конкретного слота под курсором):
```
InventoryDropArea.TryBuildValidationContext           Scripts/UI/InventoryDropArea.cs:147
  → _inventory.CanAcceptItem(request, out suggestedBaseSlot)   :171
      → UniversalInventory.CanAcceptItem(...)          Scripts/Inventories/UniversalInventory.cs:1445
          → _acceptanceStrategy.CanAcceptItem(_slots, request, canCreateNewSlot, potentialNewSlots, prefab, out suggestedBaseSlot)  :1458
  → _foundBaseSlot = suggestedBaseSlot                 InventoryDropArea.cs:74
  → CreateDropProcessor(_foundBaseSlot)                → _targetBaseSlot процессора
      → TransferPlanner.BuildPlan(..., targetBaseSlotHint: _foundBaseSlot, ...)
```
`canCreateNewSlot`/`potentialNewSlots` вычисляются в `UniversalInventory.CanAcceptItem` (`:1456-1457`) из
`_slotManagementSettings` и приходят в стратегию **готовыми**.

Текущая eligibility/preference по стратегиям (то, что Фаза 1 обязана сохранить 1-в-1):
- **UniqueItemStrategy** (`Strategies/UniqueItemStrategy.cs:105`): первый пустой (`PassesRules(slot,item,1)`) по индексу → иначе `canCreateNewSlot && potentialNewSlots>0 && PrefabPassesRules(...,1)`.
- **StackableItemStrategy** (`Strategies/StackableItemStrategy.cs:175`): **stack-first** — первый непустой слот с местом (`CanStack` && `canFit>0` && rules) по индексу; пустой запоминается как fallback; в конце пустой-fallback, иначе `canCreateNewSlot && PrefabPassesRules(..., Min(desired,maxSize))`. Итог: **stack(по индексу) → empty(по индексу) → new**. Дока класса: «one item type **can occupy multiple slots**», авто-merge с overflow.
- **SeparableStacksStrategy** (`Strategies/SeparableStacksStrategy.cs:192`): **позиционный** — первый по индексу слот, который либо пустой-ok, либо stack-с-местом-ok; иначе `canCreateNewSlot && PrefabPassesRules(...)`. Дока: несколько стеков одного предмета, merge только по явному дропу.

Зовут стратегический `CanAcceptItem` ровно 2 места: `UniversalInventory.cs:1458` и `DynamicSlotDecorator.cs:175`.

Хелперы `InventoryStrategyBase` (`Strategies/InventoryStrategyBase.cs`): `PassesRules` (:159), `PrefabPassesRules` (:192),
`FindSlotWithItem` (:180), `GetMaxStackSize(item,defaultMax,allowOverride)` static (:123).

### Что планировщик делает с выбранным слотом (критично для Фаз 2/3)

- Конкретный `targetBaseSlotHint != null` → `preferHint = true`, слот пробуется первым (`TransferPlanner.cs:756`, `PlanHintOnlyEntry:623`). **Выбор конкретного слота исполняется.**
- `targetBaseSlotHint == null` → раскладка в существующие через `AllocateForStrategyInventory` (`:742`) / blocked-resolver / `CanUseAlternativeSlot`.
- **Создание нового слота — только fallback (deferred):** `PlannedSlotAllocation(null, amount)` возникает при пустом инвентаре (`:411`) или при `plannedAmount==0 && CanUseDeferredPlacement && !HasCurrentSlotPlacementCapacity` (`:445-448`). Исполнение deferred идёт через `TryAddStack(-1)`, который **переиспользует ПЕРВЫЙ пустой слот** (executor `TransferPlanExecutor.cs:894-906`, `TryAddToTargetInventory:1004`). То есть `New()`-намерение при наличии годных слотов сейчас НЕ исполняется.

### Буфер свободных слотов (важно для Фазы 2)

`DynamicSlotManagementSettings` (`Strategies/DynamicSlotManagementSettings.cs`):
- `_maxFreeSlots = 0` (дефолт): пустых слотов нет; новый создаётся только on-demand в `TryAddStack` (deferred), а тот срабатывает лишь когда в существующих нет места.
- `_maxFreeSlots > 0`: инвентарь держит буфер **реальных пустых слотов** (`EnsureFreeSlots:37`, обрезка в `HandleSlotEmptied:58`). Значит «новый слот» = уже существующий пустой слот → **выбор конкретного пустого слота политикой исполняется планировщиком без всякой новой машинерии**.
- `CanCreateNewSlot(count) = count < _maxSlots`; `GetPotentialNewSlots = max(0, _maxSlots - count)`.
- Слоты создаёт приватный `UniversalInventory.CreateSlot()` (`:366`), проброшенный в рантайм-стратегию через `WrapRuntimeStrategy(... CreateSlot ...)` (`:401`).

---

## 2. Фаза 1 — рефактор выбора (СТАРТ, ноль изменений поведения)

### 2.1 Новые типы (namespace `UDND.Inventories`, папка `Scripts/Inventories/`)

> Ссылаются на `BaseSlot` (`UDND.Slots`) и `InventoryAcceptanceRequest` (`UDND.Inventories`).
> `InventoryDropArea` (`UDND.UI`) уже делает `using UDND.Inventories`.

**Богатый кандидат** (чтобы политики не пересчитывали capacity/fit):
```csharp
public readonly struct SlotAcceptanceCandidate
{
    public BaseSlot Slot { get; }
    public int RemainingCapacity { get; } // empty: maxSize; stack: maxSize - count (для Unique = 1)
    public bool IsEmpty { get; }
    // ctor
}

public sealed class SlotAcceptanceCandidates
{
    public IReadOnlyList<SlotAcceptanceCandidate> Slots { get; } // в порядке индекса, без preference
    public bool CanCreateNewSlot { get; }   // capability: можно положить в слот, который будет создан сейчас
    public int  PotentialNewSlots { get; }
    public bool HasAny => (Slots != null && Slots.Count > 0) || CanCreateNewSlot;
    // ctor + static Empty (Array.Empty<SlotAcceptanceCandidate>(), false, 0)
}
```
`Slots` никогда не `null`. Список — чистая eligibility в порядке индекса; **порядок-предпочтение задаёт политика**, не стратегия.

**Результат выбора:**
```csharp
public readonly struct SlotSelection
{
    public bool Accepted { get; }
    public BaseSlot Slot { get; }   // конкретный слот, или null если CreateNew
    public bool CreateNew { get; }  // СИЛЬНАЯ семантика: "force create a new slot" (Slot == null).
    public static SlotSelection None => new(false, null, false);
    public static SlotSelection Existing(BaseSlot s) => new(true, s, false);
    public static SlotSelection New() => new(true, null, true);
}
```
> `CreateNew == true` означает именно «создать новый слот», а не «нет подсказки». Этот intent **обязан** дойти до
> исполнения (Фаза 2: `TryCreateSlot` + `TryAddToSlot`). Запрещено молча трактовать `New()` как «area-drop без слота».
> В Фазе 1 дефолт-политики возвращают `New()` только когда конкретных кандидатов нет (это совпадает с текущим
> deferred-поведением и корректно); intent протаскивается до процессора, но планировщик исполнит его лишь в Фазе 2.

**Политика:**
```csharp
[Serializable]
public abstract class SlotSelectionPolicyBase
{
    public abstract SlotSelection Select(SlotAcceptanceCandidates candidates, InventoryAcceptanceRequest request);
}

// Первый по индексу; если конкретных нет, но CanCreateNewSlot => New(). Иначе None.
[Serializable] public sealed class FirstSlotSelectionPolicy : SlotSelectionPolicyBase { ... }

// Непустые кандидаты (по индексу) → затем пустые (по индексу); если нет ни одного, но CanCreateNewSlot => New().
// Воспроизводит текущий stack-first у Stackable.
[Serializable] public sealed class StackFirstSlotSelectionPolicy : SlotSelectionPolicyBase { ... }
```

### 2.2 Контракт стратегии

`IAcceptanceStrategy` (`Strategies/IAcceptanceStrategy.cs`):
- **Добавить** `SlotAcceptanceCandidates GetSlotCandidates(List<BaseSlot> slots, InventoryAcceptanceRequest request, bool canCreateNewSlot, int potentialNewSlots, BaseSlot baseSlotPrefab);`
- **Добавить** `SlotSelectionPolicyBase DefaultSlotSelectionPolicy { get; }` — дефолт-предпочтение стратегии (используется когда `request.SelectionPolicy == null`: программные вызовы и сохранение текущего поведения). Политики stateless → возвращать общий `static readonly` инстанс (ноль аллокаций), напр. `private static readonly SlotSelectionPolicyBase _default = new StackFirstSlotSelectionPolicy();`. Сами `FirstSlotSelectionPolicy`/`StackFirstSlotSelectionPolicy` тоже стоит держать `static readonly`-синглтонами для переиспользования.
- **Убрать** `CanAcceptItem(... out suggestedBaseSlot)`. `GetAcceptableCount` — без изменений.

`InventoryStrategyBase` (`:43`): убрать abstract `CanAcceptItem`, добавить abstract `GetSlotCandidates` и `DefaultSlotSelectionPolicy`.

`DynamicSlotDecorator` (`:173`): делегировать `GetSlotCandidates` и `DefaultSlotSelectionPolicy` внутреннему `_acceptanceStrategy`.

### 2.3 `GetSlotCandidates` по стратегиям (eligibility = текущая) + дефолт-политика

Кандидаты собираются в порядке индекса; правила те же, что сейчас.

**ОБЯЗАТЕЛЬНОЕ общее правило — исключать исходный слот при area-drop в тот же инвентарь.** Иначе area-drop внутри
одного инвентаря может выбрать исходный слот как matching stack (особенно Stackable + `StackFirstSlotSelectionPolicy`).
В цикле сбора кандидатов (во ВСЕХ трёх стратегиях):
```csharp
if (ReferenceEquals(request.SourceInventory, request.TargetInventory) &&
    ReferenceEquals(slot, request.SourceBaseSlot))
    continue;
```
(`request.SourceInventory`/`request.SourceBaseSlot` уже доступны на `InventoryAcceptanceRequest`, см.
`InventoryAcceptanceRequest.cs:32-33`.) Покрыть тестом (см. §2.6).

- **UniqueItemStrategy** — `DefaultSlotSelectionPolicy = FirstSlotSelectionPolicy`:
  - `Slots` = пустые, прошедшие `PassesRules(slot,item,1,request)`, `RemainingCapacity=1`, `IsEmpty=true`.
  - `CanCreateNewSlot = canCreateNewSlot && potentialNewSlots>0 && PrefabPassesRules(slots,prefab,item,1,request)`.

- **StackableItemStrategy** — `DefaultSlotSelectionPolicy = StackFirstSlotSelectionPolicy` (сохраняет stack-first):
  - `maxSize = GetMaxStackSize(item, DefaultMaxStackSize, AllowItemStackOverride)`.
  - `Slots` (в порядке индекса): непустые с `CanStack(item) && canFit>0 && PassesRules(...,Min(desired,canFit))` (`RemainingCapacity=canFit`, `IsEmpty=false`) **и** пустые с `PassesRules(...,Min(desired,maxSize))` (`RemainingCapacity=maxSize`, `IsEmpty=true`).
  - `CanCreateNewSlot = canCreateNewSlot && PrefabPassesRules(slots,prefab,item,Min(desired,maxSize),request)`.
  - Поведение сохраняется: StackFirst берёт первый непустой-с-местом, иначе первый пустой, иначе New().

- **SeparableStacksStrategy** — `DefaultSlotSelectionPolicy = FirstSlotSelectionPolicy` (позиционный):
  - как Stackable по составу `Slots`, но дефолт First = первый годный по индексу (воспроизводит текущее).
  - `CanCreateNewSlot = canCreateNewSlot && PrefabPassesRules(slots,prefab,item,Min(desired,maxSize),request)`.

> Зачем разные дефолты: текущее поведение различается (Stackable stack-first, остальные позиционные). Капабилити-флаг
> `CanCreateNewSlot` теперь чистый (не загейчен на пустоту списка), но дефолт-политики берут New() **только когда
> конкретных кандидатов нет** → наблюдаемое поведение идентично текущему. Кастомная политика сможет переупорядочить
> empty/stack через `candidate.IsEmpty`/`RemainingCapacity`.

### 2.4 Применение политики и сохранение intent

Ключевой принцип: **не терять `SlotSelection`**. `CreateNew` обязан дойти до `InventoryDropProcessor` (исполнится в
Фазе 2). Нельзя сводить выбор к одному `out BaseSlot`.

- **`InventoryAcceptanceRequest`** (`Scripts/Inventories/InventoryAcceptanceRequest.cs`): добавить необязательное
  `SlotSelectionPolicyBase SelectionPolicy { get; }` + параметр ctor (default `null`).
- **`UniversalInventory`**: добавить метод, отдающий полный результат:
  ```csharp
  public bool TrySelectDropSlot(InventoryAcceptanceRequest request, out SlotSelection selection)
  {
      // EnsureStrategyInitialized + CanAcceptShape как в CanAcceptItem
      var candidates = _acceptanceStrategy.GetSlotCandidates(_slots, request, canCreateNewSlot, potentialNewSlots, baseSlotPrefab);
      var policy = request.SelectionPolicy ?? _acceptanceStrategy.DefaultSlotSelectionPolicy;
      selection = policy.Select(candidates, request);
      return selection.Accepted;
  }
  ```
  `CanAcceptItem(request, out suggestedBaseSlot)` (`:1445`) делегирует: `var ok = TrySelectDropSlot(request, out var sel); suggestedBaseSlot = sel.Slot; return ok;` — сохраняем для обратной совместимости. Overload
  `CanAcceptItem(IItemAdapter,int,out slot)` (`:1439`) не трогаем. Лог success/reject сохранить.
  (Объявить `TrySelectDropSlot` в `BaseInventory`/интерфейсе инвентаря, чтобы `InventoryDropArea` мог звать через `IInventory`.)
- **`InventoryDropArea`** (`Scripts/UI/InventoryDropArea.cs`):
  - Поле политики (мимикрия под `DropPolicySettings.cs:10-11`):
    ```csharp
    [Header("Slot Selection")]
    [SerializeReference, ManagedReferencePicker, InlineProperty, HideLabel,
     Tooltip("Как выбрать слот при наведении на область (нет конкретного слота под курсором).")]
    private SlotSelectionPolicyBase _slotSelectionPolicy; // null => дефолт стратегии
    ```
  - Хранить **весь `SlotSelection`**, а не только слот: заменить `private BaseSlot _foundBaseSlot;` на
    `private SlotSelection _foundSelection;` (или добавить `private bool _forceCreateNewSlot;`).
    `GetTargetSlot()` → `_foundSelection.Slot`; `OnTargetDeactivated` сбрасывает `_foundSelection = default`.
  - В `TryBuildValidationContext` (`:164`) передать `_slotSelectionPolicy` в `SelectionPolicy` запроса и через
    `inventory.TrySelectDropSlot(request, out _foundSelection)` получить полный результат (вместо `CanAcceptItem(out slot)`).
  - `CreateDropProcessor(...)` должен передавать `forceCreateNewSlot: _foundSelection.CreateNew` в процессор.
- **`InventoryDropProcessor`** (`Scripts/Inventories/InventoryDropProcessor.cs`): добавить ctor-параметр
  `bool forceCreateNewSlot = false` и сохранить в поле. **Фаза 1:** только хранит (планировщик пока игнорирует).
  **Фаза 2:** прокидывает в `TransferPlanner.BuildPlan` и исполняет (см. §3).

> Так intent-путь готов уже в Фазе 1 (selection → область → процессор), а исполнение добавляется точечно в Фазе 2,
> не переписывая снова UI-слой.

### 2.5 Чеклист файлов Фазы 1

- [ ] `Scripts/Inventories/SlotAcceptanceCandidate.cs` (new) — struct
- [ ] `Scripts/Inventories/SlotAcceptanceCandidates.cs` (new)
- [ ] `Scripts/Inventories/SlotSelection.cs` (new)
- [ ] `Scripts/Inventories/SlotSelectionPolicyBase.cs` (new; + `FirstSlotSelectionPolicy`, `StackFirstSlotSelectionPolicy` — здесь же или рядом)
- [ ] `Scripts/Inventories/InventoryAcceptanceRequest.cs` (+ SelectionPolicy)
- [ ] `Scripts/Inventories/Strategies/IAcceptanceStrategy.cs` (контракт)
- [ ] `Scripts/Inventories/Strategies/InventoryStrategyBase.cs` (контракт)
- [ ] `Scripts/Inventories/Strategies/UniqueItemStrategy.cs`
- [ ] `Scripts/Inventories/Strategies/StackableItemStrategy.cs`
- [ ] `Scripts/Inventories/Strategies/SeparableStacksStrategy.cs`
- [ ] `Scripts/Inventories/Strategies/DynamicSlotDecorator.cs`
- [ ] `Scripts/Inventories/UniversalInventory.cs` (+ `TrySelectDropSlot`, `CanAcceptItem` делегирует)
- [ ] `Scripts/Inventories/IInventory.cs` / `BaseInventory.cs` (объявить `TrySelectDropSlot`)
- [ ] `Scripts/Inventories/InventoryDropProcessor.cs` (+ `forceCreateNewSlot` ctor-параметр, хранение)
- [ ] `Scripts/UI/InventoryDropArea.cs` (поле политики, хранить `_foundSelection`, проброс `forceCreateNewSlot`)

### 2.6 Проверка Фазы 1

- Компиляция Unity без ошибок; других вызовов стратегического `CanAcceptItem` нет (только `:1458`, `:175`).
- Дефолт-политики воспроизводят текущее поведение: Unique/SeparableStacks — позиционно, Stackable — stack-first.
- Демо `Examples/Demo1 Inventories/InventoriesDemo.unity`: дроп на `InventoryDropArea` для трёх стратегий ведёт себя как до рефактора.
- **Тест исключения исходного слота:** area-drop в тот же инвентарь (Stackable + `StackFirstSlotSelectionPolicy`),
  где исходный слот содержит тот же предмет, — кандидат НЕ должен указывать на `request.SourceBaseSlot` (предмет не
  должен «выбираться сам в себя»). Проверить, что выбран другой стек/пустой/новый, а не источник.
- Кастомная `LastSlotSelectionPolicy` (для ручной проверки): выбирает последний по индексу → планировщик кладёт туда.

---

## 3. Фаза 2 — forced-new intent (после Фазы 1)

**Зачем (узко):** дать политике выбрать «создать НОВЫЙ слот, даже если есть годные существующие». Реально нужно лишь
для `SeparableStacks` + политика «новый стек» при `maxFreeSlots = 0`. При `maxFreeSlots > 0` буфер пустых уже даёт
эффект через выбор конкретного пустого слота (Фаза 1).

**Механика (что менять):**
1. Intent уже доходит до `InventoryDropProcessor.forceCreateNewSlot` (плюмбинг сделан в Фазе 1, §2.4). Фаза 2 = исполнить его в planner+executor.
2. Протащить флаг дальше: `InventoryDropProcessor` (хранимый `forceCreateNewSlot`) → `TransferPlanner.BuildPlan` (новый параметр) → `PlanEntry`.
3. `PlanEntry`: если forceNewSlot — пропустить раскладку в существующие, вернуть `PlannedEntryTransfer` с новым флагом `RequiresNewSlot` + `PlannedSlotAllocation(null, amount)`.
4. `TransferPlanExecutor`: в ветке null-allocation (`TryAddToTargetInventory`, `:1004`) при `RequiresNewSlot` — НЕ `TryAddStack(-1)` (он переиспользует первый пустой), а создать слот через новую capability и `TryAddToSlot(stack, newSlot)`.
5. Новая capability на инвентаре: расширить `IDynamicSlotLifecycle` (`InventoryRuntimeCapabilities.cs:30`) методом `bool TryCreateSlot(out BaseSlot newSlot)`, открывающим приватный `UniversalInventory.CreateSlot()` (`:366`), с проверкой `_slotManagementSettings.CanCreateNewSlot`.
6. `GetAcceptableCount` уже учитывает `potentialNewSlots` — accept не сломается.

**Риски:** откат созданного слота при неудаче размещения (Atomic — снапшот восстанавливает кол-во слотов, `UniversalInventory.cs:1100-1115`; BestEffort — нужно убедиться, что пустой созданный слот уберётся через `HandleSlotEmptied`/trim). Не задеть «unresolved-target guard» (`TransferPlanExecutor.cs:886-925`) — он про targetSlot==null с резолвом в источник; forced-new даёт конкретный новый слот, резолв ≠ источник, guard проходит.

---

## 4. Фаза 3 — one-per-ID (отдельный проект, после Фаз 1–2)

**Решение:** не переопределять `StackableItemStrategy` (она задокументирована как мульти-слотовая «one item type can
occupy multiple slots»), а сделать **новый класс стратегии** (рабочее имя `SingleSlotStackStrategy`): один слот на вид
предмета (ID). Для нескольких стеков одного предмета остаётся `SeparableStacksStrategy`.

**Семантика:** предмет уже есть → единственный целевой слот = его слот (если есть место), иначе отказ; новый слот
запрещён. Предмета нет → пустые слоты / создать новый.

**Точки консистентности (всё должно согласоваться, иначе ручной дроп/alternative разойдутся с hover):**
- `GetSlotCandidates` (как выше).
- `GetAcceptableCount`: предмет есть → только остаток его слота; предмета нет → пустые + новые.
- `TryAdd` (ветка `else`, льёт в существующие потом в пустые) — не переполнять во второй слот.
- `TryAddToSlot`.
- **Главная трудность — `CanUseAlternativeSlot(BaseSlot, IItemAdapter)`** (`StackableItemStrategy.cs:160`): сигнатура
  послотовая, **не видит остального инвентаря**, поэтому не может выразить «пустой нельзя, если предмет уже в другом
  слоте». Её использует планировщик в alternative-поиске (`AllocateForStrategyInventory`/blocked-resolver,
  `TransferPlanner.cs:999-1040`). Консистентный one-per-ID требует **прокинуть состояние инвентаря** в этот чек —
  меняется `CanUseAlternativeSlot`, `BlockedTargetResolutionContext`, `FindAlternativeBlockedTargetResolver`. Это и есть
  основная стоимость/риск Фазы 3.

---

## 5. Зафиксированные решения (history)

1. Один плоский список кандидатов (не два списка empty/stack); политика классифицирует по `candidate.IsEmpty`. Богатый кандидат-объект (`RemainingCapacity`/`IsEmpty`), чтобы не пересчитывать.
2. Политика — полиморфный `SlotSelectionPolicyBase` (не enum). Живёт на инициаторе (`InventoryDropArea`), без поля в `DropPolicySettings`; fallback — `DefaultSlotSelectionPolicy` стратегии.
3. `CanCreateNewSlot` — чистая capability (Dynamic `count<maxSlots` + `PrefabPassesRules` + домен), порядок относительно empty/stack решает политика.
4. `BlockedTargetResolver` не трогаем: SlotSelectionPolicy — фаза hover (до дропа, нет конкретного слота); BlockedTargetResolver — после отклонённой попытки дропа в слот.
5. Фаза 1 — ноль регрессий: дефолт-политики воспроизводят текущее поведение (Stackable stack-first, остальные позиционно). Forced-new и one-per-ID — Фазы 2 и 3.
6. one-per-ID — новый класс стратегии, не переопределение Stackable.
7. `SlotSelection.New()` — **сильная семантика** (force create new slot) уже в контракте; intent протаскивается до `InventoryDropProcessor` в Фазе 1, исполняется в Фазе 2. Не схлопывать в «нет подсказки».
8. **Release gate = Фаза 1 + Фаза 2** (Фаза 1 в одиночку не релизится как «кастомные политики выбора»).
9. **Исключать исходный слот** при area-drop в тот же инвентарь (общее правило `GetSlotCandidates` + тест).
10. `DefaultSlotSelectionPolicy` и сами политики — `static readonly`-синглтоны (stateless, ноль аллокаций).
11. `ClosestToCursor` — лишь пример кастомной политики, не входит в план поставки.
