# План: SlotSelectionPolicy — единый механизм выбора слота

> Самодостаточный план. Реализацию можно вести, имея в контексте **только этот файл** + перечисленные исходники.
> Статус: **согласован, единая модель, режем на коммиты.** Релиз один — после всех коммитов; промежуточных
> релизов нет, но каждый коммит **компилируется** и (по умолчанию) сохраняет наблюдаемое поведение для
> single-slot дропа.

---

## 0. Модель (TL;DR)

**Область применения.** Механизм вводится для случаев, когда **слот выбирает не игрок вручную**, а система: дроп в
**область** инвентаря (надо просто добавить предмет), **автоматический перенос** (auto-transfer), batch и overflow.
Когда игрок дропнул в **конкретный слот** — это остаётся explicit: первая попытка строго в этот слот, SelectionPolicy
его не подменяет; если слот заблокирован — включается `BlockedTargetResolver` (§2.4, §2.6).

Выбор слота (в этих «системных» случаях) — **один механизм** на всех путях:

- **Стратегия = eligibility.** `GetSlotCandidates(slots, request, ...)` отдаёт классифицированных кандидатов
  (+ capability «создать новый»). Работает на **виртуальном** состоянии (`VirtualSlotState`): на границе UI реальные
  слоты оборачиваются в свежие `VirtualSlotState`, в планировщике — те же, что заполняются по ходу аллокации.
- **`SlotSelectionPolicy` = selection.** Полиморфный класс. Выбирает один слот (или «создать новый») из кандидатов.
  **Планировщик зовёт политику на каждом шаге аллокации** и наполняет ею виртуальные слоты → этим автоматически
  покрываются single-drop, **batch** и overflow (распределение большого стека по нескольким слотам).
- **`BlockedTargetResolver` сужается** до своей настоящей роли: срабатывает только при попытке дропа в
  **конкретный заблокированный слот** (занят другим предметом / отклонён правилами) → альтернатива / swap / reject.
  Он больше **не** общий распределитель.

Приятное следствие: при таком устройстве **one-per-ID и forced-new упрощаются** — стратегия видит полное
виртуальное состояние в `GetSlotCandidates`, поэтому может сама не предлагать второй слот для того же ID, а
forced-new выражается как синтетический кандидат «новый слот». Послотовый `CanUseAlternativeSlot` для распределения
больше не нужен (остаётся только в blocked-resolver).

---

## 1. Текущее устройство (проверено по коду; reference)

Цепочка hover-дропа на область:
```
InventoryDropArea.TryBuildValidationContext            Scripts/UI/InventoryDropArea.cs:147
  → (только если !IsBatchDrag) _inventory.CanAcceptItem(request, out suggestedBaseSlot)   :155-176, :171
      → UniversalInventory.CanAcceptItem(...)           Scripts/Inventories/UniversalInventory.cs:1445
          → _acceptanceStrategy.CanAcceptItem(_slots, request, canCreateNewSlot, potentialNewSlots, prefab, out suggested)  :1458
  → _foundBaseSlot = suggestedBaseSlot                  InventoryDropArea.cs:74
  → CreateDropProcessor(_foundBaseSlot) → _targetBaseSlot → TransferPlanner.BuildPlan(..., targetBaseSlotHint, ...)
```

Ключевые факты:
- **batch не выбирает слот**: при `IsBatchDrag` `CanAcceptItem` не зовётся, `suggestedBaseSlot == null`
  (`InventoryDropArea.cs:155`). Приёмку batch валидирует только планировщик (`CanAcceptDrop`→`BuildPlan`) с null-hint.
- В планировщике hint влияет **только на первую запись**: `preferHint = targetBaseSlotHint != null && isFirstEntry`
  (`TransferPlanner.cs:402`, ветка `:354`). Записи 2..N и overflow распределяются `AllocateForStrategyInventory`
  (`:742`) через **blocked-resolver** (`EnumerateAlternativeVirtualSlots:1043` → `ResolveBlockedTarget:984` →
  `EmptyFirstAlternativePlacementStrategy`).
