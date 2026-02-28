# Transfer Pipeline Architecture (Policy + Planner + Executor)

Документ описывает новую архитектуру переноса предметов в системе DragAndDropSystem,
включая множественный перенос и swap в рамках единого pipeline.

## Зачем это сделано

Старая логика переноса была распределена по нескольким местам и слабо масштабировалась
для комбинаций поведения (single/batch, occupied slot, partial/atomic, swap).

Новая схема разделяет ответственность на три уровня:

1. `DropPolicy` - что система должна делать.
2. `TransferPlanner` - как построить план действий без изменения данных.
3. `TransferPlanExecutor` - как безопасно применить план к инвентарям.

Это позволило:

- централизовать принятие решений;
- сделать поведение предсказуемым для single и batch;
- внедрить atomic rollback для batch;
- встроить swap в тот же pipeline, а не в отдельную ветку с дублированием.

## Основные компоненты

### 1) Policy layer

Файл: `Scripts/Core/DropPolicy.cs`

`DropPolicy` задает параметры поведения:

- `OccupiedTargetPolicy`
- `CapacityPolicy`
- `BatchExecutionPolicy`
- `TargetUsagePolicy`

Поддерживаются дефолтные профили:

- `DropPolicy.SingleDefault`
- `DropPolicy.BatchAtomic`
- `DropPolicy.BatchBestEffort`

Также есть `DropPolicySettings` для Inspector-friendly override.

### 2) Planning layer

Файл: `Scripts/Inventories/TransferPlanner.cs`

`TransferPlanner` принимает `DragContext + target + policy` и возвращает `TransferPlan`.

Ключевые принципы:

- Planner не меняет инвентари (только рассчитывает).
- Для batch используется virtual view слотов (`VirtualSlotState`), чтобы учитывать уже
  запланированные размещения между entry.
- Валидация кандидатов идет через `RuleEvaluationService`.
- При невозможности обычного размещения planner может вернуть `RequiresSwap=true`
  для entry (если policy допускает swap и выполнены условия).

`PlannedEntryTransfer` теперь поддерживает два режима:

- allocation-mode: список `Allocations` + `PlannedAmount`;
- swap-mode: `RequiresSwap=true` + `SwapTargetSlot`.

### 3) Execution layer

Файл: `Scripts/Inventories/TransferPlanExecutor.cs`

`TransferPlanExecutor` применяет `TransferPlan`.

Особенности:

- Поддержка `Atomic` (snapshot + rollback) и `BestEffort`.
- Обычные переносы выполняются через `InventoryTransferService`.
- Swap выполняется в отдельной ветке исполнения (`TryExecuteSwap`).
- Swap и transfer события эмитятся отложенно, только после успешного завершения
  всего плана (важно для корректности при atomic).

## Swap в новой архитектуре

### Как теперь работает swap

1. Planner пытается построить обычный allocation.
2. Если allocation невозможен и policy разрешает swap, planner может поставить
   swap-entry (`RequiresSwap=true`).
3. Executor на этапе исполнения:
   - валидирует swap для обоих направлений через rule-service;
   - вызывает `SwapAttempting` callback (cancelable);
   - выполняет `TrySwapSlots`;
   - откладывает событие `SwapCompleted` до конца успешного выполнения плана.

### Текущие ограничения swap

На текущем этапе swap включен для одиночного entry и полного стака source slot.
Batch-swap (несколько независимых swap-операций в одной drag batch) пока не реализован
как отдельный сценарий.

## Интеграция с UI и manager

### `InventoryDropHandler`

Файл: `Scripts/Inventories/InventoryDropHandler.cs`

Handler теперь:

- строит план через planner;
- исполняет через executor;
- передает в executor `TransferExecutionOptions` (global rules + swap hooks).

### `DragAndDropManager`

Файл: `Scripts/DragAndDropManager.cs`

Manager:

- формирует `InventoryDropHandler` и передает swap callbacks;
- публикует `RaiseSwapAttempting` и `RaiseSwapCompleted` как единые точки
  интеграции с внешними подписчиками.

### Drop targets

- `Scripts/UI/InventoryDropArea.cs`
- `Scripts/Slots/DragDropEventListener.cs`

Оба компонента создают handler с прокидыванием swap callbacks менеджера.

## Изменения по файлам

Ключевые изменения архитектуры затрагивают:

- `Scripts/Core/DropPolicy.cs`
- `Scripts/Inventories/TransferPlanner.cs`
- `Scripts/Inventories/TransferPlanExecutor.cs`
- `Scripts/Inventories/InventoryDropHandler.cs`
- `Scripts/DragAndDropManager.cs`
- `Scripts/UI/InventoryDropArea.cs`
- `Scripts/Slots/DragDropEventListener.cs`

## Что это дает в практическом плане

- Единая модель поведения для всех drop-операций.
- Явная и тестируемая фаза планирования.
- Предсказуемая обработка partial/atomic без скрытых сайд-эффектов.
- Снижение дублирования логики swap/transfer.
- Удобная эволюция policy без переписывания UI-слоя.

## Рекомендованный next step

1. Добавить unit-тесты для `TransferPlanner` (таблица policy-комбинаций).
2. Добавить интеграционные тесты для `TransferPlanExecutor`:
   - atomic rollback;
   - deferred event dispatch;
   - swap cancel / swap success.
3. После стабилизации - расширить swap на batch-сценарии и частичный swap,
   если это нужно продуктовой логике.
