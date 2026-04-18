# Карта файлов

Эта страница является полным справочником по коду пакета.

Она специально длиннее других страниц документации: цель здесь не обучить системе с нуля, а помочь быстро найти нужный runtime-, editor- или example-тип без долгого просмотра исходников.

В таблицах ниже перечислены все script-файлы и указано назначение основных классов, интерфейсов, enum'ов и служебных типов, объявленных внутри.

---

## Как пользоваться этой страницей

- Начинайте с подсистемы, которая ближе всего к вашей задаче.
- Открывайте файл из первого столбца.
- По столбцу `Types` быстро проверяйте, что вы попали в нужную точку расширения.
- Скрипты из `Examples/` воспринимайте как образцы интеграции, а не как единственно правильную архитектуру.

---

## Главные точки входа

| Файл | Types | Назначение |
|---|---|---|
| `Scripts/DragAndDropManager.cs` | `DragAndDropManager` | Глобальный менеджер активного drag-and-drop в сцене. Отслеживает lifecycle перетаскивания, текущий context и верхнеуровневую координацию. |
| `Scripts/Inventories/UniversalInventory.cs` | `UniversalInventory`, `ItemBehaviorType`, `SlotManagementType` | Главный компонент инвентаря. Владеет слотами, конфигурацией стратегий, наборами правил и inventory-level поведением переноса. |
| `Scripts/Slots/BaseSlot.cs` | `BaseSlot` | Абстрактная базовая сущность слота, которую используют инвентари, planning и execution. |
| `Scripts/Slots/UniversalSlot.cs` | `UniversalSlot` | Стандартная конкретная реализация слота, используемая пакетом. |
| `Scripts/DataBinding/InventoryDataBindingBase.cs` | `InventoryDataBindingBase` | Базовый binding, связывающий `UniversalInventory` с вашим источником игровых данных. |

---

## Базовые модели и контракты

| Файл | Types | Назначение |
|---|---|---|
| `Scripts/Core/IItemAdapter.cs` | `IItemAdapter` | Минимальное представление предмета, которое хранится в слотах и передаётся через UI-конвейер. |
| `Scripts/Core/IDescribable.cs` | `IDescribable` | Опциональный интерфейс для адаптеров, которые отдают расширенное описание для UI, например tooltip. |
| `Scripts/Core/IFilterable.cs` | `IFilterable`, `ISortable` | Опциональные интерфейсы для систем фильтрации и сортировки. |
| `Scripts/Core/IStackSizeLimitable.cs` | `IStackSizeLimitable` | Опциональное переопределение лимита стака на уровне конкретного предмета. |
| `Scripts/Core/ItemStack.cs` | `ItemStack` | Runtime-модель стака, используемая слотами, planning, swap и execution. |
| `Scripts/Core/DragContext.cs` | `DragContext` | Контекст одного drag-оператора: source entries, target info и флаги текущей операции. |
| `Scripts/Core/ActionResult.cs` | `ActionResult` | Универсальный result-объект для action-style API. |
| `Scripts/Core/DropResult.cs` | `DropResult` | Result-объект, возвращаемый обработчиками drop. |
| `Scripts/Core/InventoryEvents.cs` | `InventoryItemEventContext`, `InventorySwapContext` | Payload-типы для событий переноса и swap. |
| `Scripts/Slots/SlotHoverEventArgs.cs` | `SlotHoverEventArgs` | Данные hover-события слота для UI и связанных подсистем. |

---

## Drop и policy system

| Файл | Types | Назначение |
|---|---|---|
| `Scripts/Core/IDropTarget.cs` | `IDropTarget` | Контракт для любого объекта, который может принимать drop. |
| `Scripts/Core/IDropProcessor.cs` | `IDropProcessor` | Контракт для объектов, умеющих обрабатывать попытку drop. |
| `Scripts/Core/IDropRequestProcessor.cs` | `IDropRequestProcessor` | Специализированный интерфейс для request-driven drop processing. |
| `Scripts/Core/DropAreaBase.cs` | `DropAreaBase` | Базовый класс для не-слотовых drop target'ов, например inventory area или world drop zone. |
| `Scripts/UI/InventoryDropArea.cs` | `InventoryDropArea` | Стандартная drop-area инвентаря на основе `DropAreaBase`. |
| `Scripts/Core/DropPolicy.cs` | `BlockedTargetBehavior`, `DragAmount`, `DragAmountStepRounding`, `BatchMode`, `AlternativePlacementMode`, `DropPolicySettings`, `DropRequestPolicySettings`, `DragRequestPolicySettings` | Enum'ы и settings-объекты, управляющие planning, batch-семантикой, alternative placement и выбором количества предметов. |
| `Scripts/Inventories/IDropPolicyProvider.cs` | `IDropPolicyProvider` | Интерфейс для объектов, которые отдают активные настройки drop policy. |
| `Scripts/Inventories/InventoryAcceptanceRequest.cs` | `InventoryAcceptanceRequest` | Модель запроса на проверку, может ли инвентарь принять входящий предмет или стек. |
| `Scripts/Inventories/InventoryDropProcessor.cs` | `InventoryDropProcessor` | UI-facing точка входа, переводящая drop attempt в planning и execution. |

---

## Слой data binding

