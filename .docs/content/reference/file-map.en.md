# File Map

This page is a full code map for the package.

It is intentionally longer than the other docs pages: the goal here is not to teach the system, but to help you quickly locate the exact runtime, editor, or example type you need.

The tables below list every script file and describe the main class, interface, enum, or helper types declared inside it.

---

## How to use this page

- Start with the subsystem that matches your task.
- Open the file listed in the first column.
- Use the `Types` column to confirm that you are in the right place.
- Treat example scripts as reference integrations, not mandatory architecture.

---

## Core Entry Points

| File | Types | Role |
|---|---|---|
| `Scripts/DragAndDropManager.cs` | `DragAndDropManager` | Global scene manager for active drags. Tracks drag lifecycle, current context, and high-level orchestration. |
| `Scripts/Inventories/UniversalInventory.cs` | `UniversalInventory`, `ItemBehaviorType`, `SlotManagementType` | Main inventory component. Owns slots, direct `[SerializeReference]` strategy fields, rule collections, and inventory-level transfer behavior. |
| `Scripts/Slots/BaseSlot.cs` | `BaseSlot` | Abstract slot base used by inventories and the transfer engine. |
| `Scripts/Slots/UniversalSlot.cs` | `UniversalSlot` | Default concrete slot implementation used by the package. |
| `Scripts/DataBinding/InventoryDataBindingBase.cs` | `InventoryDataBindingBase` | Base class that connects `UniversalInventory` to your game data source. |

---

## Core Models And Contracts

| File | Types | Role |
|---|---|---|
| `Scripts/Core/Contracts/IItemAdapter.cs` | `IItemAdapter` | Minimal item representation stored in slots and transferred through the UI pipeline. |
| `Scripts/Core/Contracts/IDescribable.cs` | `IDescribable` | Optional interface for adapters that provide extended description data for UI such as tooltips. |
| `Scripts/Core/Contracts/IFilterable.cs` | `IFilterable`, `ISortable` | Optional interfaces for filter and sort systems. |
| `Scripts/Core/Contracts/IStackSizeLimitable.cs` | `IStackSizeLimitable` | Optional per-item stack size override. |
| `Scripts/Core/Models/ItemStack.cs` | `ItemStack` | Runtime stack model used by slots, transfer, and swapping. |
| `Scripts/Core/Models/DragContext.cs` | `DragContext` | Per-drag context containing source entries, target info, and flags for the current operation. |
| `Scripts/Core/Models/ActionResult.cs` | `ActionResult` | Generic result object for action-style APIs. |
| `Scripts/Core/Models/DropResult.cs` | `DropResult` | Result object returned by drop processing. |
| `Scripts/Core/Models/InventoryEvents.cs` | `InventoryItemEventContext`, `InventorySwapContext` | Event payload types used when transfer and swap notifications are dispatched. |
| `Scripts/Slots/SlotHoverEventArgs.cs` | `SlotHoverEventArgs` | Hover event data for slot UI and related systems. |

---

## Drop And Policy System

| File | Types | Role |
|---|---|---|
| `Scripts/Core/Contracts/IDropTarget.cs` | `IDropTarget` | Contract for anything that can receive dragged items. |
| `Scripts/Core/Contracts/IDropProcessor.cs` | `IDropProcessor` | Contract for objects that can process a drop attempt. |
| `Scripts/Core/Contracts/IDropRequestProcessor.cs` | `IDropRequestProcessor` | Specialized processor interface used by request-driven drop handling. |
| `Scripts/Core/Drop/DropAreaBase.cs` | `DropAreaBase` | Base class for non-slot drop targets such as inventory areas or world drop zones. |
| `Scripts/UI/InventoryDropArea.cs` | `InventoryDropArea` | Standard inventory area drop target built on top of `DropAreaBase`. |
| `Scripts/Core/Drop/DropPolicy.cs` | `BlockedTargetResolutionKind`, `PartialTransferMode`, `ResolvedDropPolicy`, `DropRequestPolicy`, `DragRequestPolicy` | Scalar policy values used by the JIT transfer pipeline. |
| `Scripts/Core/Drop/DropPolicySettings.cs` | `DropPolicySettings` | Inventory-level blocked-target, orderer, same-inventory, and partial-transfer defaults. |
| `Scripts/Core/Drop/DropRequestPolicySettings.cs` | `DropRequestPolicySettings` | Serializable authoring helper for one-off drop request overrides in actions and triggers. |
| `Scripts/Core/Drop/DragRequestPolicySettings.cs` | `DragRequestPolicySettings` | Serializable authoring helper for temporary drag-amount overrides when starting a drag. |
| `Scripts/Inventories/IDropPolicyProvider.cs` | `IDropPolicyProvider` | Interface for objects that expose drop policy settings. |
| `Scripts/Inventories/InventoryAcceptanceRequest.cs` | `InventoryAcceptanceRequest` | Request model used when checking whether an inventory can accept an incoming item/stack. |
| `Scripts/Inventories/InventoryDropProcessor.cs` | `InventoryDropProcessor` | UI-facing entry point that resolves policy and starts JIT transfer execution. |

---

## Data Binding Layer

