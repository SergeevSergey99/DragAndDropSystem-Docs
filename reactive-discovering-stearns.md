# Переделка Policy-системы — финальный план

## Контекст

Текущий `DropPolicy` — sealed class с 4 enum-осями (`OccupiedTargetPolicy`, `CapacityPolicy`, `BatchExecutionPolicy`, `TargetUsagePolicy`). Проблемы:
- Policy мутируется на `DragContext` в `InventoryDropProcessor.CanAcceptDrop()` (строка 72)
- 3 разных места подставляют дефолты (DragContext конструктор, ResolveEffectivePolicy, TransferPlanner)
- Часть комбинаций противоречива (StrictTarget + TryAlternativeSlots)
- **Input-слой (Ctrl/Shift) не может влиять на policy** — `CompleteDragAction` вызывает `CompleteDrag()` без параметров
- DragAmount жёстко на инвентаре, не меняется через модификаторы
- `_cachedPlan` в `InventoryDropProcessor` не зависит от policy — при смене модификатора можно получить план от другого запроса

## Принцип: User-intent request vs Inventory config

Разделяем на два слоя:
1. **Request** — что хочет пользователь (приходит из binding/action, nullable overrides)
2. **Inventory config** — что разрешено инвентарём (Inspector, фиксированные значения)

---

## 1. Новые типы

### Enums

```csharp
namespace DragAndDropSystem.Core
{
    /// <summary>
    /// Что делать когда целевой слот заблокирован (занят, не прошёл правила, неподходящий тип).
    /// User-intent — задаётся через модификаторы/bindings.
    /// </summary>
    public enum BlockedTargetBehavior : byte
    {
        Reject = 0,          // отклонить
        Swap = 1,            // обменять с содержимым целевого слота
        FindAlternative = 2  // найти другой подходящий слот
    }

    /// <summary>
    /// Как интерпретировать целевой слот.
    /// </summary>
    public enum TargetMode : byte
    {
        Strict = 0,  // только указанный слот, никогда не искать альтернативы
        Hint = 1     // слот как подсказка, можно искать альтернативы при неудаче
    }

    /// <summary>
    /// Сколько предметов подбирать при начале drag.
    /// </summary>
    public enum DragAmount : byte
    {
        All = 0,
        Half = 1,
        One = 2,
        Custom = 3
    }

    /// <summary>
    /// Режим выполнения batch-операции. Inventory-level config.
    /// </summary>
    public enum BatchMode : byte
    {
        Atomic = 0,
        BestEffort = 1
    }

    /// <summary>
    /// Как раскладывать остаток/альтернативное размещение.
    /// Особенно важно для SeparableStacks.
    /// </summary>
    public enum AlternativePlacementMode : byte
    {
        StrategyDefault = 0,
        MergeFirst = 1,
        EmptyFirst = 2,
        MergeOnly = 3,
        EmptyOnly = 4
    }
}
```

### DropRequestPolicy (user-intent, nullable overrides)

```csharp
/// <summary>
/// Запрос пользователя на поведение дропа.
/// Все поля nullable — null = "используй default инвентаря / derive from context".
/// Передаётся из Action → Manager → Processor → Provider.
/// </summary>
public readonly struct DropRequestPolicy
{
    public BlockedTargetBehavior? BlockedTarget { get; }
    public TargetMode? Target { get; }

    public DropRequestPolicy(BlockedTargetBehavior? blockedTarget, TargetMode? target = null)
    {
        BlockedTarget = blockedTarget;
        Target = target;
    }

    // Convenience constructors
    public static DropRequestPolicy WithBlocked(BlockedTargetBehavior behavior)
        => new DropRequestPolicy(behavior);

    public static DropRequestPolicy WithSwap()
        => new DropRequestPolicy(BlockedTargetBehavior.Swap);

    public static DropRequestPolicy WithFindAlternative()
        => new DropRequestPolicy(BlockedTargetBehavior.FindAlternative, TargetMode.Hint);
}
```