- Создание нового слота — только deferred-fallback: `PlannedSlotAllocation(null, amount)` при пустом инвентаре
  (`:411`) или `plannedAmount==0 && CanUseDeferredPlacement && !HasCurrentSlotPlacementCapacity` (`:445-448`);
  исполняется `TryAddStack(-1)`, который переиспользует **первый пустой** слот (executor
  `TransferPlanExecutor.cs:894-906`, `TryAddToTargetInventory:1004`).
- `VirtualSlotState` (`Scripts/Inventories/VirtualSlotState.cs`) — `internal sealed`: `BaseSlot`, `IsEmpty`,
  `ItemAdapter`, `Count`, `CanAccept(item, uniqueMode)`, `Apply(item, amount)`, `MarkEmpty()`. Строится
  `BuildVirtualSlots` (`TransferPlanner.cs:1102`).
- Буфер свободных слотов: `DynamicSlotManagementSettings` (`Strategies/DynamicSlotManagementSettings.cs`):
  `_maxFreeSlots>0` держит реальные пустые слоты (`EnsureFreeSlots:37`); `_maxFreeSlots=0` — слот создаётся только
  on-demand в `TryAddStack`. `CanCreateNewSlot(count)=count<_maxSlots`; `GetPotentialNewSlots=max(0,_maxSlots-count)`.
  Слот создаёт приватный `UniversalInventory.CreateSlot()` (`:366`).

Текущая eligibility/preference (что сохраняем по умолчанию):
- **Unique** (`Strategies/UniqueItemStrategy.cs:105`): первый пустой по индексу; иначе `canCreate && potential>0 && PrefabPassesRules(...,1)`.
- **Stackable** (`Strategies/StackableItemStrategy.cs:175`): **stack-first** (первый непустой с местом по индексу), иначе первый пустой, иначе new. Дока: «one item type **can occupy multiple slots**», авто-merge.
- **SeparableStacks** (`Strategies/SeparableStacksStrategy.cs:192`): **позиционно** (первый годный по индексу), иначе new.

Хелперы `InventoryStrategyBase`: `PassesRules` (:159), `PrefabPassesRules` (:192), `FindSlotWithItem` (:180),
`GetMaxStackSize(item,defaultMax,allowOverride)` static (:123).

---

## 2. Целевая архитектура

### 2.1 Виртуальное состояние слота — `VirtualSlotState` (без новых типов)

`GetSlotCandidates` должен читать **виртуальное** (гипотетическое) состояние, а не реальный `BaseSlot` (тот не меняется
до исполнения; иначе overflow/batch выберут уже занятый слот повторно). Это состояние уже несёт существующий
`VirtualSlotState` (`Scripts/Inventories/VirtualSlotState.cs`) — поля `BaseSlot/IsEmpty/ItemAdapter/Count` те же,
что в `BaseSlot.Stack`, но отражают ход аллокации. Отдельный интерфейс не нужен.

Изменение: сделать `VirtualSlotState` **public** с публичными read-полями; мутацию (`Apply`/`MarkEmpty`) и ctor
оставить `internal` (доступны только внутри сборки `UDND.Inventories`: планировщик/инвентарь). Тогда публичный
`IAcceptanceStrategy.GetSlotCandidates` может принимать `IReadOnlyList<VirtualSlotState>`. На границе UI реальные
`_slots` оборачиваются в свежие `VirtualSlotState` (как `BuildVirtualSlots`, `TransferPlanner.cs:1102`) — там
виртуального заполнения нет, просто снапшот. Стратегия только читает; `Apply` зовёт планировщик после выбора.

### 2.2 Стратегия = eligibility

