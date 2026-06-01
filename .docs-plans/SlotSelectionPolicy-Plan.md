# План: SlotSelectionPolicy — разделение eligibility (стратегия) и selection (политика)

> Самодостаточный план реализации. Можно начинать реализацию, имея в контексте **только этот файл** + перечисленные исходники.
> Статус: **согласован, не начат**. Версия архитектуры скилла: 2.4.

---

## 1. Зачем

Сейчас `InventoryDropArea` при наведении (когда конкретный слот мышью не указан) спрашивает инвентарь
«куда положить предмет», и инвентарь возвращает **один** `suggestedBaseSlot`. Решение о том, какой слот
выбрать, целиком зашито внутрь `CanAcceptItem` каждой стратегии и продублировано между ними.

Нужно:
- В одном случае выбирать слот **с тем же видом предмета** (стек).
- В другом — сначала **пустой слот**, а если пусто и инвентарь Dynamic — **создать новый**.
- И вообще дать возможность легко подключать свои классы выбора (первый/последний/случайный/ближайший
  к курсору и т.д., включая собственный порядок «пустой / такой же / новый»).

## 2. Ключевое решение (архитектура)

Расщепляем `CanAcceptItem` на две ответственности:

- **Стратегия = eligibility.** Возвращает **классифицированный набор** годных слотов и флаг «можно ли
  создать новый». Применяет доменные правила (включая `PassesRules`/`PrefabPassesRules` и доменную
  семантику конкретной стратегии). Полиморфная политика НЕ может нарушить домен — стратегия просто не
  предложит запрещённый вариант.
- **Политика (`SlotSelectionPolicy`) = ordering/selection.** Из предложенного набора выбирает один слот
  (или «создать новый»). Владеет порядком empty/same/new **среди уже предложенного**.

Правила остаются в стратегии — кандидатами становятся только слоты, прошедшие `PassesRules`. Политика
правил не проверяет.

### Зафиксированные развилки (ответы пользователя)

1. **Полный список кандидатов**, не «по одному представителю». Расширяемость важнее микрооптимизации;
   кэширование (пул списков на время drag-операции) — отдельной задачей потом.
   **Один плоский список** годных слотов (НЕ два отдельных empty/stack) — каждый слот сам знает, пустой он
   или нет, два списка только провоцируют путаницу. Политика при необходимости классифицирует слот сама по
   `slot.IsEmpty` (а «тот же предмет» — по `slot.Stack.CanStack(request.ItemAdapter)`).
2. **Дефолтная политика — «первый по индексу» везде** (`FirstSlotSelectionPolicy`).
3. **`CanCreateNewSlot` — чистая capability**: «можно положить предмет в слот, который будет создан прямо
   сейчас» (Dynamic `potentialNewSlots > 0` + `PrefabPassesRules` + домен стратегии разрешает свежий слот для
   этого предмета). Флаг **НЕ гейтится на наличие пустых слотов** — может быть `true`, даже когда свободные
   слоты есть. Использовать его или нет — решает `SelectionPolicy`. Доменное ограничение остаётся только там,
   где оно есть по смыслу (Stackable one-per-ID: если предмет уже есть — свежий слот запрещён, см. §5).
4. **Политика живёт на инициаторе** (`InventoryDropArea`), НЕ в `DropPolicySettings` инвентаря. Для обычного
   дропа на конкретный слот политика не нужна (цель задана явно). **Fallback на инвентаре не делаем** —
   просто хардкодим конкретный класс-фолбэк в коде, если до него дойдёт.
5. `BlockedTargetResolver` НЕ трогаем и концептуально разводим: SlotSelectionPolicy работает **до** дропа на
   фазе hover (нет конкретного слота); `BlockedTargetResolver` — **после** отклонённой попытки дропа в слот
   (в основном при ручном наведении на слот). Это две разные фазы.
6. **`GetAcceptableCount` в этом заходе НЕ трогаем** — правим отдельной задачей (см. §7).

## 3. Текущее устройство (как есть, с привязками)

Цепочка выбора слота при наведении на область:

