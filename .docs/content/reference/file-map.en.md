# File Map

A quick reference for finding the file you need.

## Core

| File | Description |
|------|-------------|
| `Scripts/DragAndDropManager.cs` | Main singleton, drag lifecycle, swap events |
| `Scripts/Core/DropPolicy.cs` | Drop behavior configuration (4 policy dimensions) |
| `Scripts/Core/DragContext.cs` | Runtime drag state (entries, source, target) |
| `Scripts/Core/IInventoryItem.cs` | Base item interface (ItemId, Icon, DisplayName) |
| `Scripts/Core/IDropTarget.cs` | Drop target interface |
| `Scripts/Core/IDropProcessor.cs` | Drop processor interface |
| `Scripts/Core/ItemStack.cs` | Item + count wrapper with splitting and merging |
| `Scripts/Core/InventoryEvents.cs` | Event context types (add, remove, swap) |
| `Scripts/Core/IFilterable.cs` | Interface for filtering and sorting support |
| `Scripts/Core/IDescribable.cs` | Description interface for tooltips |

## Transfer Pipeline

| File | Description |
|------|-------------|
| `Scripts/Inventories/TransferPlanner.cs` | Builds an immutable transfer plan |
| `Scripts/Inventories/TransferPlanExecutor.cs` | Executes the plan with rollback support |
| `Scripts/Inventories/InventoryTransferService.cs` | Low-level transfer primitive |
| `Scripts/Inventories/InventoryDropProcessor.cs` | Adapter between UI and pipeline |
| `Scripts/Inventories/TransferItemConversionUtility.cs` | Cross-inventory item conversion |
| `Scripts/Inventories/EntryPlanningOperation.cs` | Planning helper for each entry |
| `Scripts/Inventories/TargetPlacementOperation.cs` | Slot placement helper |
| `Scripts/Inventories/AlternativeSlotSearchOperation.cs` | Alternative slot search |
| `Scripts/Inventories/VirtualSlotState.cs` | Virtual slot tracking for batch planning |
| `Scripts/Inventories/SlotOperationContext.cs` | Slot operation context |

## Inventory and Slots

| File | Description |
|------|-------------|
| `Scripts/Inventories/UniversalInventory.cs` | Main inventory component |
| `Scripts/Inventories/IInventory.cs` | Inventory interface |
| `Scripts/Slots/UniversalSlot.cs` | Visual slot with stack display |
| `Scripts/Inventories/AutoTransferService.cs` | Quick transfer on click logic |

## Strategies

| File | Description |
|------|-------------|
| `Scripts/Inventories/Strategies/IInventoryStrategy.cs` | Strategy interface |
| `Scripts/Inventories/Strategies/InventoryStrategyBase.cs` | Base class with rule checking |
| `Scripts/Inventories/Strategies/UniqueItemStrategy.cs` | One item per slot |
| `Scripts/Inventories/Strategies/StackableItemStrategy.cs` | Automatic stack merging |
| `Scripts/Inventories/Strategies/SeparableStacksStrategy.cs` | Separate stacks with merge capability |
| `Scripts/Inventories/Strategies/DynamicSlotDecorator.cs` | Decorator for dynamic slot creation |

## Data Binding

| File | Description |
|------|-------------|
| `Scripts/DataBinding/InventoryDataBindingBase.cs` | Abstract base: synchronization and validation |
| `Scripts/DataBinding/ListInventoryDataBinding.cs` | Template for list-based data source |
| `Scripts/DataBinding/MappedSlotInventoryDataBinding.cs` | Template for declarative slot-to-data binding |
| `Scripts/DataBinding/SlotIndexedInventoryDataBinding.cs` | Template for index-based slot storage |

## Rules

| File | Description |
|------|-------------|
| `Scripts/Rules/IDragRule.cs` | Rule interface with priority |
| `Scripts/Rules/RuleValidator.cs` | Rule evaluation engine |
| `Scripts/Rules/BuiltInRules.cs` | Built-in rules |
| `Scripts/Rules/CompositeRule.cs` | Composite rule (Composite pattern) |
| `Scripts/Rules/Presets/RulePreset.cs` | ScriptableObject container for rules |
| `Scripts/Rules/RuleNameFilter.cs` | Rule filtering by name |

## Input and Interaction

| File | Description |
|------|-------------|
| `Scripts/Interaction/InputEventRouter.cs` | Input routing and phase detection |
| `Scripts/Interaction/SlotInputAdapter.cs` | Input handler for each slot |
| `Scripts/Interaction/InteractionBindingsProfile.cs` | Phase-to-action mapping |
| `Scripts/Interaction/InventoryExtraInteractionBinder.cs` | Additional interaction configuration |
| `Scripts/Interaction/Bindings/PointerBinding.cs` | Pointer input binding |

## Selection

| File | Description |
|------|-------------|
| `Scripts/Selection/SelectionManager.cs` | Selection state manager |
| `Scripts/Selection/SelectionContext.cs` | Selection context data |
| `Scripts/Selection/SlotSelectionView.cs` | Visual highlight for selected slot |
| `Scripts/Selection/StartMultiDragAction.cs` | Batch drag from selection |
| `Scripts/Selection/Operations/*.cs` | Selection operations (toggle, range, etc.) |
| `Scripts/Selection/Triggers/*.cs` | Input triggers for selection |

## UI

| File | Description |
|------|-------------|
| `Scripts/UI/TooltipManager.cs` | Tooltip lifecycle manager |
| `Scripts/UI/DefaultDragVisual.cs` | Simple drag visual |
| `Scripts/UI/FancyDragVisual.cs` | Enhanced drag visual with effects |
| `Scripts/UI/DragVisualPresenter.cs` | Drag visual display logic |
| `Scripts/UI/InventoryDropArea.cs` | Background area for dropping items |

## Context Menu

| File | Description |
|------|-------------|
| `Scripts/ContextMenu/ShowContextMenuAction.cs` | Context menu invocation |
| `Scripts/ContextMenu/ContextMenuPreset.cs` | Menu entry set |
| `Scripts/ContextMenu/ContextMenuEntryDefinitionSO.cs` | Base ScriptableObject for menu entry |
| `Scripts/ContextMenu/ContextMenuViewBase.cs` | Abstract menu view |
| `Scripts/ContextMenu/UI/UniversalContextMenuView.cs` | Default menu implementation |

## Filtering and Sorting

| File | Description |
|------|-------------|
| `Scripts/Filter/FilterSortController.cs` | Main filtering and sorting controller |
| `Scripts/Filter/FilterPreset.cs` | ScriptableObject filter preset |
| `Scripts/Filter/SortPreset.cs` | ScriptableObject sort preset |
| `Scripts/Filter/FilterButton.cs` | UI button for filters |
| `Scripts/Filter/SortButton.cs` | UI button for sorting |

## 3D World

| File | Description |
|------|-------------|
| `Scripts/World3D/WorldDropZone.cs` | Drop zone for items into the world |
| `Scripts/World3D/WorldItem.cs` | Pickable 3D item |
| `Scripts/World3D/IWorld3DAdapter.cs` | Item <-> 3D prefab adapter |