`IAcceptanceStrategy`:
- **Добавить** `SlotAcceptanceCandidates GetSlotCandidates(IReadOnlyList<VirtualSlotState> slots, InventoryAcceptanceRequest request, bool canCreateNewSlot, int potentialNewSlots, BaseSlot baseSlotPrefab);`
- **Добавить** `SlotSelectionPolicyBase DefaultSlotSelectionPolicy { get; }` (для `request.SelectionPolicy == null` и программных вызовов; сохраняет текущее поведение).
- **Убрать** `CanAcceptItem(... out suggestedBaseSlot)`. `GetAcceptableCount` — без изменений.

Правила (`PassesRules`/`PrefabPassesRules`) — внутри `GetSlotCandidates`, как сейчас. **Общее обязательное правило:**
исключать исходный слот при area-drop в тот же инвентарь:
```csharp
if (ReferenceEquals(request.SourceInventory, request.TargetInventory) &&
    ReferenceEquals(view.BaseSlot, request.SourceBaseSlot))
    continue;
```

Per-strategy `GetSlotCandidates`. Дефолт-политика везде `FirstSlotSelectionPolicy` (первый по индексу):
- **Unique** (без изменений): `Slots` = пустые (rules,1), `RemainingCapacity=1`; `CanCreateNewSlot = canCreate && potential>0 && PrefabPassesRules(...,1)`.
- **Stackable = one-per-ID** (НАМЕРЕННАЯ смена поведения, см. §2.8): один слот на вид предмета.
  - предмет уже есть в инвентаре (есть `view` с `CanStack(item)`): `Slots` = [тот слот, если `canFit>0` и rules], `RemainingCapacity=canFit`; пустые НЕ предлагаются; `CanCreateNewSlot=false`.
  - предмета нет: `Slots` = пустые (rules), `RemainingCapacity=maxSize`; `CanCreateNewSlot = canCreate && PrefabPassesRules(...,Min(desired,maxSize))`.
  - (списки никогда не смешаны → выбор тривиален, `First` достаточно.)
- **SeparableStacks** (без изменений): `Slots` = непустые с `CanStack && canFit>0` (rules) + пустые (rules), в порядке индекса; `CanCreateNewSlot = canCreate && PrefabPassesRules(...,Min(desired,maxSize))`. Дефолт First = первый годный по индексу.

> `CanCreateNewSlot` — чистая capability (не загейчена на пустоту списка). Дефолт `First` берёт New() только когда
> конкретных кандидатов нет. Поведение Unique/SeparableStacks идентично текущему; **Stackable меняется намеренно**.
> `StackFirstSlotSelectionPolicy` остаётся как **опциональная** shipped-политика (для SeparableStacks — «сначала
> доложить в существующий стек»), но **дефолтом нигде не является** (это и есть исходный запрос «выбирать слот с тем же видом предмета»).

### 2.3 Политика = selection

```csharp
[Serializable]
public abstract class SlotSelectionPolicyBase
{
    // Выбрать один слот ИЛИ "создать новый" из кандидатов. Вызывается планировщиком на каждом шаге аллокации.
    public abstract SlotSelection Select(SlotAcceptanceCandidates candidates, InventoryAcceptanceRequest request);
}
[Serializable] public sealed class FirstSlotSelectionPolicy : SlotSelectionPolicyBase { /* первый по Index */ }
[Serializable] public sealed class StackFirstSlotSelectionPolicy : SlotSelectionPolicyBase { /* непустые по Index, затем пустые */ }
```
Политики stateless → `static readonly`-синглтоны (ноль аллокаций); `DefaultSlotSelectionPolicy` возвращает их.

### 2.4 Планировщик — единый цикл аллокации через политику

