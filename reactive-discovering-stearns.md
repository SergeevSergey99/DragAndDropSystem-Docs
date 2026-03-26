# Drop Policy Refactor — Current State

**Last Updated**: 2026-03-26

Этот файл больше не хранит старый план с `OccupiedTargetPolicy / CapacityPolicy / TargetUsagePolicy`.
Ниже зафиксирована актуальная модель после рефакторинга.

## Что изменилось

- старый `DropPolicy` class удалён
- `DragContext.Policy` удалён
- policy больше не мутируется на runtime-контексте
- `InventoryDropProcessor` резолвит policy на входе в pipeline
- `TransferPlanner` работает только с `ResolvedDropPolicy`
- input/actions передают временный `DropRequestPolicy`

## Текущая модель

### `DropRequestPolicy`

Runtime override для одной операции.

Поля:
- `BlockedTargetBehavior?`
- `AlternativePlacementMode?`
- `bool? AllowPartial`

Используется из:
- `CompleteDragAction`
- `InventoryDropArea`
- `AutoTransferService`

### `DropPolicySettings`

Inventory-level defaults в `UniversalInventory`.

Поля:
- `BlockedTargetBehavior`
- `AllowMergeOnDrop`
- `AllowPartial`
- `BatchMode`
- `AlternativePlacementMode`

### `ResolvedDropPolicy`

Нормализованный итог, который получает planner.

Поля:
- `BlockedTargetBehavior`
- `AllowPartial`
- `BatchMode`
- `AlternativePlacementMode`

## Порядок resolution

1. action или drop target может передать `DropRequestPolicy`
2. `InventoryDropProcessor` объединяет request с bound target override
3. если target inventory реализует `IDropPolicyProvider`, вызывается `ResolveDropPolicy(...)`
4. inventory defaults + request override превращаются в `ResolvedDropPolicy`
5. planner получает только resolved policy

## Порядок обработки одного entry

1. Валидируется source entry
2. Исходный item preview-конвертируется в target-side item
3. Через `InventoryAcceptanceRequest` считается acceptable count
4. Если есть `target slot`, planner сначала пробует его
5. Дальше есть три случая:

### 1. Full placement

- всё вошло в target
- entry success

### 2. Partial placement

- `AllowPartial = false` -> fail
- `AllowPartial = true` -> partial success
- remainder ищет alternative slots только если `BlockedTargetBehavior = FindAlternative`

### 3. Zero placement

- `Reject` -> fail
- `Swap` -> planner строит `RequiresSwap`
- `FindAlternative` -> стратегия перечисляет alternative slots

## Strategy-aware alternative placement

`FindAlternative` не означает “искать любой слот”.
Planner вызывает `IPlacementStrategy.EnumerateAlternativeSlots(...)`, а стратегия сама определяет порядок кандидатов.

`AlternativePlacementMode`:
- `MergeFirst`
- `EmptyFirst`
- `MergeOnly`
- `EmptyOnly`

### `Stackable`

Обычно естественный default:
- `MergeFirst`

### `SeparableStacks`

Поддерживает разные режимы:
- `MergeFirst`
- `EmptyFirst`
- `MergeOnly`
- `EmptyOnly`

`AllowMergeOnDrop` хранится в `DropPolicySettings` и влияет на то, можно ли merge-ить при явном drop в занятый compatible slot.

### `Unique`

Альтернативы фактически сводятся к empty slots.

## Same-inventory rule

Если `source inventory == target inventory`, то `FindAlternative` не перераскладывает предметы по другим слотам того же инвентаря.

Поведение:
- если target slot подошёл -> используем его
- если target slot не подошёл -> предмет остаётся на месте

Это правило убирает неожиданное “самоперемешивание” инвентаря при drop внутри того же контейнера.

## Batch mode

`BatchMode` хранится в `DropPolicySettings` и попадает в `ResolvedDropPolicy`.

- `Atomic` -> один fail отменяет весь plan
- `BestEffort` -> успешные entry остаются, failed entry отбрасываются

## Что ещё важно

- `InventoryDropProcessor` больше не кеширует plan между разными policy requests
- drop-area override влияет и на preview, и на execution
- same preview path и execution path используют одну policy-модель
- `TargetMode` удалён из публичной policy-модели как лишняя и противоречивая ось

## Ключевые файлы

- `Scripts/Core/DropPolicy.cs`
- `Scripts/Core/IDropRequestProcessor.cs`
- `Scripts/Inventories/IDropPolicyProvider.cs`
- `Scripts/Inventories/InventoryDropProcessor.cs`
- `Scripts/Inventories/TransferPlanner.cs`
- `Scripts/Inventories/TransferPlanExecutor.cs`
- `Scripts/Inventories/UniversalInventory.cs`

## Regression checklist

- `Reject / Swap / FindAlternative` дают ожидаемое поведение
- `AllowPartial` работает как inventory default и как request override
- `SeparableStacks` уважает `AllowMergeOnDrop`
- `AlternativePlacementMode` меняет порядок alternative placement
- same-inventory `FindAlternative` не перераскладывает предметы
- preview и execution не расходятся по policy
