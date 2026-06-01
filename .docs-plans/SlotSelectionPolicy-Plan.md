# План: SlotSelectionPolicy — единый механизм выбора слота

> Самодостаточный план. Реализацию можно вести, имея в контексте **только этот файл** + перечисленные исходники.
> Статус: **согласован, единая модель, режем на коммиты.** Релиз один — после всех коммитов; промежуточных
> релизов нет, но каждый коммит **компилируется** и (по умолчанию) сохраняет наблюдаемое поведение для
> single-slot дропа.

---

## 0. Модель (TL;DR)

Выбор слота — **один механизм** на всех путях:

- **Стратегия = eligibility.** `GetSlotCandidates(slotViews, request, ...)` отдаёт классифицированных кандидатов
  (+ capability «создать новый»). Работает и на **реальных** слотах (граница UI), и на **виртуальных** слотах
  планировщика (через общий `ISlotView`).
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

### 2.1 Общий вид слота `ISlotView` (новый публичный read-интерфейс)

`VirtualSlotState` — `internal`, а `IAcceptanceStrategy` публичный, поэтому `GetSlotCandidates` не может принимать
`VirtualSlotState` напрямую. Вводим:
```csharp
public interface ISlotView
{
    BaseSlot Slot { get; }
    bool IsEmpty { get; }
    IItemAdapter ItemAdapter { get; }
    int Count { get; }
}
```
- `VirtualSlotState` реализует `ISlotView` (поля уже есть; `Slot => BaseSlot`).
- Для границы UI — лёгкий адаптер над реальным `BaseSlot` (или просто оборачиваем реальные слоты в свежие
  `VirtualSlotState`, как `BuildVirtualSlots`). Так eligibility-код один на оба случая.

### 2.2 Стратегия = eligibility

`IAcceptanceStrategy`:
- **Добавить** `SlotAcceptanceCandidates GetSlotCandidates(IReadOnlyList<ISlotView> slots, InventoryAcceptanceRequest request, bool canCreateNewSlot, int potentialNewSlots, BaseSlot baseSlotPrefab);`
- **Добавить** `SlotSelectionPolicyBase DefaultSlotSelectionPolicy { get; }` (для `request.SelectionPolicy == null` и программных вызовов; сохраняет текущее поведение).
- **Убрать** `CanAcceptItem(... out suggestedBaseSlot)`. `GetAcceptableCount` — без изменений.

Правила (`PassesRules`/`PrefabPassesRules`) — внутри `GetSlotCandidates`, как сейчас. **Общее обязательное правило:**
исключать исходный слот при area-drop в тот же инвентарь:
```csharp
if (ReferenceEquals(request.SourceInventory, request.TargetInventory) &&
    ReferenceEquals(view.Slot, request.SourceBaseSlot))
    continue;
```

Per-strategy `GetSlotCandidates` (eligibility = текущая) + дефолт-политика:
- **Unique** → `DefaultSlotSelectionPolicy = FirstSlotSelectionPolicy`: `Slots` = пустые (rules,1), `RemainingCapacity=1`; `CanCreateNewSlot = canCreate && potential>0 && PrefabPassesRules(...,1)`.
- **Stackable** → `DefaultSlotSelectionPolicy = StackFirstSlotSelectionPolicy`: `Slots` = непустые с `CanStack && canFit>0` (rules) + пустые (rules), в порядке индекса; `CanCreateNewSlot = canCreate && PrefabPassesRules(...,Min(desired,maxSize))`.
- **SeparableStacks** → `DefaultSlotSelectionPolicy = FirstSlotSelectionPolicy`: состав `Slots` как Stackable; дефолт First = первый годный по индексу.

> `CanCreateNewSlot` — чистая capability (не загейчена на пустоту списка). Дефолт-политики берут New() только когда
> конкретных кандидатов нет → single-slot поведение идентично текущему.

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