Примечание по coherence:
- `BlockedTargetBehavior.FindAlternative` имеет смысл только вместе с `TargetMode.Hint`
- если request задаёт `FindAlternative`, но после resolution получается `TargetMode.Strict`, это считается противоречием
- inventory/provider обязан устранить противоречие детерминированно, без передачи неконсистентного policy в planner

### DragRequestPolicy (user-intent для drag start)

```csharp
/// <summary>
/// Запрос пользователя на поведение при начале drag.
/// null Amount = "используй default инвентаря".
/// </summary>
public readonly struct DragRequestPolicy
{
    public DragAmount? Amount { get; }
    public int CustomAmount { get; }

    public DragRequestPolicy(DragAmount amount, int customAmount = 0)
    {
        Amount = amount;
        CustomAmount = amount == DragAmount.Custom ? System.Math.Max(1, customAmount) : 0;
    }

    public static readonly DragRequestPolicy All = new(DragAmount.All);
    public static readonly DragRequestPolicy Half = new(DragAmount.Half);
    public static readonly DragRequestPolicy One = new(DragAmount.One);
}
```

### ResolvedDropPolicy (результат resolution, не-nullable)

```csharp
/// <summary>
/// Полностью resolved policy для передачи в planner/executor.
/// Все поля заполнены — нет nullable, нет default-resolution.
/// </summary>
public readonly struct ResolvedDropPolicy
{
    public BlockedTargetBehavior BlockedTarget { get; }
    public TargetMode Target { get; }
    public bool AllowPartial { get; }
    public BatchMode BatchMode { get; }
    public AlternativePlacementMode AlternativePlacement { get; }

    public ResolvedDropPolicy(
        BlockedTargetBehavior blockedTarget,
        TargetMode target,
        bool allowPartial,
        BatchMode batchMode,
        AlternativePlacementMode alternativePlacement)
    {
        BlockedTarget = blockedTarget;
        Target = target;
        AllowPartial = allowPartial;
        BatchMode = batchMode;
        AlternativePlacement = alternativePlacement;
    }
}
```

### Inspector wrappers

```csharp
[Serializable]
public sealed class DropRequestPolicySettings
{
    [SerializeField] private bool _overrideBlockedTarget;
    [SerializeField, ShowIf(nameof(_overrideBlockedTarget))]
    private BlockedTargetBehavior _blockedTarget = BlockedTargetBehavior.FindAlternative;

    [SerializeField] private bool _overrideTargetMode;
    [SerializeField, ShowIf(nameof(_overrideTargetMode))]
    private TargetMode _targetMode = TargetMode.Strict;

    /// <summary>Возвращает null если ни один override не включён.</summary>
    public DropRequestPolicy? TryBuild()
    {
        if (!_overrideBlockedTarget && !_overrideTargetMode)
            return null;

        return new DropRequestPolicy(
            _overrideBlockedTarget ? _blockedTarget : null,
            _overrideTargetMode ? _targetMode : null);
    }
}

[Serializable]
public sealed class DragRequestPolicySettings
{
    [SerializeField] private bool _enabled;
    [SerializeField, ShowIf(nameof(_enabled))]
    private DragAmount _amount = DragAmount.All;
    [SerializeField, Range(1, 100), ShowIf(nameof(ShowCustom))]
    private int _customAmount = 1;

    private bool ShowCustom => _enabled && _amount == DragAmount.Custom;

    public DragRequestPolicy? TryBuild()
    {
        if (!_enabled) return null;
        return new DragRequestPolicy(_amount, _customAmount);
    }
}
```

---

## 2. IDropPolicyProvider

```csharp
namespace DragAndDropSystem.Inventories
{
    /// <summary>
    /// Реализуется target-инвентарём. Принимает request от action-слоя,
    /// мержит с inventory defaults, применяет вето.
    /// </summary>
    public interface IDropPolicyProvider
    {
        /// <summary>
        /// Резолвит финальный ResolvedDropPolicy.
        /// requested может быть null (action не задал override).
        /// </summary>
        ResolvedDropPolicy ResolveDropPolicy(DropRequestPolicy? requested, DragContext context);
    }
}
```

---

## 3. Strategy-Aware Planning

Policy не должна сама решать, **как именно** стратегия размещает предмет по альтернативным слотам.
Это strategy-specific знание.