| File | Types | Role |
|---|---|---|
| `Scripts/DataBinding/ListInventoryDataBinding.cs` | `ListInventoryDataBinding<TData, TAdapter>` | Binding base for list-backed inventories. |
| `Scripts/DataBinding/MappedSlotInventoryDataBinding.cs` | `MappedSlotInventoryDataBinding<TData, TAdapter>` | Binding base for named or fixed semantic slots such as equipment layouts. |
| `Scripts/DataBinding/SlotIndexedInventoryDataBinding.cs` | `SlotIndexedInventoryDataBinding<TData, TAdapter>` | Binding base for slot-indexed data such as hotbars, crafting grids, and fixed arrays. |
| `Scripts/DataBinding/GameManagerExample.cs` | `GameManagerExample`, `ItemData` | Example-only sample showing how an external manager can act as the data source for a binding. |

---

## Inventory Pipeline

| File | Types | Role |
|---|---|---|
| `Scripts/Inventories/IInventory.cs` | `IInventory` | Base inventory contract consumed by the transfer engine, validators, and services. |
| `Scripts/Inventories/ITransferDomainHandler.cs` | `ITransferDomainHandler` | Sync pre-commit and post-success business hook interface. |
| `Scripts/Inventories/IAsyncTransferDomainHandler.cs` | `IAsyncTransferDomainHandler` | Async validation hook interface for server/file/network-like commit checks. |
| `Scripts/Inventories/IItemAdapterConverter.cs` | `IItemAdapterConverter` | Converts item adapters when crossing inventory boundaries with different item models. |
| `Scripts/Inventories/IdentityItemAdapterConverter.cs` | `IdentityItemAdapterConverter` | Pass-through converter used when no model conversion is needed. |
| `Scripts/Inventories/TransferKind.cs` | `TransferKind` | Enum describing the type of transfer flow being executed. |
| `Scripts/Inventories/TransferDomainContext.cs` | `TransferDomainContext` | Context object passed into domain handlers. |
| `Scripts/Inventories/InventoryTransferEngine.cs` | `InventoryTransferService`, `TransferEntryRequest` | Sequential JIT transfer service with per-entry rollback, swap, and automatic candidate handling. |
| `Scripts/Inventories/InventoryTransferService.cs` | `TransferProbe` | Advisory probe result used for preview and acceptance. |
| `Scripts/Inventories/PlacementCandidate.cs` | `PlacementCandidate`, `PlacementCandidateKind` | Canonical merge/create/dynamic target descriptor. |
| `Scripts/Inventories/PlacementCandidateOrderer.cs` | placement candidate orderers | Ordering policies used only for automatic placement. |
| `Scripts/Inventories/IPlacementGeometry.cs` | `IPlacementGeometry` | Topology-aware anchor, footprint, occupancy, and covered-slot contract. |
| `Scripts/Inventories/InventorySnapshot.cs` | `InventorySnapshot`, `IInventorySnapshotProvider` | Snapshot model and provider interface used for stable inventory inspection and operations such as sorting or previewing. |
| `Scripts/Inventories/InventorySnapshotUtility.cs` | `InventorySnapshotUtility` | Helper methods for building and reading snapshots. |
| `Scripts/Inventories/AutoTransferService.cs` | `AutoTransferService` | Service that performs quick-transfer style moves between inventories. |
| `Scripts/Inventories/TransferItemConversionUtility.cs` | `TransferItemConversionUtility` | Internal utility that applies target/source adapter conversion consistently across preview and execution. |

---

## Inventory Strategies

| File | Types | Role |
|---|---|---|
| `Scripts/Inventories/Strategies/IStrategy.cs` | `IStrategy` | Read-only contract: explicit-target validation, automatic candidate enumeration, and capacity. |
| `Scripts/Inventories/Strategies/InventoryStrategyBase.cs` | `InventoryStrategyBase` | Base class for read-only candidate-producing strategies. |
| `Scripts/Inventories/Strategies/StackBasedInventoryStrategyBase.cs` | `StackBasedInventoryStrategyBase` | Shared stack-aware base with max stack size and per-item override support. |
| `Scripts/Inventories/Strategies/UniqueItemStrategy.cs` | `UniqueItemStrategy` | Strategy where each slot holds one independent item stack/unit. |
| `Scripts/Inventories/Strategies/StackableItemStrategy.cs` | `StackableItemStrategy` | Strategy focused on regular stack merging behavior. |
| `Scripts/Inventories/Strategies/SeparableStacksStrategy.cs` | `SeparableStacksStrategy` | Strategy for stacks that support splitting and granular partial moves. |
| `Scripts/Inventories/Strategies/SlotManagementSettingsBase.cs` | `SlotManagementSettingsBase` | Base class for direct slot lifecycle modes selected in `UniversalInventory`. |
| `Scripts/Inventories/Strategies/FixedSlotManagementSettings.cs` | `FixedSlotManagementSettings` | Fixed slot lifecycle mode. |
| `Scripts/Inventories/Strategies/DynamicSlotManagementSettings.cs` | `DynamicSlotManagementSettings` | Dynamic slot lifecycle mode with free-slot maintenance and trimming hooks. |
| `Scripts/Inventories/Strategies/StrategyConfiguration.cs` | strategy configuration types | Runtime refresh snapshot for strategy, slot management, and merge-policy configuration. |

