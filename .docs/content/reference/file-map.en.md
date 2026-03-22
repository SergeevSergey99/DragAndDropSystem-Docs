# File Map

This page is for users who already understand the system and want to jump to the right extension point in code.

The first section lists files most asset users actually need.
Internal helper files are grouped separately.

---

## Start here

| File | Use it for |
|---|---|
| `Scripts/Inventories/UniversalInventory.cs` | configuring and understanding the inventory component |
| `Scripts/DataBinding/InventoryDataBindingBase.cs` | base binding lifecycle |
| `Scripts/DataBinding/ListInventoryDataBinding.cs` | list-based inventories |
| `Scripts/DataBinding/MappedSlotInventoryDataBinding.cs` | fixed named slots |
| `Scripts/Inventories/Strategies/InventoryStrategyBase.cs` | custom placement behavior |
| `Scripts/Rules/IDragRule.cs` | `IDragRule` interface and the `DragRuleBase` base class |
| `Scripts/Inventories/ITransferDomainHandler.cs` | pre-commit and post-success hooks |
| `Scripts/Interaction/InputEventRouter.cs` | custom input bindings |

---

## If you are extending the transfer pipeline

| File | Role |
|---|---|
| `Scripts/Inventories/TransferPlanner.cs` | planning without mutating state |
| `Scripts/Inventories/TransferPlanExecutor.cs` | commit, rollback, deferred events |
| `Scripts/Inventories/InventoryDropProcessor.cs` | UI entry point into the pipeline |
| `Scripts/Inventories/TransferItemConversionUtility.cs` | target-side conversion helpers |

---

## Internal implementation details

These files are usually unnecessary unless you are modifying the asset itself:

| File | Role |
|---|---|
| `Scripts/Inventories/InventoryTransferService.cs` | low-level transfer helper |
| `Scripts/Inventories/EntryPlanningOperation.cs` | planning operation object |
| `Scripts/Inventories/TargetPlacementOperation.cs` | placement operation object |
| `Scripts/Inventories/AlternativeSlotSearchOperation.cs` | alternative slot search |
| `Scripts/Inventories/VirtualSlotState.cs` | virtual slot state for planning |
| `Scripts/Inventories/SlotOperationContext.cs` | low-level slot operation context |