Общая часть policy отвечает только на:
- можно ли искать альтернативы вообще
- можно ли делать partial
- можно ли делать swap
- strict или hint target

Но порядок обхода альтернатив и тип допустимых кандидатов определяется стратегией:

- `Unique`
  - только пустые слоты
  - по 1 предмету в слот
  - `AlternativePlacementMode` игнорируется

- `Stackable`
  - same-item merge slots + empty slots
  - дефолт: `MergeFirst`
  - `FindAlternative` означает "продолжить canonical compact placement"

- `SeparableStacks`
  - empty slots всегда допустимы
  - merge slots допустимы только если `_allowMergeOnDrop`
  - `AlternativePlacementMode` обязательно влияет на результат:
    - `MergeFirst`
    - `EmptyFirst`
    - `MergeOnly`
    - `EmptyOnly`

Следствие:
- общая policy resolution остаётся в inventory/provider
- интерпретация `FindAlternative` / remainder allocation переносится в strategy-aware planning layer
- planner не должен навязывать stackable-логику всем стратегиям

---

## 4. IDropRequestProcessor

`IDropProcessor` остаётся базовым интерфейсом для legacy/non-inventory targets (`WorldDropZone` и т.д.).
Для policy-aware inventory pipeline вводится отдельный контракт, чтобы request не протекал через concrete-type cast.

```csharp
namespace DragAndDropSystem.Core
{
    public interface IDropRequestProcessor : IDropProcessor
    {
        bool CanAcceptDrop(DragContext context, DropRequestPolicy? requested);
        DropResult ProcessDrop(DragContext context, DropRequestPolicy? requested);
    }
}
```

`InventoryDropProcessor` реализует `IDropRequestProcessor`.
`DragAndDropManager` работает так:
- если `processor is IDropRequestProcessor requestProcessor` → вызывает overload с request
- иначе использует legacy `IDropProcessor` без request

---

## 5. Inventory config — новые поля на UniversalInventory

```csharp
// Заменяют старый _dropPolicySettings

[FoldoutGroup("Drop Policy")]
[SerializeField, EnumToggleButtons]
private BlockedTargetBehavior _defaultBlockedTargetBehavior = BlockedTargetBehavior.FindAlternative;

[FoldoutGroup("Drop Policy")]
[SerializeField, EnumToggleButtons]
private TargetMode _defaultTargetMode = TargetMode.Strict;

[FoldoutGroup("Drop Policy")]
[SerializeField, Tooltip("Разрешить swap на этом инвентаре")]
private bool _allowSwap = true;

[FoldoutGroup("Strategy")]
[SerializeField, Tooltip("Разрешить частичный перенос стека")]
private bool _allowPartialTransfer = true;

[FoldoutGroup("Strategy")]
[SerializeField, EnumToggleButtons]
private BatchMode _batchMode = BatchMode.BestEffort;

[FoldoutGroup("Strategy")]
[SerializeField, EnumToggleButtons]
private AlternativePlacementMode _alternativePlacementMode = AlternativePlacementMode.StrategyDefault;

// Properties
public bool AllowPartialTransfer => _allowPartialTransfer;
public BatchMode BatchExecutionMode => _batchMode;
public AlternativePlacementMode AlternativePlacement => _alternativePlacementMode;
```

### UniversalInventory реализует IDropPolicyProvider

```csharp
public ResolvedDropPolicy ResolveDropPolicy(DropRequestPolicy? requested, DragContext context)
{
    // 1. BlockedTarget: request override → inventory default
    var blocked = requested?.BlockedTarget ?? _defaultBlockedTargetBehavior;

    // 2. TargetMode: request override → derive from context → inventory default
    var target = requested?.Target
        ?? (context.IsBatchDrag ? TargetMode.Hint : _defaultTargetMode);

    // 3. Veto: swap запрещён → downgrade до дефолта
    if (!_allowSwap && blocked == BlockedTargetBehavior.Swap)
        blocked = _defaultBlockedTargetBehavior;

    // 4. Coherence: FindAlternative несовместим с Strict.
    // Inventory strict-mode важнее request без явного Target override,
    // поэтому downgrade до Reject вместо скрытого upgrade в Hint.
    if (target == TargetMode.Strict && blocked == BlockedTargetBehavior.FindAlternative)
        blocked = BlockedTargetBehavior.Reject;

    // 5. Inventory-level config (не overrideable через request)
    return new ResolvedDropPolicy(
        blocked,
        target,
        _allowPartialTransfer,
        _batchMode,
        _alternativePlacementMode);
}
```