---

## Rule System

| File | Types | Role |
|---|---|---|
| `Scripts/Rules/IDragRule.cs` | `RuleResult`, `IDragRule`, `IGlobalRule`, `IInventoryRule`, `ISlotRule`, `DragRuleBase` | Core rule contracts and result type used for drag start and drop validation. |
| `Scripts/Rules/RuleEvaluationService.cs` | `RuleEvaluationService` | High-level service that runs the relevant rule validators for a drag entry. |
| `Scripts/Rules/RuleValidator.cs` | `RuleValidator<TRule>`, `GlobalRuleValidator`, `InventoryRuleValidator`, `SlotRuleValidator` | Rule collection and execution helpers for specific rule scopes. |
| `Scripts/Rules/CompositeRule.cs` | `CompositeRule<TRule>`, `CompositeGlobalRule`, `CompositeInventoryRule`, `CompositeSlotRule`, `CompositeRuleMode` | Composite rules that combine multiple child rules using `AND` or `OR` semantics. |
| `Scripts/Rules/BuiltInRules.cs` | `SameSlotRule`, `SameInventoryRule`, `ItemIdFilterRule`, `UniqueItemLimitRule`, `SlotLockRule`, `CustomRule` | Built-in rule implementations for common drag/drop restrictions. |
| `Scripts/Rules/RuleNameFilter.cs` | `RuleNameFilter`, `NameFilterType` | Rule that filters by rule name/pattern and can be reused for selective allow/deny behavior. |
| `Scripts/Rules/Presets/RulePreset.cs` | `RulePreset<TRule>` | Generic ScriptableObject preset container for reusable rule sets. |
| `Scripts/Rules/Presets/GlobalRulePreset.cs` | `GlobalRulePreset` | Preset asset for global rules. |
| `Scripts/Rules/Presets/InventoryRulePreset.cs` | `InventoryRulePreset` | Preset asset for inventory-scoped rules. |
| `Scripts/Rules/Presets/SlotRulePreset.cs` | `SlotRulePreset` | Preset asset for slot-scoped rules. |

---

## Interaction And Input

| File | Types | Role |
|---|---|---|
| `Scripts/Interaction/InteractionBindingsProfile.cs` | `InteractionBindingsProfile` | ScriptableObject profile that stores input bindings for drag, click, hold, context, and selection actions. |
| `Scripts/Interaction/InputEventRouter.cs` | `InputEventRouter`, `RuntimeState` | Central runtime router that resolves bindings and translates input into slot/inventory actions. |
| `Scripts/Interaction/SlotInputAdapter.cs` | `SlotInputAdapter` | UI-facing adapter attached to slot visuals so they participate in navigation, pointer, and routed input. |
| `Scripts/Interaction/FocusSource.cs` | `FocusSource` | Enum that tracks where current slot focus came from. |
| `Scripts/Interaction/InputModalityTracker.cs` | `InputModalityTracker`, `InputModality` | Tracks whether the player is currently using pointer or navigation-style input in both legacy and Input System projects. |
| `Scripts/Interaction/InventoryExtraInteractionBinder.cs` | `InventoryExtraInteractionBinder` | Helper that binds inventory-level extra actions and integration points beyond basic slot input. |
| `Scripts/Interaction/HoldDragSettings.cs` | `HoldDragSettings` | ScriptableObject for configuring hold-to-drag timings and thresholds. |
| `Scripts/Interaction/HoldDragPreviewDisplay.cs` | `HoldDragPreviewDisplay` | Visual feedback component for hold-drag preparation state. |
| `Scripts/Interaction/HoldDragActions.cs` | `StartHoldCountAction`, `StartHoldDragAction` | Slot interaction actions related to hold counting and hold-triggered drag start. |
| `Scripts/Interaction/SlotInteractionActions.cs` | `SlotInteractionAction`, `AssetSafeSlotInteractionAction`, `DragSlotAction`, `CompleteDragAction`, `CancelDragAction`, `InventorySlotAction` | Core action types invoked by the input router for slot and inventory interaction flows. |

---

## Input Binding Models

| File | Types | Role |
|---|---|---|
| `Scripts/Interaction/Bindings/PointerBinding.cs` | `PointerBinding` | Runtime pointer binding definition. |
| `Scripts/Interaction/Bindings/KeyBinding.cs` | `KeyBinding` | Runtime legacy key binding definition. |
| `Scripts/Interaction/Bindings/LegacyInputActionBinding.cs` | `LegacyInputActionBinding` | Runtime old Input Manager button-name binding definition. |
| `Scripts/Interaction/Bindings/InputActionBinding.cs` | `InputActionBinding` | Runtime Input System binding definition. |
| `Scripts/Interaction/Bindings/AssetPointerBinding.cs` | `AssetPointerBinding` | Serialized pointer binding asset/profile entry. |
| `Scripts/Interaction/Bindings/AssetKeyBinding.cs` | `AssetKeyBinding` | Serialized legacy key binding asset/profile entry. |
| `Scripts/Interaction/Bindings/AssetLegacyInputActionBinding.cs` | `AssetLegacyInputActionBinding` | Serialized old Input Manager button-name binding asset/profile entry. |
| `Scripts/Interaction/Bindings/AssetInputActionBinding.cs` | `AssetInputActionBinding` | Serialized Input System binding asset/profile entry. |
| `Scripts/Interaction/Bindings/ModifierKeyHelper.cs` | `ModifierKeyHelper` | Helper for modifier-key checks used by binding evaluation. |

