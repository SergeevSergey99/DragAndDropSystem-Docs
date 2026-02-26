# Data Flow - Operation Diagrams

Detailed flow diagrams for all operations.

## Manual Drag & Drop

```
1. USER CLICK
   DragDropEventListener.OnBeginDrag()
   ↓
2. START DRAG
   DragAndDropManager.StartDrag(sourceSlot)
   ├─ Create DragContext
   ├─ Validate: GlobalRules.ValidateStartDrag()
   ├─ Validate: InventoryRules.ValidateStartDrag()
   ├─ Event: OnDragStarting (cancelable)
   ├─ Create visual
   └─ Event: OnDragStarted
   ↓
3. HOVER TARGET
   DragDropEventListener.OnPointerEnter()
   ├─ PushDropTarget(dropTarget)
   ├─ SetHoveredSlot(slot, inventory)
   ├─ Update DragContext.TargetSlot/TargetInventory
   ├─ Validate: GlobalRules.ValidateDrop()
   ├─ Validate: InventoryRules.ValidateDrop()
   ├─ Validate: SlotRules.ValidateDrop()
   └─ If valid → Highlight
   ↓
4. DROP
   DragAndDropManager.CompleteDrag()
   ├─ Event: OnDropAttempting (cancelable)
   ├─ Create InventoryTransferRequest
   ├─ _transferService.TryExecuteTransfer(request, out result)
   │   ├─ Capture snapshots
   │   ├─ Remove from source
   │   ├─ Try add to target
   │   └─ Rollback if failed
   ├─ DispatchTransferEvents(result)
   ├─ Event: OnDropCompleted
   └─ EndDrag()
```

---

## Auto-Transfer Flow

```
1. TRIGGER
   User hotkey OR quick click
   ↓
   AutoTransferAction.Execute(inventory, slot)
   ↓
2. VALIDATION
   DragAndDropManager.TryAutoTransfer(...)
   ├─ Check: IsDragging && CurrentContext.SourceSlot == slot? → BLOCK
   ├─ Create DragContext
   ├─ Event: OnAutoTransferAttempting (cancelable)
   ├─ Validate: GlobalRules + InventoryRules + SlotRules
   └─ FindValidAutoTransferSlot(targetInventory, stack)
   ↓
3. TRANSFER
   _transferService.TryExecuteTransfer(request, out result)
   ↓
4. ANIMATION (if strategy exists)
   ├─ Hide targetSlot icon
   ├─ Animate visual from source to target
   └─ On complete:
       ├─ Show targetSlot icon
       ├─ Event: OnDropCompleted
       └─ Event: OnAutoTransferCompleted
```

---

## Swap Operation

```
1. VALIDATION
   ValidateSwap(dragContext, targetSlot, out reverseContext)
   ├─ Check: Can drag from targetSlot?
   ├─ Check: Can drop target item to source?
   └─ Check: Can drop source item to target?
   ↓
2. EVENT
   OnSwapAttempting.Invoke(swapEventArgs)
   ├─ Cancel? → return false
   ↓
3. EXECUTE
   targetInventory.TrySwapSlots(targetSlot, sourceSlot, out swapResult)
   ├─ Backup stacks
   ├─ Swap: targetSlot.SetStack(sourceStack)
   ├─ Swap: sourceSlot.SetStack(targetStack)
   └─ UpdateVisuals()
   ↓
4. EVENTS
   DispatchSwapEvents(swapResult)
   ├─ OnItemRemoved for both slots
   └─ OnItemAdded for both slots
```

---

## Transaction Pipeline (InventoryTransferService)

```
TryExecuteTransfer(request, out result)
    ↓
1. PREPARE
   ├─ Extract source/target from request
   ├─ Determine transfer amount
   └─ Create operation context
    ↓
2. SNAPSHOT
   ├─ sourceSnapshot = sourceInventory.CaptureSnapshot()
   └─ targetSnapshot = targetInventory.CaptureSnapshot()
    ↓
3. REMOVE FROM SOURCE
   ├─ removed = sourceSlot.Stack.RemoveFromStack(amount)
   └─ If sourceSlot.IsEmpty → sourceInventory.HandleSlotEmptied()
    ↓
4. ADD TO TARGET
   ├─ TryAddToTargetInventory(...)
   │   ├─ If targetSlot specified → TryAdd to that slot
   │   └─ Else → TryAddItem (find any slot)
   └─ Success?
    ↓
5A. SUCCESS PATH
   ├─ Build InventoryTransferResult
   └─ return true
    ↓
5B. FAILURE PATH
   ├─ RestoreSnapshot(sourceSnapshot)
   ├─ RestoreSnapshot(targetSnapshot)
   └─ return false
```

---

**[Back to SKILL.md](./SKILL.md)**