---

## 6. Цепочка resolution (end-to-end)

### Drop

```
CompleteDragAction._dropPolicyOverride.TryBuild()  →  DropRequestPolicy?
    ↓
DragAndDropManager.CompleteDrag(DropRequestPolicy? requested)
    ↓
IDropRequestProcessor.ProcessDrop(context, requested)
    ↓
ResolveEffectivePolicy(context, requested):
    1. target is IDropPolicyProvider → provider.ResolveDropPolicy(requested, context)
    2. fallback: merge requested с system defaults → ResolvedDropPolicy
    ↓
TransferPlanner.BuildPlan(context, resolvedPolicy, targetInventory, targetSlotHint, globalRules)
    ↓
strategy-aware placement logic uses resolvedPolicy + strategy capabilities
    ↓
TransferPlanExecutor.Execute(plan) — читает plan.ResolvedPolicy.BatchMode
```

### Drag

```
DragSlotAction._dragPolicyOverride.TryBuild()  →  DragRequestPolicy?
    ↓
DragAndDropManager.StartDrag(slots, DragRequestPolicy? requested)
    ↓
  requested != null → ResolveDragCount(slot, requested)
  null → slot.Inventory.GetDragAmount(slot) (текущее поведение)
```

---

## 7. Binding profile в Inspector

```
[LMB + None  + Down] → DragSlotAction     (dragPolicy: disabled → All from inventory)
[LMB + Shift + Down] → DragSlotAction     (dragPolicy: Half)
[LMB + None  + Up]   → CompleteDragAction (dropPolicy: disabled → from inventory)
[LMB + Ctrl  + Up]   → CompleteDragAction (dropPolicy: BlockedTarget=Swap)
[LMB + Shift + Up]   → CompleteDragAction (dropPolicy: BlockedTarget=FindAlternative, Target=Hint)
```

Per-inventory: merchant с `_allowSwap = false` → Ctrl+drop downgrade'ится до дефолта.

---

## 8. Изменения по файлам

### `Scripts/Core/DropPolicy.cs` — полная перезапись
- Удалить: `OccupiedTargetPolicy`, `CapacityPolicy`, `BatchExecutionPolicy`, `TargetUsagePolicy` enum'ы
- Удалить: `DropPolicy` sealed class, `DropPolicySettings` sealed class
- Добавить: все новые enum'ы, struct'ы, Settings-классы (см. раздел 1)

### `Scripts/Core/IDropRequestProcessor.cs` — новый файл
- Новый policy-aware интерфейс:
  - `bool CanAcceptDrop(DragContext, DropRequestPolicy?)`
  - `DropResult ProcessDrop(DragContext, DropRequestPolicy?)`
- `IDropProcessor` остаётся как базовый legacy-контракт

### `Scripts/Core/DragContext.cs`
- Удалить: `public DropPolicy Policy { get; set; }` (строка 32)
- Удалить: `Policy = DropPolicy.SingleDefault` / `Policy = DropPolicy.BatchAtomic` из конструкторов
- Удалить: private конструктор с `DropPolicy policy` параметром (строка 91)
- `WithTarget()` больше не копирует Policy

### `Scripts/DragAndDropManager.cs`
- `StartDrag(ISlot, DragRequestPolicy? = null)` — резолвит drag count
- `StartDrag(IReadOnlyList<ISlot>, DragRequestPolicy? = null)` — аналогично
- `CompleteDrag(DropRequestPolicy? = null)` — прокидывает в processor
- `CompleteDragAsync(DropRequestPolicy?)` — внутренняя async версия
- Добавить `ResolveDragCount(ISlot, DragRequestPolicy)` static helper
- При completion:
  - `processor is IDropRequestProcessor` → использовать overload с request
  - иначе fallback на legacy `IDropProcessor`