Вместо `EnumerateAlternativeVirtualSlots` (blocked-resolver) для распределения:
- Для каждой записи (и single, и batch) планировщик в цикле: построить `ISlotView` поверх текущих
  `VirtualSlotState` → `GetSlotCandidates` → `policy.Select` → если `Existing(slot)`: `Apply` в виртуальный слот,
  добавить `PlannedSlotAllocation`, уменьшить `remaining`; если `New()`: добавить синтетический кандидат «новый
  слот» (forced-new) → `PlannedSlotAllocation(null, amount)` + флаг `RequiresNewSlot`; если `None`: стоп.
  Повторять, пока `remaining>0` и есть кандидаты. **Виртуальное состояние переносится между записями batch.**
- Политика берётся из плана (см. 2.5). `null` → `DefaultSlotSelectionPolicy` стратегии.

### 2.5 Проводка политики (intent не теряется)

- `InventoryAcceptanceRequest`: + необязательное `SlotSelectionPolicyBase SelectionPolicy`.
- `InventoryDropArea`: + `[SerializeReference, ManagedReferencePicker, InlineProperty, HideLabel] SlotSelectionPolicyBase _slotSelectionPolicy;` (null → дефолт стратегии). Передаёт политику в processor; **batch больше не пропускает выбор** (снять гейт `!IsBatchDrag`, см. C5).
- `InventoryDropProcessor`: + ctor-параметр `SlotSelectionPolicyBase selectionPolicy` (и для forced-new — поведение через политику, отдельный bool не нужен). Прокидывает в `TransferPlanner.BuildPlan`.
- `TransferPlanner.BuildPlan`: + параметр политики, использует в 2.4.
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
  в `ISlotView` → `GetSlotCandidates` → дефолт/`request.SelectionPolicy` → `selection.Slot`. (Главный путь выбора
  теперь в планировщике; этот метод — для accept-проверки/превью и внешних вызовов.)

### 2.6 blocked-resolver — сузить роль

Оставить вызов blocked-resolver **только** при попытке дропа в конкретный слот, который заблокирован
(занят другим предметом / отклонён правилами): альтернатива / swap / reject. Убрать его из общего распределения
(его заменяет 2.4). Swap-резолв (`TryPlanSwap`, occupied-target) сохраняется в blocked-resolver.

### 2.7 forced-new (исполнение)

- В плане синтетический «новый слот» → `PlannedSlotAllocation(null, amount)` + `RequiresNewSlot` на `PlannedEntryTransfer`.
- Executor: в null-allocation ветке (`TransferPlanExecutor.cs:1004`) при `RequiresNewSlot` — НЕ `TryAddStack(-1)`
  (переиспользует первый пустой), а создать слот и положить в него: новая capability
  `IDynamicSlotLifecycle.TryCreateSlot(out BaseSlot newSlot)` (`InventoryRuntimeCapabilities.cs:30`), открывающая
  приватный `UniversalInventory.CreateSlot()` (`:366`) с проверкой `_slotManagementSettings.CanCreateNewSlot`, затем
  `TryAddToSlot(stack, newSlot)`.
- Откат: Atomic — снапшот восстанавливает кол-во слотов (`UniversalInventory.cs:1100-1115`); BestEffort —
  пустой созданный слот убирается через `HandleSlotEmptied`/trim. Не задеть «unresolved-target guard» (`:886-925`).

### 2.8 one-per-ID (новый класс стратегии)

Отдельный класс (`SingleSlotStackStrategy`), не переопределение Stackable. Под единой моделью enforce делается
**прямо в `GetSlotCandidates`** (стратегия видит весь `ISlotView`-список): предмет уже есть в каком-то слоте →
кандидаты = только тот слот (если есть место), `CanCreateNewSlot=false`; предмета нет → пустые + new. Поскольку
распределение теперь спрашивает `GetSlotCandidates` на каждом шаге, послотовый `CanUseAlternativeSlot` менять **не
нужно** (он остаётся только для blocked-resolver). `GetAcceptableCount` — привести к той же семантике.

---

## 3. Ключевые типы (сводка)