| Файл | Types | Назначение |
|---|---|---|
| `Scripts/DataBinding/ListInventoryDataBinding.cs` | `ListInventoryDataBinding<TData, TAdapter>` | Базовый binding для инвентарей, построенных на списке. |
| `Scripts/DataBinding/MappedSlotInventoryDataBinding.cs` | `MappedSlotInventoryDataBinding<TData, TAdapter>` | Базовый binding для именованных или фиксированных semantic slots, например экипировки. |
| `Scripts/DataBinding/SlotIndexedInventoryDataBinding.cs` | `SlotIndexedInventoryDataBinding<TData, TAdapter>` | Базовый binding для slot-indexed данных, например hotbar, crafting grid и фиксированных массивов. |
| `Scripts/DataBinding/GameManagerExample.cs` | `GameManagerExample`, `ItemData` | Небольшой пример, показывающий, как внешний manager может быть источником данных для binding'а. |

---

## Inventory pipeline

| Файл | Types | Назначение |
|---|---|---|
| `Scripts/Inventories/IInventory.cs` | `IInventory` | Базовый контракт инвентаря, который используют planner, validator и сервисы. |
| `Scripts/Inventories/ITransferDomainHandler.cs` | `ITransferDomainHandler` | Синхронные business hooks перед commit и после успешного переноса. |
| `Scripts/Inventories/IAsyncTransferDomainHandler.cs` | `IAsyncTransferDomainHandler` | Асинхронные hooks для pre-commit проверок вроде серверной валидации. |
| `Scripts/Inventories/IItemAdapterConverter.cs` | `IItemAdapterConverter` | Конвертирует item adapter при переносе между инвентарями с разными моделями данных. |
| `Scripts/Inventories/IdentityItemAdapterConverter.cs` | `IdentityItemAdapterConverter` | Pass-through converter для случаев, когда конвертация не нужна. |
| `Scripts/Inventories/TransferKind.cs` | `TransferKind` | Enum, описывающий тип transfer flow. |
| `Scripts/Inventories/TransferDomainContext.cs` | `TransferDomainContext` | Контекст, который передаётся в domain handlers. |
| `Scripts/Inventories/InventoryTransferService.cs` | runtime transfer request/result models | Общие модели transfer request/result, используемые по всему конвейеру. |
| `Scripts/Inventories/InventorySnapshot.cs` | `InventorySnapshot`, `IInventorySnapshotProvider` | Snapshot-модель и provider-интерфейс для безопасного чтения состояния инвентаря. |
| `Scripts/Inventories/InventorySnapshotUtility.cs` | `InventorySnapshotUtility` | Вспомогательные методы для построения и чтения snapshot'ов. |
| `Scripts/Inventories/AutoTransferService.cs` | `AutoTransferService` | Сервис для quick-transfer поведения между инвентарями. |
| `Scripts/Inventories/SlotOperationContext.cs` | `SlotOperationContext` | Low-level контекст, используемый placement и execution helper'ами. |
| `Scripts/Inventories/SwapOperationResult.cs` | `SwapOperationResult` | Result-модель для helper'ов swap planning/execution. |
| `Scripts/Inventories/EntryPlanningOperation.cs` | planning operation types | Внутренний helper planning'а для одного drag entry. |
| `Scripts/Inventories/TargetPlacementOperation.cs` | placement operation types | Внутренний helper, моделирующий решение о размещении на стороне цели. |
| `Scripts/Inventories/SlotRelocationService.cs` | `SlotRelocationService` | Fallback-helper, который пытается освободить или перестроить слоты, если прямое размещение заблокировано. |
| `Scripts/Inventories/VirtualSlotState.cs` | `VirtualSlotState` | Виртуальное состояние слота, используемое при planning без мутации реального UI. |
| `Scripts/Inventories/TransferItemConversionUtility.cs` | `TransferItemConversionUtility` | Внутренний utility, который последовательно применяет конвертацию adapter'ов в planning и execution. |
| `Scripts/Inventories/TransferPlanExecutor.cs` | `TransferExecutionSummary`, `TransferExecutionOptions`, `TransferPlanExecutor` | Выполняет transfer plan, вносит мутации, запускает commit hooks, rollback и deferred events. |
| `Scripts/Inventories/TransferPlanner.cs` | `TransferPlanFailureCode`, `PlanFailure`, `PlannedSwapData`, `PlannedEntryTransfer`, `TransferPlan`, `TransferPlanner` | Строит план переноса без изменения состояния инвентаря. Центральный файл для preview-логики, placement search, swap-решений и диагностики ошибок. |

---

## Стратегии инвентаря