- Auto-transfer: без изменений (пока), передаёт `null` как request

### `Scripts/Interaction/SlotInteractionActions.cs`
- `DragSlotAction`: добавить `[SerializeField] DragRequestPolicySettings _dragPolicyOverride`
  - **Только drag policy**. Drop policy НЕ добавляем — start и complete не смешиваются.
  - При `IsDragging` вызывает `CompleteDrag(null)` — без policy override (дефолт инвентаря)
- `CompleteDragAction`: добавить `[SerializeField] DropRequestPolicySettings _dropPolicyOverride`
  - **Только drop policy.**
  - Вызывает `CompleteDrag(_dropPolicyOverride.TryBuild())`

### `Scripts/Inventories/InventoryDropProcessor.cs`
- Убрать `_policyOverride` из конструктора и поля
- **Убрать `_cachedPlan`** целиком — plan строится заново при каждом вызове
- Реализовать `IDropRequestProcessor`
- `CanAcceptDrop(DragContext)` — legacy wrapper, вызывает overload с `null`
- `CanAcceptDrop(DragContext, DropRequestPolicy?)` — основной метод
- `ProcessDrop(DragContext)` — legacy wrapper, вызывает overload с `null`
- `ProcessDrop(DragContext, DropRequestPolicy?)` — основной sync path
- `ProcessDropWithSummary(DragContext, DropRequestPolicy?)` — принимает request
- Убрать `context.Policy = effectivePolicy` мутацию
- `ResolveEffectivePolicy(DragContext, DropRequestPolicy?)`:
  ```csharp
  if (_targetInventory is IDropPolicyProvider provider)
      return provider.ResolveDropPolicy(requested, context);

  // Fallback для не-UniversalInventory:
  var blocked = requested?.BlockedTarget ?? BlockedTargetBehavior.FindAlternative;
  var target = requested?.Target ?? (context.IsBatchDrag ? TargetMode.Hint : TargetMode.Strict);

  // Coherence fallback: FindAlternative + Strict -> Reject
  if (target == TargetMode.Strict && blocked == BlockedTargetBehavior.FindAlternative)
      blocked = BlockedTargetBehavior.Reject;

  return new ResolvedDropPolicy(
      blocked,
      target,
      allowPartial: true,
      batchMode: BatchMode.BestEffort);
  ```

### `Scripts/Inventories/TransferPlanner.cs`
- `BuildPlan` сигнатура: `DropPolicy policy` → `ResolvedDropPolicy policy`
- Заменить все `effectivePolicy.BatchExecution == BatchExecutionPolicy.Atomic` → `policy.BatchMode == BatchMode.Atomic`
- Заменить все `effectivePolicy.Capacity == CapacityPolicy.Partial` → `policy.AllowPartial`
- **Зафиксировать семантику `TargetMode.Strict`:**
  - `Strict` = никогда не искать альтернативный слот
  - `Hint` = можно искать альтернативы, но только если `BlockedTarget == FindAlternative`
- `ResolveSlot` больше не должен содержать скрытую логику "Strict, но всё же ищем альтернативу".
- **Переписать planning на strategy-aware multi-allocation.**
  Нельзя иметь один общий `AllocateForDefaultInventory()` для всех не-unique стратегий.
  Planner должен:
  1. спросить стратегию, какие candidate slots допустимы
  2. спросить стратегию, в каком порядке обходить альтернативы
  3. строить multi-allocation по strategy-specific правилам
- Поведение по стратегиям:
  - `Unique`:
    - только пустые слоты
    - по 1 предмету в слот
  - `Stackable`:
    - canonical compact placement
    - дефолт alternative order = `MergeFirst`
    - пример: `5 [target full], 4 4 4 0` + drag `3` -> `5 5 5 5 0`
  - `SeparableStacks`:
    - target slot работает как explicit drop
    - alternative placement order зависит от `policy.AlternativePlacement`
    - примеры при max stack `5`, target `5`, рядом `4 4 4 0`, drag `3`:
      - `MergeFirst` -> `5 5 5 5 0`
      - `EmptyFirst` -> `5 4 4 4 3`
      - `MergeOnly` -> только existing merge targets
      - `EmptyOnly` -> только новый отдельный стак