**Explicit drop остаётся explicit.** Если пользователь дропнул в конкретный слот (`targetBaseSlotHint != null`),
**первая** попытка размещения — именно в этот слот. SelectionPolicy НЕ перекидывает explicit-дроп в другой слот
(иначе, напр., `RandomSlotSelectionPolicy` сломает ручной дроп). Если hinted-слот заблокирован (занят другим
предметом / отклонён правилами) — включается `BlockedTargetResolver` (alt/swap/reject, §2.6). Policy-driven цикл
применяется к **area-drop** (нет hint) и к **overflow** (остаток после заполнения explicit-слота).

Цикл распределения (заменяет blocked-resolver-ordering из `EnumerateAlternativeVirtualSlots`):
- Подготовка: `remaining = amount`; `remainingPotentialNewSlots = potentialNewSlots`; `maxStackSize` от стратегии.
- Если есть незаблокированный explicit hint — сначала аллокация в него, `remaining -= placed`.
- Пока `remaining > 0`:
  - взять текущие `VirtualSlotState` (их и наполняем по ходу);
  - `candidates = GetSlotCandidates(views, request, canCreateNewSlot && remainingPotentialNewSlots > 0, remainingPotentialNewSlots, prefab)`;
  - `selection = policy.Select(candidates, request)`;
  - **`Existing(slot)`**: `cap = slot.IsEmpty ? maxStackSize : (maxStackSize - count)`; `place = min(remaining, cap)`; `virtual.Apply(item, place)`; `allocations += PlannedSlotAllocation(slot, place)`; `remaining -= place`;
  - **`New()`**: `place = min(remaining, maxStackSize)` — **новый слот имеет capacity = max stack, не «бесконечная дырка»**; `allocations += PlannedSlotAllocation(null, place){ RequiresNewSlot = true }`; `remainingPotentialNewSlots--`; (виртуально учесть новый слот заполненным на `place`);
  - **`None`**: стоп.
- **Виртуальное состояние И `remainingPotentialNewSlots` переносятся между записями batch** — нельзя запланировать больше новых слотов, чем dynamic-инвентарь реально создаст (`potentialNewSlots = max(0, maxSlots - count)`).
- Политика из плана (см. 2.5); `null` → `DefaultSlotSelectionPolicy` стратегии.

### 2.5 Проводка политики (intent не теряется)

- `InventoryAcceptanceRequest`: + необязательное `SlotSelectionPolicyBase SelectionPolicy`.
- `InventoryDropArea`: + `[SerializeReference, ManagedReferencePicker, InlineProperty, HideLabel] SlotSelectionPolicyBase _slotSelectionPolicy;` (null → дефолт стратегии). Передаёт политику в processor; **batch больше не пропускает выбор** (снять гейт `!IsBatchDrag`, см. C5).
- `InventoryDropProcessor`: + ctor-параметр `SlotSelectionPolicyBase selectionPolicy` (и для forced-new — поведение через политику, отдельный bool не нужен). Прокидывает в `TransferPlanner.BuildPlan`.
- `TransferPlanner.BuildPlan`: + параметр политики, использует в 2.4.
- **`AutoTransferService`** (`Scripts/Inventories/AutoTransferService.cs:115`): строит `InventoryDropProcessor` с `targetBaseSlot: null` (`:130`) и идёт через тот же планировщик → передать `selectionPolicy` в ctor процессора. Добавить необязательный параметр `SlotSelectionPolicyBase` в `ExecuteAsync` (рядом с `requestedPolicy`); `null` → `DefaultSlotSelectionPolicy` стратегии. Это и есть точка проводки для **авто-переноса**.
- `SlotSelection` — сильная семантика `New()` = «force create new slot» (не «нет подсказки»):
  ```csharp
  public readonly struct SlotSelection {
      public bool Accepted; public BaseSlot Slot; public bool CreateNew;
      public static SlotSelection None => new(false,null,false);
      public static SlotSelection Existing(BaseSlot s) => new(true,s,false);
      public static SlotSelection New() => new(true,null,true);
  }
  ```