- `ISlotView` (public) — общий вид слота; реализует `VirtualSlotState`.
- `SlotAcceptanceCandidate` (struct): `BaseSlot Slot; int RemainingCapacity; bool IsEmpty;`
- `SlotAcceptanceCandidates`: `IReadOnlyList<SlotAcceptanceCandidate> Slots; bool CanCreateNewSlot; int PotentialNewSlots; bool HasAny;`
- `SlotSelection` (struct): `bool Accepted; BaseSlot Slot; bool CreateNew;` (см. 2.5)
- `SlotSelectionPolicyBase` + `FirstSlotSelectionPolicy`, `StackFirstSlotSelectionPolicy`.
- `IDynamicSlotLifecycle.TryCreateSlot(out BaseSlot)` (расширение).
- `PlannedEntryTransfer.RequiresNewSlot` (новый флаг).

---

## 4. Разбивка по коммитам

Каждый коммит компилируется. Инвариант «зелёного дерева»: до C9 дефолт-политики сохраняют single-slot поведение;
распределение/batch ordering становится policy-driven (нормализуется) — валидировать по демо.

- **C1 — типы.** `ISlotView`, `SlotAcceptanceCandidate(s)`, `SlotSelection`, `SlotSelectionPolicyBase` + `First`/`StackFirst`. Чистые добавления.
- **C2 — eligibility.** `VirtualSlotState : ISlotView`. В `IAcceptanceStrategy`/`InventoryStrategyBase`/3 стратегии/`DynamicSlotDecorator`: `GetSlotCandidates(ISlotView...)` + `DefaultSlotSelectionPolicy`; убрать стратегический `CanAcceptItem`. Source-exclusion. `UniversalInventory.CanAcceptItem` переписан на новый путь (обёртка `_slots`→`ISlotView`, дефолт-политика) — single-slot поведение сохранено. Build green.
- **C3 — проводка политики (без смены поведения).** `InventoryAcceptanceRequest.SelectionPolicy`; `InventoryDropArea` поле + хранит `SlotSelection`; `InventoryDropProcessor` + параметр политики; `BuildPlan` + параметр (пока планировщик использует дефолт внутри → поведение не меняется). Green.
- **C4 — единый цикл аллокации (single + overflow).** Планировщик: распределение через `GetSlotCandidates(virtual)` + политика (2.4) вместо blocked-resolver-ordering. Дефолт воспроизводит текущее single-slot; multi-slot ordering нормализуется — сверить с демо.
- **C5 — batch.** Снять гейт `!IsBatchDrag` в `InventoryDropArea`; per-entry политика + перенос виртуального состояния между записями (из C4 почти бесплатно). Валидировать batch-дроп.
- **C6 — сузить blocked-resolver.** Только explicit-blocked-target (occupied/rules) → alt/swap/reject; убрать из общего распределения. Проверить swap.
- **C7 — forced-new.** `SlotSelection.New()` → синтетический кандидат → `RequiresNewSlot` → executor `TryCreateSlot`+`TryAddToSlot`; `IDynamicSlotLifecycle.TryCreateSlot`. Откат (2.7).
- **C8 — one-per-ID.** Новый `SingleSlotStackStrategy` (2.8) + `GetAcceptableCount` под ту же семантику.
- **C9 — тесты/демо/доки.** Тест source-exclusion; batch; forced-new при `maxFreeSlots=0`; one-per-ID (предмет есть → второй слот не создаётся). Демо `Examples/Demo1 Inventories/InventoriesDemo.unity`. Обновить скиллы (`dragdrop-architecture` и др.).

Релиз — после C9.

---

## 5. Риски / заметки

- **Нормализация ordering при распределении.** Сейчас multi-slot распределение идёт через
  `EmptyFirstAlternativePlacementStrategy` (empty-first), а single-hint у Stackable — stack-first. Унификация делает
  ordering policy-driven; в части multi-slot краёв поведение может слегка измениться. Сверять с демо, документировать.
- **`ISlotView` vs `internal VirtualSlotState`.** Следить за accessibility (интерфейс public, реализация internal — ок).
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
8. one-per-ID — отдельный класс стратегии; enforce в `GetSlotCandidates` (видит всё виртуальное состояние) → `CanUseAlternativeSlot` для распределения менять не нужно.
9. Релиз один, после всех коммитов; делим работу на коммиты C1..C9, каждый компилируется.
10. `ClosestToCursor` — лишь пример кастомной политики, не входит в поставку.
