# Events Reference

Global drag / drop / auto-transfer / swap events live in `UDNDEvents`. Content-change events for a specific inventory live in `UniversalInventory`.

## Short Version

| If you need to... | Listen to |
|---|---|
| Know when the player starts or ends a drag | `UDNDEvents.OnDragStarted`, `UDNDEvents.OnDragEnded` |
| React to a successful drop | `UDNDEvents.OnDropCompleted` |
| Show a cancellation reason | `UDNDEvents.OnDragCancelled` |
| Track quick-click transfer | `UDNDEvents.OnAutoTransferCompleted`, `UDNDEvents.OnAutoTransferFailed` |
| Track inventory content changes | `UniversalInventory.OnItemAdded`, `UniversalInventory.OnItemRemoved` |
| Block swap before it runs | `UDNDEvents.OnSwapAttempting` |

## Normal Drag / Drop

Typical successful order:

1. `OnDragAttempting`
2. `OnDragStarted`
3. `OnDragEnterSlot` / `OnDragExitSlot` while hovering
4. `OnDropAttempting`
5. `OnDropCompleted`
6. `OnDragEnded`

If drag or drop is cancelled, `OnDragCancelled` runs instead of `OnDropCompleted`, and `OnDragEnded` still runs after it.

`OnDragEnded` is the safest place for cleaning up temporary UI because it runs at the end of both successful and failed flows.

## Global Events (`UDNDEvents`)

These are `static` events. Subscribe with `UDNDEvents.OnX += handler`; unsubscribe in `OnDisable` or another teardown method.

`DragAndDropManager` clears subscribers from the previous Play session at the start of a new Play session. This matters for Fast Enter Play Mode.

### Drag

| Event | When it runs | Note |
|---|---|---|
| `OnDragAttempting` | Before drag starts | Rules can cancel the operation |
| `OnDragStarted` | Drag is confirmed | `DragContext` is available |
| `OnDragEnterSlot` | Pointer enters a slot | Useful for hover UI |
| `OnDragExitSlot` | Pointer leaves a slot | Useful for clearing hover UI |
| `OnDropAttempting` | Before drop processing | Target is already known |
| `OnDropCompleted` | Drop succeeded | Changes are already applied |
| `OnDragCancelled` | Drag or drop was cancelled | Use for error messages or UI cleanup |
| `OnDragStackChanged` | Drag stack amount changed | For example after split drop |
| `OnDragEnded` | Drag cycle ended | Always runs at the end |

### Auto-Transfer

| Event | When it runs |
|---|---|
| `OnAutoTransferAttempting` | Before quick-click transfer |
| `OnAutoTransferCompleted` | Quick-click transfer succeeded |
| `OnAutoTransferFailed` | Quick-click transfer failed |

### Swap

| Event | When it runs | Note |
|---|---|---|
| `OnSwapAttempting` | Before swap | Set `Cancel = true` to block swap |
| `OnSwapCompleted` | Swap succeeded | Slots already contain the resulting stacks |

## Inventory Events

`UniversalInventory` sends events when its content actually changed.

| Event | When it runs | Context |
|---|---|---|
| `OnItemAdded` | Item was added to the inventory | `InventoryItemEventContext` |
| `OnItemRemoved` | Item was removed from the inventory | `InventoryItemEventContext` |

### `InventoryItemEventContext` Fields

| Field | Type | Description |
|---|---|---|
| `Stack` | `ItemStack` | Stack that was added or removed |
| `SlotIndex` | `int` | Slot index |
| `SourceInventory` | `IInventory` | Where the item came from, if known |
| `TargetInventory` | `IInventory` | Where the item went, if known |
| `SourceSlot` | `BaseSlot` | Source slot, if known |
| `TargetSlot` | `BaseSlot` | Target slot, if known |

In practice:

- `context.Stack.PrimaryAdapter` gives the main item adapter
- `context.Stack.Count` gives the amount
- for transfers between different inventory types, remove usually contains the item before conversion, and add contains the item after conversion

## Swap Context

### `InventorySwapContext` Fields

| Field | Type | Description |
|---|---|---|
| `SourceStack` | `ItemStack` | Stack in the source slot before swap |
| `TargetStack` | `ItemStack` | Stack in the target slot before swap |
| `SourceSlot` | `BaseSlot` | Slot where dragging started |
| `TargetSlot` | `BaseSlot` | Slot where the item was dropped |
| `SourceInventory` | `IInventory` | Source inventory |
| `TargetInventory` | `IInventory` | Target inventory |
| `Cancel` | `bool` | Set `true` to cancel swap |

For swap between different inventory types, the final adapter objects in slots may differ from the ones in `SourceStack` and `TargetStack` before execution. That is expected: items are converted for the target inventory.

## When Events Are Sent

Events are sent only after the inventory was changed successfully.

If transfer validation fails, there is no space, or the operation is cancelled, add/remove events are not sent. Subscribers do not see temporary state that the system later rolled back.

For group transfer, each item is processed separately:

- a successful item sends its own events
- a failed item stays where it was and sends no events
- failure of one item does not cancel events from items that were already transferred successfully

See also:

- [Transfer Pipeline](../architecture/transfer-pipeline.md)
- [Logs And Debugging](logs-and-debugging.md)
