# Interaction Input System

**Last Updated**: 2026-03-02

Документ описывает актуальную (после миграции) систему ввода/взаимодействия для инвентарей.

## Цель

Свести все пользовательские интенты (мышь, UI navigation, Input System actions) к единому маршруту:

1. `SlotInputAdapter` собирает raw-события.
2. `InputEventRouter` маршрутизирует события в нужный инвентарь.
3. `InventoryInteractionCoordinator` принимает решение и вызывает доменные API.

Старая схема на `DragDropEventListener` и `SlotPointerSelectionTrigger` удалена из slot-prefab.

## Компоненты

### SlotInputAdapter (per-slot)

Файл: `Scripts/Interaction/SlotInputAdapter.cs`

Роль:
- принимает `IPointer*`, `IBeginDrag`, `ISelect/IDeselect`, `ISubmit/ICancel`;
- прокидывает события в `InputEventRouter`;
- реализует `IDropTarget` для интеграции с drop stack менеджера;
- не содержит бизнес-логики переноса/выделения.

### InputEventRouter (singleton)

Файл: `Scripts/Interaction/InputEventRouter.cs`

Роль:
- хранит маппинг `IInventory -> InventoryInteractionCoordinator`;
- маршрутизирует события от адаптеров и `InventoryInputHandler`;
- выполняет frame-level anti-dup для action-intents.

### InventoryInteractionCoordinator (per-inventory)

Файл: `Scripts/Interaction/InventoryInteractionCoordinator.cs`

Роль:
- единая state-машина взаимодействия инвентаря;
- держит `FocusedSlot` и источник фокуса (`FocusSource`);
- исполняет `PointerBinding`, `NavigationBinding`, `InputActionBinding`;
- управляет drop target stack (`PushDropTarget/PopDropTarget`) через `DragAndDropManager`;
- маршрутизирует selection- и inventory-actions.

### SlotInteractionActions

Файл: `Scripts/Interaction/SlotInteractionActions.cs`

Поддерживаемые действия:
- `DragSlotAction`
- `CancelDragAction`
- `SelectionSlotAction`
- `InventorySlotAction`

## Поток событий

### Мышь (pointer)

`EventSystem -> SlotInputAdapter -> InputEventRouter -> InventoryInteractionCoordinator -> DragAndDropManager/Selection`

### Геймпад/клавиатура (actions)

`InputAction -> InventoryInputHandler -> InputEventRouter -> InventoryInteractionCoordinator -> InventoryAction/Selection/Drag`

### UI navigation focus

`ISelect/IDeselect -> SlotInputAdapter -> Router -> Coordinator (FocusedSlot)`

## Префабы и сцены

### Slot prefabs

- `Prefabs/Slot.prefab`
- `Prefabs/Selectable Slot.prefab`

Содержат `SlotInputAdapter`.
Legacy-компоненты (`DragDropEventListener`, `SlotPointerSelectionTrigger`) удалены.

### Router и coordinator

- `InputEventRouter` добавлен в `Prefabs/DragCanvas.prefab`;
- `InventoryInteractionCoordinator` должен быть на каждом `UniversalInventory` (в demo-сценах уже добавлен).

## InventoryDropArea

Файл: `Scripts/UI/InventoryDropArea.cs`

Поведение:
- drop-area активна как raycast-target только во время активного drag;
- в обычном состоянии не перехватывает клики слотов;
- поддерживает drop в dynamic inventories, включая сценарии с созданием новых слотов.

## Связанные изменения transfer pipeline

Файлы:
- `Scripts/Inventories/TransferPlanner.cs`
- `Scripts/Inventories/TransferPlanExecutor.cs`

Добавлено deferred-планирование для inventory-area drop (когда таргет-слот не задан и слот может создаться в execution phase).

## Debug checklist

1. На слоте есть `SlotInputAdapter` и `UniversalSlot`.
2. На `UniversalInventory` есть `InventoryInteractionCoordinator`.
3. В сцене присутствует `InputEventRouter` (через `DragCanvas`).
4. В `InventoryInputHandler` action уходит в router (без legacy fallback).
5. Для проблем с дропом в область проверить `InventoryDropArea` и `DropPolicy`.