- `UniversalInventory.CanAcceptItem(request, out suggested)` — оставить для обратной совместимости: обернуть `_slots`
  в `VirtualSlotState` → `GetSlotCandidates` → дефолт/`request.SelectionPolicy` → `selection.Slot`. (Главный путь выбора
  теперь в планировщике; этот метод — для accept-проверки/превью и внешних вызовов.)

### 2.6 blocked-resolver — сузить роль

Оставить вызов blocked-resolver **только** при попытке дропа в конкретный слот, который заблокирован
(занят другим предметом / отклонён правилами): альтернатива / swap / reject. Убрать его из общего распределения
(его заменяет 2.4). Swap-резолв (`TryPlanSwap`, occupied-target) сохраняется в blocked-resolver.

### 2.7 forced-new (исполнение)

- Флаг **на уровне allocation, не entry**: `PlannedSlotAllocation.RequiresNewSlot` (новое поле). Одна entry может
  содержать аллокации и в существующие слоты, и в один/несколько новых — forced-new не смешивается с обычным
  deferred/null. Признак forced-new = `BaseSlot == null && RequiresNewSlot`; обычный deferred = `BaseSlot == null && !RequiresNewSlot`.
- Executor: в null-allocation ветке (`TransferPlanExecutor.cs:1004`) при `allocation.RequiresNewSlot` — НЕ `TryAddStack(-1)`
  (переиспользует первый пустой), а создать слот и положить в него: новая capability
  `IDynamicSlotLifecycle.TryCreateSlot(out BaseSlot newSlot)` (`InventoryRuntimeCapabilities.cs:30`), открывающая
  приватный `UniversalInventory.CreateSlot()` (`:366`) с проверкой `_slotManagementSettings.CanCreateNewSlot`, затем
  `TryAddToSlot(stack, newSlot)`. Amount allocation уже ограничен `maxStackSize` на этапе планирования (§2.4).
- Откат: Atomic — снапшот восстанавливает кол-во слотов (`UniversalInventory.cs:1100-1115`); BestEffort —
  пустой созданный слот убирается через `HandleSlotEmptied`/trim. Не задеть «unresolved-target guard» (`:886-925`).

### 2.8 one-per-ID — это сам `StackableItemStrategy` (без новых классов)

`StackableItemStrategy` **переопределяется** в one-per-ID (один слот на вид предмета). Никаких новых классов; для
нескольких стеков одного предмета по-прежнему `SeparableStacksStrategy`. Это **намеренная смена поведения**: текущая
дока класса «one item type can occupy multiple slots» + авто-merge с overflow — **устаревает, обновить**.

Enforce — под единой моделью делается **прямо в `GetSlotCandidates`** (стратегия видит весь `VirtualSlotState`-список,
в т.ч. виртуальные размещения предыдущих записей batch): предмет уже есть → кандидаты = только тот слот (если есть
место), `CanCreateNewSlot=false`; предмета нет → пустые + new. Поскольку распределение теперь спрашивает
`GetSlotCandidates` на каждом шаге, послотовый `CanUseAlternativeSlot` менять **не нужно** (остаётся только для
blocked-resolver).

Для полной консистентности привести к one-per-ID и прямые placement-методы Stackable (используются вне планировщика —
`TryAddStack`→`TryAdd`, swap/executor→`TryAddToSlot`, программные вызовы):
- `GetAcceptableCount`: предмет есть → только остаток его слота; предмета нет → пустые + новые.
- `TryAdd` (ветка `targetIndex<0`, `StackableItemStrategy.cs:58-101` — сейчас льёт в существующие, потом в пустые): не переполнять во второй слот того же предмета.
- `TryAddToSlot`.

---

## 3. Ключевые типы (сводка)

