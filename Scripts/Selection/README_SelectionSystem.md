# Selection System

**Last Updated**: 2026-03-02

Система выделения слотов (single/multi-select) поверх нового interaction pipeline.

## Ключевая идея

- `SelectionManager` хранит состояние выделения.
- `SelectionOperationBase` описывает действие над выделением.
- `InventoryInteractionCoordinator` запускает selection-операции через `SelectionSlotAction` в биндингах.

Отдельный `SlotPointerSelectionTrigger` больше не используется в slot-prefab.

## Архитектура

```
Pointer/Navigate/InputAction
        │
        ▼
SlotInputAdapter -> InputEventRouter -> InventoryInteractionCoordinator
                                           │
                                           ▼
                                 SelectionSlotAction
                                           │
                                           ▼
                                   SelectionOperationBase
                                           │
                                           ▼
                                     SelectionManager
                                           │
                                           ▼
                                     SlotSelectionView
```

## Основные компоненты

### SelectionManager

Файл: `Scripts/Selection/SelectionManager.cs`

API:
- `Select(slot)`
- `Deselect(slot)`
- `Toggle(slot)`
- `SelectRange(toSlot)`
- `SelectAll(inventory)`
- `Clear()`
- `CurrentContext`

### SelectionOperationBase

Файл: `Scripts/Selection/Operations/SelectionOperationBase.cs`

Встроенные операции:
- `ClearAndSelectOperation`
- `ToggleSlotOperation`
- `RangeSelectOperation`
- `SelectAllOperation`
- `ClearSelectionOperation`
- `SelectByConditionOperation`

### SlotSelectionView

Файл: `Scripts/Selection/SlotSelectionView.cs`

Визуализирует состояние выделения на слоте.

## Как настраивать теперь

Настройка производится в `InventoryInteractionCoordinator` через pointer/navigation/action bindings.

Типовой набор pointer-биндингов:
- `LMB + None -> SelectionSlotAction(ClearAndSelectOperation)`
- `LMB + Ctrl -> SelectionSlotAction(ToggleSlotOperation)`
- `LMB + Shift -> SelectionSlotAction(RangeSelectOperation)`

Типовой набор action-биндингов:
- `SelectAll action -> SelectionSlotAction(SelectAllOperation)`
- `ClearSelection action -> SelectionSlotAction(ClearSelectionOperation)`

## Пример реакции на выделение

```csharp
SelectionManager.OnSelectionChanged += context =>
{
    // пример: обновить кнопку продажи
    sellButton.interactable = context.HasSelection;
};
```

## Совместимость

Selection не меняет transfer pipeline напрямую.
Перенос/дроп выполняется через `DragAndDropManager` + transfer planner/executor.

## См. также

- `Scripts/Interaction/README_InteractionSystem.md`
- `Scripts/Inventories/README_TransferPipeline.md`