```
InventoryDropArea.TryBuildValidationContext   Scripts/UI/InventoryDropArea.cs:147
  → _inventory.CanAcceptItem(acceptanceRequest, out suggestedBaseSlot)   :171
      → UniversalInventory.CanAcceptItem(request, out suggestedBaseSlot)  Scripts/Inventories/UniversalInventory.cs:1445
          → _acceptanceStrategy.CanAcceptItem(_slots, request, canCreateNewSlot, potentialNewSlots, baseSlotPrefab, out suggestedBaseSlot)  :1458
  → context.WithTarget(suggestedBaseSlot, _inventory)   :179   (seeds preview/validation + сам дроп)
```

`canCreateNewSlot` и `potentialNewSlots` вычисляются в `UniversalInventory.CanAcceptItem`
(`UniversalInventory.cs:1456-1457`) из `_slotManagementSettings` и приходят в стратегию **готовыми** —
стратегия их не пересчитывает, только применяет доменную логику + `PrefabPassesRules`.

Текущее поведение `CanAcceptItem` по стратегиям:

- **UniqueItemStrategy** (`Scripts/Inventories/Strategies/UniqueItemStrategy.cs:105`): первый пустой
  (прошедший `PassesRules(slot,item,1)`) → вернуть; иначе `canCreateNewSlot && potentialNewSlots>0 &&
  PrefabPassesRules(...)`.
- **StackableItemStrategy** (`Scripts/Inventories/Strategies/StackableItemStrategy.cs:175`): первый непустой
  слот, куда можно доложить (есть место) → сразу вернуть (приоритет стек); пустой запоминается как fallback
  (`suggestedBaseSlot == null`); в конце — empty-fallback, иначе create-new. Итог: **стек → пустой → новый**.
  ⚠️ Это допускает **второй стек того же предмета**, когда первый полон (см. §6).
- **SeparableStacksStrategy** (`Scripts/Inventories/Strategies/SeparableStacksStrategy.cs:192`): первый по
  индексу слот, который **либо пустой, либо стекуемый** (что встретится раньше) → вернуть; иначе create-new.
  Позиционно, без явного приоритета.

Все три зовутся только из двух мест (ограниченный blast radius):
- `UniversalInventory.cs:1458`
- `DynamicSlotDecorator.cs:175` (просто делегирует внутреннему `_acceptanceStrategy`).

Полезные хелперы в `InventoryStrategyBase` (`Scripts/Inventories/Strategies/InventoryStrategyBase.cs`):
- `PassesRules(BaseSlot, IItemAdapter, int previewCount, InventoryAcceptanceRequest=null)` — :159
- `PrefabPassesRules(List<BaseSlot>, BaseSlot prefab, IItemAdapter, int previewCount, InventoryAcceptanceRequest)` — :192
- `FindSlotWithItem(List<BaseSlot>, IItemAdapter)` — :180 (первый непустой со стекуемым предметом)
- `GetMaxStackSize(IItemAdapter, defaultMax, allowOverride)` (static) — :123

Паттерн полиморфного поля в проекте (мимикрируем под него):
- `DropPolicySettings.cs:10-11` — `[SerializeReference, ManagedReferencePicker, InlineProperty, HideLabel] BlockedTargetResolverBase _blockedTargetResolver = new FindAlternativeBlockedTargetResolver();`
- `DropRequestPolicySettings.cs:10-12` — override-вариант с `[SerializeField] bool _override...` + `[ShowIf(...)]`.

`InventoryDropArea` уже имеет поле `_dropPolicyOverride` типа `DropRequestPolicySettings` (`InventoryDropArea.cs:30`)
— но это про blocked-resolver/allowPartial (transfer policy), **НЕ** про selection. SlotSelectionPolicy будет
ОТДЕЛЬНЫМ полем на `InventoryDropArea`.

## 4. Новые типы (namespace `UDND.Inventories`, папка `Scripts/Inventories/`)

> Все три типа ссылаются на `BaseSlot` (`UDND.Slots`) и `InventoryAcceptanceRequest` (`UDND.Inventories`),
> поэтому живут в `UDND.Inventories`. `InventoryDropArea` (`UDND.UI`) уже делает `using UDND.Inventories`.

### 4.1 `SlotAcceptanceCandidates` — результат стратегии

Один плоский список годных слотов (и пустые, и со стекуемым предметом — вперемешку, в порядке индекса) +
capability-флаг создания нового слота.