- `VirtualSlotState` → сделать **public** (read-поля public; `Apply`/`MarkEmpty`/ctor — `internal`).
- `SlotAcceptanceCandidate` (struct): `BaseSlot Slot; int RemainingCapacity; bool IsEmpty;`
- `SlotAcceptanceCandidates`: `IReadOnlyList<SlotAcceptanceCandidate> Slots; bool CanCreateNewSlot; int PotentialNewSlots; bool HasAny;`
- `SlotSelection` (struct): `bool Accepted; BaseSlot Slot; bool CreateNew;` (см. 2.5)
- `SlotSelectionPolicyBase` + `FirstSlotSelectionPolicy`, `StackFirstSlotSelectionPolicy`.
- `IDynamicSlotLifecycle.TryCreateSlot(out BaseSlot)` (расширение).
- `PlannedSlotAllocation.RequiresNewSlot` (новый флаг — на уровне allocation, не entry).

---

## 4. Разбивка по коммитам

Каждый коммит компилируется. Инвариант «зелёного дерева»: дефолт `First` сохраняет single-slot поведение для
**Unique и SeparableStacks**; **Stackable намеренно меняется на one-per-ID** (см. §2.8). Распределение/batch ordering
становится policy-driven (нормализуется) — валидировать по демо.

- **C1 — типы.** `SlotAcceptanceCandidate(s)`, `SlotSelection`, `SlotSelectionPolicyBase` + `First`/`StackFirst`. `VirtualSlotState` → public (read public, мутация internal). Чистые добавления.
- **C2 — eligibility.** В `IAcceptanceStrategy`/`InventoryStrategyBase`/3 стратегии/`DynamicSlotDecorator`: `GetSlotCandidates(IReadOnlyList<VirtualSlotState>...)` + `DefaultSlotSelectionPolicy = First`; убрать стратегический `CanAcceptItem`. Source-exclusion. Unique/SeparableStacks — eligibility как сейчас; **Stackable.GetSlotCandidates сразу one-per-ID** (§2.2/§2.8). `UniversalInventory.CanAcceptItem` переписан на новый путь (обёртка `_slots`→`VirtualSlotState`). Build green. (До C8 placement-методы Stackable ещё мульти-слотовые — окно несогласованности, релиз атомарный.)
- **C3 — проводка политики (без смены поведения).** `InventoryAcceptanceRequest.SelectionPolicy`; `InventoryDropArea` поле + хранит `SlotSelection`; `InventoryDropProcessor` + параметр политики; `AutoTransferService.ExecuteAsync` + параметр политики (точка авто-переноса); `BuildPlan` + параметр (пока планировщик использует дефолт внутри → поведение не меняется). Green.
- **C4 — единый цикл аллокации (single + overflow).** Планировщик: распределение через `GetSlotCandidates(virtual)` + политика (2.4) вместо blocked-resolver-ordering. Дефолт воспроизводит текущее single-slot; multi-slot ordering нормализуется — сверить с демо.
- **C5 — batch.** Снять гейт `!IsBatchDrag` в `InventoryDropArea`; per-entry политика + перенос виртуального состояния между записями (из C4 почти бесплатно). Валидировать batch-дроп.
- **C6 — сузить blocked-resolver.** Только explicit-blocked-target (occupied/rules) → alt/swap/reject; убрать из общего распределения. Проверить swap.
- **C7 — forced-new.** `SlotSelection.New()` → синтетический кандидат → `RequiresNewSlot` → executor `TryCreateSlot`+`TryAddToSlot`; `IDynamicSlotLifecycle.TryCreateSlot`. Откат (2.7).
- **C8 — Stackable one-per-ID: placement-консистентность.** Привести к one-per-ID прямые методы Stackable: `GetAcceptableCount`, `TryAdd` (`StackableItemStrategy.cs:58-101`), `TryAddToSlot` (2.8). Обновить доку класса (была «multiple slots»). `GetSlotCandidates` уже one-per-ID (C2).
- **C9 — тесты/демо/доки.** Тест source-exclusion; batch; forced-new при `maxFreeSlots=0`; one-per-ID (предмет есть → второй слот не создаётся). Демо `Examples/Demo1 Inventories/InventoriesDemo.unity`. Обновить скиллы (`dragdrop-architecture` и др.).