| Файл | Types | Назначение |
|---|---|---|
| `Scripts/Inventories/Strategies/IPlacementStrategy.cs` | `IPlacementStrategy` | Контракт стратегии размещения. |
| `Scripts/Inventories/Strategies/IAcceptanceStrategy.cs` | `IAcceptanceStrategy` | Контракт стратегии оценки вместимости и accept logic. |
| `Scripts/Inventories/Strategies/IDragPolicy.cs` | `IDragPolicy` | Контракт стратегии для решений, связанных с drag amount и drag policy. |
| `Scripts/Inventories/Strategies/IInventoryQueryStrategy.cs` | `IInventoryQueryStrategy` | Контракт стратегии для query/read-операций по инвентарю. |
| `Scripts/Inventories/Strategies/IInventoryStrategy.cs` | `IInventoryStrategy` | Сводный интерфейс, который реализуют конкретные inventory behavior strategy. |
| `Scripts/Inventories/Strategies/InventoryStrategyBase.cs` | `InventoryStrategyBase` | Базовый класс для placement/acceptance стратегий. |
| `Scripts/Inventories/Strategies/UniqueItemStrategy.cs` | `UniqueItemStrategy` | Стратегия, в которой каждый слот хранит отдельный предмет или стек без merge-логики. |
| `Scripts/Inventories/Strategies/StackableItemStrategy.cs` | `StackableItemStrategy` | Стратегия для обычного merge-поведения stackable предметов. |
| `Scripts/Inventories/Strategies/SeparableStacksStrategy.cs` | `SeparableStacksStrategy` | Стратегия для стеков, поддерживающих split и частичные переносы. |
| `Scripts/Inventories/Strategies/DynamicSlotDecorator.cs` | `DynamicSlotDecorator` | Декоратор, добавляющий dynamic slot creation/management поверх другой стратегии. |
| `Scripts/Inventories/Strategies/StrategyConfiguration.cs` | strategy configuration types | Сериализуемая конфигурация стратегии, используемая в `UniversalInventory`. |

---

## Rule system

| Файл | Types | Назначение |
|---|---|---|
| `Scripts/Rules/IDragRule.cs` | `RuleResult`, `IDragRule`, `IGlobalRule`, `IInventoryRule`, `ISlotRule`, `DragRuleBase` | Базовые rule-контракты и result-тип для валидации drag start и drop. |
| `Scripts/Rules/RuleEvaluationService.cs` | `RuleEvaluationService` | High-level сервис, который прогоняет нужные rule validators для одного drag entry. |
| `Scripts/Rules/RuleValidator.cs` | `RuleValidator<TRule>`, `GlobalRuleValidator`, `InventoryRuleValidator`, `SlotRuleValidator` | Helper'ы для хранения и выполнения правил в нужном scope. |
| `Scripts/Rules/CompositeRule.cs` | `CompositeRule<TRule>`, `CompositeGlobalRule`, `CompositeInventoryRule`, `CompositeSlotRule`, `CompositeRuleMode` | Composite-правила, объединяющие несколько child rules через `AND` или `OR`. |
| `Scripts/Rules/BuiltInRules.cs` | `SameSlotRule`, `SameInventoryRule`, `ItemIdFilterRule`, `UniqueItemLimitRule`, `SlotLockRule`, `CustomRule` | Встроенные rule-реализации для типовых ограничений drag-and-drop. |
| `Scripts/Rules/RuleNameFilter.cs` | `RuleNameFilter`, `NameFilterType` | Правило, фильтрующее по имени/паттерну rule и удобное для selective allow/deny поведения. |
| `Scripts/Rules/Presets/RulePreset.cs` | `RulePreset<TRule>` | Generic ScriptableObject-контейнер для переиспользуемых наборов правил. |
| `Scripts/Rules/Presets/GlobalRulePreset.cs` | `GlobalRulePreset` | Preset-asset для global rules. |
| `Scripts/Rules/Presets/InventoryRulePreset.cs` | `InventoryRulePreset` | Preset-asset для inventory-level rules. |
| `Scripts/Rules/Presets/SlotRulePreset.cs` | `SlotRulePreset` | Preset-asset для slot-level rules. |

---

## Interaction и input

| Файл | Types | Назначение |
|---|---|---|
| `Scripts/Interaction/InteractionBindingsProfile.cs` | `InteractionBindingsProfile` | ScriptableObject-профиль, хранящий input bindings для drag, click, hold, context и selection actions. |
| `Scripts/Interaction/InputEventRouter.cs` | `InputEventRouter`, `RuntimeState` | Центральный runtime-router, который сопоставляет bindings и переводит input в slot/inventory actions. |
| `Scripts/Interaction/SlotInputAdapter.cs` | `SlotInputAdapter` | UI-адаптер на визуале слота, чтобы он участвовал в navigation, pointer и routed input. |
| `Scripts/Interaction/FocusSource.cs` | `FocusSource` | Enum, показывающий источник текущего focus. |
| `Scripts/Interaction/InputModalityTracker.cs` | `InputModalityTracker`, `InputModality` | Отслеживает, использует ли игрок pointer- или navigation-style input в legacy и Input System проектах. |
| `Scripts/Interaction/InventoryExtraInteractionBinder.cs` | `InventoryExtraInteractionBinder` | Helper для привязки дополнительных inventory-level действий и интеграций поверх базового slot input. |
| `Scripts/Interaction/HoldDragSettings.cs` | `HoldDragSettings` | ScriptableObject с настройками таймингов и порогов hold-to-drag. |
| `Scripts/Interaction/HoldDragPreviewDisplay.cs` | `HoldDragPreviewDisplay` | Компонент визуальной обратной связи для состояния подготовки hold-drag. |
| `Scripts/Interaction/HoldDragActions.cs` | `StartHoldCountAction`, `StartHoldDragAction` | Slot actions, связанные с hold counting и запуском drag по удержанию. |
| `Scripts/Interaction/SlotInteractionActions.cs` | `SlotInteractionAction`, `AssetSafeSlotInteractionAction`, `DragSlotAction`, `CompleteDragAction`, `CancelDragAction`, `InventorySlotAction` | Основные action-типы, которые input router вызывает для slot/inventory interaction flows. |