```csharp
public sealed class SlotAcceptanceCandidates
{
    public IReadOnlyList<BaseSlot> Slots { get; }  // ВСЕ годные слоты (empty И same-item-with-room), прошли правила
    public bool CanCreateNewSlot { get; }          // capability: можно положить в слот, который будет создан сейчас
    public int  PotentialNewSlots { get; }         // сколько ещё можно создать (инфо/для будущего count)

    public bool HasAny => (Slots != null && Slots.Count > 0) || CanCreateNewSlot;

    // ctor + static Empty (пустой набор, ничего не годится)
}
```
`Slots` никогда не `null` (использовать `Array.Empty<BaseSlot>()` по умолчанию). В списке непустой слот гарантированно
= «тот же предмет, есть место» (стратегия добавляет только прошедшие `CanStack` + `canFit>0` + правила), поэтому
политике для классификации достаточно `slot.IsEmpty`.

### 4.2 `SlotSelection` — результат политики

```csharp
public readonly struct SlotSelection
{
    public bool Accepted { get; }
    public BaseSlot Slot { get; }   // конкретный слот, или null если CreateNew
    public bool CreateNew { get; }  // true => создать новый слот (Slot == null)

    public static SlotSelection None      => new(false, null, false);
    public static SlotSelection Existing(BaseSlot slot) => new(true, slot, false);
    public static SlotSelection New()     => new(true, null, true);
    // private ctor(accepted, slot, createNew)
}
```
Семантика совместима с текущей конвенцией `CanAcceptItem`: «принято + slot == null» = «через создание
нового слота» (ср. хвост `SeparableStacksStrategy.cs:221`). Флаг `CreateNew` делает это явным.

### 4.3 `SlotSelectionPolicyBase` + `FirstSlotSelectionPolicy`

```csharp
[Serializable]
public abstract class SlotSelectionPolicyBase
{
    public abstract SlotSelection Select(SlotAcceptanceCandidates candidates, InventoryAcceptanceRequest request);
}

[Serializable]
public sealed class FirstSlotSelectionPolicy : SlotSelectionPolicyBase
{
    // Кандидат с наименьшим BaseSlot.Index из списка.
    // Если конкретных слотов нет, но CanCreateNewSlot => SlotSelection.New().
    // Иначе SlotSelection.None.
    public override SlotSelection Select(SlotAcceptanceCandidates c, InventoryAcceptanceRequest request)
    {
        BaseSlot best = null;
        foreach (var s in c.Slots) if (best == null || s.Index < best.Index) best = s;
        if (best != null) return SlotSelection.Existing(best);
        return c.CanCreateNewSlot ? SlotSelection.New() : SlotSelection.None;
    }
}
```
`FirstSlotSelectionPolicy` — дефолт везде, сохраняет текущее «первый по индексу» и трактует create-new как
последний вариант (только если конкретных слотов нет), хотя сам флаг может быть `true` при наличии пустых.

## 5. Изменения контракта стратегии

`IAcceptanceStrategy` (`Scripts/Inventories/Strategies/IAcceptanceStrategy.cs`):
- **Добавить** `SlotAcceptanceCandidates GetSlotCandidates(List<BaseSlot> slots, InventoryAcceptanceRequest request, bool canCreateNewSlot, int potentialNewSlots, BaseSlot baseSlotPrefab);`
- **Убрать** `bool CanAcceptItem(... out BaseSlot suggestedBaseSlot)` (выбор больше не задача стратегии).
- `GetAcceptableCount(...)` оставить без изменений.

`InventoryStrategyBase` (`:43`): убрать `abstract CanAcceptItem`, добавить `abstract GetSlotCandidates`.

`DynamicSlotDecorator` (`:173`): заменить делегирование `CanAcceptItem` на делегирование `GetSlotCandidates`
внутреннему `_acceptanceStrategy`.

### 5.1 Реализации `GetSlotCandidates` по стратегиям

Все добавляемые слоты проходят правила: пустые — через `PassesRules(slot, item, Math.Min(desired, maxSize), request)`;
непустые (стек) — через `PassesRules(slot, item, Math.Min(desired, canFit), request)` где `canFit = max(0, maxSize - count) > 0`.
`item == null || desired <= 0` → `SlotAcceptanceCandidates.Empty`.
`Slots` строится в порядке обхода `slots` (т.е. по индексу), пустые и стек-слоты идут вперемешку.