Релиз — после C9.

---

## 5. Риски / заметки

- **Нормализация ordering при распределении.** Сейчас multi-slot распределение идёт через
  `EmptyFirstAlternativePlacementStrategy` (empty-first), а single-hint у Stackable — stack-first. Унификация делает
  ordering policy-driven; в части multi-slot краёв поведение может слегка измениться. Сверять с демо, документировать.
- **Stackable = breaking-смена поведения.** Раньше Stackable допускал несколько стеков одного предмета (авто-merge + overflow в новые слоты). Теперь one-per-ID. Проекты, полагавшиеся на мульти-слот Stackable, должны перейти на `SeparableStacksStrategy`. Отметить в release notes/доке; проверить демо и пресеты.
- **`VirtualSlotState` → public.** Read-поля public, `Apply`/`MarkEmpty`/ctor — `internal` (мутация только внутри сборки). Следить, чтобы публичный `GetSlotCandidates` не выставил мутацию наружу.
- **forced-new откат** (см. 2.7) и **unresolved-target guard** (`TransferPlanExecutor.cs:886-925`) — не сломать.
- **Шейпы/placement-инвентари** (`IPlacementInventory`, shaped placement в `TransferPlanner.TryPlanShapedPlacement`)
  идут отдельной веткой планирования — единый цикл 2.4 их не трогает; проверить, что выбор слотов их не задевает.

---

## 6. Лог решений

1. Один плоский список кандидатов (богатый: `RemainingCapacity`/`IsEmpty`); политика классифицирует сама.
2. Политика — полиморфный `SlotSelectionPolicyBase` (не enum), на инициаторе (`InventoryDropArea`); fallback — `DefaultSlotSelectionPolicy` стратегии (`static readonly`-синглтоны).
3. `CanCreateNewSlot` — чистая capability; порядок empty/stack/new решает политика.
4. **SelectionPolicy — единый механизм:** заполняет виртуальные слоты в планировщике (single/batch/overflow).
5. **blocked-resolver сужен** до «дроп в конкретный заблокированный слот» (occupied/rules) → alt/swap/reject.
6. `SlotSelection.New()` — сильная семантика «force create new slot»; intent доходит до исполнения.
7. **Исключать исходный слот** при area-drop в тот же инвентарь (общее правило + тест).
8. **one-per-ID — это сам `StackableItemStrategy`** (НИКАКИХ новых классов; для нескольких стеков одного предмета — `SeparableStacks`). Намеренная смена поведения Stackable (была «multiple slots» + авто-merge). Enforce в `GetSlotCandidates` (видит всё виртуальное состояние) + placement-методы (`GetAcceptableCount`/`TryAdd`/`TryAddToSlot`) + обновить доку класса; `CanUseAlternativeSlot` для распределения менять не нужно.
9. Релиз один, после всех коммитов; делим работу на коммиты C1..C9, каждый компилируется.
10. `ClosestToCursor` — лишь пример кастомной политики, не входит в поставку.
11. **Explicit drop остаётся explicit:** при `targetBaseSlotHint != null` первая попытка — в этот слот; policy не перекидывает; blocked-resolver только если слот заблокирован.
12. **`RequiresNewSlot` — на уровне `PlannedSlotAllocation`**, не entry (одна entry = смесь existing + new; forced-new отличается от обычного deferred-null).
13. **Виртуальный учёт новых слотов:** в цикле каждый `New()` уменьшает `remainingPotentialNewSlots`; перенос между записями batch — нельзя запланировать больше, чем dynamic создаст.
14. **Новый слот имеет capacity = `maxStackSize`** (не «бесконечная дырка»): amount forced-new allocation = `min(remaining, maxStackSize)`.
