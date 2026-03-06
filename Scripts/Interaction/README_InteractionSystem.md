# Interaction Input System

**Last Updated**: 2026-03-06

Документ описывает актуальный input pipeline для UI-инвентарей после миграции с legacy listeners.

## Цель

Свести мышь, navigation и `InputAction` к одному маршруту:

1. `SlotInputAdapter` собирает raw UI-события.
2. `InputEventRouter` маршрутизирует их в контекст нужного инвентаря.
3. Bindings из `InventoryExtraInteractionBinder` или `InteractionBindingsProfile` выбирают `SlotInteractionAction`.
4. Action вызывает drag, selection, context menu или scene inventory action.

Legacy-компоненты (`DragDropEventListener`, `SlotPointerSelectionTrigger`) больше не являются целевой схемой.

## Компоненты

### SlotInputAdapter

Файл: `Scripts/Interaction/SlotInputAdapter.cs`

Роль:
- принимает `IPointer*`, `IBeginDrag`, `ISelect/IDeselect`, `ISubmit/ICancel`;
- прокидывает события в `InputEventRouter`;
- реализует `IDropTarget` и создает `InventoryDropProcessor` для slot-drop;
- не содержит доменной логики drag, selection или context menu.

### InputEventRouter

Файл: `Scripts/Interaction/InputEventRouter.cs`

Роль:
- хранит runtime-state по инвентарям;
- разрешает pointer/navigation/input-action bindings;
- отслеживает focus/pressed state;
- классифицирует pointer phases: `Down`, `Up`, `Click`, `ClickShort`, `ClickLong`;
- изолирует drag-only обработку на pointer-up;
- выполняет anti-dup для action intents.

### InventoryExtraInteractionBinder

Файл: `Scripts/Interaction/InventoryExtraInteractionBinder.cs`

Роль:
- хранит локальные `PointerBinding`, `NavigationBinding`, `InputActionBinding`;
- опционально добавляет bindings из profile asset;
- может подключать глобальный `InputEventRouter.DefaultBindingsProfile`;
- используется как per-inventory override.

Важно:
- в `InteractionBindingsProfile` доступны только `AssetOnlySlotInteractionAction`;
- в локальных bindings доступны любые `SlotInteractionAction`, включая scene-bound действия.

### InteractionBindingsProfile

Файл: `Scripts/Interaction/InteractionBindingsProfile.cs`

SO-профиль для переиспользуемых bindings.

Использование:
- глобально через `InputEventRouter.DefaultBindingsProfile`;
- локально через `InventoryExtraInteractionBinder._bindingsProfile`.

## Поддерживаемые actions

### Interaction namespace

Файл: `Scripts/Interaction/SlotInteractionActions.cs`

- `DragSlotAction`
- `CompleteDragAction`
- `CancelDragAction`
- `InventorySlotAction`

### Selection namespace

Файлы:
- `Scripts/Selection/SelectionSlotAction.cs`
- `Scripts/Selection/StartMultiDragAction.cs`

- `SelectionSlotAction`
- `StartMultiDragAction`

### ContextMenu namespace

Файл:
- `Scripts/ContextMenu/ShowContextMenuAction.cs`

- `ShowContextMenuAction`

## Поток событий

### Pointer

`EventSystem -> SlotInputAdapter -> InputEventRouter -> PointerBinding -> SlotInteractionAction`

### InputAction

`InputAction -> InputEventRouter (bindings from InventoryExtraInteractionBinder or profile) -> SlotInteractionAction`

### Navigation focus

`ISelect/IDeselect -> SlotInputAdapter -> InputEventRouter`

## Pointer phases

Pointer bindings поддерживают:
- `Any`
- `Down`
- `Up`
- `Click`
- `ClickShort`
- `ClickLong`

Рекомендации:
- start drag: `Down`
- complete drag: `Up`
- selection/context menu: `ClickShort`
- special alt behavior: `ClickLong`

## Типовые конфигурации

### Базовый drag&drop

`Pointer Bindings`:
- `LMB + Down -> DragSlotAction`
- `LMB + Up -> CompleteDragAction`

`Navigation Bindings`:
- `Submit -> DragSlotAction`
- `Cancel -> CancelDragAction`

### Multi-selection + drag на другой кнопке

`Pointer Bindings`:
- `LMB + None + ClickShort -> SelectionSlotAction(ClearAndSelectOperation)`
- `LMB + Ctrl + ClickShort -> SelectionSlotAction(ToggleSlotOperation)`
- `LMB + Shift + ClickShort -> SelectionSlotAction(RangeSelectOperation)`
- `RMB + Down -> DragSlotAction`
- `RMB + Up -> CompleteDragAction`

### Context menu

`Pointer Bindings`:
- `RMB + ClickShort -> ShowContextMenuAction`

`Navigation Bindings`:
- `Cancel -> ShowContextMenuAction`

### Scene-specific inventory actions

`InputAction Bindings`:
- `QuickMove -> InventorySlotAction(scene AutoTransferAction)`
- `Sort -> InventorySlotAction(scene SortInventoryAction)`

## Runtime notes

- `InventorySlotAction` и `ShowContextMenuAction` используют `adapter?.Slot ?? inventory.ResolveAutoTransferSlot()`.
- `ResolveAutoTransferSlot()` сейчас ищет слот в порядке: `hover -> EventSystem.currentSelectedGameObject -> lastInteracted`.
- `InputEventRouter` хранит текущий `FocusSource`, который используется и для context menu actions без pointer event.

## Prefabs и сцены

Slot prefabs:
- `Prefabs/Slot.prefab`
- `Prefabs/Selectable Slot.prefab`

Ожидаемые компоненты:
- `UniversalSlot`
- `SlotInputAdapter`

В сцене должен присутствовать `InputEventRouter` обычно через `Prefabs/DragCanvas.prefab`.

## Debug checklist

1. На слоте есть `UniversalSlot` и `SlotInputAdapter`.
2. На инвентаре есть `InventoryExtraInteractionBinder`, если нужны локальные bindings.
3. В сцене есть `InputEventRouter`.
4. Для drag completion настроен отдельный binding на `Up`.
5. Для selection/context menu pointer binding использует `ClickShort`, а не `Any`.