---

## Модели input bindings

| Файл | Types | Назначение |
|---|---|---|
| `Scripts/Interaction/Bindings/PointerBinding.cs` | `PointerBinding` | Runtime-описание pointer binding. |
| `Scripts/Interaction/Bindings/KeyBinding.cs` | `KeyBinding` | Runtime-описание legacy key binding. |
| `Scripts/Interaction/Bindings/InputActionBinding.cs` | `InputActionBinding` | Runtime-описание binding для нового Input System. |
| `Scripts/Interaction/Bindings/AssetPointerBinding.cs` | `AssetPointerBinding` | Сериализуемая profile-версия pointer binding. |
| `Scripts/Interaction/Bindings/AssetKeyBinding.cs` | `AssetKeyBinding` | Сериализуемая profile-версия legacy key binding. |
| `Scripts/Interaction/Bindings/AssetInputActionBinding.cs` | `AssetInputActionBinding` | Сериализуемая profile-версия Input System binding. |
| `Scripts/Interaction/Bindings/ModifierKeyHelper.cs` | `ModifierKeyHelper` | Helper для проверки modifier keys в процессе оценки bindings. |

---

## Actions

| Файл | Types | Назначение |
|---|---|---|
| `Scripts/Actions/InventoryActionBase.cs` | `InventoryActionBase` | Базовый класс inventory-wide действий, которые можно вызывать через bindings. |
| `Scripts/Actions/Enums.cs` | `PointerTriggerPhase`, `TriggerPhaseEnum`, `ModifierKey`, `KeyTriggerPhase` | Общие enum'ы для конфигурации bindings и actions. |
| `Scripts/Actions/Inheritors/AutoTransferAction.cs` | `AutoTransferAction` | Inventory action, вызывающий quick transfer поведение. |
| `Scripts/Actions/Inheritors/SortInventoryAction.cs` | `SortInventoryAction` | Inventory action для физической сортировки предметов через `ISlotSorter`. |

---

## Система выделения

| Файл | Types | Назначение |
|---|---|---|
| `Scripts/Selection/SelectionContext.cs` | `SelectionContext` | Runtime-контейнер с текущим состоянием выделения и вспомогательными данными. |
| `Scripts/Selection/SelectionManager.cs` | `SelectionManager` | Глобальный координатор selection-системы для multi-select и multi-drag. |
| `Scripts/Selection/SlotSelectionView.cs` | `SlotSelectionView` | Визуальный компонент, отображающий selection state на слоте. |
| `Scripts/Selection/SelectionSlotAction.cs` | `SelectionSlotAction` | Slot action, встраивающий selection-поведение в input pipeline. |
| `Scripts/Selection/StartMultiDragAction.cs` | `StartMultiDragAction` | Slot action, запускающий drag из текущего selection. |
| `Scripts/Selection/Operations/SelectionOperationBase.cs` | `SelectionOperationBase` | Базовый класс переиспользуемых selection-команд. |
| `Scripts/Selection/Operations/ClearSelectionOperation.cs` | `ClearSelectionOperation` | Очищает текущее выделение. |
| `Scripts/Selection/Operations/ClearAndSelectOperation.cs` | `ClearAndSelectOperation` | Сбрасывает прежний selection и выбирает новый слот/набор. |
| `Scripts/Selection/Operations/SelectSlotOperation.cs` | `SelectSlotOperation` | Выбирает конкретный слот. |
| `Scripts/Selection/Operations/ToggleSlotOperation.cs` | `ToggleSlotOperation` | Переключает состояние выделения у одного слота. |
| `Scripts/Selection/Operations/RangeSelectOperation.cs` | `RangeSelectOperation` | Выделяет диапазон слотов. |
| `Scripts/Selection/Operations/SelectAllOperation.cs` | `SelectAllOperation` | Выделяет все доступные/подходящие слоты. |
| `Scripts/Selection/Operations/SelectByConditionOperation.cs` | `SelectByConditionOperation` | Базовый класс для массового выделения по условию. |
| `Scripts/Selection/Triggers/SelectionTriggerBase.cs` | `SelectionTriggerBase` | Базовый класс trigger-компонентов, которые запускают selection operations. |
| `Scripts/Selection/Triggers/ButtonSelectionTrigger.cs` | `ButtonSelectionTrigger` | UI-button trigger для selection operation. |
| `Scripts/Selection/Triggers/InputActionSelectionTrigger.cs` | `InputActionSelectionTrigger`, `TriggerPhase` | Trigger для selection operation через новый Input System. |

---

## Tooltip system

| Файл | Types | Назначение |
|---|---|---|
| `Scripts/UI/Tooltip/BaseTooltipView.cs` | `BaseTooltipView` | Базовый класс для реализации tooltip UI. |
| `Scripts/UI/Tooltip/DefaultTooltipView.cs` | `DefaultTooltipView` | Готовая реализация tooltip view, поставляемая с пакетом. |
| `Scripts/UI/Tooltip/TooltipManager.cs` | `TooltipManager`, `TooltipAnchor` | Глобальный контроллер tooltip'ов: позиционирование, открытие и скрытие. |

---

## Drag visual и layout

