# Transfer Pipeline Architecture

**Last Updated**: 2026-03-06

Документ описывает текущую архитектуру drop/transfer pipeline, включая batch transfer и swap.

## Зачем это сделано

Старая логика переноса была распределена между manager/UI-target кодом и плохо масштабировалась для:
- single vs batch;
- partial vs atomic;
- occupied target;
- swap.

Текущая схема разделяет ответственность:

1. `DropPolicy` определяет поведение.
2. `TransferPlanner` строит план без мутаций.
3. `TransferPlanExecutor` применяет план.
4. `InventoryDropProcessor` связывает UI drop-target с planner/executor.

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
- планирует размещение без изменения данных;
- использует virtual slot state для batch;
- валидирует кандидатов через `RuleEvaluationService`;
- может пометить entry как `RequiresSwap`.

### TransferPlanExecutor

Файл: `Scripts/Inventories/TransferPlanExecutor.cs`

Роль:
- исполняет `TransferPlan`;
- выполняет обычные переносы через `InventoryTransferService`;
- исполняет swap-ветку;
- поддерживает rollback в atomic mode;
- откладывает события до успешного завершения плана.

### InventoryDropProcessor

Файл: `Scripts/Inventories/InventoryDropProcessor.cs`

Это текущая boundary between UI target layer и transfer core.

Роль:
- принимает `DragContext`;
- резолвит effective target inventory/slot;
- резолвит effective `DropPolicy`;
- строит plan через `TransferPlanner`;
- исполняет plan через `TransferPlanExecutor`;
- возвращает `DropResult` или `TransferExecutionSummary`.

## Интеграция с UI и manager

### DragAndDropManager

Файл: `Scripts/DragAndDropManager.cs`

Manager:
- хранит активный drag context;
- резолвит верхний `IDropTarget`;
- берет у target `IDropProcessor`;
- вызывает `CanAcceptDrop` и `ProcessDrop`;
- публикует swap callbacks (`RaiseSwapAttempting`, `RaiseSwapCompleted`).

### Drop targets

Актуальные target-компоненты:
- `Scripts/Interaction/SlotInputAdapter.cs`
- `Scripts/UI/InventoryDropArea.cs`
- `Scripts/World3D/WorldDropZone.cs`

Все они реализуют `IDropTarget` и предоставляют `IDropProcessor`.

Для inventory UI используется `InventoryDropProcessor`.

## Swap flow

1. Planner сначала пытается построить обычный allocation.
2. Если allocation невозможен и policy разрешает swap, entry получает `RequiresSwap`.
3. Executor:
- валидирует оба направления через rules;
- вызывает `SwapAttempting`;
- выполняет `TrySwapSlots`;
- откладывает `SwapCompleted` до конца успешного выполнения.

Текущие ограничения:
- swap включен для одиночного entry и полного source stack;
- batch swap как отдельный orchestration mode не реализован.

## Что это дает

- единое поведение для slot-drop и area-drop;
- тестируемую planning фазу;
- rollback-safe atomic batch execution;
- меньше дублирования между swap и обычным transfer;
- тонкий UI target code.

## Рекомендованные проверки

1. `TransferPlanner` для policy matrix.
2. `TransferPlanExecutor` atomic rollback и deferred events.
3. `InventoryDropProcessor` effective policy resolution.
4. Slot-drop и area-drop должны давать один и тот же результат при одинаковом `DropPolicy`.
