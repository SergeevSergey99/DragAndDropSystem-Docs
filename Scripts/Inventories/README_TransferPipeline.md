# Transfer Pipeline Architecture

**Last Updated**: 2026-03-22

Документ описывает текущую архитектуру drop/transfer pipeline, включая batch transfer, swap и target-aware preview.

## Зачем это сделано

Старая логика переноса была распределена между manager/UI-target кодом и плохо масштабировалась для:
- single vs batch
- partial vs atomic
- occupied target
- swap
- cross-inventory adapter conversion
- area-drop preview без конкретного target slot

Текущая схема разделяет ответственность:

1. `DropPolicy` определяет поведение
2. `TransferPlanner` строит план без мутаций
3. `TransferPlanExecutor` применяет план
4. `InventoryDropProcessor` связывает UI drop-target с planner/executor

## Основные компоненты

### DropPolicy

Файл: `Scripts/Core/DropPolicy.cs`

Определяет:
- `OccupiedTargetPolicy`
- `CapacityPolicy`
- `BatchExecutionPolicy`
- `TargetUsagePolicy`

Готовые профили:
- `DropPolicy.SingleDefault`
- `DropPolicy.BatchAtomic`
- `DropPolicy.BatchBestEffort`

### TransferPlanner

Файл: `Scripts/Inventories/TransferPlanner.cs`

Роль:
- планирует размещение без изменения данных
- использует `TransferItemConversionUtility` для target-side preview item
- считает capacity через `InventoryAcceptanceRequest`
- использует `VirtualSlotState` для batch planning
- валидирует кандидатов через `RuleEvaluationService`
- может пометить entry как `RequiresSwap`

Связанные helper-объекты:
- `EntryPlanningOperation`
- `VirtualSlotState`

### TransferPlanExecutor

Файл: `Scripts/Inventories/TransferPlanExecutor.cs`

Роль:
- исполняет `TransferPlan`
- выполняет обычные переносы через `InventoryTransferService`
- исполняет swap-ветку
- поддерживает rollback в atomic mode
- откладывает события до успешного завершения плана
- уведомляет DataBinding напрямую через `HandleItemAdded()` / `HandleItemRemoved()`
- публикует `OnItemAdded` / `OnItemRemoved` для внешних подписчиков

Важно:
- для обычных переносов executor dispatch-ит remove/add на основе итогового `InventoryTransferResult`
- remove использует `SourceItem`
- add использует `TargetItem`

Это критично для корректной работы adapter conversion между разными инвентарями.

### InventoryTransferService

Файл: `Scripts/Inventories/InventoryTransferService.cs`

Роль:
- выполняет транзакционный source -> target перенос
- резолвит target-side preview item
- считает acceptable count через `InventoryAcceptanceRequest`
- пытается разместить transfer stack в target inventory
- откатывает source/target snapshot при неудаче

Связанные helper-объекты:
- `TargetPlacementOperation`
- `AlternativeSlotSearchOperation`

### InventoryAcceptanceRequest

Файл: `Scripts/Inventories/InventoryAcceptanceRequest.cs`

Роль:
- описывает preview-проверку в контексте конкретного drag entry
- содержит target inventory, preview item, desired count, source entry и исходный `DragContext`
- позволяет strategy-проверкам валидировать реальные candidate slots, а не абстрактный item без контекста

### InventoryDropProcessor

Файл: `Scripts/Inventories/InventoryDropProcessor.cs`

Это boundary между UI target layer и transfer core.

Роль:
- принимает `DragContext`
- резолвит effective target inventory/slot
- резолвит effective `DropPolicy`
- строит plan через `TransferPlanner`
- исполняет plan через `TransferPlanExecutor`
- возвращает `DropResult` / `TransferExecutionSummary`

## Preview flow

### Area drop / planner preview

1. Source item preview-конвертируется в target item через `TransferItemConversionUtility`
2. Создаётся `InventoryAcceptanceRequest`
3. Strategy вызывает `PassesRules(..., request)` для candidate slots
4. `UniversalInventory.CanAcceptByRules(...)` пересобирает корректный preview `DragContext`

Это устраняет необходимость писать ad-hoc preview guards в feature bindings.

## Интеграция с UI и manager

### DragAndDropManager

Файл: `Scripts/DragAndDropManager.cs`

Manager:
- хранит активный drag context
- резолвит верхний `IDropTarget`
- берёт у target `IDropProcessor`
- вызывает `CanAcceptDrop` и `ProcessDrop`
- публикует swap callbacks (`RaiseSwapAttempting`, `RaiseSwapCompleted`)

### Drop targets

Актуальные target-компоненты:
- `Scripts/Interaction/SlotInputAdapter.cs`
- `Scripts/UI/InventoryDropArea.cs`
- `Scripts/World3D/WorldDropZone.cs`

Все они реализуют `IDropTarget` и предоставляют `IDropProcessor`.

Для inventory UI используется `InventoryDropProcessor`.

## Swap flow

1. Planner сначала пытается построить обычный allocation
2. Если allocation невозможен и policy разрешает swap, entry получает `RequiresSwap`
3. Executor:
   - валидирует оба направления через rules
   - вызывает `SwapAttempting`
   - выполняет `TrySwapSlots`
   - откладывает `SwapCompleted` до конца успешного выполнения

Текущие ограничения:
- swap включен для одиночного entry и полного source stack
- batch swap как отдельный orchestration mode не реализован

## Что это даёт

- единое поведение для slot-drop и area-drop
- тестируемую planning фазу
- rollback-safe atomic batch execution
- корректную target-side adapter conversion уже на этапе preview
- меньше дублирования между swap и обычным transfer
- тонкий UI target code

## Рекомендованные проверки

1. `TransferPlanner` для policy matrix
2. `TransferPlanExecutor` atomic rollback и deferred events
3. `InventoryDropProcessor` effective policy resolution
4. area-drop и slot-drop должны давать одинаковый результат при одинаковом `DropPolicy`
5. cross-inventory adapter conversion должна давать корректный `TargetItem` в add event