---

## Actions

| File | Types | Role |
|---|---|---|
| `Scripts/Actions/InventoryActionBase.cs` | `InventoryActionBase` | Base class for inventory-wide actions that can be triggered by bindings. |
| `Scripts/Actions/Enums.cs` | `PointerTriggerPhase`, `TriggerPhaseEnum`, `ModifierKey`, `KeyTriggerPhase` | Shared enums for binding/action trigger configuration. |
| `Scripts/Actions/Inheritors/AutoTransferAction.cs` | `AutoTransferAction` | Inventory action that invokes quick transfer behavior. |
| `Scripts/Actions/Inheritors/SortInventoryAction.cs` | `SortInventoryAction` | Inventory action that physically sorts items using an `ISlotSorter`. |

---

## Selection System

| File | Types | Role |
|---|---|---|
| `Scripts/Selection/SelectionContext.cs` | `SelectionContext` | Runtime container for current slot selection state and helper data. |
| `Scripts/Selection/SelectionManager.cs` | `SelectionManager` | Global selection coordinator used by multi-select and multi-drag flows. |
| `Scripts/Selection/SlotSelectionView.cs` | `SlotSelectionView` | Visual component that renders selection state on a slot. |
| `Scripts/Selection/SelectionSlotAction.cs` | `SelectionSlotAction` | Slot action that integrates selection behavior into the input pipeline. |
| `Scripts/Selection/StartMultiDragAction.cs` | `StartMultiDragAction` | Slot action that starts a drag from the current selection. |
| `Scripts/Selection/Operations/SelectionOperationBase.cs` | `SelectionOperationBase` | Base class for reusable selection commands. |
| `Scripts/Selection/Operations/ClearSelectionOperation.cs` | `ClearSelectionOperation` | Clears the current selection. |
| `Scripts/Selection/Operations/ClearAndSelectOperation.cs` | `ClearAndSelectOperation` | Clears previous selection and selects a new slot/set. |
| `Scripts/Selection/Operations/SelectSlotOperation.cs` | `SelectSlotOperation` | Selects a specific slot. |
| `Scripts/Selection/Operations/ToggleSlotOperation.cs` | `ToggleSlotOperation` | Toggles selection state for one slot. |
| `Scripts/Selection/Operations/RangeSelectOperation.cs` | `RangeSelectOperation` | Selects a range of slots. |
| `Scripts/Selection/Operations/SelectAllOperation.cs` | `SelectAllOperation` | Selects all available/eligible slots. |
| `Scripts/Selection/Operations/SelectByConditionOperation.cs` | `SelectByConditionOperation` | Base class for condition-based bulk selection. |
| `Scripts/Selection/Triggers/SelectionTriggerBase.cs` | `SelectionTriggerBase` | Base class for trigger components that invoke selection operations. |
| `Scripts/Selection/Triggers/ButtonSelectionTrigger.cs` | `ButtonSelectionTrigger` | UI button trigger for a selection operation. |
| `Scripts/Selection/Triggers/InputActionSelectionTrigger.cs` | `InputActionSelectionTrigger`, `TriggerPhase` | Input System trigger for selection operations. |

---

## Tooltip System

| File | Types | Role |
|---|---|---|
| `Scripts/UI/Tooltip/BaseTooltipView.cs` | `BaseTooltipView` | Base class for tooltip rendering implementations. |
| `Scripts/UI/Tooltip/DefaultTooltipView.cs` | `DefaultTooltipView` | Ready-to-use tooltip view implementation. |
| `Scripts/UI/Tooltip/TooltipManager.cs` | `TooltipManager`, `TooltipAnchor` | Global tooltip controller that positions, opens, and hides tooltip views. |

---

## Drag Visuals And Layout

| File | Types | Role |
|---|---|---|
| `Scripts/UI/IDragVisual.cs` | `IDragVisual` | Interface for drag visual implementations. |
| `Scripts/UI/DefaultDragVisual.cs` | `DefaultDragVisual` | Basic drag visual presenter. |
| `Scripts/UI/FancyDragVisual.cs` | `FancyDragVisual` | More stylized drag visual with richer presentation/animation behavior. |
| `Scripts/UI/DragVisualPresenter.cs` | `DragVisualPresenter`, `VisualInstance` | Global presenter that spawns and updates the active drag visual. |
| `Scripts/UI/InventoryDragVisualBinder.cs` | `InventoryDragVisualBinder` | Binds an inventory or scene setup to a chosen drag visual configuration. |
| `Scripts/UI/FreeFormSlotLayout.cs` | `FreeFormSlotLayout` | Layout helper for inventories whose slots are positioned manually or semi-manually rather than in a regular grid. |

---

## Auto Transfer Animation

