# Input Architecture Plan (Completed)

**Last Updated**: 2026-03-02
**Status**: Implemented

Этот файл больше не является "планом работ". Миграция завершена, ниже зафиксировано итоговое состояние.

## Что было сделано

1. Введен единый interaction pipeline:
- `SlotInputAdapter` (per-slot)
- `InputEventRouter` (singleton)
- `InventoryInteractionCoordinator` (per-inventory)

2. `InventoryInputHandler` переведен на router-only маршрут.

3. Legacy-компоненты удалены из slot prefab:
- `DragDropEventListener`
- `SlotPointerSelectionTrigger`

4. Demo-сцены обновлены:
- на `UniversalInventory` добавлены `InventoryInteractionCoordinator`
- `InputEventRouter` присутствует через `Prefabs/DragCanvas.prefab`

5. Доработан transfer pipeline для area-drop в dynamic inventory:
- deferred planning для сценариев с созданием новых слотов при исполнении.

## Актуальная документация

Основной документ по текущей системе:
- `Scripts/Interaction/README_InteractionSystem.md`

Связанные документы:
- `Scripts/Inventories/README_TransferPipeline.md`
- `Scripts/Selection/README_SelectionSystem.md`
- `Scripts/Slots/README_SlotHoverSystem.md`

## Текущее целевое состояние

- Ввод/интеракции идут только через interaction pipeline.
- Slot prefab содержит `SlotInputAdapter` и не содержит legacy listeners.
- `InventoryDropArea` не блокирует слоты в idle и работает как цель drop во время drag.

## Примечание

Если нужен исторический план миграции (этапы, шаги, риски), восстановите его из истории git.