Общая форма capability-флага:
`CanCreateNewSlot = domainAllowsFreshSlot && canCreateNewSlot && potentialNewSlots > 0 && PrefabPassesRules(slots, prefab, item, Math.Min(desired, maxSize), request)`,
где `domainAllowsFreshSlot` зависит от стратегии (ниже). **Не** гейтить на пустоту `Slots`.

**UniqueItemStrategy** (`previewCount` всегда 1, стеков нет):
- `Slots` = все пустые, прошедшие `PassesRules(slot, item, 1, request)`.
- `domainAllowsFreshSlot = true` (каждый предмет в своём слоте; свежий слот всегда легитимен).
- `CanCreateNewSlot = canCreateNewSlot && potentialNewSlots > 0 && PrefabPassesRules(slots, prefab, item, 1, request)`.

**StackableItemStrategy** — семантика **one-per-ID** (один слот на вид предмета):
- Определить «предмет уже есть»: существует непустой слот с `slot.Stack.CanStack(item)` (ср. `FindSlotWithItem`).
- Если предмет ЕСТЬ:
  - `Slots` = `[тот слот]`, **только если** в нём есть место (`canFit > 0`) и проходит `PassesRules`; иначе пусто.
  - `domainAllowsFreshSlot = false` (второй слот того же ID запрещён).
  - `CanCreateNewSlot = false`.
  - (Следствие: предмет есть, стек полон → `Slots` пуст и `CanCreateNewSlot=false` → отказ. **Намеренно**, см. §6.)
- Если предмета НЕТ:
  - `Slots` = все пустые, прошедшие `PassesRules(slot, item, Math.Min(desired, maxSize), request)`.
  - `domainAllowsFreshSlot = true`.
  - `CanCreateNewSlot = canCreateNewSlot && potentialNewSlots > 0 && PrefabPassesRules(...)`.
    **Может быть `true` одновременно с непустым `Slots`** (есть свободные слоты, и при этом Dynamic может
    создать ещё один) — это нормально, выбор за политикой.
- `maxSize = GetMaxStackSize(item, DefaultMaxStackSize, AllowItemStackOverride)`.

**SeparableStacksStrategy** — несколько стеков одного предмета разрешены:
- `Slots` = все непустые слоты с `CanStack(item)` и `canFit > 0` (прошедшие правила) **+** все пустые
  (прошедшие правила), в порядке индекса.
- `domainAllowsFreshSlot = true`.
- `CanCreateNewSlot = canCreateNewSlot && potentialNewSlots > 0 && PrefabPassesRules(...)` (может быть `true`
  и при наличии пустых/стеков — решает политика).
- `maxSize = GetMaxStackSize(item, DefaultMaxStackSize, AllowItemStackOverride)`.

## 6. Поведенческие изменения (ОБЯЗАТЕЛЬНО зафиксировать)

⚠️ **StackableItemStrategy меняет поведение.** Сейчас (`StackableItemStrategy.cs:185-206`), если предмет
есть, но его стек полон, старый код падает в пустой/создаёт новый слот — т.е. допускает **второй стек того же
предмета**. Новая one-per-ID семантика это **запретит**: предмет есть + стек полон → отказ, нового слота нет.
Пользователь подтвердил, что one-per-ID — задуманный смысл Stackable (для нескольких стеков одного предмета —
`SeparableStacksStrategy`). Это исправление, но регрессионно заметное; при наличии тестов — обновить.

## 7. Вне scope этого захода

- **`GetAcceptableCount` для Stackable** сейчас тоже считает ёмкость пустых слотов, когда предмет уже есть
  (`StackableItemStrategy.cs:209`). Для консистентности с one-per-ID его надо привести к той же логике
  (предмет есть → ёмкость только его слота). **Делаем отдельной задачей**, не в этом PR.
- Кэширование/пул списков кандидатов — отдельной задачей.

## 8. Wiring (по файлам)

1. **`Scripts/Inventories/InventoryAcceptanceRequest.cs`**: добавить необязательное свойство
   `SlotSelectionPolicyBase SelectionPolicy { get; }` + параметр ctor (по умолчанию `null`). Запрос уже носит
   `DragContext Context` и `DragEntry? SourceEntry`, добавляем рядом.