| File | Types | Role |
|---|---|---|
| `Scripts/Core/AutoTransfer/AutoTransferAnimationStrategy.cs` | `AutoTransferAnimationStrategy` | Base class for quick-transfer animation strategies. |
| `Scripts/Core/AutoTransfer/TweenAutoTransferAnimation.cs` | `TweenAutoTransferAnimation` | Tween-based animation strategy for auto-transfer effects. |
| `Scripts/Core/AutoTransfer/AutoTransferContext.cs` | `InventoryList` | Auto-transfer related helper model used by quick-transfer animation/selection flows. |

---

## Context Menu System

| File | Types | Role |
|---|---|---|
| `Scripts/ContextMenu/IContextMenuEntry.cs` | `IContextMenuEntry` | Contract for anything that can appear as a context menu entry. |
| `Scripts/ContextMenu/ContextMenuContext.cs` | context menu context types | Payload object describing the slot/inventory/item for which a menu is being built. |
| `Scripts/ContextMenu/ContextMenuEntryDefinitionSO.cs` | `ContextMenuEntryDefinitionSO` | ScriptableObject base for reusable menu entry definitions. |
| `Scripts/ContextMenu/ContextMenuSceneEntryBase.cs` | `ContextMenuSceneEntryBase` | Scene object base class for context entries that need runtime scene references. |
| `Scripts/ContextMenu/ContextMenuPreset.cs` | `ContextMenuPreset` | Asset that groups multiple menu entries into a reusable preset. |
| `Scripts/ContextMenu/ContextMenuManager.cs` | `ContextMenuManager` | Global manager that builds and opens context menus. |
| `Scripts/ContextMenu/ContextMenuBinder.cs` | `ContextMenuBinder` | Binds menu presets/entries to an inventory or scene object. |
| `Scripts/ContextMenu/InventoryContextMenuViewBinder.cs` | `InventoryContextMenuViewBinder` | Connects inventories to a concrete context menu view implementation. |
| `Scripts/ContextMenu/ShowContextMenuAction.cs` | `ShowContextMenuAction` | Slot action that requests a menu for the current slot/context. |
| `Scripts/ContextMenu/UI/ContextMenuViewBase.cs` | `ContextMenuViewBase` | Base class for context menu UI implementations. |
| `Scripts/ContextMenu/UI/UniversalContextMenuView.cs` | `UniversalContextMenuView` | Default context menu UI implementation shipped with the package. |
| `Scripts/ContextMenu/UI/ContextMenuEntryView.cs` | `ContextMenuEntryView` | UI element for one menu entry row/button. |
| `Scripts/ContextMenu/BuiltInEntries/SortContextMenuEntrySO.cs` | `SortContextMenuEntrySO` | Built-in menu entry that triggers inventory sorting. |
| `Scripts/ContextMenu/BuiltInEntries/DebugContextMenuEntrySO.cs` | `DebugContextMenuEntrySO` | Built-in diagnostic/debug menu entry. |

---

## Filter And Sort UI

| File | Types | Role |
|---|---|---|
| `Scripts/Filter/ISlotFilter.cs` | `ISlotFilter`, `ISlotSorter`, `FilterPredicate`, `SortComparison` | Core interfaces and delegates for filtering and sorting. |
| `Scripts/Filter/FilterContext.cs` | `FilterContext` | Context struct passed to filters and sorters (Slot, Inventory, AllSlots, SlotIndex). |
| `Scripts/Filter/FilterDisplayMode.cs` | `FilterDisplayMode` | Enum: Hide, Dim, MoveToEnd. |
| `Scripts/Filter/FilterSortController.cs` | `FilterSortController` | Orchestrator: applies `ISlotFilter` / `ISlotSorter` to an inventory UI. |
| `Scripts/Filter/FilterSortPreset.cs` | `FilterSortPreset` | ScriptableObject combining filter + sorter + display mode. |
| `Scripts/Filter/FilterButton.cs` | `FilterButton` | UI button that applies a `SlotFilterSO`. |
| `Scripts/Filter/SortButton.cs` | `SortButton` | UI button that applies a `SlotSorterSO`. |
| `Scripts/Filter/FilterSortButton.cs` | `FilterSortButton` | UI button that applies a combined `FilterSortPreset`. |
| `Scripts/Filter/SlotFilterSO.cs` | `SlotFilterSO` | ScriptableObject wrapper for shareable `ISlotFilter` assets. |
| `Scripts/Filter/SlotSorterSO.cs` | `SlotSorterSO` | ScriptableObject wrapper for shareable `ISlotSorter` assets. |
| `Scripts/Filter/Filters/*.cs` | `CategoryFilter`, `RarityRangeFilter`, `NameSearchFilter`, `CompositeFilter` | Built-in `[Serializable]` filter implementations. |
| `Scripts/Filter/Sorters/*.cs` | `NameSorter`, `CategorySorter`, `RaritySorter`, `SortValueSorter`, `StackCountSorter`, `CompositeSorter` | Built-in `[Serializable]` sorter implementations. |

---

## Utilities

| File | Types | Role |
|---|---|---|
| `Scripts/Tools/MonoSingleton.cs` | `MonoSingleton<T>` | Generic MonoBehaviour singleton base used by scene-global managers. |
| `Scripts/Tools/Extensions.cs` | `Extensions` | Shared extension methods used throughout runtime code. |
| `Scripts/Tools/MiniTweenRunner.cs` | `MiniTweenEase`, `MiniTweenRunner`, `ActiveTween` | Lightweight tween runner used for simple runtime animations. |

