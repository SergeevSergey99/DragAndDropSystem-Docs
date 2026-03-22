# Карта файлов

Краткий справочник для поиска нужного файла.

## Ядро

| Файл | Описание |
|------|----------|
| `Scripts/DragAndDropManager.cs` | Главный синглтон, жизненный цикл перетаскивания, события обмена |
| `Scripts/Core/DropPolicy.cs` | Конфигурация поведения при сбросе (4 измерения политики) |
| `Scripts/Core/DragContext.cs` | Состояние перетаскивания в runtime (записи, источник, цель) |
| `Scripts/Core/IInventoryItem.cs` | Базовый интерфейс предмета (ItemId, Icon, DisplayName) |
| `Scripts/Core/IDropTarget.cs` | Интерфейс цели сброса |
| `Scripts/Core/IDropProcessor.cs` | Интерфейс обработчика сброса |
| `Scripts/Core/ItemStack.cs` | Обёртка предмет + количество с разделением и объединением |
| `Scripts/Core/InventoryEvents.cs` | Типы контекстов событий (добавление, удаление, обмен) |
| `Scripts/Core/IFilterable.cs` | Интерфейс поддержки фильтрации и сортировки |
| `Scripts/Core/IDescribable.cs` | Интерфейс описания для тултипов |

## Конвейер переноса

| Файл | Описание |
|------|----------|
| `Scripts/Inventories/TransferPlanner.cs` | Строит неизменяемый план переноса |
| `Scripts/Inventories/TransferPlanExecutor.cs` | Выполняет план с поддержкой отката |
| `Scripts/Inventories/InventoryTransferService.cs` | Низкоуровневый примитив переноса |
| `Scripts/Inventories/InventoryDropProcessor.cs` | Адаптер между UI и конвейером |
| `Scripts/Inventories/TransferItemConversionUtility.cs` | Кросс-инвентарная конвертация предметов |
| `Scripts/Inventories/EntryPlanningOperation.cs` | Помощник планирования для каждой записи |
| `Scripts/Inventories/TargetPlacementOperation.cs` | Помощник размещения в слот |
| `Scripts/Inventories/AlternativeSlotSearchOperation.cs` | Поиск альтернативного слота |
| `Scripts/Inventories/VirtualSlotState.cs` | Виртуальное отслеживание слотов для пакетного планирования |
| `Scripts/Inventories/SlotOperationContext.cs` | Контекст операции над слотом |

## Инвентарь и слоты

| Файл | Описание |
|------|----------|
| `Scripts/Inventories/UniversalInventory.cs` | Главный компонент инвентаря |
| `Scripts/Inventories/IInventory.cs` | Интерфейс инвентаря |
| `Scripts/Slots/UniversalSlot.cs` | Визуальный слот с отображением стака |
| `Scripts/Inventories/AutoTransferService.cs` | Логика быстрого переноса по клику |

## Стратегии

| Файл | Описание |
|------|----------|
| `Scripts/Inventories/Strategies/IInventoryStrategy.cs` | Интерфейс стратегии |
| `Scripts/Inventories/Strategies/InventoryStrategyBase.cs` | Базовый класс с проверкой правил |
| `Scripts/Inventories/Strategies/UniqueItemStrategy.cs` | Один предмет на слот |
| `Scripts/Inventories/Strategies/StackableItemStrategy.cs` | Автоматическое объединение стаков |
| `Scripts/Inventories/Strategies/SeparableStacksStrategy.cs` | Отдельные стаки с возможностью объединения |
| `Scripts/Inventories/Strategies/DynamicSlotDecorator.cs` | Декоратор для динамического создания слотов |

## Привязка данных

| Файл | Описание |
|------|----------|
| `Scripts/DataBinding/InventoryDataBindingBase.cs` | Абстрактная база: синхронизация и валидация |
| `Scripts/DataBinding/ListInventoryDataBinding.cs` | Шаблон для источника данных на основе списка |
| `Scripts/DataBinding/MappedSlotInventoryDataBinding.cs` | Шаблон для декларативной привязки слотов к данным |
| `Scripts/DataBinding/SlotIndexedInventoryDataBinding.cs` | Шаблон для индексированного хранения в слотах |