| Файл | Types | Назначение |
|---|---|---|
| `Scripts/UI/IDragVisual.cs` | `IDragVisual` | Интерфейс для реализаций drag visual. |
| `Scripts/UI/DefaultDragVisual.cs` | `DefaultDragVisual` | Базовая реализация drag visual. |
| `Scripts/UI/FancyDragVisual.cs` | `FancyDragVisual` | Более выразительный drag visual с расширенной презентацией/анимацией. |
| `Scripts/UI/DragVisualPresenter.cs` | `DragVisualPresenter`, `VisualInstance` | Глобальный presenter, который создаёт и обновляет активный drag visual. |
| `Scripts/UI/InventoryDragVisualBinder.cs` | `InventoryDragVisualBinder` | Связывает inventory или сцену с выбранной конфигурацией drag visual. |
| `Scripts/UI/FreeFormSlotLayout.cs` | `FreeFormSlotLayout` | Helper для инвентарей, где слоты расположены вручную или не по стандартной grid-схеме. |

---

## Анимация auto transfer

| Файл | Types | Назначение |
|---|---|---|
| `Scripts/Core/AutoTransferAnimationStrategy.cs` | `AutoTransferAnimationStrategy` | Базовый класс стратегий анимации quick transfer. |
| `Scripts/Core/TweenAutoTransferAnimation.cs` | `TweenAutoTransferAnimation` | Tween-based стратегия анимации для auto-transfer эффекта. |
| `Scripts/Core/AutoTransferContext.cs` | `InventoryList` | Вспомогательная модель, используемая в quick-transfer анимации и related flows. |

---

## Context menu system

| Файл | Types | Назначение |
|---|---|---|
| `Scripts/ContextMenu/IContextMenuEntry.cs` | `IContextMenuEntry` | Контракт для любого объекта, который может появиться как пункт context menu. |
| `Scripts/ContextMenu/ContextMenuContext.cs` | context menu context types | Payload-объект, описывающий slot/inventory/item, для которого строится меню. |
| `Scripts/ContextMenu/ContextMenuEntryDefinitionSO.cs` | `ContextMenuEntryDefinitionSO` | Базовый ScriptableObject для переиспользуемых описаний пунктов меню. |
| `Scripts/ContextMenu/ContextMenuSceneEntryBase.cs` | `ContextMenuSceneEntryBase` | Базовый scene-object класс для menu entries, которым нужны runtime scene references. |
| `Scripts/ContextMenu/ContextMenuPreset.cs` | `ContextMenuPreset` | Asset, группирующий несколько menu entries в переиспользуемый preset. |
| `Scripts/ContextMenu/ContextMenuManager.cs` | `ContextMenuManager` | Глобальный менеджер, который строит и открывает context menu. |
| `Scripts/ContextMenu/ContextMenuBinder.cs` | `ContextMenuBinder` | Привязывает menu presets/entries к inventory или scene object. |
| `Scripts/ContextMenu/InventoryContextMenuViewBinder.cs` | `InventoryContextMenuViewBinder` | Соединяет инвентарь с конкретной реализацией context menu view. |
| `Scripts/ContextMenu/ShowContextMenuAction.cs` | `ShowContextMenuAction` | Slot action, который запрашивает меню для текущего slot/context. |
| `Scripts/ContextMenu/UI/ContextMenuViewBase.cs` | `ContextMenuViewBase` | Базовый класс для context menu UI. |
| `Scripts/ContextMenu/UI/UniversalContextMenuView.cs` | `UniversalContextMenuView` | Стандартная реализация context menu UI из пакета. |
| `Scripts/ContextMenu/UI/ContextMenuEntryView.cs` | `ContextMenuEntryView` | UI-элемент одной строки/кнопки пункта меню. |
| `Scripts/ContextMenu/BuiltInEntries/SortContextMenuEntrySO.cs` | `SortContextMenuEntrySO` | Встроенный пункт меню, запускающий сортировку инвентаря. |
| `Scripts/ContextMenu/BuiltInEntries/DebugContextMenuEntrySO.cs` | `DebugContextMenuEntrySO` | Встроенный диагностический/debug-пункт меню. |

---

## Filter и sort UI

| Файл | Types | Назначение |
|---|---|---|
| `Scripts/Filter/ISlotFilter.cs` | `ISlotFilter`, `ISlotSorter`, `FilterPredicate`, `SortComparison` | Базовые интерфейсы и делегаты для фильтрации и сортировки. |
| `Scripts/Filter/FilterContext.cs` | `FilterContext` | Контекст, передаваемый в фильтры и сортировщики (Slot, Inventory, AllSlots, SlotIndex). |
| `Scripts/Filter/FilterDisplayMode.cs` | `FilterDisplayMode` | Enum: Hide, Dim, MoveToEnd. |
| `Scripts/Filter/FilterSortController.cs` | `FilterSortController` | Оркестратор: применяет `ISlotFilter` / `ISlotSorter` к UI инвентаря. |
| `Scripts/Filter/FilterSortPreset.cs` | `FilterSortPreset` | ScriptableObject, объединяющий фильтр + сортировщик + режим отображения. |
| `Scripts/Filter/FilterButton.cs` | `FilterButton` | UI-кнопка, применяющая `SlotFilterSO`. |
| `Scripts/Filter/SortButton.cs` | `SortButton` | UI-кнопка, применяющая `SlotSorterSO`. |
| `Scripts/Filter/FilterSortButton.cs` | `FilterSortButton` | UI-кнопка, применяющая комбинированный `FilterSortPreset`. |
| `Scripts/Filter/SlotFilterSO.cs` | `SlotFilterSO` | SO-обёртка для переиспользуемых ассетов `ISlotFilter`. |
| `Scripts/Filter/SlotSorterSO.cs` | `SlotSorterSO` | SO-обёртка для переиспользуемых ассетов `ISlotSorter`. |
| `Scripts/Filter/Filters/*.cs` | `CategoryFilter`, `RarityRangeFilter`, `NameSearchFilter`, `CompositeFilter` | Встроенные `[Serializable]` реализации фильтров. |
| `Scripts/Filter/Sorters/*.cs` | `NameSorter`, `CategorySorter`, `RaritySorter`, `SortValueSorter`, `StackCountSorter`, `CompositeSorter` | Встроенные `[Serializable]` реализации сортировщиков. |

