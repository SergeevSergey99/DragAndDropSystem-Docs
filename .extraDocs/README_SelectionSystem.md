# Selection System

**Last Updated**: 2026-03-06

Selection работает поверх interaction pipeline и не имеет собственного input-router.

## Ключевая идея

- `SelectionManager` хранит состояние выделения.
- `SelectionOperationBase` описывает мутацию selection state.
- `SelectionSlotAction` вызывает operation из bindings.
- `SlotSelectionView` визуализирует результат на UI.

Legacy-компонент `SlotPointerSelectionTrigger` удалён из проекта.

## Архитектура

```text
Pointer / Navigation / InputAction
        |
        v
SlotInputAdapter -> InputEventRouter -> SelectionSlotAction
                                           |
                                           v
                                   SelectionOperationBase
                                           |
                                           v
                                     SelectionManager
                                           |
                                           v
                                     SlotSelectionView
```

## Основные компоненты

### SelectionManager

Файл: `Scripts/Selection/SelectionManager.cs`

Хранит:
- текущее выделение;
- группировку по инвентарям;
- `CurrentContext`;
- `lastSelectedSlot` для range-select.

API:
- `Select(slot)`
- `Deselect(slot)`
- `Toggle(slot)`
- `SelectRange(toSlot)`
- `SelectAll(inventory)`
- `Clear()`
- `IsSelected(slot)`

### SelectionOperationBase

Файл: `Scripts/Selection/Operations/SelectionOperationBase.cs`

Встроенные операции:
- `ClearAndSelectOperation`
- `ToggleSlotOperation`
- `RangeSelectOperation`
- `SelectAllOperation`
- `ClearSelectionOperation`
- `SelectByConditionOperation`

### SelectionSlotAction

Файл: `Scripts/Selection/SelectionSlotAction.cs`

Asset-safe action для interaction bindings.
Берет slot из `SlotInputAdapter` и вызывает выбранную `SelectionOperationBase`.

### SlotSelectionView

Файл: `Scripts/Selection/SlotSelectionView.cs`

Подписывается на `SelectionManager.OnSelectionChanged` и обновляет визуал слота.

## Как настраивать

Selection настраивается через `PointerBinding` / `InputActionBinding`.

Типовой набор:
- `LMB + None + ClickShort -> SelectionSlotAction(ClearAndSelectOperation)`
- `LMB + Ctrl + ClickShort -> SelectionSlotAction(ToggleSlotOperation)`
- `LMB + Shift + ClickShort -> SelectionSlotAction(RangeSelectOperation)`

Action bindings:
- `SelectAll -> SelectionSlotAction(SelectAllOperation)`
- `ClearSelection -> SelectionSlotAction(ClearSelectionOperation)`

Для multi-drag:
- `StartMultiDragAction` может использовать текущее selection как source set.

## Пример реакции на выделение

```csharp
SelectionManager.OnSelectionChanged += context =>
{
    sellButton.interactable = context.HasSelection;
};
```

## Совместимость

- Selection не меняет transfer pipeline напрямую.
- Перенос выполняется через `DragAndDropManager` и `InventoryDropProcessor`.
- Batch actions могут использовать `SelectionContext` как источник слотов.

## См. также

- `Scripts/Interaction/README_InteractionSystem.md`
- `Scripts/Inventories/README_TransferPipeline.md`
- `Scripts/ContextMenu/README_ContextMenu.md`
