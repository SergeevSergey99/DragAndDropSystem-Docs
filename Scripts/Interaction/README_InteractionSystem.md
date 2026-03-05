# Interaction Input System

**Last Updated**: 2026-03-05

Документ описывает актуальную (после миграции) систему ввода/взаимодействия для инвентарей.

## Цель

Свести все пользовательские интенты (мышь, UI navigation, Input System actions) к единому маршруту:

1. `SlotInputAdapter` собирает raw-события.
2. `InputEventRouter` маршрутизирует события в нужный инвентарь.
3. `InventoryExtraInteractionBinder` задает переопределения биндингов для инвентаря, а `InputEventRouter` исполняет их.

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
- хранит маппинг `IInventory -> InventoryExtraInteractionBinder`;
- хранит глобальный `DefaultBindingsProfile` (SO);
- маршрутизирует события от адаптеров и `InventoryInputHandler`;
- выполняет frame-level anti-dup для action-intents.

### InventoryExtraInteractionBinder (per-inventory)

Файл: `Scripts/Interaction/InventoryExtraInteractionBinder.cs`

Роль:
- хранит локальные `PointerBinding`, `NavigationBinding`, `InputActionBinding`;
- опционально добавляет биндинги из `InteractionBindingsProfile`;
- опционально добавляет биндинги из глобального `InputEventRouter.DefaultBindingsProfile`;
- регистрируется в `InputEventRouter` как override для конкретного `UniversalInventory`.

Важно:
- в `InteractionBindingsProfile` (SO) доступны только `AssetOnlySlotInteractionAction`;
- в локальных биндингах `InventoryExtraInteractionBinder` доступны любые `SlotInteractionAction`, включая scene-bound.

## Профили биндингов (SO)

Файл типа:
- `Scripts/Interaction/InteractionBindingsProfile.cs`

Глобальный дефолтный профиль:
- `Settings/DefaultInteractionBindingsProfile.asset` (или любой назначенный в `InputEventRouter.DefaultBindingsProfile`)

Где задаётся:
- на `InputEventRouter` поле `DefaultBindingsProfile`.

Override на конкретном инвентаре:
- `InventoryExtraInteractionBinder._bindingsProfile` — профиль для конкретного инвентаря;
- `InventoryExtraInteractionBinder._useGlobalBindingsProfile` — подключать глобальный профиль роутера;
- локальные биндинги в компоненте всегда добавляются в итоговый набор.

### SlotInteractionActions

Файл: `Scripts/Interaction/SlotInteractionActions.cs`

Поддерживаемые действия:
- `DragSlotAction`
- `CancelDragAction`
- `SelectionSlotAction`
- `InventorySlotAction`

`InventorySlotAction` (local-only) может вызывать:
- сценовый `InventoryActionBase` (MonoBehaviour на объекте сцены/префаба);
- `InventoryActionAssetBase` (ScriptableObject asset для переиспользуемых конфигураций).

`InventoryAssetSlotAction` (SO-safe) вызывает только `InventoryActionAssetBase`.

## Поток событий

### Мышь (pointer)

`EventSystem -> SlotInputAdapter -> InputEventRouter -> (resolved PointerBinding) -> SlotInteractionAction -> DragAndDropManager/Selection`

### Геймпад/клавиатура (actions)

`InputAction -> InventoryInputHandler -> InputEventRouter -> InventoryActionBase`

или

`InputAction -> InputEventRouter (bindings from InventoryExtraInteractionBinder) -> SlotInteractionAction`

### UI navigation focus

`ISelect/IDeselect -> SlotInputAdapter -> InputEventRouter (FocusedSlot)`

## Примеры конфигураций действий

Ниже примеры, как настраивать биндинги в `InventoryExtraInteractionBinder`.

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
- `PlayerInventoryBinder`:
  - `LMB -> DragSlotAction`
  - `Ctrl+LMB -> ToggleSlotOperation`
- `MerchantInventoryBinder`:
  - `LMB -> SelectionSlotAction(ClearAndSelectOperation)`
  - `RMB -> DragSlotAction`

Важный момент:
- профили задаются на каждом `InventoryExtraInteractionBinder` отдельно, а не глобально.

### Случай 6: Auto-transfer и sort по кнопкам

На `InventoryInputHandler`:
- привязать `AutoTransferAction` к action `QuickMove`.
- привязать `SortInventoryAction` к action `Sort`.

Поток:
- `InventoryInputHandler -> InputEventRouter -> InventoryActionBase`.

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
- роутер уже блокирует часть конфликтующих интентов во время drag и разводит pointer-up обработку.

Используйте это как базовое поведение, если хотите избежать гонок между переносом и выделением.

## Префабы и сцены

### Slot prefabs

- `Prefabs/Slot.prefab`
- `Prefabs/Selectable Slot.prefab`

Содержат `SlotInputAdapter`.
Legacy-компоненты (`DragDropEventListener`, `SlotPointerSelectionTrigger`) удалены.

### Router и binder

- `InputEventRouter` добавлен в `Prefabs/DragCanvas.prefab`;
- `InventoryExtraInteractionBinder` добавляется на `UniversalInventory`, где нужны override биндингов.

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
2. На `UniversalInventory` есть `InventoryExtraInteractionBinder`, если для него нужны отдельные биндинги.
3. В сцене присутствует `InputEventRouter` (через `DragCanvas`).
4. В `InventoryInputHandler` action уходит в router (без legacy fallback).
5. Для проблем с дропом в область проверить `InventoryDropArea` и `DropPolicy`.