---

## Утилиты

| Файл | Types | Назначение |
|---|---|---|
| `Scripts/Tools/MonoSingleton.cs` | `MonoSingleton<T>` | Generic MonoBehaviour singleton base для scene-global менеджеров. |
| `Scripts/Tools/Extensions.cs` | `Extensions` | Общие extension methods, используемые по runtime-коду. |
| `Scripts/Tools/MiniTweenRunner.cs` | `MiniTweenEase`, `MiniTweenRunner`, `ActiveTween` | Лёгкий tween-runner для простых runtime-анимаций. |

---

## Inspector attributes и editor tooling

| Файл | Types | Назначение |
|---|---|---|
| `Scripts/Tools/Inspector/InspectorAttributes.cs` | `InfoMessageType`, `TitleAlignments`, `FoldoutGroupAttribute`, `InfoBoxAttribute`, `RequiredAttribute`, `ShowIfAttribute`, `HideLabelAttribute`, `ButtonAttribute`, `DisableInEditorModeAttribute`, `EnumToggleButtonsAttribute`, `LabelTextAttribute`, `ReadOnlyAttribute`, `ShowInInspectorAttribute`, `ListDrawerSettingsAttribute`, `TitleAttribute`, `InlinePropertyAttribute`, `PreviewFieldAttribute`, `TitleGroupAttribute`, `OnValueChangedAttribute`, `ManagedReferencePickerAttribute`, `RulePresetPickerAttribute`, `FixedArraySizeAttribute` | Кастомные inspector attributes, используемые пакетом для улучшения authoring UX. |
| `Scripts/Tools/Inspector/Editor/InspectorDrawers.cs` | `InspectorReflectionUtility`, `FoldoutGroupStateCache`, `InspectorPreviewUtility`, `FoldoutGroupStyles`, `GroupedInspectorEditorBase`, `GroupedMonoBehaviourEditor`, `GroupedScriptableObjectEditor`, `ShowIfPropertyDrawer`, `RequiredPropertyDrawer`, `EnumToggleButtonsPropertyDrawer`, `ReadOnlyPropertyDrawer`, `HideLabelPropertyDrawer`, `LabelTextPropertyDrawer`, `InfoBoxDecoratorDrawer`, `TitleDecoratorDrawer`, `TitleGroupDecoratorDrawer`, `PreviewFieldPropertyDrawer`, `ManagedReferencePickerPropertyDrawer`, `RulePresetPickerPropertyDrawer`, `FixedArraySizePropertyDrawer` | Editor-only реализация кастомной inspector-системы и property drawers. |
| `Scripts/Tools/Inspector/Editor/InspectorOdinBridge.cs` | `DragAndDropOdinAttributeProcessor` | Мост для Odin Inspector, чтобы inspector attributes пакета корректно сосуществовали с Odin. |

---

## Пример: Demo1 Inventories

| Файл | Types | Назначение |
|---|---|---|
| `Examples/Demo1 Inventories/ItemExampleSO.cs` | `ItemExampleSO` | Простая ScriptableObject-модель предмета для вводного примера инвентаря. |
| `Examples/Demo1 Inventories/Adapters/ItemAdapterSoAdapter.cs` | `ItemAdapterSoAdapter` | Адаптер, который представляет `ItemExampleSO` внутри inventory system. |
| `Examples/Demo1 Inventories/DataBindings/ItemsSOInventoryDataBinding.cs` | `ItemsSOInventoryDataBinding` | List-based binding, связывающий demo-список предметов с UI-инвентарём. |
| `Examples/Demo1 Inventories/ItemTypeExampleFilterRule.cs` | `ItemTypeExampleFilterRule` | Demo-правило, показывающее ограничение drop по категории/типу предмета. |

---

## Пример: Demo2 Loot