---

## Inspector Attributes And Editor Tooling

| File | Types | Role |
|---|---|---|
| `Scripts/Tools/Inspector/InspectorAttributes.cs` | `InfoMessageType`, `TitleAlignments`, `FoldoutGroupAttribute`, `InfoBoxAttribute`, `RequiredAttribute`, `ShowIfAttribute`, `HideLabelAttribute`, `ButtonAttribute`, `DisableInEditorModeAttribute`, `EnumToggleButtonsAttribute`, `LabelTextAttribute`, `ReadOnlyAttribute`, `ShowInInspectorAttribute`, `ListDrawerSettingsAttribute`, `TitleAttribute`, `InlinePropertyAttribute`, `PreviewFieldAttribute`, `TitleGroupAttribute`, `OnValueChangedAttribute`, `ManagedReferencePickerAttribute`, `RulePresetPickerAttribute`, `FixedArraySizeAttribute` | Custom inspector attributes used across the package to improve authoring UX. |
| `Scripts/Tools/Inspector/Editor/InspectorDrawers.cs` | `InspectorReflectionUtility`, `FoldoutGroupStateCache`, `InspectorPreviewUtility`, `FoldoutGroupStyles`, `GroupedInspectorEditorBase`, `GroupedMonoBehaviourEditor`, `GroupedScriptableObjectEditor`, `ShowIfPropertyDrawer`, `RequiredPropertyDrawer`, `EnumToggleButtonsPropertyDrawer`, `ReadOnlyPropertyDrawer`, `HideLabelPropertyDrawer`, `LabelTextPropertyDrawer`, `InfoBoxDecoratorDrawer`, `TitleDecoratorDrawer`, `TitleGroupDecoratorDrawer`, `PreviewFieldPropertyDrawer`, `ManagedReferencePickerPropertyDrawer`, `RulePresetPickerPropertyDrawer`, `FixedArraySizePropertyDrawer` | Editor-only implementation for the custom inspector system and property drawers. |
| `Scripts/Tools/Inspector/Editor/InspectorOdinBridge.cs` | `DragAndDropOdinAttributeProcessor` | Odin bridge so the package inspector attributes coexist cleanly with Odin Inspector. |

---

## Example: Demo1 Inventories

| File | Types | Role |
|---|---|---|
| `Examples/Demo1 Inventories/ItemExampleSO.cs` | `ItemExampleSO` | Simple ScriptableObject item data used by the introductory inventory demo. |
| `Examples/Demo1 Inventories/Adapters/ItemAdapterSoAdapter.cs` | `ItemAdapterSoAdapter` | Adapter that exposes `ItemExampleSO` to the inventory system. |
| `Examples/Demo1 Inventories/DataBindings/ItemsSOInventoryDataBinding.cs` | `ItemsSOInventoryDataBinding` | List-based binding connecting demo item lists to the UI inventory. |
| `Examples/Demo1 Inventories/ItemTypeExampleFilterRule.cs` | `ItemTypeExampleFilterRule` | Demo-specific rule showing how to restrict drops by item category/type. |

---

## Example: Demo2 Loot

| File | Types | Role |
|---|---|---|
| `Examples/Demo2 Loot/Scripts/Core/IInteractable.cs` | `IInteractable` | Simple interaction contract for world objects in the loot demo. |
| `Examples/Demo2 Loot/Scripts/Core/Chest.cs` | `Chest` | Interactable chest that owns loot data and UI-opening behavior. |
| `Examples/Demo2 Loot/Scripts/Core/ItemController.cs` | `ItemController` | World item/chest-side interaction component. |
| `Examples/Demo2 Loot/Scripts/ItemExampleWith3DSO.cs` | `ItemExampleWith3DSO` | ScriptableObject item data for the loot/world example. |
| `Examples/Demo2 Loot/Scripts/ItemAdapterSoWith3DAdapter.cs` | `ItemAdapterSoWith3DAdapter` | Adapter for loot demo items, including filter data. |
| `Examples/Demo2 Loot/Scripts/DataBinding/ChestInventoryDataBinding.cs` | `ChestInventoryDataBinding` | Binding for chest inventory contents. |
| `Examples/Demo2 Loot/Scripts/DataBinding/PlayerInventoryDataBinding.cs` | `PlayerInventoryDataBinding` | Binding for the player's inventory in the loot demo. |
| `Examples/Demo2 Loot/Scripts/Player/PlayerController.cs` | `PlayerController` | Simple player movement/controller for the scene. |
| `Examples/Demo2 Loot/Scripts/Player/PlayerInteraction.cs` | `PlayerInteraction` | Performs interact raycasts/checks and opens/uses world objects. |
| `Examples/Demo2 Loot/Scripts/Player/PlayerInventoryData.cs` | `PlayerInventoryData` | Player-side backing data container for the demo inventory. |
| `Examples/Demo2 Loot/Scripts/UI/LootUIController.cs` | `LootUIController` | Opens/closes and refreshes the loot UI. |
| `Examples/Demo2 Loot/Scripts/World3D/WorldItem.cs` | `WorldItem` | 3D world pickup representation. |
| `Examples/Demo2 Loot/Scripts/World3D/WorldDropZone.cs` | `WorldDropZone` | World-space drop target for dropping items out of the UI back into the scene. |

