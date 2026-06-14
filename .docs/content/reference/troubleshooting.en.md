# Troubleshooting

This page is organized by symptom.

Format:

- what you see
- what it usually means
- where to look in code and setup

---

## `Wrong item type`

This usually means a binding received an adapter from the wrong inventory boundary.

Typical causes:

- target-side conversion did not happen
- swap was committed as a raw exchange
- after a previous operation, the slot still stores a foreign adapter type

Where to look:

- `CreateItemConverter()` in the binding
- `TransferItemConversionUtility`
- `MappedSlotInventoryDataBinding.CanDrop()`
- `MappedSlotInventoryDataBinding.CanStartDrag()`

---

## Warnings for other slots while dropping into one slot

This usually means the system fell into inventory-wide search when you expected a direct slot path.

Typical causes:

- the operation used `FindAlternative`
- area-drop was activated instead of slot-drop
- the transfer engine re-evaluated `GetAcceptableCount()`
- the transfer engine did not receive a concrete target hint

Where to look:

- `DropPolicySettings`
- the resolved `BlockedTargetResolutionKind`
- `InventoryDropProcessor`
- `InventoryTransferService`
- `GetAcceptableCount` logs

---

## Preview passes, but commit fails

Usually the problem is not in rules, but in execution or domain hooks.

Typical causes:

- `CanStartTransfer` / `CanCommitTransfer` veto
- `CanStartTransferAsync` veto
- conversion failed during execution
- placement failed after split

Where to look:

- `ITransferDomainHandler`
- `IAsyncTransferDomainHandler`
- `InventoryTransferService`

---

## First swap works, second swap breaks

This almost always means the slot stores the wrong adapter type after the first swap.

Typical causes:

- swap was a raw exchange
- conversion was applied in preview but not in commit
- add/remove events synchronized one format while the slot physically stores another

Where to look:

- swap execution path
- `ValidateSwapRules`
- conversion in both directions

---

## Drag does not start at all

Typical causes:

- source slot is empty
- `CanStartDrag` returned failure
- the slot stores the wrong adapter type
- the binding did not load data into UI

Where to look:

- `OnDragAttempting`
- `ValidateStartDrag`
- `ReloadUI()`
- `GetItems()`

---

## Data did not sync after a successful transfer

Typical causes:

- execution never reached deferred events
- the binding is attached to the wrong inventory
- `AddToData` / `RemoveFromData` work against the wrong backing source

Where to look:

- `DispatchTransferEvents`
- `DispatchSwapEvents`
- `OnItemAdded` / `OnItemRemoved`
- the concrete binding

---

## A stack behaves like one repeated item, but instances should be different

Typical causes:

- one adapter instance is reused as a representative for the whole stack
- conversion does not preserve instance state
- `ItemId` does not match real stacking semantics

Where to look:

- adapter implementation
- conversion cookbook
- `ItemId`

---

## `CanDrop` is called many times

This can be normal when:

- preview candidate search is running
- `FindAlternative` is active
- area-drop is active
- swap validates both directions

This is not normal when:

- you are doing a direct slot drop into a concrete slot without `FindAlternative`
- and logs still show neighboring slots being checked

In that case, look for a route/policy problem.

---

## Where to start debugging

1. Identify the phase: drag start, preview, domain validation, execution, or swap.
2. Look at the first meaningful log in the stack, not the last one.
3. Check whether a concrete `targetSlot` exists.
4. Check which adapter type is physically stored in the slot after the operation.

---

## Related pages

- [Logs and Debugging](logs-and-debugging.md)
- [Transfer Pipeline](../architecture/transfer-pipeline.md)
- [Cookbook: Item Conversion](../architecture/item-conversion-cookbook.md)
- [Drop Policy Matrix](../architecture/drop-policy-matrix.md)