- Если strategy умеет merge и empty placement одновременно, policy не выбирает конкретные слоты сама, а только разрешает path; конкретный порядок задаёт стратегия + `AlternativePlacementMode`
- `ShouldPlanSwap`: проверять `policy.BlockedTarget == BlockedTargetBehavior.Swap`
- `EntryPlanningOperation`: поле `DropPolicy Policy` → `ResolvedDropPolicy Policy`
- `PlanEntry` должен использовать `policy.AllowPartial`, а не inventory-wide acceptable count как будто это один слот
- `TransferPlan` должен описывать все конечные target slots заранее; executor не занимается дополнительным slot resolution
- Рекомендуемое направление: вынести strategy-specific обработку placement policy в strategy planner hooks, а не держать всё в одном большом switch внутри `TransferPlanner`

### `Scripts/Inventories/TransferPlan` (внутри TransferPlanner.cs)
- `DropPolicy Policy` → `ResolvedDropPolicy Policy`
- Конструктор обновить

### `Scripts/Inventories/TransferPlanExecutor.cs`
- `plan.Policy.BatchExecution` → `plan.Policy.BatchMode`
- `plan.Policy.BatchExecution == BatchExecutionPolicy.Atomic` → `plan.Policy.BatchMode == BatchMode.Atomic`
- `plan.Policy.BatchExecution == BatchExecutionPolicy.BestEffort` → `plan.Policy.BatchMode == BatchMode.BestEffort`
- **Убрать скрытый поиск alternative slot в executor.**
  После рефакторинга executor:
  - исполняет только те `allocation`, которые уже пришли из planner
  - не вызывает fallback-поиск альтернативных слотов сам
  - не расходится по поведению с preview / `CanAcceptDrop`
- `TargetPlacementOperation.AllowAlternativeSlots` и связанный fallback-путь либо удаляются, либо остаются только как legacy path вне planner-driven inventory pipeline

### `Scripts/Inventories/UniversalInventory.cs`
- Реализовать `IDropPolicyProvider` (см. раздел 3)
- Новые поля (см. раздел 3)
- Удалить: `_dropPolicySettings` поле, `GetDropPolicy(bool)` метод
- `DragAmountType` enum (строка 250) → `DragAmount` из Core namespace
- `GetDragAmount(ISlot)` (строка 789) — обновить вызов `_dragPolicy.ResolveDragAmount` с новым enum
- Добавить inventory-level `AlternativePlacementMode` config

### `Scripts/Inventories/AutoTransferService.cs`
- `ExecuteAsync` — добавить optional `DropRequestPolicy? requestedPolicy = null`
- Передать в `InventoryDropProcessor.CanAcceptDrop(context, requestedPolicy)`

### `Scripts/UI/InventoryDropArea.cs`
- `_dropPolicyOverride` (старый `DropPolicySettings`) → `DropRequestPolicySettings`
- `CreateDropProcessor` — убрать передачу policy в конструктор
- `TryResolveFocusedTargetSlot` — если processor поддерживает `IDropRequestProcessor`, передать `_dropPolicyOverride.TryBuild()` в `CanAcceptDrop`

### `Scripts/Inventories/Strategies/IDragPolicy.cs`
- `DragAmountType` → `DragAmount` (только rename)

### `Scripts/Inventories/Strategies/IPlacementStrategy.cs`
- Текущего `CanUseAlternativeSlot()` недостаточно для planner-driven policy system
- Нужен planner-facing extension point:
  - либо расширение `IPlacementStrategy`
  - либо отдельный `IPlacementPlanningStrategy`
- Минимум нужно уметь:
  - определить strategy-default `AlternativePlacementMode`
  - перечислить допустимые alternative candidates в strategy-specific порядке
  - различать merge-candidates и empty-candidates

### `Scripts/Inventories/Strategies/InventoryStrategyBase.cs`
- `ResolveDragAmount(int, DragAmountType, int)` → `ResolveDragAmount(int, DragAmount, int)`

