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

## Примеры конфигураций действий

Ниже примеры, как настраивать биндинги в `InventoryInteractionCoordinator`.

### Случай 1: Базовый mouse drag&drop (без мультивыделения)

`Pointer Bindings`:
- `LMB + None -> DragSlotAction`

`Navigation Bindings`:
- `Submit -> DragSlotAction`
- `Cancel -> CancelDragAction`

Применение:
- простой инвентарь лута, где нужно только перетаскивание.

### Случай 2: Mouse + мультивыделение (RTS/ARPG стиль)

`Pointer Bindings`:
- `LMB + None -> SelectionSlotAction(ClearAndSelectOperation)`
- `LMB + Ctrl -> SelectionSlotAction(ToggleSlotOperation)`
- `LMB + Shift -> SelectionSlotAction(RangeSelectOperation)`
- `RMB + None -> DragSlotAction`

`Input Action Bindings` (через `InventoryInputHandler`):
- `SelectAll -> SelectionSlotAction(SelectAllOperation)`
- `ClearSelection -> SelectionSlotAction(ClearSelectionOperation)`

Применение:
- нужно отделить выбор слотов и перенос на разные кнопки мыши.

### Случай 3: Gamepad-only inventory

Предусловия:
- слоты навигируемы через `Selectable` и `EventSystem` (`Navigate/Submit/Cancel`).

`Navigation Bindings`:
- `Submit -> DragSlotAction`
- `Cancel -> CancelDragAction`

`Input Action Bindings`:
- `SecondaryInteract -> SelectionSlotAction(ToggleSlotOperation)`
- `SelectAll -> SelectionSlotAction(SelectAllOperation)` (опционально)

Применение:
- консольный UI без мыши.

### Случай 4: Read-only инвентарь (только просмотр и выделение)

`Pointer Bindings`:
- `LMB + None -> SelectionSlotAction(ClearAndSelectOperation)`
- `LMB + Ctrl -> SelectionSlotAction(ToggleSlotOperation)`

Не добавлять `DragSlotAction` в pointer/navigation bindings.

Дополнительно:
- можно назначить правило DataBinding, запрещающее `CanStartDrag`.

Применение:
- витрина магазина, журнал предметов, квестовые списки.

### Случай 5: Два разных профиля в одной сцене

Пример:
- `PlayerInventoryCoordinator`:
  - `LMB -> DragSlotAction`
  - `Ctrl+LMB -> ToggleSlotOperation`
- `MerchantInventoryCoordinator`:
  - `LMB -> SelectionSlotAction(ClearAndSelectOperation)`
  - `RMB -> DragSlotAction`

Важный момент:
- профили задаются на каждом `InventoryInteractionCoordinator` отдельно, а не глобально.

### Случай 6: Auto-transfer и sort по кнопкам

На `InventoryInputHandler`:
- привязать `AutoTransferAction` к action `QuickMove`.
- привязать `SortInventoryAction` к action `Sort`.

Поток:
- `InventoryInputHandler -> InputEventRouter -> InventoryInteractionCoordinator -> InventorySlotAction`.

Применение:
- Diablo-like quick move (`Shift`/`Y`) и сортировка (`R3`/`V`).

### Случай 7: Drag только правой кнопкой, выбор только левой

`Pointer Bindings`:
- `LMB + None -> SelectionSlotAction(ClearAndSelectOperation)`
- `LMB + Ctrl -> SelectionSlotAction(ToggleSlotOperation)`
- `RMB + None -> DragSlotAction`

Применение:
- чтобы случайные клики ЛКМ не начинали перенос.

### Случай 8: Отключить выделение во время drag (по умолчанию)

Ничего отдельно настраивать не нужно:
- координатор уже блокирует selection-интенты в состоянии drag.

Используйте это как базовое поведение, если хотите избежать гонок между переносом и выделением.

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