---

## Example: Demo3 Craft

| File | Types | Role |
|---|---|---|
| `Examples/Demo3 Craft/Data/CraftItemSO.cs` | `CraftItemSO` | ScriptableObject item definition used by the crafting demo. |
| `Examples/Demo3 Craft/Data/RuntimeItem.cs` | `RuntimeItem` | Runtime item wrapper/model used by the demo where needed. |
| `Examples/Demo3 Craft/Adapters/CraftItemAdapterAdapter.cs` | `CraftItemAdapterAdapter` | Adapter exposing crafting demo items to the inventory UI. |
| `Examples/Demo3 Craft/Crafting/CraftingRecipePattern.cs` | `CraftingRecipePattern` | Serialized recipe grid/pattern definition. |
| `Examples/Demo3 Craft/Crafting/CraftingRecipeSO.cs` | `CraftingRecipeSO` | ScriptableObject recipe asset. |
| `Examples/Demo3 Craft/Crafting/CraftingManager.cs` | `CraftingManager` | Demo domain controller that evaluates recipes and owns craft result state. |
| `Examples/Demo3 Craft/DataBindings/MainInventoryDataBinding.cs` | `MainInventoryDataBinding` | Binding for the main player inventory. |
| `Examples/Demo3 Craft/DataBindings/HotbarDataBinding.cs` | `HotbarDataBinding` | Binding for the hotbar. |
| `Examples/Demo3 Craft/DataBindings/CraftTableDataBinding.cs` | `CraftTableDataBinding` | Binding for the crafting grid input slots. |
| `Examples/Demo3 Craft/DataBindings/CraftResultDataBinding.cs` | `CraftResultDataBinding` | Read-oriented binding for the crafting output slot. |
| `Examples/Demo3 Craft/Editor/CraftingRecipePatternPropertyDrawer.cs` | `CraftingRecipePatternPropertyDrawer` | Custom editor drawer for recipe pattern authoring. |

---

## Example: Demo4 Trading

| File | Types | Role |
|---|---|---|
| `Examples/Demo4 Trading/Data/ItemType.cs` | `ItemType` | Demo enum describing trading/equipment item categories. |
| `Examples/Demo4 Trading/TradableItemSO.cs` | `TradableItemSO` | ScriptableObject merchant item definition. |
| `Examples/Demo4 Trading/Data/TradableItemModel.cs` | `TradableItemModel` | Runtime tradable item model used on the player side. |
| `Examples/Demo4 Trading/Data/PlayerData.cs` | `PlayerData` | Player-side money/inventory data model. |
| `Examples/Demo4 Trading/Data/MerchantData.cs` | `MerchantData` | Merchant-side inventory/pricing data model. |
| `Examples/Demo4 Trading/Data/TradingEconomyManager.cs` | `TradingEconomyManager`, `Merchant` | Demo manager that stores economy state and merchant/player trading data. |
| `Examples/Demo4 Trading/Adapters/ITradableItem.cs` | `ITradableItem` | Shared interface for anything with trading-specific metadata. |
| `Examples/Demo4 Trading/Adapters/TradableSoAdapter.cs` | `TradableSoAdapter` | Adapter for merchant-side ScriptableObject items. |
| `Examples/Demo4 Trading/Adapters/TradableItemAdapterModelAdapter.cs` | `TradableItemAdapterModelAdapter` | Adapter for player/runtime tradable item models. |
| `Examples/Demo4 Trading/Converters/MerchantItemAdapterConverter.cs` | `MerchantItemAdapterConverter` | Converts incoming items into the merchant-side adapter/data model. |
| `Examples/Demo4 Trading/Converters/ModelItemAdapterConverter.cs` | `ModelItemAdapterConverter` | Converts incoming items into the player/runtime adapter/data model. |
| `Examples/Demo4 Trading/DataBindings/IMerchantInventory.cs` | `IMerchantInventory` | Marker interface for merchant inventory bindings/components. |
| `Examples/Demo4 Trading/DataBindings/TradingHelper.cs` | `TradingHelper` | Trading utility methods for price checks, affordability, and side effects. |
| `Examples/Demo4 Trading/DataBindings/PlayerInventoryDataBinding.cs` | `PlayerInventoryDataBinding` | Player inventory binding with transfer-domain hooks for trading rules. |
| `Examples/Demo4 Trading/DataBindings/MerchantInventoryDataBinding.cs` | `MerchantInventoryDataBinding` | Merchant inventory binding with conversion and trade-specific behavior. |
| `Examples/Demo4 Trading/DataBindings/EquipmentInventoryDataBinding.cs` | `EquipmentInventoryDataBinding` | Equipment-slot binding for the trading/equipment scene. |
| `Examples/Demo4 Trading/UI/PlayerGoldView.cs` | `PlayerGoldView` | UI view showing current player gold. |
| `Examples/Demo4 Trading/UI/SelectedPurchasePriceView.cs` | `SelectedPurchasePriceView` | UI view showing the price of the currently selected item. |