### `Scripts/Inventories/Strategies/UniqueItemStrategy.cs`
- Rename `DragAmountType` → `DragAmount` в сигнатуре

### `Scripts/Inventories/Strategies/DynamicSlotDecorator.cs`
- Rename `DragAmountType` → `DragAmount` в сигнатуре

### `Scripts/Selection/StartMultiDragAction.cs`
- Добавить `DragRequestPolicySettings _dragPolicyOverride`
- Прокидывать в `StartDrag`

---

## 9. Порядок реализации

1. Новые enum'ы и struct'ы в `DropPolicy.cs` (рядом со старыми, без удаления старых)
2. `IDropPolicyProvider` интерфейс
3. `IDropRequestProcessor` интерфейс
4. `ResolvedDropPolicy` struct
5. `UniversalInventory` — новые поля + реализация `IDropPolicyProvider`
6. `EntryPlanningOperation` — `ResolvedDropPolicy` вместо `DropPolicy`
7. `TransferPlanner` — перевести на `ResolvedDropPolicy` и сразу переписать planning на strategy-aware multi-allocation
8. `TransferPlan` — `ResolvedDropPolicy` вместо `DropPolicy`
9. `IPlacementStrategy` / planner-facing strategy hook — добавить strategy-aware alternative placement API
10. `TransferPlanExecutor` — `plan.Policy.BatchMode` + удалить hidden alternative-slot fallback
11. `InventoryDropProcessor` — убрать `_cachedPlan`, убрать `_policyOverride`, новый `ResolveEffectivePolicy`, убрать мутацию `context.Policy`, реализовать `IDropRequestProcessor`
12. `DragContext` — убрать `Policy`
13. `DragAndDropManager` — новые сигнатуры `CompleteDrag`/`StartDrag` + request-aware dispatch в processor
14. `SlotInteractionActions` — policy settings на actions (`DragSlotAction` → drag only, `CompleteDragAction` → drop only)
15. `InventoryDropArea` — обновить на новые типы
16. `AutoTransferService` — optional request parameter
17. Strategies: rename `DragAmountType` → `DragAmount`
18. Cleanup — удалить старые типы (`DropPolicy` class, старые enum'ы, `DropPolicySettings`)

---

## 10. Верификация

1. Demo1 — drag/drop одиночного предмета с дефолтами инвентаря
2. Настроить bindings: Ctrl+Up → `CompleteDragAction(BlockedTarget=Swap)`, проверить swap
3. Настроить bindings: Shift+Up → `CompleteDragAction(BlockedTarget=FindAlternative, Target=Hint)`, проверить поиск альтернативы
4. Merchant inventory: `_allowSwap = false` → Ctrl+drop не свапает, downgrade до дефолта
5. Batch drag → `_batchMode = Atomic` откатывает при ошибке
6. Shift+Down → `DragSlotAction(dragPolicy: Half)` → подбирает половину стека
7. Auto-transfer — работает без модификаторов, берёт default инвентаря
8. `TargetMode.Strict` на single drag → дроп в невалидный слот отклоняется без поиска альтернатив
9. `TargetMode.Hint` через override → single drag находит альтернативный слот
10. Partial stack into occupied same-item slot:
    - часть входит в target slot
    - remainder либо ищет другие слоты (`FindAlternative + Hint`), либо остаётся в source (`Strict` / no alternative)
11. Preview и execution совпадают:
    - `CanAcceptDrop` и `ProcessDrop` дают одинаковый результат при одинаковом request
    - executor не находит "лишний" альтернативный слот, которого не было в плане
12. `Stackable` + `FindAlternative`:
    - проверка merge-first compact placement
13. `SeparableStacks` + `AlternativePlacementMode.MergeFirst`:
    - `5 4 4 4 0` + drag `3` -> `5 5 5 5 0`
14. `SeparableStacks` + `AlternativePlacementMode.EmptyFirst`:
    - `5 4 4 4 0` + drag `3` -> `5 4 4 4 3`
15. `Unique`:
    - alternatives используют только пустые слоты