2. **`Scripts/Inventories/UniversalInventory.cs:1445`** `CanAcceptItem(request, out suggestedBaseSlot)`:
   ```csharp
   var candidates = _acceptanceStrategy.GetSlotCandidates(_slots, request, canCreateNewSlot, potentialNewSlots, baseSlotPrefab);
   var policy = request.SelectionPolicy ?? _fallbackSlotSelectionPolicy;   // hardcoded fallback
   var selection = policy.Select(candidates, request);
   suggestedBaseSlot = selection.Slot;
   return selection.Accepted;
   ```
   `_fallbackSlotSelectionPolicy` = `private static readonly SlotSelectionPolicyBase _fallbackSlotSelectionPolicy = new FirstSlotSelectionPolicy();`
   Сохранить существующие лог-строки success/reject.
   Overload `CanAcceptItem(IItemAdapter, int, out slot)` (`:1439`) НЕ трогаем — он строит request без политики
   → уйдёт в fallback.

3. **`Scripts/Inventories/Strategies/DynamicSlotDecorator.cs:173`**: заменить `CanAcceptItem`-делегат на
   `GetSlotCandidates`-делегат.

4. **`Scripts/UI/InventoryDropArea.cs`**:
   - Добавить поле (мимикрия под `DropPolicySettings.cs:10-11`):
     ```csharp
     [Header("Slot Selection")]
     [SerializeReference, ManagedReferencePicker, InlineProperty, HideLabel,
      Tooltip("Как выбрать слот при наведении на область (нет конкретного слота под курсором).")]
     private SlotSelectionPolicyBase _slotSelectionPolicy = new FirstSlotSelectionPolicy();
     ```
     (атрибуты `ManagedReferencePicker`/`InlineProperty`/`HideLabel` — из `UDND.Tools.Inspector`, уже
     заюзаны в проекте; проверить usings.)
   - В `TryBuildValidationContext` (`:164`) при создании `InventoryAcceptanceRequest` передать
     `_slotSelectionPolicy` в новый параметр `SelectionPolicy`.

5. **Стратегии**: `UniqueItemStrategy.cs:105`, `StackableItemStrategy.cs:175`, `SeparableStacksStrategy.cs:192`
   — заменить `CanAcceptItem` на `GetSlotCandidates` по §5.1.

6. **`IAcceptanceStrategy.cs` + `InventoryStrategyBase.cs`** — обновить контракт по §5.

## 9. Чеклист файлов

- [ ] `Scripts/Inventories/SlotAcceptanceCandidates.cs` (new)
- [ ] `Scripts/Inventories/SlotSelection.cs` (new)
- [ ] `Scripts/Inventories/SlotSelectionPolicyBase.cs` (new, + `FirstSlotSelectionPolicy` — можно в том же файле или отдельным)
- [ ] `Scripts/Inventories/InventoryAcceptanceRequest.cs` (+ SelectionPolicy)
- [ ] `Scripts/Inventories/Strategies/IAcceptanceStrategy.cs` (контракт)
- [ ] `Scripts/Inventories/Strategies/InventoryStrategyBase.cs` (контракт)
- [ ] `Scripts/Inventories/Strategies/UniqueItemStrategy.cs` (GetSlotCandidates)
- [ ] `Scripts/Inventories/Strategies/StackableItemStrategy.cs` (GetSlotCandidates, one-per-ID)
- [ ] `Scripts/Inventories/Strategies/SeparableStacksStrategy.cs` (GetSlotCandidates)
- [ ] `Scripts/Inventories/Strategies/DynamicSlotDecorator.cs` (делегат)
- [ ] `Scripts/Inventories/UniversalInventory.cs` (применение политики + fallback)
- [ ] `Scripts/UI/InventoryDropArea.cs` (поле политики + проброс в request)

## 10. Проверка после реализации

- Компиляция Unity без ошибок; нет других вызовов старого `CanAcceptItem` стратегии (проверено: только
  `UniversalInventory.cs:1458` и `DynamicSlotDecorator.cs:175`).
- Дефолт (`FirstSlotSelectionPolicy`) воспроизводит «первый по индексу» во всех стратегиях, кроме намеренного
  изменения Stackable (§6).
- Демо-сцена `Examples/Demo1 Inventories/InventoriesDemo.unity`: дроп на `InventoryDropArea` для Unique /
  Stackable / SeparableStacks выбирает ожидаемый слот; Dynamic-инвентарь создаёт новый слот, когда пустых нет.