| Файл | Types | Назначение |
|---|---|---|
| `Examples/Demo2 Loot/Scripts/Core/IInteractable.cs` | `IInteractable` | Простой interaction-контракт для world-объектов в loot demo. |
| `Examples/Demo2 Loot/Scripts/Core/Chest.cs` | `Chest` | Интерактивный сундук, владеющий loot data и логикой открытия UI. |
| `Examples/Demo2 Loot/Scripts/Core/ItemController.cs` | `ItemController` | Компонент world item/chest-side interaction logic. |
| `Examples/Demo2 Loot/Scripts/ItemExampleWith3DSO.cs` | `ItemExampleWith3DSO` | ScriptableObject-данные предмета для примера с world loot. |
| `Examples/Demo2 Loot/Scripts/ItemAdapterSoWith3DAdapter.cs` | `ItemAdapterSoWith3DAdapter` | Адаптер для loot demo item'ов, включая filter-данные. |
| `Examples/Demo2 Loot/Scripts/DataBinding/ChestInventoryDataBinding.cs` | `ChestInventoryDataBinding` | Binding содержимого сундука. |
| `Examples/Demo2 Loot/Scripts/DataBinding/PlayerInventoryDataBinding.cs` | `PlayerInventoryDataBinding` | Binding инвентаря игрока в loot demo. |
| `Examples/Demo2 Loot/Scripts/Player/PlayerController.cs` | `PlayerController` | Простой контроллер/движение игрока для сцены. |
| `Examples/Demo2 Loot/Scripts/Player/PlayerInteraction.cs` | `PlayerInteraction` | Делает interact-проверки и открывает/использует world objects. |
| `Examples/Demo2 Loot/Scripts/Player/PlayerInventoryData.cs` | `PlayerInventoryData` | Контейнер backing data для инвентаря игрока в примере. |
| `Examples/Demo2 Loot/Scripts/UI/LootUIController.cs` | `LootUIController` | Открывает/закрывает и обновляет loot UI. |
| `Examples/Demo2 Loot/Scripts/World3D/WorldItem.cs` | `WorldItem` | 3D-представление pickup-предмета в мире. |
| `Examples/Demo2 Loot/Scripts/World3D/WorldDropZone.cs` | `WorldDropZone` | World-space drop target для выбрасывания предметов из UI обратно в сцену. |

---

## Пример: Demo3 Minecraft

| Файл | Types | Назначение |
|---|---|---|
| `Examples/Demo3 Minecraft/Data/MinecraftItemSO.cs` | `MinecraftItemSO` | ScriptableObject-описание предмета для crafting demo. |
| `Examples/Demo3 Minecraft/Data/RuntimeItem.cs` | `RuntimeItem` | Runtime item wrapper/model, используемый в нужных местах примера. |
| `Examples/Demo3 Minecraft/Adapters/MinecraftItemAdapterAdapter.cs` | `MinecraftItemAdapterAdapter` | Адаптер, представляющий предметы crafting demo в inventory UI. |
| `Examples/Demo3 Minecraft/Crafting/CraftingRecipePattern.cs` | `CraftingRecipePattern` | Сериализуемое описание recipe grid/pattern. |
| `Examples/Demo3 Minecraft/Crafting/CraftingRecipeSO.cs` | `CraftingRecipeSO` | ScriptableObject-рецепт. |
| `Examples/Demo3 Minecraft/Crafting/CraftingManager.cs` | `CraftingManager` | Domain-controller примера, который вычисляет рецепты и хранит craft result state. |
| `Examples/Demo3 Minecraft/DataBindings/MainInventoryDataBinding.cs` | `MainInventoryDataBinding` | Binding основного инвентаря игрока. |
| `Examples/Demo3 Minecraft/DataBindings/HotbarDataBinding.cs` | `HotbarDataBinding` | Binding hotbar. |
| `Examples/Demo3 Minecraft/DataBindings/CraftTableDataBinding.cs` | `CraftTableDataBinding` | Binding входных слотов crafting grid. |
| `Examples/Demo3 Minecraft/DataBindings/CraftResultDataBinding.cs` | `CraftResultDataBinding` | Read-oriented binding для выходного craft slot. |
| `Examples/Demo3 Minecraft/Editor/CraftingRecipePatternPropertyDrawer.cs` | `CraftingRecipePatternPropertyDrawer` | Custom editor drawer для удобного редактирования recipe pattern. |

---

## Пример: Demo4 Trading

| Файл | Types | Назначение |
|---|---|---|
| `Examples/Demo4 Trading/Data/ItemType.cs` | `ItemType` | Demo-enum категорий предметов для торговли и экипировки. |
| `Examples/Demo4 Trading/TradableItemSO.cs` | `TradableItemSO` | ScriptableObject-описание предмета у продавца. |
| `Examples/Demo4 Trading/Data/TradableItemModel.cs` | `TradableItemModel` | Runtime-модель торгуемого предмета на стороне игрока. |
| `Examples/Demo4 Trading/Data/PlayerData.cs` | `PlayerData` | Player-side модель денег и inventory data. |
| `Examples/Demo4 Trading/Data/MerchantData.cs` | `MerchantData` | Merchant-side модель inventory/pricing data. |
| `Examples/Demo4 Trading/Data/TradingEconomyManager.cs` | `TradingEconomyManager`, `Merchant` | Demo-manager, который хранит economy state и данные игрока/торговцев. |
| `Examples/Demo4 Trading/Adapters/ITradableItem.cs` | `ITradableItem` | Общий интерфейс для предметов с торговой метаинформацией. |
| `Examples/Demo4 Trading/Adapters/TradableSoAdapter.cs` | `TradableSoAdapter` | Адаптер для merchant-side ScriptableObject item'ов. |
| `Examples/Demo4 Trading/Adapters/TradableItemAdapterModelAdapter.cs` | `TradableItemAdapterModelAdapter` | Адаптер для player/runtime tradable item model. |
| `Examples/Demo4 Trading/Converters/MerchantItemAdapterConverter.cs` | `MerchantItemAdapterConverter` | Конвертирует входящие предметы в merchant-side adapter/data model. |
| `Examples/Demo4 Trading/Converters/ModelItemAdapterConverter.cs` | `ModelItemAdapterConverter` | Конвертирует входящие предметы в player/runtime adapter/data model. |
| `Examples/Demo4 Trading/DataBindings/IMerchantInventory.cs` | `IMerchantInventory` | Marker-interface для merchant inventory bindings/components. |
| `Examples/Demo4 Trading/DataBindings/TradingHelper.cs` | `TradingHelper` | Утилиты торговли: price checks, affordability и side effects. |
| `Examples/Demo4 Trading/DataBindings/PlayerInventoryDataBinding.cs` | `PlayerInventoryDataBinding` | Binding инвентаря игрока с transfer-domain hooks для торговых правил. |
| `Examples/Demo4 Trading/DataBindings/MerchantInventoryDataBinding.cs` | `MerchantInventoryDataBinding` | Binding инвентаря продавца с conversion и trade-specific поведением. |
| `Examples/Demo4 Trading/DataBindings/EquipmentInventoryDataBinding.cs` | `EquipmentInventoryDataBinding` | Binding fixed equipment slots в сцене торговли/экипировки. |
| `Examples/Demo4 Trading/UI/PlayerGoldView.cs` | `PlayerGoldView` | UI-view с текущим количеством золота игрока. |
| `Examples/Demo4 Trading/UI/SelectedPurchasePriceView.cs` | `SelectedPurchasePriceView` | UI-view с ценой текущего выбранного предмета. |