## Правила

| Файл | Описание |
|------|----------|
| `Scripts/Rules/IDragRule.cs` | Интерфейс правила с приоритетом |
| `Scripts/Rules/RuleValidator.cs` | Движок вычисления правил |
| `Scripts/Rules/BuiltInRules.cs` | Встроенные правила |
| `Scripts/Rules/CompositeRule.cs` | Составное правило (паттерн Composite) |
| `Scripts/Rules/Presets/RulePreset.cs` | ScriptableObject-контейнер для правил |
| `Scripts/Rules/RuleNameFilter.cs` | Фильтрация правил по имени |

## Ввод и взаимодействие

| Файл | Описание |
|------|----------|
| `Scripts/Interaction/InputEventRouter.cs` | Маршрутизация ввода и определение фаз |
| `Scripts/Interaction/SlotInputAdapter.cs` | Обработчик ввода для каждого слота |
| `Scripts/Interaction/InteractionBindingsProfile.cs` | Маппинг фаз на действия |
| `Scripts/Interaction/InventoryExtraInteractionBinder.cs` | Настройка дополнительных взаимодействий |
| `Scripts/Interaction/Bindings/PointerBinding.cs` | Привязка ввода указателя |

## Выделение

| Файл | Описание |
|------|----------|
| `Scripts/Selection/SelectionManager.cs` | Менеджер состояния выделения |
| `Scripts/Selection/SelectionContext.cs` | Данные контекста выделения |
| `Scripts/Selection/SlotSelectionView.cs` | Визуальная подсветка выделенного слота |
| `Scripts/Selection/StartMultiDragAction.cs` | Пакетное перетаскивание из выделения |
| `Scripts/Selection/Operations/*.cs` | Операции выделения (переключение, диапазон и т.д.) |
| `Scripts/Selection/Triggers/*.cs` | Триггеры ввода для выделения |

## Интерфейс

| Файл | Описание |
|------|----------|
| `Scripts/UI/TooltipManager.cs` | Менеджер жизненного цикла тултипов |
| `Scripts/UI/DefaultDragVisual.cs` | Простой визуал перетаскивания |
| `Scripts/UI/FancyDragVisual.cs` | Улучшенный визуал с эффектами |
| `Scripts/UI/DragVisualPresenter.cs` | Логика отображения визуала перетаскивания |
| `Scripts/UI/InventoryDropArea.cs` | Фоновая область для сброса предметов |

## Контекстное меню

| Файл | Описание |
|------|----------|
| `Scripts/ContextMenu/ShowContextMenuAction.cs` | Вызов контекстного меню |
| `Scripts/ContextMenu/ContextMenuPreset.cs` | Набор пунктов меню |
| `Scripts/ContextMenu/ContextMenuEntryDefinitionSO.cs` | Базовый ScriptableObject пункта меню |
| `Scripts/ContextMenu/ContextMenuViewBase.cs` | Абстрактное представление меню |
| `Scripts/ContextMenu/UI/UniversalContextMenuView.cs` | Реализация меню по умолчанию |

## Фильтрация и сортировка

| Файл | Описание |
|------|----------|
| `Scripts/Filter/FilterSortController.cs` | Главный контроллер фильтрации и сортировки |
| `Scripts/Filter/FilterPreset.cs` | ScriptableObject-пресет фильтра |
| `Scripts/Filter/SortPreset.cs` | ScriptableObject-пресет сортировки |
| `Scripts/Filter/FilterButton.cs` | UI-кнопка для фильтров |
| `Scripts/Filter/SortButton.cs` | UI-кнопка для сортировки |

## 3D мир

| Файл | Описание |
|------|----------|
| `Scripts/World3D/WorldDropZone.cs` | Зона сброса предметов в мир |
| `Scripts/World3D/WorldItem.cs` | Подбираемый 3D-предмет |
| `Scripts/World3D/IWorld3DAdapter.cs` | Адаптер предмет <-> 3D-префаб |