---

## Example: Demo5 Containers

| File | Types | Role |
|---|---|---|
| `Examples/Demo5 Containers/Scripts/Data/BaseItemSO.cs` | `BaseItemSO` | Base ScriptableObject item definition for the container demo. |
| `Examples/Demo5 Containers/Scripts/Data/ContainerItemSO.cs` | `ContainerItemSO` | Item definition for items that contain another inventory. |
| `Examples/Demo5 Containers/Scripts/Data/IContainerizeItemInstance.cs` | `IContainerizeItemInstance` | Interface for runtime item instances that may expose container behavior. |
| `Examples/Demo5 Containers/Scripts/Data/ItemInstance.cs` | `ItemInstance` | Normal runtime item instance implementation. |
| `Examples/Demo5 Containers/Scripts/Data/ContainerItemInstance.cs` | `ContainerItemInstance` | Runtime item instance that owns nested inventory data. |
| `Examples/Demo5 Containers/Scripts/Adapters/ContainerItemAdapterAdapter.cs` | `ContainerItemAdapterAdapter` | Adapter exposing container demo runtime items to the UI system. |
| `Examples/Demo5 Containers/Scripts/Bindings/PlayerContainerInventoryDataBinding.cs` | `PlayerContainerInventoryDataBinding` | Binding for the player's top-level inventory in the container demo. |
| `Examples/Demo5 Containers/Scripts/Bindings/ContainerInventoryDataBinding.cs` | `ContainerInventoryDataBinding` | Binding for the currently opened nested container inventory. |
| `Examples/Demo5 Containers/Scripts/ContainerDemoManager.cs` | `ContainerDemoManager` | Scene manager that owns active container state and demo coordination. |
| `Examples/Demo5 Containers/Scripts/UI/ContainerUIController.cs` | `ContainerUIController` | Controls opening, switching, and presenting nested container UI. |
| `Examples/Demo5 Containers/Scripts/ContextMenu/OpenContainerMenuEntrySO.cs` | `OpenContainerMenuEntrySO` | Context menu entry that opens a container item. |
| `Examples/Demo5 Containers/Scripts/Events.cs` | `Events` | Shared event names/helpers used inside the container demo. |

---

## Example: Demo6 Shaped Items

| File | Types | Role |
|---|---|---|
| `Examples/Demo6 Shaped Items/ShapedItemExampleSO.cs` | `ShapedItemExampleSO` | Base ScriptableObject definition for rectangular shaped items through width/height. |
| `Examples/Demo6 Shaped Items/ComplexShapedItemExampleSO.cs` | `ComplexShapedItemExampleSO` | ScriptableObject definition for a non-rectangular footprint through a bool occupied-cell mask. |
| `Examples/Demo6 Shaped Items/Adapters/ShapedItemAdapter.cs` | `ShapedItemAdapter` | Adapter that implements `IItemPlacementShapeProvider` and exposes a `ComplexPlacementShape`. |
| `Examples/Demo6 Shaped Items/DataBindings/ShapedItemsInventoryDataBinding.cs` | `ShapedPlacementSeed`, `ShapedItemsInventoryDataBinding` | Placement-aware binding that stores item, anchor index, and orientation. |
| `Examples/Demo6 Shaped Items/Editor/ComplexShapedItemExampleSOEditor.cs` | `ComplexShapedItemExampleSOEditor` | Custom inspector with a clickable grid for editing a complex footprint over the icon sprite. |

---

## What to open first for common tasks

| If you need to... | Open these files first |
|---|---|
| Bind your own list-based inventory data | `InventoryDataBindingBase.cs`, `ListInventoryDataBinding.cs`, one demo binding from Demo1 or Demo4 |
| Build fixed equipment slots | `MappedSlotInventoryDataBinding.cs`, `EquipmentInventoryDataBinding.cs` |
| Understand drag/drop transfer | `InventoryDropProcessor.cs`, `InventoryTransferEngine.cs`, `IStrategy.cs`, `PlacementCandidate.cs` |
| Add custom drag rules | `IDragRule.cs`, `BuiltInRules.cs`, `CompositeRule.cs` |
| Support quick transfer | `AutoTransferService.cs`, `AutoTransferAction.cs`, `AutoTransferAnimationStrategy.cs` |
| Add custom input bindings | `InteractionBindingsProfile.cs`, `InputEventRouter.cs`, `SlotInteractionActions.cs` |
| Add context menu actions | `IContextMenuEntry.cs`, `ContextMenuEntryDefinitionSO.cs`, `ContextMenuManager.cs` |
| Add tooltip content | `IDescribable.cs`, `TooltipManager.cs`, `DefaultTooltipView.cs` |
| Understand world drop integration | `DropAreaBase.cs`, `InventoryDropArea.cs`, `WorldDropZone.cs` |
| Implement item conversion across inventories | `IItemAdapterConverter.cs`, `TransferItemConversionUtility.cs`, Demo4 converters |
| Build multi-cell items in a grid inventory | `PlacementInventoryDataBinding.cs`, `IPlacementInventory.cs`, `ShapedItemAdapter.cs`, Demo6 Shaped Items |
