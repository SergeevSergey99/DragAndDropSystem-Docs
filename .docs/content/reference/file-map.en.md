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
| `Scripts/Inventories/IAsyncTransferDomainHandler.cs` | async pre-commit validation for servers, files, and external sources |
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

## If you are working with the examples

| File | Role |
|---|---|
| `Examples/Demo4 Trading/DataBindings/*.cs` | trading bindings |
| `Examples/Demo4 Trading/Converters/*.cs` | trading item converters |
| `Examples/Demo4 Trading/DataBindings/TradingHelper.cs` | trading checks and side effects |
| `Examples/Demo2 Loot/Scripts/*.cs` | world loot example |

---

## Internal implementation details

These files are usually unnecessary unless you are modifying the asset itself:

| File | Role |
|---|---|
| `Scripts/Inventories/InventoryTransferService.cs` | `InventoryTransferRequest` / `InventoryTransferResult` models |
| `Scripts/Inventories/EntryPlanningOperation.cs` | planning operation object |
| `Scripts/Inventories/TargetPlacementOperation.cs` | placement operation object |
| `Scripts/Inventories/SlotRelocationService.cs` | relocation-based fallback that tries to free a suitable slot |
| `Scripts/Inventories/VirtualSlotState.cs` | virtual slot state for planning |
| `Scripts/Inventories/SlotOperationContext.cs` | low-level slot operation context |