---

## Пример: Demo5 Containers

| Файл | Types | Назначение |
|---|---|---|
| `Examples/Demo5 Containers/Scripts/Data/BaseItemSO.cs` | `BaseItemSO` | Базовое ScriptableObject-описание предмета для container demo. |
| `Examples/Demo5 Containers/Scripts/Data/ContainerItemSO.cs` | `ContainerItemSO` | Описание предмета, который сам содержит другой инвентарь. |
| `Examples/Demo5 Containers/Scripts/Data/IContainerizeItemInstance.cs` | `IContainerizeItemInstance` | Интерфейс runtime item instance, который может вести себя как контейнер. |
| `Examples/Demo5 Containers/Scripts/Data/ItemInstance.cs` | `ItemInstance` | Обычная runtime-реализация item instance. |
| `Examples/Demo5 Containers/Scripts/Data/ContainerItemInstance.cs` | `ContainerItemInstance` | Runtime item instance, владеющий вложенными inventory data. |
| `Examples/Demo5 Containers/Scripts/Adapters/ContainerItemAdapterAdapter.cs` | `ContainerItemAdapterAdapter` | Адаптер, который представляет runtime items container demo в UI-системе. |
| `Examples/Demo5 Containers/Scripts/Bindings/PlayerContainerInventoryDataBinding.cs` | `PlayerContainerInventoryDataBinding` | Binding верхнеуровневого инвентаря игрока в container demo. |
| `Examples/Demo5 Containers/Scripts/Bindings/ContainerInventoryDataBinding.cs` | `ContainerInventoryDataBinding` | Binding текущего открытого вложенного контейнера. |
| `Examples/Demo5 Containers/Scripts/ContainerDemoManager.cs` | `ContainerDemoManager` | Scene-manager, управляющий active container state и координацией демо. |
| `Examples/Demo5 Containers/Scripts/UI/ContainerUIController.cs` | `ContainerUIController` | Управляет открытием, переключением и отображением nested container UI. |
| `Examples/Demo5 Containers/Scripts/ContextMenu/OpenContainerMenuEntrySO.cs` | `OpenContainerMenuEntrySO` | Context menu entry, открывающий container item. |
| `Examples/Demo5 Containers/Scripts/Events.cs` | `Events` | Общие event names/helpers внутри container demo. |

---

## Что открывать первым для типовых задач

| Если вам нужно... | Сначала откройте |
|---|---|
| Привязать свои list-based данные | `InventoryDataBindingBase.cs`, `ListInventoryDataBinding.cs`, любой demo binding из Demo1 или Demo4 |
| Сделать fixed slots для экипировки | `MappedSlotInventoryDataBinding.cs`, `EquipmentInventoryDataBinding.cs` |
| Понять planning drag/drop | `TransferPlanner.cs`, `TransferPlanExecutor.cs`, `InventoryDropProcessor.cs` |
| Добавить свои drag rules | `IDragRule.cs`, `BuiltInRules.cs`, `CompositeRule.cs` |
| Поддержать quick transfer | `AutoTransferService.cs`, `AutoTransferAction.cs`, `AutoTransferAnimationStrategy.cs` |
| Добавить свои input bindings | `InteractionBindingsProfile.cs`, `InputEventRouter.cs`, `SlotInteractionActions.cs` |
| Добавить context menu actions | `IContextMenuEntry.cs`, `ContextMenuEntryDefinitionSO.cs`, `ContextMenuManager.cs` |
| Добавить tooltip content | `IDescribable.cs`, `TooltipManager.cs`, `DefaultTooltipView.cs` |
| Понять world drop integration | `DropAreaBase.cs`, `InventoryDropArea.cs`, `WorldDropZone.cs` |
| Реализовать конвертацию предметов между инвентарями | `IItemAdapterConverter.cs`, `TransferItemConversionUtility.cs`, конвертеры из Demo4 |

